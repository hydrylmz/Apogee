using Apogee.Core.Simulation.Report;

namespace Apogee.UI.Screens.Report
{
    /// <summary>
    /// Arguments passed when navigating to the Mission Report screen (ScreenId.MissionReport).
    /// GDD §10.5, §14.1 #16.
    /// </summary>
    public sealed class MissionReportNavigationArgs
    {
        public string MissionId { get; }
        public MissionReport Report { get; }

        public MissionReportNavigationArgs(string missionId, MissionReport report = null)
        {
            MissionId = missionId ?? string.Empty;
            Report = report;
        }
    }
}
