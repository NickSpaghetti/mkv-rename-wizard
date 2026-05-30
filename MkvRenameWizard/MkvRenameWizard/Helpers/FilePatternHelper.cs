using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MkvRenameWizard.Models.Renaming;
using MkvRenameWizard.Models.TvMaze;

namespace MkvRenameWizard.Helpers;

public static partial class FilePatternHelper
{
    public static readonly IReadOnlyList<PatternToken> ValidTokens = new List<PatternToken>
    {
        new(Constants.TokenNames.Show, "Show name", "Initial D"),
        new(Constants.TokenNames.Season, "Season Number", "1"),
        new(Constants.TokenNames.SeasonPadded, "Season Padded", "S01"),
        new(Constants.TokenNames.Episode, "Episode Number", "9"),
        new(Constants.TokenNames.EpisodePadded, "Episode Padded", "E09"),
        new(Constants.TokenNames.SeasonEpisodePadded, "Season & Episode Padded", "S01E09"),
        new(Constants.TokenNames.Title, "Episode Title", "Act.  9: Battle to the Limit"),
        new(Constants.TokenNames.Year, "Aired Year", "1998"),
        new(Constants.TokenNames.Ext, "Extension", "mkv"),
        new(Constants.TokenNames.RunTime, "Run Time", "30m"),
    };

    public static readonly HashSet<string> ValidTokenNames =
        ValidTokens.Select(token => token.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

    // Matches any {..} span. Example input "{S##E##} {Title}
    // Group 0          (full match) {S##E##}
    // Group TokenGroup (capture)    S##E##
    private const string TokenGroup = nameof(TokenGroup);

    [GeneratedRegex(@"\{(?<" + TokenGroup + @">[^{}]*)\}", RegexOptions.Compiled)]
    private static partial Regex TokenRegex();

    public static IReadOnlyList<PatternError> Validate(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return Array.Empty<PatternError>();
        }

        var errors = new List<PatternError>();
        foreach (Match match in TokenRegex().Matches(pattern))
        {
            var name = match.Groups[TokenGroup].Value;
            if (!ValidTokenNames.Contains(name))
            {
                errors.Add(new PatternError(match.Index + 1, name, FindClosestValidToken(name)));
            }
        }

        return errors;
    }

    public static IReadOnlyList<PatternSegment> Parse(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return Array.Empty<PatternSegment>();
        }

        var segments = new List<PatternSegment>();
        var pointer = 0;

        foreach (Match match in TokenRegex().Matches(pattern))
        {
            if (match.Index > pointer)
            {
                var parsedText = pattern[pointer..match.Index];
                segments.Add(new PatternSegment(parsedText, PatternSegmentType.Literal));
            }

            var name = match.Groups[TokenGroup].Value;
            segments.Add(new PatternSegment(name,
                ValidTokenNames.Contains(name) ? PatternSegmentType.Literal : PatternSegmentType.InvalidToken));

            pointer = match.Index + match.Length;
        }

        if (pointer < pattern.Length)
        {
            segments.Add(new PatternSegment(pattern[pointer..], PatternSegmentType.Literal));
        }

