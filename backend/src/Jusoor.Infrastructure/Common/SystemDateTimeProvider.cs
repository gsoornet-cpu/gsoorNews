using Jusoor.Application.Common.Interfaces;

namespace Jusoor.Infrastructure.Common;

public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
