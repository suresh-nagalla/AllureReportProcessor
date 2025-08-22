using AllureReportProcessor.Models;
using AllureReportProcessor.Services;

namespace AllureReportProcessor;

public class Program
{
    public static async Task Main(string[] args)
    {
        var config = ParseArguments(args);
        if (config == null) return;

        try
        {
            Console.WriteLine("Starting Allure Report Processing...");
            
            // Load summary configuration from appsettings.json
            var summaryConfig = ConfigurationService.LoadSummaryConfiguration();
            
            // Validate configuration
            if (!ConfigurationService.ValidateConfiguration(summaryConfig))
            {
                Console.WriteLine("⚠️  Using default values for invalid configuration settings.");
                summaryConfig = new SummaryConfiguration(); // Reset to defaults
            }

            var processor = new AllureProcessor(config, summaryConfig);
            var results = await processor.ProcessAllureReportAsync();

            Console.WriteLine("Generating output files...");
            var fileManager = new FileManager(config, summaryConfig);
            await fileManager.SaveResultsAsync(results);

            Console.WriteLine($"\nScript completed successfully!");
            Console.WriteLine($"Output directory: {config.OutputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private static ProcessingConfig? ParseArguments(string[] args)
    {
        var config = new ProcessingConfig();

        // Parse command-line arguments
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "-allurereportpath":
                    if (i + 1 < args.Length) config.AllureReportPath = args[++i];
                    break;
                case "-outputpath":
                    if (i + 1 < args.Length) config.OutputPath = args[++i];
                    break;
                case "-exceloutput":
                    // Always use Excel output
                    config.ExcelOutput = true;
                    break;
                case "-htmlonly":
                    // Generate only HTML report (skip Excel/CSV)
                    config.HtmlOnly = true;
                    break;
                case "-createconfig":
                    // New parameter to create sample configuration
                    ConfigurationService.CreateSampleConfigurationAsync().Wait();
                    Console.WriteLine("Sample configuration created. You can now customize appsettings.json and run the application again.");
                    return null;
            }
        }

        // Prompt for missing required arguments
        if (string.IsNullOrEmpty(config.AllureReportPath))
        {
            Console.Write("Enter path to Allure report directory: ");
            config.AllureReportPath = Console.ReadLine() ?? string.Empty;
        }
        if (string.IsNullOrEmpty(config.OutputPath))
        {
            Console.Write("Enter output directory path: ");
            config.OutputPath = Console.ReadLine() ?? string.Empty;
        }

        // Force Excel output selection (option 1) without prompting
        config.ExcelOutput = true;

        // Final validation
        if (string.IsNullOrEmpty(config.AllureReportPath) || string.IsNullOrEmpty(config.OutputPath))
        {
            Console.WriteLine("Error: Both AllureReportPath and OutputPath are required.");
            ShowUsage();
            return null;
        }

        return config;
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage: AllureReportProcessor.exe -allurereportpath <path> -outputpath <path> [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  -allurereportpath <path>    Path to Allure report directory (required)");
        Console.WriteLine("  -outputpath <path>          Output directory path (required)");
        Console.WriteLine("  -exceloutput               Generate Excel output in addition to CSV (forced by default)");
        Console.WriteLine("  -htmlonly                  Generate only HTML report (skip Excel/CSV)");
        Console.WriteLine("  -createconfig              Create a sample appsettings.json configuration file");
        Console.WriteLine();
        Console.WriteLine("Output Formats:");
        Console.WriteLine("  • HTML Report (always generated) - Interactive web report with screenshots");
        Console.WriteLine("  • Excel Reports (enabled automatically) - Detailed spreadsheets for analysis");
        Console.WriteLine("  • CSV Files (default when -htmlonly not used)");
        Console.WriteLine("  • Text Summary (always generated) - Console-friendly summary");
    }
}