using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;
using Avalonia.Media.Imaging;
using Avalonia.Remote.Protocol.Designer;

namespace MkvRenameWizard.Models.TvMaze;

public class Show
{
  public long Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string Language { get; set; } = string.Empty;
  public IList<string> Genres { get; set; } 
  public string Premiered  { get; set; } = string.Empty;
  public string? Ended { get; set; }
  public double? RatingAverage { get; set; }
  public string AirNetworkLabel {get; set;} = string.Empty;
  public string MediumImageUrl { get; set; } = string.Empty;
  public string OriginalImageUrl { get; set; } = string.Empty;
  public Bitmap? Thumbnail { get; set; }
  public string PlainSummary { get; set; } = string.Empty;

  public string YearRange
  {
    get
    {
      var startYear = GetYear(Premiered);
      var endYear = GetYear(Ended);

      return (startYear, endYear) switch
      {
        (null, null) => string.Empty,
        ({ } start, null) => $"{start}",
        (null, { } end) => end,
        ({ } start, { } end) when start == end => start,
        ({ } start, { } end) => $"{start}-{end}",
      };
    }
  }

  private static string? GetYear(string? date)
  {
    return DateOnly.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
      ? parsedDate.Year.ToString(CultureInfo.InvariantCulture)
      : null;
  }
}