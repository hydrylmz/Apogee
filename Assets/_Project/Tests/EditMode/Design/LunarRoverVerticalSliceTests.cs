using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Apogee.Core;
using Apogee.Design;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Screens.Review;
using Apogee.UI.Screens.Workshop;

namespace Apogee.Tests
{
    /// <summary>
    /// EditMode unit tests for Milestone 2 Vertical Slice:
    /// Destination Moon, Rover Mission Type, ~12 Modules, 3 Engines, 3 Materials, 1 Supplier.
    /// Complies with GDD §21 M2-07:
    /// "Kabul: Oyuncu Ay rover'ı tasarlayıp review'dan geçirebilir."
    /// </summary>
    [TestFixture]
    public class LunarRoverVerticalSliceTests
    {
        [Test]
        public void MoonDestination_CatalogIntegrity_MatchesGDD()
        {
            Assert.AreEqual("moon", LunarRoverVerticalSlice.DestinationId);
            Assert.AreEqual("Ay (Lunar Surface)", LunarRoverVerticalSlice.DestinationName);
            Assert.GreaterOrEqual(LunarRoverVerticalSlice.TargetDeltaV, 13000.0);

            var profile = LunarRoverVerticalSlice.CreateMissionProfile();
            Assert.AreEqual("moon", profile.TargetDestinationId);
            Assert.AreEqual("rover", profile.TargetMissionTypeId);
            Assert.IsTrue(profile.RequiresEdl);
        }

        [Test]
        public void RoverMissionType_CatalogIntegrity_MatchesGDD()
        {
            Assert.AreEqual("rover", LunarRoverVerticalSlice.MissionTypeId);
            Assert.AreEqual("Ay Gezgini (Rover)", LunarRoverVerticalSlice.MissionTypeName);
            Assert.AreEqual(0.50, LunarRoverVerticalSlice.MinReliability);

            var profile = LunarRoverVerticalSlice.CreateMissionProfile();
            Assert.IsTrue(profile.RequiredModuleCategories.Contains("bus"));
            Assert.IsTrue(profile.RequiredModuleCategories.Contains("power"));
            Assert.IsTrue(profile.RequiredModuleCategories.Contains("edl"));
            Assert.IsFalse(profile.RequiresLifeSupport);
        }

        [Test]
        public void Materials_CatalogIntegrity_ThreeMaterialsWithMassAndCostScaling()
        {
            var materials = LunarRoverVerticalSlice.Materials;
            Assert.AreEqual(3, materials.Count);

            var al = materials[0];
            Assert.AreEqual("al-alloy", al.Id);
            Assert.AreEqual(1.0, al.MassMultiplier);
            Assert.AreEqual(1.0, al.CostMultiplier);

            var alLi = materials[1];
            Assert.AreEqual("al-li", alLi.Id);
            Assert.Less(alLi.MassMultiplier, 1.0);
            Assert.Greater(alLi.CostMultiplier, 1.0);

            var carbon = materials[2];
            Assert.AreEqual("carbon-composite", carbon.Id);
            Assert.Less(carbon.MassMultiplier, alLi.MassMultiplier);
            Assert.Greater(carbon.CostMultiplier, alLi.CostMultiplier);
        }

        [Test]
        public void ModulesAndEngines_Counts_ProvidesThreeEnginesAndTwelvePlusModules()
        {
            var modules = LunarRoverVerticalSlice.Modules;
            Assert.GreaterOrEqual(modules.Count, 12);

            var engines = modules.Where(m => m.Category == "engine").ToList();
            Assert.AreEqual(3, engines.Count);

            Assert.AreEqual("engine-kerosene-lox-mk1", LunarRoverVerticalSlice.EngineKeroseneMk1.Id);
            Assert.AreEqual(310.0, LunarRoverVerticalSlice.EngineKeroseneMk1.Isp);

            Assert.AreEqual("engine-kerosene-lox-heavy", LunarRoverVerticalSlice.EngineKeroseneHeavy.Id);
            Assert.AreEqual(320.0, LunarRoverVerticalSlice.EngineKeroseneHeavy.Isp);

            Assert.AreEqual("engine-hydrolox-upper-mk1", LunarRoverVerticalSlice.EngineHydroloxUpper.Id);
            Assert.AreEqual(445.0, LunarRoverVerticalSlice.EngineHydroloxUpper.Isp);
        }

        [Test]
        public void HelionSupplier_Specs_MatchesFastDeliveryAndLowCost()
        {
            var helion = LunarRoverVerticalSlice.HelionSupplier;
            Assert.AreEqual("helion-dynamics", helion.Id);
            Assert.AreEqual("Helion Dynamics", helion.Name);
            Assert.AreEqual(0.85, helion.CostMultiplier);
            Assert.AreEqual(0.90, helion.DeliveryMultiplier);
            Assert.AreEqual(0.96, helion.ReliabilityMultiplier);
        }

