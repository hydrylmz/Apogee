using System.Linq;
using NUnit.Framework;
using Apogee.Design;
using Apogee.UI.Screens.Workshop;

namespace Apogee.Tests
{
    /// <summary>
    /// EditMode unit tests for WorkshopViewModel, VehicleSlotViewModel, ModuleOptionViewModel,
    /// and GhostDeltaViewModel (GDD §7.2, §7.3 & M2-02).
    /// </summary>
    [TestFixture]
    public class WorkshopSlotUITests
    {
        [Test]
        public void VehicleSlotViewModel_EquippedStateAndSelection_NotifiesObservables()
        {
            var def = VehicleSlotDefinition.Get(VehicleSlotId.PayloadBus);
            bool selectedInvoked = false;
            var vm = new VehicleSlotViewModel(def, () => selectedInvoked = true);

            Assert.IsTrue(vm.EquippedName.Value.Contains("Zorunlu"));

            var comp = new DesignComponent("bus-light", "bus", "Hafif Şasi", dryMass: 0.35, cost: 5.5, reliability: 0.96);
            vm.SetEquipped(comp);

            Assert.AreEqual("Hafif Şasi", vm.EquippedName.Value);
            Assert.IsTrue(vm.SpecsSummary.Value.Contains("0.35t") || vm.SpecsSummary.Value.Contains("0,35t"));
            Assert.IsTrue(vm.SpecsSummary.Value.Contains("5.5M") || vm.SpecsSummary.Value.Contains("5,5M"));

            vm.SelectSlotCommand.Execute();
            Assert.IsTrue(selectedInvoked);
        }

        [Test]
        public void ModuleOptionViewModel_LockedBehaviorAndCommands_FunctionAccurately()
        {
            var comp = new DesignComponent("eng-adv", "engine", "Gelişmiş Motor", dryMass: 2.0);
            bool hovered = false;
            bool selected = false;

            var unlockedOption = new ModuleOptionViewModel(
                comp,
                isLocked: false,
                onHover: () => hovered = true,
                onSelect: () => selected = true
            );

            Assert.IsTrue(unlockedOption.SelectCommand.CanExecute());
            unlockedOption.OnHover?.Invoke();
            Assert.IsTrue(hovered);

            unlockedOption.SelectCommand.Execute();
            Assert.IsTrue(selected);

            var lockedOption = new ModuleOptionViewModel(
                comp,
                isLocked: true,
                lockReason: "Gerekli araştırma tamamlanmadı"
            );
            Assert.IsFalse(lockedOption.SelectCommand.CanExecute());
        }

        [Test]
        public void GhostDeltaViewModel_ApplyAndClear_ControlsActiveState()
        {
            var ghostVm = new GhostDeltaViewModel();
            Assert.IsFalse(ghostVm.IsActive.Value);

            var baseBp = WorkshopViewModel.CreateDefaultLunarRoverBlueprint();
            var candidate = new DesignComponent("eng-heavy", "engine", "Ağır Motor", dryMass: 4.0, cost: 15.0, isp: 330);
            var delta = GhostDelta.Compute(baseBp, VehicleSlotId.Stage1Engine, candidate);

            ghostVm.ApplyDelta(delta);
            Assert.IsTrue(ghostVm.IsActive.Value);
            Assert.AreNotEqual(0.0, ghostVm.DeltaDeltaV.Value);
            Assert.IsTrue(!string.IsNullOrEmpty(ghostVm.SummaryText.Value));

            ghostVm.Clear();
            Assert.IsFalse(ghostVm.IsActive.Value);
            Assert.AreEqual(0.0, ghostVm.DeltaDeltaV.Value);
            Assert.AreEqual(string.Empty, ghostVm.SummaryText.Value);
        }

        [Test]
        public void WorkshopViewModel_SlotSelection_PopulatesCompatibleOptions()
        {
            var vm = new WorkshopViewModel();

            Assert.IsNotNull(vm.ActiveBlueprint);
            Assert.Greater(vm.Slots.Count, 0);

            // Select Bus slot
            vm.SelectSlot(VehicleSlotId.PayloadBus);

            Assert.IsNotNull(vm.SelectedSlot.Value);
            Assert.AreEqual(VehicleSlotId.PayloadBus, vm.SelectedSlot.Value.SlotId);
            Assert.Greater(vm.AvailableOptions.Value.Count, 0);

            // All options must be category "bus"
            foreach (var opt in vm.AvailableOptions.Value)
            {
                Assert.AreEqual("bus", opt.Category);
            }
        }

        [Test]
        public void WorkshopViewModel_HoverAndUnhover_TogglesGhostDelta()
        {
            var vm = new WorkshopViewModel();
            vm.SelectSlot(VehicleSlotId.Stage1Engine);

            var candidate = vm.AvailableOptions.Value.First(o => o.Id != vm.SelectedSlot.Value.EquippedComponent.Value?.Id);

            // Hover
            candidate.OnHover?.Invoke();
            Assert.IsTrue(vm.GhostDelta.IsActive.Value);
            Assert.AreNotEqual(0.0, vm.GhostDelta.DeltaDeltaV.Value);

            // Unhover
            candidate.OnUnhover?.Invoke();
            Assert.IsFalse(vm.GhostDelta.IsActive.Value);
        }

        [Test]
        public void WorkshopViewModel_EquipOption_ExecutesCommand_And_UndoRedoWorks()
        {
            var vm = new WorkshopViewModel();
            vm.SelectSlot(VehicleSlotId.Stage1Engine);

            var initialEngineId = vm.SelectedSlot.Value.EquippedComponent.Value?.Id;
            var differentCandidate = vm.AvailableOptions.Value.First(o => o.Id != initialEngineId);

            Assert.IsFalse(vm.CanUndo.Value);

            // Equip module via command
            differentCandidate.SelectCommand.Execute();

            // Active blueprint updated
            var newEngineId = vm.SelectedSlot.Value.EquippedComponent.Value?.Id;
            Assert.AreEqual(differentCandidate.Id, newEngineId);
            Assert.AreNotEqual(initialEngineId, newEngineId);
            Assert.IsTrue(vm.CanUndo.Value);

            // Undo
            vm.UndoCommand.Execute();
            Assert.AreEqual(initialEngineId, vm.SelectedSlot.Value.EquippedComponent.Value?.Id);
            Assert.IsTrue(vm.CanRedo.Value);

            // Redo
            vm.RedoCommand.Execute();
            Assert.AreEqual(differentCandidate.Id, vm.SelectedSlot.Value.EquippedComponent.Value?.Id);
        }
    }
}
