using AllureReportProcessor.Utils;

namespace AllureReportProcessor.Models;

// Quality Analysis Models

/// <summary>
/// Comprehensive quality analysis result containing all QA insights
/// </summary>
public class QualityAnalysisResult
{
    public DateTime AnalysisTimestamp { get; set; }
    public int TotalTestsAnalyzed { get; set; }
    public TestStabilityAnalysis TestStabilityAnalysis { get; set; } = new();
    public FlakyTestAnalysis FlakyTestAnalysis { get; set; } = new();
    public RegressionAnalysis RegressionAnalysis { get; set; } = new();
    public CoverageAnalysis CoverageGaps { get; set; } = new();
    public QualityRiskAssessment RiskAssessment { get; set; } = new();
    public PerformanceTrendAnalysis PerformanceTrends { get; set; } = new();
    public EnvironmentImpactAnalysis EnvironmentImpact { get; set; } = new();
    public List<CriticalIssue> CriticalIssues { get; set; } = new();
    public List<ActionableRecommendation> ActionableRecommendations { get; set; } = new();
    public ExecutiveSummary ExecutiveSummary { get; set; } = new();
}

/// <summary>
/// Historical test data for trend analysis
/// </summary>
public class HistoricalTestData
{
    public string SuiteName { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public DateTime ExecutionDate { get; set; }
    public string BuildNumber { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
}

/// <summary>
/// Test stability analysis results
/// </summary>
public class TestStabilityAnalysis
{
    public List<SuiteStability> SuiteStabilityScores { get; set; } = new();
    public double OverallStabilityScore { get; set; }
    public List<SuiteStability> UnstableSuites { get; set; } = new();
}

public class SuiteStability
{
    public string SuiteName { get; set; } = string.Empty;
    public int TotalTests { get; set; }
    public double PassRate { get; set; }
    public string FailPattern { get; set; } = string.Empty;
    public double ConsistencyScore { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
}

/// <summary>
/// Flaky test detection results
/// </summary>
public class FlakyTestAnalysis
{
    public List<FlakyTest> DetectedFlakyTests { get; set; } = new();
    public int FlakyTestCount { get; set; }
    public List<FlakyTest> HighPriorityFlakyTests { get; set; } = new();
}

public class FlakyTest
{
    public string SuiteName { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public double FlakyScore { get; set; }
    public int RecentExecutions { get; set; }
    public int StatusChangeCount { get; set; }
    public string LastFailureReason { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
}

/// <summary>
/// Regression analysis results
/// </summary>
public class RegressionAnalysis
{
    public List<RegressionIssue> NewFailures { get; set; } = new();
    public List<PerformanceRegression> PerformanceRegressions { get; set; } = new();
    public double RegressionScore { get; set; }
    public List<RegressionIssue> CriticalRegressions { get; set; } = new();
}

public class RegressionIssue
{
    public string SuiteName { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public string FailingStep { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
}

public class PerformanceRegression
{
    public string SuiteName { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string PreviousDuration { get; set; } = string.Empty;
    public string CurrentDuration { get; set; } = string.Empty;
    public double PerformanceChange { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
}

/// <summary>
/// Coverage analysis results
/// </summary>
public class CoverageAnalysis
{
    public List<FeatureCoverage> FeatureCoverage { get; set; } = new();
    public List<FeatureCoverage> UncoveredAreas { get; set; } = new();
    public List<FeatureCoverage> HighRiskAreas { get; set; } = new();
}

public class FeatureCoverage
{
    public string FeatureName { get; set; } = string.Empty;
    public int TestCount { get; set; }
    public double PassRate { get; set; }
    public List<string> CoverageGaps { get; set; } = new();
    public string RiskLevel { get; set; } = string.Empty;
}

/// <summary>
/// Quality risk assessment
/// </summary>
public class QualityRiskAssessment
{
    public string OverallRiskLevel { get; set; } = string.Empty;
    public double FailureRate { get; set; }
    public int CriticalSuiteFailures { get; set; }
    public List<string> RiskFactors { get; set; } = new();
    public List<string> MitigationStrategies { get; set; } = new();
}

/// <summary>
/// Performance trend analysis
/// </summary>
public class PerformanceTrendAnalysis
{
    public List<PerformanceRegression> PerformanceRegressions { get; set; } = new();
    public double AverageExecutionTimeChange { get; set; }
    public List<SlowTestTrend> SlowTestTrends { get; set; } = new();
    public string OverallTrend { get; set; } = string.Empty;
}

public class SlowTestTrend
{
    public string TestName { get; set; } = string.Empty;
    public string SuiteName { get; set; } = string.Empty;
    public List<double> HistoricalDurations { get; set; } = new();
    public double TrendDirection { get; set; }
    public string TrendDescription { get; set; } = string.Empty;
}

/// <summary>
/// Environment impact analysis
/// </summary>
public class EnvironmentImpactAnalysis
{
    public List<EnvironmentComparison> EnvironmentComparisons { get; set; } = new();
    public List<EnvironmentSpecificIssue> EnvironmentIssues { get; set; } = new();
    public string RecommendedEnvironment { get; set; } = string.Empty;
}

public class EnvironmentComparison
{
    public string Environment { get; set; } = string.Empty;
    public double PassRate { get; set; }
    public double AverageExecutionTime { get; set; }
    public int UniqueFailures { get; set; }
    public string StabilityRating { get; set; } = string.Empty;
}

public class EnvironmentSpecificIssue
{
    public string Environment { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> AffectedTests { get; set; } = new();
    public string RecommendedAction { get; set; } = string.Empty;
}

/// <summary>
/// Critical issues requiring immediate attention
/// </summary>
public class CriticalIssue
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string AffectedArea { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public int Priority { get; set; }
}

/// <summary>
/// Actionable recommendations for QA team
/// </summary>
public class ActionableRecommendation
{
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> ActionItems { get; set; } = new();
    public string EstimatedEffort { get; set; } = string.Empty;
    public string ExpectedImpact { get; set; } = string.Empty;
}

/// <summary>
/// Executive summary for QA leadership
/// </summary>
public class ExecutiveSummary
{
    public int OverallHealthScore { get; set; }
    public List<string> KeyFindings { get; set; } = new();
    public string BusinessImpact { get; set; } = string.Empty;
    public List<string> NextSteps { get; set; } = new();
    public string RiskLevel { get; set; } = string.Empty;
    public string QualityTrend { get; set; } = string.Empty;
}

// Enhanced configuration models
public class QualityAnalysisSettings
{
    public bool EnableFlakyTestDetection { get; set; } = true;
    public bool EnableRegressionAnalysis { get; set; } = true;
    public bool EnableCoverageAnalysis { get; set; } = true;
    public bool EnableRiskAssessment { get; set; } = true;
    public int FlakyTestThreshold { get; set; } = 3;
    public List<string> CriticalSuitesForRegression { get; set; } = new();
    public bool EnableTrendAnalysis { get; set; } = true;
    public int HistoricalRunsToCompare { get; set; } = 5;
    public bool EnableTestStabilityMetrics { get; set; } = true;
    public bool EnableEnvironmentComparison { get; set; } = true;
    public bool GenerateActionableRecommendations { get; set; } = true;
}

public class AlertingSettings
{
    public bool EnableSlackIntegration { get; set; } = false;
    public bool EnableEmailAlerts { get; set; } = false;
    public int CriticalFailureThreshold { get; set; } = 5;
    public double PerformanceDegradationThreshold { get; set; } = 20.0;
    public string SlackWebhookUrl { get; set; } = string.Empty;
    public List<string> EmailRecipients { get; set; } = new();
}

public class ExportSettings
{
    public bool EnableJiraIntegration { get; set; } = false;
    public bool EnableTestRailIntegration { get; set; } = false;
    public bool GenerateExecutiveSummary { get; set; } = true;
    public bool EnableAutomatedTicketCreation { get; set; } = false;
}