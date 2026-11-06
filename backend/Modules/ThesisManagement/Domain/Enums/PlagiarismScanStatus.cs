namespace UGTM.Modules.ThesisManagement.Domain.Enums;

public enum PlagiarismScanStatus
{
    PendingSubmission = 1,
    Queued = 2,
    Processing = 3,
    WaitingForCompute = 4,
    Completed = 5,
    ComputeUnavailable = 6,
    Failed = 7,
    UnsupportedFormat = 8
}

