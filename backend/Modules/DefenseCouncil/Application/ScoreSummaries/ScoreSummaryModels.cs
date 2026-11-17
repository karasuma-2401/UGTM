using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Domain.Entities;

namespace UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;

public static class ScoreSummaryErrors
{
    public static Error InvalidRequest => Error.Validation("ScoreSummary.InvalidRequest", "Yêu cầu không hợp lệ.");
    public static Error AuthenticationRequired => Error.Unauthorized("ScoreSummary.AuthenticationRequired", "Bạn cần đăng nhập.");
    public static Error AccessDenied => Error.Forbidden("ScoreSummary.AccessDenied", "Bạn không có quyền tra cứu hoặc tính điểm.");
    public static Error NotFound => Error.NotFound("ScoreSummary.NotFound", "Không tìm thấy bảng điểm.");
    public static Error Conflict(string reason) => Error.Conflict("ScoreSummary." + reason, reason switch
    {
        "AlreadyPublished" => "Điểm đã công bố và bị khóa.",
        "VersionConflict" => "Bảng điểm hoặc cấu hình đã đổi; hãy tải lại.",
        "IdempotencyConflict" => "Key đã dùng cho ý định khác.",
        "ConcurrencyConflict" => "Dữ liệu đang thay đổi; vui lòng thử lại.",
        _ => "Chưa đủ điều kiện hoặc nguồn/cấu hình tính điểm không hợp lệ: " + reason
    });
}
public sealed record ScoreComponentDto(string ComponentCode, string ComponentName, string Source, decimal Score,
    decimal Weight, bool IsIgnoredInCalculation, string? ExclusionReason, string SourceReferences);
public sealed record ScoreRevisionDto(Guid RevisionId, int RevisionNo, Guid ConfigId, int ConfigVersionNo, decimal FinalScore,
    decimal FinalScoreRaw, decimal TotalWeight, Guid CalculatedBy, DateTime CalculatedAt, string InputFingerprint, string SourceSnapshot, string ConfigSnapshot);
public sealed record ScoreSummaryDto(Guid SummaryId, Guid GroupId, Guid BatchId, string Status, decimal? FinalScore, decimal? TotalWeight,
    string? CalculationFormula, int? ConfigVersionNo, DateTime? CalculatedAt, Guid? CalculatedBy, bool IsStale, uint SummaryVersion,
    bool IsLegacy, Guid? ActiveRevisionId, Guid? PublishedRevisionId, Guid? PublishedBy, DateTime? PublishedAt,
    IReadOnlyList<ScoreComponentDto> Components, IReadOnlyList<ScoreRevisionDto> Revisions, string? SourceSnapshot, string? ConfigSnapshot);
public sealed record ScoreReadinessDto(Guid GroupId, bool IsReadyForCalculation, bool IsReadyForPublication, bool IsCouncilMinuteLocked,
    bool HasAllRequiredScores, bool IsArchiveConfirmed, bool HasActiveScoreConfig, uint? SummaryVersion, IReadOnlyList<string> BlockingReasons);
public sealed record ScoreListItemDto(Guid GroupId, string TopicTitle, Guid? SummaryId, uint? SummaryVersion, string Status,
    decimal? FinalScore, bool IsLegacy, bool IsStale, bool IsReadyForCalculation, bool IsReadyForPublication, DateTime? CalculatedAt, DateTime? PublishedAt);
public sealed record ScorePageDto(IReadOnlyList<ScoreListItemDto> Items, int TotalCount, int PageNumber, int PageSize);
public sealed record ScoreFacts(IReadOnlyList<AdvisorEvaluation> Advisors, IReadOnlyList<ReviewerAssignment> Reviewers,
    IReadOnlyList<ReviewerEvaluation> Evaluations, IReadOnlyList<CouncilAssignment> Assignments, IReadOnlyList<Council> Councils,
    IReadOnlyList<CouncilMember> Members, IReadOnlyList<CouncilMinute> Minutes, IReadOnlyList<CouncilScore> Scores,
    IReadOnlyList<EvaluationDetail> Details);
public sealed record ScoreHistory(ScoreSummary Summary, IReadOnlyList<ScoreSummaryRevision> Revisions, IReadOnlyList<ScoreSummaryComponent> Components);
public interface IScoreSummaryStore
{
    Task KeyLockAsync(Guid actor, string operation, Guid key, CancellationToken token);
    Task<ScoreSummaryIdempotency?> ReceiptAsync(Guid actor, string operation, Guid key, CancellationToken token);
    Task LockAdvisorsAsync(IReadOnlyCollection<Guid> groups, CancellationToken token);
    Task<IReadOnlyDictionary<Guid, ScoreFacts>> FactsAsync(IReadOnlyCollection<Guid> groups, bool locked, CancellationToken token);
    Task<ScoreSummary?> SummaryAsync(Guid group, bool locked, CancellationToken token);
    Task<IReadOnlyDictionary<Guid, ScoreHistory>> HistoriesAsync(IReadOnlyCollection<Guid> groups, CancellationToken token);
    Task<Guid?> AssignmentGroupAsync(Guid assignment, CancellationToken token);
    Task<IReadOnlyList<Guid>> CouncilGroupsAsync(Guid council, CancellationToken token);
    Task<bool> PublishedAsync(IReadOnlyCollection<Guid> groups, CancellationToken token);
    void Add(ScoreSummary summary);
    void Add(ScoreSummaryRevision revision, IReadOnlyList<ScoreSummaryComponent> components);
    void Add(ScoreSummaryIdempotency receipt);
}
