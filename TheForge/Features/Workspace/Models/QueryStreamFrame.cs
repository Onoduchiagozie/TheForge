using TheForge.Models;

namespace TheForge.Features.Workspace.Models;

public enum QueryStreamFrameKind { Text, Stage, Sources, Complete, Error }

public sealed record QueryStreamFrame(
    QueryStreamFrameKind Kind,
    string Text = "",
    QueryStage Stage = QueryStage.Idle,
    List<SourceInfo>? Sources = null);
