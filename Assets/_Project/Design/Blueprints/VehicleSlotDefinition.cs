using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Metadata definition for a vehicle slot, describing its functional category,
    /// friendly name, group classification, and requirement flags (GDD §7.2, §14.3).
    /// </summary>
    public sealed class VehicleSlotDefinition
    {
        public VehicleSlotId Id { get; }
        public string CategoryTag { get; }
        public string DisplayName { get; }
        public string GroupName { get; }
        public bool IsRequired { get; }

        public VehicleSlotDefinition(
            VehicleSlotId id,
            string categoryTag,
            string displayName,
            string groupName,
            bool isRequired)
        {
            Id = id;
            CategoryTag = categoryTag ?? throw new ArgumentNullException(nameof(categoryTag));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            GroupName = groupName ?? "Genel";
            IsRequired = isRequired;
        }

        private static readonly Dictionary<VehicleSlotId, VehicleSlotDefinition> Definitions = new()
        {
            [VehicleSlotId.PayloadBus] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadBus, "bus", "Gövde Yapısı (Bus)", "Uzay Aracı / Gezgin", true),
            [VehicleSlotId.PayloadPower] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadPower, "power", "Güç Sistemi (Power)", "Uzay Aracı / Gezgin", true),
            [VehicleSlotId.PayloadThermal] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadThermal, "thermal", "Isıl Kontrol (Thermal)", "Uzay Aracı / Gezgin", true),
            [VehicleSlotId.PayloadComms] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadComms, "comms", "Haberleşme (Comms)", "Uzay Aracı / Gezgin", true),
            [VehicleSlotId.PayloadAdcs] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadAdcs, "adcs", "Yönelim Kontrol (ADCS)", "Uzay Aracı / Gezgin", true),
            [VehicleSlotId.PayloadEdl] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadEdl, "edl", "İniş Sistemi (EDL)", "Uzay Aracı / Gezgin", false),
            [VehicleSlotId.PayloadLifeSupport] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadLifeSupport, "life-support", "Yaşam Destek", "Uzay Aracı / Gezgin", false),
            [VehicleSlotId.PayloadInstrument1] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadInstrument1, "instrument", "Bilimsel Alet 1", "Bilimsel Yük", false),
            [VehicleSlotId.PayloadInstrument2] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadInstrument2, "instrument", "Bilimsel Alet 2", "Bilimsel Yük", false),

            [VehicleSlotId.Stage1Engine] = new VehicleSlotDefinition(
                VehicleSlotId.Stage1Engine, "engine", "Aşama 1 Motoru", "Fırlatıcı Aşama 1", true),
            [VehicleSlotId.Stage1Tank] = new VehicleSlotDefinition(
                VehicleSlotId.Stage1Tank, "tank", "Aşama 1 Yakıt Tankı", "Fırlatıcı Aşama 1", true),
            [VehicleSlotId.Stage2Engine] = new VehicleSlotDefinition(
                VehicleSlotId.Stage2Engine, "engine", "Aşama 2 Motoru", "Fırlatıcı Aşama 2", false),
            [VehicleSlotId.Stage2Tank] = new VehicleSlotDefinition(
                VehicleSlotId.Stage2Tank, "tank", "Aşama 2 Yakıt Tankı", "Fırlatıcı Aşama 2", false),
            [VehicleSlotId.Stage3Engine] = new VehicleSlotDefinition(
                VehicleSlotId.Stage3Engine, "engine", "Aşama 3 Motoru", "Fırlatıcı Aşama 3", false),
            [VehicleSlotId.Stage3Tank] = new VehicleSlotDefinition(
                VehicleSlotId.Stage3Tank, "tank", "Aşama 3 Yakıt Tankı", "Fırlatıcı Aşama 3", false),
        };

        public static VehicleSlotDefinition Get(VehicleSlotId slotId)
        {
            if (Definitions.TryGetValue(slotId, out var def))
            {
                return def;
            }
            return new VehicleSlotDefinition(slotId, "generic", slotId.ToString(), "Diğer", false);
        }

        public static IReadOnlyCollection<VehicleSlotDefinition> All => Definitions.Values;
    }
}
