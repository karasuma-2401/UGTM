namespace UGTM.Modules.Identity.Application.Activation;

public sealed record CompleteActivationCommand(
    string Token,
    string Password);