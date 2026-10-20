using ClosedXML.Excel;
using UGTM.Modules.Identity.Infrastructure.Importing;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Tests.Infrastructure;

public sealed class ClosedXmlUserImportFileReaderTests
{
    [Fact]
    public void Read_parses_vietnamese_headers_and_skips_empty_rows()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Users");
        sheet.Cell("A1").Value = "Email";
        sheet.Cell("B1").Value = "Họ và tên";
        sheet.Cell("C1").Value = "Mã sinh viên";
        sheet.Cell("D1").Value = "Số điện thoại";
        sheet.Cell("A2").Value = "student@ugtm.edu.vn";
        sheet.Cell("B2").Value = "Nguyễn Văn A";
        sheet.Cell("C2").Value = "SV001";
        sheet.Cell("D2").Value = "0901234567";
        sheet.Cell("A4").Value = "second@ugtm.edu.vn";
        sheet.Cell("B4").Value = "Trần Thị B";
        sheet.Cell("C4").Value = "SV002";

        using var stream = Save(workbook);
        var result = new ClosedXmlUserImportFileReader().Read(stream);

        Assert.Empty(result.Errors);
        Assert.Collection(
            result.Rows,
            row =>
            {
                Assert.Equal(2, row.RowNumber);
                Assert.Equal("student@ugtm.edu.vn", row.Email);
                Assert.Equal("Nguyễn Văn A", row.FullName);
                Assert.Equal("SV001", row.UserCode);
                Assert.Equal("0901234567", row.PhoneNumber);
            },
            row =>
            {
                Assert.Equal(4, row.RowNumber);
                Assert.Equal("second@ugtm.edu.vn", row.Email);
            });
    }

    [Fact]
    public void Read_reports_formula_cells()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Users");
        sheet.Cell("A1").Value = "Email";
        sheet.Cell("B1").Value = "FullName";
        sheet.Cell("C1").Value = "UserCode";
        sheet.Cell("D1").Value = "PhoneNumber";
        sheet.Cell("A2").FormulaA1 = "=\"student@ugtm.edu.vn\"";
        sheet.Cell("B2").Value = "Student";
        sheet.Cell("C2").Value = "SV001";

        using var stream = Save(workbook);
        var result = new ClosedXmlUserImportFileReader().Read(stream);

        Assert.Contains(result.Errors, error =>
            error.RowNumber == 2
            && error.Field == "email"
            && error.Code == "FormulaNotAllowed");
    }

    [Fact]
    public void Read_rejects_corrupted_workbook()
    {
        using var stream = new MemoryStream([1, 2, 3, 4]);

        Assert.Throws<InvalidDataException>(() =>
            new ClosedXmlUserImportFileReader().Read(stream));
    }

    [Theory]
    [InlineData(SystemRoleName.Student)]
    [InlineData(SystemRoleName.Lecturer)]
    public void Generated_template_can_be_read_by_import_reader(SystemRoleName role)
    {
        var content = new ClosedXmlUserImportTemplateGenerator().Generate(role);
        using var stream = new MemoryStream(content);

        var result = new ClosedXmlUserImportFileReader().Read(stream);

        Assert.Empty(result.Errors);
        Assert.Empty(result.Rows);
    }

    private static MemoryStream Save(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
