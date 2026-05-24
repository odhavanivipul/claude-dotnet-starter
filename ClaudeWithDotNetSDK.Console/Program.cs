// ════════════════════════════════════════════════════════════════
//  ClaudeWithDotNetSDK — Test Drive
//
//  Author  : Vipul Odhavani
//  Blog    : https://blog.vipulodhavani.tech/
//  GitHub  : https://github.com/odhavanivipul/claude-dotnet-starter
//  Series  : Claude AI in .NET: Real-World Playbook — Post 1 of 7
//
//  ── SET API KEY ──────────────────────────────────────────────
//
//  Visual Studio:
//    Properties → launchSettings.json → replace PASTE_YOUR_KEY_HERE
//
//  Windows terminal:
//    set Anthropic__ApiKey=sk-ant-api03-...
//
//  macOS / Linux:
//    export Anthropic__ApiKey="sk-ant-api03-..."
//
//  Then: dotnet run --project ClaudeWithDotNetSDK.Console
// ════════════════════════════════════════════════════════════════

using ClaudeWithDotNetSDK.AI;
using ClaudeWithDotNetSDK.AI.Exceptions;
using ClaudeWithDotNetSDK.AI.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, svc) => svc.AddClaudeAI())
    .Build();

await host.StartAsync();

var claude = host.Services.GetRequiredService<IClaudeService>();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

PrintBanner();

// ── Test 1: Blocking ask ──────────────────────────────────────
PrintSection("Test 1 — Blocking ask");

try
{
    var answer = await claude.AskAsync(
        "In one sentence: what is dependency injection?",
        systemPrompt: "You are a senior .NET engineer. Be blunt and concise.",
        ct: cts.Token);

    PrintClaude(answer);
}
catch (ClaudeServiceException ex) when (ex.IsNonRetryable)
{
    PrintError(ex.Message);
    Console.WriteLine("  Fix: set Anthropic__ApiKey environment variable.");
    await host.StopAsync();
    return;
}
catch (ClaudeServiceException ex)
{
    PrintError(ex.Message);
}

// ── Test 2: Streaming ─────────────────────────────────────────
PrintSection("Test 2 — Streaming response");

try
{
    Console.Write("  Claude: ");
    await foreach (var token in claude.AskStreamingAsync(
        "List 3 .NET 10 features for backend developers. One per line.",
        ct: cts.Token))
    {
        Console.Write(token);
    }
    Console.WriteLine("\n");
}
catch (ClaudeServiceException ex)
{
    PrintError(ex.Message);
}

// ── Test 3: System prompt ─────────────────────────────────────
PrintSection("Test 3 — Code review");

var code =
    "public string GetName(int id)\n" +
    "{\n" +
    "    var user = _db.Users.FirstOrDefault(u => u.Id == id);\n" +
    "    return user.Name;\n" +
    "}";

Console.ForegroundColor = ConsoleColor.DarkGray;
foreach (var line in code.Split('\n'))
    Console.WriteLine($"    {line}");
Console.ResetColor();
Console.WriteLine();

try
{
    var review = await claude.AskAsync(
        $"Find the bug and show the fix:\n\n{code}",
        systemPrompt: "You are a strict C# reviewer. One bug, one fix.",
        maxTokens: 250,
        ct: cts.Token);

    PrintClaude(review);
}
catch (ClaudeServiceException ex)
{
    PrintError(ex.Message);
}

// ── Test 4: JSON extraction ───────────────────────────────────
PrintSection("Test 4 — JSON extraction");

var text =
    "Riya Shah joined on 15 March 2024. " +
    "She is a Backend Engineer at Surat office. " +
    "Employee ID: EMP-4421.";

Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine($"  Input: {text}");
Console.ResetColor();
Console.WriteLine();

try
{
    var json = await claude.AskAsync(
        "Extract details. Return ONLY valid JSON, no markdown:\n\n" +
        $"{text}\n\nFields: name, role, office, joiningDate, employeeId",
        systemPrompt: "Return only raw JSON. No explanation.",
        maxTokens: 150,
        ct: cts.Token);

    Console.ForegroundColor = ConsoleColor.Cyan;
    foreach (var line in json.Trim().Split('\n'))
        Console.WriteLine($"  {line}");
    Console.ResetColor();
    Console.WriteLine();
}
catch (ClaudeServiceException ex)
{
    PrintError(ex.Message);
}

// ── Test 5: Cancellation ──────────────────────────────────────
PrintSection("Test 5 — Cancellation");

try
{
    using var shortCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
    await claude.AskAsync("Write a very long essay.", ct: shortCts.Token);
}
catch (OperationCanceledException)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  OK — OperationCanceledException propagated correctly.");
    Console.ResetColor();
    Console.WriteLine();
}

// ── Done ──────────────────────────────────────────────────────
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("  All tests completed.");
Console.ResetColor();
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("\n  Blog   : https://blog.vipulodhavani.tech/");
Console.WriteLine("  GitHub : https://github.com/odhavanivipul/claude-dotnet-starter\n");
Console.ResetColor();

await host.StopAsync();

// ── Helpers ───────────────────────────────────────────────────
static void PrintBanner()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine();
    Console.WriteLine("  +=======================================================+");
    Console.WriteLine("  |  ClaudeWithDotNetSDK  |  .NET 10  |  Post 1 of 7     |");
    Console.WriteLine("  |  Author : Vipul Odhavani                              |");
    Console.WriteLine("  +=======================================================+");
    Console.ResetColor();
    Console.WriteLine();
}

static void PrintSection(string title)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  ── {title}");
    Console.ResetColor();
}

static void PrintClaude(string text)
{
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"  Claude: {text.Trim()}");
    Console.ResetColor();
    Console.WriteLine();
}

static void PrintError(string msg)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  ERROR: {msg}");
    Console.ResetColor();
}
