namespace MkvRenameWizard.Models.TvMaze;

public sealed class ShowSearchResult
{
    public ShowSearchResult(double score, Show show)
    {
        Score = score;
        Show =  show;
    }
    
    public double Score { get; }
    public Show Show { get; }
}