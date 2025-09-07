using AllureReportProcessor.Models;
using System.Text.RegularExpressions;

namespace AllureReportProcessor.Services;

/// <summary>
/// Advanced failure analysis service for identifying patterns, categorizing failures,
/// and extracting actionable insights from test failures
/// </summary>
public class FailureAnalysisService
{
    private readonly SummaryConfiguration _config;
    
    // Regex patterns for failure detection
    private static readonly Regex TestCaseIdRegex = new(@"\bC\d{4,5}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TimeoutRegex = new(@"timeout|timed out|time out|wait|took too long|timeout exception", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SeleniumRegex = new(@"selenium|webdriver|browser|element|locator|xpath|css selector|stale element|no such element|window|session", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AssertionRegex = new(@"assert|expected|actual|should be|to be|but found|but was|mismatch", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NetworkRegex = new(@"connection|network|http|https|url|endpoint|service|api|server|unavailable|refused", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DatabaseRegex = new(@"database|sql|connection string|timeout|deadlock|transaction|query", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public FailureAnalysisService(SummaryConfiguration config)
    {
        _config = config ?? new SummaryConfiguration();
    }

    /// <summary>
    /// Performs comprehensive failure analysis on test results
    /// </summary>
    public FailureAnalysisResult AnalyzeFailures(List<TestResult> testResults)
    {
        var analysis = new FailureAnalysisResult();
        var failedTests = testResults.Where(t => t.Status != "Passed").ToList();

        if (!failedTests.Any())
        {
            return analysis; // No failures to analyze
        }

        // Categorize failures by type
        analysis.FailureCategories = CategorizeFailures(failedTests);
        
        // Find common failure patterns
        analysis.CommonFailures = IdentifyCommonFailurePatterns(failedTests);
        
        // Analyze by test case IDs
        analysis.TestCaseAnalysis = AnalyzeByTestCaseIds(failedTests);
        
        // Detailed Selenium analysis
        analysis.SeleniumAnalysis = AnalyzeSeleniumFailures(failedTests);
        
        // Timeout analysis
        analysis.TimeoutAnalysis = AnalyzeTimeoutFailures(failedTests);
        
        // Environment analysis
        analysis.EnvironmentAnalysis = AnalyzeEnvironmentFailures(failedTests);

        return analysis;
    }

    private FailureCategorization CategorizeFailures(List<TestResult> failedTests)
    {
        var categorization = new FailureCategorization
        {
            TotalFailures = failedTests.Count
        };

        foreach (var test in failedTests)
        {
            var failureText = $"{test.FailureReason} {test.FailingStep}".ToLowerInvariant();
            
            if (AssertionRegex.IsMatch(failureText))
            {
                categorization.AssertionFailures++;
            }
            else if (SeleniumRegex.IsMatch(failureText))
            {
                categorization.SeleniumIssues++;
            }
            else if (TimeoutRegex.IsMatch(failureText))
            {
                categorization.TimeoutIssues++;
            }
            else if (NetworkRegex.IsMatch(failureText) || DatabaseRegex.IsMatch(failureText))
            {
                categorization.EnvironmentIssues++;
            }
            else
            {
                categorization.UnknownIssues++;
            }
        }

        return categorization;
    }

    private List<CommonFailurePattern> IdentifyCommonFailurePatterns(List<TestResult> failedTests)
    {
        var patterns = new List<CommonFailurePattern>();

        // Group by failure reason and step
        var reasonGroups = failedTests
            .Where(t => !string.IsNullOrEmpty(t.FailureReason))
            .GroupBy(t => NormalizeFailureReason(t.FailureReason))
            .Where(g => g.Count() > 1) // Only patterns with multiple occurrences
            .OrderByDescending(g => g.Count())
            .Take(10);

        foreach (var group in reasonGroups)
        {
            var tests = group.ToList();
            var testCaseIds = ExtractTestCaseIds(tests);
            
            patterns.Add(new CommonFailurePattern
            {
                Pattern = group.Key,
                Category = CategorizeFailurePattern(group.Key),
                FailureCount = group.Count(),
                AffectedTestCases = testCaseIds,
                AffectedSuites = tests.Select(t => t.SuiteName).Distinct().ToList(),
                Impact = DetermineImpact(group.Count(), tests.Select(t => t.SuiteName).Distinct().Count()),
                RecommendedAction = GenerateRecommendation(group.Key, CategorizeFailurePattern(group.Key))
            });
        }

        // Group by failing step
        var stepGroups = failedTests
            .Where(t => !string.IsNullOrEmpty(t.FailingStep))
            .GroupBy(t => NormalizeFailingStep(t.FailingStep))
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Count())
            .Take(5);

        foreach (var group in stepGroups)
        {
            var tests = group.ToList();
            var testCaseIds = ExtractTestCaseIds(tests);
            
            if (!patterns.Any(p => p.Pattern.Contains(group.Key))) // Avoid duplicates
            {
                patterns.Add(new CommonFailurePattern
                {
                    Pattern = $"Step: {group.Key}",
                    Category = "Step Failure",
                    FailureCount = group.Count(),
                    AffectedTestCases = testCaseIds,
                    AffectedSuites = tests.Select(t => t.SuiteName).Distinct().ToList(),
                    Impact = DetermineImpact(group.Count(), tests.Select(t => t.SuiteName).Distinct().Count()),
                    RecommendedAction = "Review step implementation and add proper error handling"
                });
            }
        }

        return patterns.OrderByDescending(p => p.FailureCount).ToList();
    }

