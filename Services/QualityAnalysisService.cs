using AllureReportProcessor.Models;
using AllureReportProcessor.Utils;
using System.Text;
using System.Text.Json;

namespace AllureReportProcessor.Services;

/// <summary>
/// Advanced QA analysis service providing insights for functional QA and QA Leads
/// </summary>
public class QualityAnalysisService
{
    private readonly SummaryConfiguration _config;

    public QualityAnalysisService(SummaryConfiguration config)
    {
        _config = config ?? new SummaryConfiguration();
    }

    /// <summary>
    /// Performs comprehensive quality analysis on test results
    /// </summary>
    public QualityAnalysisResult AnalyzeTestQuality(ProcessingResults results, List<HistoricalTestData>? historicalData = null)
    {
        var analysis = new QualityAnalysisResult
        {
            AnalysisTimestamp = DateTime.Now,
            TotalTestsAnalyzed = results.TestResults.Count
        };

        // Core Analysis
        analysis.TestStabilityAnalysis = AnalyzeTestStability(results, historicalData);
        analysis.FlakyTestAnalysis = DetectFlakyTests(results, historicalData);
        analysis.RegressionAnalysis = PerformRegressionAnalysis(results, historicalData);
        analysis.CoverageGaps = AnalyzeCoverageGaps(results);
        analysis.RiskAssessment = AssessQualityRisk(results);
        analysis.PerformanceTrends = AnalyzePerformanceTrends(results, historicalData);
        analysis.EnvironmentImpact = AnalyzeEnvironmentImpact(results);
        
        // NEW: Advanced Failure Analysis
        var failureAnalysisService = new FailureAnalysisService(_config);
        analysis.FailureAnalysis = failureAnalysisService.AnalyzeFailures(results.TestResults);
        
        // Actionable Insights
        analysis.CriticalIssues = IdentifyCriticalIssues(results);
        analysis.ActionableRecommendations = GenerateActionableRecommendations(analysis);
        analysis.ExecutiveSummary = GenerateExecutiveSummary(analysis);

        return analysis;
    }

    private TestStabilityAnalysis AnalyzeTestStability(ProcessingResults results, List<HistoricalTestData>? historicalData)
    {
        var stability = new TestStabilityAnalysis();
        
        // Group tests by suite and analyze consistency
        var suiteStability = results.TestResults
            .GroupBy(t => t.SuiteName)
            .Select(g => new SuiteStability
            {
                SuiteName = g.Key,
                TotalTests = g.Count(),
                PassRate = g.Count(t => t.Status == "Passed") / (double)g.Count() * 100,
                FailPattern = AnalyzeFailurePattern(g.ToList()),
                ConsistencyScore = CalculateConsistencyScore(g.ToList(), historicalData),
                RecommendedAction = DetermineStabilityAction(g.ToList())
            })
            .OrderBy(s => s.ConsistencyScore)
            .ToList();

        stability.SuiteStabilityScores = suiteStability;
        stability.OverallStabilityScore = suiteStability.Average(s => s.ConsistencyScore);
        stability.UnstableSuites = suiteStability.Where(s => s.ConsistencyScore < 70).ToList();

        return stability;
    }

