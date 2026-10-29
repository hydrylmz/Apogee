using System.Linq;
using NUnit.Framework;
using Apogee.Design;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Screens.Review;
using Apogee.UI.Screens.Workshop;

namespace Apogee.Tests.UI
{
    /// <summary>
    /// EditMode unit tests for the Design Review screen, ReviewViewModel, ReviewIssueItem,
    /// and Navigation integration (GDD §7.4, §8.3 & §21 M2-05).
    /// </summary>
    [TestFixture]
    public class ReviewScreenUITests
    {
        [Test]
        public void ReviewViewModel_RunsValidation_PopulatesTrafficLightIssues_AndCounts()
        {
            var defaultBp = WorkshopViewModel.CreateDefaultLunarRoverBlueprint();
            var context = new ValidationContext(
                missionProfile: new MissionProfile("moon-test", requiredDeltaV: 13500, minReliabilityThreshold: 0.80),
                launchPad: LaunchPadInfo.Level2Medium
            );

            var vm = new ReviewViewModel(null, defaultBp, context);

            // Verify telemetry bars and issue list
            Assert.AreEqual(7, vm.ResultBars.Count);
            Assert.Greater(vm.Issues.Count, 0);

            // Verify counts consistency
            Assert.AreEqual(vm.Issues.Count(i => i.IsError), vm.ErrorCount.Value);
            Assert.AreEqual(vm.Issues.Count(i => i.IsWarning), vm.WarningCount.Value);
            Assert.AreEqual(vm.Issues.Count(i => i.IsPass), vm.PassCount.Value);
            Assert.AreEqual(vm.ErrorCount.Value == 0, vm.IsValid.Value);

            // Verify GO / NO-GO badge status text
            if (vm.ErrorCount.Value > 0)
            {
                Assert.IsTrue(vm.GoNoGoBadgeText.Value.Contains("NO-GO") || vm.GoNoGoBadgeText.Value.Contains("ENGEL"));
            }
            else if (vm.WarningCount.Value > 0)
            {
                Assert.IsTrue(vm.GoNoGoBadgeText.Value.Contains("ŞARTLI") || vm.GoNoGoBadgeText.Value.Contains("UYARI"));
            }
            else
            {
                Assert.IsTrue(vm.GoNoGoBadgeText.Value.Contains("GO") || vm.GoNoGoBadgeText.Value.Contains("UYGUN"));
            }
        }

        [Test]
        public void ReviewViewModel_FilterMode_FiltersIssuesCorrectly()
        {
            var defaultBp = WorkshopViewModel.CreateDefaultLunarRoverBlueprint();
            // A strict context to force at least one error and warning
            var strictContext = new ValidationContext(
                missionProfile: new MissionProfile("strict-moon", requiredDeltaV: 20000, minReliabilityThreshold: 0.999),
                launchPad: LaunchPadInfo.Level1Small
            );

            var vm = new ReviewViewModel(null, defaultBp, strictContext);

            // Filter All
            vm.SetFilter(ReviewFilterMode.All);
            Assert.AreEqual(vm.Issues.Count, vm.FilteredIssues.Count);

            // Filter Errors (Kırmızı)
            vm.SetFilter(ReviewFilterMode.Errors);
            Assert.AreEqual(vm.ErrorCount.Value, vm.FilteredIssues.Count);
            Assert.IsTrue(vm.FilteredIssues.All(i => i.IsError));

            // Filter Warnings (Sarı)
            vm.SetFilter(ReviewFilterMode.Warnings);
            Assert.AreEqual(vm.WarningCount.Value, vm.FilteredIssues.Count);
            Assert.IsTrue(vm.FilteredIssues.All(i => i.IsWarning));

            // Filter Passed (Yeşil)
            vm.SetFilter(ReviewFilterMode.Passed);
            Assert.AreEqual(vm.PassCount.Value, vm.FilteredIssues.Count);
            Assert.IsTrue(vm.FilteredIssues.All(i => i.IsPass));
        }

