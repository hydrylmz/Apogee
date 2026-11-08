using Apogee.Strategy;
using Apogee.Strategy.Campaign;

namespace Apogee.UI.Bootstrap
{
    /// <summary>
    /// Contract for managing the lifecycle of an APOGEE campaign (GDD §4.1, §6.1, M9-02, M9-03).
    /// Implemented by CampaignBootstrapper and mockable for test runners.
    /// </summary>
    public interface ICampaignLifecycleHandler
    {
        CampaignState CurrentCampaign { get; }
        bool IsInitialized { get; }

        CampaignState StartNewCampaign(CampaignSetupArgs args = null);
        CampaignState LoadCampaign(string jsonSaveData);
    }
}
