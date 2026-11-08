namespace Apogee.Core.Simulation.Science
{
    /// <summary>
    /// Health/performance status of an operational science event.
    /// GDD §8.3 (Yeşil = rahat, sarı = sınırda, kırmızı = yetersiz/darboğaz).
    /// </summary>
    public enum ScienceEventStatus
    {
        Nominal,
        Warning,
        Bottleneck,
        Critical
    }
}