        [Test]
        public void ReviewIssueItem_NedenAndHint_Integrity()
        {
            var item = new ReviewIssueItem(
                title:        "Menzil Açığı (Yetersiz Δv)",
                description:  "Daha yüksek Isp motor seçin.",
                reason:       "Net Δv yetersiz (12000 < 13500 m/s).",
                severity:     "Error",
                ruleId:       ValidationRuleId.DeltaV,
                hint:         "Daha yüksek Isp değerine sahip bir motor seçmeyi değerlendirin.",
                currentValue: 12000,
                targetValue:  13500
            );

            Assert.AreEqual("Menzil Açığı (Yetersiz Δv)", item.Title);
            Assert.AreEqual("Net Δv yetersiz (12000 < 13500 m/s).", item.Reason);
            Assert.AreEqual("Daha yüksek Isp değerine sahip bir motor seçmeyi değerlendirin.", item.Hint);
            Assert.AreEqual("Error", item.Severity);
            Assert.AreEqual(ValidationRuleId.DeltaV, item.RuleId);
            Assert.IsTrue(item.IsError);
            Assert.IsFalse(item.IsWarning);
            Assert.IsFalse(item.IsPass);
            Assert.AreEqual("ENGEL", item.SeverityBadgeText);
            Assert.AreEqual("⛔", item.SeveritySymbol);
        }

        [Test]
        public void ReviewViewModel_NavigationArgs_And_BackToWorkshopCommand()
        {
            var nav = new ScreenNavigator();
            var dummyWorkshop = new MockReviewScreen(ScreenId.Workshop);
            var dummyReview = new MockReviewScreen(ScreenId.DesignReview);

            nav.RegisterScreen(dummyWorkshop);
            nav.RegisterScreen(dummyReview);

            nav.GoTo(ScreenId.Workshop);
            Assert.AreEqual(ScreenId.Workshop, nav.CurrentScreenId);

            var defaultBp = WorkshopViewModel.CreateDefaultLunarRoverBlueprint();
            var context = new ValidationContext(
                missionProfile: new MissionProfile("moon-demo", requiredDeltaV: 10000, minReliabilityThreshold: 0.60)
            );
            var args = new ReviewNavigationArgs(defaultBp, context);

            // Navigate to Review screen passing args
            nav.GoTo(ScreenId.DesignReview, args);
            Assert.AreEqual(ScreenId.DesignReview, nav.CurrentScreenId);
            Assert.AreSame(args, dummyReview.LastContext);

            // ReviewViewModel evaluation with args
            var reviewVm = new ReviewViewModel(nav);
            reviewVm.RunValidation(args.Blueprint, args.Context);

            Assert.AreSame(defaultBp, reviewVm.ActiveBlueprint);
            Assert.AreEqual(7, reviewVm.ResultBars.Count);
            Assert.Greater(reviewVm.Issues.Count, 0);

            // Execute BackToWorkshopCommand
            reviewVm.BackToWorkshopCommand.Execute();
            Assert.AreEqual(ScreenId.Workshop, nav.CurrentScreenId);
        }

        [Test]
        public void WorkshopViewModel_GoToReviewCommand_TransfersBlueprintAndContext()
        {
            var nav = new ScreenNavigator();
            var dummyWorkshop = new MockReviewScreen(ScreenId.Workshop);
            var dummyReview = new MockReviewScreen(ScreenId.DesignReview);

            nav.RegisterScreen(dummyWorkshop);
            nav.RegisterScreen(dummyReview);

            var workshopVm = new WorkshopViewModel(nav);

            nav.GoTo(ScreenId.Workshop);
            Assert.AreEqual(ScreenId.Workshop, nav.CurrentScreenId);

            // Trigger GoToReviewCommand
            workshopVm.GoToReviewCommand.Execute();

            Assert.AreEqual(ScreenId.DesignReview, nav.CurrentScreenId);
            Assert.IsInstanceOf<ReviewNavigationArgs>(dummyReview.LastContext);

            var args = (ReviewNavigationArgs)dummyReview.LastContext;
            Assert.AreEqual(workshopVm.ActiveBlueprint.Id, args.Blueprint.Id);
            Assert.IsNotNull(args.Context);
            Assert.AreEqual(workshopVm.TargetDeltaV.Value, args.Context.MissionProfile.RequiredDeltaV);
        }

        private class MockReviewScreen : IScreenController
        {
            public ScreenId ScreenId { get; }
            public UnityEngine.UIElements.VisualElement RootElement => null;
            public bool IsVisible { get; private set; }
            public object LastContext { get; private set; }

            public MockReviewScreen(ScreenId id)
            {
                ScreenId = id;
            }

            public void Initialize(UnityEngine.UIElements.VisualElement parentContainer) { }

            public void OnEnter(object context = null)
            {
                IsVisible = true;
                LastContext = context;
            }

            public void OnExit()
            {
                IsVisible = false;
            }

            public void Dispose() { }
        }
    }
}
