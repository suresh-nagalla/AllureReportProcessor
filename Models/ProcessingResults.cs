namespace AllureReportProcessor.Models;

public class TestResult
{
    public string SuiteName { get; set; } = string.Empty;
    public string CaseTags { get; set; } = string.Empty;
    public string TestCaseName { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string FailingStep { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public string ScreenshotPath { get; set; } = string.Empty;
    public string ParametersKey { get; set; } = string.Empty;
}

public class StepTiming
{
    public string JsonFile { get; set; } = string.Empty;
    public string SuiteName { get; set; } = string.Empty;
    public string CaseTags { get; set; } = string.Empty;
    public string TestCaseName { get; set; } = string.Empty;
    public string StepType { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public string Duration { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StepCategory { get; set; } = string.Empty;
    public string ParametersKey { get; set; } = string.Empty;
}

public class FailedFile
{
    public string FilePath { get; set; } = string.Empty;
    public string ErrorReason { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
}

public class Attachment
{
    public string Location { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public string UID { get; set; } = string.Empty;
}

public class ProcessingResults
{
    public List<TestResult> TestResults { get; set; } = new();
    public List<StepTiming> StepTimings { get; set; } = new();
    public List<FailedFile> FailedFiles { get; set; } = new();
    public int ProcessedCount { get; set; }
    public int FailedCount { get; set; }
    public int ScreenshotsCopied { get; set; }
    public int StepsProcessed { get; set; }
}