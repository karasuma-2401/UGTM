using ClosedXML.Excel;
using UGTM.Modules.Identity.Application.Users.ImportUsers;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Infrastructure.Importing;

public sealed class ClosedXmlUserImportTemplateGenerator : IUserImportTemplateGenerator
{
    public byte[] Generate(SystemRoleName role)
    {
        using var workbook = new XLWorkbook();
        var users = workbook.AddWorksheet("Users");
        var headers = new[] { "Email", "FullName", "UserCode", "PhoneNumber" };
        for (var column = 1; column <= headers.Length; column++)
        {
            users.Cell(1, column).Value = headers[column - 1];
        }

        var headerRange = users.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        users.SheetView.FreezeRows(1);
        users.Column(1).Width = 32;
        users.Column(2).Width = 28;
        users.Column(3).Width = 18;
        users.Column(4).Width = 18;
        users.Columns(1, 4).Style.NumberFormat.Format = "@";

        var instructions = workbook.AddWorksheet("Instructions");
        instructions.Cell("A1").Value = "UGTM User Import";
        instructions.Cell("A1").Style.Font.Bold = true;
        instructions.Cell("A2").Value = $"Role: {role}";
        instructions.Cell("A3").Value = "Không đổi tên hoặc xóa bốn cột trong sheet Users.";
        instructions.Cell("A4").Value = role == SystemRoleName.Student
            ? "UserCode (mã sinh viên) là bắt buộc."
            : "UserCode (mã giảng viên) có thể để trống.";
        instructions.Cell("A5").Value = "PhoneNumber có thể để trống; nếu nhập phải dùng định dạng Việt Nam.";
        instructions.Cell("A6").Value = "Không sử dụng công thức Excel trong các ô dữ liệu.";
        instructions.Column(1).Width = 80;

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }
}
