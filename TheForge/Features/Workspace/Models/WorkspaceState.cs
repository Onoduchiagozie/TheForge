using TheForge.Models;

namespace TheForge.Features.Workspace.Models;

/// <summary>State owned by one workspace page, never shared between users or page instances.</summary>
public sealed class WorkspaceState
{
    public string SessionId { get; } = $"forge-{Guid.NewGuid():N}";
    public int ConversationTurn { get; private set; }
    public WorkspaceMode Mode { get; set; } = WorkspaceMode.Explorer;
    public NarratorSubMode SubMode { get; set; } = NarratorSubMode.Focus;
    public string QueryText { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public StreamState Status { get; set; } = StreamState.Idle;
    public QueryStage Stage { get; set; } = QueryStage.Idle;
    public string ResponseText { get; set; } = "";
    public List<SourceInfo> Sources { get; set; } = [];
    public List<string> ExplorerSections { get; private set; } = [];
    public HealthPayload? Health { get; set; }
    public SourceListPayload? SourceList { get; set; }
    public List<BookInfo> Books { get; set; } = [];
    public string? SelectedBookFilename { get; private set; }
    public List<string> SelectedSources { get; private set; } = [];
    public string? BookClearSignal { get; private set; }
    public bool ShowBookFilterHint { get; set; } = true;
    public string? ActiveChipText { get; private set; }
    private readonly PromptChip[] _narratorChips = WorkspacePrompts.PickNarrator();
    private readonly PromptChip[] _explorerChips = WorkspacePrompts.PickExplorer();

    public bool IsBusy => Status == StreamState.Streaming;
    public bool IsWideNet => Mode == WorkspaceMode.Narrator && SubMode == NarratorSubMode.WideNet;
    public bool SubmitDisabled => IsBusy || string.IsNullOrWhiteSpace(QueryText);
    public bool IsHealthOnline => Health is not null && !string.Equals(Health.Status, "error", StringComparison.OrdinalIgnoreCase);
    public string HealthStatusLabel => Health?.Status?.ToUpperInvariant() switch
    {
        null => "OFFLINE", "OK" or "HEALTHY" => "ONLINE", "ERROR" => "OFFLINE", var other => other
    };
    public string HealthModelText => !string.IsNullOrWhiteSpace(Health?.LlmModel)
        ? Health.LlmModel : Health?.OllamaModel ?? "";
    private string? ChunkCount => Health?.MetadataLoaded is > 0 ? Health.MetadataLoaded.ToString("N0") : null;
    public string ChunkText => ChunkCount is null ? "CHUNKS —" : $"{ChunkCount} CHUNKS";
    private int? BookCount => Books.Count > 0 ? Books.Count : SourceList is null ? null : SourceList.Total > 0 ? SourceList.Total : SourceList.Sources.Count;
    public string BookCountText => BookCount is > 0 ? $"{BookCount:N0} BOOKS" : "BOOKS —";
    public string BookFilterPlaceholder => ChunkCount is null ? "Optional source title" : $"Narrows the {ChunkCount}-chunk pool to one book";
    public bool HasAnyFilter => SelectedBookFilename is not null || SelectedSources.Count > 0;
    public string? SelectedBookTitle => SelectedBookFilename is null ? null : Books.FirstOrDefault(b => b.Filename == SelectedBookFilename)?.Title ?? SelectedBookFilename;
    public PromptChip[] ActiveChips => Mode == WorkspaceMode.Explorer ? _explorerChips : _narratorChips;
    public string ModeLabelText => Mode == WorkspaceMode.Explorer ? "EXPLORER" : SubMode == NarratorSubMode.Focus ? "NARRATOR · FOCUS" : "NARRATOR · WIDE NET";
    public string ModeCaptionText => Mode == WorkspaceMode.Explorer ? "Explorer analyses your question using retrieved passages." : SubMode == NarratorSubMode.Focus
        ? "Focus narrows to the top sources — fast, accuracy-first, no streaming."
        : "Wide Net pools across the full corpus — broader, slower, streamed live.";
    public string SubmitLabel => Mode == WorkspaceMode.Explorer ? IsBusy ? "ANALYSING…" : "ANALYSE" : IsBusy ? "CHRONICLING…" : "CHRONICLE";
    public string IdleText => Mode == WorkspaceMode.Explorer ? "Input a designation for analysis..." : "The archive awaits your query...";
    public bool IsEmptyResult => Status == StreamState.Complete && string.IsNullOrWhiteSpace(ResponseText) && (Mode != WorkspaceMode.Explorer || Sources.Count == 0);
    public string ResponsePanelClass => Mode == WorkspaceMode.Explorer ? "response-panel response-panel--explorer" : "response-panel response-panel--narrator";
    public string QueryPlaceholder => Mode == WorkspaceMode.Explorer ? "Try: \"What Corrupted the emperors children\" or \"Why Did Horus Turn traitor\"" : "Horus Virus Bombs the Loyalists";

    public void SelectBook(string? filename)
    {
        SelectedBookFilename = filename;
        if (filename is not null) SelectedSources = [];
    }

    public void ClearBookFilter()
    {
        SelectedBookFilename = null;
        BookClearSignal = Guid.NewGuid().ToString();
    }

    public void SelectSources(IEnumerable<string> sources)
    {
        SelectedSources = sources.ToList();
        if (SelectedSources.Count > 0) ClearBookFilter();
    }

    public void SelectChip(PromptChip chip)
    {
        if (IsBusy) return;
        QueryText = chip.Text;
        ActiveChipText = chip.Text;
        if (Mode != WorkspaceMode.Narrator) return;
        if (string.IsNullOrWhiteSpace(chip.BookHint))
        {
            ClearBookFilter();
            return;
        }
        var match = Books.FirstOrDefault(b => b.Title.Contains(chip.BookHint, StringComparison.OrdinalIgnoreCase));
        if (match is not null) SelectBook(match.Filename);
    }

    public QueryRequest BuildRequest() => new()
    {
        Query = QueryText.Trim(),
        SessionId = SessionId,
        ConversationTurn = ConversationTurn,
        BookFilter = Mode == WorkspaceMode.Narrator ? SelectedBookFilename : null,
        SourceFilter = Mode == WorkspaceMode.Narrator && SelectedSources.Count > 0 ? SelectedSources.ToList() : null
    };

    public void ApplyResponse(QueryResponse result)
    {
        ResponseText = result.Response;
        Sources = result.Sources;
        ExplorerSections = Mode == WorkspaceMode.Explorer
            ? ResponseText.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList()
            : [];
        Status = StreamState.Complete;
        CompleteTurn();
    }

    public void CompleteTurn()
    {
        checked { ConversationTurn++; }
    }

    public void ResetResponse()
    {
        ResponseText = "";
        Sources = [];
        ExplorerSections = [];
        ErrorMessage = null;
        Status = StreamState.Idle;
        Stage = QueryStage.Idle;
    }
}
