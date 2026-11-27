using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MkvRenameWizard.Core.Diagnostics;

public static class CoreTelemetry
{
    public const string MeterName = "MkvRenameWizard.Core";
    public const string ActivitySourceName = "MkvRenameWizard.Core";

    internal static readonly Meter Meter = new(MeterName, "1.0.0");
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    internal static readonly Histogram<double> TvMazeRequestDurationMs =
        Meter.CreateHistogram<double>("mkv_rename.tvmaze.request.duration", "ms", "Duration of TVMaze API requests.");

    internal static readonly Histogram<double> MkvScanDurationMs =
        Meter.CreateHistogram<double>("mkv_rename.mkv.scan.duration", "ms", "Duration of MKV file discovery and header validation.");

    internal static readonly Counter<long> MkvFilesScanned =
        Meter.CreateCounter<long>("mkv_rename.mkv.files.scanned", "files", "Total valid MKV files discovered.");

    internal static readonly Histogram<double> RenamePlanDurationMs =
        Meter.CreateHistogram<double>("mkv_rename.plan.duration", "ms", "Duration of rename plan generation.");

    internal static readonly Histogram<double> RenameExecuteDurationMs =
        Meter.CreateHistogram<double>("mkv_rename.execute.duration", "ms", "Duration of file rename batch execution.");

    internal static readonly Counter<long> RenameFilesExecuted =
        Meter.CreateCounter<long>("mkv_rename.execute.files", "files", "Total file rename operations attempted.");
}
