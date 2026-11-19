using System.Text.Json.Serialization;

namespace UGTM.Modules.DefenseCouncil.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CouncilMemberRole
{
    Chairman,
    Secretary,
    Member
}
