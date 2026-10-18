using ClosedXML.Excel;
using UGTM.Modules.Identity.Application.Users.ImportUsers;

namespace UGTM.Modules.Identity.Infrastructure.Importing;

public sealed class ClosedXmlUserImportFileReader : IUserImportFileReader
{
    private static readonly IReadOnlyDictionary<string, string> HeaderAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["email"] = "email",
            ["fullname"] = "fullName",
            ["hoten"] = "fullName",
            ["hovaten"] = "fullName",
            ["usercode"] = "userCode",
            ["manguoidung"] = "userCode",
            ["mssv"] = "userCode",
            ["masinhvien"] = "userCode",
            ["magiangvien"] = "userCode",
            ["phonenumber"] = "phoneNumber",
            ["sodienthoai"] = "phoneNumber",
            ["dienthoai"] = "phoneNumber"
        };

    public ImportFileReadResult Read(Stream stream)
    {
        try
        {
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet is null || worksheet.RangeUsed() is null)
            {
                return FileError("EmptyFile", "Tệp Excel không có dữ liệu.");
            }

            var usedRange = worksheet.RangeUsed()!;
            var headerRowNumber = usedRange.FirstRow().RowNumber();
            var columns = ReadColumns(worksheet, usedRange, headerRowNumber);
            var missingHeaders = new[] { "email", "fullName", "userCode", "phoneNumber" }
                .Where(header => !columns.ContainsKey(header))
                .ToArray();
            if (missingHeaders.Length > 0)
            {
                return FileError(
                    "MissingHeaders",
                    $"Thiếu cột bắt buộc: {string.Join(", ", missingHeaders)}. Tên cột chuẩn: Email, FullName, UserCode, PhoneNumber.");
            }

            var rows = new List<ImportUserRow>();
            var errors = new List<ImportUserRowError>();
            var lastRowNumber = usedRange.LastRow().RowNumber();
            if (lastRowNumber - headerRowNumber > ImportUsersHandler.MaxRows)
            {
                return FileError(
                    "TooManyRows",
                    $"Tệp chỉ được chứa tối đa {ImportUsersHandler.MaxRows} dòng dữ liệu.");
            }

            for (var rowNumber = headerRowNumber + 1; rowNumber <= lastRowNumber; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);
                var values = columns.ToDictionary(
                    pair => pair.Key,
                    pair => ReadCell(row.Cell(pair.Value)),
                    StringComparer.OrdinalIgnoreCase);

                if (values.Values.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                foreach (var column in columns)
                {
                    if (row.Cell(column.Value).HasFormula)
                    {
                        errors.Add(new ImportUserRowError(
                            rowNumber,
                            column.Key,
                            "FormulaNotAllowed",
                            "Ô công thức không được hỗ trợ; hãy nhập giá trị văn bản trực tiếp."));
                    }
                }

                rows.Add(new ImportUserRow(
                    rowNumber,
                    values["email"],
                    values["fullName"],
                    values["userCode"],
                    values["phoneNumber"]));

            }

            return new ImportFileReadResult(rows, errors);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new InvalidDataException("Không thể đọc tệp Excel.", exception);
        }
    }

    private static Dictionary<string, int> ReadColumns(
        IXLWorksheet worksheet,
        IXLRange usedRange,
        int headerRowNumber)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var firstColumn = usedRange.FirstColumn().ColumnNumber();
        var lastColumn = usedRange.LastColumn().ColumnNumber();
        for (var columnNumber = firstColumn; columnNumber <= lastColumn; columnNumber++)
        {
            var rawHeader = worksheet.Cell(headerRowNumber, columnNumber).GetString();
            var normalizedHeader = NormalizeHeader(rawHeader);
            if (HeaderAliases.TryGetValue(normalizedHeader, out var canonicalHeader)
                && !columns.ContainsKey(canonicalHeader))
            {
                columns[canonicalHeader] = columnNumber;
            }
        }

        return columns;
    }

    private static string ReadCell(IXLCell cell)
        => cell.HasFormula ? string.Empty : cell.GetFormattedString().Trim();

    private static string NormalizeHeader(string value)
        => string.Concat(value
                .Normalize(System.Text.NormalizationForm.FormD)
                .Where(character => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character)
                                    != System.Globalization.UnicodeCategory.NonSpacingMark))
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

    private static ImportFileReadResult FileError(string code, string message)
        => new(
            [],
            [new ImportUserRowError(0, "file", code, message)]);
}
