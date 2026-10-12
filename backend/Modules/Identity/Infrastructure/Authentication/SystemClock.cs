using UGTM.Modules.Identity.Application.Common;

namespace UGTM.Modules.Identity.Infrastructure.Authentication;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}