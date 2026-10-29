namespace Apogee.Design
{
    /// <summary>
    /// Represents any mutable container or session holding the active vehicle blueprint
    /// (e.g. CampaignState, WorkshopContext, WorkshopViewModel).
    /// Used by design commands to mutate and observe vehicle configuration.
    /// </summary>
    public interface IBlueprintHolder
    {
        Blueprint ActiveBlueprint { get; set; }
    }
}
