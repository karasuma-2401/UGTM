namespace UGTM.Modules.ThesisManagement.Domain.Enums;

public enum RenameRequestStatus
{
    Created,
    AdvisorConfirmed,
    FacultyApproved,
    AcademicAffairsApproved,
    RejectedByAdvisor,
    RejectedByFaculty,
    RejectedByAA,
    Completed,
    Closed
}
