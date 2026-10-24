using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.TopicRegistrations;
using UGTM.Modules.TopicCatalog.Application.Topics.GetAllTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.GetMyTopics;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;

namespace UGTM.Modules.TopicCatalog.Tests.Infrastructure;

public sealed class TopicQueryReaderTests
{
    private static readonly DateTime BaseTime = new(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetAllTopicsAsync_only_returns_published_topics_and_orders_deterministically()
    {
        var batch1 = Guid.NewGuid();
        var batch2 = Guid.NewGuid();
        var advisorId = Guid.NewGuid();

        var idLow = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var idHigh = Guid.Parse("00000000-0000-0000-0000-000000000099");

        var topics = new List<Topic>
        {
            new()
            {
                Id = idLow,
                BatchId = batch1,
                AdvisorId = advisorId,
                TitleVi = "Ứng dụng Trí tuệ nhân tạo",
                TitleEn = "Artificial Intelligence App",
                Status = TopicStatus.Published,
                StudentNumber = 2,
                CreatedAt = BaseTime // Identical CreatedAt to test tie-breaker ThenByDescending(Id)
            },
            new()
            {
                Id = idHigh,
                BatchId = batch1,
                AdvisorId = advisorId,
                TitleVi = "Phân tích dữ liệu với Trí tuệ nhân tạo",
                TitleEn = "Data Analysis with AI",
                Status = TopicStatus.Published,
                StudentNumber = 2,
                CreatedAt = BaseTime // Identical CreatedAt
            },
            // Topics NOT published must NOT be visible to students in GetAllTopicsAsync:
            new()
            {
                Id = Guid.NewGuid(),
                BatchId = batch1,
                AdvisorId = advisorId,
                TitleVi = "Trí tuệ nhân tạo mới Approved nhưng chưa công bố",
                Status = TopicStatus.Approved,
                StudentNumber = 2,
                CreatedAt = BaseTime.AddMinutes(5)
            },
            new()
            {
                Id = Guid.NewGuid(),
                BatchId = batch1,
                AdvisorId = advisorId,
                TitleVi = "Trí tuệ nhân tạo đang chờ duyệt",
                Status = TopicStatus.Submitted,
                StudentNumber = 2,
                CreatedAt = BaseTime.AddMinutes(6)
            },
            new()
            {
                Id = Guid.NewGuid(),
                BatchId = batch1,
                AdvisorId = advisorId,
                TitleVi = "Trí tuệ nhân tạo bản nháp",
                Status = TopicStatus.Draft,
                StudentNumber = 2,
                CreatedAt = BaseTime.AddMinutes(7)
            },
            new()
            {
                Id = Guid.NewGuid(),
                BatchId = batch1,
                AdvisorId = advisorId,
                TitleVi = "Trí tuệ nhân tạo yêu cầu sửa",
                Status = TopicStatus.NeedRevision,
                StudentNumber = 2,
                CreatedAt = BaseTime.AddMinutes(8)
            },
            new()
            {
                Id = Guid.NewGuid(),
                BatchId = batch1,
                AdvisorId = advisorId,
                TitleVi = "Trí tuệ nhân tạo bị từ chối",
                Status = TopicStatus.Rejected,
                StudentNumber = 2,
                CreatedAt = BaseTime.AddMinutes(9)
            },
            new()
            {
                Id = Guid.NewGuid(),
                BatchId = batch2,
                AdvisorId = advisorId,
                TitleVi = "Trí tuệ nhân tạo đợt khác",
                Status = TopicStatus.Published,
                StudentNumber = 2,
                CreatedAt = BaseTime.AddMinutes(10)
            }
        };

        var reader = TopicQueryReader.CreateInMemory(
            topics,
            new FakeRegistrationContract(),
            new FakeStudentInfoReader());

        var result = await reader.GetAllTopicsAsync(
            new GetAllTopicsQuery("trí tuệ nhân tạo", batch1, 1, 10),
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item =>
        {
            Assert.Equal("Published", item.Status);
            Assert.True(item.IsPublished);
        });
        // Tie-breaker: idHigh must come before idLow when CreatedAt is equal
        Assert.Equal(idHigh, result.Items[0].Id);
        Assert.Equal(idLow, result.Items[1].Id);
    }

    [Theory]
    [InlineData("Empty", 1, "Empty")]
    [InlineData("Registered", 1, "Registered")]
    [InlineData("Full", 1, "Full")]
    [InlineData("All", 3, null)]
    public async Task GetMyTopicsAsync_filters_accurately_by_registration_status_without_mixing_registered_and_full(
        string registrationFilter,
        int expectedCount,
        string? expectedSingleStatus)
    {
        var advisorId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var emptyTopicId = Guid.NewGuid();
        var partialTopicId = Guid.NewGuid();
        var fullTopicId = Guid.NewGuid();

        var topics = new List<Topic>
        {
            new()
            {
                Id = emptyTopicId,
                BatchId = batchId,
                AdvisorId = advisorId,
                TitleVi = "Đề tài trống",
                Status = TopicStatus.Approved,
                StudentNumber = 2,
                CreatedAt = BaseTime.AddMinutes(3)
            },
            new()
            {
                Id = partialTopicId,
                BatchId = batchId,
                AdvisorId = advisorId,
                TitleVi = "Đề tài 1/2 sinh viên",
                Status = TopicStatus.Approved,
                StudentNumber = 2,
                CreatedAt = BaseTime.AddMinutes(2)
            },
            new()
            {
                Id = fullTopicId,
                BatchId = batchId,
                AdvisorId = advisorId,
                TitleVi = "Đề tài đã đủ 2/2 sinh viên",
                Status = TopicStatus.Approved,
                StudentNumber = 2,
                CreatedAt = BaseTime.AddMinutes(1)
            }
        };

        var student1 = Guid.NewGuid();
        var student2 = Guid.NewGuid();
        var student3 = Guid.NewGuid();

        var regContract = new FakeRegistrationContract(new Dictionary<Guid, TopicRegistrationContractDto>
        {
            [partialTopicId] = new(
                Guid.NewGuid(),
                partialTopicId,
                "Registered",
                BaseTime,
                [new TopicMemberContractDto(student1, batchId, BaseTime)]),
            [fullTopicId] = new(
                Guid.NewGuid(),
                fullTopicId,
                "Registered",
                BaseTime,
                [
                    new TopicMemberContractDto(student2, batchId, BaseTime),
                    new TopicMemberContractDto(student3, batchId, BaseTime.AddMinutes(1))
                ])
        });

        var studentReader = new FakeStudentInfoReader(new Dictionary<Guid, StudentInfoContractDto>
        {
            [student1] = new(student1, "sv1@uit.edu.vn", "Sinh Viên 1", "SV001", "0901111111"),
            [student2] = new(student2, "sv2@uit.edu.vn", "Sinh Viên 2", "SV002", "0902222222"),
            [student3] = new(student3, "sv3@uit.edu.vn", "Sinh Viên 3", "SV003", "0903333333")
        });

        var reader = TopicQueryReader.CreateInMemory(topics, regContract, studentReader);

        var result = await reader.GetMyTopicsAsync(
            new GetMyTopicsQuery(null, batchId, "Approved", registrationFilter, null, 1, 10),
            advisorId,
            isPrivileged: false,
            CancellationToken.None);

        Assert.Equal(expectedCount, result.TotalCount);
        Assert.Equal(expectedCount, result.Items.Count);

        if (expectedSingleStatus is not null)
        {
            var item = Assert.Single(result.Items);
            Assert.Equal(expectedSingleStatus, item.RegistrationStatus);
        }
    }

    [Fact]
    public async Task GetMyTopicsAsync_treats_group_with_zero_members_as_empty_and_null_group()
    {
        var advisorId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var topics = new List<Topic>
        {
            new()
            {
                Id = topicId,
                BatchId = batchId,
                AdvisorId = advisorId,
                TitleVi = "Đề tài có nhóm rỗng",
                Status = TopicStatus.Approved,
                StudentNumber = 2,
                CreatedAt = BaseTime
            }
        };

        var regContract = new FakeRegistrationContract(new Dictionary<Guid, TopicRegistrationContractDto>
        {
            [topicId] = new(Guid.NewGuid(), topicId, "Registered", BaseTime, [])
        });

        var reader = TopicQueryReader.CreateInMemory(topics, regContract, new FakeStudentInfoReader());

        var result = await reader.GetMyTopicsAsync(
            new GetMyTopicsQuery(null, null, null, null, null, 1, 10),
            advisorId,
            isPrivileged: false,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(0, item.RegisteredStudentsCount);
        Assert.Equal(2, item.RemainingSlots);
        Assert.Equal("Empty", item.RegistrationStatus);
        Assert.False(item.HasRegistration);
        Assert.True(item.IsAvailable);
        Assert.Null(item.Group);
    }

    [Fact]
    public async Task GetTopicTrackingDetailAsync_returns_registered_status_and_orders_students_by_joined_at()
    {
        var advisorId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var topics = new List<Topic>
        {
            new()
            {
                Id = topicId,
                BatchId = batchId,
                AdvisorId = advisorId,
                TitleVi = "Đề tài chi tiết",
                Status = TopicStatus.Published,
                StudentNumber = 2,
                CreatedAt = BaseTime
            }
        };

        var studentLater = Guid.NewGuid();
        var studentEarlier = Guid.NewGuid();

        var regContract = new FakeRegistrationContract(new Dictionary<Guid, TopicRegistrationContractDto>
        {
            [topicId] = new(
                Guid.NewGuid(),
                topicId,
                "Registered",
                BaseTime,
                [
                    new TopicMemberContractDto(studentLater, batchId, BaseTime.AddMinutes(10)),
                    new TopicMemberContractDto(studentEarlier, batchId, BaseTime.AddMinutes(1))
                ])
        });

        var studentReader = new FakeStudentInfoReader(new Dictionary<Guid, StudentInfoContractDto>
        {
            [studentEarlier] = new(studentEarlier, "first@uit.edu.vn", "Sinh Viên Vào Trước", "SV001", "0901"),
            [studentLater] = new(studentLater, "second@uit.edu.vn", "Sinh Viên Vào Sau", "SV002", "0902")
        });

        var reader = TopicQueryReader.CreateInMemory(topics, regContract, studentReader);

        var detail = await reader.GetTopicTrackingDetailAsync(topicId, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Full", detail.RegistrationStatus);
        Assert.True(detail.IsPublished);
        Assert.Equal(2, detail.RegisteredStudentsCount);
        Assert.Equal(0, detail.RemainingSlots);
        Assert.NotNull(detail.Group);
        Assert.Equal(2, detail.Group.Students.Count);
        // Must be ordered by JoinedAt ascending!
        Assert.Equal(studentEarlier, detail.Group.Students[0].StudentId);
        Assert.Equal(studentLater, detail.Group.Students[1].StudentId);
    }

    private sealed class FakeRegistrationContract(
        IReadOnlyDictionary<Guid, TopicRegistrationContractDto>? data = null) : IThesisRegistrationContract
    {
        private readonly IReadOnlyDictionary<Guid, TopicRegistrationContractDto> _data =
            data ?? new Dictionary<Guid, TopicRegistrationContractDto>();

        public Task<IReadOnlyDictionary<Guid, TopicRegistrationContractDto>> GetRegistrationsByTopicIdsAsync(
            IEnumerable<Guid> topicIds,
            CancellationToken cancellationToken = default)
        {
            var result = topicIds
                .Distinct()
                .Where(_data.ContainsKey)
                .ToDictionary(id => id, id => _data[id]);
            return Task.FromResult<IReadOnlyDictionary<Guid, TopicRegistrationContractDto>>(result);
        }

        public Task<TopicRegistrationContractDto?> GetRegistrationByTopicIdAsync(
            Guid topicId,
            CancellationToken cancellationToken = default)
        {
            _data.TryGetValue(topicId, out var dto);
            return Task.FromResult(dto);
        }
    }

    private sealed class FakeStudentInfoReader(
        IReadOnlyDictionary<Guid, StudentInfoContractDto>? data = null) : IStudentInfoReader
    {
        private readonly IReadOnlyDictionary<Guid, StudentInfoContractDto> _data =
            data ?? new Dictionary<Guid, StudentInfoContractDto>();

        public Task<IReadOnlyDictionary<Guid, StudentInfoContractDto>> GetStudentsByIdsAsync(
            IEnumerable<Guid> studentIds,
            CancellationToken cancellationToken = default)
        {
            var result = studentIds
                .Distinct()
                .Where(_data.ContainsKey)
                .ToDictionary(id => id, id => _data[id]);
            return Task.FromResult<IReadOnlyDictionary<Guid, StudentInfoContractDto>>(result);
        }

        public Task<StudentInfoContractDto?> GetStudentByIdOrCodeAsync(
            Guid? studentId,
            string? userCode,
            CancellationToken cancellationToken = default)
        {
            if (studentId.HasValue && _data.TryGetValue(studentId.Value, out var byId))
            {
                return Task.FromResult<StudentInfoContractDto?>(byId);
            }

            if (!string.IsNullOrWhiteSpace(userCode))
            {
                var byCode = _data.Values.FirstOrDefault(s => string.Equals(s.UserCode, userCode.Trim(), StringComparison.OrdinalIgnoreCase));
                return Task.FromResult<StudentInfoContractDto?>(byCode);
            }

            return Task.FromResult<StudentInfoContractDto?>(null);
        }
    }
}
