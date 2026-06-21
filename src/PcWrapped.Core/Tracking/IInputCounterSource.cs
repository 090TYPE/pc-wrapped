using PcWrapped.Core.Models;

namespace PcWrapped.Core.Tracking;

public interface IInputCounterSource
{
    /// <summary>Возвращает накопленные счётчики и обнуляет их.</summary>
    InputCounters DrainCounters();
}
