using Anthropic;
using Anthropic.Models.Messages;
using ClaudeWithDotNetSDK.AI.Configuration;
using ClaudeWithDotNetSDK.AI.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Runtime.CompilerServices;

namespace ClaudeWithDotNetSDK.AI;

internal sealed class ClaudeService(
    AnthropicClient            client,
    ResiliencePipeline         pipeline,
    IOptions<AnthropicOptions> options,
    ILogger<ClaudeService>     logger
) : IClaudeService
{
    private readonly AnthropicOptions _opts = options.Value;

    public async Task<string> AskAsync(
        string userMessage,
        string? systemPrompt = null,
        int?  maxTokens    = null,
        CancellationToken ct   = default)
    {
        logger.LogDebug("Ask: {Len} chars, model={Model}", userMessage.Length, _opts.DefaultModel);

        try
        {
            var response = await pipeline.ExecuteAsync(
                async _ => await client.Messages.Create(
                    BuildRequest(userMessage, systemPrompt, maxTokens)),
                ct);

            var text = response.Content
                .Select(b => b.Value)
                .OfType<TextBlock>()
                .FirstOrDefault()?.Text ?? string.Empty;

            logger.LogInformation(
                "Claude OK: in={In} out={Out} stop={Stop}",
                response.Usage.InputTokens,
                response.Usage.OutputTokens,
                response.StopReason);

            return text;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not ClaudeServiceException)
        {
            logger.LogError(ex, "Claude request failed");
            throw new ClaudeServiceException($"Claude error: {ex.Message}", ex);
        }
    }

    public async IAsyncEnumerable<string> AskStreamingAsync(
        string            userMessage,
        string?           systemPrompt = null,
        int?              maxTokens    = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogDebug("Stream: {Len} chars, model={Model}", userMessage.Length, _opts.DefaultModel);

        await foreach (var ev in client.Messages
            .CreateStreaming(BuildRequest(userMessage, systemPrompt, maxTokens))
            .WithCancellation(ct))
        {
            if (ev.TryPickContentBlockDelta(out var delta) &&
                delta.Delta.TryPickText(out var textDelta))
                yield return textDelta.Text;
        }
    }

    private MessageCreateParams BuildRequest(
        string  userMessage,
        string? systemPrompt,
        int?    maxTokens)
    {
        MessageParam[] msgs = [new() { Role = Role.User, Content = userMessage }];
        int            max  = maxTokens ?? _opts.DefaultMaxTokens;

        return systemPrompt is not null
            ? new MessageCreateParams { Model = _opts.DefaultModel, MaxTokens = max, Messages = msgs, System = systemPrompt }
            : new MessageCreateParams { Model = _opts.DefaultModel, MaxTokens = max, Messages = msgs };
    }
}
