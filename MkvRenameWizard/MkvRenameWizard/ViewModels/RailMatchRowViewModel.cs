using MkvRenameWizard.Helpers;
using MkvRenameWizard.Models.Mkv;
using MkvRenameWizard.Models.Rail;
using MkvRenameWizard.Models.TvMaze;

namespace MkvRenameWizard.ViewModels;

public class RailMatchRowViewModel : ViewModelBase
{
    public RailMatchRowViewModel(
        int index,
        Episode? episode,
        MkvFile? mkvFile,
        RailSettleType railSettleType = RailSettleType.None,
        int settleDirection = 0)
    {
        Index = index;
        Episode = episode;
        MkvFile = mkvFile;
        SettleType = railSettleType;
        SettleDirection = settleDirection;
        RecomputeCachedFields();
    }

    public int Index { get; private set; }
    public Episode? Episode { get; private set; }
    public MkvFile? MkvFile { get; private set; }
    public RailSettleType SettleType { get; private set; }
    public int SettleDirection { get; private set; }

    public bool ShouldSettleIndependent => SettleType == RailSettleType.Independent;
    public bool ShouldSettleLinked => SettleType == RailSettleType.Linked;
    public bool ShouldAnimateLink => SettleType == RailSettleType.Linked && IsPaired;
    public bool HasEpisode => Episode != null;
    public bool HasMkv => MkvFile != null;
    public bool IsPaired => HasEpisode && HasMkv;
    public bool IsMissingFile => HasEpisode && !HasMkv;
    public bool IsExtraFile => !HasEpisode && HasMkv;
    public int ZeroBasedIndex => Index - 1;

    public string IndexLabel { get; private set; } = string.Empty;
    public string FileSizeLabel { get; private set; } = string.Empty;
    public bool HasFileSizeLabel { get; private set; }
    public string FileSizeRootLabel => MkvFile?.Root ?? string.Empty;
    public string EpisodeCode { get; private set; } = string.Empty;
    public string EpisodeTitle { get; private set; } = string.Empty;
    public string EpisodeDisplayName { get; private set; } = string.Empty;
    public bool HasEpisodeRunTime { get; private set; }
    public string EpisodeRunTimeLabel { get; private set; } = string.Empty;
    public string FileDisplayName { get; private set; } = string.Empty;

    public void Update(int index, Episode? episode, MkvFile? mkvFile, RailSettleType settleType, int settleDirection)
    {
        var indexChanged = Index != index;
        var episodeChanged = !ReferenceEquals(Episode, episode);
        var mkvChanged = !ReferenceEquals(MkvFile, mkvFile);
        var settleChanged = SettleType != settleType || SettleDirection != settleDirection;

        if (!indexChanged && !episodeChanged && !mkvChanged && !settleChanged)
        {
            return;
        }

        Index = index;
        Episode = episode;
        MkvFile = mkvFile;
        SettleType = settleType;
        SettleDirection = settleDirection;

        RecomputeCachedFields();

        if (indexChanged)
        {
            OnPropertyChanged(nameof(Index));
            OnPropertyChanged(nameof(ZeroBasedIndex));
            OnPropertyChanged(nameof(IndexLabel));
        }

        if (episodeChanged)
        {
            OnPropertyChanged(nameof(Episode));
            OnPropertyChanged(nameof(HasEpisode));
            OnPropertyChanged(nameof(EpisodeCode));
            OnPropertyChanged(nameof(EpisodeTitle));
            OnPropertyChanged(nameof(EpisodeDisplayName));
            OnPropertyChanged(nameof(HasEpisodeRunTime));
            OnPropertyChanged(nameof(EpisodeRunTimeLabel));
        }

        if (mkvChanged)
        {
            OnPropertyChanged(nameof(MkvFile));
            OnPropertyChanged(nameof(HasMkv));
            OnPropertyChanged(nameof(FileDisplayName));
            OnPropertyChanged(nameof(FileSizeLabel));
            OnPropertyChanged(nameof(HasFileSizeLabel));
            OnPropertyChanged(nameof(FileSizeRootLabel));
        }

        if (episodeChanged || mkvChanged)
        {
            OnPropertyChanged(nameof(IsPaired));
            OnPropertyChanged(nameof(IsMissingFile));
            OnPropertyChanged(nameof(IsExtraFile));
        }

        if (settleChanged)
        {
            OnPropertyChanged(nameof(SettleType));
            OnPropertyChanged(nameof(SettleDirection));
            OnPropertyChanged(nameof(ShouldSettleIndependent));
            OnPropertyChanged(nameof(ShouldSettleLinked));
            OnPropertyChanged(nameof(ShouldAnimateLink));
        }
    }

    private void RecomputeCachedFields()
    {
        IndexLabel = Index.ToString("00");
        FileSizeLabel = MkvFile?.FormattedSize ?? string.Empty;
        HasFileSizeLabel = !string.IsNullOrEmpty(FileSizeLabel);

        EpisodeCode = Episode?.EpisodeNumber is { } num ? $"S{Episode.Season:D2}E{num:D2}" : string.Empty;
        EpisodeTitle = Episode?.Name ?? string.Empty;
        EpisodeDisplayName = Episode != null
            ? $"{EpisodeCode}  {EpisodeTitle}"
            : "File will not be mapped to an episode";

        HasEpisodeRunTime = Episode is { RunTime: > 0 };
        EpisodeRunTimeLabel = HasEpisodeRunTime && Episode is { RunTime: > 0 }
            ? LabelFormaterHelper.FormatRunTime(Episode.RunTime)
            : string.Empty;

        FileDisplayName = HasMkv
            ? PathDisplay.GetSafeFileName(MkvFile?.FullPath)
            : "File will not be mapped for this episode";
    }
}
