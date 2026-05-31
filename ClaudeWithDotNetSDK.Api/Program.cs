using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using ClaudeWithDotNetSDK.AI;
using ClaudeWithDotNetSDK.AI.Extensions;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddClaudeAI();

var app = builder.Build();

// ── GET / ─────────────────────────────────────────────────────
app.MapGet("/", () => new
{
    status = "running",
    version = "1.0",
    endpoints = new[] { "POST /api/ask", "GET /api/ask/stream", "POST /api/review" }
});

// ── POST /api/ask ─────────────────────────────────────────────
app.MapPost("/api/ask", async (
    AskRequest req,
    IClaudeService claude,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Prompt))
        return Results.BadRequest("Prompt cannot be empty.");

    var answer = await claude.AskAsync(req.Prompt, req.System, ct: ct);
    return Results.Ok(new { answer });
});

// ── GET /api/ask/stream ───────────────────────────────────────
app.MapGet("/api/ask/stream", async (
    [FromQuery] string prompt,
    [FromQuery] string? system,
    IClaudeService claude,
    HttpResponse response,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(prompt))
    {
        response.StatusCode = 400;
        await response.WriteAsync("Prompt cannot be empty.", ct);
        return;
    }

    // Headers must be set before first write
    response.Headers.Append("Content-Type", "text/event-stream");
    response.Headers.Append("Cache-Control", "no-cache");
    response.Headers.Append("Connection", "keep-alive");

    // Stream each token as it arrives from Claude
    await foreach (var token in claude.AskStreamingAsync(prompt, system, ct: ct))
    {
        await response.WriteAsync($"data: {token}\n\n", ct);
        await response.Body.FlushAsync(ct); // push immediately — do not remove
    }

    // Signal end of stream
    await response.WriteAsync("data: [DONE]\n\n", ct);
    await response.Body.FlushAsync(ct);
});

// ── POST /api/review  (tool use) ──────────────────────────────
app.MapPost("/api/review", async (
    AskRequest req,
    AnthropicClient client,  // inject directly — tool use needs conversation control
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Prompt))
        return Results.BadRequest("Prompt cannot be empty.");

    try
    {
    // 1. Define the tool Claude can call
    //    Tool implicitly converts to ToolUnion (the SDK union type for Tools param)
    var tools = new List<ToolUnion>
    {
        new Tool
        {
            Name        = "get_order_status",
            Description = "Get the current status of a customer order by its ID.",
            InputSchema = InputSchema.FromRawUnchecked(
                JsonSerializer.Deserialize<IReadOnlyDictionary<string, JsonElement>>(
                    """{"type":"object","properties":{"order_id":{"type":"string"}},"required":["order_id"]}"""
                )!
            )
        }
    };

    var messages = new List<MessageParam>
    {
        new() { Role = Role.User, Content = req.Prompt }
    };

    // 2. First API call — Claude decides whether to use the tool
    var response = await client.Messages.Create(new()
    {
        Model = "claude-sonnet-4-6",
        MaxTokens = 1024,
        Tools = tools,
        Messages = messages
    });

    // 3. If Claude called a tool — execute it and send the result back
    if (response.StopReason?.ToString() == "tool_use")
    {
        var toolUse = response.Content
            .Select(b => b.Value)
            .OfType<ToolUseBlock>()
            .FirstOrDefault();

        if (toolUse?.Name == "get_order_status")
        {
            // Input is IReadOnlyDictionary<string, JsonElement> — index directly
            var orderId = toolUse.Input["order_id"].GetString()!;
            var toolResult = GetOrderStatus(orderId); // swap for real DB call

            // Append the assistant turn — explicit cast required (no implicit conversion)
            messages.Add(new() { Role = Role.Assistant, Content = (MessageParamContent)response.Content });

            // Append the tool result as a user turn
            messages.Add(new()
            {
                Role    = Role.User,
                Content = new List<ContentBlockParam>
                {
                    new ToolResultBlockParam
                    {
                        ToolUseID = toolUse.ID,   // property is ToolUseID (not ToolUseId)
                        Content   = toolResult
                    }
                }
            });

            // 4. Second API call — Claude uses the result to write its final answer
            response = await client.Messages.Create(new()
            {
                Model = "claude-sonnet-4-6",
                MaxTokens = 512,
                Tools = tools,
                Messages = messages
            });
        }
    }

    var text = response.Content
        .Select(b => b.Value)
        .OfType<TextBlock>()
        .FirstOrDefault()?.Text ?? "";

    return Results.Ok(new { answer = text });
    }
    catch (AnthropicApiException ex) when ((int)ex.StatusCode is 401 or 403)
    {
        return Results.Problem("Invalid or missing API key.", statusCode: 401);
    }
    catch (AnthropicApiException ex)
    {
        return Results.Problem($"Claude API error: {ex.Message}", statusCode: 502);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem($"Network error reaching Claude: {ex.Message}", statusCode: 503);
    }
});

app.Run();

// ── Helpers ───────────────────────────────────────────────────
// Replace this with your real EF Core / database call
static string GetOrderStatus(string orderId) => orderId switch
{
    "ORD-1234" => "Shipped — arriving Thursday 29 May",
    "ORD-5678" => "Processing — expected dispatch tomorrow",
    _ => $"Order {orderId} not found."
};

// ── Models ────────────────────────────────────────────────────
record AskRequest(string Prompt, string? System);
