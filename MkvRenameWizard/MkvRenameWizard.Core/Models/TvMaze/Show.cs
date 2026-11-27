namespace MkvRenameWizard.Core.Models.TvMaze;

public sealed record Show
{
    public long Id { get; init; }
    public double Score { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public IReadOnlyList<string> Genres { get; init; } = Array.Empty<string>();
    public string Premiered { get; init; } = string.Empty;
    public string? Ended { get; init; }
    public string YearRange { get; init; } = string.Empty;
    public double? RatingAverage { get; init; }
    public string AirNetworkLabel { get; init; } = string.Empty;
    public string MediumImageUrl { get; init; } = string.Empty;
    public string OriginalImageUrl { get; init; } = string.Empty;
    public string PlainSummary { get; init; } = string.Empty;
}
