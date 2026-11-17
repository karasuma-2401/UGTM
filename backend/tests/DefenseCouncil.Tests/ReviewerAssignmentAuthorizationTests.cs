using UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class ReviewerAssignmentAuthorizationTests
{
    [Theory]
    [InlineData(false, "Admin", false, "ReviewerAssignment.AuthenticationRequired")]
    [InlineData(true, "Lecturer", true, "ReviewerAssignment.AccessDenied")]
    [InlineData(true, "Faculty", false, "ReviewerAssignment.AccessDenied")]
    public async Task Creation_authorizes_before_input_or_resource_validation(
        bool authenticated, string role, bool manage, string code)
    {
        // No persistence collaborators: reaching one must fail instead of touching a database.
        var handler = new CreateReviewerAssignmentHandler(null!, null!, null!, null!, null!, null!, null!,
            User(authenticated, role, manage ? PermissionCodes.ThesisReviewerAssignmentsManage : null), TimeProvider.System);

        var result = await handler.HandleAsync(new(Guid.Empty, Guid.Empty, Guid.Empty, 0, Guid.Empty));

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
    }

    [Theory]
    [InlineData(false, "Admin", false, "ReviewerAssignment.AuthenticationRequired")]
    [InlineData(true, "Lecturer", true, "ReviewerAssignment.AccessDenied")]
    [InlineData(true, "Faculty", false, "ReviewerAssignment.AccessDenied")]
    public async Task Cancellation_authorizes_before_input_or_resource_validation(
        bool authenticated, string role, bool manage, string code)
    {
        var handler = new CancelReviewerAssignmentHandler(null!, null!, null!, null!, null!, null!, null!,
            User(authenticated, role, manage ? PermissionCodes.ThesisReviewerAssignmentsManage : null), TimeProvider.System);

        var result = await handler.HandleAsync(new(Guid.Empty, 0, null, Guid.Empty));

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
    }

    [Theory]
    [InlineData(false, "Admin", false, "ReviewerAssignment.AuthenticationRequired")]
    [InlineData(true, "Lecturer", true, "ReviewerAssignment.AccessDenied")]
    [InlineData(true, "Faculty", false, "ReviewerAssignment.AccessDenied")]
    public async Task Management_reads_authorize_before_validation_or_database_lookup(
        bool authenticated, string role, bool read, string code)
    {
        var handler = new ReviewerAssignmentReadHandler(null!, null!, null!, null!,
            User(authenticated, role, read ? PermissionCodes.ThesisReviewerAssignmentsRead : null));

        Assert.Equal(code, (await handler.ListAsync(new(GroupId: Guid.Empty))).Error.Code);
        Assert.Equal(code, (await handler.EligibilityAsync(new(Guid.Empty))).Error.Code);
        Assert.Equal(code, (await handler.CandidatesAsync(new(Guid.Empty))).Error.Code);
    }

    private static FakeCurrentUser User(bool authenticated, string role, string? permission) => new()
    {
        IsAuthenticated = authenticated,
        Roles = new HashSet<string> { role },
        Permissions = permission is null ? new HashSet<string>() : new HashSet<string> { permission }
    };
}
