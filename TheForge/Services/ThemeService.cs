using TheForge.Models.UI;
using TheForge.Services.Interfaces;

namespace TheForge.Services;

public sealed class ThemeService : IThemeService
{
    private WorkspaceMode _current = WorkspaceMode.Explorer;
    public event Action? OnThemeChanged;

    public void ApplyModeTheme(WorkspaceMode mode)
    {
        if (_current == mode) return;
        _current = mode;
        OnThemeChanged?.Invoke();
    }

    public IReadOnlyDictionary<string, string> GetCssVariables() =>
        new Dictionary<string, string>
        {
            ["--forge-accent"] = _current == WorkspaceMode.Narrator ? "#b08d57" : "#667d93",
            ["--forge-bg"] = "#10131a",
            ["--forge-text"] = "#e9edf5",
        };
}
