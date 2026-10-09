using UGTM.Modules.ThesisManagement.Domain.Entities;

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
}
