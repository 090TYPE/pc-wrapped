namespace PcWrapped.Core.Tracking;

public readonly record struct ForegroundInfo(string ProcessName, string WindowTitle, string? ExecutablePath = null);

public interface IForegroundWindowSource
{
    /// <summary>Текущее активное окно, или null если рабочий стол/неизвестно.</summary>
    ForegroundInfo? GetForeground();

    /// <summary>Секунд с последнего ввода пользователя (для определения простоя).</summary>
    int GetIdleSeconds();
}
