namespace UGTM.Modules.Identity.Contracts.Authorization;

public static class PermissionCodes
{
    public const string AcademicBatchesRead = "academic.batches.read";
    public const string AcademicBatchesManage = "academic.batches.manage";
    public const string AcademicScoreConfigurationsRead = "academic.score-config.read";
    public const string AcademicScoreConfigurationsManage = "academic.score-config.manage";

    public const string ThesisReviewGateConfigurationsManage = "thesis.review-gate-config.manage";
    public const string ThesisReviewGatesReview = "thesis.review-gates.review";
    public const string ThesisReviewerAssignmentsRead = "thesis.reviewer-assignments.read";
    public const string ThesisReviewerAssignmentsManage = "thesis.reviewer-assignments.manage";
    public const string ThesisReviewerEvaluationsRead = "thesis.reviewer-evaluations.read";

    public const string IdentityUsersRead = "identity.users.read";
    public const string IdentityUsersManage = "identity.users.manage";
    public const string IdentityUsersImport = "identity.users.import";
    public const string IdentityPermissionsRead = "identity.permissions.read";
    public const string IdentityRolesManage = "identity.roles.manage";
    public const string IdentitySessionsRevoke = "identity.sessions.revoke";
}
