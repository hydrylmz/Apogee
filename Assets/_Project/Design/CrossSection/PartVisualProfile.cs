using System;

namespace Apogee.Design
{
    /// <summary>
    /// Visual archetype / shape classifications for rocket modules and spacecraft components (GDD §7.2, §15.1).
    /// Used by both 2D vector illustrations and CAD schematic views without taking engine dependencies.
    /// </summary>
    public enum NozzleVisualType
    {
        StandardBell,
        HeavyCluster,
        VacuumExtended,
        Aerospike,
        SolidBell
    }

    public enum SolarArrayVisualType
    {
        None,
        FoldedDualWing,
        ExpandedDualWing,
        CompactBodyPanel
    }

    public enum AntennaVisualType
    {
        None,
        OmniWhip,
        HighGainDish,
        PhasedArray
    }

    public enum EdlVisualType
    {
        None,
        SkycraneRockets,
        AirbagCluster,
        HeatshieldRetro
    }

    public enum TankShapeVisualType
    {
        CylindricalStandard,
        HeavyBulged,
        CompactUpper,
        SolidCylinder
    }

    /// <summary>
    /// Data-driven visual presentation profile mapped to a component or slot.
    /// Determines how the section is drawn across all 4 hangar visualization layers (Exterior, Cutaway, Subsystems, Schematic).
    /// </summary>
    public sealed class PartVisualProfile
    {
        public string ComponentId { get; }
        public string Category { get; }
        public NozzleVisualType NozzleType { get; }
        public SolarArrayVisualType SolarType { get; }
        public AntennaVisualType AntennaType { get; }
        public EdlVisualType EdlType { get; }
        public TankShapeVisualType TankShape { get; }
        public bool IsNuclearRtg { get; }
        public bool IsCrewModule { get; }
        public bool HasEscapeTower { get; }
        public double FillPercentage { get; }

        public PartVisualProfile(
            string componentId,
            string category,
            NozzleVisualType nozzleType = NozzleVisualType.StandardBell,
            SolarArrayVisualType solarType = SolarArrayVisualType.None,
            AntennaVisualType antennaType = AntennaVisualType.None,
            EdlVisualType edlType = EdlVisualType.None,
            TankShapeVisualType tankShape = TankShapeVisualType.CylindricalStandard,
            bool isNuclearRtg = false,
            bool isCrewModule = false,
            bool hasEscapeTower = false,
            double fillPercentage = 1.0)
        {
            ComponentId = componentId ?? string.Empty;
            Category = category ?? string.Empty;
            NozzleType = nozzleType;
            SolarType = solarType;
            AntennaType = antennaType;
            EdlType = edlType;
            TankShape = tankShape;
            IsNuclearRtg = isNuclearRtg;
            IsCrewModule = isCrewModule;
            HasEscapeTower = hasEscapeTower;
            FillPercentage = Math.Clamp(fillPercentage, 0.0, 1.0);
        }

        /// <summary>
        /// Derives a visual profile from a design component and propellant status.
        /// Falls back to generic defaults if the component ID is unrecognized.
        /// </summary>
        public static PartVisualProfile FromComponent(DesignComponent component, double fillPercentage = 1.0)
        {
            if (component == null)
            {
                return new PartVisualProfile("empty", "generic", fillPercentage: 0.0);
            }

            var id = component.Id?.ToLowerInvariant() ?? string.Empty;
            var cat = component.Category?.ToLowerInvariant() ?? string.Empty;

            var nozzle = NozzleVisualType.StandardBell;
            if (id.Contains("heavy") || id.Contains("cluster")) nozzle = NozzleVisualType.HeavyCluster;
            else if (id.Contains("upper") || id.Contains("vacuum") || id.Contains("hydrolox")) nozzle = NozzleVisualType.VacuumExtended;
            else if (id.Contains("solid")) nozzle = NozzleVisualType.SolidBell;
            else if (id.Contains("aerospike")) nozzle = NozzleVisualType.Aerospike;

            var solar = SolarArrayVisualType.None;
            if (id.Contains("solar") || cat == "power")
            {
                solar = id.Contains("advanced") ? SolarArrayVisualType.ExpandedDualWing : SolarArrayVisualType.FoldedDualWing;
            }

            var antenna = AntennaVisualType.None;
            if (id.Contains("dish") || id.Contains("highgain")) antenna = AntennaVisualType.HighGainDish;
            else if (id.Contains("omni") || cat == "comms") antenna = AntennaVisualType.OmniWhip;

            var edl = EdlVisualType.None;
            if (id.Contains("skycrane")) edl = EdlVisualType.SkycraneRockets;
            else if (id.Contains("airbag")) edl = EdlVisualType.AirbagCluster;
            else if (id.Contains("shield") || cat == "edl") edl = EdlVisualType.HeatshieldRetro;

            var tank = TankShapeVisualType.CylindricalStandard;
            if (id.Contains("heavy") || id.Contains("65t")) tank = TankShapeVisualType.HeavyBulged;
            else if (id.Contains("upper") || id.Contains("12t")) tank = TankShapeVisualType.CompactUpper;
            else if (id.Contains("solid")) tank = TankShapeVisualType.SolidCylinder;

            bool isRtg = id.Contains("rtg") || id.Contains("nuclear");
            bool isCrew = id.Contains("cabin") || cat == "cabin";
            bool hasTower = id.Contains("abort") || cat == "abort-system";

            return new PartVisualProfile(
                componentId: component.Id,
                category: component.Category,
                nozzleType: nozzle,
                solarType: solar,
                antennaType: antenna,
                edlType: edl,
                tankShape: tank,
                isNuclearRtg: isRtg,
                isCrewModule: isCrew,
                hasEscapeTower: hasTower,
                fillPercentage: fillPercentage
            );
        }
    }
}
