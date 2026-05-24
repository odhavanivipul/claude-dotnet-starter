# Claude C# SDK Integration in .NET — Without Losing Your Mind

A .NET 10 starter project demonstrating how to integrate the **Anthropic Claude API** into a C# application using the official [Anthropic .NET SDK](https://github.com/anthropics/anthropic-sdk-dotnet).

> **Part 1 of 7** — *Claude AI in .NET: Real-World Playbook* by [Vipul Odhavani](https://blog.vipulodhavani.tech/)

---

## What This Project Covers

- Wrapping the Anthropic SDK behind a clean `IClaudeService` abstraction
- Single blocking requests and real-time token streaming
- Polly v8 retry pipeline with exponential back-off and jitter
- Startup validation — missing API key crashes early with a clear message
- `CancellationToken` propagation (streaming and blocking)
- Dependency injection with `Microsoft.Extensions.Hosting`

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An [Anthropic API key](https://console.anthropic.com/)

---

## Project Structure

```
ClaudeWithDotNetSDK.sln
├── ClaudeWithDotNetSDK.AI/          # Reusable library (IClaudeService abstraction)
│   ├── IClaudeService.cs
│   ├── ClaudeService.cs
│   ├── Configuration/AnthropicOptions.cs
│   ├── Exceptions/ClaudeServiceException.cs
│   └── Extensions/ServiceCollectionExtensions.cs
└── ClaudeWithDotNetSDK.Console/     # Console demo app
    ├── Program.cs
    └── appsettings.json
```

---

## Getting Started

### 1. Clone the repo

```bash
git clone https://github.com/odhavanivipul/claude-dotnet-starter.git
cd claude-dotnet-starter
```

### 2. Set your API key

**Windows (Command Prompt)**
```cmd
set Anthropic__ApiKey=sk-ant-api03-...
```

**Windows (PowerShell)**
```powershell
$env:Anthropic__ApiKey = "sk-ant-api03-..."
```

**macOS / Linux**
```bash
export Anthropic__ApiKey="sk-ant-api03-..."
```

Or paste it directly into `ClaudeWithDotNetSDK.Console/Properties/launchSettings.json` (not committed to source control).

### 3. Build and run

```bash
dotnet build ClaudeWithDotNetSDK.sln
dotnet run --project ClaudeWithDotNetSDK.Console
```

---

## Demo Scenarios

The console app runs five sequential tests:

| # | Scenario | What it shows |
|---|----------|---------------|
| 1 | **Blocking ask** | `AskAsync` with a system prompt |
| 2 | **Streaming** | `AskStreamingAsync` printing tokens as they arrive |
| 3 | **Code review** | System prompt shaping Claude's reviewer persona |
| 4 | **JSON extraction** | Structured output from unstructured text |
| 5 | **Cancellation** | `OperationCanceledException` propagated correctly |

---

## Configuration

All settings live under the `"Anthropic"` key in `appsettings.json` or as `Anthropic__*` environment variables:

| Key | Default | Notes |
|-----|---------|-------|
| `ApiKey` | *(required)* | Validated at startup |
| `DefaultModel` | `claude-sonnet-4-6` | Any Claude model identifier |
| `DefaultMaxTokens` | `1024` | Per-request override also available |
| `MaxRetryAttempts` | `3` | Polly retry count for 5xx errors |
| `BreakerThreshold` | `5` | Reserved for future circuit-breaker |
| `BreakerDuration` | `00:00:30` | Reserved for future circuit-breaker |

---

## Using the Library in Your Own Project

Register the service in your DI container:

```csharp
builder.Services.AddClaudeAI();
```

Or with programmatic overrides:

```csharp
builder.Services.AddClaudeAI(opts =>
{
    opts.DefaultModel     = "claude-opus-4-5";
    opts.DefaultMaxTokens = 2048;
});
```

Inject and call:

```csharp
public class MyService(IClaudeService claude)
{
    public async Task<string> SummarizeAsync(string text, CancellationToken ct)
        => await claude.AskAsync(text, systemPrompt: "Summarize in 3 bullet points.", ct: ct);
}
```

Stream tokens:

```csharp
await foreach (var token in claude.AskStreamingAsync("Tell me a story.", ct: ct))
    Console.Write(token);
```

---

## NuGet Packages

### ClaudeWithDotNetSDK.AI

| Package | Version | Purpose |
|---------|---------|---------|
| [`Anthropic`](https://www.nuget.org/packages/Anthropic) | 12.23.0 | Official Anthropic .NET SDK |
| [`Polly.Core`](https://www.nuget.org/packages/Polly.Core) | 8.6.6 | Resiliency pipeline (retry + jitter) |
| [`Microsoft.Extensions.DependencyInjection.Abstractions`](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions) | 10.0.8 | DI interfaces (`IServiceCollection`) |
| [`Microsoft.Extensions.Logging.Abstractions`](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions) | 10.0.8 | `ILogger<T>` support |
| [`Microsoft.Extensions.Options.ConfigurationExtensions`](https://www.nuget.org/packages/Microsoft.Extensions.Options.ConfigurationExtensions) | 10.0.8 | `BindConfiguration()` for `AnthropicOptions` |
| [`Microsoft.Extensions.Options.DataAnnotations`](https://www.nuget.org/packages/Microsoft.Extensions.Options.DataAnnotations) | 10.0.8 | `ValidateDataAnnotations()` at startup |

### ClaudeWithDotNetSDK.Console

| Package | Version | Purpose |
|---------|---------|---------|
| [`Microsoft.Extensions.Hosting`](https://www.nuget.org/packages/Microsoft.Extensions.Hosting) | 10.0.8 | Host, DI, configuration, logging |

---

## Author

**Vipul Odhavani**
- Blog: [blog.vipulodhavani.tech](https://blog.vipulodhavani.tech/)
- GitHub: [github.com/odhavanivipul](https://github.com/odhavanivipul)
