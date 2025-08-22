using System.Text.Json;
using AllureReportProcessor.Models;

namespace AllureReportProcessor.Services;

/// <summary>
/// Service for loading and managing application configuration
/// </summary>
public class ConfigurationService
{
    private const string DefaultConfigFileName = "appsettings.json";
    private const string SummaryConfigurationSection = "SummaryConfiguration";

    /// <summary>
    /// Loads summary configuration from appsettings.json
    /// </summary>
    /// <param name="configFilePath">Optional path to config file. Defaults to appsettings.json</param>
    /// <returns>Summary configuration with default values if file not found</returns>
    public static SummaryConfiguration LoadSummaryConfiguration(string? configFilePath = null)
    {
        string filePath;
        
        if (configFilePath != null)
        {
            filePath = configFilePath;
        }
        else
        {
            // Try to find appsettings.json in multiple locations
            var possiblePaths = new[]
            {
                DefaultConfigFileName, // Current working directory
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultConfigFileName), // Application directory
                Path.Combine(Directory.GetCurrentDirectory(), DefaultConfigFileName), // Current directory
                Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", DefaultConfigFileName) // Assembly location
            };
            
            filePath = possiblePaths.FirstOrDefault(File.Exists) ?? DefaultConfigFileName;
        }
        
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Configuration file '{DefaultConfigFileName}' not found in any of the following locations:");
            Console.WriteLine($"  • Current working directory: {Directory.GetCurrentDirectory()}");
            Console.WriteLine($"  • Application directory: {AppDomain.CurrentDomain.BaseDirectory}");
            Console.WriteLine($"  • Assembly location: {Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)}");
            Console.WriteLine("Using default configuration values.");
            Console.WriteLine();
            Console.WriteLine("💡 Tip: Run with -createconfig to generate a sample appsettings.json file.");
            return new SummaryConfiguration();
        }

        try
        {
            var jsonContent = File.ReadAllText(filePath);
            var jsonDocument = JsonDocument.Parse(jsonContent);
            
            if (jsonDocument.RootElement.TryGetProperty(SummaryConfigurationSection, out var configSection))
            {
                var config = JsonSerializer.Deserialize<SummaryConfiguration>(configSection.GetRawText(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                Console.WriteLine($"✅ Configuration loaded successfully from '{filePath}'");
                PrintConfigurationSummary(config ?? new SummaryConfiguration());
                
                return config ?? new SummaryConfiguration();
            }
            else
            {
                Console.WriteLine($"⚠️  '{SummaryConfigurationSection}' section not found in '{filePath}'. Using default values.");
                return new SummaryConfiguration();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading configuration from '{filePath}': {ex.Message}. Using default values.");
            return new SummaryConfiguration();
        }
    }

    /// <summary>
    /// Creates a sample appsettings.json file with default configuration
    /// </summary>
    /// <param name="filePath">Path where to create the file</param>
    public static async Task CreateSampleConfigurationAsync(string filePath = DefaultConfigFileName)
    {
        var sampleConfig = new
        {
            SummaryConfiguration = new SummaryConfiguration()
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var jsonContent = JsonSerializer.Serialize(sampleConfig, jsonOptions);
        await File.WriteAllTextAsync(filePath, jsonContent);
        Console.WriteLine($"✅ Sample configuration file created: {filePath}");
    }

    /// <summary>
    /// Validates the configuration and reports any issues
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <returns>True if configuration is valid</returns>
    public static bool ValidateConfiguration(SummaryConfiguration config)
    {
        var isValid = true;
        var issues = new List<string>();

        if (config.TopSlowStepsCount <= 0)
        {
            issues.Add($"TopSlowStepsCount must be greater than 0. Current value: {config.TopSlowStepsCount}");
            isValid = false;
        }

        if (config.TopTestsPerSuiteCount <= 0)
        {
            issues.Add($"TopTestsPerSuiteCount must be greater than 0. Current value: {config.TopTestsPerSuiteCount}");
            isValid = false;
        }

        if (config.StepNameTruncationLength <= 0)
        {
            issues.Add($"StepNameTruncationLength must be greater than 0. Current value: {config.StepNameTruncationLength}");
            isValid = false;
        }

        if (config.PerformanceThresholds.CriticalThresholdMs <= config.PerformanceThresholds.HighThresholdMs)
        {
            issues.Add("CriticalThresholdMs must be greater than HighThresholdMs");
            isValid = false;
        }

        if (config.PerformanceThresholds.HighThresholdMs <= config.PerformanceThresholds.MediumThresholdMs)
        {
            issues.Add("HighThresholdMs must be greater than MediumThresholdMs");
            isValid = false;
        }

        if (!isValid)
        {
            Console.WriteLine("❌ Configuration validation failed:");
            foreach (var issue in issues)
            {
                Console.WriteLine($"   • {issue}");
            }
        }

        return isValid;
    }

    private static void PrintConfigurationSummary(SummaryConfiguration config)
    {
        Console.WriteLine("📋 Configuration Summary:");
        Console.WriteLine($"   • Top Slow Steps: {config.TopSlowStepsCount}");
        Console.WriteLine($"   • Top Tests per Suite: {config.TopTestsPerSuiteCount}");
        Console.WriteLine($"   • Step Name Truncation: {config.StepNameTruncationLength} chars");
        Console.WriteLine($"   • Performance Thresholds: Critical={config.PerformanceThresholds.CriticalThresholdMs}ms, High={config.PerformanceThresholds.HighThresholdMs}ms, Medium={config.PerformanceThresholds.MediumThresholdMs}ms");
        Console.WriteLine($"   • Auto-size Excel Columns: {config.ExcelSettings.AutoSizeColumns}");
    }
}