    private FlakyTestAnalysis DetectFlakyTests(ProcessingResults results, List<HistoricalTestData>? historicalData)
    {
        var analysis = new FlakyTestAnalysis();
        
        if (historicalData == null || !historicalData.Any())
        {
            analysis.DetectedFlakyTests = new List<FlakyTest>();
            analysis.FlakyTestCount = 0;
            return analysis;
        }

        var flakyTests = new List<FlakyTest>();
        
        foreach (var test in results.TestResults)
        {
            var historicalResults = historicalData
                .Where(h => h.TestName == test.TestCaseName && h.SuiteName == test.SuiteName)
                .OrderByDescending(h => h.ExecutionDate)
                .Take(10)
                .ToList();

            if (historicalResults.Count >= _config.QualityAnalysisSettings.FlakyTestThreshold)
            {
                var statusChanges = CountStatusChanges(historicalResults);
                var inconsistencyRate = statusChanges / (double)historicalResults.Count * 100;

                if (inconsistencyRate > 30) // 30% inconsistency threshold
                {
                    flakyTests.Add(new FlakyTest
                    {
                        SuiteName = test.SuiteName,
                        TestName = test.TestCaseName,
                        FlakyScore = inconsistencyRate,
                        RecentExecutions = historicalResults.Count,
                        StatusChangeCount = statusChanges,
                        LastFailureReason = test.FailureReason,
                        RecommendedAction = DetermineFlakyTestAction(inconsistencyRate),
                        Priority = ClassifyFlakinessPriority(inconsistencyRate, test.SuiteName)
                    });
                }
            }
        }

        analysis.DetectedFlakyTests = flakyTests.OrderByDescending(f => f.FlakyScore).ToList();
        analysis.FlakyTestCount = flakyTests.Count;
        analysis.HighPriorityFlakyTests = flakyTests.Where(f => f.Priority == "High").ToList();

        return analysis;
    }

    private RegressionAnalysis PerformRegressionAnalysis(ProcessingResults results, List<HistoricalTestData>? historicalData)
    {
        var analysis = new RegressionAnalysis();
        
        // Identify newly failing tests
        var newFailures = new List<RegressionIssue>();
        var performanceRegressions = new List<PerformanceRegression>();

        if (historicalData != null && historicalData.Any())
        {
            var lastSuccessfulRun = historicalData
                .GroupBy(h => h.ExecutionDate.Date)
                .OrderByDescending(g => g.Key)
                .Skip(1) // Skip current run
                .FirstOrDefault();

            if (lastSuccessfulRun != null)
            {
                var previousResults = lastSuccessfulRun.ToList();
                
                foreach (var currentTest in results.TestResults.Where(t => t.Status != "Passed"))
                {
                    var previousTest = previousResults.FirstOrDefault(p => 
                        p.TestName == currentTest.TestCaseName && p.SuiteName == currentTest.SuiteName);
                    
                    if (previousTest != null && previousTest.Status == "Passed")
                    {
                        newFailures.Add(new RegressionIssue
                        {
                            SuiteName = currentTest.SuiteName,
                            TestName = currentTest.TestCaseName,
                            PreviousStatus = "Passed",
                            CurrentStatus = currentTest.Status,
                            FailureReason = currentTest.FailureReason,
                            FailingStep = currentTest.FailingStep,
                            Impact = DetermineRegressionImpact(currentTest.SuiteName),
                            Priority = ClassifyRegressionPriority(currentTest)
                        });
                    }
                }

                // Analyze performance regressions
                performanceRegressions = DetectPerformanceRegressions(results, previousResults);
            }
        }

        analysis.NewFailures = newFailures;
        analysis.PerformanceRegressions = performanceRegressions;
        analysis.RegressionScore = CalculateRegressionScore(newFailures, performanceRegressions);
        analysis.CriticalRegressions = newFailures.Where(r => r.Priority == "Critical").ToList();

        return analysis;
    }

    private CoverageAnalysis AnalyzeCoverageGaps(ProcessingResults results)
    {
        var analysis = new CoverageAnalysis();
        
        // Analyze test distribution across features/components
        var featureCoverage = results.TestResults
            .GroupBy(t => t.SuiteName)
            .Select(g => new FeatureCoverage
            {
                FeatureName = g.Key,
                TestCount = g.Count(),
                PassRate = g.Count(t => t.Status == "Passed") / (double)g.Count() * 100,
                CoverageGaps = IdentifyCoverageGaps(g.Key, g.ToList()),
                RiskLevel = AssessFeatureRisk(g.ToList())
            })
            .ToList();

        analysis.FeatureCoverage = featureCoverage;
        analysis.UncoveredAreas = featureCoverage.Where(f => f.TestCount < 5).ToList(); // Threshold for minimum coverage
        analysis.HighRiskAreas = featureCoverage.Where(f => f.RiskLevel == "High").ToList();

        return analysis;
    }

