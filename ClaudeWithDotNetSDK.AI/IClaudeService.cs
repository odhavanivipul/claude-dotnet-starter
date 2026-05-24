namespace ClaudeWithDotNetSDK.AI;

public interface IClaudeService
{
    Task<string> AskAsync(
        string            userMessage,
        string?           systemPrompt = null,
        int?              maxTokens    = null,
        CancellationToken ct           = default);

    IAsyncEnumerable<string> AskStreamingAsync(
        string            userMessage,
        string?           systemPrompt = null,
        int?              maxTokens    = null,
        CancellationToken ct           = default);
}