        [Test]
        public void MissingEdlBlueprint_FailsReview_WithMandatoryModuleError()
        {
            var blueprint = LunarRoverVerticalSlice.CreateMissingEdlBlueprint();
            var context = LunarRoverVerticalSlice.CreateValidationContext();

            var report = BlueprintValidator.Validate(blueprint, context);
            Assert.IsFalse(report.IsValid);
            Assert.Greater(report.ErrorCount, 0);

            bool hasEdlIssue = report.Issues.Any(i =>
                i.RuleId == ValidationRuleId.MandatoryModules &&
                (i.Reason.IndexOf("EDL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 i.Title.IndexOf("EDL", StringComparison.OrdinalIgnoreCase) >= 0));

            Assert.IsTrue(hasEdlIssue, "Validation report should flag missing mandatory EDL module");
        }

        [Test]
        public void EquippingEdl_ConvertsToPassingReview()
        {
            var failingBlueprint = LunarRoverVerticalSlice.CreateMissingEdlBlueprint();
            var context = LunarRoverVerticalSlice.CreateValidationContext();

            var initialReport = BlueprintValidator.Validate(failingBlueprint, context);
            Assert.IsFalse(initialReport.IsValid);

            // Player equips EDL Skycrane via slot
            var edlComponent = LunarRoverVerticalSlice.EdlLunarSkycrane;
            var correctedBlueprint = failingBlueprint.WithSlotComponent(VehicleSlotId.PayloadEdl, edlComponent);

            var resolvedReport = BlueprintValidator.Validate(correctedBlueprint, context);
            Assert.IsTrue(resolvedReport.IsValid);
            Assert.AreEqual(0, resolvedReport.ErrorCount);
        }

        [Test]
        public void PassingBlueprint_AllEightValidatorsPass()
        {
            var blueprint = LunarRoverVerticalSlice.CreatePassingBlueprint();
            var context = LunarRoverVerticalSlice.CreateValidationContext();

            var report = BlueprintValidator.Validate(blueprint, context);
            Assert.IsTrue(report.IsValid);
            Assert.AreEqual(0, report.ErrorCount);

            var stats = BlueprintEvaluator.Evaluate(blueprint);
            Assert.GreaterOrEqual(stats.NetDeltaV, context.MissionProfile.RequiredDeltaV);
            Assert.GreaterOrEqual(stats.Reliability, context.MissionProfile.MinReliabilityThreshold);
            Assert.GreaterOrEqual(stats.PowerMargin, 0.0);
            Assert.Greater(stats.DryMass, 0.0);
        }

        [Test]
        public void MaterialSwitch_ChangesMassAndDeltaV()
        {
            var bpAlloy = LunarRoverVerticalSlice.CreatePassingBlueprint(LunarRoverVerticalSlice.AluminumAlloy);
            var bpCarbon = LunarRoverVerticalSlice.CreatePassingBlueprint(LunarRoverVerticalSlice.CarbonComposite);

            var statsAlloy = BlueprintEvaluator.Evaluate(bpAlloy);
            var statsCarbon = BlueprintEvaluator.Evaluate(bpCarbon);

            Assert.Less(statsCarbon.DryMass, statsAlloy.DryMass);
            Assert.Greater(statsCarbon.NetDeltaV, statsAlloy.NetDeltaV);
            Assert.Greater(statsCarbon.Cost, statsAlloy.Cost);
        }

        [Test]
        public void Workshop_GoToReview_PassesValidLunarRoverContext()
        {
            var nav = new ScreenNavigator();
            var bus = new CommandBus();
            var bp = LunarRoverVerticalSlice.CreatePassingBlueprint();
            var vm = new WorkshopViewModel(nav, bus, bp, LunarRoverVerticalSlice.Modules);

            ReviewNavigationArgs capturedArgs = null;
            var dummyReview = new DummyScreen(ScreenId.DesignReview, ctx =>
            {
                if (ctx is ReviewNavigationArgs rArgs)
                    capturedArgs = rArgs;
            });
            nav.RegisterScreen(dummyReview);

            vm.GoToReviewCommand.Execute();

            Assert.IsNotNull(capturedArgs);
            Assert.AreEqual(bp.Id, capturedArgs.Blueprint.Id);
            Assert.AreEqual("moon", capturedArgs.Context.MissionProfile.TargetDestinationId);
            Assert.AreEqual("rover", capturedArgs.Context.MissionProfile.TargetMissionTypeId);
            Assert.IsTrue(capturedArgs.Context.MissionProfile.RequiresEdl);

            var reviewVm = new ReviewViewModel(nav, capturedArgs.Blueprint, capturedArgs.Context);
            Assert.IsTrue(reviewVm.IsValid.Value);
            Assert.AreEqual(0, reviewVm.ErrorCount.Value);

            reviewVm.Dispose();
            vm.Dispose();
        }

        private class DummyScreen : IScreenController
        {
            private readonly Action<object> _onEnterCallback;
            public ScreenId ScreenId { get; }
            public UnityEngine.UIElements.VisualElement RootElement => null;
            public bool IsVisible { get; private set; }

            public DummyScreen(ScreenId id, Action<object> onEnterCallback = null)
            {
                ScreenId = id;
                _onEnterCallback = onEnterCallback;
            }

            public void Initialize(UnityEngine.UIElements.VisualElement parent) { }
            public void OnEnter(object context = null)
            {
                IsVisible = true;
                _onEnterCallback?.Invoke(context);
            }
            public void OnExit() => IsVisible = false;
            public void Dispose() { }
        }
    }
}