    private List<TestCaseFailureGroup> AnalyzeByTestCaseIds(List<TestResult> failedTests)
    {
        var testCaseGroups = new Dictionary<string, List<TestResult>>();

        foreach (var test in failedTests)
        {
            var testCaseIds = ExtractTestCaseIdsFromTags(test.CaseTags);
            foreach (var testCaseId in testCaseIds)
            {
                if (!testCaseGroups.ContainsKey(testCaseId))
                {
                    testCaseGroups[testCaseId] = new List<TestResult>();
                }
                testCaseGroups[testCaseId].Add(test);
            }
        }

        var result = new List<TestCaseFailureGroup>();

        foreach (var group in testCaseGroups.Where(g => g.Value.Count > 0).OrderByDescending(g => g.Value.Count))
        {
            var tests = group.Value;
            var primaryFailure = GetPrimaryFailureReason(tests);
            
            result.Add(new TestCaseFailureGroup
            {
                TestCaseId = group.Key,
                TotalFailures = tests.Count,
                FailureDetails = tests.Select(t => new TestFailureDetail
                {
                    SuiteName = t.SuiteName,
                    TestName = t.TestCaseName,
                    FailureReason = TruncateText(t.FailureReason, 100),
                    FailingStep = TruncateText(t.FailingStep, 80),
                    Duration = t.Duration
                }).ToList(),
                PrimaryFailureReason = primaryFailure,
                FailureCategory = CategorizeFailurePattern(primaryFailure),
                AffectedSuites = tests.Select(t => t.SuiteName).Distinct().ToList()
            });
        }

        return result;
    }

    private SeleniumFailureAnalysis AnalyzeSeleniumFailures(List<TestResult> failedTests)
    {
        var seleniumTests = failedTests.Where(t => 
            SeleniumRegex.IsMatch($"{t.FailureReason} {t.FailingStep}".ToLowerInvariant())).ToList();

        var analysis = new SeleniumFailureAnalysis
        {
            TotalSeleniumIssues = seleniumTests.Count
        };

        // Categorize Selenium issues
        var categories = new Dictionary<string, List<TestResult>>
        {
            ["Element Not Found"] = seleniumTests.Where(t => IsElementNotFoundIssue(t)).ToList(),
            ["Stale Element"] = seleniumTests.Where(t => IsStaleElementIssue(t)).ToList(),
            ["Timeout/Wait Issues"] = seleniumTests.Where(t => IsSeleniumTimeoutIssue(t)).ToList(),
            ["Browser/Driver Issues"] = seleniumTests.Where(t => IsBrowserDriverIssue(t)).ToList(),
            ["Window/Session Issues"] = seleniumTests.Where(t => IsWindowSessionIssue(t)).ToList()
        };

        analysis.IssueCategories = categories.Where(c => c.Value.Any()).Select(c => new SeleniumIssueCategory
        {
            Category = c.Key,
            Count = c.Value.Count,
            Percentage = seleniumTests.Any() ? (double)c.Value.Count / seleniumTests.Count * 100 : 0,
            CommonPatterns = GetSeleniumPatterns(c.Value),
            RecommendedFix = GetSeleniumRecommendation(c.Key)
        }).OrderByDescending(c => c.Count).ToList();

        // Top Selenium failure patterns
        analysis.TopSeleniumFailures = seleniumTests
            .GroupBy(t => NormalizeFailureReason(t.FailureReason))
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new SeleniumFailureDetail
            {
                Pattern = g.Key,
                Count = g.Count(),
                AffectedTests = g.Select(t => $"{t.SuiteName}.{t.TestCaseName}").ToList(),
                Category = CategorizeSeleniumPattern(g.Key),
                Severity = g.Count() > 5 ? "High" : g.Count() > 2 ? "Medium" : "Low"
            }).ToList();

