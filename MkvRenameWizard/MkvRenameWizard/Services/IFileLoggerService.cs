namespace MkvRenameWizard.Services;

public interface IFileLoggerService
{
    string LogDirectory { get; }
    void OpenLogDirectory();
}