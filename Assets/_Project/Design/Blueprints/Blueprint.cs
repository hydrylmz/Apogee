using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Represents a complete vehicle design template (rocket stages + spacecraft payload).
    /// Stores slot choices, stages, payload subsystems, and links to target mission/destination.
    /// Complies with GDD §7.1, §7.2, and §19.11.
    /// </summary>
    public sealed class Blueprint
    {
        private readonly List<StageDesign> _stages = new List<StageDesign>();

        public string Id { get; }
        public string Name { get; }
        public int Version { get; }

        public string TargetDestinationId { get; }
        public string TargetMissionTypeId { get; }

        public PayloadDesign Payload { get; }
        public IReadOnlyList<StageDesign> Stages => _stages;
        public DesignComponent FairingAdapter { get; }

        public Blueprint(
            string id,
            string name,
            int version = 1,
            string targetDestinationId = null,
            string targetMissionTypeId = null,
            PayloadDesign payload = null,
            IEnumerable<StageDesign> stages = null,
            DesignComponent fairingAdapter = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Blueprint ID cannot be empty.", nameof(id));

            Id = id;
            Name = !string.IsNullOrWhiteSpace(name) ? name : id;
            Version = version > 0 ? version : 1;

            TargetDestinationId = targetDestinationId;
            TargetMissionTypeId = targetMissionTypeId;

            Payload = payload;
            FairingAdapter = fairingAdapter;

            if (stages != null)
            {
                _stages.AddRange(stages);
            }
        }

        public Blueprint WithPayload(PayloadDesign payload) =>
            new Blueprint(Id, Name, Version + 1, TargetDestinationId, TargetMissionTypeId, payload, _stages, FairingAdapter);

        public Blueprint WithStages(IEnumerable<StageDesign> stages) =>
            new Blueprint(Id, Name, Version + 1, TargetDestinationId, TargetMissionTypeId, Payload, stages, FairingAdapter);

        public Blueprint WithStage(int stageIndex, StageDesign stage)
        {
            var newStages = new List<StageDesign>(_stages);
            while (newStages.Count <= stageIndex)
            {
                int nextIndex = newStages.Count + 1;
                newStages.Add(new StageDesign($"stage-{nextIndex}", $"Aşama {nextIndex}"));
            }
            newStages[stageIndex] = stage;
            return new Blueprint(Id, Name, Version + 1, TargetDestinationId, TargetMissionTypeId, Payload, newStages, FairingAdapter);
        }

        public DesignComponent GetComponentInSlot(VehicleSlotId slotId)
        {
            return slotId switch
            {
                VehicleSlotId.PayloadBus => Payload?.Bus,
                VehicleSlotId.PayloadPower => Payload?.Power,
                VehicleSlotId.PayloadThermal => Payload?.Thermal,
                VehicleSlotId.PayloadComms => Payload?.Comms,
                VehicleSlotId.PayloadAdcs => Payload?.Adcs,
                VehicleSlotId.PayloadEdl => Payload?.Edl,
                VehicleSlotId.PayloadLifeSupport => Payload?.LifeSupport,
                VehicleSlotId.PayloadInstrument1 => Payload?.Instruments != null && Payload.Instruments.Count > 0 ? Payload.Instruments[0] : null,
                VehicleSlotId.PayloadInstrument2 => Payload?.Instruments != null && Payload.Instruments.Count > 1 ? Payload.Instruments[1] : null,
                VehicleSlotId.Stage1Engine => _stages.Count > 0 ? _stages[0].Engine : null,
                VehicleSlotId.Stage1Tank => _stages.Count > 0 ? _stages[0].Tank : null,
                VehicleSlotId.Stage2Engine => _stages.Count > 1 ? _stages[1].Engine : null,
                VehicleSlotId.Stage2Tank => _stages.Count > 1 ? _stages[1].Tank : null,
                VehicleSlotId.Stage3Engine => _stages.Count > 2 ? _stages[2].Engine : null,
                VehicleSlotId.Stage3Tank => _stages.Count > 2 ? _stages[2].Tank : null,
                _ => null
            };
        }

        public Blueprint WithSlotComponent(VehicleSlotId slotId, DesignComponent component)
        {
            var payload = Payload ?? new PayloadDesign("payload-default", "Uzay Aracı Yükü");

            switch (slotId)
            {
                case VehicleSlotId.PayloadBus:
                    return WithPayload(payload.WithBus(component));
                case VehicleSlotId.PayloadPower:
                    return WithPayload(payload.WithPower(component));
                case VehicleSlotId.PayloadThermal:
                    return WithPayload(payload.WithThermal(component));
                case VehicleSlotId.PayloadComms:
                    return WithPayload(payload.WithComms(component));
                case VehicleSlotId.PayloadAdcs:
                    return WithPayload(payload.WithAdcs(component));
                case VehicleSlotId.PayloadEdl:
                    return WithPayload(payload.WithEdl(component));
                case VehicleSlotId.PayloadLifeSupport:
                    return WithPayload(payload.WithLifeSupport(component));
                case VehicleSlotId.PayloadInstrument1:
                    return WithPayload(payload.WithInstrument(0, component));
                case VehicleSlotId.PayloadInstrument2:
                    return WithPayload(payload.WithInstrument(1, component));

                case VehicleSlotId.Stage1Engine:
                {
                    var stage = _stages.Count > 0 ? _stages[0] : new StageDesign("stage-1", "Aşama 1");
                    return WithStage(0, stage.WithEngine(component));
                }
                case VehicleSlotId.Stage1Tank:
                {
                    var stage = _stages.Count > 0 ? _stages[0] : new StageDesign("stage-1", "Aşama 1");
                    return WithStage(0, stage.WithTank(component));
                }
                case VehicleSlotId.Stage2Engine:
                {
                    var stage = _stages.Count > 1 ? _stages[1] : new StageDesign("stage-2", "Aşama 2");
                    return WithStage(1, stage.WithEngine(component));
                }
                case VehicleSlotId.Stage2Tank:
                {
                    var stage = _stages.Count > 1 ? _stages[1] : new StageDesign("stage-2", "Aşama 2");
                    return WithStage(1, stage.WithTank(component));
                }
                case VehicleSlotId.Stage3Engine:
                {
                    var stage = _stages.Count > 2 ? _stages[2] : new StageDesign("stage-3", "Aşama 3");
                    return WithStage(2, stage.WithEngine(component));
                }
                case VehicleSlotId.Stage3Tank:
                {
                    var stage = _stages.Count > 2 ? _stages[2] : new StageDesign("stage-3", "Aşama 3");
                    return WithStage(2, stage.WithTank(component));
                }

                default:
                    return this;
            }
        }

        public override string ToString() => $"[Blueprint '{Name}' (v{Version}) Stages: {_stages.Count}]";
    }
}
