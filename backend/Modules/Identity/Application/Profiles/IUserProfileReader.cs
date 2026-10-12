namespace UGTM.Modules.Identity.Application.Profiles;

public interface IUserProfileReader
{
    Task<UserProfileDto?> GetAsync( 
        Guid userId,
        CancellationToken cancellationToken); 
}