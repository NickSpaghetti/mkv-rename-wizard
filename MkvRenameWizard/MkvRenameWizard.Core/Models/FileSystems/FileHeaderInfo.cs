namespace MkvRenameWizard.Core.Models.FileSystems;

internal sealed record FileHeaderInfo(
    byte Byte0,
    byte Byte1,
    byte Byte2,
    byte Byte3,
    int BytesRead,
    long SizeInBytes);
