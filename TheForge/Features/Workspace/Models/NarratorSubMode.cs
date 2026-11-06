namespace TheForge.Features.Workspace.Models;

// Nested under WorkspaceMode.Narrator only — Explorer/Narrator stay the
// two top-level buttons. This exists purely so Narrator can toggle between
// a narrow, capped, sync answer (Focus) and a broad, uncapped, streamed
// one (WideNet) without touching WorkspaceMode.cs at all.
public enum NarratorSubMode
{
    Focus,
    WideNet
}