        return segments;
    }

    public static string Apply(string pattern, Episode episode, string showName, string prefix, string fileExtension, string runTime, CaseStyle caseStyle)
    {
        var expanded = TokenRegex().Replace(pattern, match =>
        {
            var name = match.Groups[TokenGroup].Value;
            return name switch
            {
                Constants.TokenNames.Show => SanitizeToken(showName),
                Constants.TokenNames.Season => episode.Season.ToString(),
                Constants.TokenNames.SeasonPadded => $"S{episode.Season:D2}",
                Constants.TokenNames.Episode => episode.EpisodeNumber?.ToString() ?? string.Empty,
                Constants.TokenNames.EpisodePadded => $"E{episode.EpisodeNumber:D2}",
                Constants.TokenNames.SeasonEpisodePadded =>
                    $"S{episode.Season:D2}E{episode.EpisodeNumber?.ToString("D2") ?? string.Empty}",
                Constants.TokenNames.Title => SanitizeToken(episode.Name),
                Constants.TokenNames.Year => episode?.AirDate?.Year.ToString() ?? string.Empty,
                Constants.TokenNames.Ext => fileExtension,
                Constants.TokenNames.RunTime => runTime,
                _ => match.Value
            };
        });

        var transformed = ApplyCaseStyle(expanded, caseStyle);

        return $"{prefix}{transformed}";
    }

    public static readonly SearchValues<char> InvalidFileNameChars =
        SearchValues.Create(
            Path.GetInvalidFileNameChars()
                .Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|'])
                .Distinct()
                .ToArray()
        );

    private const int _statckAllocThreshold = 1 << 8;

    private static string SanitizeToken(ReadOnlySpan<char> token)
    {
        if (token.IsEmpty)
        {
            return string.Empty;
        }

        var firstInvalidIndex = token.IndexOfAny(InvalidFileNameChars);
        if (firstInvalidIndex == -1)
        {
            return string.Empty;
        }

        char[]? borrowedArray = null;
        Span<char> buffer = token.Length <= _statckAllocThreshold
            ? stackalloc char[token.Length]
            : (borrowedArray = ArrayPool<char>.Shared.Rent(token.Length));
        token[..firstInvalidIndex].CopyTo(buffer);

        int count = firstInvalidIndex;

        for (var i = firstInvalidIndex; i < token.Length; i++)
        {
            if (!InvalidFileNameChars.Contains(token[i]))
            {
                buffer[count++] = token[i];
            }
        }

        try
        {
            return new string(buffer[..count]);
        }
        finally
        {
            if (borrowedArray != null)
            {
                ArrayPool<char>.Shared.Return(borrowedArray);
            }
        }
    }

    public static string ApplyCaseStyle(ReadOnlySpan<char> token, CaseStyle caseStyle)
    {
        if (token.IsEmpty) return string.Empty;

        return caseStyle switch
        {
            CaseStyle.SnakeCase => ToSnakeCase(token),
            CaseStyle.PascalCase => ToPascalCase(token),
            CaseStyle.CamelCase => ToCamelCase(token),
            _ or CaseStyle.Default => token.ToString()
        };
    }

    private static string ToSnakeCase(ReadOnlySpan<char> token)
    {
        if (token.IsEmpty)
        {
            return string.Empty;
        }

        char[]? borrowedArray = null;
        Span<char> buffer = token.Length <= _statckAllocThreshold
            ? stackalloc char[token.Length]
            : (borrowedArray = ArrayPool<char>.Shared.Rent(token.Length));

        for (var i = 0; i < token.Length; i++)
        {
            buffer[i] = token[i] == ' ' ? '_' : char.ToLowerInvariant(token[i]);
        }

        try
        {
            return new string(buffer[..token.Length]);
        }
        finally
        {
            if (borrowedArray != null)
            {
                ArrayPool<char>.Shared.Return(borrowedArray);
            }
        }
    }

    private static string ToPascalCase(ReadOnlySpan<char> token)
    {
        if (token.IsEmpty)
        {
            return string.Empty;
        }

        char[]? borrowedArray = null;
        Span<char> buffer = token.Length <= _statckAllocThreshold
            ? stackalloc char[token.Length]
            : (borrowedArray = ArrayPool<char>.Shared.Rent(token.Length));

        var count = 0;
        var capitalizeNext = true;
        foreach (var c in token)
        {
            if (c is ' ' or '_' or '-')
            {
                capitalizeNext = true;
                continue;
            }

            buffer[count++] = capitalizeNext ? char.ToUpperInvariant(c) : c;
            capitalizeNext = false;
        }

        try
        {
            return new string(buffer[..count]);
        }
        finally
        {
            if (borrowedArray != null)
            {
                ArrayPool<char>.Shared.Return(borrowedArray);
            }
        }
    }

    private static string ToCamelCase(ReadOnlySpan<char> token)
    {
        var pascal = ToPascalCase(token);
        if (pascal.Length == 0 || char.IsLower(pascal[0]))
        {
            return pascal;
        }

        Span<char> first = [char.ToLowerInvariant(pascal[0])];
        return string.Concat(first, pascal.AsSpan()[1..]);
    }


    private static string? FindClosestValidToken(string invalidTokenName)
    {
        var threshold = Math.Max(1, invalidTokenName.Length / 2);

        string? bestTokenName = null;
        var bestDistance = int.MaxValue;

        foreach (var token in ValidTokens)
        {
            var distance =
                LevenshteinDistance(invalidTokenName.ToLowerInvariant(), token.Name.ToLowerInvariant());
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTokenName = token.Name;
            }
        }

        return bestDistance <= threshold ? bestTokenName : null;
    }

    private static int LevenshteinDistance(ReadOnlySpan<char> source, ReadOnlySpan<char> target)
    {
        if (source.Length > target.Length)
        {
            ReadOnlySpan<char> temp = source;
            source = target;
            target = temp;
        }

        var m = source.Length;
        var n = target.Length;

        if (m == 0) return n;
        
        const int stackCutoff = _statckAllocThreshold / 2;
        int[]? rentedPrevious = null;
        int[]? rentedCurrent = null;
        
        Span<int> previous = n + 1 <= stackCutoff
            ? stackalloc int[n + 1]
            : (rentedPrevious = ArrayPool<int>.Shared.Rent(n + 1));

        Span<int> current = n + 1 <= stackCutoff
            ? stackalloc int[n + 1]
            : (rentedCurrent = ArrayPool<int>.Shared.Rent(n + 1));

        try
        {
            for (var j = 0; j <= n; j++)
            {
                previous[j] = j;
            }

            for (var i = 1; i <= m; i++)
            {
                current[0] = i;
                for (var j = 1; j <= n; j++)
                {
                    if (source[i - 1] == target[j - 1])
                    {
                        current[j] = previous[j - 1];
                    }
                    else
                    {
                        var minOfTwo = Math.Min(previous[j - 1], previous[j]);
                        current[j] = 1 + Math.Min(minOfTwo, current[j - 1]);
                    }
                }
                
                Span<int> temp = previous;
                previous = current;
                current = temp;
            }

            return previous[n];
        }
        finally
        {
            if (rentedPrevious != null)
            {
                ArrayPool<int>.Shared.Return(rentedPrevious);
            }
            if (rentedCurrent != null)
            {
                ArrayPool<int>.Shared.Return(rentedCurrent);
            }
        }
    }
}