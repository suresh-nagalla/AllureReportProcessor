namespace AllureReportProcessor.Models;

public class ProcessingConfig
{
    public string AllureReportPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public bool ExcelOutput { get; set; } = false;
    public bool HtmlOnly { get; set; } = false;
}

// Summary Models for Structured Export
/// <summary>
/// Represents the complete processing summary with both overview metrics and detailed step analysis
/// </summary>
public class ProcessingSummary
{
    public SummaryOverview Overview { get; set; } = new();
    public List<TopStepGroup> TopSlowSteps { get; set; } = new();
    public List<TestResult> TestResults { get; set; } = new(); // Add this property
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public string ReportVersion { get; set; } = "1.0";
}

/// <summary>
/// Contains high-level processing statistics and metrics
/// </summary>
public class SummaryOverview
{
    public int TotalFilesFound { get; set; }
    public int SuccessfullyProcessed { get; set; }
    public int FailedToProcess { get; set; }
    public int ScreenshotsCopied { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public int BrokenTests { get; set; }
    public int TotalStepsAnalyzed { get; set; }
    public string? TotalExecutionTime { get; set; }

    // Calculated properties
    public double SuccessRate => TotalFilesFound > 0 ? (double)SuccessfullyProcessed / TotalFilesFound * 100 : 0;
    public double TestPassRate => (PassedTests + FailedTests + BrokenTests) > 0 ? (double)PassedTests / (PassedTests + FailedTests + BrokenTests) * 100 : 0;
    public int TotalTests => PassedTests + FailedTests + BrokenTests;
}

/// <summary>
/// Represents a group of steps with aggregated performance metrics
/// </summary>
public class TopStepGroup
{
    public int Rank { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string TruncatedStepName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double AvgDurationMs { get; set; }
    public long MinDurationMs { get; set; }
    public long MaxDurationMs { get; set; }
    public long TotalDurationMs { get; set; }
    public int FailedCount { get; set; }
    public double FailRate { get; set; }

    // Human-readable duration strings
    public string AvgDurationReadable { get; set; } = string.Empty;
    public string MinDurationReadable { get; set; } = string.Empty;
    public string MaxDurationReadable { get; set; } = string.Empty;
    public string TotalDurationReadable { get; set; } = string.Empty;

    // Performance indicators
    public string PerformanceCategory { get; set; } = string.Empty; // Critical, High, Medium, Low
    public string ReliabilityCategory { get; set; } = string.Empty; // Unreliable, Poor, Good, Excellent
}