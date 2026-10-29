using Apogee.Design;

namespace Apogee.Data
{
    /// <summary>
    /// Helper extension methods to map catalog data models (<see cref="ModuleRecord"/>)
    /// to runtime design domain components (<see cref="DesignComponent"/>).
    /// </summary>
    public static class ModuleRecordExtensions
    {
        public static DesignComponent ToDesignComponent(
            this ModuleRecord record,
            SupplierSpec supplier = null,
            MaterialSpec material = null)
        {
            if (record == null) return null;

            return new DesignComponent(
                record.Id,
                record.Category,
                record.NameKey ?? record.Id,
                dryMass: record.DryMassTons,
                cost: record.CostMUSD,
                buildTime: record.BuildTimeDays,
                reliability: record.BaseReliability > 0 ? record.BaseReliability : 1.0,
                powerGeneration: record.PowerGenerationKw,
                powerConsumption: record.PowerConsumptionKw,
                thermalLoad: record.ThermalLoadKw,
                thermalDissipation: record.ThermalDissipationKw,
                dataRate: record.DataRateGbPerDay,
                dataStorage: record.DataStorageGb,
                downlinkRate: record.DownlinkRateGbPerDay,
                isp: record.Isp,
                thrust: record.ThrustVacKn,
                supplier: supplier,
                material: material,
                requiredResearchId: record.ResearchRequiredId);
        }
    }
}
