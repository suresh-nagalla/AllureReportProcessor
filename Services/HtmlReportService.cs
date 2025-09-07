using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AllureReportProcessor.Models;

namespace AllureReportProcessor.Services;

public class HtmlReportService
{
    private readonly SummaryConfiguration _config;
    private static readonly Regex DurationTokenRegex = new(@"(?<val>\d+(?:\.\d+)?)\s*(?<unit>ms|s|m|h)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public HtmlReportService(SummaryConfiguration config) => _config = config;

    public async Task GenerateHtmlReportAsync(ProcessingSummary summary, ProcessingResults results, string outputPath)
    {
        if (!_config.HtmlReportSettings.EnableHtmlReport) return;
        var outputDir = Path.GetDirectoryName(outputPath)!;
        Directory.CreateDirectory(outputDir);
        StrictCopyAssets(outputDir);
        var html = BuildHtml(summary, results);
        await File.WriteAllTextAsync(outputPath, html, new UTF8Encoding(false));
    }

    private void StrictCopyAssets(string outputDir)
    {
        var sourceAssets = ResolveAssetsSourceDirectory() ?? throw new DirectoryNotFoundException("Unable to locate 'Assets' directory for report generation.");
        var destAssets = Path.Combine(outputDir, "Assets");
        if (Directory.Exists(destAssets)) Directory.Delete(destAssets, true);
        Directory.CreateDirectory(destAssets);
        foreach (var dir in Directory.GetDirectories(sourceAssets, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destAssets, Path.GetRelativePath(sourceAssets, dir)));
        foreach (var file in Directory.GetFiles(sourceAssets, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceAssets, file);
            var target = Path.Combine(destAssets, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private string? ResolveAssetsSourceDirectory()
    {
        var candidates = new List<string> { Path.Combine(AppContext.BaseDirectory, "Assets"), Path.Combine(Directory.GetCurrentDirectory(), "Assets") };
        var walker = Directory.GetCurrentDirectory();
        for (int i = 0; i < 8 && !string.IsNullOrEmpty(walker); i++)
        {
            candidates.Add(Path.Combine(walker, "Assets"));
            walker = Directory.GetParent(walker)?.FullName ?? string.Empty;
        }
        return candidates.Distinct().FirstOrDefault(Directory.Exists);
    }

    private string BuildHtml(ProcessingSummary summary, ProcessingResults results)
    {
        var reportData = PrepareReportData(summary, results);
        var sb = new StringBuilder(500_000);
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\" />");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine("<title>Allure Test Report</title>");
        sb.AppendLine("<link rel=\"stylesheet\" href=\"https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.0/css/all.min.css\" referrerpolicy=\"no-referrer\">");
        sb.AppendLine("<link rel=\"stylesheet\" href=\"Assets/styles.css\" />");
        sb.AppendLine("</head>");
        sb.AppendLine("<body data-theme=\"dark\"><div class='app'>");
        BuildHeader(sb); 
        BuildNavigation(sb); 
        BuildDashboard(sb); 
        BuildTestResultsSection(sb); 
        BuildSuitePerformanceSection(sb); 
        BuildSlowTestsSection(sb); 
        BuildTopStepsSection(sb, summary.TopSlowSteps);
        BuildFailureAnalysisSection(sb); // NEW: Add failure analysis section
        BuildScreenshotModal(sb);
        sb.AppendLine("</div>");
        sb.AppendLine("<script>function updateStickyOffset(){const h=(document.querySelector('.header')?.offsetHeight||0)+(document.querySelector('.nav')?.offsetHeight||0);document.documentElement.style.setProperty('--sticky-offset',h+'px');}window.addEventListener('load',updateStickyOffset);window.addEventListener('resize',updateStickyOffset);</script>");
        sb.AppendLine("<script id=\"reportData\" type=\"application/json\">");
        sb.AppendLine(JsonSerializer.Serialize(reportData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false }));
        sb.AppendLine("</script>");
        sb.AppendLine("<script src=\"https://cdn.jsdelivr.net/npm/chart.js\"></script>");
        sb.AppendLine("<script src=\"Assets/script.js\"></script>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private object PrepareReportData(ProcessingSummary summary, ProcessingResults results)
    {
        var o = summary.Overview;
        var enriched = results.TestResults.Select(t => new
        {
            t.SuiteName,
            t.TestCaseName,
            t.Duration,
            t.Status,
            t.CaseTags,
            t.FailingStep,
            t.FailureReason,
            t.ScreenshotPath,
            DurationMs = ParseDurationToMs(t.Duration),
            PerformanceCategory = ClassifyPerformance(ParseDurationToMs(t.Duration)),
            ScreenshotFileName = !string.IsNullOrEmpty(t.ScreenshotPath) ? Path.GetFileName(t.ScreenshotPath) : null
        }).ToList();
        double passRate = o.TotalTests > 0 ? (double)o.PassedTests / o.TotalTests * 100 : 0;
        double failureRate = o.TotalTests > 0 ? (double)(o.FailedTests + o.BrokenTests) / o.TotalTests * 100 : 0;
        int healthScore = CalculateHealthScore(passRate, failureRate, enriched.Count(t => t.Status == "Failed"));
        var suitePerformance = enriched.GroupBy(t => t.SuiteName).Select(g =>
        {
            long total = g.Sum(x => x.DurationMs); 
            double avg = g.Any() ? g.Average(x => (double)x.DurationMs) : 0;
            var totalTests = g.Count();
            var passedTests = g.Count(t => t.Status == "Passed");
            var failedTests = g.Count(t => t.Status == "Failed");
            var brokenTests = g.Count(t => t.Status == "Broken");
            var allFailures = failedTests + brokenTests; // Combine failed and broken for display
            
            return new { 
                SuiteName = g.Key, 
                TotalTests = totalTests, 
                PassedTests = passedTests, 
                FailedTests = failedTests,  // Keep individual failed count for sorting
                BrokenTests = brokenTests,  // Keep individual broken count for sorting
                AllFailures = allFailures,  // Combined failures for display
                PassRate = passedTests / (double)totalTests * 100, 
                TotalDurationMs = total, 
                TotalDurationReadable = FormatDuration(total), 
                AvgDurationMs = avg, 
                AvgDurationReadable = FormatDuration((long)avg), 
                PerformanceCategory = ClassifyPerformance((long)avg) 
            };
        }).OrderByDescending(s => s.TotalDurationMs).Take(_config.HtmlReportSettings.ShowTopSuitesCount).ToList();
        var slowTests = enriched.OrderByDescending(t => t.DurationMs).Take(_config.HtmlReportSettings.ShowTopSlowTestsCount).Select(t => new { t.SuiteName, t.TestCaseName, t.Duration, t.Status, t.ScreenshotPath, t.DurationMs, t.PerformanceCategory, t.ScreenshotFileName }).ToList();
        var orderedTests = enriched.OrderBy(t => t.SuiteName).ThenBy(t => t.TestCaseName).ToList();
        
        // Generate failure analysis - Fixed to use actual TestResult objects
        var failureAnalysisService = new FailureAnalysisService(_config);
        var failureAnalysis = failureAnalysisService.AnalyzeFailures(results.TestResults);
        
        return new
        {
            Overview = new { o.TotalTests, o.PassedTests, o.FailedTests, o.BrokenTests, o.ScreenshotsCopied, PassRate = passRate, FailureRate = failureRate, HealthScore = healthScore, ExecutionTime = o.TotalExecutionTime ?? "0ms", GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
            SuitePerformance = suitePerformance,
            SlowTests = slowTests,
            TestResults = orderedTests,
            TopSteps = summary.TopSlowSteps.Select(s => new { s.Rank, s.StepName, s.TruncatedStepName, s.AvgDurationMs, s.MinDurationMs, s.MaxDurationMs, s.AvgDurationReadable, s.MinDurationReadable, s.MaxDurationReadable, s.TotalDurationReadable, s.Count, s.FailRate, s.PerformanceCategory, s.ReliabilityCategory }).ToList(),
            // Include failure analysis data with proper structure
            FailureAnalysis = new {
                FailureCategories = new {
                    TotalFailures = failureAnalysis.FailureCategories.TotalFailures,
                    AssertionFailures = failureAnalysis.FailureCategories.AssertionFailures,
                    SeleniumIssues = failureAnalysis.FailureCategories.SeleniumIssues,
                    TimeoutIssues = failureAnalysis.FailureCategories.TimeoutIssues,
                    EnvironmentIssues = failureAnalysis.FailureCategories.EnvironmentIssues,
                    UnknownIssues = failureAnalysis.FailureCategories.UnknownIssues,
                    AssertionFailureRate = failureAnalysis.FailureCategories.TotalFailures > 0 ? 
                        (double)failureAnalysis.FailureCategories.AssertionFailures / failureAnalysis.FailureCategories.TotalFailures * 100 : 0,
                    SeleniumFailureRate = failureAnalysis.FailureCategories.TotalFailures > 0 ? 
                        (double)failureAnalysis.FailureCategories.SeleniumIssues / failureAnalysis.FailureCategories.TotalFailures * 100 : 0,
                    TimeoutFailureRate = failureAnalysis.FailureCategories.TotalFailures > 0 ? 
                        (double)failureAnalysis.FailureCategories.TimeoutIssues / failureAnalysis.FailureCategories.TotalFailures * 100 : 0,
                    EnvironmentFailureRate = failureAnalysis.FailureCategories.TotalFailures > 0 ? 
                        (double)failureAnalysis.FailureCategories.EnvironmentIssues / failureAnalysis.FailureCategories.TotalFailures * 100 : 0
                },
                CommonFailures = failureAnalysis.CommonFailures.Take(10).ToList(),
                TestCaseAnalysis = failureAnalysis.TestCaseAnalysis.Take(20).ToList(),
                SeleniumAnalysis = new {
                    TotalSeleniumIssues = failureAnalysis.SeleniumAnalysis.TotalSeleniumIssues,
                    IssueCategories = failureAnalysis.SeleniumAnalysis.IssueCategories,
                    TopSeleniumFailures = failureAnalysis.SeleniumAnalysis.TopSeleniumFailures
                },
                TimeoutAnalysis = new {
                    TotalTimeoutIssues = failureAnalysis.TimeoutAnalysis.TotalTimeoutIssues,
                    TimeoutCategories = failureAnalysis.TimeoutAnalysis.TimeoutCategories,
                    TopTimeoutSteps = failureAnalysis.TimeoutAnalysis.TopTimeoutSteps,
                    AverageTimeoutDuration = ""  // Could be calculated if needed
                }
            },
            Config = new { IncludeScreenshots = _config.HtmlReportSettings.IncludeScreenshots, EnableInteractiveFiltering = _config.HtmlReportSettings.EnableInteractiveFiltering, PerformanceThresholds = new { CriticalMs = _config.PerformanceThresholds.CriticalThresholdMs, HighMs = _config.PerformanceThresholds.HighThresholdMs, MediumMs = _config.PerformanceThresholds.MediumThresholdMs } }
        };
    }

    private void BuildHeader(StringBuilder sb)
    {
        sb.AppendLine("<header class='header'><div class='header-layout'><div class='header-left'></div><div class='header-center'><h1><i class='fa-solid fa-vials'></i> Allure Test Report</h1><div class='header-meta' id='generatedTime'></div></div><div class='header-actions'><button id='themeToggle' class='theme-toggle' type='button' aria-label='Toggle theme'>🌗</button></div></div></header>");
    }

    private void BuildNavigation(StringBuilder sb)
    {
        sb.AppendLine("<nav class='nav'><div class='container'><ul class='nav-links'>" +
            "<li><a href='#dashboard' class='nav-link active'><i class='fa-solid fa-chart-pie'></i><span class='icon-label'>Dashboard Summary</span></a></li>" +
            "<li><a href='#test-results' class='nav-link'><i class='fa-solid fa-list-check'></i><span class='icon-label'>Test Results</span></a></li>" +
            "<li><a href='#suite-performance' class='nav-link'><i class='fa-solid fa-layer-group'></i><span class='icon-label'>Suite Performance</span></a></li>" +
            "<li><a href='#slow-tests' class='nav-link'><i class='fa-solid fa-hourglass-half'></i><span class='icon-label'>Slow Tests</span></a></li>" +
            "<li><a href='#top-steps' class='nav-link'><i class='fa-solid fa-list-ol'></i><span class='icon-label'>Top Steps</span></a></li>" +
            "<li><a href='#failure-analysis' class='nav-link'><i class='fa-solid fa-magnifying-glass-chart'></i><span class='icon-label'>Failure Analysis</span></a></li>" +
            "</ul></div></nav>");
    }

    private void BuildDashboard(StringBuilder sb) => sb.AppendLine("<section id='dashboard' class='section active'><div class='container'><h2><i class='fa-solid fa-chart-pie'></i> Dashboard Summary</h2><div class='metrics-grid' id='metricsGrid'></div><div class='charts-row'><div class='chart-container'><canvas id='statusChart'></canvas></div></div></div></section>");

    private void BuildTestResultsSection(StringBuilder sb)
    {
        sb.AppendLine("<section id='test-results' class='section'><div class='container'><h2><i class='fa-solid fa-list-check'></i> Test Results</h2><div class='filters'><div class='filter-group'><input type='text' id='searchInput' placeholder='Search tests or tags...' class='search-input search-with-icon' /></div><div class='filter-group'><select id='statusFilter' class='filter-select'><option value=''>All Status</option><option value='Passed'>Passed</option><option value='Failed'>Failed</option><option value='Broken'>Broken</option></select></div><div class='filter-group'><select id='suiteFilter' class='filter-select'><option value=''>All Suites</option></select></div></div><div class='table-controls'><div class='results-info' id='resultsInfo'></div><div class='pagination-controls'><select id='pageSizeSelect' class='page-size-select'><option value='25'>25 per page</option><option value='50'>50 per page</option><option value='100'>100 per page</option><option value='all'>Show All</option></select><div class='pagination' id='pagination'></div></div></div><div class='table-container'><table id='testResultsTable' class='results-table'><thead><tr><th data-sort='index'>#</th><th data-sort='suiteName'>Suite <span class='sort-indicator'></span></th><th data-sort='testCaseName'>Test Name <span class='sort-indicator'></span></th><th data-sort='status'>Status <span class='sort-indicator'></span></th><th data-sort='durationMs'>Duration <span class='sort-indicator'></span></th><th data-sort='performanceCategory'>Performance <span class='sort-indicator'></span></th><th>Tags</th><th>Failing Step</th><th>Failure Reason</th><th>Screenshot</th></tr></thead><tbody id='testResultsBody'></tbody></table></div></div></section>");
    }

    private void BuildSuitePerformanceSection(StringBuilder sb) => sb.AppendLine("<section id='suite-performance' class='section'><div class='container'><h2><i class='fa-solid fa-layer-group'></i> Suite Performance</h2><div class='table-container'><table class='performance-table'><thead><tr><th>#</th><th>Suite Name</th><th>Total Tests</th><th>Passed</th><th>Failed + Broken</th><th>Pass Rate</th><th>Total Duration</th><th>Avg Duration</th><th>Performance</th></tr></thead><tbody id='suitePerformanceBody'></tbody></table></div></div></section>");

    private void BuildSlowTestsSection(StringBuilder sb) => sb.AppendLine("<section id='slow-tests' class='section'><div class='container'><h2><i class='fa-solid fa-hourglass-half'></i> Slow Tests</h2><div class='table-container'><table class='slow-tests-table'><thead><tr><th>#</th><th>Test Name</th><th>Suite</th><th>Duration</th><th>Status</th><th>Performance</th><th>Screenshot</th></tr></thead><tbody id='slowTestsBody'></tbody></table></div></div></section>");

    private void BuildTopStepsSection(StringBuilder sb, dynamic topSteps)
    {
        sb.AppendLine("<section id='top-steps' class='section'><div class='container'><h2><i class='fa-solid fa-list-ol'></i> Top Steps</h2>");
        if (topSteps?.Count > 0)
            sb.AppendLine("<div class='table-container'><table class='steps-table'><thead><tr><th>#</th><th>Step Name</th><th data-sort='minDurationMs'>Min <span class='sort-indicator'></span></th><th data-sort='avgDurationMs'>Avg <span class='sort-indicator'></span></th><th data-sort='maxDurationMs'>Max <span class='sort-indicator'></span></th><th data-sort='totalDurationMs'>Total <span class='sort-indicator'></span></th><th data-sort='count'>Count <span class='sort-indicator'></span></th><th data-sort='failRate'>Fail Rate % <span class='sort-indicator'></span></th><th>Performance</th><th>Reliability</th></tr></thead><tbody id='topStepsBody'></tbody></table></div>");
        else sb.AppendLine("<div class='empty-state'><p>No step timing data available.</p></div>");
        sb.AppendLine("</div></section>");
    }

    private void BuildFailureAnalysisSection(StringBuilder sb) // NEW: Build failure analysis section
    {
        sb.AppendLine("<section id='failure-analysis' class='section'><div class='container'>");
        sb.AppendLine("<h2><i class='fa-solid fa-magnifying-glass-chart'></i> Failure Analysis</h2>");
        
        // Failure Categorization Overview
        sb.AppendLine("<div class='failure-overview'>");
        sb.AppendLine("<h3><i class='fa-solid fa-chart-column'></i> Failure Categorization</h3>");
        sb.AppendLine("<div class='failure-stats-grid' id='failureStatsGrid'></div>");
        sb.AppendLine("</div>");
        
        // Common Failure Patterns
        sb.AppendLine("<div class='common-failures'>");
        sb.AppendLine("<h3><i class='fa-solid fa-exclamation-triangle'></i> Common Failure Patterns</h3>");
        sb.AppendLine("<div class='table-container'>");
        sb.AppendLine("<table class='failure-patterns-table'>");
        sb.AppendLine("<thead><tr><th>Pattern</th><th>Category</th><th>Count</th><th>Test Cases</th><th>Affected Suites</th><th>Impact</th><th>Recommended Action</th></tr></thead>");
        sb.AppendLine("<tbody id='commonFailuresBody'></tbody>");
        sb.AppendLine("</table></div></div>");
        
        // Test Case Analysis
        sb.AppendLine("<div class='testcase-analysis'>");
        sb.AppendLine("<h3><i class='fa-solid fa-hashtag'></i> Test Case Failure Analysis</h3>");
        sb.AppendLine("<div class='table-container'>");
        sb.AppendLine("<table class='testcase-failures-table'>");
        sb.AppendLine("<thead><tr><th>Test Case ID</th><th>Failures</th><th>Category</th><th>Primary Reason</th><th>Affected Suites</th><th>Actions</th></tr></thead>");
        sb.AppendLine("<tbody id='testCaseFailuresBody'></tbody>");
        sb.AppendLine("</table></div></div>");
        
        // Selenium-specific Analysis
        sb.AppendLine("<div class='selenium-analysis'>");
        sb.AppendLine("<h3><i class='fa-solid fa-robot'></i> Selenium Issues Analysis</h3>");
        sb.AppendLine("<div class='selenium-stats' id='seleniumStats'></div>");
        sb.AppendLine("<div class='table-container'>");
        sb.AppendLine("<table class='selenium-issues-table'>");
        sb.AppendLine("<thead><tr><th>Issue Category</th><th>Count</th><th>Percentage</th><th>Recommended Fix</th></tr></thead>");
        sb.AppendLine("<tbody id='seleniumIssuesBody'></tbody>");
        sb.AppendLine("</table></div></div>");
        
        // Timeout Analysis
        sb.AppendLine("<div class='timeout-analysis'>");
        sb.AppendLine("<h3><i class='fa-solid fa-clock'></i> Timeout Issues Analysis</h3>");
        sb.AppendLine("<div class='timeout-stats' id='timeoutStats'></div>");
        sb.AppendLine("<div class='table-container'>");
        sb.AppendLine("<table class='timeout-issues-table'>");
        sb.AppendLine("<thead><tr><th>Timeout Type</th><th>Count</th><th>Percentage</th><th>Recommended Fix</th></tr></thead>");
        sb.AppendLine("<tbody id='timeoutIssuesBody'></tbody>");
        sb.AppendLine("</table></div></div>");
        
        sb.AppendLine("</div></section>");
    }

    private void BuildScreenshotModal(StringBuilder sb) => sb.AppendLine("<div id='screenshotModal' class='modal'><div class='modal-content'><div class='modal-header'><h3 id='modalTitle'><i class='fa-solid fa-image'></i> Screenshot</h3><button class='modal-close' id='modalClose' aria-label='Close'>&times;</button></div><div class='modal-body'><div id='modalDetails' class='modal-details'></div><img id='modalImage' src='' alt='Screenshot' loading='lazy' /><div id='modalError' class='modal-error' style='display:none'></div></div></div></div>");

    private long ParseDurationToMs(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration)) return 0; double total = 0;
        foreach (Match m in DurationTokenRegex.Matches(duration))
        {
            if (!double.TryParse(m.Groups["val"].Value, out var value)) continue;
            switch (m.Groups["unit"].Value.ToLowerInvariant()) { case "ms": total += value; break; case "s": total += value * 1000; break; case "m": total += value * 60_000; break; case "h": total += value * 3_600_000; break; }
        }
        return (long)Math.Round(total, MidpointRounding.AwayFromZero);
    }

    private static string FormatDuration(long ms)
    {
        if (ms <= 0) return "0 ms"; var h = ms / 3_600_000; ms %= 3_600_000; var m = ms / 60_000; ms %= 60_000; var s = ms / 1000; var builder = new StringBuilder();
        if (h > 0) builder.Append(h).Append(" h "); if (m > 0) builder.Append(m).Append(" m "); if (s > 0 && h < 5) builder.Append(s).Append(" s "); if (h == 0 && m == 0 && s == 0) builder.Append(ms).Append(" ms"); return builder.ToString().Trim();
    }

    private string ClassifyPerformance(long durationMs)
    { if (durationMs >= _config.PerformanceThresholds.CriticalThresholdMs) return "Critical"; if (durationMs >= _config.PerformanceThresholds.HighThresholdMs) return "High"; if (durationMs >= _config.PerformanceThresholds.MediumThresholdMs) return "Medium"; return "Low"; }

    private int CalculateHealthScore(double passRate, double failureRate, int failedCount)
    { var score = 100; score -= (int)failureRate; score -= failedCount; return Math.Clamp(score, 0, 100); }

    private static string Html(string? s) => string.IsNullOrEmpty(s) ? string.Empty : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
