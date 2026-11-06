using TheForge.Features.Workspace.Models;
using TheForge.Models.UI;
using TheForge.Services.Interfaces;

namespace TheForge.Services;

public class ThemeService : IThemeService
{
    public ThemePreset ActivePreset { get; private set; } = ThemePreset.MechanicusRed;
    public string BackgroundColor { get; private set; } = "#0d0d0d";
    public string AccentColor { get; private set; } = "#c8922a";
    public string TextColor { get; private set; } = "#e8dcc8";
    public string FontFamily { get; private set; } = "Georgia, 'Times New Roman', serif";
    public event Action? OnThemeChanged;

    public void ApplyModeTheme(WorkspaceMode mode)
    {
        switch (mode)
        {
            case WorkspaceMode.Explorer:
                BackgroundColor = "#12181f";
                AccentColor = "#4a9eba";
                TextColor = "#c8d8e8";
                FontFamily = "'Courier New', Courier, monospace";
                break;
            default:
                BackgroundColor = "#0d0d0d";
                AccentColor = "#c8922a";
                TextColor = "#e8dcc8";
                FontFamily = "Georgia, 'Times New Roman', serif";
                break;
        }

        OnThemeChanged?.Invoke();
    }

    public void ApplyPreset(ThemePreset preset)
    {
        ActivePreset = preset;
        (BackgroundColor, AccentColor, TextColor) = preset switch
        {
            ThemePreset.ImperialGold => ("#0a0907", "#c8a84b", "#e8e0c8"),
            ThemePreset.UltramarineBlue => ("#060a12", "#1a4a8b", "#c8d4e8"),
            ThemePreset.ChaosVoid => ("#080408", "#6b1a8b", "#d8c8e8"),
            ThemePreset.DarkArchive => ("#080808", "#4a4a4a", "#c8c8c8"),
            ThemePreset.TacticalSlate => ("#0a0f14", "#2a5a6a", "#b8c8d8"),
            _ => ("#0f0a0a", "#8b1a1a", "#e8c8c8")
        };

        OnThemeChanged?.Invoke();
    }

    public Dictionary<string, string> GetCssVariables() => new()
    {
        ["--bg-primary"] = BackgroundColor,
        ["--bg-panel"] = "rgba(255, 255, 255, 0.03)",
        ["--bg-elevated"] = "rgba(255, 255, 255, 0.055)",
        ["--text-primary"] = TextColor,
        ["--text-muted"] = "rgba(232, 220, 200, 0.62)",
        ["--accent"] = AccentColor,
        ["--accent-soft"] = $"{AccentColor}26",
        ["--danger"] = "#ff4444",
        ["--font-body"] = FontFamily,
        ["--font-mono"] = "'Courier New', Courier, monospace",
        ["--space-1"] = "4px",
        ["--space-2"] = "8px",
        ["--space-3"] = "12px",
        ["--space-4"] = "16px",
        ["--space-5"] = "24px"
    };
}
