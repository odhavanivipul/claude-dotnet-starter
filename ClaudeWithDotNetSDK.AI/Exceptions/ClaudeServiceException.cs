using Anthropic;
using Anthropic.Exceptions;

namespace ClaudeWithDotNetSDK.AI.Exceptions;

public sealed class ClaudeServiceException(
    string message, Exception? inner = null)
    : Exception(message, inner)
{
    public bool IsNonRetryable =>
        InnerException is AnthropicApiException ex &&
        ((int)ex.StatusCode is 400 or 401 or 403);
}
