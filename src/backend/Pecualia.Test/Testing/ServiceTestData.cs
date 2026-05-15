using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Test.Testing;

public static class ServiceTestData
{
    public static AppUser CreateUser(long id, UserRole role, string name, string surname, bool isActive = true, string? email = null)
    {
        return new AppUser
        {
            Id = id,
            Role = role,
            Name = name,
            Surname = surname,
            Email = email,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public static Farmer CreateFarmer(
        long userId,
        AppUser user,
        long? managerId = null,
        string nifCif = "12345678Z",
        PersonType personType = PersonType.Individual,
        FarmerStatus status = FarmerStatus.Active)
    {
        return new Farmer
        {
            UserId = userId,
            User = user,
            ManagerId = managerId,
            NifCif = nifCif,
            PersonType = personType,
            Status = status,
            Town = "Sevilla",
            Province = "Sevilla",
            PhoneNumber = "600000000"
        };
    }

    public static Manager CreateManager(long userId, AppUser user)
    {
        return new Manager
        {
            UserId = userId,
            User = user,
            OrganizationName = "Gestor Test",
            ProfessionalIdentifier = $"PRO-{userId}",
            InvitationCode = $"INV-{userId}"
        };
    }

    public static LivestockFarm CreateFarm(
        long id,
        long farmerId,
        LivestockSpecies species,
        string name,
        string regaCode,
        int? authorisedCapacity = null,
        int? porcineMothersCapacity = null,
        int? porcineFatteningCapacity = null)
    {
        return new LivestockFarm
        {
            Id = id,
            FarmerId = farmerId,
            Name = name,
            RegaCode = regaCode,
            LivestockSpecies = species,
            Regime = FarmRegime.Intensive,
            Town = "Sevilla",
            Province = "Sevilla",
            Spindle = 30,
            AuthorisedCapacity = authorisedCapacity,
            PorcineRegistryNumber = species == LivestockSpecies.Porcine ? $"PR-{id}" : string.Empty,
            PorcineMothersCapacity = porcineMothersCapacity,
            PorcineFatteningCapacity = porcineFatteningCapacity
        };
    }

    public static Animal CreateAnimal(
        long id,
        long farmId,
        string identification,
        DateOnly registrationDate,
        DateOnly? birthDate = null,
        int? birthYear = null,
        AnimalRegistrationCause? registrationCause = null,
        string? sex = null,
        long? sourceBirthId = null)
    {
        return new Animal
        {
            Id = id,
            LivestockFarmId = farmId,
            Identification = identification,
            RegistrationDate = registrationDate,
            BirthDate = birthDate,
            BirthYear = birthYear,
            RegistrationCause = registrationCause,
            Sex = sex,
            SourceBirthId = sourceBirthId
        };
    }

    public static PorcinoAnimal CreatePorcinoAnimal(long animalId, string animalType)
    {
        return new PorcinoAnimal
        {
            AnimalId = animalId,
            AnimalType = animalType
        };
    }

    public static OvinoCaprinoAnimal CreateOvinoCaprinoAnimal(long animalId, LivestockSpecies species)
    {
        return new OvinoCaprinoAnimal
        {
            AnimalId = animalId,
            SpeciesType = species
        };
    }

    public static AnimalBirth CreateBirth(long id, long farmId, DateOnly birthDate, int offspringNumber, long? balanceId = null)
    {
        return new AnimalBirth
        {
            Id = id,
            LivestockFarmId = farmId,
            BirthDate = birthDate,
            OffspringNumber = offspringNumber,
            BalanceId = balanceId
        };
    }
}
