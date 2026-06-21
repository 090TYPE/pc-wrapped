namespace PcWrapped.Core.Models;

/// <summary>Только счётчики — никогда не содержимое ввода.</summary>
public sealed record InputCounters(long Keystrokes, long Clicks, double MousePixels)
{
    public static readonly InputCounters Zero = new(0, 0, 0);

    public InputCounters Add(InputCounters other) =>
        new(Keystrokes + other.Keystrokes,
            Clicks + other.Clicks,
            MousePixels + other.MousePixels);
}