        return analysis;
    }

    private TimeoutFailureAnalysis AnalyzeTimeoutFailures(List<TestResult> failedTests)
    {
        var timeoutTests = failedTests.Where(t => 
            TimeoutRegex.IsMatch($"{t.FailureReason} {t.FailingStep}".ToLowerInvariant())).ToList();

        var analysis = new TimeoutFailureAnalysis
        {
            TotalTimeoutIssues = timeoutTests.Count
        };

        // Categorize timeout types
        var categories = new Dictionary<string, List<TestResult>>
        {
            ["WebDriver Timeout"] = timeoutTests.Where(t => IsWebDriverTimeout(t)).ToList(),
            ["Page Load Timeout"] = timeoutTests.Where(t => IsPageLoadTimeout(t)).ToList(),
            ["Element Wait Timeout"] = timeoutTests.Where(t => IsElementWaitTimeout(t)).ToList(),
            ["General Timeout"] = timeoutTests.Where(t => IsGeneralTimeout(t)).ToList()
        };

        analysis.TimeoutCategories = categories.Where(c => c.Value.Any()).Select(c => new TimeoutCategory
        {
            Type = c.Key,
            Count = c.Value.Count,
            Percentage = timeoutTests.Any() ? (double)c.Value.Count / timeoutTests.Count * 100 : 0,
            AffectedTests = c.Value.Select(t => $"{t.SuiteName}.{t.TestCaseName}").ToList(),
            RecommendedFix = GetTimeoutRecommendation(c.Key)
        }).OrderByDescending(c => c.Count).ToList();

        // Extract timeout steps
        analysis.TopTimeoutSteps = timeoutTests
            .Where(t => !string.IsNullOrEmpty(t.FailingStep))
            .GroupBy(t => NormalizeFailingStep(t.FailingStep))
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => $"{g.Key} ({g.Count()} times)")
            .ToList();

        return analysis;
    }

    private EnvironmentFailureAnalysis AnalyzeEnvironmentFailures(List<TestResult> failedTests)
    {
        var envTests = failedTests.Where(t => 
            NetworkRegex.IsMatch($"{t.FailureReason} {t.FailingStep}".ToLowerInvariant()) ||
            DatabaseRegex.IsMatch($"{t.FailureReason} {t.FailingStep}".ToLowerInvariant())).ToList();

        var analysis = new EnvironmentFailureAnalysis
        {
            TotalEnvironmentIssues = envTests.Count,
            DatabaseConnectivityIssues = envTests.Any(t => IsDatabaseIssue(t)),
            NetworkConnectivityIssues = envTests.Any(t => IsNetworkIssue(t)),
            BrowserIssues = envTests.Any(t => IsBrowserIssue(t)),
            ServiceUnavailableIssues = envTests.Any(t => IsServiceUnavailableIssue(t))
        };

        // Categorize environment issues
        var categories = new Dictionary<string, List<TestResult>>
        {
            ["Database Issues"] = envTests.Where(t => IsDatabaseIssue(t)).ToList(),
            ["Network Connectivity"] = envTests.Where(t => IsNetworkIssue(t)).ToList(),
            ["Service Unavailable"] = envTests.Where(t => IsServiceUnavailableIssue(t)).ToList(),
            ["Browser Environment"] = envTests.Where(t => IsBrowserIssue(t)).ToList()
        };

        analysis.IssueCategories = categories.Where(c => c.Value.Any()).Select(c => new EnvironmentIssueCategory
        {
            Category = c.Key,
            Count = c.Value.Count,
            Percentage = envTests.Any() ? (double)c.Value.Count / envTests.Count * 100 : 0,
            AffectedTests = c.Value.Select(t => $"{t.SuiteName}.{t.TestCaseName}").ToList(),
            Impact = DetermineEnvironmentImpact(c.Key, c.Value.Count)
        }).OrderByDescending(c => c.Count).ToList();

        return analysis;
    }

    #region Helper Methods

    private List<string> ExtractTestCaseIdsFromTags(string tags)
    {
        if (string.IsNullOrEmpty(tags)) return new List<string>();
        
        var matches = TestCaseIdRegex.Matches(tags);
        return matches.Select(m => m.Value.ToUpperInvariant()).Distinct().ToList();
    }

    private List<string> ExtractTestCaseIds(List<TestResult> tests)
    {
        var allIds = new List<string>();
        foreach (var test in tests)
        {
            allIds.AddRange(ExtractTestCaseIdsFromTags(test.CaseTags));
        }
        return allIds.Distinct().ToList();
    }

    private string NormalizeFailureReason(string reason)
    {
        if (string.IsNullOrEmpty(reason)) return "Unknown";
        
        // Remove specific values but keep the pattern
        var normalized = reason;
        normalized = Regex.Replace(normalized, @"\b\d+\b", "[NUMBER]"); // Replace numbers
        normalized = Regex.Replace(normalized, @"'[^']*'", "'[VALUE]'"); // Replace quoted values
        normalized = Regex.Replace(normalized, @"""[^""]*""", "\"[VALUE]\""); // Replace quoted values
        normalized = Regex.Replace(normalized, @"\s+", " "); // Normalize whitespace
        
        return normalized.Length > 150 ? normalized.Substring(0, 150) + "..." : normalized;
    }

    private string NormalizeFailingStep(string step)
    {
        if (string.IsNullOrEmpty(step)) return "Unknown Step";
        
        // Extract the core step action
        var normalized = step;
        if (normalized.StartsWith("When ") || normalized.StartsWith("Then ") || normalized.StartsWith("Given "))
        {
            normalized = normalized.Substring(5);
        }
        
        return normalized.Length > 100 ? normalized.Substring(0, 100) + "..." : normalized;
    }

    private string CategorizeFailurePattern(string pattern)
    {
        var lowerPattern = pattern.ToLowerInvariant();
        
        if (AssertionRegex.IsMatch(lowerPattern)) return "Assertion Failure";
        if (SeleniumRegex.IsMatch(lowerPattern)) return "Selenium Issue";
        if (TimeoutRegex.IsMatch(lowerPattern)) return "Timeout Issue";
        if (NetworkRegex.IsMatch(lowerPattern)) return "Network Issue";
        if (DatabaseRegex.IsMatch(lowerPattern)) return "Database Issue";
        
        return "Other";
    }

    private string GetPrimaryFailureReason(List<TestResult> tests)
    {
        return tests.GroupBy(t => NormalizeFailureReason(t.FailureReason))
                   .OrderByDescending(g => g.Count())
                   .FirstOrDefault()?.Key ?? "Unknown";
    }

    private string DetermineImpact(int failureCount, int suiteCount)
    {
        if (failureCount > 10 || suiteCount > 3) return "High";
        if (failureCount > 5 || suiteCount > 1) return "Medium";
        return "Low";
    }

    private string GenerateRecommendation(string pattern, string category)
    {
        return category switch
        {
            "Assertion Failure" => "Review test logic and expected vs actual values",
            "Selenium Issue" => "Add proper waits and element existence checks",
            "Timeout Issue" => "Increase timeout values or optimize performance",
            "Network Issue" => "Check network connectivity and service availability",
            "Database Issue" => "Verify database connection and query performance",
            _ => "Investigate root cause and add proper error handling"
        };
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text ?? "";
        return text.Substring(0, maxLength - 3) + "...";
    }

    // Selenium-specific detection methods
    private bool IsElementNotFoundIssue(TestResult test) => 
        Regex.IsMatch($"{test.FailureReason} {test.FailingStep}", @"no such element|element not found|unable to locate|locator", RegexOptions.IgnoreCase);

    private bool IsStaleElementIssue(TestResult test) => 
        Regex.IsMatch($"{test.FailureReason} {test.FailingStep}", @"stale element|element is no longer attached", RegexOptions.IgnoreCase);

    private bool IsSeleniumTimeoutIssue(TestResult test) => 
        Regex.IsMatch($"{test.FailureReason} {test.FailingStep}", @"webdriver.*timeout|element.*timeout|wait.*timeout", RegexOptions.IgnoreCase);

    private bool IsBrowserDriverIssue(TestResult test) => 
        Regex.IsMatch($"{test.FailureReason} {test.FailingStep}", @"browser|driver|chrome|firefox|session.*not.*created", RegexOptions.IgnoreCase);

    private bool IsWindowSessionIssue(TestResult test) => 
        Regex.IsMatch($"{test.FailureReason} {test.FailingStep}", @"window|session|tab|frame", RegexOptions.IgnoreCase);

    // Timeout-specific detection methods
    private bool IsWebDriverTimeout(TestResult test) => 
        Regex.IsMatch($"{test.FailureReason} {test.FailingStep}", @"webdriver.*timeout|selenium.*timeout", RegexOptions.IgnoreCase);

    private bool IsPageLoadTimeout(TestResult test) => 
        Regex.IsMatch($"{test.FailureReason} {test.FailingStep}", @"page.*load.*timeout|navigation.*timeout", RegexOptions.IgnoreCase);

    private bool IsElementWaitTimeout(TestResult test) => 
        Regex.IsMatch($"{test.FailureReason} {test.FailingStep}", @"element.*wait.*timeout|wait.*element.*timeout", RegexOptions.IgnoreCase);

    private bool IsGeneralTimeout(TestResult test) => 
        TimeoutRegex.IsMatch($"{test.FailureReason} {test.FailingStep}") && 
        !IsWebDriverTimeout(test) && !IsPageLoadTimeout(test) && !IsElementWaitTimeout(test);

    // Environment-specific detection methods
    private bool IsDatabaseIssue(TestResult test) => 
        DatabaseRegex.IsMatch($"{test.FailureReason} {test.FailingStep}");

    private bool IsNetworkIssue(TestResult test) => 
        Regex.IsMatch($"{test.FailureReason} {test.FailingStep}", @"connection.*refused|network|http.*error", RegexOptions.IgnoreCase);

    private bool IsServiceUnavailableIssue(TestResult test) => 
        Regex.IsMatch($"{test.FailureReason} {test.FailingStep}", @"service.*unavailable|server.*error|503|500", RegexOptions.IgnoreCase);

    private bool IsBrowserIssue(TestResult test) => 
        Regex.IsMatch($"{test.FailureReason} {test.FailingStep}", @"browser.*crash|browser.*hung|browser.*not.*responding", RegexOptions.IgnoreCase);

    private List<string> GetSeleniumPatterns(List<TestResult> tests) => 
        tests.Take(3).Select(t => TruncateText(t.FailureReason, 80)).ToList();

    private string GetSeleniumRecommendation(string category) => category switch
    {
        "Element Not Found" => "Add explicit waits and verify element locators",
        "Stale Element" => "Re-find elements after page changes",
        "Timeout/Wait Issues" => "Increase wait times and use dynamic waits",
        "Browser/Driver Issues" => "Update browser drivers and check browser compatibility",
        "Window/Session Issues" => "Add proper window/session management",
        _ => "Review Selenium best practices"
    };

    private string CategorizeSeleniumPattern(string pattern) => "Selenium";

    private string GetTimeoutRecommendation(string type) => type switch
    {
        "WebDriver Timeout" => "Increase WebDriver timeout configuration",
        "Page Load Timeout" => "Optimize page performance or increase page load timeout",
        "Element Wait Timeout" => "Use more specific element locators and increase wait times",
        "General Timeout" => "Review timeout configuration and system performance",
        _ => "Optimize timeout handling"
    };

    private string DetermineEnvironmentImpact(string category, int count) => category switch
    {
        "Database Issues" when count > 5 => "Critical - Database connectivity problems",
        "Network Connectivity" when count > 3 => "High - Network infrastructure issues",
        "Service Unavailable" when count > 3 => "High - Service deployment or configuration issues",
        _ => count > 2 ? "Medium" : "Low"
    };

    #endregion
}