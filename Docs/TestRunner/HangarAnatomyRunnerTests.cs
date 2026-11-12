using System;
using System.Linq;
using Apogee.Design;
using Apogee.UI.Screens.Workshop;

namespace ApogeeLocalTestRunner
{
    public static class HangarAnatomyRunnerTests
    {
        private static void AssertEqual<T>(T expected, T actual, string message = null)
        {
            if (!Equals(expected, actual))
            {
                throw new Exception($"AssertEqual failed. Expected: {expected}, Actual: {actual}. {message}");
            }
        }

        private static void AssertTrue(bool condition, string message = null)
        {
            if (!condition)
            {
                throw new Exception($"AssertTrue failed. {message}");
            }
        }

        public static void RunAll()
        {
            Test_Anatomy_FullRocket_BuildsAllSectionsWithProportions();
            Test_Anatomy_DynamicScaling_PropellantAndThrust();
            Test_Anatomy_CrewedMission_IncludesCabinLifeSupportAndEscapeTower();
            Test_Anatomy_FindSectionBySlot_MapsAllSlotsAccurately();
            Test_Workshop_AllSeventeenSlots_InitializedAndSelectable();
            Test_Workshop_ResearchLockedOption_HasLockFlagAndReason();
        }

        public static void Test_Anatomy_FullRocket_BuildsAllSectionsWithProportions()
        {
            var s3Engine = new DesignComponent("eng-s3", "engine", "S3 Engine", 0.8, 2.0, 30, 0.98, isp: 350, thrust: 60);
            var s3Tank = new DesignComponent("tank-s3", "tank", "S3 Tank", 1.2, 1.5, 20, 0.99);
            var stage3 = new StageDesign("s3", "Stage 3", s3Engine, s3Tank, propellantMass: 15.0);

            var s2Engine = new DesignComponent("eng-s2", "engine", "S2 Engine", 1.5, 3.5, 45, 0.97, isp: 330, thrust: 220);
            var s2Tank = new DesignComponent("tank-s2", "tank", "S2 Tank", 3.0, 2.5, 35, 0.99);
            var stage2 = new StageDesign("s2", "Stage 2", s2Engine, s2Tank, propellantMass: 40.0);

            var s1Engine = new DesignComponent("eng-s1", "engine", "S1 Engine", 4.0, 6.0, 60, 0.96, isp: 290, thrust: 1500);
            var s1Tank = new DesignComponent("tank-s1", "tank", "S1 Tank", 8.0, 5.0, 50, 0.99);
            var stage1 = new StageDesign("s1", "Stage 1", s1Engine, s1Tank, propellantMass: 130.0);

            var bus = new DesignComponent("bus-1", "bus", "Bus 1", 0.5, 2.0, 20, 0.98);
            var payload = new PayloadDesign("p-1", "Payload", bus: bus);

            var bp = new Blueprint("bp-3stage", "Saturn", payload: payload, stages: new[] { stage1, stage2, stage3 });
            var anatomy = RocketVisualAnatomy.Build(bp);

            AssertTrue(anatomy.Sections.Count > 10, "Rocket anatomy must have at least 10 sections.");
            AssertEqual(3, anatomy.StageCount, "Stage count must be 3.");
            AssertTrue(anatomy.TotalHeightMeters > 20.0, "Total rocket height in meters must exceed 20m.");
            AssertTrue(anatomy.MaxDiameterMeters >= 3.0, "Booster diameter must be realistic (>= 3.0m).");

            // Top section must be fairing
            var fairing = anatomy.Sections.First(s => s.PartType == CrossSectionPartType.FairingNose);
            AssertTrue(fairing.NormalizedDiameter < 1.0, "Fairing must be narrower than lower booster.");
        }

        public static void Test_Anatomy_DynamicScaling_PropellantAndThrust()
        {
            var s1EngineSmall = new DesignComponent("eng-small", "engine", "Small", 1.0, 1.0, 10, 0.95, isp: 300, thrust: 500);
            var s1TankSmall = new DesignComponent("tank-small", "tank", "Small", 2.0, 1.0, 10, 0.95);
            var stageSmall = new StageDesign("s1", "S1", s1EngineSmall, s1TankSmall, propellantMass: 40.0);
            var bpSmall = new Blueprint("bp-small", "Small", stages: new[] { stageSmall });
            var anatSmall = RocketVisualAnatomy.Build(bpSmall);

            var s1EngineBig = new DesignComponent("eng-big", "engine", "Big", 4.0, 4.0, 40, 0.95, isp: 300, thrust: 2000);
            var s1TankBig = new DesignComponent("tank-big", "tank", "Big", 8.0, 4.0, 40, 0.95);
            var stageBig = new StageDesign("s1", "S1", s1EngineBig, s1TankBig, propellantMass: 180.0);
            var bpBig = new Blueprint("bp-big", "Big", stages: new[] { stageBig });
            var anatBig = RocketVisualAnatomy.Build(bpBig);

            var smallTankSec = anatSmall.Sections.First(s => s.SlotId == VehicleSlotId.Stage1Tank);
            var bigTankSec = anatBig.Sections.First(s => s.SlotId == VehicleSlotId.Stage1Tank);

            AssertTrue(bigTankSec.HeightMeters > smallTankSec.HeightMeters,
                "Bigger propellant mass must yield taller tank section.");
        }

