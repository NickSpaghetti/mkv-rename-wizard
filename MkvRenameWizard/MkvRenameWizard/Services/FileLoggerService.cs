using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace MkvRenameWizard.Services;

public class FileLoggerService : IFileLoggerService
{
    private readonly ILogger<FileLoggerService> _logger;

    public string LogDirectory { get; }

   
    public FileLoggerService(string logDirectory, ILogger<FileLoggerService>? logger)
    {
        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            throw new ArgumentException("Log directory path cannot be null or empty.", nameof(logDirectory));
        }

        LogDirectory = logDirectory;
        _logger = logger;
    }

    public void OpenLogDirectory()
    {
        if (!Directory.Exists(LogDirectory))
        {
            _logger.LogError("Logs directory does not exist or is not initialized: {Path}", LogDirectory);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = LogDirectory, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open logs folder: {ExMessage}", ex.Message);
        }
    }
}