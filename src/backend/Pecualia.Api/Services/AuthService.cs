using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pecualia.Api.Configuration;
using Pecualia.Api.Contracts.Auth;
using Pecualia.Api.Data;
using Pecualia.Api.Infrastructure.Email;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterManagerAsync(RegisterManagerRequest request, CancellationToken cancellationToken);

    Task<AuthResponse> RegisterFarmerAsync(RegisterFarmerRequest request, CancellationToken cancellationToken);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<ActivationResponse> ActivateAccountAsync(ActivateAccountRequest request, CancellationToken cancellationToken);

    Task<ActivationResponse> ResendActivationAsync(ResendActivationRequest request, CancellationToken cancellationToken);

    Task<UserProfileResponse?> GetCurrentUserAsync(long userId, CancellationToken cancellationToken);

    Task<UserProfileResponse> UpdateCurrentUserSettingsAsync(long userId, UpdateUserSettingsRequest request, CancellationToken cancellationToken);
}

public sealed class AuthService(
    PecualiaDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IAccountActivationService accountActivationService,
    IEmailSender emailSender,
    IClock clock,
    IOptions<ActivationOptions> activationOptions)
    : IAuthService
{
    private readonly ActivationOptions _activationOptions = activationOptions.Value;

    public async Task<AuthResponse> RegisterManagerAsync(RegisterManagerRequest request, CancellationToken cancellationToken)
    {
        await EnsureUniqueIdentityAsync(request.Email, request.Username, cancellationToken);

        var now = clock.UtcNow;
        var user = new AppUser
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            Name = request.Name.Trim(),
            Surname = request.Surname.Trim(),
            Username = request.Username.Trim(),
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = UserRole.Manager,
            EmailVerifiedAt = now,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var manager = new Manager
        {
            User = user,
            OrganizationName = request.OrganizationName.Trim(),
            ProfessionalIdentifier = request.ProfessionalIdentifier.Trim(),
            PhoneNumber = CleanOptional(request.PhoneNumber),
            Province = CleanOptional(request.Province),
            Town = CleanOptional(request.Town),
            InvitationCode = CreateInvitationCode()
        };

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var subscription = new Subscription
        {
            User = user,
            Autorenew = false,
            InitialDate = today,
            ExpirationDate = today.AddMonths(1),
            PlanType = PlanType.Basic,
            State = SubscriptionState.Active
        };

        dbContext.Users.Add(user);
        dbContext.Managers.Add(manager);
        dbContext.Subscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponse(jwtTokenService.CreateToken(user), MapProfile(user));
    }

    public async Task<AuthResponse> RegisterFarmerAsync(RegisterFarmerRequest request, CancellationToken cancellationToken)
    {
        await EnsureUniqueIdentityAsync(request.Email, request.Username, cancellationToken);

        if (!DomainValidators.IsValidTaxIdentifier(request.PersonType, request.NifCif))
        {
            throw new DomainException(request.PersonType == PersonType.Company
                ? "El NIF de la persona jurídica no es válido."
                : "El DNI/NIF de la persona física no es válido.");
        }

        var manager = await ResolveManagerAsync(request.ManagerInvitationCode, request.ManagerEmail, cancellationToken);
        if (manager is not null)
        {
            await EnsureManagedFarmerPlanCapacityAsync(manager.UserId, cancellationToken);
        }
        var now = clock.UtcNow;
        var user = new AppUser
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            Name = request.Name.Trim(),
            Surname = request.Surname.Trim(),
            Username = request.Username.Trim(),
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = UserRole.Farmer,
            EmailVerifiedAt = now,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var farmer = new Farmer
        {
            User = user,
            ManagerId = manager?.UserId,
            NifCif = DomainValidators.NormalizeTaxIdentifier(request.NifCif),
            PhoneNumber = CleanOptional(request.PhoneNumber),
            Residence = CleanOptional(request.Residence),
            Town = CleanOptional(request.Town),
            Province = CleanOptional(request.Province),
            ZipCode = CleanOptional(request.ZipCode),
            PersonType = request.PersonType,
            BirthDate = request.BirthDate,
            Status = FarmerStatus.Active
        };

        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponse(jwtTokenService.CreateToken(user), MapProfile(user));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .Include(entity => entity.Manager)
            .Include(entity => entity.Farmer)
            .Include(entity => entity.Subscription)
            .SingleOrDefaultAsync(entity =>
                (entity.Email != null && entity.Email == identifier) ||
                (entity.Username != null && entity.Username.ToLower() == identifier),
                cancellationToken);

        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash) || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new DomainException("Credenciales inválidas.");
        }

        if (!user.IsActive)
        {
            throw new DomainException("La cuenta aún no está activa. Revisa el correo de activación.");
        }

        return new AuthResponse(jwtTokenService.CreateToken(user), MapProfile(user));
    }

    public async Task<ActivationResponse> ActivateAccountAsync(ActivateAccountRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = ComputeTokenHash(request.Token);
        var token = await dbContext.AccountActivationTokens
            .Include(entity => entity.User)
            .ThenInclude(entity => entity!.Farmer)
            .SingleOrDefaultAsync(entity => entity.TokenHash == tokenHash, cancellationToken);

        if (token is null || token.UsedAt.HasValue || token.ExpiresAt < clock.UtcNow)
        {
            throw new DomainException("El enlace de activación no es válido o ha caducado.");
        }

        var username = request.Username.Trim();
        var usernameTaken = await dbContext.Users.AnyAsync(entity => entity.Username == username && entity.Id != token.UserId, cancellationToken);
        if (usernameTaken)
        {
            throw new DomainException("El nombre de usuario ya está en uso.");
        }

        token.User.Username = username;
        token.User.PasswordHash = passwordHasher.Hash(request.Password);
        token.User.EmailVerifiedAt = clock.UtcNow;
        token.User.IsActive = true;
        token.User.UpdatedAt = clock.UtcNow;

        if (token.User.Farmer is not null)
        {
            token.User.Farmer.Status = FarmerStatus.Active;
        }

        token.UsedAt = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ActivationResponse("Cuenta activada correctamente. Ya puedes iniciar sesión.", null);
    }

    public async Task<ActivationResponse> ResendActivationAsync(ResendActivationRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .Include(entity => entity.Farmer)
            .SingleOrDefaultAsync(entity => entity.Email == email, cancellationToken);

        if (user is null || user.Role != UserRole.Farmer || user.IsActive)
        {
            return new ActivationResponse("Si existe una cuenta pendiente, se ha enviado una nueva invitación.", null);
        }

        var activationUrl = await CreateActivationAsync(user, null, cancellationToken);
        return new ActivationResponse("Se ha reenviado la invitación.", activationUrl);
    }

    public async Task<UserProfileResponse?> GetCurrentUserAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(entity => entity.Manager)
            .Include(entity => entity.Farmer)
            .Include(entity => entity.Subscription)
            .SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);

        return user is null ? null : MapProfile(user);
    }

    public async Task<UserProfileResponse> UpdateCurrentUserSettingsAsync(long userId, UpdateUserSettingsRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(entity => entity.Manager)
            .Include(entity => entity.Farmer)
            .Include(entity => entity.Subscription)
            .SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new DomainException("Usuario no encontrado.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedUsername = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();
        var normalizedName = request.Name.Trim();
        var normalizedSurname = request.Surname.Trim();
        var normalizedOrganizationName = string.IsNullOrWhiteSpace(request.OrganizationName) ? null : request.OrganizationName.Trim();
        var normalizedCurrentPassword = string.IsNullOrWhiteSpace(request.CurrentPassword) ? null : request.CurrentPassword.Trim();
        var normalizedNewPassword = string.IsNullOrWhiteSpace(request.NewPassword) ? null : request.NewPassword.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName) ||
            string.IsNullOrWhiteSpace(normalizedSurname) ||
            string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new DomainException("Nombre, apellidos y correo son obligatorios.");
        }

        if (await dbContext.Users.AnyAsync(entity => entity.Email == normalizedEmail && entity.Id != userId, cancellationToken))
        {
            throw new DomainException("El correo ya está en uso.");
        }

        if (normalizedUsername is not null &&
            await dbContext.Users.AnyAsync(entity => entity.Username == normalizedUsername && entity.Id != userId, cancellationToken))
        {
            throw new DomainException("El nombre de usuario ya está en uso.");
        }

        if (user.Role == UserRole.Manager && string.IsNullOrWhiteSpace(normalizedOrganizationName))
        {
            throw new DomainException("La organización es obligatoria para cuentas Gestor@s.");
        }

        if (normalizedNewPassword is not null)
        {
            if (string.IsNullOrWhiteSpace(normalizedCurrentPassword))
            {
                throw new DomainException("Debes indicar la contraseña actual para establecer una nueva.");
            }

            if (string.IsNullOrWhiteSpace(user.PasswordHash) || !passwordHasher.Verify(normalizedCurrentPassword, user.PasswordHash))
            {
                throw new DomainException("La contraseña actual no es válida.");
            }

            if (normalizedNewPassword.Length < 8)
            {
                throw new DomainException("La nueva contraseña debe tener al menos 8 caracteres.");
            }

            user.PasswordHash = passwordHasher.Hash(normalizedNewPassword);
        }

        user.Name = normalizedName;
        user.Surname = normalizedSurname;
        user.Email = normalizedEmail;
        user.Username = normalizedUsername;
        user.UpdatedAt = clock.UtcNow;

        if (user.Manager is not null)
        {
            user.Manager.OrganizationName = normalizedOrganizationName ?? user.Manager.OrganizationName;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapProfile(user);
    }

    public async Task<string> CreateActivationAsync(AppUser user, long? createdByUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new DomainException("No se puede enviar la activación sin un correo electrónico.");
        }

        var recipientEmail = user.Email;

        var (plainToken, tokenHash) = accountActivationService.GenerateTokenPair();
        var token = new AccountActivationToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedByUserId = createdByUserId,
            CreatedAt = clock.UtcNow,
            ExpiresAt = clock.UtcNow.AddHours(_activationOptions.TokenHours)
        };

        dbContext.AccountActivationTokens.Add(token);
        await dbContext.SaveChangesAsync(cancellationToken);

        var activationUrl = $"{_activationOptions.BaseUrl}?token={Uri.EscapeDataString(plainToken)}";
        var htmlBody = BuildActivationEmailHtml(user.Name, activationUrl);
        var plainTextBody =
            $"""
            Hola {user.Name},

            Tu acceso a Pecualia ya está preparado.

            Activa tu cuenta desde este enlace:
            {activationUrl}

            Este enlace caduca en {_activationOptions.TokenHours} horas.

            Si no esperabas este correo, puedes ignorarlo.
            """;

        await emailSender.SendAsync(
            new EmailMessage(
                recipientEmail,
                "Activa tu cuenta de Pecualia",
                htmlBody,
                plainTextBody),
            cancellationToken);

        return activationUrl;
    }

    private string BuildActivationEmailHtml(string userName, string activationUrl)
    {
        var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(userName) ? "ganadero" : userName.Trim());
        var safeUrl = WebUtility.HtmlEncode(activationUrl);

        return
            $$"""
            <!DOCTYPE html>
            <html lang="es">
              <body style="margin:0;padding:0;background-color:#f6f8f5;font-family:Inter,Segoe UI,Arial,sans-serif;color:#1e2a24;">
                <div style="display:none;max-height:0;overflow:hidden;opacity:0;">
                  Activa tu cuenta de Pecualia y termina la configuración de tu acceso.
                </div>
                <table role="presentation" cellpadding="0" cellspacing="0" width="100%" style="background-color:#f6f8f5;padding:32px 16px;">
                  <tr>
                    <td align="center">
                      <table role="presentation" cellpadding="0" cellspacing="0" width="100%" style="max-width:640px;">
                        <tr>
                          <td style="padding-bottom:18px;">
                            <table role="presentation" cellpadding="0" cellspacing="0">
                              <tr>
                                <td style="width:40px;height:40px;border-radius:12px;background-color:#e7b84c;color:#214d39;font-size:22px;font-weight:800;text-align:center;">
                                  P
                                </td>
                                <td style="padding-left:12px;">
                                  <div style="font-size:22px;line-height:1.1;font-weight:800;color:#214d39;">Pecualia</div>
                                  <div style="font-size:13px;line-height:1.5;color:#637168;">Gestión ganadera digital, clara y siempre al día.</div>
                                </td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                        <tr>
                          <td style="border-radius:24px;overflow:hidden;background-color:#ffffff;border:1px solid #d7ded8;box-shadow:0 18px 48px rgba(33,77,57,0.08);">
                            <div style="padding:40px;background:linear-gradient(180deg,#214d39 0%,#2f6b4f 100%);">
                              <div style="display:inline-block;padding:7px 12px;border-radius:999px;background-color:rgba(255,255,255,0.14);border:1px solid rgba(255,255,255,0.18);font-size:12px;font-weight:700;letter-spacing:0.04em;text-transform:uppercase;color:#ddebdf;">
                                Invitación a Pecualia
                              </div>
                              <h1 style="margin:20px 0 12px;font-size:32px;line-height:1.15;color:#ffffff;">Activa tu acceso</h1>
                              <p style="margin:0;font-size:16px;line-height:1.7;color:#ddebdf;">
                                Tu cuenta ya está preparada para acceder a la plataforma y gestionar tu actividad ganadera.
                              </p>
                            </div>
                            <div style="padding:36px 40px 40px;">
                              <p style="margin:0 0 14px;font-size:16px;line-height:1.7;color:#1e2a24;">Hola {{safeName}},</p>
                              <p style="margin:0 0 16px;font-size:16px;line-height:1.7;color:#405048;">
                                Hemos creado tu acceso en <strong style="color:#214d39;">Pecualia</strong>. Para terminar la configuración de la cuenta, activa el acceso desde el botón inferior.
                              </p>
                              <table role="presentation" cellpadding="0" cellspacing="0" style="margin:28px 0 24px;">
                                <tr>
                                  <td align="center" style="border-radius:12px;background-color:#2f6b4f;">
                                    <a href="{{safeUrl}}" style="display:inline-block;padding:14px 24px;font-size:15px;font-weight:700;color:#ffffff;text-decoration:none;">
                                      Activar cuenta
                                    </a>
                                  </td>
                                </tr>
                              </table>
                              <div style="padding:16px 18px;border-radius:16px;background-color:#f6f8f5;border:1px solid #e3e9e4;">
                                <p style="margin:0 0 8px;font-size:14px;font-weight:700;color:#214d39;">Detalles importantes</p>
                                <p style="margin:0;font-size:14px;line-height:1.7;color:#637168;">
                                  Este enlace caduca en <strong>{{_activationOptions.TokenHours}} horas</strong>. Si el botón no funciona, copia y pega esta URL en tu navegador:
                                </p>
                                <p style="margin:12px 0 0;font-size:13px;line-height:1.7;word-break:break-all;color:#2f6b4f;">
                                  <a href="{{safeUrl}}" style="color:#2f6b4f;text-decoration:underline;">{{safeUrl}}</a>
                                </p>
                              </div>
                              <p style="margin:24px 0 0;font-size:13px;line-height:1.7;color:#7a877f;">
                                Si no esperabas este correo, puedes ignorarlo sin hacer ninguna acción.
                              </p>
                            </div>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                </table>
              </body>
            </html>
            """;
    }

    private async Task EnsureUniqueIdentityAsync(string email, string username, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedUsername = username.Trim();

        if (await dbContext.Users.AnyAsync(entity => entity.Email == normalizedEmail, cancellationToken))
        {
            throw new DomainException("Ya existe una cuenta con ese correo electrónico.");
        }

        if (await dbContext.Users.AnyAsync(entity => entity.Username == normalizedUsername, cancellationToken))
        {
            throw new DomainException("Ya existe una cuenta con ese nombre de usuario.");
        }
    }

    private async Task<Manager?> ResolveManagerAsync(string? invitationCode, string? managerEmail, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(invitationCode))
        {
            return await dbContext.Managers.SingleOrDefaultAsync(
                entity => entity.InvitationCode == invitationCode.Trim().ToUpperInvariant(),
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(managerEmail))
        {
            var normalizedEmail = managerEmail.Trim().ToLowerInvariant();
            return await dbContext.Managers.Include(entity => entity.User)
                .SingleOrDefaultAsync(entity => entity.User.Email == normalizedEmail, cancellationToken);
        }

        return null;
    }

    private async Task EnsureManagedFarmerPlanCapacityAsync(long managerUserId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var subscription = await dbContext.Subscriptions
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.UserId == managerUserId, cancellationToken);

        var planType = SubscriptionPlanSupport.ResolveEffectivePlanType(subscription, today);
        var farmerLimit = SubscriptionPlanSupport.GetManagedFarmerLimit(planType);
        if (farmerLimit is null)
        {
            return;
        }

        var currentFarmerCount = await dbContext.Farmers.CountAsync(entity => entity.ManagerId == managerUserId, cancellationToken);
        if (currentFarmerCount >= farmerLimit.Value)
        {
            throw new DomainException(SubscriptionPlanSupport.BuildManagedFarmerLimitError(planType, farmerLimit.Value));
        }
    }

    private static UserProfileResponse MapProfile(AppUser user)
    {
        return new UserProfileResponse(
            user.Id,
            user.Email,
            user.Username,
            user.Name,
            user.Surname,
            user.Role.ToString(),
            user.IsActive,
            user.Manager?.OrganizationName,
            user.Farmer?.Status.ToString(),
            user.Subscription?.PlanType.ToString(),
            user.Subscription?.State.ToString(),
            user.Subscription?.Autorenew,
            user.Subscription?.InitialDate,
            user.Subscription?.ExpirationDate);
    }

    private static string CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string CreateInvitationCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = Random.Shared.GetItems(chars.ToCharArray(), 8);
        return new string(bytes);
    }

    private static string ComputeTokenHash(string plainToken)
    {
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plainToken)));
    }
}
