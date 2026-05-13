using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Data;

public sealed class PecualiaDbContext(DbContextOptions<PecualiaDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<Manager> Managers => Set<Manager>();

    public DbSet<Farmer> Farmers => Set<Farmer>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public DbSet<LivestockFarm> Farms => Set<LivestockFarm>();

    public DbSet<Animal> Animals => Set<Animal>();

    public DbSet<OvinoCaprinoAnimal> OvinoCaprinoAnimals => Set<OvinoCaprinoAnimal>();

    public DbSet<PorcinoAnimal> PorcinoAnimals => Set<PorcinoAnimal>();

    public DbSet<AnimalBirth> AnimalBirths => Set<AnimalBirth>();

    public DbSet<Vaccination> Vaccinations => Set<Vaccination>();

    public DbSet<MovementCertificate> MovementCertificates => Set<MovementCertificate>();

    public DbSet<MovementCertificateAnimal> MovementCertificateAnimals => Set<MovementCertificateAnimal>();

    public DbSet<Balance> Balances => Set<Balance>();

    public DbSet<BalanceOvinoCaprino> BalanceOvinoCaprino => Set<BalanceOvinoCaprino>();

    public DbSet<BalancePorcino> BalancePorcino => Set<BalancePorcino>();

    public DbSet<Census> Census => Set<Census>();

    public DbSet<CensusOvinoCaprino> CensusOvinoCaprino => Set<CensusOvinoCaprino>();

    public DbSet<CensusPorcino> CensusPorcino => Set<CensusPorcino>();

    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<Inspection> Inspections => Set<Inspection>();

    public DbSet<AccountActivationToken> AccountActivationTokens => Set<AccountActivationToken>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var appUser = modelBuilder.Entity<AppUser>();
        appUser.ToTable("app_user");
        appUser.HasKey(entity => entity.Id);
        appUser.Property(entity => entity.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        appUser.Property(entity => entity.Email).HasColumnName("email").HasMaxLength(255);
        appUser.Property(entity => entity.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        appUser.Property(entity => entity.Surname).HasColumnName("surname").HasMaxLength(180).IsRequired();
        appUser.Property(entity => entity.Username).HasColumnName("username").HasMaxLength(120);
        appUser.Property(entity => entity.PasswordHash).HasColumnName("password_hash");
        appUser.Property(entity => entity.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
        appUser.Property(entity => entity.EmailVerifiedAt).HasColumnName("email_verified_at");
        appUser.Property(entity => entity.IsActive).HasColumnName("is_active").IsRequired();
        appUser.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        appUser.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
        appUser.HasIndex(entity => entity.Email).IsUnique();
        appUser.HasIndex(entity => entity.Username).IsUnique();

        var manager = modelBuilder.Entity<Manager>();
        manager.ToTable("manager");
        manager.HasKey(entity => entity.UserId);
        manager.Property(entity => entity.UserId).HasColumnName("user_id");
        manager.Property(entity => entity.OrganizationName).HasColumnName("organization_name").HasMaxLength(180).IsRequired();
        manager.Property(entity => entity.ProfessionalIdentifier).HasColumnName("professional_identifier").HasMaxLength(32).IsRequired();
        manager.Property(entity => entity.PhoneNumber).HasColumnName("phone_number").HasMaxLength(32);
        manager.Property(entity => entity.Province).HasColumnName("province").HasMaxLength(120);
        manager.Property(entity => entity.Town).HasColumnName("town").HasMaxLength(120);
        manager.Property(entity => entity.InvitationCode).HasColumnName("invitation_code").HasMaxLength(32).IsRequired();
        manager.HasIndex(entity => entity.InvitationCode).IsUnique();
        manager.HasOne(entity => entity.User)
            .WithOne(entity => entity.Manager)
            .HasForeignKey<Manager>(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var farmer = modelBuilder.Entity<Farmer>();
        farmer.ToTable("farmer");
        farmer.HasKey(entity => entity.UserId);
        farmer.Property(entity => entity.UserId).HasColumnName("user_id");
        farmer.Property(entity => entity.ManagerId).HasColumnName("manager_id");
        farmer.Property(entity => entity.NifCif).HasColumnName("nif_cif").HasMaxLength(32).IsRequired();
        farmer.Property(entity => entity.SecondSurname).HasColumnName("second_surname").HasMaxLength(180);
        farmer.Property(entity => entity.CompanyName).HasColumnName("company_name").HasMaxLength(180);
        farmer.Property(entity => entity.LegalRepresentative).HasColumnName("legal_representative").HasMaxLength(180);
        farmer.Property(entity => entity.PhoneNumber).HasColumnName("phone_number").HasMaxLength(32);
        farmer.Property(entity => entity.Province).HasColumnName("province").HasMaxLength(120);
        farmer.Property(entity => entity.Residence).HasColumnName("residence").HasMaxLength(255);
        farmer.Property(entity => entity.Town).HasColumnName("town").HasMaxLength(120);
        farmer.Property(entity => entity.ZipCode).HasColumnName("zip_code").HasMaxLength(16);
        farmer.Property(entity => entity.PersonType).HasColumnName("person_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        farmer.Property(entity => entity.BirthDate).HasColumnName("birth_date");
        farmer.Property(entity => entity.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        farmer.HasIndex(entity => entity.NifCif).IsUnique();
        farmer.HasOne(entity => entity.User)
            .WithOne(entity => entity.Farmer)
            .HasForeignKey<Farmer>(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        farmer.HasOne(entity => entity.Manager)
            .WithMany(entity => entity.Farmers)
            .HasForeignKey(entity => entity.ManagerId)
            .OnDelete(DeleteBehavior.SetNull);

        var subscription = modelBuilder.Entity<Subscription>();
        subscription.ToTable("subscription");
        subscription.HasKey(entity => entity.Id);
        subscription.Property(entity => entity.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        subscription.Property(entity => entity.UserId).HasColumnName("user_id");
        subscription.Property(entity => entity.Autorenew).HasColumnName("autorenew");
        subscription.Property(entity => entity.ExpirationDate).HasColumnName("expiration_date");
        subscription.Property(entity => entity.InitialDate).HasColumnName("initial_date");
        subscription.Property(entity => entity.PlanType).HasColumnName("plan_type").HasConversion<string>().HasMaxLength(60).IsRequired();
        subscription.Property(entity => entity.State).HasColumnName("state").HasConversion<string>().HasMaxLength(40).IsRequired();
        subscription.Property(entity => entity.StripeCustomerId).HasColumnName("stripe_customer_id").HasMaxLength(64);
        subscription.Property(entity => entity.StripeSubscriptionId).HasColumnName("stripe_subscription_id").HasMaxLength(64);
        subscription.Property(entity => entity.StripePriceId).HasColumnName("stripe_price_id").HasMaxLength(64);
        subscription.HasOne(entity => entity.User)
            .WithOne(entity => entity.Subscription)
            .HasForeignKey<Subscription>(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var farm = modelBuilder.Entity<LivestockFarm>();
        farm.ToTable("livestock_farm");
        farm.HasKey(entity => entity.Id);
        farm.Property(entity => entity.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        farm.Property(entity => entity.FarmerId).HasColumnName("farmer_id");
        farm.Property(entity => entity.AuthorisedCapacity).HasColumnName("authorised_capacity");
        farm.Property(entity => entity.Address).HasColumnName("address").HasMaxLength(255);
        farm.Property(entity => entity.LivestockSpecies)
            .HasColumnName("livestock_species")
            .HasConversion(
                entity => entity.ToString().ToLowerInvariant(),
                value => Enum.Parse<LivestockSpecies>(value, true))
            .HasMaxLength(40)
            .IsRequired();
        farm.Property(entity => entity.LivestockType).HasColumnName("livestock_type").HasMaxLength(80);
        farm.Property(entity => entity.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
        farm.Property(entity => entity.PorcineFatteningCapacity).HasColumnName("porcine_fattening_capacity");
        farm.Property(entity => entity.PorcineMothersCapacity).HasColumnName("porcine_mothers_capacity");
        farm.Property(entity => entity.PorcineRegistryNumber).HasColumnName("porcine_registry_number").HasMaxLength(32);
        farm.Property(entity => entity.Province).HasColumnName("province").HasMaxLength(120);
        farm.Property(entity => entity.RegaCode).HasColumnName("rega_code").HasMaxLength(32).IsRequired();
        farm.Property(entity => entity.Regime)
            .HasColumnName("regime")
            .HasConversion(
                entity => entity == null ? null : entity.ToString()!.ToLowerInvariant(),
                value => string.IsNullOrWhiteSpace(value) ? null : Enum.Parse<FarmRegime>(value, true))
            .HasMaxLength(80);
        farm.Property(entity => entity.Responsible).HasColumnName("responsible").HasMaxLength(180);
        farm.Property(entity => entity.Spindle).HasColumnName("spindle");
        farm.Property(entity => entity.Town).HasColumnName("town").HasMaxLength(120);
        farm.Property(entity => entity.XCoordinate).HasColumnName("x_coordinate");
        farm.Property(entity => entity.YCoordinate).HasColumnName("y_coordinate");
        farm.Property(entity => entity.ZipCode).HasColumnName("zip_code").HasMaxLength(16);
        farm.Property(entity => entity.ZootechnicClassification).HasColumnName("zootechnic_classification").HasMaxLength(120);
        farm.HasIndex(entity => entity.RegaCode).IsUnique();
        farm.HasOne(entity => entity.Farmer)
            .WithMany(entity => entity.Farms)
            .HasForeignKey(entity => entity.FarmerId)
            .OnDelete(DeleteBehavior.Cascade);

        var animal = modelBuilder.Entity<Animal>();
        animal.ToTable("animal");
        animal.HasKey(entity => entity.Id);
        animal.Property(entity => entity.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        animal.Property(entity => entity.LivestockFarmId).HasColumnName("livestock_farm_id");
        animal.Property(entity => entity.BirthDate).HasColumnName("birth_date");
        animal.Property(entity => entity.BirthYear).HasColumnName("birth_year");
        animal.Property(entity => entity.Breed).HasColumnName("breed").HasMaxLength(80);
        animal.Property(entity => entity.DestinationCode).HasColumnName("destination_code").HasMaxLength(32);
        animal.Property(entity => entity.DischargeCause)
            .HasColumnName("discharge_cause")
            .HasConversion(
                entity => entity == null ? null : entity.ToString(),
                value => ParseAnimalDischargeCause(value))
            .HasMaxLength(80);
        animal.Property(entity => entity.Identification).HasColumnName("identification").HasMaxLength(80).IsRequired();
        animal.Property(entity => entity.OriginCode).HasColumnName("origin_code").HasMaxLength(32);
        animal.Property(entity => entity.RegistrationCause)
            .HasColumnName("registration_cause")
            .HasConversion(
                entity => entity == null ? null : entity.ToString(),
                value => ParseAnimalRegistrationCause(value))
            .HasMaxLength(80);
        animal.Property(entity => entity.RegistrationDate).HasColumnName("registration_date");
        animal.Property(entity => entity.DischargeDate).HasColumnName("discharge_date");
        animal.Property(entity => entity.Sex).HasColumnName("sex").HasMaxLength(20);
        animal.Property(entity => entity.SourceBirthId).HasColumnName("source_birth_id");
        animal.HasIndex(entity => entity.Identification).IsUnique();
        animal.HasOne(entity => entity.LivestockFarm)
            .WithMany(entity => entity.Animals)
            .HasForeignKey(entity => entity.LivestockFarmId)
            .OnDelete(DeleteBehavior.Cascade);
        animal.HasOne(entity => entity.SourceBirth)
            .WithMany()
            .HasForeignKey(entity => entity.SourceBirthId)
            .OnDelete(DeleteBehavior.SetNull);

        var ovinoCaprino = modelBuilder.Entity<OvinoCaprinoAnimal>();
        ovinoCaprino.ToTable("ovino_caprino");
        ovinoCaprino.HasKey(entity => entity.AnimalId);
        ovinoCaprino.Property(entity => entity.AnimalId).HasColumnName("animal_id");
        ovinoCaprino.Property(entity => entity.DominantAllele).HasColumnName("dominant_allele").HasMaxLength(80);
        ovinoCaprino.Property(entity => entity.Genotyping).HasColumnName("genotyping").HasMaxLength(120);
        ovinoCaprino.Property(entity => entity.LowAllele).HasColumnName("low_allele").HasMaxLength(80);
        ovinoCaprino.Property(entity => entity.SpeciesType)
            .HasColumnName("species_type")
            .HasConversion(
                entity => entity.ToString().ToLowerInvariant(),
                value => Enum.Parse<LivestockSpecies>(value, true))
            .HasMaxLength(40)
            .IsRequired();
        ovinoCaprino.HasOne(entity => entity.Animal)
            .WithOne(entity => entity.OvinoCaprino)
            .HasForeignKey<OvinoCaprinoAnimal>(entity => entity.AnimalId)
            .OnDelete(DeleteBehavior.Cascade);

        var porcino = modelBuilder.Entity<PorcinoAnimal>();
        porcino.ToTable("porcino");
        porcino.HasKey(entity => entity.AnimalId);
        porcino.Property(entity => entity.AnimalId).HasColumnName("animal_id");
        porcino.Property(entity => entity.AnimalType).HasColumnName("animal_type").HasMaxLength(80).IsRequired();
        porcino.Property(entity => entity.IdentificationDate).HasColumnName("identification_date");
        porcino.Property(entity => entity.PigRegistrationNumber).HasColumnName("pig_registration_number").HasMaxLength(80);
        porcino.Property(entity => entity.Tag).HasColumnName("tag").HasMaxLength(80);
        porcino.HasOne(entity => entity.Animal)
            .WithOne(entity => entity.Porcino)
            .HasForeignKey<PorcinoAnimal>(entity => entity.AnimalId)
            .OnDelete(DeleteBehavior.Cascade);

        var animalBirth = modelBuilder.Entity<AnimalBirth>();
        animalBirth.ToTable("animal_birth");
        animalBirth.HasKey(entity => entity.Id);
        animalBirth.Property(entity => entity.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        animalBirth.Property(entity => entity.LivestockFarmId).HasColumnName("livestock_farm_id");
        animalBirth.Property(entity => entity.MotherAnimalId).HasColumnName("mother_animal_id");
        animalBirth.Property(entity => entity.FatherAnimalId).HasColumnName("father_animal_id");
        animalBirth.Property(entity => entity.BirthDate).HasColumnName("birth_date");
        animalBirth.Property(entity => entity.BirthWeight).HasColumnName("birth_weight");
        animalBirth.Property(entity => entity.Observations).HasColumnName("observations");
        animalBirth.Property(entity => entity.OffspringNumber).HasColumnName("offspring_number");
        animalBirth.Property(entity => entity.BalanceId).HasColumnName("balance_id");
        animalBirth.HasOne(entity => entity.LivestockFarm)
            .WithMany()
            .HasForeignKey(entity => entity.LivestockFarmId)
            .OnDelete(DeleteBehavior.Cascade);
        animalBirth.HasOne(entity => entity.Balance)
            .WithMany()
            .HasForeignKey(entity => entity.BalanceId)
            .OnDelete(DeleteBehavior.SetNull);
        animalBirth.HasOne(entity => entity.MotherAnimal)
            .WithMany()
            .HasForeignKey(entity => entity.MotherAnimalId)
            .OnDelete(DeleteBehavior.SetNull);
        animalBirth.HasOne(entity => entity.FatherAnimal)
            .WithMany()
            .HasForeignKey(entity => entity.FatherAnimalId)
            .OnDelete(DeleteBehavior.SetNull);

        var vaccination = modelBuilder.Entity<Vaccination>();
        vaccination.ToTable("vaccination");
        vaccination.HasKey(entity => entity.Id);
        vaccination.Property(entity => entity.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        vaccination.Property(entity => entity.AnimalId).HasColumnName("animal_id");
        vaccination.Property(entity => entity.VaccinationDate).HasColumnName("vaccination_date");
        vaccination.Property(entity => entity.NextDose).HasColumnName("next_dose");
        vaccination.Property(entity => entity.VaccinationType).HasColumnName("vaccination_type").HasMaxLength(120).IsRequired();
        vaccination.Property(entity => entity.Observations).HasColumnName("observations");
        vaccination.HasOne(entity => entity.Animal)
            .WithMany()
            .HasForeignKey(entity => entity.AnimalId)
            .OnDelete(DeleteBehavior.Cascade);

        var movementCertificate = modelBuilder.Entity<MovementCertificate>();
        movementCertificate.ToTable("movement_certificate");
        movementCertificate.HasKey(entity => entity.Id);
        movementCertificate.Property(entity => entity.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        movementCertificate.Property(entity => entity.OriginLivestockId).HasColumnName("origin_livestock_id");
        movementCertificate.Property(entity => entity.DestinationLivestockId).HasColumnName("destination_livestock_id");
        movementCertificate.Property(entity => entity.ArrivalDate).HasColumnName("arrival_date");
        movementCertificate.Property(entity => entity.Status).HasColumnName("status").HasMaxLength(32).HasConversion<string>();
        movementCertificate.Property(entity => entity.OriginExternalCode).HasColumnName("origin_external_code").HasMaxLength(32);
        movementCertificate.Property(entity => entity.OriginExternalName).HasColumnName("origin_external_name").HasMaxLength(180);
        movementCertificate.Property(entity => entity.DestinationExternalCode).HasColumnName("destination_external_code").HasMaxLength(32);
        movementCertificate.Property(entity => entity.DestinationExternalName).HasColumnName("destination_external_name").HasMaxLength(180);
        movementCertificate.Property(entity => entity.DepartureDate).HasColumnName("departure_date");
        movementCertificate.Property(entity => entity.MeansOfTransport).HasColumnName("means_of_transport").HasMaxLength(120);
        movementCertificate.Property(entity => entity.NumberOfAnimals).HasColumnName("number_of_animals");
        movementCertificate.Property(entity => entity.Specie).HasColumnName("specie").HasMaxLength(40).IsRequired();
        movementCertificate.Property(entity => entity.CodRemo).HasColumnName("cod_remo").HasMaxLength(80);
        movementCertificate.Property(entity => entity.Serie).HasColumnName("serie").HasMaxLength(80);
        movementCertificate.Property(entity => entity.SolicitationDate).HasColumnName("solicitation_date");
        movementCertificate.Property(entity => entity.TransportName).HasColumnName("transport_name").HasMaxLength(180);
        movementCertificate.Property(entity => entity.VehicleRegistrationNumber).HasColumnName("vehicle_registration_number").HasMaxLength(40);
        movementCertificate.Property(entity => entity.UnidentifiedCategory)
            .HasColumnName("unidentified_category")
            .HasConversion(
                entity => entity == null ? null : entity.ToString(),
                value => string.IsNullOrWhiteSpace(value) ? null : Enum.Parse<MovementUnidentifiedCategory>(value, true))
            .HasMaxLength(40);
        movementCertificate.HasOne(entity => entity.OriginFarm)
            .WithMany()
            .HasForeignKey(entity => entity.OriginLivestockId)
            .OnDelete(DeleteBehavior.Restrict);
        movementCertificate.HasOne(entity => entity.DestinationFarm)
            .WithMany()
            .HasForeignKey(entity => entity.DestinationLivestockId)
            .OnDelete(DeleteBehavior.Restrict);

        var movementCertificateAnimal = modelBuilder.Entity<MovementCertificateAnimal>();
        movementCertificateAnimal.ToTable("movement_certificate_animals");
        movementCertificateAnimal.HasKey(entity => entity.Id);
        movementCertificateAnimal.Property(entity => entity.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        movementCertificateAnimal.Property(entity => entity.MovementCertificateId).HasColumnName("movement_certificate_id");
        movementCertificateAnimal.Property(entity => entity.AnimalId).HasColumnName("animal_id");
        movementCertificateAnimal.HasIndex(entity => new { entity.MovementCertificateId, entity.AnimalId }).IsUnique();
        movementCertificateAnimal.HasOne(entity => entity.MovementCertificate)
            .WithMany(entity => entity.Animals)
            .HasForeignKey(entity => entity.MovementCertificateId)
            .OnDelete(DeleteBehavior.Cascade);
        movementCertificateAnimal.HasOne(entity => entity.Animal)
            .WithMany(entity => entity.MovementCertificates)
            .HasForeignKey(entity => entity.AnimalId)
            .OnDelete(DeleteBehavior.Restrict);

        var balance = modelBuilder.Entity<Balance>();
        balance.ToTable("balance");
        balance.HasKey(entity => entity.Id);
        balance.Property(entity => entity.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        balance.Property(entity => entity.LivestockFarmId).HasColumnName("livestock_farm_id");
        balance.Property(entity => entity.BalanceDate).HasColumnName("balance_date");
        balance.Property(entity => entity.DestinationLivestockCode).HasColumnName("destination_livestock_code").HasMaxLength(32);
        balance.Property(entity => entity.ModificationCause).HasColumnName("modification_cause").HasMaxLength(80).IsRequired();
        balance.Property(entity => entity.NumberOfAnimals).HasColumnName("number_of_animals");
        balance.Property(entity => entity.OriginLivestockCode).HasColumnName("origin_livestock_code").HasMaxLength(32);
        balance.HasOne(entity => entity.LivestockFarm)
            .WithMany()
            .HasForeignKey(entity => entity.LivestockFarmId)
            .OnDelete(DeleteBehavior.Cascade);

        var balanceOvinoCaprino = modelBuilder.Entity<BalanceOvinoCaprino>();
        balanceOvinoCaprino.ToTable("balance_ovino_caprino");
        balanceOvinoCaprino.HasKey(entity => entity.BalanceId);
        balanceOvinoCaprino.Property(entity => entity.BalanceId).HasColumnName("balance_id");
        balanceOvinoCaprino.Property(entity => entity.NonReproductiveBetween4And12Months).HasColumnName("non_reproductive_between_4_12m");
        balanceOvinoCaprino.Property(entity => entity.NonReproductiveUnder4Months).HasColumnName("non_reproductive_under_4m");
        balanceOvinoCaprino.Property(entity => entity.ReproductiveFemales).HasColumnName("reproductive_females");
        balanceOvinoCaprino.Property(entity => entity.ReproductiveMales).HasColumnName("reproductive_males");
        balanceOvinoCaprino.Property(entity => entity.TransportTicketNumber).HasColumnName("transport_ticket_number").HasMaxLength(80);
        balanceOvinoCaprino.Property(entity => entity.TransporterName).HasColumnName("transporter_name").HasMaxLength(180);
        balanceOvinoCaprino.HasOne(entity => entity.Balance)
            .WithOne(entity => entity.OvinoCaprino)
            .HasForeignKey<BalanceOvinoCaprino>(entity => entity.BalanceId)
            .OnDelete(DeleteBehavior.Cascade);

        var balancePorcino = modelBuilder.Entity<BalancePorcino>();
        balancePorcino.ToTable("balance_porcino");
        balancePorcino.HasKey(entity => entity.BalanceId);
        balancePorcino.Property(entity => entity.BalanceId).HasColumnName("balance_id");
        balancePorcino.Property(entity => entity.Baits).HasColumnName("baits");
        balancePorcino.Property(entity => entity.Boars).HasColumnName("boars");
        balancePorcino.Property(entity => entity.Breed).HasColumnName("breed").HasMaxLength(80);
        balancePorcino.Property(entity => entity.Piglets).HasColumnName("piglets");
        balancePorcino.Property(entity => entity.PigsReposition).HasColumnName("pigs_reposition");
        balancePorcino.Property(entity => entity.Rear).HasColumnName("rear");
        balancePorcino.Property(entity => entity.SowsForLive).HasColumnName("sows_for_live");
        balancePorcino.Property(entity => entity.SowsReposition).HasColumnName("sows_reposition");
        balancePorcino.Property(entity => entity.Tag).HasColumnName("tag").HasMaxLength(80);
        balancePorcino.Property(entity => entity.Type).HasColumnName("type").HasMaxLength(80);
        balancePorcino.HasOne(entity => entity.Balance)
            .WithOne(entity => entity.Porcino)
            .HasForeignKey<BalancePorcino>(entity => entity.BalanceId)
            .OnDelete(DeleteBehavior.Cascade);

        var census = modelBuilder.Entity<Census>();
        census.ToTable("census");
        census.HasKey(entity => entity.Id);
        census.Property(entity => entity.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        census.Property(entity => entity.LivestockFarmId).HasColumnName("livestock_farm_id");
        census.Property(entity => entity.CensusDate).HasColumnName("census_date");
        census.HasOne(entity => entity.LivestockFarm)
            .WithMany()
            .HasForeignKey(entity => entity.LivestockFarmId)
            .OnDelete(DeleteBehavior.Cascade);

        var censusOvinoCaprino = modelBuilder.Entity<CensusOvinoCaprino>();
        censusOvinoCaprino.ToTable("census_ovino_caprino");
        censusOvinoCaprino.HasKey(entity => entity.CensusId);
        censusOvinoCaprino.Property(entity => entity.CensusId).HasColumnName("census_id");
        censusOvinoCaprino.Property(entity => entity.NonReproductiveBetween4And12Months).HasColumnName("non_reproductive_between_4_12m");
        censusOvinoCaprino.Property(entity => entity.NonReproductiveUnder4Months).HasColumnName("non_reproductive_under_4m");
        censusOvinoCaprino.Property(entity => entity.ReproductiveFemale).HasColumnName("reproductive_female");
        censusOvinoCaprino.Property(entity => entity.ReproductiveMale).HasColumnName("reproductive_male");
        censusOvinoCaprino.HasOne(entity => entity.Census)
            .WithOne(entity => entity.OvinoCaprino)
            .HasForeignKey<CensusOvinoCaprino>(entity => entity.CensusId)
            .OnDelete(DeleteBehavior.Cascade);

        var censusPorcino = modelBuilder.Entity<CensusPorcino>();
        censusPorcino.ToTable("census_porcino");
        censusPorcino.HasKey(entity => entity.CensusId);
        censusPorcino.Property(entity => entity.CensusId).HasColumnName("census_id");
        censusPorcino.Property(entity => entity.Baits).HasColumnName("baits");
        censusPorcino.Property(entity => entity.Boars).HasColumnName("boars");
        censusPorcino.Property(entity => entity.Piglets).HasColumnName("piglets");
        censusPorcino.Property(entity => entity.PigsReposition).HasColumnName("pigs_reposition");
        censusPorcino.Property(entity => entity.Rears).HasColumnName("rears");
        censusPorcino.Property(entity => entity.Sow).HasColumnName("sow");
        censusPorcino.Property(entity => entity.SowsReposition).HasColumnName("sows_reposition");
        censusPorcino.HasOne(entity => entity.Census)
            .WithOne(entity => entity.Porcino)
            .HasForeignKey<CensusPorcino>(entity => entity.CensusId)
            .OnDelete(DeleteBehavior.Cascade);

        var incident = modelBuilder.Entity<Incident>();
        incident.ToTable("incident");
        incident.HasKey(entity => entity.Id);
        incident.Property(entity => entity.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        incident.Property(entity => entity.LivestockFarmId).HasColumnName("livestock_farm_id");
        incident.Property(entity => entity.AnimalId).HasColumnName("animal_id");
        incident.Property(entity => entity.ChangeReason).HasColumnName("change_reason").HasMaxLength(120);
        incident.Property(entity => entity.Description).HasColumnName("description");
        incident.Property(entity => entity.IncidentDate).HasColumnName("incident_date");
        incident.Property(entity => entity.LastIdentification).HasColumnName("last_identification").HasMaxLength(80);
        incident.Property(entity => entity.NewIdentification).HasColumnName("new_identification").HasMaxLength(80);
        incident.HasOne(entity => entity.LivestockFarm)
            .WithMany()
            .HasForeignKey(entity => entity.LivestockFarmId)
            .OnDelete(DeleteBehavior.Cascade);
        incident.HasOne(entity => entity.Animal)
            .WithMany()
            .HasForeignKey(entity => entity.AnimalId)
            .OnDelete(DeleteBehavior.SetNull);

        var inspection = modelBuilder.Entity<Inspection>();
        inspection.ToTable("inspection");
        inspection.HasKey(entity => entity.Id);
        inspection.Property(entity => entity.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        inspection.Property(entity => entity.LivestockFarmId).HasColumnName("livestock_farm_id");
        inspection.Property(entity => entity.InspectionDate).HasColumnName("inspection_date");
        inspection.Property(entity => entity.Reason).HasColumnName("reason").HasMaxLength(120);
        inspection.Property(entity => entity.Observations).HasColumnName("observations");
        inspection.Property(entity => entity.Veterinary).HasColumnName("veterinary").HasMaxLength(180);
        inspection.Property(entity => entity.TaggedAnimals).HasColumnName("tagged_animals");
        inspection.HasOne(entity => entity.LivestockFarm)
            .WithMany()
            .HasForeignKey(entity => entity.LivestockFarmId)
            .OnDelete(DeleteBehavior.Cascade);

        var activationToken = modelBuilder.Entity<AccountActivationToken>();
        activationToken.ToTable("account_activation_token");
        activationToken.HasKey(entity => entity.Id);
        activationToken.Property(entity => entity.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        activationToken.Property(entity => entity.UserId).HasColumnName("user_id");
        activationToken.Property(entity => entity.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        activationToken.Property(entity => entity.ExpiresAt).HasColumnName("expires_at");
        activationToken.Property(entity => entity.UsedAt).HasColumnName("used_at");
        activationToken.Property(entity => entity.CreatedByUserId).HasColumnName("created_by_user_id");
        activationToken.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        activationToken.HasIndex(entity => entity.TokenHash).IsUnique();
        activationToken.HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        activationToken.HasOne(entity => entity.CreatedByUser)
            .WithMany()
            .HasForeignKey(entity => entity.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        var passwordResetToken = modelBuilder.Entity<PasswordResetToken>();
        passwordResetToken.ToTable("password_reset_token");
        passwordResetToken.HasKey(entity => entity.Id);
        passwordResetToken.Property(entity => entity.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        passwordResetToken.Property(entity => entity.UserId).HasColumnName("user_id");
        passwordResetToken.Property(entity => entity.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        passwordResetToken.Property(entity => entity.ExpiresAt).HasColumnName("expires_at");
        passwordResetToken.Property(entity => entity.UsedAt).HasColumnName("used_at");
        passwordResetToken.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        passwordResetToken.HasIndex(entity => entity.TokenHash).IsUnique();
        passwordResetToken.HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static AnimalRegistrationCause? ParseAnimalRegistrationCause(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<AnimalRegistrationCause>(value, true, out var cause)
            ? cause
            : AnimalRegistrationCause.Entrada;
    }

    private static AnimalDischargeCause? ParseAnimalDischargeCause(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<AnimalDischargeCause>(value, true, out var cause)
            ? cause
            : AnimalDischargeCause.Salida;
    }
}
