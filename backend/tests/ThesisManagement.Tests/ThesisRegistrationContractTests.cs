using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class ThesisRegistrationContractTests
{
    private static readonly Guid Topic1Id = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid Topic2Id = Guid.Parse("70000000-0000-0000-0000-000000000002");
    private static readonly Guid BatchId = Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid Student1Id = Guid.Parse("90000000-0000-0000-0000-000000000001");
    private static readonly Guid Student2Id = Guid.Parse("90000000-0000-0000-0000-000000000002");

    private static void SetStatus(StudentGroup group, StudentGroupStatus status)
    {
        typeof(StudentGroup)
            .GetProperty(nameof(StudentGroup.Status))!
            .SetValue(group, status);
    }

    [Fact]
    public async Task GetRegistrationsByTopicIds_EmptyTopicIds_ReturnsEmptyDictionary()
    {
        var group = new StudentGroup(Guid.NewGuid(), Topic1Id, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        var contract = ThesisRegistrationContract.CreateInMemory([group]);

        var result = await contract.GetRegistrationsByTopicIdsAsync([]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRegistrationsByTopicIds_ExcludesStoppedGroups()
    {
        var stoppedGroup = new StudentGroup(Guid.NewGuid(), Topic1Id, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        stoppedGroup.AddMember(Student1Id, BatchId, new DateTime(2026, 3, 1, 1, 0, 0, DateTimeKind.Utc));
        SetStatus(stoppedGroup, StudentGroupStatus.Stopped);

        var contract = ThesisRegistrationContract.CreateInMemory([stoppedGroup]);

        var result = await contract.GetRegistrationsByTopicIdsAsync([Topic1Id]);
        var single = await contract.GetRegistrationByTopicIdAsync(Topic1Id);

        Assert.Empty(result);
        Assert.Null(single);
    }

    [Fact]
    public async Task GetRegistrationsByTopicIds_TopicWithStoppedAndActiveGroup_ReturnsActiveGroup()
    {
        var stoppedGroup = new StudentGroup(Guid.NewGuid(), Topic1Id, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        stoppedGroup.AddMember(Student1Id, BatchId, new DateTime(2026, 3, 1, 1, 0, 0, DateTimeKind.Utc));
        SetStatus(stoppedGroup, StudentGroupStatus.Stopped);

        var activeGroup = new StudentGroup(Guid.NewGuid(), Topic1Id, new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc));
        activeGroup.AddMember(Student2Id, BatchId, new DateTime(2026, 3, 2, 1, 0, 0, DateTimeKind.Utc));

        var contract = ThesisRegistrationContract.CreateInMemory([stoppedGroup, activeGroup]);

        var dict = await contract.GetRegistrationsByTopicIdsAsync([Topic1Id]);
        var single = await contract.GetRegistrationByTopicIdAsync(Topic1Id);

        Assert.Single(dict);
        Assert.Equal(activeGroup.Id, dict[Topic1Id].GroupId);
        Assert.NotNull(single);
        Assert.Equal(activeGroup.Id, single.GroupId);
    }

    [Fact]
    public async Task GetRegistrationByTopicId_DeterministicTieBreakerByCreatedAtAndId()
    {
        var sameCreatedAt = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc);
        var lowerIdGroup = new StudentGroup(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Topic1Id,
            sameCreatedAt);
        var higherIdGroup = new StudentGroup(
            Guid.Parse("10000000-0000-0000-0000-000000000009"),
            Topic1Id,
            sameCreatedAt);

        var contract = ThesisRegistrationContract.CreateInMemory([lowerIdGroup, higherIdGroup]);

        var dict = await contract.GetRegistrationsByTopicIdsAsync([Topic1Id]);
        var single = await contract.GetRegistrationByTopicIdAsync(Topic1Id);

        Assert.Equal(higherIdGroup.Id, dict[Topic1Id].GroupId);
        Assert.NotNull(single);
        Assert.Equal(higherIdGroup.Id, single.GroupId);
    }

    [Fact]
    public async Task GetRegistration_OrdersMembersByJoinedAtThenStudentId()
    {
        var group = new StudentGroup(Guid.NewGuid(), Topic2Id, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        var laterJoined = new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc);
        var earlierJoined = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

        group.AddMember(Student2Id, BatchId, laterJoined);
        group.AddMember(Student1Id, BatchId, earlierJoined);

        var contract = ThesisRegistrationContract.CreateInMemory([group]);

        var single = await contract.GetRegistrationByTopicIdAsync(Topic2Id);
        var dict = await contract.GetRegistrationsByTopicIdsAsync([Topic2Id]);

        Assert.NotNull(single);
        Assert.Equal([Student1Id, Student2Id], single.Members.Select(m => m.StudentId).ToArray());
        Assert.Equal([Student1Id, Student2Id], dict[Topic2Id].Members.Select(m => m.StudentId).ToArray());
    }
}
