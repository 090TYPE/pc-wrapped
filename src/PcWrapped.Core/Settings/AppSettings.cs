namespace PcWrapped.Core.Settings;

public sealed record AppSettings(bool HasOnboarded, bool CountInput, bool Autostart, string Language = "ru")
{
    public static AppSettings Default => new(false, true, true, "ru");
}
