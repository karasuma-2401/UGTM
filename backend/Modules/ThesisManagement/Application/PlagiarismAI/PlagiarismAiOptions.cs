namespace UGTM.Modules.ThesisManagement.Application.PlagiarismAI;

public sealed class PlagiarismAiOptions
{
    public const string SectionName = "PlagiarismAi";

    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultCorpus { get; set; } = "uit-kltn-library-v1";
    public int TimeoutSeconds { get; set; } = 60;
    public int PollingIntervalSeconds { get; set; } = 30;
    public int MaxColabWaitingMinutes { get; set; } = 120;
    public int BatchResumeSize { get; set; } = 20;
    public string? WorkerPartition { get; set; }

    public string? GetNormalizedWorkerPartition()
        => string.IsNullOrWhiteSpace(WorkerPartition) ? null : WorkerPartition.Trim();
}

