using UGTM.BuildingBlocks.Application.Caching;

namespace UGTM.Modules.DefenseCouncil.Application.Common;

/// <summary>
/// Quản lý tập trung quy ước đặt tên khóa Cache, Version Scopes, TTL
/// và các hàm vô hiệu hóa cache (Cache Invalidation) cho module DefenseCouncil.
/// </summary>
public static class DefenseCouncilCacheKeys
{
    public const string GlobalScope = "dc:global";

    public static readonly TimeSpan ResultsTtl = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan GroupBatchMapTtl = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan OverviewTtl = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ScheduleTtl = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MinuteWorkspaceTtl = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan DiscrepancyTtl = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan GradingRubricTtl = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan MemberCandidatesTtl = TimeSpan.FromMinutes(3);

    public static string BatchScope(Guid batchId) => $"dc:batch:{batchId:N}";

    public static string CouncilScope(Guid councilId) => $"dc:council:{councilId:N}";

    public static string AssignmentScope(Guid assignmentId) => $"dc:assignment:{assignmentId:N}";

    public static string AcademicResults(Guid batchId, string phase, string batchVer, string globalVer)
        => $"dc:academic-results:{batchId:N}:{phase}:{batchVer}:{globalVer}";

    public static string GroupBatchMap(Guid groupId, string? globalVer = null)
        => $"dc:group-batch:{groupId:N}:{globalVer}";

    public static string CouncilsOverview(Guid? batchId, string statusKey, string batchVer, string globalVer)
        => $"dc:councils-overview:{(batchId.HasValue ? batchId.Value.ToString("N") : "all")}:{statusKey}:{batchVer}:{globalVer}";

    public static string CouncilSchedule(Guid councilId, string councilVer, string batchVer, string globalVer)
        => $"dc:council-schedule:{councilId:N}:{councilVer}:{batchVer}:{globalVer}";

    public static string CouncilMinuteWorkspace(Guid assignmentId, string assignVer, string councilVer, string globalVer)
        => $"dc:minute-workspace:{assignmentId:N}:{assignVer}:{councilVer}:{globalVer}";

    public static string CouncilDiscrepancy(Guid assignmentId, string assignVer, string councilVer, string globalVer)
        => $"dc:discrepancy:{assignmentId:N}:{assignVer}:{councilVer}:{globalVer}";

    public static string GradingRubric(Guid batchId, string batchVer, string? globalVer = null)
        => $"dc:grading-rubric:{batchId:N}:{batchVer}:{globalVer}";

    public static string CouncilMemberCandidates(Guid councilId, string keywordKey, string councilVer, string batchVer, string globalVer)
        => $"dc:member-candidates:{councilId:N}:{keywordKey}:{councilVer}:{batchVer}:{globalVer}";

    public static async Task InvalidateBatchAsync(
        ICacheService? cache,
        Guid batchId,
        CancellationToken ct = default)
    {
        if (cache is null)
        {
            return;
        }

        if (batchId != Guid.Empty)
        {
            await cache.BumpVersionAsync(BatchScope(batchId), ct);
        }

        await cache.BumpVersionAsync(GlobalScope, ct);
    }

    public static async Task InvalidateCouncilAsync(
        ICacheService? cache,
        Guid councilId,
        Guid? batchId = null,
        CancellationToken ct = default)
    {
        if (cache is null)
        {
            return;
        }

        if (councilId != Guid.Empty)
        {
            await cache.BumpVersionAsync(CouncilScope(councilId), ct);
        }

        if (batchId.HasValue && batchId.Value != Guid.Empty)
        {
            await cache.BumpVersionAsync(BatchScope(batchId.Value), ct);
        }

        await cache.BumpVersionAsync(GlobalScope, ct);
    }

    public static async Task InvalidateAssignmentAsync(
        ICacheService? cache,
        Guid assignmentId,
        Guid? councilId = null,
        Guid? batchId = null,
        CancellationToken ct = default)
    {
        if (cache is null)
        {
            return;
        }

        if (assignmentId != Guid.Empty)
        {
            await cache.BumpVersionAsync(AssignmentScope(assignmentId), ct);
        }

        if (councilId.HasValue && councilId.Value != Guid.Empty)
        {
            await cache.BumpVersionAsync(CouncilScope(councilId.Value), ct);
        }

        if (batchId.HasValue && batchId.Value != Guid.Empty)
        {
            await cache.BumpVersionAsync(BatchScope(batchId.Value), ct);
        }

        await cache.BumpVersionAsync(GlobalScope, ct);
    }

    public static async Task InvalidateGlobalAsync(
        ICacheService? cache,
        CancellationToken ct = default)
    {
        if (cache is null)
        {
            return;
        }

        await cache.BumpVersionAsync(GlobalScope, ct);
    }
}
