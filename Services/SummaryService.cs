using System.Text;
using AllureReportProcessor.Models;
using AllureReportProcessor.Utils;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;

namespace AllureReportProcessor.Services;

public class SummaryService
{
    private readonly SummaryConfiguration _config;

    public SummaryService(SummaryConfiguration config)
    {
        _config = config ?? new SummaryConfiguration();
    }

    // Parameterless constructor for backward compatibility
    public SummaryService() : this(new SummaryConfiguration())
    {
    }

    /// <summary>
    /// Creates a structured summary model from processing results
    /// </summary>
    public ProcessingSummary CreateProcessingSummary(ProcessingResults results, int totalFiles)
    {
        var summary = new ProcessingSummary();

        // DEBUG: Add debugging for execution time calculation
        Console.WriteLine($"[DEBUG] CreateProcessingSummary - Processing {results.TestResults.Count} test results");
        
        var testsWithDuration = results.TestResults.Where(t => !string.IsNullOrEmpty(t.Duration)).ToList();
        Console.WriteLine($"[DEBUG] Tests with non-empty duration: {testsWithDuration.Count}");
        
        if (testsWithDuration.Count > 0)
        {
            Console.WriteLine($"[DEBUG] Sample durations: {string.Join(", ", testsWithDuration.Take(5).Select(t => t.Duration))}");
        }

        // Calculate total execution time from all test results
        var totalExecutionTimeMs = results.TestResults
            .Where(t => !string.IsNullOrEmpty(t.Duration))
            .Sum(t => ParseDurationToMs(t.Duration));

        Console.WriteLine($"[DEBUG] Total execution time calculated: {totalExecutionTimeMs} ms");
        Console.WriteLine($"[DEBUG] Total execution time readable: {TimeUtils.ConvertMillisecondsToReadable(totalExecutionTimeMs)}");

        // Let's also check a few individual parsing results
        if (testsWithDuration.Count > 0)
        {
            foreach (var test in testsWithDuration.Take(3))
            {
                var parsedMs = ParseDurationToMs(test.Duration);
                Console.WriteLine($"[DEBUG] Duration '{test.Duration}' -> {parsedMs} ms");
            }
        }

        // Populate overview
        summary.Overview = new SummaryOverview
        {
            TotalFilesFound = totalFiles,
            SuccessfullyProcessed = results.ProcessedCount,
            FailedToProcess = results.FailedCount,
            ScreenshotsCopied = results.ScreenshotsCopied,
            PassedTests = results.TestResults.Count(t => t.Status == "Passed"),
            FailedTests = results.TestResults.Count(t => t.Status == "Failed"),
            BrokenTests = results.TestResults.Count(t => t.Status == "Broken"),
            TotalStepsAnalyzed = results.StepsProcessed,
            TotalExecutionTime = TimeUtils.ConvertMillisecondsToReadable(totalExecutionTimeMs)
        };

        // Calculate top slow steps (configurable count)
        if (results.StepTimings.Count > 0)
        {
            var stepGroups = results.StepTimings
                .GroupBy(s => s.StepName.Trim())
                .Select(g => new
                {
                    StepName = g.Key,
                    Count = g.Count(),
                    AvgDurationMs = g.Average(s => s.DurationMs),
                    MinDurationMs = g.Min(s => s.DurationMs),
                    MaxDurationMs = g.Max(s => s.DurationMs),
                    TotalDurationMs = g.Sum(s => (long)s.DurationMs),
                    FailedCount = g.Count(s => !s.Status.Equals("passed", StringComparison.OrdinalIgnoreCase))
                })
                .OrderByDescending(g => g.TotalDurationMs)
                .Take(_config.TopSlowStepsCount)
                .ToList();

            int rank = 1;
            summary.TopSlowSteps = stepGroups.Select(g => new TopStepGroup
            {
                Rank = rank++,
                StepName = g.StepName,
                TruncatedStepName = g.StepName.Length > _config.StepNameTruncationLength
                    ? g.StepName[.._config.StepNameTruncationLength] + "..."
                    : g.StepName,
                Count = g.Count,
                AvgDurationMs = g.AvgDurationMs,
                MinDurationMs = g.MinDurationMs,
                MaxDurationMs = g.MaxDurationMs,
                TotalDurationMs = g.TotalDurationMs,
                FailedCount = g.FailedCount,
                FailRate = g.Count > 0 ? (double)g.FailedCount / g.Count * 100 : 0,
                AvgDurationReadable = TimeUtils.ConvertMillisecondsToReadable((long)g.AvgDurationMs),
                MinDurationReadable = TimeUtils.ConvertMillisecondsToReadable(g.MinDurationMs),
                MaxDurationReadable = TimeUtils.ConvertMillisecondsToReadable(g.MaxDurationMs),
                TotalDurationReadable = TimeUtils.ConvertMillisecondsToReadable(g.TotalDurationMs),
                PerformanceCategory = CategorizePerformance(g.TotalDurationMs),
                ReliabilityCategory = CategorizeReliability(g.Count > 0 ? (double)g.FailedCount / g.Count * 100 : 0)
            }).ToList();
        }

        return summary;
    }