    private QualityRiskAssessment AssessQualityRisk(ProcessingResults results)
    {
        var assessment = new QualityRiskAssessment();
        
        var totalTests = results.TestResults.Count;
        var failedTests = results.TestResults.Count(t => t.Status == "Failed");
        var brokenTests = results.TestResults.Count(t => t.Status == "Broken");
        
        // Calculate risk metrics
        var failureRate = (failedTests + brokenTests) / (double)totalTests * 100;
        var criticalSuiteFailures = results.TestResults
            .Where(t => t.Status != "Passed" && IsCriticalSuite(t.SuiteName))
            .Count();

        assessment.OverallRiskLevel = DetermineOverallRisk(failureRate, criticalSuiteFailures);
        assessment.FailureRate = failureRate;
        assessment.CriticalSuiteFailures = criticalSuiteFailures;
        assessment.RiskFactors = IdentifyRiskFactors(results);
        assessment.MitigationStrategies = GenerateMitigationStrategies(assessment.RiskFactors);

        return assessment;
    }

    private List<PerformanceRegression> DetectPerformanceRegressions(ProcessingResults current, List<HistoricalTestData> previous)
    {
        var regressions = new List<PerformanceRegression>();
        var summaryService = new SummaryService(_config);

        foreach (var currentTest in current.TestResults)
        {
            var previousTest = previous.FirstOrDefault(p => 
                p.TestName == currentTest.TestCaseName && p.SuiteName == currentTest.SuiteName);

            if (previousTest != null)
            {
                var currentDurationMs = summaryService.ParseDurationStringToMs(currentTest.Duration);
                var previousDurationMs = summaryService.ParseDurationStringToMs(previousTest.Duration);

                if (previousDurationMs > 0)
                {
                    var performanceChange = ((currentDurationMs - previousDurationMs) / (double)previousDurationMs) * 100;
                    
                    if (performanceChange > _config.AlertingSettings.PerformanceDegradationThreshold)
                    {
                        regressions.Add(new PerformanceRegression
                        {
                            SuiteName = currentTest.SuiteName,
                            TestName = currentTest.TestCaseName,
                            PreviousDuration = TimeUtils.ConvertMillisecondsToReadable(previousDurationMs),
                            CurrentDuration = currentTest.Duration,
                            PerformanceChange = performanceChange,
                            Severity = ClassifyPerformanceRegression(performanceChange),
                            Impact = DeterminePerformanceImpact(currentTest.SuiteName, performanceChange)
                        });
                    }
                }
            }
        }

        return regressions.OrderByDescending(r => r.PerformanceChange).ToList();
    }

    // Helper methods for analysis
    private string AnalyzeFailurePattern(List<TestResult> tests)
    {
        var failures = tests.Where(t => t.Status != "Passed").ToList();
        if (!failures.Any()) return "No failures";
        
        var commonSteps = failures
            .GroupBy(f => f.FailingStep)
            .OrderByDescending(g => g.Count())
            .First();
            
        return $"Common failure: {commonSteps.Key} ({commonSteps.Count()}/{failures.Count} tests)";
    }

    private double CalculateConsistencyScore(List<TestResult> tests, List<HistoricalTestData>? historicalData)
    {
        // Base score on current run pass rate
        var currentPassRate = tests.Count(t => t.Status == "Passed") / (double)tests.Count() * 100;
        
        // Adjust based on historical consistency if available
        if (historicalData != null && historicalData.Any())
        {
            var suiteName = tests.First().SuiteName;
            var historicalPassRates = historicalData
                .Where(h => h.SuiteName == suiteName)
                .GroupBy(h => h.ExecutionDate.Date)
                .Select(g => g.Count(t => t.Status == "Passed") / (double)g.Count() * 100)
                .ToList();
                
            if (historicalPassRates.Any())
            {
                var variance = CalculateVariance(historicalPassRates);
                return Math.Max(0, currentPassRate - variance);
            }
        }
        
        return currentPassRate;
    }

    private double CalculateVariance(List<double> values)
    {
        if (!values.Any()) return 0;
        
        var mean = values.Average();
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        return Math.Sqrt(variance);
    }

