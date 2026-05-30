using System;
using MkvRenameWizard.Models.TvMaze;

namespace MkvRenameWizard.Helpers;

public static class LabelFormaterHelper
{
    public static string FormatRunTime(long runTime)
    {
        if (runTime <=  0)
        {
            return string.Empty;
        }
        var time = TimeSpan.FromMinutes(runTime);
            
        return FormatRunTime(time);
    }

    public static string FormatRunTime(TimeSpan runTime)
    {
        return (runTime.Hours, runTime.Minutes) switch
        {
            (0, var m) => $"{m} {(m == 1 ? "min" : "mins")}",
            (var h, 0) => $"{h} {(h == 1 ? "hour" : "hours")}",
            var (h, m) => $"{h} {(h == 1 ? "hour" : "hours")} {m} {(m == 1 ? "min" : "mins")}"
        };
    }
}