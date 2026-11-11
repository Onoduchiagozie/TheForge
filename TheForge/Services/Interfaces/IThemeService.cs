using TheForge.Models.UI;

namespace TheForge.Services.Interfaces;

public interface IThemeService
{
    event Action? OnThemeChanged;
    void ApplyModeTheme(WorkspaceMode mode);
    IReadOnlyDictionary<string, string> GetCssVariables();
}
