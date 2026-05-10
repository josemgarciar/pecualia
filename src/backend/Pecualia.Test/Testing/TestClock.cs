using Pecualia.Api.Services;

namespace Pecualia.Test.Testing;

public sealed class TestClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = utcNow;

    public void Set(DateTimeOffset value)
    {
        UtcNow = value;
    }
}
