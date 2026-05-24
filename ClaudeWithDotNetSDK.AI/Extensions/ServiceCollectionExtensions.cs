using Anthropic;
using Anthropic.Exceptions;
using ClaudeWithDotNetSDK.AI.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace ClaudeWithDotNetSDK.AI.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClaudeAI(
        this IServiceCollection   services,
        Action<AnthropicOptions>? configure = null)
    {
        // Bind + validate — crash at startup if ApiKey is missing
        var ob = services
            .AddOptions<AnthropicOptions>()
            .BindConfiguration(AnthropicOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
            ob.Configure(configure);

        // AnthropicClient as Singleton — must not be created per-request
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value;
            return new AnthropicClient { ApiKey = opts.ApiKey };
        });

        // Polly v8 retry pipeline — exponential back-off with jitter
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value;

            return new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = opts.MaxRetryAttempts,
                    BackoffType      = DelayBackoffType.Exponential,
                    Delay            = TimeSpan.FromSeconds(2),
                    UseJitter        = true,
                    ShouldHandle     = new PredicateBuilder()
                        .Handle<AnthropicApiException>(e => (int)e.StatusCode >= 500)
                        .Handle<HttpRequestException>()
                })
                .Build();
        });

        services.AddSingleton<IClaudeService, ClaudeService>();

        return services;
    }
}
