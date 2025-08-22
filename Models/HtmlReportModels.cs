using AllureReportProcessor.Utils;

namespace AllureReportProcessor.Models;

/// <summary>
/// Data structure for HTML report generation
/// </summary>
public class HtmlReportData
{
    public ProcessingSummary Summary { get; set; } = new();
    public List<TestResult> TestResults { get; set; } = new();
    public List<SuiteStatistic> SuiteStatistics { get; set; } = new();
    public List<SlowTestInfo> SlowRunningTests { get; set; } = new();
    public List<TopStepGroup> TopSlowSteps { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Enhanced data structure for HTML report generation with quality analysis
/// </summary>
public class EnhancedHtmlReportData : HtmlReportData
{
    public QualityAnalysisResult? QualityAnalysis { get; set; }
}

/// <summary>
/// Suite-level statistics for performance analysis
/// </summary>
public class SuiteStatistic
{
    public string SuiteName { get; set; } = string.Empty;
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public int BrokenTests { get; set; }
    public long TotalDurationMs { get; set; }
    public double AvgDurationMs { get; set; }
    public double PassRate => TotalTests > 0 ? (double)PassedTests / TotalTests * 100 : 0;
    public string TotalDurationReadable => TimeUtils.ConvertMillisecondsToReadable(TotalDurationMs);
    public string AvgDurationReadable => TimeUtils.ConvertMillisecondsToReadable((long)AvgDurationMs);
}

/// <summary>
/// Information about slow running tests
/// </summary>
public class SlowTestInfo
{
    public string SuiteName { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FailingStep { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public string ScreenshotPath { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
}