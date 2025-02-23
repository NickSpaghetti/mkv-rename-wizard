using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MkvRenameWizard.Models.TvMaze;

public class Show
{
  public long Id { get; set; }
  public string Name { get; set; }
  public string Language { get; set; }
  public IList<string> Genres { get; set; }
}