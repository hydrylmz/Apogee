using System.Linq;
using Apogee.Design;
using Apogee.UI.Screens.Workshop;
using NUnit.Framework;

namespace Apogee.Tests.EditMode.UI
{
    [TestFixture]
    public class RocketCrossSectionUITests
    {
        [Test]
        public void CrossSectionViewModel_UpdatesLayoutAndGeneratesNodes()
        {
            var s1Engine = new DesignComponent("eng-1", "engine", "S1 Booster Engine", 2.0, 3.0, 30, 0.95, isp: 310, thrust: 800);
            var s1Tank = new DesignComponent("tank-1", "tank", "S1 Propellant Tank", 4.0, 2.0, 20, 0.99);
            var stage1 = new StageDesign("s1", "Stage 1", s1Engine, s1Tank, 80.0);

            var bus = new DesignComponent("bus-1", "bus", "Rover Chassis", 0.5, 4.0, 25, 0.98);
            var payload = new PayloadDesign("p-1", "Rover", bus: bus);

            var blueprint = new Blueprint("bp-test", "Test Vehicle", payload: payload, stages: new[] { stage1 });

            var vm = new RocketCrossSectionViewModel();
            vm.UpdateLayout(blueprint);

            Assert.IsNotEmpty(vm.Nodes.Value);
            Assert.AreEqual(1, vm.StageCount.Value);
            Assert.IsTrue(vm.HasPayload.Value);

            var busNode = vm.Nodes.Value.FirstOrDefault(n => n.SlotId == VehicleSlotId.PayloadBus);
            Assert.IsNotNull(busNode);
            Assert.IsFalse(busNode.IsEmptySlot);
        }

        [Test]
        public void CrossSectionViewModel_TwoWaySlotSelectionSync()
        {
            var s1Engine = new DesignComponent("eng-1", "engine", "S1 Engine", 2.0, 3.0, 30, 0.95);
            var stage1 = new StageDesign("s1", "Stage 1", s1Engine);
            var blueprint = new Blueprint("bp-sync", "Sync Test", stages: new[] { stage1 });

            var vm = new RocketCrossSectionViewModel(blueprint);

            VehicleSlotId? notifiedSlot = null;
            vm.SlotSelected += slot => notifiedSlot = slot;

            var targetNode = vm.Nodes.Value.First(n => n.SlotId == VehicleSlotId.Stage1Engine);

            // Act: Request part selection in cross-section
            vm.RequestSelectPart(targetNode);

            // Assert
            Assert.AreEqual(targetNode, vm.SelectedNode.Value);
            Assert.AreEqual(VehicleSlotId.Stage1Engine, vm.SelectedSlotId.Value);
            Assert.AreEqual(VehicleSlotId.Stage1Engine, notifiedSlot);

            // Act: Change selected slot externally (from left slot panel)
            vm.SetSelectedSlot(VehicleSlotId.PayloadBus);

            // Assert
            Assert.AreEqual(VehicleSlotId.PayloadBus, vm.SelectedSlotId.Value);
            Assert.IsNotNull(vm.SelectedNode.Value);
            Assert.AreEqual(VehicleSlotId.PayloadBus, vm.SelectedNode.Value.SlotId);
        }

        [Test]
        public void WorkshopViewModel_CrossSectionIntegration_LiveSyncs()
        {
            var workshopVm = new WorkshopViewModel();

            Assert.IsNotNull(workshopVm.CrossSection);
            Assert.IsNotEmpty(workshopVm.CrossSection.Nodes.Value);

            // Selecting a slot in Workshop updates CrossSection
            workshopVm.SelectSlot(VehicleSlotId.Stage1Engine);
            Assert.AreEqual(VehicleSlotId.Stage1Engine, workshopVm.CrossSection.SelectedSlotId.Value);

            // Clicking a part in CrossSection selects the slot in Workshop
            var tankNode = workshopVm.CrossSection.Nodes.Value.First(n => n.SlotId == VehicleSlotId.Stage1Tank);
            workshopVm.CrossSection.RequestSelectPart(tankNode);

            Assert.AreEqual(VehicleSlotId.Stage1Tank, workshopVm.SelectedSlot.Value.SlotId);
            Assert.IsTrue(workshopVm.SelectedSlot.Value.IsSelected.Value);
        }
    }
}
