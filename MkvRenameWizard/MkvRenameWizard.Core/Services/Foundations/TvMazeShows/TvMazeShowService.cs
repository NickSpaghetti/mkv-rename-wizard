using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using MkvRenameWizard.Core.Brokers.Loggings;
using MkvRenameWizard.Core.Brokers.TvMaze;
using MkvRenameWizard.Core.Diagnostics;
using MkvRenameWizard.Core.Models.TvMaze;

namespace MkvRenameWizard.Core.Services.Foundations.TvMazeShows;

internal sealed partial class TvMazeShowService(
    ITvMazeApiBroker tvMazeApiBroker,
    ILoggingBroker loggingBroker) : ITvMazeShowService
{
    public Task<IReadOnlyList<Show>> SearchShowsByNameAsync(string showName, CancellationToken cancellationToken = default) =>
        TryCatchAsync(async () =>
        {
            ValidateShowName(showName);

            using var activity = CoreTelemetry.ActivitySource.StartActivity("TvMaze.SearchShows");
            var start = Stopwatch.GetTimestamp();
            var dtoList = await tvMazeApiBroker.SearchShowsAsync(showName.Trim(), cancellationToken);
            CoreTelemetry.TvMazeRequestDurationMs.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds);

            if (dtoList is null || dtoList.Count == 0)
            {
                return Array.Empty<Show>();
            }

            var shows = new List<Show>(dtoList.Count);
            foreach (var resultDto in dtoList)
            {
                var dto = resultDto.ShowDto;
                if (dto is null)
                {
                    continue;
                }

                shows.Add(new Show
                {
                    Id = dto.Id,
                    Score = resultDto.Score,
                    Name = dto.Name,
                    Language = dto.Language,
                    Genres = dto.Genres ?? new List<string>(),
                    Premiered = dto.Premiered,
                    Ended = dto.Ended,
                    YearRange = FormatYearRange(dto.Premiered, dto.Ended),
                    RatingAverage = dto.RatingDto?.Average,
                    AirNetworkLabel = dto.NetworkDto?.Name ?? dto.WebChannel?.Name ?? string.Empty,
                    MediumImageUrl = dto.ImageDto?.Medium ?? string.Empty,
                    OriginalImageUrl = dto.ImageDto?.Original ?? string.Empty,
                    PlainSummary = StripTvMazeHtml(dto.Summary ?? "No Summary found")
                });
            }

            return (IReadOnlyList<Show>)shows;
        });

    internal static string StripTvMazeHtml(string summaryHtml)
    {
        if (string.IsNullOrEmpty(summaryHtml))
        {
            return string.Empty;
        }

        var src = summaryHtml.AsSpan();
        if (src.IndexOf('<') < 0)
        {
            return src.IndexOf('&') < 0 ? summaryHtml : WebUtility.HtmlDecode(summaryHtml);
        }

        char[]? rented = null;
        Span<char> buffer = src.Length <= 512
            ? stackalloc char[src.Length]
            : (rented = ArrayPool<char>.Shared.Rent(src.Length));

        try
        {
            var written = 0;
            var inTag = false;
            var hasEntity = false;

            for (var i = 0; i < src.Length; i++)
            {
                var c = src[i];
                if (c == '<')
                {
                    inTag = true;
                }
                else if (c == '>' && inTag)
                {
                    inTag = false;
                }
                else if (!inTag)
                {
                    if (c == '&')
                    {
                        hasEntity = true;
                    }

                    buffer[written++] = c;
                }
            }

            var stripped = new string(buffer[..written]);
            return hasEntity ? WebUtility.HtmlDecode(stripped) : stripped;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    private static string FormatYearRange(string? premiered, string? ended)
    {
        var startYear = TryExtractYear(premiered);
        var endYear = TryExtractYear(ended);

        return (startYear, endYear) switch
        {
            (null, null) => string.Empty,
            ({ } start, null) => start,
            (null, { } end) => end,
            ({ } start, { } end) when start == end => start,
            ({ } start, { } end) => $"{start}-{end}"
        };
    }

    private static string? TryExtractYear(string? date) =>
        DateOnly.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.Year.ToString(CultureInfo.InvariantCulture)
            : null;
}
