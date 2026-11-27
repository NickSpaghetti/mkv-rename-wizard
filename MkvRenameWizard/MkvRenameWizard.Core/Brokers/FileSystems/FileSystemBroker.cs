using MkvRenameWizard.Core.Models.FileSystems;

namespace MkvRenameWizard.Core.Brokers.FileSystems;

internal sealed class FileSystemBroker : IFileSystemBroker
{
    public bool DirectoryExists(string path) =>
        Directory.Exists(path);

    public bool FileExists(string path) =>
        File.Exists(path);

    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateFiles(directoryPath, searchPattern, searchOption);

    public FileHeaderInfo ReadFileHeaderAndLength(string filePath)
    {
        using var handle = File.OpenHandle(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Span<byte> header = stackalloc byte[4];
        var bytesRead = RandomAccess.Read(handle, header, 0);
        var length = RandomAccess.GetLength(handle);

        return new FileHeaderInfo(
            header[0],
            header[1],
            header[2],
            header[3],
            bytesRead,
            length);
    }

    public void CreateDirectory(string directoryPath) =>
        Directory.CreateDirectory(directoryPath);

    public void MoveFile(string sourcePath, string destinationPath, bool overwrite) =>
        File.Move(sourcePath, destinationPath, overwrite);
}
