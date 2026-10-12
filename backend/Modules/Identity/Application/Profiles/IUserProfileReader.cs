namespace UGTM.Modules.Identity.Application.Profiles;

public interface IUserProfileReader
{
    Task<UserProfileDto?> GeAsync( 
        Guid userId,
        CancellationToken cancellationToken); 
}