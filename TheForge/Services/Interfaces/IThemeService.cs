using TheForge.Features.Workspace.Models;
using TheForge.Models.UI;

namespace TheForge.Services.Interfaces;

public interface IThemeService
{
    ThemePreset ActivePreset { get; }
    string BackgroundColor { get; }
    string AccentColor { get; }
    string TextColor { get; }
    string FontFamily { get; }
    event Action? OnThemeChanged;
    void ApplyPreset(ThemePreset preset);
    void ApplyModeTheme(WorkspaceMode mode);
    Dictionary<string, string> GetCssVariables();
}
