using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;

namespace Pecualia.Test.Services;

public sealed class DashboardServiceTests
{
    [Fact]
    public void IsPendingMovementConfirmationVisible_ReturnsTrue_WhenGuideArrivedAndWithinGraceWindow()
    {
        var now = new DateTime(2026, 05, 10, 12, 00, 00, DateTimeKind.Utc);
        var movement = new MovementCertificate
        {
            Status = MovementStatus.Pending,
            ArrivalDate = now.AddDays(-3)
        };

        DashboardService.IsPendingMovementConfirmationVisible(movement, now).Should().BeTrue();
    }

    [Fact]
    public void IsPendingMovementConfirmationVisible_ReturnsFalse_WhenGuideIsAlreadyConfirmed()
    {
        var now = new DateTime(2026, 05, 10, 12, 00, 00, DateTimeKind.Utc);
        var movement = new MovementCertificate
        {
            Status = MovementStatus.Confirmed,
            ArrivalDate = now.AddDays(-1)
        };

        DashboardService.IsPendingMovementConfirmationVisible(movement, now).Should().BeFalse();
    }

    [Fact]
    public void IsPendingMovementConfirmationVisible_ReturnsFalse_WhenArrivalIsStillInFuture()
    {
        var now = new DateTime(2026, 05, 10, 12, 00, 00, DateTimeKind.Utc);
        var movement = new MovementCertificate
        {
            Status = MovementStatus.Pending,
            ArrivalDate = now.AddHours(2)
        };

        DashboardService.IsPendingMovementConfirmationVisible(movement, now).Should().BeFalse();
    }

    [Fact]
    public void IsPendingMovementConfirmationVisible_ReturnsFalse_WhenGraceWindowExpired()
    {
        var now = new DateTime(2026, 05, 10, 12, 00, 00, DateTimeKind.Utc);
        var movement = new MovementCertificate
        {
            Status = MovementStatus.Pending,
            ArrivalDate = now.AddDays(-(DashboardService.MovementConfirmationGraceDays + 1))
        };

        DashboardService.IsPendingMovementConfirmationVisible(movement, now).Should().BeFalse();
    }

    [Fact]
    public void BuildPendingMovementConfirmationTasks_CreatesDeadlineBasedTasks_ForDestinationFarm()
    {
        var today = new DateOnly(2026, 05, 10);
        var tasks = DashboardService.BuildPendingMovementConfirmationTasks(
            new[]
            {
                new MovementCertificate
                {
                    Id = 14,
                    Status = MovementStatus.Pending,
                    DestinationLivestockId = 99,
                    CodRemo = "RM-1001",
                    ArrivalDate = new DateTime(2026, 05, 08, 09, 00, 00, DateTimeKind.Utc)
                }
            },
            new Dictionary<long, string>
            {
                [99] = "Dehesa El Robledal"
            },
            today);

        tasks.Should().ContainSingle();
        tasks[0].Kind.Should().Be("MovementConfirmation");
        tasks[0].Title.Should().Be("Confirmar guía pendiente");
        tasks[0].Detail.Should().Contain("Dehesa El Robledal");
        tasks[0].Detail.Should().Contain("Guía RM-1001");
        tasks[0].DueDate.Should().Be(new DateOnly(2026, 05, 18));
        tasks[0].Tone.Should().Be("warning");
    }

    [Fact]
    public void BuildPendingMovementConfirmationTasks_FallsBackToSerieOrId_WhenCodRemoIsMissing()
    {
        var today = new DateOnly(2026, 05, 20);
        var tasks = DashboardService.BuildPendingMovementConfirmationTasks(
            new[]
            {
                new MovementCertificate
                {
                    Id = 77,
                    Status = MovementStatus.Pending,
                    DestinationLivestockId = 7,
                    Serie = "SER-77",
                    ArrivalDate = new DateTime(2026, 05, 10, 08, 00, 00, DateTimeKind.Utc)
                },
                new MovementCertificate
                {
                    Id = 78,
                    Status = MovementStatus.Pending,
                    DestinationLivestockId = 7,
                    ArrivalDate = new DateTime(2026, 05, 09, 08, 00, 00, DateTimeKind.Utc)
                }
            },
            new Dictionary<long, string>
            {
                [7] = "Valle Ibérico"
            },
            today);

        tasks.Should().HaveCount(2);
        tasks[0].Detail.Should().Contain("Guía #78");
        tasks[1].Detail.Should().Contain("Serie SER-77");
        tasks.Should().OnlyContain(task => task.Tone == "danger");
    }

    [Fact]
    public void BuildPendingPorcineTransitionTasks_CreatesWarningTask_WithPendingAnimals()
    {
        var today = new DateOnly(2026, 05, 15);
        var tasks = DashboardService.BuildPendingPorcineTransitionTasks(
            new[]
            {
                new AnimalBirth
                {
                    Id = 41,
                    LivestockFarmId = 8,
                    BirthDate = new DateOnly(2026, 03, 01),
                    OffspringNumber = 12
                }
            },
            new Dictionary<long, int>
            {
                [41] = 4
            },
            new Dictionary<long, string>
            {
                [8] = "Sierra del Este"
            },
            today);

        tasks.Should().ContainSingle();
        tasks[0].Kind.Should().Be("PorcineTransition");
        tasks[0].Title.Should().Be("Reclasificación porcina pendiente");
        tasks[0].Detail.Should().Contain("Sierra del Este");
        tasks[0].Detail.Should().Contain("8 animales");
        tasks[0].Tone.Should().Be("warning");
        tasks[0].DueDate.Should().Be(new DateOnly(2026, 06, 01));
    }

    [Fact]
    public void BuildPendingPorcineTransitionTasks_CreatesDangerTask_WhenFinalWindowIsReached()
    {
        var today = new DateOnly(2026, 08, 15);
        var tasks = DashboardService.BuildPendingPorcineTransitionTasks(
            new[]
            {
                new AnimalBirth
                {
                    Id = 55,
                    LivestockFarmId = 11,
                    BirthDate = new DateOnly(2026, 02, 10),
                    OffspringNumber = 5
                }
            },
            new Dictionary<long, int>(),
            new Dictionary<long, string>
            {
                [11] = "Monte de las Encinas"
            },
            today);

        tasks.Should().ContainSingle();
        tasks[0].Tone.Should().Be("danger");
        tasks[0].DueDate.Should().Be(new DateOnly(2026, 05, 10));
        tasks[0].Detail.Should().Contain("Atrasada");
    }
}
