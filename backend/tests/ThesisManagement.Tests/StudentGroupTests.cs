using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Tests;

public class StudentGroupTests
{
    private static readonly DateTime Timestamp = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_group_rejects_a_third_student()
    {
        var group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), Timestamp);
        var batch = Guid.NewGuid();
        group.AddMember(Guid.NewGuid(), batch, Timestamp);
        group.AddMember(Guid.NewGuid(), batch, Timestamp);

        Assert.Throws<InvalidOperationException>(() => group.AddMember(Guid.NewGuid(), batch, Timestamp));
        Assert.Equal(2, group.Members.Count);
    }

    [Fact]
    public void A_group_rejects_the_same_student_twice()
    {
        var group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), Timestamp);
        var batch = Guid.NewGuid();
        var student = Guid.NewGuid();
        group.AddMember(student, batch, Timestamp);

        Assert.Throws<InvalidOperationException>(() => group.AddMember(student, batch, Timestamp));
        Assert.Single(group.Members);
    }

    [Fact]
    public void Group_members_must_belong_to_the_same_batch()
    {
        var group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), Timestamp);
        group.AddMember(Guid.NewGuid(), Guid.NewGuid(), Timestamp);

        Assert.Throws<InvalidOperationException>(() => group.AddMember(Guid.NewGuid(), Guid.NewGuid(), Timestamp));
    }

    [Fact]
    public void A_group_follows_the_two_review_gate_transitions()
    {
        var group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), Timestamp);

        Assert.True(group.TrySubmitSyllabus());
        Assert.Equal(StudentGroupStatus.SyllabusSubmitted, group.Status);
        Assert.True(group.TryApproveSyllabus());
        Assert.Equal(StudentGroupStatus.InProgress, group.Status);
        Assert.True(group.TrySubmitMidtermReport());
        Assert.Equal(StudentGroupStatus.MidtermSubmitted, group.Status);
        Assert.True(group.TryApproveMidtermReport());
        Assert.Equal(StudentGroupStatus.InProgress, group.Status);
    }

    [Fact]
    public void A_group_cannot_skip_the_syllabus_gate_or_change_membership_after_submission()
    {
        var group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), Timestamp);
        var batch = Guid.NewGuid();

        Assert.False(group.TrySubmitMidtermReport());
        Assert.True(group.TrySubmitSyllabus());
        Assert.Throws<InvalidOperationException>(() => group.AddMember(Guid.NewGuid(), batch, Timestamp));
    }

    [Fact]
    public void A_group_cannot_be_stopped_twice()
    {
        var group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), Timestamp);

        Assert.True(group.TryStop());
        Assert.Equal(StudentGroupStatus.Stopped, group.Status);
        Assert.False(group.TryStop());
    }
}
