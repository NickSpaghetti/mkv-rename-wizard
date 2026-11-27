namespace MkvRenameWizard.Core.Brokers.Loggings;

internal interface ILoggingBroker
{
    void LogDebug(string message);
    void LogInformation(string message);
    void LogWarning(string message);
    void LogError(Exception exception);
    void LogCritical(Exception exception);
}
