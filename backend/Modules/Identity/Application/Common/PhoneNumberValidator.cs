using System.Text.RegularExpressions;

namespace UGTM.Modules.Identity.Application.Common;

public static class PhoneNumberValidator
{
    // Hỗ trợ số điện thoại Việt Nam:
    // 10 chữ số bắt đầu bằng 0 (ví dụ: 0901234567, 0362120100)
    // hoặc định dạng quốc tế bắt đầu bằng +84 hoặc 84 theo sau là 9 chữ số.
    private static readonly Regex VietnamPhoneRegex = new(
        @"^(0|\+84|84)\d{9}$",
        RegexOptions.Compiled);

    public static bool IsValid(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return true; // Trường không bắt buộc
        }

        return VietnamPhoneRegex.IsMatch(phoneNumber.Trim());
    }
}
