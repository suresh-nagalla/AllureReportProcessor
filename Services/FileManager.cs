using System.Text;
using ClosedXML.Excel;
using AllureReportProcessor.Models;

namespace AllureReportProcessor.Services;

public class FileManager
{
    private readonly ProcessingConfig _config;
    private readonly SummaryService _summaryService;
    private readonly SummaryConfiguration _summaryConfig;

    public FileManager(ProcessingConfig config, SummaryConfiguration? summaryConfig = null)
    {
        _config = config;
        _summaryConfig = summaryConfig ?? new SummaryConfiguration();
        _summaryService = new SummaryService(_summaryConfig);
    }

    public async Task SaveResultsAsync(ProcessingResults results)
    {
        var timestamp = DateTime.Now.ToString("yyMMddHHmmss");
        
        // Create structured summary
        var summary = _summaryService.CreateProcessingSummary(results, results.ProcessedCount + results.FailedCount);

        // Always generate HTML Report first
        var htmlReportService = new HtmlReportService(_summaryConfig);
        await htmlReportService.GenerateHtmlReportAsync(summary, results, Path.Combine(_config.OutputPath, $"TestReport-{timestamp}.html"));
        Console.WriteLine($"Successfully exported HTML report: TestReport-{timestamp}.html");

        // Skip Excel/CSV generation if HtmlOnly is specified
        if (_config.HtmlOnly)
        {
            Console.WriteLine("HTML-only mode: Skipping Excel and CSV generation.");
            return;
        }

        if (_config.ExcelOutput)
        {
            // Save AllureResults Excel file
            await SaveTestResultsExcelAsync(results, Path.Combine(_config.OutputPath, $"AllureResults-{timestamp}.xlsx"));
            Console.WriteLine($"Successfully exported Excel file: AllureResults-{timestamp}.xlsx");

            // Save StepTimingAnalysis Excel file
            await SaveStepTimingExcelAsync(results, Path.Combine(_config.OutputPath, $"StepTimingAnalysis-{timestamp}.xlsx"));
            Console.WriteLine($"Successfully exported Excel file: StepTimingAnalysis-{timestamp}.xlsx");
            
            // Save Processing Summary Excel file
            await _summaryService.SaveSummaryExcelAsync(summary, results, Path.Combine(_config.OutputPath, $"ProcessingSummary-{timestamp}.xlsx"));
            Console.WriteLine($"Successfully exported Excel file: ProcessingSummary-{timestamp}.xlsx");
        }
        else
        {
            // Only save CSV files
            await SaveCsvAsync(results.TestResults, Path.Combine(_config.OutputPath, $"AllureResults-{timestamp}.csv"));
            await SaveCsvAsync(results.StepTimings.OrderByDescending(s => s.DurationMs), Path.Combine(_config.OutputPath, $"StepTimingAnalysis-{timestamp}.csv"));

            if (results.FailedFiles.Count > 0)
            {
                await SaveCsvAsync(results.FailedFiles, Path.Combine(_config.OutputPath, $"FailedFiles-{timestamp}.csv"));
                await SaveFailuresTextAsync(results.FailedFiles, Path.Combine(_config.OutputPath, $"FailedFiles-{timestamp}.txt"));
            }

            Console.WriteLine($"Successfully exported {results.TestResults.Count} test results (CSV)");
            Console.WriteLine($"Successfully exported {results.StepTimings.Count} step timing records (CSV)");
        }

        // Generate and save summary report
        var summaryReport = _summaryService.GenerateSummaryReport(results, results.ProcessedCount + results.FailedCount);
        await _summaryService.SaveSummaryToFileAsync(summaryReport, Path.Combine(_config.OutputPath, $"SummaryReport-{timestamp}.txt"));
    }

