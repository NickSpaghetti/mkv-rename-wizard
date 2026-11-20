using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MkvRenameWizard.Models.Renaming;
using MkvRenameWizard.Models.TvMaze;

namespace MkvRenameWizard.Helpers;

public static class FilePatternHelper
{
    public static readonly IReadOnlyList<PatternToken> ValidTokens = new List<PatternToken>
    {
        new(Constants.TokenNames.Show, "Show name", "Initial D"),
        new(Constants.TokenNames.Season, "Season Number", "1"),
        new(Constants.TokenNames.SeasonPadded, "Season Padded", "S01"),
        new(Constants.TokenNames.Episode, "Episode Number", "9"),
        new(Constants.TokenNames.EpisodePadded, "Episode Padded", "E09"),
        new(Constants.TokenNames.SeasonEpisodePadded, "Season & Episode Padded", "S01E09"),
        new(Constants.TokenNames.Title, "Episode Title", "Act. 9: Battle to the Limit"),
        new(Constants.TokenNames.Year, "Aired Year", "1998"),
        new(Constants.TokenNames.Ext, "Extension", "mkv"),
        new(Constants.TokenNames.RunTime, "Run Time", "30m"),
    };

    public static readonly FrozenSet<string> ValidTokenNames =
        ValidTokens.Select(token => token.Name).ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> ValidTokenLookup =
        ValidTokenNames.GetAlternateLookup<ReadOnlySpan<char>>();

    public static readonly SearchValues<char> InvalidFileNameChars =
        SearchValues.Create(
            Path.GetInvalidFileNameChars()
                .Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|'])
                .Distinct()
                .ToArray()
        );

    private const int StackAllocThreshold = 1 << 8;

    public static IReadOnlyList<PatternError> Validate(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return Array.Empty<PatternError>();
        }

        var span = pattern.AsSpan();
        List<PatternError>? errors = null;
        var pos = 0;

        while (TryFindNextToken(span, pos, out var open, out var close))
        {
            var tokenSpan = span[(open + 1)..close];
            if (!ValidTokenLookup.Contains(tokenSpan))
            {
                errors ??= new List<PatternError>();
                var name = new string(tokenSpan);
                errors.Add(new PatternError(open + 1, name, FindClosestValidToken(tokenSpan)));
            }

            pos = close + 1;
        }

