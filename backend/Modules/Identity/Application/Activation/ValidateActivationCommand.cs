namespace UGTM.Modules.Identity.Application.Activation;

public sealed record ValidateActivationCommand(string Token);

public sealed record ValidateActivationResult( 
    string MaskedEmail,
    string FullName,
    DateTime ExpiresAt);