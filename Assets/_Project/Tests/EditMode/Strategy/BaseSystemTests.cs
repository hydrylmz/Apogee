using NUnit.Framework;
using Apogee.Core;
using Apogee.Design;
using Apogee.Strategy;

namespace Apogee.Tests.EditMode.Strategy
{
    /// <summary>
    /// Unit tests for M4-03: BaseType, BaseLevelSpec, BaseFacility, BuildBaseCommand, UpgradeBaseCommand.
    /// Complies with GDD §6.1, §16.7, §19.8.
    /// </summary>
    [TestFixture]
    public class BaseSystemTests
    {
        [Test]
        public void CampaignState_InitialBases_ConfiguredPerGDD()
        {
            var state = new CampaignState(seed: 1);

            // GDD §4.2: Starting base facilities (1 small launch pad, 1 factory, 1 small research center)
            Assert.AreEqual(3, state.Bases.Count);
            Assert.IsTrue(state.Bases.ContainsKey("base_cape_pad"));
            Assert.IsTrue(state.Bases.ContainsKey("base_cape_factory"));
            Assert.IsTrue(state.Bases.ContainsKey("base_cape_research"));

            var pad = state.Bases["base_cape_pad"];
            Assert.AreEqual(BaseType.LaunchPad, pad.Type);
            Assert.AreEqual(1, pad.Level);
        }

        [Test]
        public void BaseLevelSpec_CapacityRatings_ScaleWithLevel()
        {
            double pad1 = BaseLevelSpec.GetCapacityRating(BaseType.LaunchPad, 1);
            double pad3 = BaseLevelSpec.GetCapacityRating(BaseType.LaunchPad, 3);
            double pad5 = BaseLevelSpec.GetCapacityRating(BaseType.LaunchPad, 5);

            Assert.AreEqual(60.0, pad1);
            Assert.AreEqual(1500.0, pad3);
            Assert.AreEqual(9000.0, pad5);

            double factory1 = BaseLevelSpec.GetCapacityRating(BaseType.Factory, 1);
            double factory5 = BaseLevelSpec.GetCapacityRating(BaseType.Factory, 5);
            Assert.Greater(factory5, factory1);
        }

        [Test]
        public void BaseFacility_AsLaunchPadInfo_MatchesTierCapabilities()
        {
            var pad = new BaseFacility("pad_test", "guiana_coast", BaseType.LaunchPad, level: 2);
            LaunchPadInfo info = pad.AsLaunchPadInfo();

            Assert.IsNotNull(info);
            Assert.AreEqual(2, info.Level);
            Assert.AreEqual(350.0, info.MaxWetMass);
            Assert.AreEqual(3, info.MaxStages);
        }

        [Test]
        public void BuildBaseCommand_ExecutesAndUndoes_Correctly()
        {
            var state = new CampaignState(seed: 1);
            double initialBudget = state.Budget;

            var ctx = CommandContext.CreateLocalAdmin(state, state.Time.Tick);
            var cmd = new BuildBaseCommand("base_guiana_pad", "guiana_coast", BaseType.LaunchPad, laborCostMultiplier: 1.1);

            var res = state.CommandBus.Execute(cmd, ctx);
            Assert.IsTrue(res.Success, res.Message);
            Assert.IsTrue(state.Bases.ContainsKey("base_guiana_pad"));
            Assert.Less(state.Budget, initialBudget);

            // Undo
            var undoRes = state.CommandBus.Undo(ctx);
            Assert.IsTrue(undoRes.Success, undoRes.Message);
            Assert.IsFalse(state.Bases.ContainsKey("base_guiana_pad"));
            Assert.AreEqual(initialBudget, state.Budget);
        }

        [Test]
        public void BuildBaseCommand_RejectsDuplicateBaseId()
        {
            var state = new CampaignState(seed: 1);
            var cmd = new BuildBaseCommand("base_cape_pad", "cape_peninsula", BaseType.LaunchPad);

            var res = state.Execute(cmd);
            Assert.IsFalse(res.Success);
            Assert.That(res.Message, Does.Contain("already exists"));
        }

        [Test]
        public void BuildBaseCommand_RejectsWhenInsufficientBudget()
        {
            var state = new CampaignState(seed: 1);
            state.Budget = 5.0; // low budget

            var cmd = new BuildBaseCommand("expensive_pad", "guiana_coast", BaseType.LaunchPad);
            var res = state.Execute(cmd);

            Assert.IsFalse(res.Success);
            Assert.That(res.Message, Does.Contain("Insufficient budget"));
        }

        [Test]
        public void UpgradeBaseCommand_UpgradesLevel_AndUndoes()
        {
            var state = new CampaignState(seed: 1);
            var pad = state.Bases["base_cape_pad"];
            Assert.AreEqual(1, pad.Level);
            double initialBudget = state.Budget;

            var ctx = CommandContext.CreateLocalAdmin(state, state.Time.Tick);
            var cmd = new UpgradeBaseCommand("base_cape_pad", laborCostMultiplier: 1.0);

            var res = state.CommandBus.Execute(cmd, ctx);
            Assert.IsTrue(res.Success, res.Message);
            Assert.AreEqual(2, pad.Level);
            Assert.Less(state.Budget, initialBudget);

            // Undo
            var undoRes = state.CommandBus.Undo(ctx);
            Assert.IsTrue(undoRes.Success, undoRes.Message);
            Assert.AreEqual(1, pad.Level);
            Assert.AreEqual(initialBudget, state.Budget);
        }

        [Test]
        public void UpgradeBaseCommand_RejectsWhenMaxLevelReached()
        {
            var state = new CampaignState(seed: 1);
            var pad = state.Bases["base_cape_pad"];
            // Advance to level 5
            for (int i = 0; i < 4; i++)
            {
                state.Execute(new UpgradeBaseCommand("base_cape_pad"));
            }
            Assert.AreEqual(5, pad.Level);

            var res = state.Execute(new UpgradeBaseCommand("base_cape_pad"));
            Assert.IsFalse(res.Success);
            Assert.That(res.Message, Does.Contain("maximum level"));
        }
    }
}
