using System.ComponentModel.DataAnnotations;

namespace ClaudeWithDotNetSDK.AI.Configuration;

public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    [Required]
    public required string ApiKey { get; set; }

    public string DefaultModel     { get; set; } = "claude-sonnet-4-6";
    public int    DefaultMaxTokens { get; set; } = 1024;
    public int    MaxRetryAttempts { get; set; } = 3;
    public int    BreakerThreshold { get; set; } = 5;

    public TimeSpan BreakerDuration { get; set; } = TimeSpan.FromSeconds(30);
}
