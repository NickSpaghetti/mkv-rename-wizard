using System;
using MkvRenameWizard.Helpers;
using MkvRenameWizard.Models.Mkv;
using MkvRenameWizard.Models.Rail;
using MkvRenameWizard.Models.TvMaze;

namespace MkvRenameWizard.ViewModels;

public class RailMatchRowViewModel(
    int index,
    Episode? episode,
    MkvFile? mkvFile,
    RailSettleType railSettleType = RailSettleType.None,
    int settleDirection = 0)
{
    public int Index { get; } = index;
    public Episode? Episode { get; } = episode;
    public MkvFile? MkvFile { get; } = mkvFile;
    public RailSettleType SettleType { get; } = railSettleType;
    public int SettleDirection { get; } = settleDirection;
    public bool ShouldSettleIndependent => SettleType == RailSettleType.Independent;
    public bool ShouldSettleLinked => SettleType == RailSettleType.Linked;
    public bool ShouldAnimateLink => SettleType == RailSettleType.Linked && IsPaired;
    public bool HasEpisode => Episode != null;
    public bool HasMkv => MkvFile != null;
    public bool IsPaired => HasEpisode && HasMkv;
    public bool IsMissingFile => HasEpisode && !HasMkv;
    public bool IsExtraFile => !HasEpisode && HasMkv;
    public int ZeroBasedIndex => Index - 1;
    public string IndexLabel => Index.ToString("00");
    public string FileSizeLabel => MkvFile?.FormattedSize ?? string.Empty;
    public bool HasFileSizeLabel => !string.IsNullOrEmpty(FileSizeLabel);
    public string FileSizeRootLabel => MkvFile?.Root ?? string.Empty;

    public string EpisodeCode  => Episode?.EpisodeNumber is { } num ? $"{Episode.Season:D2}E{num:D2}" : string.Empty;
    public string EpisodeTitle => Episode?.Name ?? string.Empty;

    public string EpisodeDisplayName =>
        Episode != null ? $"{EpisodeCode}  {EpisodeTitle}" : "File will not be mapped to an episode";

    public bool HasEpisodeRunTime => Episode is { RunTime : > 0 };

    public string EpisodeRunTimeLabel
    {
        get
        {
            if (!HasEpisodeRunTime || Episode?.RunTime is not > 0)
            {
                return string.Empty;
            }
            
            var time = TimeSpan.FromMinutes(Episode.RunTime);
            
            return (time.Hours, time.Minutes) switch
            {
                (0, var m) => $"{m} {(m == 1 ? "min" : "mins")}",
                (var h, 0) => $"{h} {(h == 1 ? "hour" : "hours")}",
                var (h, m) => $"{h} {(h == 1 ? "hour" : "hours")} {m} {(m == 1 ? "min" : "mins")}"
            };
        }
    }

    public string FileDisplayName =>
        HasMkv ? PathDisplay.GetSafeFileName(MkvFile?.FullPath) : "File will not be mapped for this episode";
}