    private async Task SaveTestResultsExcelAsync(ProcessingResults results, string filePath)
    {
        await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();

            var testResultsWs = workbook.Worksheets.Add("Test Results");
            testResultsWs.Cell(1, 1).Value = "Suite Name";
            testResultsWs.Cell(1, 2).Value = "Case Tags";
            testResultsWs.Cell(1, 3).Value = "Test Case Name";
            testResultsWs.Cell(1, 4).Value = "Duration";
            testResultsWs.Cell(1, 5).Value = "Status";
            testResultsWs.Cell(1, 6).Value = "Failing Step";
            testResultsWs.Cell(1, 7).Value = "Failure Reason";
            testResultsWs.Cell(1, 8).Value = "Screenshot Path";

            int row = 2;
            foreach (var result in results.TestResults)
            {
                testResultsWs.Cell(row, 1).Value = result.SuiteName;
                testResultsWs.Cell(row, 2).Value = result.CaseTags;
                testResultsWs.Cell(row, 3).Value = result.TestCaseName;
                testResultsWs.Cell(row, 4).Value = result.Duration;
                testResultsWs.Cell(row, 5).Value = result.Status;
                testResultsWs.Cell(row, 6).Value = result.FailingStep;
                testResultsWs.Cell(row, 7).Value = result.FailureReason;
                testResultsWs.Cell(row, 8).Value = result.ScreenshotPath;
                row++;
            }

            var testResultsRange = testResultsWs.Range(1, 1, row - 1, 8);
            var testResultsTable = testResultsRange.CreateTable();
            testResultsTable.Theme = XLTableTheme.TableStyleMedium2;
            testResultsWs.Columns().AdjustToContents();

            // Failed Files worksheet (if any)
            if (results.FailedFiles.Count > 0)
            {
                var failedFilesWs = workbook.Worksheets.Add("Failed Files");
                failedFilesWs.Cell(1, 1).Value = "File Path";
                failedFilesWs.Cell(1, 2).Value = "Error Reason";
                failedFilesWs.Cell(1, 3).Value = "Timestamp";

                row = 2;
                foreach (var failedFile in results.FailedFiles)
                {
                    failedFilesWs.Cell(row, 1).Value = failedFile.FilePath;
                    failedFilesWs.Cell(row, 2).Value = failedFile.ErrorReason;
                    failedFilesWs.Cell(row, 3).Value = failedFile.Timestamp;
                    row++;
                }

                var failedFilesRange = failedFilesWs.Range(1, 1, row - 1, 3);
                var failedFilesTable = failedFilesRange.CreateTable();
                failedFilesTable.Theme = XLTableTheme.TableStyleMedium2;
                failedFilesWs.Columns().AdjustToContents();
            }

            // Summary worksheet
            var summaryWs = workbook.Worksheets.Add("Summary");
            summaryWs.Cell(1, 1).Value = "Total files found";
            summaryWs.Cell(2, 1).Value = "Successfully processed";
            summaryWs.Cell(3, 1).Value = "Failed to process";
            summaryWs.Cell(4, 1).Value = "Screenshots copied";
            summaryWs.Cell(5, 1).Value = "Passed tests";
            summaryWs.Cell(6, 1).Value = "Failed tests";
            summaryWs.Cell(7, 1).Value = "Broken tests";
            summaryWs.Cell(8, 1).Value = "Total steps analyzed";

            summaryWs.Cell(1, 2).Value = results.ProcessedCount + results.FailedCount;
            summaryWs.Cell(2, 2).Value = results.ProcessedCount;
            summaryWs.Cell(3, 2).Value = results.FailedCount;
            summaryWs.Cell(4, 2).Value = results.ScreenshotsCopied;
            summaryWs.Cell(5, 2).Value = results.TestResults.Count(t => t.Status == "Passed");
            summaryWs.Cell(6, 2).Value = results.TestResults.Count(t => t.Status == "Failed");
            summaryWs.Cell(7, 2).Value = results.TestResults.Count(t => t.Status == "Broken");
            summaryWs.Cell(8, 2).Value = results.StepsProcessed;

            summaryWs.Columns().AdjustToContents();

            workbook.SaveAs(filePath);
        });
    }

    private async Task SaveStepTimingExcelAsync(ProcessingResults results, string filePath)
    {
        await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();

            // Step Timing Analysis worksheet
            var stepTimingWs = workbook.Worksheets.Add("Step Timing Analysis");
            stepTimingWs.Cell(1, 1).Value = "JSON File";
            stepTimingWs.Cell(1, 2).Value = "Suite Name";
            stepTimingWs.Cell(1, 3).Value = "Case Tags";
            stepTimingWs.Cell(1, 4).Value = "Test Case Name";
            stepTimingWs.Cell(1, 5).Value = "Step Type";
            stepTimingWs.Cell(1, 6).Value = "Step Name";
            stepTimingWs.Cell(1, 7).Value = "Duration (ms)";
            stepTimingWs.Cell(1, 8).Value = "Duration";
            stepTimingWs.Cell(1, 9).Value = "Status";
            stepTimingWs.Cell(1, 10).Value = "Step Category";

            int row = 2;
            var sortedStepTimings = results.StepTimings.OrderByDescending(s => s.DurationMs);
            foreach (var timing in sortedStepTimings)
            {
                stepTimingWs.Cell(row, 1).Value = timing.JsonFile;
                stepTimingWs.Cell(row, 2).Value = timing.SuiteName;
                stepTimingWs.Cell(row, 3).Value = timing.CaseTags;
                stepTimingWs.Cell(row, 4).Value = timing.TestCaseName;
                stepTimingWs.Cell(row, 5).Value = timing.StepType;
                stepTimingWs.Cell(row, 6).Value = timing.StepName;
                stepTimingWs.Cell(row, 7).Value = timing.DurationMs;
                stepTimingWs.Cell(row, 8).Value = timing.Duration;
                stepTimingWs.Cell(row, 9).Value = timing.Status;
                stepTimingWs.Cell(row, 10).Value = timing.StepCategory;
                row++;
            }

            var stepTimingRange = stepTimingWs.Range(1, 1, row - 1, 10);
            var stepTimingTable = stepTimingRange.CreateTable();
            stepTimingTable.Theme = XLTableTheme.TableStyleMedium2;
            stepTimingWs.Columns().AdjustToContents();

            var durationColumn = stepTimingWs.Range(2, 7, row - 1, 7);
            durationColumn.AddConditionalFormat().ColorScale()
                .LowestValue(XLColor.LightGreen)
                .HighestValue(XLColor.Red);

            // Failed Files worksheet (if any)
            if (results.FailedFiles.Count > 0)
            {
                var failedFilesWs = workbook.Worksheets.Add("Failed Files");
                failedFilesWs.Cell(1, 1).Value = "File Path";
                failedFilesWs.Cell(1, 2).Value = "Error Reason";
                failedFilesWs.Cell(1, 3).Value = "Timestamp";

                row = 2;
                foreach (var failedFile in results.FailedFiles)
                {
                    failedFilesWs.Cell(row, 1).Value = failedFile.FilePath;
                    failedFilesWs.Cell(row, 2).Value = failedFile.ErrorReason;
                    failedFilesWs.Cell(row, 3).Value = failedFile.Timestamp;
                    row++;
                }

                var failedFilesRange = failedFilesWs.Range(1, 1, row - 1, 3);
                var failedFilesTable = failedFilesRange.CreateTable();
                failedFilesTable.Theme = XLTableTheme.TableStyleMedium2;
                failedFilesWs.Columns().AdjustToContents();
            }

            // Summary worksheet
            var summaryWs = workbook.Worksheets.Add("Summary");
            summaryWs.Cell(1, 1).Value = "Total files found";
            summaryWs.Cell(2, 1).Value = "Successfully processed";
            summaryWs.Cell(3, 1).Value = "Failed to process";
            summaryWs.Cell(4, 1).Value = "Screenshots copied";
            summaryWs.Cell(5, 1).Value = "Passed tests";
            summaryWs.Cell(6, 1).Value = "Failed tests";
            summaryWs.Cell(7, 1).Value = "Broken tests";
            summaryWs.Cell(8, 1).Value = "Total steps analyzed";

            summaryWs.Cell(1, 2).Value = results.ProcessedCount + results.FailedCount;
            summaryWs.Cell(2, 2).Value = results.ProcessedCount;
            summaryWs.Cell(3, 2).Value = results.FailedCount;
            summaryWs.Cell(4, 2).Value = results.ScreenshotsCopied;
            summaryWs.Cell(5, 2).Value = results.TestResults.Count(t => t.Status == "Passed");
            summaryWs.Cell(6, 2).Value = results.TestResults.Count(t => t.Status == "Failed");
            summaryWs.Cell(7, 2).Value = results.TestResults.Count(t => t.Status == "Broken");
            summaryWs.Cell(8, 2).Value = results.StepsProcessed;

            summaryWs.Columns().AdjustToContents();

            workbook.SaveAs(filePath);
        });
    }

    private async Task SaveCsvAsync<T>(IEnumerable<T> data, string filePath)
    {
        var csv = new StringBuilder();
        var properties = typeof(T).GetProperties()
            .Where(p => p.Name != "ParametersKey")
            .ToArray();

        // Write headers
        csv.AppendLine(string.Join(",", properties.Select(p => $"\"{p.Name}\"")));

        // Write data
        foreach (var item in data)
        {
            var values = properties.Select(p =>
            {
                var value = p.GetValue(item)?.ToString() ?? string.Empty;
                return $"\"{value.Replace("\"", "\"\"")}\""; // Escape quotes
            });
            csv.AppendLine(string.Join(",", values));
        }

        await File.WriteAllTextAsync(filePath, csv.ToString(), Encoding.UTF8);
    }

    private async Task SaveFailuresTextAsync(IEnumerable<FailedFile> failedFiles, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("==== Failed Files Report ====");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Total Failed Files: {failedFiles.Count()}");
        sb.AppendLine();

        int idx = 1;
        foreach (var file in failedFiles)
        {
            sb.AppendLine($"#{idx}");
            sb.AppendLine($"File Path   : {file.FilePath}");
            sb.AppendLine($"Error Reason: {file.ErrorReason}");
            sb.AppendLine($"Timestamp   : {file.Timestamp}");
            sb.AppendLine(new string('-', 40));
            idx++;
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }
}