namespace UGTM.Modules.Identity.Application.Common;

public interface IClock
{
    DateTime UtcNow { get; }
}