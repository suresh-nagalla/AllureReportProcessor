using System.Text.Json.Serialization;

namespace AllureReportProcessor.Models;

// JSON models for Allure test case structure
public class AllureTestCase
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; set; }

    [JsonPropertyName("statusTrace")]
    public string? StatusTrace { get; set; }

    [JsonPropertyName("time")]
    public AllureTime? Time { get; set; }

    [JsonPropertyName("labels")]
    public List<AllureLabel>? Labels { get; set; }

    [JsonPropertyName("beforeStages")]
    public List<AllureStage>? BeforeStages { get; set; }

    [JsonPropertyName("testStage")]
    public AllureStage? TestStage { get; set; }

    [JsonPropertyName("afterStages")]
    public List<AllureStage>? AfterStages { get; set; }

    [JsonPropertyName("parameters")]
    public List<AllureParameter>? Parameters { get; set; }
}

public class AllureParameter
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

public class AllureTime
{
    [JsonPropertyName("duration")]
    public long Duration { get; set; }

    [JsonPropertyName("start")]
    public long Start { get; set; }

    [JsonPropertyName("stop")]
    public long Stop { get; set; }
}

public class AllureLabel
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

public class AllureStage
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; set; }

    [JsonPropertyName("statusTrace")]
    public string? StatusTrace { get; set; }

    [JsonPropertyName("time")]
    public AllureTime? Time { get; set; }

    [JsonPropertyName("steps")]
    public List<AllureStep>? Steps { get; set; }

    [JsonPropertyName("attachments")]
    public List<AllureAttachment>? Attachments { get; set; }
}

public class AllureStep
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; set; }

    [JsonPropertyName("statusTrace")]
    public string? StatusTrace { get; set; }

    [JsonPropertyName("time")]
    public AllureTime? Time { get; set; }

    [JsonPropertyName("attachments")]
    public List<AllureAttachment>? Attachments { get; set; }
}

public class AllureAttachment
{
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("uid")]
    public string? UID { get; set; }
}