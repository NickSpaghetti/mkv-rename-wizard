using MkvRenameWizard.Core.Models.FileSystems;

namespace MkvRenameWizard.Core.Brokers.FileSystems;

internal interface IFileSystemBroker
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, SearchOption searchOption);
    FileHeaderInfo ReadFileHeaderAndLength(string filePath);
    void CreateDirectory(string directoryPath);
    void MoveFile(string sourcePath, string destinationPath, bool overwrite);
}
