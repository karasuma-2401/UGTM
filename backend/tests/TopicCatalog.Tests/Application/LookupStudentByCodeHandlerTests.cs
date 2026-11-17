using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.TopicCatalog.Application.Topics.LookupStudentByCode;
using UGTM.Modules.TopicCatalog.Tests.Fakes;

namespace UGTM.Modules.TopicCatalog.Tests.Application;

public sealed class LookupStudentByCodeHandlerTests
{
    [Fact]
    public async Task Lecturer_and_admin_can_lookup_active_student_name_with_normalized_code()
    {
        var reader = new FakeStudentInfoReader(new StudentInfoContractDto(
            Guid.NewGuid(),
            "24521603@gm.uit.edu.vn",
            "Nguyễn Văn A",
            "24521603",
            "0901234567"));

        var lecturerResult = await new LookupStudentByCodeHandler(reader, FakeCurrentUser.AsLecturer())
            .HandleAsync("  sv24521603  ");
        Assert.Equal("SV24521603", reader.LastRequestedCode);

        var okResult = await new LookupStudentByCodeHandler(reader, FakeCurrentUser.AsAdmin())
            .HandleAsync(" 24521603 ");
        Assert.True(okResult.IsSuccess);
        Assert.Equal(new TopicStudentLookupDto("24521603", "Nguyễn Văn A"), okResult.Value);
        Assert.False(lecturerResult.IsSuccess);
    }

    [Fact]
    public async Task Unauthenticated_wrong_role_and_invalid_code_never_query_student_reader()
    {
        foreach (var user in new[] { FakeCurrentUser.Unauthenticated(), FakeCurrentUser.AsStudent(), FakeCurrentUser.AsFaculty() })
        {
            var reader = new FakeStudentInfoReader(null);
            var result = await new LookupStudentByCodeHandler(reader, user).HandleAsync("24521603");
            Assert.False(result.IsSuccess);
            Assert.Null(reader.LastRequestedCode);
        }

        foreach (var badCode in new string?[] { null, "", "   ", new string('A', 21) })
        {
            var reader = new FakeStudentInfoReader(null);
            var result = await new LookupStudentByCodeHandler(reader, FakeCurrentUser.AsLecturer())
                .HandleAsync(badCode);
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.Validation, result.Error.Type);
            Assert.Null(reader.LastRequestedCode);
        }
    }

    [Fact]
    public async Task Unknown_student_code_returns_not_found()
    {
        var reader = new FakeStudentInfoReader(null);
        var result = await new LookupStudentByCodeHandler(reader, FakeCurrentUser.AsLecturer())
            .HandleAsync("24529999");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Topics.StudentNotFound", result.Error.Code);
    }

    private sealed class FakeStudentInfoReader(StudentInfoContractDto? match) : IStudentInfoReader
    {
        public string? LastRequestedCode { get; private set; }

        public Task<IReadOnlyDictionary<Guid, StudentInfoContractDto>> GetStudentsByIdsAsync(
            IEnumerable<Guid> studentIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, StudentInfoContractDto>>(new Dictionary<Guid, StudentInfoContractDto>());

        public Task<StudentInfoContractDto?> GetStudentByIdOrCodeAsync(
            Guid? studentId,
            string? userCode,
            CancellationToken cancellationToken = default)
        {
            LastRequestedCode = userCode;
            if (match is not null && string.Equals(match.UserCode, userCode, StringComparison.Ordinal))
            {
                return Task.FromResult<StudentInfoContractDto?>(match);
            }
            return Task.FromResult<StudentInfoContractDto?>(null);
        }
    }
}
