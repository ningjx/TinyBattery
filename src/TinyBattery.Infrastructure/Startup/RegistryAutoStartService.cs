using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using TinyBattery.Abstractions.Startup;

namespace TinyBattery.Infrastructure.Startup;

public sealed class RegistryAutoStartService : IAutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TinyBattery";

    private readonly ILogger<RegistryAutoStartService> _logger;

    public RegistryAutoStartService(ILogger<RegistryAutoStartService> logger)
    {
        _logger = logger;
    }

    public bool IsEnabled => string.Equals(
        ReadCommand(),
        BuildCommand(),
        StringComparison.OrdinalIgnoreCase);

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the Windows startup registry key.");

        if (enabled)
        {
            var command = BuildCommand();
            key.SetValue(ValueName, command, RegistryValueKind.String);
            _logger.LogInformation("Enabled auto-start: {Command}", command);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            _logger.LogInformation("Disabled auto-start.");
        }
    }

    public void EnsureEnabled()
    {
        if (!IsEnabled)
        {
            SetEnabled(true);
        }
    }

    private string? ReadCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) as string;
    }

    private static string BuildCommand()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            processPath = Process.GetCurrentProcess().MainModule?.FileName;
        }

        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Could not determine the TinyBattery executable path.");
        }

        if (string.Equals(
                Path.GetFileNameWithoutExtension(processPath),
                "dotnet",
                StringComparison.OrdinalIgnoreCase))
        {
            var assemblyPath = Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrWhiteSpace(assemblyPath))
            {
                return $"\"{processPath}\" \"{assemblyPath}\" --background";
            }
        }

        return $"\"{processPath}\" --background";
    }
}
