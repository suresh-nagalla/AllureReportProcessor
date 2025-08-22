using System.Text;
using System.Text.Json;
using AllureReportProcessor.Models;
using AllureReportProcessor.Utils;

namespace AllureReportProcessor.Services;

public class AllureProcessor
{
    private readonly ProcessingConfig _config;
    private readonly string _testCasesPath;
    private readonly string _attachmentsPath;
    private readonly string _imagesOutputPath;
    private readonly SummaryService _summaryService;

    public AllureProcessor(ProcessingConfig config, SummaryConfiguration? summaryConfig = null)
    {
        _config = config;
        _testCasesPath = Path.Combine(config.AllureReportPath, "data", "test-cases");
        _attachmentsPath = Path.Combine(config.AllureReportPath, "data", "attachments");
        _imagesOutputPath = Path.Combine(config.OutputPath, "images");
        _summaryService = new SummaryService(summaryConfig ?? new SummaryConfiguration());
    }

    public async Task<ProcessingResults> ProcessAllureReportAsync()
    {
        ValidatePaths();
        CreateOutputDirectories();

        var results = new ProcessingResults();
        var jsonFiles = Directory.GetFiles(_testCasesPath, "*.json");

        Console.WriteLine($"Found {jsonFiles.Length} JSON files to process...");

        int processed = 0;
        foreach (var jsonFile in jsonFiles)
        {
            try
            {
                var progress = (double)processed / jsonFiles.Length * 100;
                Console.Write($"\rProcessing: {Path.GetFileName(jsonFile)} ({progress:F1}%)");

                await ProcessSingleFileAsync(jsonFile, results);
                results.ProcessedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nFailed to process file: {jsonFile} - Error: {ex.Message}");

                results.FailedFiles.Add(new FailedFile
                {
                    FilePath = jsonFile,
                    ErrorReason = ex.Message,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });

                results.FailedCount++;
            }
            processed++;
        }

        // Deduplicate: keep only the latest occurrence of each test case (by SuiteName + TestCaseName + ParametersKey)
        results.TestResults = results.TestResults
            .GroupBy(tr => (tr.SuiteName, tr.TestCaseName, tr.ParametersKey))
            .Select(g => g.Last())
            .ToList();

        // Normalize status
        foreach (var tr in results.TestResults)
        {
            if (tr.Status.Equals("passed", StringComparison.OrdinalIgnoreCase))
                tr.Status = "Passed";
            else if (tr.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
                tr.Status = "Failed";
            else
                tr.Status = "Broken";
        }

        Console.WriteLine("\nProcessing completed.");
        _summaryService.PrintSummary(results, jsonFiles.Length);

        return results;
    }

    private void ValidatePaths()
    {
        if (!Directory.Exists(_config.AllureReportPath))
            throw new DirectoryNotFoundException($"Allure report path does not exist: {_config.AllureReportPath}");

        if (!Directory.Exists(_testCasesPath))
            throw new DirectoryNotFoundException($"Test cases folder does not exist: {_testCasesPath}");
    }

    private void CreateOutputDirectories()
    {
        Directory.CreateDirectory(_config.OutputPath);
        Directory.CreateDirectory(_imagesOutputPath);
        Console.WriteLine($"Created output directories");
    }

    private async Task ProcessSingleFileAsync(string jsonFilePath, ProcessingResults results)
    {
        var jsonContent = await File.ReadAllTextAsync(jsonFilePath, Encoding.UTF8);
        var testCase = JsonSerializer.Deserialize<AllureTestCase>(jsonContent);

        if (testCase == null) return;

        var jsonFileName = Path.GetFileNameWithoutExtension(jsonFilePath);

        // Extract suite name and tags
        var suiteName = ExtractSuiteName(testCase);
        var tags = ExtractTags(testCase);
        var testCaseName = testCase.Name ?? string.Empty;
        var parametersKey = ExtractParametersKey(testCase);

        // Extract duration and status
        var (durationMs, durationReadable) = ExtractDuration(testCase);
        var status = ExtractStatus(testCase);

        // Extract failure information
        var (failureReason, failingStep) = ExtractFailureInfo(testCase, status);

        // Process step timings
        var stepTimings = ExtractStepTimings(testCase, jsonFileName, suiteName, testCaseName, tags, parametersKey);
        results.StepTimings.AddRange(stepTimings);
        results.StepsProcessed += stepTimings.Count;

        // Process screenshots
        var screenshotPath = await ProcessScreenshotsAsync(testCase, jsonFileName, status);
        if (!string.IsNullOrEmpty(screenshotPath))
        {
            results.ScreenshotsCopied++;
        }

        // Create test result
        results.TestResults.Add(new TestResult
        {
            SuiteName = suiteName,
            CaseTags = tags,
            TestCaseName = testCaseName,
            Duration = durationReadable,
            Status = status,
            FailingStep = failingStep,
            FailureReason = failureReason,
            ScreenshotPath = screenshotPath,
            ParametersKey = parametersKey
        });
    }

    private string ExtractSuiteName(AllureTestCase testCase)
    {
        if (testCase.Labels == null) return string.Empty;

        // First try 'feature' label
        var featureLabel = testCase.Labels.FirstOrDefault(l => l.Name == "feature");
        if (featureLabel != null) return featureLabel.Value ?? string.Empty;

        // Fallback to 'suite' label
        var suiteLabel = testCase.Labels.FirstOrDefault(l => l.Name == "suite");
        return suiteLabel?.Value ?? string.Empty;
    }

    private string ExtractTags(AllureTestCase testCase)
    {
        if (testCase.Labels == null) return string.Empty;

        var tagLabels = testCase.Labels.Where(l => l.Name == "tag").Select(l => l.Value ?? string.Empty);
        return string.Join(", ", tagLabels);
    }

    private string ExtractParametersKey(AllureTestCase testCase)
    {
        if (testCase.Parameters == null || testCase.Parameters.Count == 0)
            return string.Empty;

        // Sort by name for consistent key
        var sorted = testCase.Parameters
            .OrderBy(p => p.Name)
            .Select(p => $"{p.Name}:{p.Value}")
            .ToArray();

        return string.Join("|", sorted);
    }

    private (long durationMs, string durationReadable) ExtractDuration(AllureTestCase testCase)
    {
        if (testCase.Time?.Duration == null) return (0, "0 ms");

        var durationMs = testCase.Time.Duration;
        var durationReadable = TimeUtils.ConvertMillisecondsToReadable(durationMs);

        return (durationMs, durationReadable);
    }

    private string ExtractStatus(AllureTestCase testCase)
    {
        var status = testCase.Status ?? "unknown";
        if (status.Equals("passed", StringComparison.OrdinalIgnoreCase))
            return "Passed";
        if (status.Equals("failed", StringComparison.OrdinalIgnoreCase))
            return "Failed";
        return "Broken";
    }

    private (string failureReason, string failingStep) ExtractFailureInfo(AllureTestCase testCase, string status)
    {
        if (status != "Failed" && status != "Broken") return (string.Empty, string.Empty);

        var failureReason = string.Empty;
        var failingStep = string.Empty;

        // Extract overall failure reason
        if (!string.IsNullOrEmpty(testCase.StatusMessage))
        {
            failureReason = testCase.StatusMessage;
        }
        else if (!string.IsNullOrEmpty(testCase.StatusTrace))
        {
            var traceLines = testCase.StatusTrace.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            failureReason = traceLines[0];
            if (traceLines.Length > 1)
            {
                failureReason += " | " + traceLines[1].Trim();
            }
        }

        // Find failing step
        if (testCase.TestStage?.Steps != null)
        {
            var failedStep = testCase.TestStage.Steps.FirstOrDefault(s =>
                !string.IsNullOrEmpty(s.Status) && !s.Status.Equals("passed", StringComparison.OrdinalIgnoreCase));

            if (failedStep != null)
            {
                failingStep = failedStep.Name ?? string.Empty;

                if (string.IsNullOrEmpty(failureReason))
                {
                    failureReason = failedStep.StatusMessage ?? failedStep.StatusTrace?.Split('\n')[0] ?? string.Empty;
                }
            }
        }

        // Check other stages if no failing step found
        if (string.IsNullOrEmpty(failingStep))
        {
            failingStep = FindFailingStageStep(testCase);
        }

        return (failureReason, string.IsNullOrEmpty(failingStep) ? "Unknown Step" : failingStep);
    }

    private string FindFailingStageStep(AllureTestCase testCase)
    {
        // Check beforeStages
        if (testCase.BeforeStages != null)
        {
            var failedStage = testCase.BeforeStages.FirstOrDefault(s =>
                !string.IsNullOrEmpty(s.Status) && !s.Status.Equals("passed", StringComparison.OrdinalIgnoreCase));
            if (failedStage != null)
                return $"Setup: {failedStage.Name}";
        }

        // Check afterStages
        if (testCase.AfterStages != null)
        {
            var failedStage = testCase.AfterStages.FirstOrDefault(s =>
                !string.IsNullOrEmpty(s.Status) && !s.Status.Equals("passed", StringComparison.OrdinalIgnoreCase));
            if (failedStage != null)
                return $"Cleanup: {failedStage.Name}";
        }

        // Check testStage itself
        if (testCase.TestStage != null &&
            !string.IsNullOrEmpty(testCase.TestStage.Status) &&
            !testCase.TestStage.Status.Equals("passed", StringComparison.OrdinalIgnoreCase))
        {
            return "Test Execution Stage";
        }

        return string.Empty;
    }

    private List<StepTiming> ExtractStepTimings(AllureTestCase testCase, string jsonFileName, string suiteName, string testCaseName, string tags, string parametersKey)
    {
        var stepTimings = new List<StepTiming>();

        // Extract from beforeStages
        if (testCase.BeforeStages != null)
        {
            foreach (var stage in testCase.BeforeStages)
            {
                if (stage.Time?.Duration > 0)
                {
                    stepTimings.Add(new StepTiming
                    {
                        JsonFile = jsonFileName,
                        SuiteName = suiteName,
                        CaseTags = tags,
                        TestCaseName = testCaseName,
                        StepType = "Setup",
                        StepName = stage.Name ?? string.Empty,
                        DurationMs = (int)stage.Time.Duration,
                        Duration = TimeUtils.ConvertMillisecondsToReadable(stage.Time.Duration),
                        Status = stage.Status ?? "unknown",
                        StepCategory = "Before Stage",
                        ParametersKey = parametersKey
                    });
                }
            }
        }

        // Extract from testStage steps
        if (testCase.TestStage?.Steps != null)
        {
            foreach (var step in testCase.TestStage.Steps)
            {
                if (step.Time?.Duration > 0)
                {
                    var stepCategory = CategorizeStep(step.Name ?? string.Empty);

                    stepTimings.Add(new StepTiming
                    {
                        JsonFile = jsonFileName,
                        SuiteName = suiteName,
                        CaseTags = tags,
                        TestCaseName = testCaseName,
                        StepType = "Test Step",
                        StepName = step.Name ?? string.Empty,
                        DurationMs = (int)step.Time.Duration,
                        Duration = TimeUtils.ConvertMillisecondsToReadable(step.Time.Duration),
                        Status = step.Status ?? "unknown",
                        StepCategory = stepCategory,
                        ParametersKey = parametersKey
                    });
                }
            }
        }

        // Extract from afterStages
        if (testCase.AfterStages != null)
        {
            foreach (var stage in testCase.AfterStages)
            {
                if (stage.Time?.Duration > 0)
                {
                    stepTimings.Add(new StepTiming
                    {
                        JsonFile = jsonFileName,
                        SuiteName = suiteName,
                        CaseTags = tags,
                        TestCaseName = testCaseName,
                        StepType = "Cleanup",
                        StepName = stage.Name ?? string.Empty,
                        DurationMs = (int)stage.Time.Duration,
                        Duration = TimeUtils.ConvertMillisecondsToReadable(stage.Time.Duration),
                        Status = stage.Status ?? "unknown",
                        StepCategory = "After Stage",
                        ParametersKey = parametersKey
                    });
                }
            }
        }

        return stepTimings;
    }

    private string CategorizeStep(string stepName)
    {
        var lowerStepName = stepName.ToLower();

        if (lowerStepName.Contains("given")) return "Setup/Given";
        if (lowerStepName.Contains("when")) return "Action/When";
        if (lowerStepName.Contains("then")) return "Validation/Then";
        if (lowerStepName.Contains("and")) return "Additional/And";

        return "Other";
    }

    private async Task<string> ProcessScreenshotsAsync(AllureTestCase testCase, string jsonFileName, string status)
    {
        if (status != "Failed" && status != "Broken") return string.Empty;

        var allAttachments = GetAllAttachments(testCase, jsonFileName);
        var bestScreenshot = FindBestScreenshotAttachment(allAttachments);

        if (bestScreenshot?.Source == null) return string.Empty;

        var sourceScreenshotPath = Path.Combine(_attachmentsPath, bestScreenshot.Source);
        if (!File.Exists(sourceScreenshotPath)) return string.Empty;

        try
        {
            var screenshotFileName = $"{jsonFileName}_{bestScreenshot.Source}";
            var destinationScreenshotPath = Path.Combine(_imagesOutputPath, screenshotFileName);

            await File.ReadAllBytesAsync(sourceScreenshotPath)
                .ContinueWith(async bytes => await File.WriteAllBytesAsync(destinationScreenshotPath, bytes.Result));

            return destinationScreenshotPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nWarning: Failed to copy screenshot for {jsonFileName}: {ex.Message}");
            return string.Empty;
        }
    }

    private List<Attachment> GetAllAttachments(AllureTestCase testCase, string jsonFileName)
    {
        var allAttachments = new List<Attachment>();

        // Check beforeStages
        if (testCase.BeforeStages != null)
        {
            for (int i = 0; i < testCase.BeforeStages.Count; i++)
            {
                var stage = testCase.BeforeStages[i];
                if (stage.Attachments != null)
                {
                    foreach (var attachment in stage.Attachments)
                    {
                        allAttachments.Add(new Attachment
                        {
                            Location = $"beforeStages[{i}]",
                            StageName = stage.Name ?? string.Empty,
                            Source = attachment.Source ?? string.Empty,
                            Type = attachment.Type ?? string.Empty,
                            Name = attachment.Name ?? string.Empty,
                            Size = attachment.Size,
                            UID = attachment.UID ?? string.Empty
                        });
                    }
                }
            }
        }

        // Check testStage
        if (testCase.TestStage != null)
        {
            if (testCase.TestStage.Attachments != null)
            {
                foreach (var attachment in testCase.TestStage.Attachments)
                {
                    allAttachments.Add(new Attachment
                    {
                        Location = "testStage",
                        StageName = "Main Test",
                        Source = attachment.Source ?? string.Empty,
                        Type = attachment.Type ?? string.Empty,
                        Name = attachment.Name ?? string.Empty,
                        Size = attachment.Size,
                        UID = attachment.UID ?? string.Empty
                    });
                }
            }

            // Check testStage steps
            if (testCase.TestStage.Steps != null)
            {
                for (int i = 0; i < testCase.TestStage.Steps.Count; i++)
                {
                    var step = testCase.TestStage.Steps[i];
                    if (step.Attachments != null)
                    {
                        foreach (var attachment in step.Attachments)
                        {
                            allAttachments.Add(new Attachment
                            {
                                Location = $"testStage.steps[{i}]",
                                StageName = step.Name ?? string.Empty,
                                Source = attachment.Source ?? string.Empty,
                                Type = attachment.Type ?? string.Empty,
                                Name = attachment.Name ?? string.Empty,
                                Size = attachment.Size,
                                UID = attachment.UID ?? string.Empty
                            });
                        }
                    }
                }
            }
        }

        // Check afterStages
        if (testCase.AfterStages != null)
        {
            for (int i = 0; i < testCase.AfterStages.Count; i++)
            {
                var stage = testCase.AfterStages[i];
                if (stage.Attachments != null)
                {
                    foreach (var attachment in stage.Attachments)
                    {
                        allAttachments.Add(new Attachment
                        {
                            Location = $"afterStages[{i}]",
                            StageName = stage.Name ?? string.Empty,
                            Source = attachment.Source ?? string.Empty,
                            Type = attachment.Type ?? string.Empty,
                            Name = attachment.Name ?? string.Empty,
                            Size = attachment.Size,
                            UID = attachment.UID ?? string.Empty
                        });
                    }
                }
            }
        }

        return allAttachments;
    }

    private Attachment? FindBestScreenshotAttachment(List<Attachment> allAttachments)
    {
        var screenshots = allAttachments.Where(a => a.Type == "image/png").ToList();
        if (screenshots.Count == 0) return null;

        // Priority order: afterStages screenshots (failure screenshots), then others
        var priorityOrder = new[] { "afterStages", "testStage", "beforeStages" };

        foreach (var priority in priorityOrder)
        {
            var screenshot = screenshots.FirstOrDefault(s => s.Location.StartsWith(priority));
            if (screenshot != null) return screenshot;
        }

        return screenshots.FirstOrDefault();
    }
}