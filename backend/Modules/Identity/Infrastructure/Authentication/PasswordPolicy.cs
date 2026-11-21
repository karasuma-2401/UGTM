using UGTM.Modules.Identity.Application.Security;

namespace UGTM.Modules.Identity.Infrastructure.Authentication;

public sealed class PasswordPolicy : IPasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    public PasswordPolicyResult Validate(string password)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Mật khẩu không được để trống.");
            return new PasswordPolicyResult(false, errors);
        }

        if (password.Length < MinLength)
            errors.Add($"Mật khẩu phải có ít nhất {MinLength} ký tự.");
        if (password.Length > MaxLength)
            errors.Add($"Mật khẩu không được vượt quá {MaxLength} ký tự.");
        if (!password.Any(char.IsUpper)) errors.Add("Cần ít nhất một chữ hoa.");
        if (!password.Any(char.IsLower)) errors.Add("Cần ít nhất một chữ thường.");
        if (!password.Any(char.IsDigit)) errors.Add("Cần ít nhất một chữ số.");
        if (!password.Any(ch => !char.IsLetterOrDigit(ch) && !char.IsWhiteSpace(ch) && !char.IsControl(ch)))
            errors.Add("Cần ít nhất một ký tự đặc biệt (không tính khoảng trắng).");

        return new PasswordPolicyResult(errors.Count == 0, errors);
    }
}
