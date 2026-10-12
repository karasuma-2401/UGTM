using UGTM.Modules.Identity.Application.Security;

namespace UGTM.Modules.Identity.Infrastructure.Authentication;

public sealed class PasswordPolicy : IPasswordPolicy
{
    public PasswordPolicyResult Validate(string password)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Mật khẩu không được để trống.");
            return new PasswordPolicyResult(false, errors);
        }

        if (password.Length < 12)
            errors.Add("Mật khẩu phải có ít nhất 12 ký tự.");
        if (!password.Any(char.IsUpper)) errors.Add("Cần ít nhất một chữ hoa.");
        if (!password.Any(char.IsLower)) errors.Add("Cần ít nhất một chữ thường.");
        if (!password.Any(char.IsDigit)) errors.Add("Cần ít nhất một chữ số.");
        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
            errors.Add("Cần ít nhất một ký tự đặc biệt.");

        return new PasswordPolicyResult(errors.Count == 0, errors);
    }
}