        return errors ?? (IReadOnlyList<PatternError>)Array.Empty<PatternError>();
    }

    public static IReadOnlyList<PatternSegment> Parse(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return Array.Empty<PatternSegment>();
        }

        var span = pattern.AsSpan();
        var segments = new List<PatternSegment>(8);
        var pos = 0;

        while (TryFindNextToken(span, pos, out var open, out var close))
        {
            if (open > pos)
            {
                segments.Add(new PatternSegment(pattern[pos..open], PatternSegmentType.Literal));
            }

            var tokenSpan = span[(open + 1)..close];
            var isValid = ValidTokenLookup.Contains(tokenSpan);
            segments.Add(new PatternSegment(
                new string(tokenSpan),
                isValid ? PatternSegmentType.ValidToken : PatternSegmentType.InvalidToken));

            pos = close + 1;
        }

        if (pos < pattern.Length)
        {
            segments.Add(new PatternSegment(pattern[pos..], PatternSegmentType.Literal));
        }

        return segments;
    }

    public static (IReadOnlyList<PatternSegment> Segments, IReadOnlyList<PatternError> Errors) ParseAndValidate(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return (Array.Empty<PatternSegment>(), Array.Empty<PatternError>());
        }

        var span = pattern.AsSpan();
        var segments = new List<PatternSegment>(8);
        List<PatternError>? errors = null;
        var pos = 0;

        while (TryFindNextToken(span, pos, out var open, out var close))
        {
            if (open > pos)
            {
                segments.Add(new PatternSegment(pattern[pos..open], PatternSegmentType.Literal));
            }

            var tokenSpan = span[(open + 1)..close];
            var name = new string(tokenSpan);
            var isValid = ValidTokenLookup.Contains(tokenSpan);
            segments.Add(new PatternSegment(
                name,
                isValid ? PatternSegmentType.ValidToken : PatternSegmentType.InvalidToken));

            if (!isValid)
            {
                errors ??= new List<PatternError>();
                errors.Add(new PatternError(open + 1, name, FindClosestValidToken(tokenSpan)));
            }

            pos = close + 1;
        }

        if (pos < pattern.Length)
        {
            segments.Add(new PatternSegment(pattern[pos..], PatternSegmentType.Literal));
        }

        return (segments, errors ?? (IReadOnlyList<PatternError>)Array.Empty<PatternError>());
    }

    public static string ApplyTokenValues(
        string pattern,
        Episode episode,
        string showName,
        string prefix,
        string fileExtension,
        string runTime,
        CaseStyle caseStyle)
    {
        return ApplyTokenValuesCore(
            pattern.AsSpan(),
            segments: null,
            episode,
            showName.AsSpan(),
            isShowPreSanitized: false,
            prefix.AsSpan(),
            fileExtension.AsSpan(),
            runTime.AsSpan(),
            caseStyle,
            appendExtension: ReadOnlySpan<char>.Empty);
    }

    public static string ApplyTokenValues(
        IReadOnlyList<PatternSegment> parsedSegments,
        Episode episode,
        ReadOnlySpan<char> sanitizedShowName,
        ReadOnlySpan<char> prefix,
        ReadOnlySpan<char> fileExtension,
        ReadOnlySpan<char> runTime,
        CaseStyle caseStyle,
        ReadOnlySpan<char> appendExtension)
    {
        return ApplyTokenValuesCore(
            ReadOnlySpan<char>.Empty,
            parsedSegments,
            episode,
            sanitizedShowName,
            isShowPreSanitized: true,
            prefix,
            fileExtension,
            runTime,
            caseStyle,
            appendExtension);
    }

    private static string ApplyTokenValuesCore(
        ReadOnlySpan<char> pattern,
        IReadOnlyList<PatternSegment>? segments,
        Episode episode,
        ReadOnlySpan<char> showName,
        bool isShowPreSanitized,
        ReadOnlySpan<char> prefix,
        ReadOnlySpan<char> fileExtension,
        ReadOnlySpan<char> runTime,
        CaseStyle caseStyle,
        ReadOnlySpan<char> appendExtension)
    {
        var estimatedLength = prefix.Length + showName.Length + (episode.Name?.Length ?? 0) +
                              runTime.Length + fileExtension.Length + appendExtension.Length +
                              (segments is not null ? 64 : pattern.Length) + 32;

        char[]? rented = null;
        Span<char> buffer = estimatedLength <= StackAllocThreshold
            ? stackalloc char[StackAllocThreshold]
            : (rented = ArrayPool<char>.Shared.Rent(estimatedLength));

        try
        {
            var pos = 0;

            AppendSpan(ref buffer, ref rented, ref pos, prefix);
            var caseStart = pos;

            if (segments is not null)
            {
                for (var i = 0; i < segments.Count; i++)
                {
                    var seg = segments[i];
                    if (seg.SegmentType == PatternSegmentType.Literal)
                    {
                        AppendSpan(ref buffer, ref rented, ref pos, seg.Text.AsSpan());
                    }
                    else
                    {
                        AppendTokenValue(ref buffer, ref rented, ref pos, seg.Text.AsSpan(),
                            episode, showName, isShowPreSanitized, fileExtension, runTime, fallbackWithBraces: true);
                    }
                }
            }
            else
            {
                var p = 0;
                while (TryFindNextToken(pattern, p, out var open, out var close))
                {
                    if (open > p)
                    {
                        AppendSpan(ref buffer, ref rented, ref pos, pattern[p..open]);
                    }

                    AppendTokenValue(ref buffer, ref rented, ref pos, pattern[(open + 1)..close],
                        episode, showName, isShowPreSanitized, fileExtension, runTime, fallbackWithBraces: true);
                    p = close + 1;
                }

                if (p < pattern.Length)
                {
                    AppendSpan(ref buffer, ref rented, ref pos, pattern[p..]);
                }
            }

            if (caseStyle != CaseStyle.Default && pos > caseStart)
            {
                var transformedLength = ApplyCaseStyleInPlace(buffer[caseStart..pos], caseStyle);
                pos = caseStart + transformedLength;
            }

            if (!appendExtension.IsEmpty)
            {
                AppendSpan(ref buffer, ref rented, ref pos, appendExtension);
            }

            return new string(buffer[..pos]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    private static void AppendTokenValue(
        ref Span<char> buffer,
        ref char[]? rented,
        ref int pos,
        ReadOnlySpan<char> tokenName,
        Episode episode,
        ReadOnlySpan<char> showName,
        bool isShowPreSanitized,
        ReadOnlySpan<char> fileExtension,
        ReadOnlySpan<char> runTime,
        bool fallbackWithBraces)
    {
        if (tokenName.SequenceEqual(Constants.TokenNames.Show))
        {
            if (isShowPreSanitized)
                AppendSpan(ref buffer, ref rented, ref pos, showName);
            else
                AppendSanitized(ref buffer, ref rented, ref pos, showName);
        }
        else if (tokenName.SequenceEqual(Constants.TokenNames.Season))
        {
            AppendInt(ref buffer, ref rented, ref pos, episode.Season, default);
        }
        else if (tokenName.SequenceEqual(Constants.TokenNames.SeasonPadded))
        {
            AppendChar(ref buffer, ref rented, ref pos, 'S');
            AppendInt(ref buffer, ref rented, ref pos, episode.Season, "D2");
        }
        else if (tokenName.SequenceEqual(Constants.TokenNames.Episode))
        {
            if (episode.EpisodeNumber is { } epNum)
                AppendInt(ref buffer, ref rented, ref pos, epNum, default);
        }
        else if (tokenName.SequenceEqual(Constants.TokenNames.EpisodePadded))
        {
            AppendChar(ref buffer, ref rented, ref pos, 'E');
            if (episode.EpisodeNumber is { } epNum)
                AppendInt(ref buffer, ref rented, ref pos, epNum, "D2");
        }
        else if (tokenName.SequenceEqual(Constants.TokenNames.SeasonEpisodePadded))
        {
            AppendChar(ref buffer, ref rented, ref pos, 'S');
            AppendInt(ref buffer, ref rented, ref pos, episode.Season, "D2");
            AppendChar(ref buffer, ref rented, ref pos, 'E');
            if (episode.EpisodeNumber is { } epNum)
                AppendInt(ref buffer, ref rented, ref pos, epNum, "D2");
        }
        else if (tokenName.SequenceEqual(Constants.TokenNames.Title))
        {
            AppendSanitized(ref buffer, ref rented, ref pos, episode.Name.AsSpan());
        }
        else if (tokenName.SequenceEqual(Constants.TokenNames.Year))
        {
            if (episode?.AirDate is { } airDate)
                AppendInt(ref buffer, ref rented, ref pos, airDate.Year, default);
        }
        else if (tokenName.SequenceEqual(Constants.TokenNames.Ext))
        {
            for (var i = 0; i < fileExtension.Length; i++)
            {
                if (fileExtension[i] != '.')
                    AppendChar(ref buffer, ref rented, ref pos, fileExtension[i]);
            }
        }
        else if (tokenName.SequenceEqual(Constants.TokenNames.RunTime))
        {
            AppendSpan(ref buffer, ref rented, ref pos, runTime);
        }
        else if (fallbackWithBraces)
        {
            AppendChar(ref buffer, ref rented, ref pos, '{');
            AppendSpan(ref buffer, ref rented, ref pos, tokenName);
            AppendChar(ref buffer, ref rented, ref pos, '}');
        }
    }

    private static bool TryFindNextToken(ReadOnlySpan<char> span, int startIndex, out int openIndex, out int closeIndex)
    {
        openIndex = -1;
        closeIndex = -1;
        if (startIndex >= span.Length)
        {
            return false;
        }

        var relOpen = span[startIndex..].IndexOf('{');
        if (relOpen < 0)
        {
            return false;
        }

        var open = startIndex + relOpen;
        var relClose = span[(open + 1)..].IndexOf('}');
        if (relClose < 0)
        {
            return false;
        }

        var close = open + 1 + relClose;

        // Match innermost {..} without nested braces (identical to \{[^{}]*\})
        var nestedOpen = span[(open + 1)..close].LastIndexOf('{');
        if (nestedOpen >= 0)
        {
            open = open + 1 + nestedOpen;
        }

        openIndex = open;
        closeIndex = close;
        return true;
    }

    private static void EnsureCapacity(ref Span<char> buffer, ref char[]? rented, int required)
    {
        if (required <= buffer.Length)
        {
            return;
        }

        var newSize = Math.Max(required, buffer.Length * 2);
        var newRented = ArrayPool<char>.Shared.Rent(newSize);
        buffer.CopyTo(newRented);
        if (rented is not null)
        {
            ArrayPool<char>.Shared.Return(rented);
        }
        rented = newRented;
        buffer = newRented;
    }

    private static void AppendChar(ref Span<char> buffer, ref char[]? rented, ref int pos, char c)
    {
        EnsureCapacity(ref buffer, ref rented, pos + 1);
        buffer[pos++] = c;
    }

    private static void AppendSpan(ref Span<char> buffer, ref char[]? rented, ref int pos, ReadOnlySpan<char> src)
    {
        if (src.IsEmpty) return;
        EnsureCapacity(ref buffer, ref rented, pos + src.Length);
        src.CopyTo(buffer[pos..]);
        pos += src.Length;
    }

    private static void AppendInt(ref Span<char> buffer, ref char[]? rented, ref int pos, int value, ReadOnlySpan<char> format)
    {
        EnsureCapacity(ref buffer, ref rented, pos + 16);
        if (value.TryFormat(buffer[pos..], out var written, format, CultureInfo.InvariantCulture))
        {
            pos += written;
        }
    }

    private static void AppendSanitized(ref Span<char> buffer, ref char[]? rented, ref int pos, ReadOnlySpan<char> token)
    {
        if (token.IsEmpty) return;
        var firstInvalid = token.IndexOfAny(InvalidFileNameChars);
        if (firstInvalid < 0)
        {
            AppendSpan(ref buffer, ref rented, ref pos, token);
            return;
        }

        EnsureCapacity(ref buffer, ref rented, pos + token.Length);
        token[..firstInvalid].CopyTo(buffer[pos..]);
        pos += firstInvalid;

        for (var i = firstInvalid; i < token.Length; i++)
        {
            if (!InvalidFileNameChars.Contains(token[i]))
            {
                buffer[pos++] = token[i];
            }
        }
    }

    public static string SanitizeToken(ReadOnlySpan<char> token)
    {
        if (token.IsEmpty)
        {
            return string.Empty;
        }

        var firstInvalidIndex = token.IndexOfAny(InvalidFileNameChars);
        if (firstInvalidIndex == -1)
        {
            return token.ToString();
        }

        char[]? borrowedArray = null;
        Span<char> buffer = token.Length <= StackAllocThreshold
            ? stackalloc char[token.Length]
            : (borrowedArray = ArrayPool<char>.Shared.Rent(token.Length));

        try
        {
            token[..firstInvalidIndex].CopyTo(buffer);
            var count = firstInvalidIndex;
            for (var i = firstInvalidIndex; i < token.Length; i++)
            {
                if (!InvalidFileNameChars.Contains(token[i]))
                {
                    buffer[count++] = token[i];
                }
            }
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

    private static int ApplyCaseStyleInPlace(Span<char> span, CaseStyle caseStyle)
    {
        switch (caseStyle)
        {
            case CaseStyle.SnakeCase:
                for (var i = 0; i < span.Length; i++)
                {
                    span[i] = span[i] == ' ' ? '_' : char.ToLowerInvariant(span[i]);
                }
                return span.Length;

            case CaseStyle.PascalCase:
            case CaseStyle.CamelCase:
                var count = 0;
                var capitalizeNext = true;
                for (var i = 0; i < span.Length; i++)
                {
                    var c = span[i];
                    if (c is ' ' or '_' or '-')
                    {
                        capitalizeNext = true;
                        continue;
                    }
                    span[count++] = capitalizeNext ? char.ToUpperInvariant(c) : c;
                    capitalizeNext = false;
                }
                if (caseStyle == CaseStyle.CamelCase && count > 0)
                {
                    span[0] = char.ToLowerInvariant(span[0]);
                }
                return count;

            default:
                return span.Length;
        }
    }

    public static string ApplyCaseStyle(ReadOnlySpan<char> token, CaseStyle caseStyle)
    {
        if (token.IsEmpty) return string.Empty;
        if (caseStyle == CaseStyle.Default) return token.ToString();

        char[]? borrowedArray = null;
        Span<char> buffer = token.Length <= StackAllocThreshold
            ? stackalloc char[token.Length]
            : (borrowedArray = ArrayPool<char>.Shared.Rent(token.Length));

        try
        {
            token.CopyTo(buffer);
            var count = ApplyCaseStyleInPlace(buffer[..token.Length], caseStyle);
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

    private static string? FindClosestValidToken(ReadOnlySpan<char> invalidTokenName)
    {
        var threshold = Math.Max(1, invalidTokenName.Length / 2);

        string? bestTokenName = null;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < ValidTokens.Count; i++)
        {
            var token = ValidTokens[i];
            var distance = LevenshteinDistanceIgnoreCase(invalidTokenName, token.Name.AsSpan());
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTokenName = token.Name;
            }
        }

        return bestDistance <= threshold ? bestTokenName : null;
    }

    private static int LevenshteinDistanceIgnoreCase(ReadOnlySpan<char> source, ReadOnlySpan<char> target)
    {
        if (source.Length > target.Length)
        {
            var temp = source;
            source = target;
            target = temp;
        }

        var m = source.Length;
        var n = target.Length;

        if (m == 0) return n;

        const int stackCutoff = StackAllocThreshold / 2;
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
                var sc = char.ToLowerInvariant(source[i - 1]);
                for (var j = 1; j <= n; j++)
                {
                    if (sc == char.ToLowerInvariant(target[j - 1]))
                    {
                        current[j] = previous[j - 1];
                    }
                    else
                    {
                        var minOfTwo = Math.Min(previous[j - 1], previous[j]);
                        current[j] = 1 + Math.Min(minOfTwo, current[j - 1]);
                    }
                }

                var temp = previous;
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
