using Avalonia.Media;

namespace PcWrapped.Rendering;

public sealed record CardTheme(
    string Id,
    string DisplayName,
    IBrush Background,
    Color TextColor,
    Color AccentColor,
    string FontFamily);
