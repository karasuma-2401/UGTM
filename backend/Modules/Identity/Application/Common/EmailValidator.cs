using System.Net.Mail;
using System.Text.RegularExpressions;

namespace UGTM.Modules.Identity.Application.Common;

public static class EmailValidator
{
    public const int MaxEmailLength = 150;
    public const int MaxFullNameLength = 150;
    public const int MaxUserCodeLength = 20;

    private static readonly Regex BasicEmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var trimmed = email.Trim();
        if (trimmed.Length > MaxEmailLength || !BasicEmailRegex.IsMatch(trimmed))
        {
            return false;
        }

        return MailAddress.TryCreate(trimmed, out var parsed)
               && string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase);
    }
}
