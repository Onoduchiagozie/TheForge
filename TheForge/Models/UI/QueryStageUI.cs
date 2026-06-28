namespace TheForge.Models.UI;

public enum QueryStage
{
    Idle,
    Embedding,
    Retrieving,
    Reranking,
    Generating,
    Complete,
    Error
}

public static class QueryStageExtensions
{
    public static QueryStage Parse(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "embedding" => QueryStage.Embedding,
        "retrieving" => QueryStage.Retrieving,
        "reranking" => QueryStage.Reranking,
        "generating" => QueryStage.Generating,
        "complete" => QueryStage.Complete,
        _ => QueryStage.Error
    };

    public static int ProgressPercent(this QueryStage stage) => stage switch
    {
        QueryStage.Idle => 0,
        QueryStage.Embedding => 15,
        QueryStage.Retrieving => 40,
        QueryStage.Reranking => 65,
        QueryStage.Generating => 85,
        QueryStage.Complete => 100,
        QueryStage.Error => 0,
        _ => 0
    };

    // States what just finished, not just the new stage name — this is the
    // "done" framing you liked: each label confirms the prior step's result
    // before naming what comes next.
    public static string Label(this QueryStage stage) => stage switch
    {
        QueryStage.Idle => "Awaiting query",
        QueryStage.Embedding => "Encoding query",
        QueryStage.Retrieving => "Query encoded — searching the archive",
        QueryStage.Reranking => "Candidates retrieved — ranking by relevance",
        QueryStage.Generating => "Ranking complete — composing response",
        QueryStage.Complete => "Response complete",
        QueryStage.Error => "Pipeline error",
        _ => ""
    };
}