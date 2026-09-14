using Amaliyotchi.Application.Common.Interfaces;

namespace Amaliyotchi.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
