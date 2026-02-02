using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace NeleDesktop.Services;

public static class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "NeleAI";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        if (!enabled)
        {
            key.DeleteValue(AppName, false);
            return;
        }

        var executablePath = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        if (executablePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var dotnetPath = Environment.ProcessPath ?? "dotnet";
            key.SetValue(AppName, $"\"{dotnetPath}\" \"{executablePath}\"");
            return;
        }

        key.SetValue(AppName, $"\"{executablePath}\"");
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var value = key?.GetValue(AppName) as string;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string ResolveExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return string.Empty;
        }

        if (!processPath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        var entryAssembly = Assembly.GetEntryAssembly()?.Location;
        if (string.IsNullOrWhiteSpace(entryAssembly))
        {
            return processPath;
        }

        var candidateExe = Path.ChangeExtension(entryAssembly, ".exe");
        if (File.Exists(candidateExe))
        {
            return candidateExe;
        }

        return entryAssembly;
    }
}
