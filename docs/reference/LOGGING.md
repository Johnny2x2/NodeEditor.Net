# Logging System

NodeEditor.Net includes a structured, channel-based logging system designed for debugging complex graphs with multiple plugins.

## Overview

Log messages are organized into **named channels** (Execution, Plugins, Serialization, etc.) with configurable retention policies. The `OutputTerminalPanel` component displays logs in the UI, and plugins can register their own custom channels.

## Architecture

```
INodeEditorLogger (singleton)
├── Writes log entries to channels
├── Supports multiple log levels
└── Dispatches to UI via events

ILogChannelRegistry (singleton)
├── Registers named log channels
├── Tracks channel ownership (plugin ID)
└── Manages ChannelClearPolicy per channel

LogEntry
├── Message, Level, Channel, Timestamp
└── Optional structured data
```

## Components

### INodeEditorLogger

The central logging interface. Registered as a singleton and available throughout the application.

```csharp
public interface INodeEditorLogger
{
    void Log(LogLevel level, string channel, string message);
    void LogInfo(string channel, string message);
    void LogWarning(string channel, string message);
    void LogError(string channel, string message);
    IReadOnlyList<LogEntry> GetEntries(string? channel = null);
    void Clear(string? channel = null);
    event EventHandler<LogEntry>? EntryAdded;
}
```

### ILogChannelRegistry

Manages named log channels and their configuration:

```csharp
public interface ILogChannelRegistry
{
    void RegisterChannel(string name, string? pluginId = null,
                         ChannelClearPolicy clearPolicy = ChannelClearPolicy.Manual);
    void UnregisterChannel(string name);
    IReadOnlyList<LogChannelRegistration> GetChannels();
}
```

### Log Levels

```csharp
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}
```

### Channel Clear Policies

```csharp
public enum ChannelClearPolicy
{
    Manual,        // Only cleared when explicitly requested
    OnExecution,   // Cleared when a new execution starts
    OnGraphLoad    // Cleared when a new graph is loaded
}
```

## Pre-defined Channels

The `LogChannels` class defines standard channel names used by the core system:

| Channel | Used By |
|---------|---------|
| Execution | `NodeExecutionService` — logs execution events |
| Plugins | `PluginLoader` — logs plugin loading/unloading |
| Serialization | `GraphSerializer` — logs save/load operations |

## Plugin Log Channels

Plugins that implement `ILogChannelAware` can register their own named channels:

```csharp
using NodeEditor.Net.Services.Plugins;
using NodeEditor.Net.Services.Logging;

public sealed class MyPlugin : INodePlugin, ILogChannelAware
{
    public string Name => "My Plugin";
    public string Id => "com.example.myplugin";
    // ... other INodePlugin members ...

    public void RegisterChannels(ILogChannelRegistry registry)
    {
        registry.RegisterChannel("My Plugin Output", pluginId: Id);
    }
}
```

Plugin channels are automatically unregistered when the plugin is unloaded.

## Usage in Code

### Logging from Services

```csharp
public class MyService
{
    private readonly INodeEditorLogger _logger;

    public MyService(INodeEditorLogger logger)
    {
        _logger = logger;
    }

    public void DoWork()
    {
        _logger.LogInfo("Execution", "Processing started");
        try
        {
            // ... work ...
            _logger.LogInfo("Execution", "Processing complete");
        }
        catch (Exception ex)
        {
            _logger.LogError("Execution", $"Processing failed: {ex.Message}");
        }
    }
}
```

### Subscribing to Log Events in Components

```razor
@inject INodeEditorLogger Logger
@implements IDisposable

@code {
    protected override void OnInitialized()
    {
        Logger.EntryAdded += OnLogEntryAdded;
    }

    private void OnLogEntryAdded(object? sender, LogEntry entry)
    {
        // Filter and display as needed
        if (entry.Level >= LogLevel.Warning)
        {
            StateHasChanged();
        }
    }

    public void Dispose()
    {
        Logger.EntryAdded -= OnLogEntryAdded;
    }
}
```

## Output Terminal Panel

The `OutputTerminalPanel` Blazor component provides a built-in UI for viewing log output:
- Displays log entries from all channels
- Supports filtering by channel and log level
- Auto-scrolls to latest entries
- Clear button respects channel clear policies

## Namespaces

| Type | Namespace |
|------|-----------|
| `INodeEditorLogger` | `NodeEditor.Net.Services.Logging` |
| `NodeEditorLogger` | `NodeEditor.Net.Services.Logging` |
| `ILogChannelRegistry` | `NodeEditor.Net.Services.Logging` |
| `LogChannelRegistry` | `NodeEditor.Net.Services.Logging` |
| `LogEntry` | `NodeEditor.Net.Services.Logging` |
| `LogLevel` | `NodeEditor.Net.Services.Logging` |
| `LogChannels` | `NodeEditor.Net.Services.Logging` |
| `ChannelClearPolicy` | `NodeEditor.Net.Services.Logging` |
| `ILogChannelAware` | `NodeEditor.Net.Services.Plugins` |
