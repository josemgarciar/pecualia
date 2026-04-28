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
            PlanType = request.PlanType,
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

        var manager = await ResolveManagerAsync(request.ManagerInvitationCode, request.ManagerEmail, cancellationToken);
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
            NifCif = request.NifCif.Trim().ToUpperInvariant(),
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
            .SingleOrDefaultAsync(entity =>
                entity.Email == identifier ||
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
            .SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);

        return user is null ? null : MapProfile(user);
    }

    public async Task<string> CreateActivationAsync(AppUser user, long? createdByUserId, CancellationToken cancellationToken)
    {
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
        var plainTextBody =
            $"""
            Hola {user.Name},

            Tu cuenta de Pecualia ya está creada. Actívala desde este enlace:
            {activationUrl}

            Este enlace caduca en {_activationOptions.TokenHours} horas.
            """;

        await emailSender.SendAsync(
            new EmailMessage(
                user.Email,
                "Activa tu cuenta de Pecualia",
                $"<p>Hola {user.Name},</p><p>Tu cuenta de Pecualia ya está creada.</p><p><a href=\"{activationUrl}\">Activar cuenta</a></p><p>Este enlace caduca en {_activationOptions.TokenHours} horas.</p>",
                plainTextBody),
            cancellationToken);

        return activationUrl;
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
            user.Farmer?.Status.ToString());
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