        public static void Test_Anatomy_CrewedMission_IncludesCabinLifeSupportAndEscapeTower()
        {
            var cabin = new DesignComponent("cabin-mk1", "cabin", "Crew Cabin", 3.0, 15.0, 50, 0.99);
            var lifeSupport = new DesignComponent("eclss-mk1", "life-support", "Life Support", 1.5, 8.0, 30, 0.99);
            var abort = new DesignComponent("abort-mk1", "abort-system", "Escape Tower", 1.0, 5.0, 20, 0.99);
            var payload = new PayloadDesign("p-crewed", "Crewed Craft", cabin: cabin, lifeSupport: lifeSupport, abortSystem: abort);
            var stage1 = new StageDesign("s1", "S1", new DesignComponent("e1", "engine", "E1", 2, 2, 10, 0.95));

            var bpCrewed = new Blueprint("bp-crewed", "Apollo", payload: payload, stages: new[] { stage1 });
            var anatomy = RocketVisualAnatomy.Build(bpCrewed);

            AssertTrue(anatomy.IsCrewedMission, "Anatomy should detect crewed mission.");
            AssertTrue(anatomy.Sections.Any(s => s.SlotId == VehicleSlotId.PayloadCabin), "Cabin section must exist.");
            AssertTrue(anatomy.Sections.Any(s => s.SlotId == VehicleSlotId.PayloadLifeSupport), "Life support section must exist.");
            AssertTrue(anatomy.Sections.Any(s => s.SlotId == VehicleSlotId.PayloadAbortSystem), "Abort tower section must exist.");
        }

        public static void Test_Anatomy_FindSectionBySlot_MapsAllSlotsAccurately()
        {
            var bp = WorkshopViewModel.CreateDefaultLunarRoverBlueprint();
            var anatomy = RocketVisualAnatomy.Build(bp);

            var busSec = anatomy.FindSectionBySlot(VehicleSlotId.PayloadBus);
            AssertTrue(busSec != null, "Payload bus slot must map to a section.");
            AssertEqual(VehicleSlotId.PayloadBus, busSec.SlotId.Value);

            var s1TankSec = anatomy.FindSectionBySlot(VehicleSlotId.Stage1Tank);
            AssertTrue(s1TankSec != null, "Stage 1 Tank slot must map to a section.");
            AssertEqual(VehicleSlotId.Stage1Tank, s1TankSec.SlotId.Value);
        }

        public static void Test_Workshop_AllSeventeenSlots_InitializedAndSelectable()
        {
            var vm = new WorkshopViewModel();
            AssertEqual(17, vm.Slots.Count, "Workshop must initialize all 17 standard VehicleSlotId slots.");

            // Verify Stage 3 slots and Instrument 2 are now present and selectable
            vm.SelectSlot(VehicleSlotId.Stage3Engine);
            AssertTrue(vm.SelectedSlot.Value != null, "Stage 3 Engine must be selectable.");
            AssertEqual(VehicleSlotId.Stage3Engine, vm.SelectedSlot.Value.SlotId);

            vm.SelectSlot(VehicleSlotId.Stage3Tank);
            AssertTrue(vm.SelectedSlot.Value != null, "Stage 3 Tank must be selectable.");
            AssertEqual(VehicleSlotId.Stage3Tank, vm.SelectedSlot.Value.SlotId);

            vm.SelectSlot(VehicleSlotId.PayloadInstrument2);
            AssertTrue(vm.SelectedSlot.Value != null, "Payload Instrument 2 must be selectable.");
            AssertEqual(VehicleSlotId.PayloadInstrument2, vm.SelectedSlot.Value.SlotId);
        }

        public static void Test_Workshop_ResearchLockedOption_HasLockFlagAndReason()
        {
            var lockedComp = new DesignComponent(
                id: "eng-fusion-experimental",
                category: "engine",
                name: "D-He3 Füzyon Motoru",
                dryMass: 5.0,
                cost: 80.0,
                buildTime: 120,
                reliability: 0.98,
                requiredResearchId: "tech-fusion-propulsion"
            );

            // Mock research check that marks tech-fusion-propulsion as locked
            var vm = new WorkshopViewModel(
                catalog: new[] { lockedComp },
                isResearchUnlocked: techId => techId != "tech-fusion-propulsion"
            );

            vm.SelectSlot(VehicleSlotId.Stage1Engine);
            var option = vm.AvailableOptions.Value.FirstOrDefault(o => o.Id == "eng-fusion-experimental");

            AssertTrue(option != null, "Option must be present in available list.");
            AssertTrue(option.IsLocked, "Option must be marked locked when required research is missing.");
            AssertTrue(option.LockReason.Contains("tech-fusion-propulsion"), "Lock reason must mention the research ID.");
        }
    }
}