    private int CountStatusChanges(List<HistoricalTestData> historicalResults)
    {
        if (historicalResults.Count <= 1) return 0;
        
        var changes = 0;
        for (int i = 1; i < historicalResults.Count; i++)
        {
            if (historicalResults[i].Status != historicalResults[i - 1].Status)
                changes++;
        }
        return changes;
    }

    private bool IsCriticalSuite(string suiteName)
    {
        return _config.QualityAnalysisSettings.CriticalSuitesForRegression
            .Any(critical => suiteName.ToLower().Contains(critical.ToLower()));
    }

    private string DetermineStabilityAction(List<TestResult> tests)
    {
        var failureRate = tests.Count(t => t.Status != "Passed") / (double)tests.Count() * 100;
        
        return failureRate switch
        {
            > 50 => "?? IMMEDIATE ATTENTION - Investigate and fix critical issues",
            > 20 => "?? HIGH PRIORITY - Review and stabilize failing tests",
            > 10 => "?? MONITOR - Track trends and investigate patterns",
            _ => "? STABLE - Continue monitoring"
        };
    }

    private List<CriticalIssue> IdentifyCriticalIssues(ProcessingResults results)
    {
        var issues = new List<CriticalIssue>();
        
        // High failure rate in critical suites
        var criticalSuiteFailures = results.TestResults
            .Where(t => t.Status != "Passed" && IsCriticalSuite(t.SuiteName))
            .GroupBy(t => t.SuiteName)
            .Where(g => g.Count() >= _config.AlertingSettings.CriticalFailureThreshold)
            .Select(g => new CriticalIssue
            {
                Type = "Critical Suite Failures",
                Description = $"Suite '{g.Key}' has {g.Count()} failed tests",
                Impact = "High",
                AffectedArea = g.Key,
                RecommendedAction = "Immediate investigation required",
                Priority = 1
            });
            
        issues.AddRange(criticalSuiteFailures);
        
        // Widespread failures across multiple suites
        var suitesWithFailures = results.TestResults
            .Where(t => t.Status != "Passed")
            .GroupBy(t => t.SuiteName)
            .Count();
            
        if (suitesWithFailures > results.TestResults.GroupBy(t => t.SuiteName).Count() * 0.5)
        {
            issues.Add(new CriticalIssue
            {
                Type = "Widespread Failures",
                Description = $"Failures detected across {suitesWithFailures} suites",
                Impact = "Critical",
                AffectedArea = "Multiple",
                RecommendedAction = "Check for environment or build issues",
                Priority = 1
            });
        }
        
        return issues.OrderBy(i => i.Priority).ToList();
    }

    private List<ActionableRecommendation> GenerateActionableRecommendations(QualityAnalysisResult analysis)
    {
        var recommendations = new List<ActionableRecommendation>();
        
        // Based on flaky tests
        if (analysis.FlakyTestAnalysis.HighPriorityFlakyTests.Any())
        {
            recommendations.Add(new ActionableRecommendation
            {
                Category = "Test Stability",
                Priority = "High",
                Title = "Address Flaky Tests",
                Description = $"Found {analysis.FlakyTestAnalysis.HighPriorityFlakyTests.Count} high-priority flaky tests",
                ActionItems = new List<string>
                {
                    "Review and fix flaky test logic",
                    "Add proper wait conditions",
                    "Investigate timing issues",
                    "Consider test environment stability"
                },
                EstimatedEffort = "2-3 days",
                ExpectedImpact = "Improved test reliability and reduced false failures"
            });
        }
        
        // Based on performance issues
        if (analysis.PerformanceTrends.PerformanceRegressions.Any())
        {
            recommendations.Add(new ActionableRecommendation
            {
                Category = "Performance",
                Priority = "Medium",
                Title = "Optimize Slow Tests",
                Description = $"Detected {analysis.PerformanceTrends.PerformanceRegressions.Count} performance regressions",
                ActionItems = new List<string>
                {
                    "Review slow test implementations",
                    "Optimize test data setup/teardown",
                    "Consider parallel execution",
                    "Review application performance"
                },
                EstimatedEffort = "1-2 weeks",
                ExpectedImpact = "Faster feedback cycle and improved CI/CD pipeline"
            });
        }
        
        return recommendations;
    }

