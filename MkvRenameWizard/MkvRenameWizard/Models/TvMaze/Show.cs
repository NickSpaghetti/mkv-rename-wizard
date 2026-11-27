using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace MkvRenameWizard.Models.TvMaze;

public class Show
{
  public long Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string Language { get; set; } = string.Empty;
  public IList<string> Genres { get; set; } 
  public string Premiered  { get; set; } = string.Empty;
  public string? Ended { get; set; }
  public string YearRange { get; set; } = string.Empty;
  public double? RatingAverage { get; set; }
  public string AirNetworkLabel {get; set;} = string.Empty;
  public string MediumImageUrl { get; set; } = string.Empty;
  public string OriginalImageUrl { get; set; } = string.Empty;
  public Bitmap? Thumbnail { get; set; }
  public string PlainSummary { get; set; } = string.Empty;
}