namespace Apogee.Core
{
    /// <summary>
    /// Enumerates isolated deterministic RNG streams in the simulation.
    /// Each stream operates independently so advancing one stream (e.g. event generation)
    /// does not perturb the deterministic outcome of other systems (e.g. launch or EDL).
    /// </summary>
    public enum RngStreamType
    {
        General = 0,
        Events = 1,
        Launch = 2,
        Edl = 3,
        Strategy = 4,
        Bot = 5
    }
}
