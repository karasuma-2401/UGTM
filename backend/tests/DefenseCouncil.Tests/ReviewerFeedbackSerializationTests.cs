using System.Text.Json;
using System.Text.Json.Serialization;
using UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class ReviewerFeedbackSerializationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("Góp ý đã chốt")]
    public void Student_feedback_is_exactly_one_field_including_explicit_null_even_with_global_null_omission(string? comment)
    {
        var json = JsonSerializer.Serialize(new ReviewerFeedbackDto(comment), new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        using var document = JsonDocument.Parse(json);
        var field = Assert.Single(document.RootElement.EnumerateObject());
        Assert.Equal("comment", field.Name); Assert.Equal(comment, field.Value.GetString());
    }
}
