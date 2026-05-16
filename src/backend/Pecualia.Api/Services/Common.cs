namespace Pecualia.Api.Services;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

internal static class BalanceMarkers
{
    internal const string PorcineAggregateDeath = "__PORCINE_AGGREGATE_DEATH__";
}

public sealed class DomainException(string message) : Exception(message);
