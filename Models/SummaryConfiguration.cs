namespace AllureReportProcessor.Models;

/// <summary>
/// Configuration settings for summary report generation and quality analysis
/// </summary>
public class SummaryConfiguration
{
    public int TopSlowStepsCount { get; set; } = 15;
    public int TopTestsPerSuiteCount { get; set; } = 5;
    public int StepNameTruncationLength { get; set; } = 50;
    
    public PerformanceThresholds PerformanceThresholds { get; set; } = new();
    public ReliabilityThresholds ReliabilityThresholds { get; set; } = new();
    public ExcelSettings ExcelSettings { get; set; } = new();
    public HtmlReportSettings HtmlReportSettings { get; set; } = new();
    
    // New QA-focused settings
    public QualityAnalysisSettings QualityAnalysisSettings { get; set; } = new();
    public AlertingSettings AlertingSettings { get; set; } = new();
    public ExportSettings ExportSettings { get; set; } = new();
}

public class PerformanceThresholds
{
    public long CriticalThresholdMs { get; set; } = 180000; // 3 minutes
    public long HighThresholdMs { get; set; } = 60000; // 1 minute
    public long MediumThresholdMs { get; set; } = 30000; // 30 seconds
}

public class ReliabilityThresholds
{
    public double UnreliableThreshold { get; set; } = 50.0;
    public double PoorThreshold { get; set; } = 20.0;
    public double GoodThreshold { get; set; } = 5.0;
}

public class ExcelSettings
{
    public bool IncludePerformanceCategory { get; set; } = true;
    public bool IncludeReliabilityCategory { get; set; } = true;
    public bool AutoSizeColumns { get; set; } = true;
}

public class HtmlReportSettings
{
    public bool EnableHtmlReport { get; set; } = true;
    public bool IncludeScreenshots { get; set; } = true;
    public bool EnableInteractiveFiltering { get; set; } = true;
    public int ShowTopSlowTestsCount { get; set; } = 20;
    public int ShowTopSuitesCount { get; set; } = 5;
}