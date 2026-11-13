using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Dtos;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.ExportAcademicResults;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Exporting;

public sealed class AcademicResultsExportFileGenerator : IAcademicResultsExportFileGenerator
{
    public Task<AcademicResultExportFileDto> GenerateAsync(
        IReadOnlyList<AcademicResultExportRowDto> rows,
        string format,
        string phase,
        CancellationToken cancellationToken = default)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = GenerateExcel(rows, phase);
            return Task.FromResult(new AcademicResultExportFileDto(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"KetQuaKLTN_{phase}_{timestamp}.xlsx"));
        }

        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = GeneratePdf(rows, phase);
            return Task.FromResult(new AcademicResultExportFileDto(
                bytes,
                "application/pdf",
                $"KetQuaKLTN_{phase}_{timestamp}.pdf"));
        }

        throw new ArgumentException($"Định dạng không được hỗ trợ: {format}", nameof(format));
    }

    private static byte[] GenerateExcel(IReadOnlyList<AcademicResultExportRowDto> rows, string phase)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("KetQuaKLTN");

        var phaseTitle = phase.Equals("Midterm", StringComparison.OrdinalIgnoreCase) ? "GIỮA KỲ" : "CUỐI KỲ";

        // Tiêu đề báo cáo
        worksheet.Cell(1, 1).Value = $"BÁO CÁO KẾT QUẢ KHÓA LUẬN TỐT NGHIỆP - {phaseTitle}";
        worksheet.Range(1, 1, 1, 11).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Ngày xuất
        worksheet.Cell(2, 1).Value = $"Thời gian xuất báo cáo: {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss} (UTC)";
        worksheet.Range(2, 1, 2, 11).Merge();
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Cell(2, 1).Style.Font.FontSize = 11;
        worksheet.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Tiêu đề bảng (Header) tại dòng 4
        var headers = new[]
        {
            "MSSV",
            "Họ tên SV",
            "Mã nhóm",
            "Tên đề tài",
            "Khoa quản lý",
            "Điểm GVHD",
            "Điểm GVPB",
            "Điểm Hội đồng",
            "Điểm tổng kết",
            "Trạng thái lưu chiểu",
            "Trạng thái công bố"
        };

        for (var col = 0; col < headers.Length; col++)
        {
            var cell = worksheet.Cell(4, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A"); // Navy blue
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        var currentRow = 5;
        foreach (var row in rows)
        {
            worksheet.Cell(currentRow, 1).SetValue(row.StudentCode);
            worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(currentRow, 2).SetValue(row.StudentFullName);
            worksheet.Cell(currentRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            worksheet.Cell(currentRow, 3).SetValue(row.GroupCode);
            worksheet.Cell(currentRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(currentRow, 4).SetValue(row.TopicTitleVi);
            worksheet.Cell(currentRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            worksheet.Cell(currentRow, 5).SetValue(row.FacultyName);
            worksheet.Cell(currentRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            worksheet.Cell(currentRow, 6).SetValue(row.AdvisorScoreText);
            worksheet.Cell(currentRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            worksheet.Cell(currentRow, 7).SetValue(row.ReviewerScoreText);
            worksheet.Cell(currentRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            worksheet.Cell(currentRow, 8).SetValue(row.CouncilScoreText);
            worksheet.Cell(currentRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            worksheet.Cell(currentRow, 9).SetValue(row.FinalScoreText);
            worksheet.Cell(currentRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            worksheet.Cell(currentRow, 10).SetValue(row.ArchiveStatusLabel);
            worksheet.Cell(currentRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(currentRow, 11).SetValue(row.PublicationStatusLabel);
            worksheet.Cell(currentRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            currentRow++;
        }

        // Định dạng viền (Border) cho toàn bộ bảng
        var dataRange = worksheet.Range(4, 1, Math.Max(4, currentRow - 1), 11);
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

        worksheet.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    private static byte[] GeneratePdf(IReadOnlyList<AcademicResultExportRowDto> rows, string phase)
    {
        // Tạo PDF chuẩn %PDF-1.4 khổ A4 Landscape (842 x 595 pt)
        var phaseTitle = phase.Equals("Midterm", StringComparison.OrdinalIgnoreCase) ? "GIUA KY" : "CUOI KY";
        var title = $"BAO CAO KET QUA KHOA LUAN TOT NGHIEP - {phaseTitle}";
        var subtitle = $"Thoi gian xuat: {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss} UTC";

        const int rowsPerPage = 20;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)Math.Max(1, rows.Count) / rowsPerPage));

        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII);

        var objects = new List<long>();

        void WriteObjHeader(int objNum)
        {
            writer.Flush();
            objects.Add(ms.Position);
            writer.Write($"{objNum} 0 obj\n");
        }

        void WriteObjFooter()
        {
            writer.Write("\nendobj\n");
        }

        // 1. PDF Header
        writer.Write("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");

        // Object 1: Catalog
        WriteObjHeader(1);
        writer.Write("<< /Type /Catalog /Pages 2 0 R >>");
        WriteObjFooter();

        // Object 2: Pages
        var pageObjIds = new List<int>();
        for (var p = 0; p < totalPages; p++)
        {
            pageObjIds.Add(3 + (p * 2));
        }

        WriteObjHeader(2);
        var kidsStr = string.Join(" ", pageObjIds.Select(id => $"{id} 0 R"));
        writer.Write($"<< /Type /Pages /Kids [{kidsStr}] /Count {totalPages} >>");
        WriteObjFooter();

        // Object: Font F1 (Helvetica)
        var fontObjId = 3 + (totalPages * 2);

        // Build each page and its content stream
        for (var p = 0; p < totalPages; p++)
        {
            var pageId = 3 + (p * 2);
            var contentId = pageId + 1;
            var pageSlice = rows.Skip(p * rowsPerPage).Take(rowsPerPage).ToList();

            // Object pageId: Page
            WriteObjHeader(pageId);
            writer.Write($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 842 595] /Contents {contentId} 0 R /Resources << /Font << /F1 {fontObjId} 0 R >> >> >>");
            WriteObjFooter();

            // Object contentId: Stream
            var pageStream = BuildPageContentStream(title, subtitle, pageSlice, p + 1, totalPages);
            var streamBytes = Encoding.ASCII.GetBytes(pageStream);

            WriteObjHeader(contentId);
            writer.Write($"<< /Length {streamBytes.Length} >>\nstream\n");
            writer.Flush();
            ms.Write(streamBytes, 0, streamBytes.Length);
            writer.Write("\nendstream");
            WriteObjFooter();
        }

        // Font object
        WriteObjHeader(fontObjId);
        writer.Write("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        WriteObjFooter();

        // XRef Table
        writer.Flush();
        var xrefPos = ms.Position;
        writer.Write("xref\n");
        writer.Write($"0 {objects.Count + 1}\n");
        writer.Write("0000000000 65535 f \n");
        foreach (var pos in objects)
        {
            writer.Write($"{pos:D10} 00000 n \n");
        }

        // Trailer
        writer.Write("trailer\n");
        writer.Write($"<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        writer.Write("startxref\n");
        writer.Write($"{xrefPos}\n");
        writer.Write("%%EOF\n");
        writer.Flush();

        return ms.ToArray();
    }

    private static string BuildPageContentStream(
        string title,
        string subtitle,
        IReadOnlyList<AcademicResultExportRowDto> rows,
        int pageNumber,
        int totalPages)
    {
        var sb = new StringBuilder();

        // Tiêu đề trang
        sb.AppendLine("BT");
        sb.AppendLine("/F1 16 Tf");
        sb.AppendLine("40 560 Td");
        sb.AppendLine($"({EscapePdf(title)}) Tj");
        sb.AppendLine("ET");

        // Subtitle & Số trang
        sb.AppendLine("BT");
        sb.AppendLine("/F1 9 Tf");
        sb.AppendLine("40 545 Td");
        sb.AppendLine($"({EscapePdf(subtitle)}   |   Trang {pageNumber}/{totalPages}) Tj");
        sb.AppendLine("ET");

        // Vẽ Header bảng
        // Vị trí Y của Header là 520
        var y = 520;
        sb.AppendLine("0.12 0.23 0.54 rg"); // Navy blue fill
        sb.AppendLine($"40 {y} 760 18 re f"); // Draw filled rect

        sb.AppendLine("BT");
        sb.AppendLine("/F1 8 Tf");
        sb.AppendLine("1 1 1 rg"); // White text
        sb.AppendLine($"45 {y + 5} Td");
        sb.AppendLine("(MSSV) Tj");
        sb.AppendLine("55 0 Td");
        sb.AppendLine("(Ho ten SV) Tj");
        sb.AppendLine("95 0 Td");
        sb.AppendLine("(Nhom) Tj");
        sb.AppendLine("35 0 Td");
        sb.AppendLine("(Ten de tai) Tj");
        sb.AppendLine("200 0 Td");
        sb.AppendLine("(Khoa) Tj");
        sb.AppendLine("55 0 Td");
        sb.AppendLine("(GVHD) Tj");
        sb.AppendLine("35 0 Td");
        sb.AppendLine("(GVPB) Tj");
        sb.AppendLine("35 0 Td");
        sb.AppendLine("(HD) Tj");
        sb.AppendLine("35 0 Td");
        sb.AppendLine("(Tong ket) Tj");
        sb.AppendLine("45 0 Td");
        sb.AppendLine("(Luu chieu) Tj");
        sb.AppendLine("65 0 Td");
        sb.AppendLine("(Cong bo) Tj");
        sb.AppendLine("ET");

        // Vẽ các dòng dữ liệu
        y -= 16;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (i % 2 == 1)
            {
                sb.AppendLine("0.95 0.95 0.95 rg"); // Alternating row color
                sb.AppendLine($"40 {y} 760 16 re f");
            }

            sb.AppendLine("0 0 0 rg"); // Black text
            sb.AppendLine("BT");
            sb.AppendLine("/F1 8 Tf");
            sb.AppendLine($"45 {y + 4} Td");

            var mssv = Truncate(RemoveDiacritics(row.StudentCode), 10);
            var name = Truncate(RemoveDiacritics(row.StudentFullName), 18);
            var grp = Truncate(RemoveDiacritics(row.GroupCode), 6);
            var topic = Truncate(RemoveDiacritics(row.TopicTitleVi), 40);
            var faculty = Truncate(RemoveDiacritics(row.FacultyName), 10);
            var adv = Truncate(row.AdvisorScoreText, 6);
            var rev = Truncate(row.ReviewerScoreText, 6);
            var cou = Truncate(row.CouncilScoreText, 6);
            var fin = Truncate(row.FinalScoreText, 6);
            var arch = Truncate(RemoveDiacritics(row.ArchiveStatusLabel), 12);
            var pub = Truncate(RemoveDiacritics(row.PublicationStatusLabel), 14);

            sb.AppendLine($"({EscapePdf(mssv)}) Tj");
            sb.AppendLine("55 0 Td");
            sb.AppendLine($"({EscapePdf(name)}) Tj");
            sb.AppendLine("95 0 Td");
            sb.AppendLine($"({EscapePdf(grp)}) Tj");
            sb.AppendLine("35 0 Td");
            sb.AppendLine($"({EscapePdf(topic)}) Tj");
            sb.AppendLine("200 0 Td");
            sb.AppendLine($"({EscapePdf(faculty)}) Tj");
            sb.AppendLine("55 0 Td");
            sb.AppendLine($"({EscapePdf(adv)}) Tj");
            sb.AppendLine("35 0 Td");
            sb.AppendLine($"({EscapePdf(rev)}) Tj");
            sb.AppendLine("35 0 Td");
            sb.AppendLine($"({EscapePdf(cou)}) Tj");
            sb.AppendLine("35 0 Td");
            sb.AppendLine($"({EscapePdf(fin)}) Tj");
            sb.AppendLine("45 0 Td");
            sb.AppendLine($"({EscapePdf(arch)}) Tj");
            sb.AppendLine("65 0 Td");
            sb.AppendLine($"({EscapePdf(pub)}) Tj");

            sb.AppendLine("ET");
            y -= 16;
        }

        return sb.ToString();
    }

    private static string EscapePdf(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }

    private static string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLen ? text : text[..maxLen] + "..";
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC)
            .Replace("Đ", "D")
            .Replace("đ", "d");
    }
}