    /// <summary>
    /// Saves a professional Excel summary report with four sheets
    /// </summary>
    public async Task SaveSummaryExcelAsync(ProcessingSummary summary, ProcessingResults results, string filePath)
    {
        // DEBUG: Add debugging information at the start
        Console.WriteLine($"[DEBUG] SaveSummaryExcelAsync - TestResults count: {results.TestResults?.Count ?? 0}");
        if (results.TestResults?.Count > 0)
        {
            Console.WriteLine($"[DEBUG] Sample test result: SuiteName='{results.TestResults[0].SuiteName}', TestCaseName='{results.TestResults[0].TestCaseName}', Duration='{results.TestResults[0].Duration}', Status='{results.TestResults[0].Status}'");
        }

        await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();

            // 1. Summary Overview worksheet
            CreateSummaryOverviewSheet(workbook, summary);

            // 2. Suite Statistics worksheet
            CreateSuiteStatisticsSheet(workbook, summary, results);

            // 3. Top N Tests per Suite worksheet (configurable)
            CreateTopTestsPerSuiteSheet(workbook, summary, results);

            // 4. Top N Slowest Steps worksheet (configurable)
            CreateTopSlowStepsSheet(workbook, summary);

            workbook.SaveAs(filePath);
        });
    }

    private void CreateSummaryOverviewSheet(XLWorkbook workbook, ProcessingSummary summary)
    {
        var summaryWs = workbook.Worksheets.Add("Processing Summary");

        // Title and metadata
        summaryWs.Cell(1, 1).Value = "Allure Processing Summary Report";
        summaryWs.Cell(1, 1).Style.Font.Bold = true;
        summaryWs.Cell(1, 1).Style.Font.FontSize = 16;

        summaryWs.Cell(2, 1).Value = $"Generated: {summary.GeneratedAt:yyyy-MM-dd HH:mm:ss}";
        summaryWs.Cell(3, 1).Value = $"Report Version: {summary.ReportVersion}";

        // Executive Summary Section
        int row = 5;
        summaryWs.Cell(row, 1).Value = "EXECUTIVE SUMMARY";
        summaryWs.Cell(row, 1).Style.Font.Bold = true;
        summaryWs.Cell(row, 1).Style.Font.FontSize = 14;
        summaryWs.Cell(row, 1).Style.Font.FontColor = XLColor.DarkBlue;
        row += 2;

        // Key metrics in a more prominent format
        var keyMetrics = new (string, object, string)[]
        {
            ("Total Tests Executed", summary.Overview.TotalTests, ""),
            ("Passed Tests", summary.Overview.PassedTests, $"{summary.Overview.TestPassRate:F1}%"),
            ("Failed Tests", summary.Overview.FailedTests, ""),
            ("Broken Tests", summary.Overview.BrokenTests, ""),
            ("Total Execution Time", summary.Overview.TotalExecutionTime ?? "0ms", "")
        };

        foreach (var (metric, value, percentage) in keyMetrics)
        {
            summaryWs.Cell(row, 1).Value = metric;
            summaryWs.Cell(row, 1).Style.Font.Bold = true;
            summaryWs.Cell(row, 2).Value = value switch
            {
                int intValue => intValue,
                double doubleValue => doubleValue,
                string stringValue => stringValue ?? string.Empty,
                null => string.Empty,
                _ => value.ToString() ?? string.Empty
            };
            summaryWs.Cell(row, 2).Style.Font.FontSize = 12;
            if (!string.IsNullOrEmpty(percentage))
            {
                summaryWs.Cell(row, 3).Value = percentage;
                summaryWs.Cell(row, 3).Style.Font.FontSize = 12;
            }
            row++;
        }

        // Add some spacing
        row += 2;

        // Detailed Processing Metrics
        summaryWs.Cell(row, 1).Value = "DETAILED PROCESSING METRICS";
        summaryWs.Cell(row, 1).Style.Font.Bold = true;
        summaryWs.Cell(row, 1).Style.Font.FontSize = 14;
        row += 2;

        summaryWs.Cell(row, 1).Value = "Metric";
        summaryWs.Cell(row, 2).Value = "Value";
        summaryWs.Cell(row, 3).Value = "Percentage";

        // Style headers
        var headerRange = summaryWs.Range(row, 1, row, 3);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        row++;

        // Add detailed metrics
        var detailedMetrics = new (string, object, string)[]
        {
            ("Total Files Found", summary.Overview.TotalFilesFound, ""),
            ("Successfully Processed", summary.Overview.SuccessfullyProcessed, $"{summary.Overview.SuccessRate:F1}%"),
            ("Failed to Process", summary.Overview.FailedToProcess, ""),
            ("Screenshots Copied", summary.Overview.ScreenshotsCopied, ""),
            ("Total Steps Analyzed", summary.Overview.TotalStepsAnalyzed, "")
        };

        foreach (var (metric, value, percentage) in detailedMetrics)
        {
            summaryWs.Cell(row, 1).Value = metric;
            summaryWs.Cell(row, 2).Value = value switch
            {
                int intValue => intValue,
                double doubleValue => doubleValue,
                string stringValue => stringValue ?? string.Empty,
                null => string.Empty,
                _ => value.ToString() ?? string.Empty
            };
            summaryWs.Cell(row, 3).Value = percentage;
            row++;
        }

        // Create table for detailed metrics
        var tableRange = summaryWs.Range(row - detailedMetrics.Length - 1, 1, row - 1, 3);
        var table = tableRange.CreateTable();
        table.Theme = XLTableTheme.TableStyleMedium2;

        if (_config.ExcelSettings.AutoSizeColumns)
        {
            summaryWs.Columns().AdjustToContents();
        }
    }

    private void CreateSuiteStatisticsSheet(XLWorkbook workbook, ProcessingSummary summary, ProcessingResults results)
    {
        var suiteWs = workbook.Worksheets.Add("Suite Statistics");

        // Title
        suiteWs.Cell(1, 1).Value = "Test Suite Statistics";
        suiteWs.Cell(1, 1).Style.Font.Bold = true;
        suiteWs.Cell(1, 1).Style.Font.FontSize = 14;

        int row = 3;
        var headers = new[] {
            "Suite Name", "Total Tests", "Passed", "Failed", "Broken",
            "Pass Rate (%)", "Avg Test Duration", "Total Suite Duration", "Performance Category"
        };

        for (int col = 1; col <= headers.Length; col++)
        {
            suiteWs.Cell(row, col).Value = headers[col - 1];
        }

        // Style headers
        var headerRange = suiteWs.Range(row, 1, row, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        row++;

        // DEBUG: Add debugging information
        Console.WriteLine($"[DEBUG] CreateSuiteStatisticsSheet - TestResults count: {results.TestResults?.Count ?? 0}");

        // Process actual test results data
        if (results.TestResults?.Count > 0)
        {
            var suiteStats = results.TestResults
                .GroupBy(t => t.SuiteName)
                .Select(g => new
                {
                    SuiteName = g.Key,
                    TotalTests = g.Count(),
                    PassedTests = g.Count(t => t.Status == "Passed"),
                    FailedTests = g.Count(t => t.Status == "Failed"),
                    BrokenTests = g.Count(t => t.Status == "Broken"),
                    Durations = g.Where(t => !string.IsNullOrEmpty(t.Duration)).Select(t => t.Duration).ToList()
                })
                .OrderByDescending(s => s.TotalTests)
                .ToList();

            Console.WriteLine($"[DEBUG] Suite stats count: {suiteStats.Count}");

            foreach (var suite in suiteStats)
            {
                var passRate = suite.TotalTests > 0 ? (double)suite.PassedTests / suite.TotalTests * 100 : 0;
                var avgDuration = CalculateAverageDuration(suite.Durations);
                var totalDuration = CalculateTotalDuration(suite.Durations);
                var totalDurationMs = suite.Durations.Sum(ParseDurationToMs);
                var performanceCategory = CategorizePerformance(totalDurationMs);

                suiteWs.Cell(row, 1).Value = suite.SuiteName;
                suiteWs.Cell(row, 2).Value = suite.TotalTests;
                suiteWs.Cell(row, 3).Value = suite.PassedTests;
                suiteWs.Cell(row, 4).Value = suite.FailedTests;
                suiteWs.Cell(row, 5).Value = suite.BrokenTests;
                suiteWs.Cell(row, 6).Value = $"{passRate:F1}%";
                suiteWs.Cell(row, 7).Value = avgDuration;
                suiteWs.Cell(row, 8).Value = totalDuration;
                suiteWs.Cell(row, 9).Value = performanceCategory;
                row++;
            }

            // Create table if we have data
            if (suiteStats.Count > 0)
            {
                var tableRange = suiteWs.Range(3, 1, row - 1, headers.Length);
                var table = tableRange.CreateTable();
                table.Theme = XLTableTheme.TableStyleMedium2;
            }
        }
        else
        {
            suiteWs.Cell(row, 1).Value = "No test results data available";
            suiteWs.Cell(row, 1).Style.Font.Italic = true;
        }

        if (_config.ExcelSettings.AutoSizeColumns)
        {
            suiteWs.Columns().AdjustToContents();
        }
    }

    private void CreateTopTestsPerSuiteSheet(XLWorkbook workbook, ProcessingSummary summary, ProcessingResults results)
    {
        var testsWs = workbook.Worksheets.Add($"Top {_config.TopTestsPerSuiteCount} Tests per Suite");

        // Title
        testsWs.Cell(1, 1).Value = $"Top {_config.TopTestsPerSuiteCount} Slowest Tests per Suite";
        testsWs.Cell(1, 1).Style.Font.Bold = true;
        testsWs.Cell(1, 1).Style.Font.FontSize = 14;

        int row = 3;
        var headers = new[] {
            "Suite Name", "Rank", "Test Name", "Duration", "Status",
            "Parameters", "Failing Step", "Performance Category"
        };

        for (int col = 1; col <= headers.Length; col++)
        {
            testsWs.Cell(row, col).Value = headers[col - 1];
        }

        // Style headers
        var headerRange = testsWs.Range(row, 1, row, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        row++;

        // DEBUG: Add debugging information
        Console.WriteLine($"[DEBUG] CreateTopTestsPerSuiteSheet - TestResults count: {results.TestResults?.Count ?? 0}");
        if (results.TestResults?.Count > 0)
        {
            Console.WriteLine($"[DEBUG] First test: SuiteName='{results.TestResults[0].SuiteName}', TestCaseName='{results.TestResults[0].TestCaseName}', Duration='{results.TestResults[0].Duration}'");
            
            var testsWithDuration = results.TestResults.Where(t => !string.IsNullOrEmpty(t.Duration)).ToList();
            Console.WriteLine($"[DEBUG] Tests with duration: {testsWithDuration.Count}");
            
            if (testsWithDuration.Count > 0)
            {
                Console.WriteLine($"[DEBUG] Sample test with duration: SuiteName='{testsWithDuration[0].SuiteName}', Duration='{testsWithDuration[0].Duration}'");
            }
        }

        // Process actual test results data
        if (results.TestResults?.Count > 0)
        {
            // DEBUG: Let's check the filtering step by step
            var testsWithDuration = results.TestResults.Where(t => !string.IsNullOrEmpty(t.Duration)).ToList();
            Console.WriteLine($"[DEBUG] Tests with non-empty duration: {testsWithDuration.Count}");
            
            var groupedBySuite = testsWithDuration.GroupBy(t => t.SuiteName).ToList();
            Console.WriteLine($"[DEBUG] Number of suites: {groupedBySuite.Count}");
            
            if (groupedBySuite.Count > 0)
            {
                Console.WriteLine($"[DEBUG] First suite '{groupedBySuite[0].Key}' has {groupedBySuite[0].Count()} tests");
            }

            var topTestsPerSuite = results.TestResults
                .Where(t => !string.IsNullOrEmpty(t.Duration))
                .GroupBy(t => t.SuiteName)
                .SelectMany(g => g
                    .OrderByDescending(t => ParseDurationToMs(t.Duration))
                    .Take(_config.TopTestsPerSuiteCount)
                    .Select((test, index) => new
                    {
                        SuiteName = g.Key,
                        Rank = index + 1,
                        TestName = test.TestCaseName,
                        Duration = test.Duration,
                        Status = test.Status,
                        Parameters = test.ParametersKey,
                        FailingStep = test.FailingStep,
                        DurationMs = ParseDurationToMs(test.Duration)
                    }))
                .OrderBy(t => t.SuiteName)
                .ThenBy(t => t.Rank)
                .ToList();

            Console.WriteLine($"[DEBUG] Top tests per suite count: {topTestsPerSuite.Count}");

            foreach (var test in topTestsPerSuite)
            {
                var performanceCategory = CategorizePerformance(test.DurationMs);

                testsWs.Cell(row, 1).Value = test.SuiteName;
                testsWs.Cell(row, 2).Value = test.Rank;
                testsWs.Cell(row, 3).Value = test.TestName;
                testsWs.Cell(row, 4).Value = test.Duration;
                testsWs.Cell(row, 5).Value = test.Status;
                testsWs.Cell(row, 6).Value = test.Parameters;
                testsWs.Cell(row, 7).Value = test.FailingStep;
                testsWs.Cell(row, 8).Value = performanceCategory;
                row++;
            }

            // Create table if we have data
            if (topTestsPerSuite.Count > 0)
            {
                var tableRange = testsWs.Range(3, 1, row - 1, headers.Length);
                var table = tableRange.CreateTable();
                table.Theme = XLTableTheme.TableStyleMedium2;
            }
        }
        else
        {
            testsWs.Cell(row, 1).Value = "No test results data available";
            testsWs.Cell(row, 1).Style.Font.Italic = true;
        }

        if (_config.ExcelSettings.AutoSizeColumns)
        {
            testsWs.Columns().AdjustToContents();
        }
    }

    private void CreateTopSlowStepsSheet(XLWorkbook workbook, ProcessingSummary summary)
    {
        var stepsWs = workbook.Worksheets.Add($"Top {_config.TopSlowStepsCount} Slowest Steps");

        // Headers
        stepsWs.Cell(1, 1).Value = $"Top {_config.TopSlowStepsCount} Slowest Step Groups";
        stepsWs.Cell(1, 1).Style.Font.Bold = true;
        stepsWs.Cell(1, 1).Style.Font.FontSize = 14;

        int row = 3;
        var stepHeaders = new[] {
            "Rank", "Step Name", "Count", "Min Duration", "Avg Duration", "Max Duration", "Total Duration",
            "Failed Count", "Fail Rate (%)", "Performance Category", "Reliability Category"
        };

        for (int col = 1; col <= stepHeaders.Length; col++)
        {
            stepsWs.Cell(row, col).Value = stepHeaders[col - 1];
        }

        // Style headers
        var stepHeaderRange = stepsWs.Range(row, 1, row, stepHeaders.Length);
        stepHeaderRange.Style.Font.Bold = true;
        stepHeaderRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        row++;

        // Add step data
        foreach (var step in summary.TopSlowSteps)
        {
            stepsWs.Cell(row, 1).Value = step.Rank;
            stepsWs.Cell(row, 2).Value = step.StepName;
            stepsWs.Cell(row, 3).Value = step.Count;
            stepsWs.Cell(row, 4).Value = step.MinDurationReadable;
            stepsWs.Cell(row, 5).Value = step.AvgDurationReadable;
            stepsWs.Cell(row, 6).Value = step.MaxDurationReadable;
            stepsWs.Cell(row, 7).Value = step.TotalDurationReadable;
            stepsWs.Cell(row, 8).Value = step.FailedCount;
            stepsWs.Cell(row, 9).Value = $"{step.FailRate:F1}";
            stepsWs.Cell(row, 10).Value = step.PerformanceCategory;
            stepsWs.Cell(row, 11).Value = step.ReliabilityCategory;

            row++;
        }

        // Create steps table
        if (summary.TopSlowSteps.Count > 0)
        {
            var stepsTableRange = stepsWs.Range(3, 1, row - 1, stepHeaders.Length);
            var stepsTable = stepsTableRange.CreateTable();
            stepsTable.Theme = XLTableTheme.TableStyleMedium2;
        }

        if (_config.ExcelSettings.AutoSizeColumns)
        {
            stepsWs.Columns().AdjustToContents();
        }
    }

    // Helper methods for duration calculations
    private string CalculateAverageDuration(IEnumerable<string> durations)
    {
        var validDurations = durations.Where(d => !string.IsNullOrEmpty(d)).ToList();
        if (!validDurations.Any()) return "0ms";

        var totalMs = validDurations.Sum(ParseDurationToMs);
        var avgMs = totalMs / validDurations.Count;
        return TimeUtils.ConvertMillisecondsToReadable(avgMs);
    }

    private string CalculateTotalDuration(IEnumerable<string> durations)
    {
        var validDurations = durations.Where(d => !string.IsNullOrEmpty(d)).ToList();
        if (!validDurations.Any()) return "0ms";

        var totalMs = validDurations.Sum(ParseDurationToMs);
        return TimeUtils.ConvertMillisecondsToReadable(totalMs);
    }

    /// <summary>
    /// Public method to parse duration strings to milliseconds
    /// </summary>
    public long ParseDurationStringToMs(string duration)
    {
        return ParseDurationToMs(duration);
    }

    private long ParseDurationToMs(string duration)
    {
        if (string.IsNullOrEmpty(duration)) return 0;

        // Handle different duration formats
        duration = duration.Trim().ToLower();

        try
        {
            // Handle "X m Y s" format (with spaces) - e.g., "4 m 40 s"
            if (duration.Contains(" m ") && duration.Contains(" s"))
            {
                var parts = duration.Split(' ');
                if (parts.Length == 4 && parts[1] == "m" && parts[3] == "s")
                {
                    if (double.TryParse(parts[0], out var minutes) && 
                        double.TryParse(parts[2], out var seconds))
                    {
                        return (long)((minutes * 60 + seconds) * 1000);
                    }
                }
            }
            // Handle "X m" format (with space) - e.g., "5 m"
            else if (duration.EndsWith(" m"))
            {
                var minString = duration[..^2].Trim();
                if (double.TryParse(minString, out var minutes))
                    return (long)(minutes * 60000);
            }
            // Handle "X s" format (with space) - e.g., "39.2 s"
            else if (duration.EndsWith(" s"))
            {
                var secString = duration[..^2].Trim();
                if (double.TryParse(secString, out var seconds))
                    return (long)(seconds * 1000);
            }
            // Handle formats without spaces
            else if (duration.EndsWith("ms"))
            {
                var msString = duration[..^2].Trim();
                if (long.TryParse(msString, out var ms))
                    return ms;
            }
            else if (duration.EndsWith("s"))
            {
                var secString = duration[..^1].Trim();
                if (double.TryParse(secString, out var seconds))
                    return (long)(seconds * 1000);
            }
            else if (duration.EndsWith("m") && !duration.Contains(":"))
            {
                var minString = duration[..^1].Trim();
                if (double.TryParse(minString, out var minutes))
                    return (long)(minutes * 60000);
            }
            else if (duration.Contains(":"))
            {
                // Handle MM:SS or HH:MM:SS format
                var parts = duration.Split(':');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out var minutes) &&
                    double.TryParse(parts[1], out var seconds))
                {
                    return (long)((minutes * 60 + seconds) * 1000);
                }
                else if (parts.Length == 3 &&
                    int.TryParse(parts[0], out var hours) &&
                    int.TryParse(parts[1], out var mins) &&
                    double.TryParse(parts[2], out var secs))
                {
                    return (long)((hours * 3600 + mins * 60 + secs) * 1000);
                }
            }
            else if (long.TryParse(duration, out var directMs))
            {
                // Handle direct millisecond values (numbers without suffix)
                return directMs;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error parsing duration '{duration}': {ex.Message}");
        }

        Console.WriteLine($"[DEBUG] Failed to parse duration: '{duration}' - returning 0");
        return 0;
    }

    public string GenerateSummaryReport(ProcessingResults results, int totalFiles)
    {
        var summary = CreateProcessingSummary(results, totalFiles);

        var sb = new StringBuilder();
        sb.AppendLine("=== PROCESSING SUMMARY ===");
        sb.AppendLine($"Total files found: {summary.Overview.TotalFilesFound}");
        sb.AppendLine($"Successfully processed: {summary.Overview.SuccessfullyProcessed}");
        sb.AppendLine($"Failed to process: {summary.Overview.FailedToProcess}");
        sb.AppendLine($"Screenshots copied: {summary.Overview.ScreenshotsCopied}");
        sb.AppendLine($"Passed tests: {summary.Overview.PassedTests}");
        sb.AppendLine($"Failed tests: {summary.Overview.FailedTests}");
        sb.AppendLine($"Broken tests: {summary.Overview.BrokenTests}");
        sb.AppendLine($"Total steps analyzed: {summary.Overview.TotalStepsAnalyzed}");
        sb.AppendLine($"Total execution time: {summary.Overview.TotalExecutionTime}");
        sb.AppendLine();
        sb.AppendLine($"🌟 TOP {_config.TopSlowStepsCount} SLOWEST STEP GROUPS:");

        foreach (var step in summary.TopSlowSteps)
        {
            sb.AppendLine($"{step.Rank}. {step.TruncatedStepName}");
            sb.AppendLine($"   Count: {step.Count}, Min: {step.MinDurationReadable}, Avg: {step.AvgDurationReadable}, Max: {step.MaxDurationReadable}, Total: {step.TotalDurationReadable}, Fail Rate: {step.FailRate:F1}%");
        }

        return sb.ToString();
    }

    public void PrintSummary(ProcessingResults results, int totalFiles)
    {
        var summary = CreateProcessingSummary(results, totalFiles);

        Console.WriteLine("\n=== PROCESSING SUMMARY ===");
        Console.WriteLine($"Total files found: {summary.Overview.TotalFilesFound}");
        Console.WriteLine($"Successfully processed: {summary.Overview.SuccessfullyProcessed}");
        Console.WriteLine($"Failed to process: {summary.Overview.FailedToProcess}");
        Console.WriteLine($"Screenshots copied: {summary.Overview.ScreenshotsCopied}");
        Console.WriteLine($"Passed tests: {summary.Overview.PassedTests}");
        Console.WriteLine($"Failed tests: {summary.Overview.FailedTests}");
        Console.WriteLine($"Broken tests: {summary.Overview.BrokenTests}");
        Console.WriteLine($"Total steps analyzed: {summary.Overview.TotalStepsAnalyzed}");
        Console.WriteLine($"Total execution time: {summary.Overview.TotalExecutionTime}");

        if (summary.TopSlowSteps.Count > 0)
        {
            Console.WriteLine($"\n🌟 TOP {_config.TopSlowStepsCount} SLOWEST STEP GROUPS:");

            foreach (var step in summary.TopSlowSteps)
            {
                Console.WriteLine($"{step.Rank}. {step.TruncatedStepName}");
                Console.WriteLine($"   Count: {step.Count}, Min: {step.MinDurationReadable}, Avg: {step.AvgDurationReadable}, Max: {step.MaxDurationReadable}, Total: {step.TotalDurationReadable}, Fail Rate: {step.FailRate:F1}%");
            }
        }
    }

    public async Task SaveSummaryToFileAsync(string summary, string outputPath)
    {
        try
        {
            await File.WriteAllTextAsync(outputPath, summary, Encoding.UTF8);
            Console.WriteLine($"Successfully exported summary report: {Path.GetFileName(outputPath)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nWarning: Failed to save summary report: {ex.Message}");
        }
    }

    private string CategorizePerformance(long totalDurationMs)
    {
        return totalDurationMs switch
        {
            var ms when ms > _config.PerformanceThresholds.CriticalThresholdMs => "Critical",
            var ms when ms > _config.PerformanceThresholds.HighThresholdMs => "High",
            var ms when ms > _config.PerformanceThresholds.MediumThresholdMs => "Medium",
            _ => "Low"
        };
    }

    private string CategorizeReliability(double failRate)
    {
        return failRate switch
        {
            var rate when rate > _config.ReliabilityThresholds.UnreliableThreshold => "Unreliable",
            var rate when rate > _config.ReliabilityThresholds.PoorThreshold => "Poor",
            var rate when rate > _config.ReliabilityThresholds.GoodThreshold => "Good",
            _ => "Excellent"
        };
    }
}