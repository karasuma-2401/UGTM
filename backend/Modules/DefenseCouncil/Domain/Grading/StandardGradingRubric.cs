using UGTM.Modules.DefenseCouncil.Domain.Entities;

namespace UGTM.Modules.DefenseCouncil.Domain.Grading;

/// <summary>The fixed UIT rubric. Database IDs only bind scores to these standard criteria.</summary>
public static class StandardGradingRubric
{
    public static IReadOnlyList<GradingCriteria> CreateCriteria(Guid batchId)
    {
        if (batchId == Guid.Empty) throw new ArgumentException("Batch identifier must be nonempty.", nameof(batchId));

        GradingCriteria Criterion(string code, string name, decimal max, int order, string section,
            string sectionName, int sectionOrder, string elo) => new()
            {
                Id = Guid.NewGuid(),
                BatchId = batchId,
                CriteriaCode = code,
                CriteriaName = name,
                MaxScore = max,
                OrderIndex = order,
                SectionCode = section,
                SectionName = sectionName,
                SectionOrder = sectionOrder,
                Elo = elo
            };

        return
        [
            Criterion("1.1", "Tính mới và độ phức tạp", 1.5m, 1, "1", "Ý nghĩa khoa học, giá trị thực tiễn", 1, "LO2"),
            Criterion("1.2", "Tính thực tiễn và đóng góp", 1.5m, 2, "1", "Ý nghĩa khoa học, giá trị thực tiễn", 1, "LO3"),
            Criterion("2.1", "Khả năng tìm hiểu, phân tích yêu cầu và thiết kế hệ thống", 1m, 3, "2", "Vận dụng kiến thức nền tảng và chuyên sâu ngành KTPM", 2, "LO3"),
            Criterion("2.2", "Khả năng hiện thực hóa, kiểm thử và vận hành hệ thống, đánh giá giải pháp", 1m, 4, "2", "Vận dụng kiến thức nền tảng và chuyên sâu ngành KTPM", 2, "LO4"),
            Criterion("2.3", "Học hỏi và cập nhật kiến thức, công nghệ mới, khả năng học tập suốt đời", 1m, 5, "2", "Vận dụng kiến thức nền tảng và chuyên sâu ngành KTPM", 2, "LO3"),
            Criterion("3.1", "Nội dung trình bày cuốn báo và sản phẩm (viết)", 1m, 6, "3", "Trình bày kết quả", 3, "LO6, LO8"),
            Criterion("3.2", "Kỹ năng trình bày (nói)", 1m, 7, "3", "Trình bày kết quả", 3, "LO6"),
            Criterion("3.3", "Trả lời các câu hỏi", 1m, 8, "3", "Trình bày kết quả", 3, "LO8"),
            Criterion("4.1", "Lập kế hoạch và quản lý thời gian, công việc của cá nhân hoặc đội nhóm", 0.5m, 9, "4", "Kỹ năng mềm", 4, "LO5, LO7"),
            Criterion("4.2", "Kỹ năng tìm hiểu, đọc hiểu tài liệu ngoại ngữ và khả năng tư duy, giải quyết vấn đề", 0.5m, 10, "4", "Kỹ năng mềm", 4, "LO4, LO6")
        ];
    }
}
