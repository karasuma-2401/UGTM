namespace UGTM.Modules.Identity.Application.Security;

public sealed record PasswordPolicyResult(
    bool IsValid,
    IReadOnlyList<string> Errors);
public interface IPasswordPolicy
{
    PasswordPolicyResult Validate(string password);
}