    private ExecutiveSummary GenerateExecutiveSummary(QualityAnalysisResult analysis)
    {
        return new ExecutiveSummary
        {
            OverallHealthScore = CalculateOverallHealthScore(analysis),
            KeyFindings = GenerateKeyFindings(analysis),
            BusinessImpact = AssessBusinessImpact(analysis),
            NextSteps = GenerateNextSteps(analysis),
            RiskLevel = analysis.RiskAssessment.OverallRiskLevel,
            QualityTrend = DetermineQualityTrend(analysis)
        };
    }

    private int CalculateOverallHealthScore(QualityAnalysisResult analysis)
    {
        var baseScore = 100;
        
        // Deduct for failures
        baseScore -= (int)(analysis.RiskAssessment.FailureRate * 2);
        
        // Deduct for flaky tests
        baseScore -= analysis.FlakyTestAnalysis.FlakyTestCount * 2;
        
        // Deduct for regressions
        baseScore -= analysis.RegressionAnalysis.NewFailures.Count * 3;
        
        // Deduct for stability issues
        baseScore -= (int)((100 - analysis.TestStabilityAnalysis.OverallStabilityScore) / 2);
        
        return Math.Max(0, Math.Min(100, baseScore));
    }

    private List<string> GenerateKeyFindings(QualityAnalysisResult analysis)
    {
        var findings = new List<string>();
        
        if (analysis.RiskAssessment.OverallRiskLevel == "High")
            findings.Add($"?? High quality risk detected - {analysis.RiskAssessment.FailureRate:F1}% failure rate");
            
        if (analysis.FlakyTestAnalysis.FlakyTestCount > 0)
            findings.Add($"?? {analysis.FlakyTestAnalysis.FlakyTestCount} flaky tests identified");
            
        if (analysis.RegressionAnalysis.NewFailures.Any())
            findings.Add($"?? {analysis.RegressionAnalysis.NewFailures.Count} new regressions detected");
            
        if (analysis.TestStabilityAnalysis.OverallStabilityScore < 80)
            findings.Add($"?? Test stability below threshold ({analysis.TestStabilityAnalysis.OverallStabilityScore:F1}%)");
        
        return findings;
    }

    // Additional helper methods would be implemented here...
    private PerformanceTrendAnalysis AnalyzePerformanceTrends(ProcessingResults results, List<HistoricalTestData>? historicalData) => new();
    private EnvironmentImpactAnalysis AnalyzeEnvironmentImpact(ProcessingResults results) => new();
    private List<string> IdentifyCoverageGaps(string suiteName, List<TestResult> tests) => new();
    private string AssessFeatureRisk(List<TestResult> tests) => "Medium";
    private string DetermineFlakyTestAction(double inconsistencyRate) => "Investigate and fix";
    private string ClassifyFlakinessPriority(double inconsistencyRate, string suiteName) => "Medium";
    private string DetermineRegressionImpact(string suiteName) => "Medium";
    private string ClassifyRegressionPriority(TestResult test) => "Medium";
    private double CalculateRegressionScore(List<RegressionIssue> failures, List<PerformanceRegression> performance) => 75.0;
    private string DetermineOverallRisk(double failureRate, int criticalFailures) => failureRate > 20 ? "High" : "Medium";
    private List<string> IdentifyRiskFactors(ProcessingResults results) => new() { "High failure rate" };
    private List<string> GenerateMitigationStrategies(List<string> riskFactors) => new() { "Improve test stability" };
    private string ClassifyPerformanceRegression(double change) => change > 50 ? "Critical" : "Medium";
    private string DeterminePerformanceImpact(string suiteName, double change) => "Medium";
    private string AssessBusinessImpact(QualityAnalysisResult analysis) => "Medium impact on delivery timeline";
    private List<string> GenerateNextSteps(QualityAnalysisResult analysis) => new() { "Review and fix critical issues" };
    private string DetermineQualityTrend(QualityAnalysisResult analysis) => "Stable";
}