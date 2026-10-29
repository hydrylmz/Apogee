using System.Linq;
using NUnit.Framework;
using Apogee.Core;
using Apogee.Design;
using Apogee.Design.Commands;

namespace Apogee.Tests
{
    /// <summary>
    /// EditMode unit tests for VehicleSlotDefinition, Blueprint slot operations,
    /// SetSlotComponentCommand, CommandBus Undo/Redo, and GhostDelta calculation (GDD §7.2, §7.3 & M2-02).
    /// </summary>
    [TestFixture]
    public class SlotCommandTests
    {
        private class TestBlueprintHolder : IBlueprintHolder
        {
            public Blueprint ActiveBlueprint { get; set; }

            public TestBlueprintHolder(Blueprint initial)
            {
                ActiveBlueprint = initial;
            }
        }

        private Blueprint CreateTestBlueprint()
        {
            var bus = new DesignComponent("bus-test", "bus", "Test Bus", dryMass: 0.5, cost: 5.0, reliability: 0.95);
            var payload = new PayloadDesign("payload-test", "Test Payload", bus: bus);

            var engine1 = new DesignComponent("eng-1", "engine", "Engine 1", dryMass: 2.0, cost: 8.0, reliability: 0.95, isp: 310, thrust: 800);
            var tank1 = new DesignComponent("tank-1", "tank", "Tank 1", dryMass: 3.0, cost: 3.0, reliability: 0.99);
            var stage1 = new StageDesign("stage-1", "Stage 1", engine1, tank1, propellantMass: 50.0);

            return new Blueprint("bp-test", "Test Vehicle", version: 1, payload: payload, stages: new[] { stage1 });
        }

        [Test]
        public void VehicleSlotDefinition_AllStandardSlots_ExistAndHaveValidMetadata()
        {
            var slots = VehicleSlotDefinition.All.ToList();
            Assert.IsTrue(slots.Count >= 10);

            var busDef = VehicleSlotDefinition.Get(VehicleSlotId.PayloadBus);
            Assert.AreEqual("bus", busDef.CategoryTag);
            Assert.IsTrue(busDef.IsRequired);

            var engDef = VehicleSlotDefinition.Get(VehicleSlotId.Stage1Engine);
            Assert.AreEqual("engine", engDef.CategoryTag);
            Assert.IsTrue(engDef.IsRequired);
        }

        [Test]
        public void Blueprint_WithSlotComponent_And_GetComponentInSlot_FunctionCorrectly()
        {
            var bp = CreateTestBlueprint();

            // Verify initial
            var initialBus = bp.GetComponentInSlot(VehicleSlotId.PayloadBus);
            Assert.IsNotNull(initialBus);
            Assert.AreEqual("bus-test", initialBus.Id);

            var initialPower = bp.GetComponentInSlot(VehicleSlotId.PayloadPower);
            Assert.IsNull(initialPower);

            // Equip Power
            var powerComp = new DesignComponent("power-solar", "power", "Solar Panel", dryMass: 0.1, cost: 2.0, powerGeneration: 1.5);
            var updatedBp = bp.WithSlotComponent(VehicleSlotId.PayloadPower, powerComp);

            Assert.IsNotNull(updatedBp.GetComponentInSlot(VehicleSlotId.PayloadPower));
            Assert.AreEqual("power-solar", updatedBp.GetComponentInSlot(VehicleSlotId.PayloadPower).Id);

            // Immutability: original blueprint remains untouched
            Assert.IsNull(bp.GetComponentInSlot(VehicleSlotId.PayloadPower));
        }

        [Test]
        public void SetSlotComponentCommand_ExecutionAndSymmetricUndo_ViaCommandBus()
        {
            var bp = CreateTestBlueprint();
            var holder = new TestBlueprintHolder(bp);
            var bus = new CommandBus();

            var newEngine = new DesignComponent("eng-heavy", "engine", "Heavy Engine", dryMass: 3.5, cost: 14.0, reliability: 0.96, isp: 330, thrust: 1600);
            var command = new SetSlotComponentCommand(VehicleSlotId.Stage1Engine, newEngine);

            var adminContext = CommandContext.CreateLocalAdmin(holder);

            // Execute
            var execResult = bus.Execute(command, adminContext);
            Assert.IsTrue(execResult.Success, "Command execution should succeed.");
            Assert.AreEqual("eng-heavy", holder.ActiveBlueprint.GetComponentInSlot(VehicleSlotId.Stage1Engine).Id);
            Assert.AreEqual("eng-1", command.PreviousComponent.Id);
            Assert.IsTrue(bus.CanUndo);

            // Undo
            var undoResult = bus.Undo(adminContext);
            Assert.IsTrue(undoResult.Success, "Command undo should succeed.");
            Assert.AreEqual("eng-1", holder.ActiveBlueprint.GetComponentInSlot(VehicleSlotId.Stage1Engine).Id);
            Assert.IsTrue(bus.CanRedo);

            // Redo
            var redoResult = bus.Redo(adminContext);
            Assert.IsTrue(redoResult.Success, "Command redo should succeed.");
            Assert.AreEqual("eng-heavy", holder.ActiveBlueprint.GetComponentInSlot(VehicleSlotId.Stage1Engine).Id);
        }

        [Test]
        public void SetSlotComponentCommand_RoleAuthorization_RejectsUnauthorizedActor()
        {
            var bp = CreateTestBlueprint();
            var holder = new TestBlueprintHolder(bp);
            var bus = new CommandBus();

            var newEngine = new DesignComponent("eng-heavy", "engine", "Heavy Engine", dryMass: 3.5);
            var command = new SetSlotComponentCommand(VehicleSlotId.Stage1Engine, newEngine);

            // Science Director role does not have authorization to modify rocket propulsion
            var scienceContext = new CommandContext("player-1", PlayerRole.ScienceDirector, state: holder);

            var execResult = bus.Execute(command, scienceContext);
            Assert.IsFalse(execResult.Success);
            Assert.IsTrue(execResult.Message.Contains("authorized") || execResult.Message.Contains("role"));
            // Blueprint was not changed
            Assert.AreEqual("eng-1", holder.ActiveBlueprint.GetComponentInSlot(VehicleSlotId.Stage1Engine).Id);
        }

        [Test]
        public void GhostDelta_ComputesAccurateTelemetryDifferential()
        {
            var bp = CreateTestBlueprint();

            // Candidate with higher Isp and more thrust but heavier
            var candidateEngine = new DesignComponent(
                "eng-adv",
                "engine",
                "Advanced Engine",
                dryMass: 3.0, // +1.0t heavier
                cost: 12.0,   // +4.0M more expensive
                reliability: 0.98, // +0.03 more reliable
                isp: 350,     // +40s higher Isp
                thrust: 1200);

            var ghostDelta = GhostDelta.Compute(bp, VehicleSlotId.Stage1Engine, candidateEngine);

            Assert.IsTrue(ghostDelta.HasChanges);
            Assert.Greater(ghostDelta.DeltaDeltaV, 0.0, "Higher Isp should increase total Delta-V.");
            Assert.Greater(ghostDelta.DeltaWetMassTons, 0.0, "Heavier engine should increase total mass.");
            Assert.Greater(ghostDelta.DeltaCostMillions, 0.0, "More expensive engine should increase cost.");
            Assert.Greater(ghostDelta.DeltaReliability, 0.0, "More reliable engine should increase system reliability.");

            var summary = ghostDelta.GetSummaryText();
            Assert.IsTrue(summary.Contains("menzil"));
            Assert.IsTrue(summary.Contains("kütle"));
            Assert.IsTrue(summary.Contains("maliyet"));
        }
    }
}
