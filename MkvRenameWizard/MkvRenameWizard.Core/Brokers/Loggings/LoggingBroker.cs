using Microsoft.Extensions.Logging;

namespace MkvRenameWizard.Core.Brokers.Loggings;

internal sealed class LoggingBroker(ILogger<LoggingBroker> logger) : ILoggingBroker
{
    public void LogDebug(string message) =>
        logger.LogDebug("{Message}", message);

    public void LogInformation(string message) =>
        logger.LogInformation("{Message}", message);

    public void LogWarning(string message) =>
        logger.LogWarning("{Message}", message);

    public void LogError(Exception exception) =>
        logger.LogError(exception, "A MkvRenameWizard.Core operation failed.");

    public void LogCritical(Exception exception) =>
        logger.LogCritical(exception, "A critical MkvRenameWizard.Core failure occurred.");
}
