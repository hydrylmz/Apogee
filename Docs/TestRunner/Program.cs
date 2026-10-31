using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Apogee.Core;
using Apogee.Data;
using Apogee.Design;
using Apogee.Design.Commands;
using Apogee.Strategy;
using Apogee.UI.Core.ViewModels;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using Apogee.UI.Screens.Comparison;
using Apogee.UI.Screens.Templates;
using Apogee.UI.Screens.Composer;
using UnityEngine.UIElements;

namespace ApogeeLocalTestRunner
{
    class Program
    {
        private static int _passed = 0;
        private static int _failed = 0;

        static int Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==================================================");
            Console.WriteLine("  APOGEE Core & Design Verification Tests");
            Console.WriteLine("==================================================");
            Console.ResetColor();

            // Run RNG Tests (M1-01)
            RunTest("SeededRng_DeterministicSequence", Test_SeededRng_DeterministicSequence);
            RunTest("SeededRng_RangeBounds", Test_SeededRng_RangeBounds);
            RunTest("SeededRng_StateSaveAndRestore", Test_SeededRng_StateSaveAndRestore);
            RunTest("RngManager_StreamIndependence", Test_RngManager_StreamIndependence);
            RunTest("RngManager_SaveAndRestore", Test_RngManager_SaveAndRestore);

            // Run Command Engine Tests (M1-02)
            RunTest("Command_AuthorizedExecution_SucceedsAndRecords", Test_Command_AuthorizedExecution);
            RunTest("Command_UnauthorizedRole_RejectedWithReason", Test_Command_UnauthorizedRole_Rejected);
            RunTest("Command_ValidationFailure_RejectedWithReason", Test_Command_ValidationFailure_Rejected);
            RunTest("Command_Undo_ReversesMutationsSymmetrically", Test_Command_Undo_ReversesMutations);
            RunTest("Command_UndoEmpty_FailsGracefully", Test_Command_UndoEmpty_Fails);
            RunTest("Command_Redo_ReAppliesMutations", Test_Command_Redo_ReApplies);
            RunTest("Command_NewActionAfterUndo_ClearsRedoHistory", Test_Command_NewActionAfterUndo_ClearsRedo);
            RunTest("Command_MultipleCommands_OperatesLIFO", Test_Command_MultipleCommands_OperatesLIFO);
            RunTest("Command_NonUndoable_BypassesUndoStack", Test_Command_NonUndoable_BypassesUndo);
            RunTest("Command_MaxHistorySize_TrimsOldest", Test_Command_MaxHistorySize_TrimsOldest);
            RunTest("Command_ClearHistory_EmptiesAll", Test_Command_ClearHistory_EmptiesAll);
            RunTest("Command_RoleAuthorization_MaskEvaluation", Test_Command_RoleAuthorization_MaskEvaluation);
            RunTest("Command_RecordAuditTrail_ContainsExpectedData", Test_Command_RecordAuditTrail);

            // Run Stat Model Tests (M1-03)
            RunTest("Stat_BaseValueOnly_ReturnsBaseValue", Test_Stat_BaseValueOnly);
            RunTest("Stat_FlatModifiers_AddDirectlyToBase", Test_Stat_FlatModifiers);
            RunTest("Stat_PercentAddModifiers_SumAdditivelyBeforeMultiplying", Test_Stat_PercentAddModifiers);
            RunTest("Stat_PercentMultModifiers_CompoundMultiplicatively", Test_Stat_PercentMultModifiers);
            RunTest("Stat_FullPipeline_CalculatesInExactEvaluationOrder", Test_Stat_FullPipeline);
            RunTest("Stat_Clamping_ConstrainsToMinAndMax", Test_Stat_Clamping);
            RunTest("Stat_RemoveModifier_RemovesEffectAndRecalculates", Test_Stat_RemoveModifier);
            RunTest("Stat_RemoveModifiersFromSource_RemovesAllMatchingSources", Test_Stat_RemoveModifiersFromSource);
            RunTest("StatBreakdown_AccuratelyTracksNetContributions", Test_StatBreakdown_AccuratelyTracksNetContributions);
            RunTest("StatCollection_GetOrCreateAndBatchModifiers_FunctionAccurately", Test_StatCollection_GetOrCreateAndBatchModifiers);
            RunTest("StatCollection_Events_FiresWhenStatModified", Test_StatCollection_Events);

            // Run UI Architecture & Navigation Tests (M2-01)
            RunTest("UI_ViewModel_ObservableProperty_Notification", Test_UI_ViewModel_ObservableProperty_Notification);
            RunTest("UI_ViewModel_RegisterDisposable_Lifecycle", Test_UI_ViewModel_RegisterDisposable_Lifecycle);
            RunTest("UI_ObservableValue_SubscribeAndUnsubscribe", Test_UI_ObservableValue_SubscribeAndUnsubscribe);
            RunTest("UI_ViewActionCommand_ExecutionAndCanExecute", Test_UI_ViewActionCommand_ExecutionAndCanExecute);
            RunTest("UI_ScreenNavigator_ScreenTransitionsAndHistory", Test_UI_ScreenNavigator_ScreenTransitionsAndHistory);
            RunTest("UI_ScreenNavigator_BackStack_Popping", Test_UI_ScreenNavigator_BackStack_Popping);
            RunTest("UI_TooltipData_And_TooltipService_Events", Test_UI_TooltipData_And_TooltipService_Events);

            // Run Module-Slot & Ghost Delta Tests (M2-02)
            RunTest("Slot_Definitions_MetadataValid", Test_Slot_Definitions_MetadataValid);
            RunTest("Blueprint_WithSlotComponent_And_GetComponentInSlot", Test_Blueprint_WithSlotComponent_And_GetComponentInSlot);
            RunTest("Command_SetSlotComponent_ExecutesAndUndoes_ViaCommandBus", Test_Command_SetSlotComponent_ExecutesAndUndoes_ViaCommandBus);
            RunTest("Command_SetSlotComponent_RoleAuthorization_RejectsUnauthorized", Test_Command_SetSlotComponent_RoleAuthorization_RejectsUnauthorized);
            RunTest("Design_GhostDelta_ComputesDifferentialAndSummary", Test_Design_GhostDelta_ComputesDifferentialAndSummary);
            RunTest("UI_VehicleSlotViewModel_EquippedStateAndSelection", Test_UI_VehicleSlotViewModel_EquippedStateAndSelection);
            RunTest("UI_ModuleOptionViewModel_LockedAndHoverCommands", Test_UI_ModuleOptionViewModel_LockedAndHoverCommands);
            RunTest("UI_GhostDeltaViewModel_ApplyAndClear", Test_UI_GhostDeltaViewModel_ApplyAndClear);
            RunTest("UI_WorkshopViewModel_SlotSelection_PopulatesCompatibleOptions", Test_UI_WorkshopViewModel_SlotSelection_PopulatesCompatibleOptions);
            RunTest("UI_WorkshopViewModel_HoverAndUnhover_TogglesGhostDelta", Test_UI_WorkshopViewModel_HoverAndUnhover_TogglesGhostDelta);
            RunTest("UI_WorkshopViewModel_EquipOption_ExecutesCommand_UndoRedo", Test_UI_WorkshopViewModel_EquipOption_ExecutesCommand_UndoRedo);

            // Run Cross-Section 2D CAD Assembly Tests (M2-03)
            RunTest("CrossSection_ThreeStageRocketAutoAssembledAndSnapped", Test_CrossSection_ThreeStageRocketAutoAssembledAndSnapped);
            RunTest("CrossSection_EmptySlotHandling", Test_CrossSection_EmptySlotHandling);
            RunTest("CrossSection_SupplierColorResolution", Test_CrossSection_SupplierColorResolution);
            RunTest("UI_CrossSectionViewModel_LayoutAndTwoWaySync", Test_UI_CrossSectionViewModel_LayoutAndTwoWaySync);
            RunTest("UI_WorkshopViewModel_CrossSection_LiveIntegration", Test_UI_WorkshopViewModel_CrossSection_LiveIntegration);

            // Run Results Panel & Sade Sunum Tests (M2-04)
            RunTest("ResultsPanel_ResultBarViewModel_ClampingAndStatuses", Test_ResultsPanel_ResultBarViewModel_ClampingAndStatuses);
            RunTest("ResultsPanel_BuildSevenBars_AllKeysAndValuesPresent", Test_ResultsPanel_BuildSevenBars_AllKeysAndValuesPresent);
            RunTest("ResultsPanel_Thresholds_GoodWarnErrorMapping", Test_ResultsPanel_Thresholds_GoodWarnErrorMapping);
            RunTest("ResultsPanel_ValidationReportIntegration_ReflectsValidatorFindings", Test_ResultsPanel_ValidationReportIntegration_ReflectsValidatorFindings);
            RunTest("UI_ReviewViewModel_ValidationPipelineAndBars", Test_UI_ReviewViewModel_ValidationPipelineAndBars);

            // Run Design Review Screen & Traffic-Light Issues Tests (M2-05)
            RunTest("UI_ReviewViewModel_TrafficLightCounts_And_Filters", Test_UI_ReviewViewModel_TrafficLightCounts_And_Filters);
            RunTest("UI_ReviewIssueItem_NedenAndHint_Integrity", Test_UI_ReviewIssueItem_NedenAndHint_Integrity);
            RunTest("UI_ReviewViewModel_NavigationArgs_And_BackToWorkshopCommand", Test_UI_ReviewViewModel_NavigationArgs_And_BackToWorkshopCommand);
            RunTest("UI_WorkshopViewModel_GoToReviewCommand_TransfersBlueprintAndContext", Test_UI_WorkshopViewModel_GoToReviewCommand_TransfersBlueprintAndContext);

            // Run Templates, Comparison & Mission Code Tests (M2-06)
            RunTest("Template_DesignTemplate_CreateAndClone", Test_Template_DesignTemplate_CreateAndClone);
            RunTest("Template_InMemoryRepository_SaveGetDeleteContains", Test_Template_Repository_CRUD);
            RunTest("Template_Repository_Duplicate_And_Rename", Test_Template_Repository_DuplicateAndRename);
            RunTest("Template_Repository_OnChanged_Fires", Test_Template_Repository_OnChanged_Fires);
            RunTest("Template_Command_Save_And_Undo", Test_Template_Command_SaveAndUndo);
            RunTest("Template_Command_Delete_And_Undo", Test_Template_Command_DeleteAndUndo);
            RunTest("Template_Command_Rename_And_Undo", Test_Template_Command_RenameAndUndo);
            RunTest("Template_Command_Duplicate_And_Undo", Test_Template_Command_DuplicateAndUndo);
            RunTest("Template_Command_AuthorizationCheck", Test_Template_Command_AuthorizationCheck);
            RunTest("MissionCode_Encode_And_Decode_RoundTrip", Test_MissionCode_Encode_Decode_RoundTrip);
            RunTest("MissionCode_InvalidCode_Returns_Error", Test_MissionCode_InvalidCode);
            RunTest("BlueprintDiff_StatComparisons_CorrectValues", Test_BlueprintDiff_StatComparisons);
            RunTest("BlueprintDiff_SlotComparisons_CorrectStatus", Test_BlueprintDiff_SlotComparisons);
            RunTest("UI_ComparisonViewModel_ApplyComparison_PopulatesData", Test_UI_ComparisonViewModel_Apply);
            RunTest("UI_TemplateManagerViewModel_SaveAndRefresh", Test_UI_TemplateManagerViewModel_SaveAndRefresh);

            // Run Vertical Slice Content Tests (M2-07: Moon + Rover, ~12 Modules, 3 Engines, 3 Materials, 1 Supplier)
            RunTest("VerticalSlice_MoonDestination_CatalogIntegrity", Test_VerticalSlice_MoonDestination_CatalogIntegrity);
            RunTest("VerticalSlice_RoverMissionType_CatalogIntegrity", Test_VerticalSlice_RoverMissionType_CatalogIntegrity);
            RunTest("VerticalSlice_Materials_CatalogIntegrity", Test_VerticalSlice_Materials_CatalogIntegrity);
            RunTest("VerticalSlice_ModulesAndEngines_Counts", Test_VerticalSlice_ModulesAndEngines_Counts);
            RunTest("VerticalSlice_HelionSupplier_Specs", Test_VerticalSlice_HelionSupplier_Specs);
            RunTest("VerticalSlice_MissingEdlBlueprint_FailsReview_WithMandatoryModuleError", Test_VerticalSlice_MissingEdlBlueprint_FailsReview);
            RunTest("VerticalSlice_EquippingEdl_ConvertsToPassingReview", Test_VerticalSlice_EquippingEdl_ConvertsToPassingReview);
            RunTest("VerticalSlice_PassingBlueprint_AllEightValidatorsPass", Test_VerticalSlice_PassingBlueprint_AllEightValidatorsPass);
            RunTest("VerticalSlice_MaterialSwitch_ChangesMassAndDeltaV", Test_VerticalSlice_MaterialSwitch_ChangesMassAndDeltaV);
            RunTest("VerticalSlice_Workshop_GoToReview_PassesValidLunarRoverContext", Test_VerticalSlice_Workshop_GoToReview_PassesValidLunarRoverContext);

            // Run Mission Composer catalog tests (M3-01: destinations, types, and objectives)
            RunTest("MissionComposer_ObjectivesCatalog_LoadsAllTargetsWithCompleteRequirements", Test_MissionComposer_ObjectivesCatalog_LoadsAllTargetsWithCompleteRequirements);

            // Run Mission Composer Core engine tests (M3-02)
            RunTest("MissionComposer_MarsCrewed_Vs_MarsRover_DistinctlyDifferentOutputs", Test_MissionComposer_MarsCrewed_Vs_MarsRover_DistinctlyDifferentOutputs);
            RunTest("MissionComposer_LeoOrbiter_DerivesBaselineRequirements", Test_MissionComposer_LeoOrbiter_DerivesBaselineRequirements);
            RunTest("MissionComposer_MoonLander_WithObjectives_DerivesCompleteProfile", Test_MissionComposer_MoonLander_WithObjectives_DerivesCompleteProfile);
            RunTest("MissionComposer_EuropaOrbiter_DerivesExtremeEnvironmentAndResearch", Test_MissionComposer_EuropaOrbiter_DerivesExtremeEnvironmentAndResearch);
            RunTest("MissionComposer_SampleReturn_RoundTripDeltaVAndCriteria", Test_MissionComposer_SampleReturn_RoundTripDeltaVAndCriteria);

            // Run Difficulty Calculator and Claim Level tests (M3-03)
            RunTest("Difficulty_ClaimLevel_BelowFive_IsLow", Test_Difficulty_ClaimLevel_BelowFive_IsLow);
            RunTest("Difficulty_ClaimLevel_AtFive_IsMedium", Test_Difficulty_ClaimLevel_AtFive_IsMedium);
            RunTest("Difficulty_ClaimLevel_AtThirteen_IsHigh", Test_Difficulty_ClaimLevel_AtThirteen_IsHigh);
            RunTest("Difficulty_ClaimLevel_AtTwentyFive_IsExtreme", Test_Difficulty_ClaimLevel_AtTwentyFive_IsExtreme);
            RunTest("Difficulty_DestinationMultiplier_LEO_IsOne", Test_Difficulty_DestinationMultiplier_LEO);
            RunTest("Difficulty_DestinationMultiplier_Mars_IsTwo", Test_Difficulty_DestinationMultiplier_Mars);
            RunTest("Difficulty_DestinationMultiplier_Europa_IsTwoPointFive", Test_Difficulty_DestinationMultiplier_Europa);
            RunTest("Difficulty_LEOOrbiter_NoObjectives_IsLow", Test_Difficulty_LEOOrbiter_NoObjectives_IsLow);
            RunTest("Difficulty_MarsRover_OneObjective_YieldsHigh", Test_Difficulty_MarsRover_OneObjective_YieldsHigh);
            RunTest("Difficulty_MarsCrewed_TwoObjectives_YieldsExtreme", Test_Difficulty_MarsCrewed_TwoObjectives_YieldsExtreme);
            RunTest("Difficulty_EuropaOrbiter_YieldsHigherMultipliersThanLEO", Test_Difficulty_EuropaOrbiter_HigherMultipliers);
            RunTest("Difficulty_RewardScalesMonotonically_WithDifficulty", Test_Difficulty_RewardScalesMonotonically);
            RunTest("Difficulty_RiskScalesMonotonically_WithDifficulty", Test_Difficulty_RiskScalesMonotonically);
            RunTest("Difficulty_SummaryStrings_AreNonEmpty", Test_Difficulty_SummaryStrings_NonEmpty);
            RunTest("Difficulty_Europa_RiskSummary_ContainsRadiation", Test_Difficulty_Europa_RiskSummary_ContainsRadiation);
            RunTest("Difficulty_Compose_Profile_CarriesClaimLevelAndMultipliers", Test_Difficulty_Compose_Profile_CarriesFullEval);
            RunTest("Difficulty_Compose_LEO_IsLowAndConsistent", Test_Difficulty_Compose_LEO_IsLowAndConsistent);
            RunTest("Difficulty_Compose_ChangesWhenObjectivesAdded", Test_Difficulty_Compose_ChangesWhenObjectivesAdded);
            RunTest("Difficulty_Formula_MoonRover2Obj_MatchesExpected", Test_Difficulty_Formula_MoonRover2Obj);

            // Run Claim ViewModel & UI tests (M3-03 Presentation)
            RunTest("UI_ClaimViewModel_InitialState_DefaultsToLow", Test_UI_ClaimViewModel_InitialState);
            RunTest("UI_ClaimViewModel_SetDestination_DynamicallyUpdates", Test_UI_ClaimViewModel_SetDestination);
            RunTest("UI_ClaimViewModel_SetMissionType_DynamicallyUpdatesMultipliers", Test_UI_ClaimViewModel_SetMissionType);
            RunTest("UI_ClaimViewModel_ToggleObjective_ReactivelyUpdatesScoreAndLevel", Test_UI_ClaimViewModel_ToggleObjective);
            RunTest("UI_ClaimViewModel_FullReactiveLifecycle_AndClear", Test_UI_ClaimViewModel_FullReactiveLifecycle);

            // Run Mission Composer UI Wizard Tests (M3-04)
            RunTest("UI_ComposerViewModel_InitialState_Defaults", Test_UI_ComposerViewModel_InitialState_Defaults);
            RunTest("UI_ComposerViewModel_StepNavigation_ForwardAndBackward", Test_UI_ComposerViewModel_StepNavigation_ForwardAndBackward);
            RunTest("UI_ComposerViewModel_SelectDestination_DerivesEstimates", Test_UI_ComposerViewModel_SelectDestination_DerivesEstimates);
            RunTest("UI_ComposerViewModel_SelectMissionType_CrewedVsRover", Test_UI_ComposerViewModel_SelectMissionType_CrewedVsRover);
            RunTest("UI_ComposerViewModel_Objectives_FilteringAndPruning", Test_UI_ComposerViewModel_Objectives_FilteringAndPruning);
            RunTest("UI_ComposerViewModel_Objectives_SelectAllAndClear", Test_UI_ComposerViewModel_Objectives_SelectAllAndClear);
            RunTest("UI_ComposerViewModel_ScheduleAndBudget_CustomSettings", Test_UI_ComposerViewModel_ScheduleAndBudget_CustomSettings);
            RunTest("UI_ComposerViewModel_ComposeMissionProfile_ProducesValidProfile", Test_UI_ComposerViewModel_ComposeMissionProfile_ProducesValidProfile);
            RunTest("UI_ComposerViewModel_ComposeMissionProfile_ThrowsWhenIncomplete", Test_UI_ComposerViewModel_ComposeMissionProfile_ThrowsWhenIncomplete);
            RunTest("UI_ComposerViewModel_NavigationIntegration_DispatchesArgs", Test_UI_ComposerViewModel_NavigationIntegration_DispatchesArgs);

            Console.WriteLine("\n--------------------------------------------------");
            if (_failed == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"ALL {_passed} VERIFICATION TESTS PASSED SUCCESSFULLY (0 Failed).");
                Console.ResetColor();
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"TESTS FAILED: {_passed} Passed, {_failed} Failed.");
                Console.ResetColor();
                return 1;
            }
        }

        static void RunTest(string testName, Action testAction)
        {
            try
            {
                testAction();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  [PASS] {testName}");
                Console.ResetColor();
                _passed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [FAIL] {testName}: {ex.Message}");
                Console.ResetColor();
                _failed++;
            }
        }

        static void AssertEqual<T>(T expected, T actual, string message = "")
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception($"Expected '{expected}' but got '{actual}'. {message}");
            }
        }

        static void AssertApproxEqual(double expected, double actual, double tolerance = 1e-6, string message = "")
        {
            if (Math.Abs(expected - actual) > tolerance)
            {
                throw new Exception($"Expected '{expected:0.######}' but got '{actual:0.######}'. {message}");
            }
        }

        static void AssertTrue(bool condition, string message = "")
        {
            if (!condition) throw new Exception($"Expected condition to be true. {message}");
        }

        static void AssertFalse(bool condition, string message = "")
        {
            if (condition) throw new Exception($"Expected condition to be false. {message}");
        }

        // Test state & command helpers
        class TestState
        {
            public int Value { get; set; }
            public List<string> History { get; } = new List<string>();
        }

        class AddValueCommand : CommandBase<TestState>
        {
            private readonly int _delta;
            public override PlayerRole RequiredRoles { get; }

            public AddValueCommand(int delta, PlayerRole requiredRoles = PlayerRole.All)
            {
                _delta = delta;
                RequiredRoles = requiredRoles;
            }

            protected override CommandValidationResult CanExecute(TestState state, CommandContext context)
            {
                if (_delta == 0) return CommandValidationResult.Fail("Delta cannot be zero.");
                return CommandValidationResult.Success();
            }

            protected override CommandExecutionResult Execute(TestState state, CommandContext context)
            {
                state.Value += _delta;
                state.History.Add($"+{_delta}");
                return CommandExecutionResult.Succeeded($"Value is now {state.Value}");
            }

            protected override CommandExecutionResult Undo(TestState state, CommandContext context)
            {
                state.Value -= _delta;
                state.History.Add($"-{_delta}");
                return CommandExecutionResult.Succeeded($"Reverted value to {state.Value}");
            }
        }

        class PermanentLogCommand : CommandBase<TestState>
        {
            private readonly string _msg;
            public override bool CanUndo => false;

            public PermanentLogCommand(string msg) => _msg = msg;

            protected override CommandValidationResult CanExecute(TestState state, CommandContext context) =>
                CommandValidationResult.Success();

            protected override CommandExecutionResult Execute(TestState state, CommandContext context)
            {
                state.History.Add(_msg);
                return CommandExecutionResult.Succeeded();
            }
        }

        static void Test_SeededRng_DeterministicSequence()
        {
            var rng1 = new SeededRng(12345UL);
            var rng2 = new SeededRng(12345UL);
            for (int i = 0; i < 100; i++)
            {
                AssertEqual(rng1.NextUInt64(), rng2.NextUInt64());
            }
        }

        static void Test_SeededRng_RangeBounds()
        {
            var rng = new SeededRng(54321UL);
            for (int i = 0; i < 100; i++)
            {
                int val = rng.NextInt(10, 20);
                AssertTrue(val >= 10 && val < 20);
                float f = rng.NextFloat();
                AssertTrue(f >= 0.0f && f < 1.0f);
            }
        }

        static void Test_SeededRng_StateSaveAndRestore()
        {
            var rng = new SeededRng(9999UL);
            for (int i = 0; i < 50; i++) rng.NextUInt64();

            RngState saved = rng.GetState();
            ulong n1 = rng.NextUInt64();
            ulong n2 = rng.NextUInt64();

            rng.SetState(saved);
            AssertEqual(n1, rng.NextUInt64());
            AssertEqual(n2, rng.NextUInt64());
        }

        static void Test_RngManager_StreamIndependence()
        {
            var mgr1 = new RngManager(42UL);
            var mgr2 = new RngManager(42UL);

            IRandom events1 = mgr1.GetStream(RngStreamType.Events);
            for (int i = 0; i < 200; i++) events1.NextInt();

            IRandom launch1 = mgr1.GetStream(RngStreamType.Launch);
            IRandom launch2 = mgr2.GetStream(RngStreamType.Launch);
            for (int i = 0; i < 50; i++)
            {
                AssertEqual(launch1.NextUInt64(), launch2.NextUInt64());
            }
        }

        static void Test_RngManager_SaveAndRestore()
        {
            var mgr = new RngManager(1001UL);
            mgr.GetStream(RngStreamType.Events).NextInt();
            mgr.GetStream(RngStreamType.Launch).NextInt();

            RngManagerState state = mgr.GetState();
            var mgrRestored = new RngManager(state);

            AssertEqual(mgr.GetStream(RngStreamType.Launch).NextUInt64(), mgrRestored.GetStream(RngStreamType.Launch).NextUInt64());
        }

        static void Test_Command_AuthorizedExecution()
        {
            var bus = new CommandBus();
            var state = new TestState();
            var ctx = new CommandContext("p1", PlayerRole.LeadEngineer, tick: 5, state: state);
            var cmd = new AddValueCommand(10, PlayerRole.LeadEngineer);

            var res = bus.Execute(cmd, ctx);
            AssertTrue(res.Success);
            AssertEqual(10, state.Value);
            AssertEqual(1, bus.UndoCount);
            AssertEqual(1, bus.History.Count);
        }

        static void Test_Command_UnauthorizedRole_Rejected()
        {
            var bus = new CommandBus();
            var state = new TestState();
            var ctx = new CommandContext("p2", PlayerRole.ScienceDirector, tick: 5, state: state);
            var cmd = new AddValueCommand(10, PlayerRole.AgencyDirector);

            var res = bus.Execute(cmd, ctx);
            AssertFalse(res.Success);
            AssertEqual("UNAUTHORIZED", res.ErrorCode);
            AssertEqual(0, state.Value);
            AssertEqual(0, bus.UndoCount);
        }

        static void Test_Command_ValidationFailure_Rejected()
        {
            var bus = new CommandBus();
            var state = new TestState();
            var ctx = CommandContext.CreateLocalAdmin(state);
            var cmd = new AddValueCommand(0);

            var res = bus.Execute(cmd, ctx);
            AssertFalse(res.Success);
            AssertEqual("VALIDATION_FAILED", res.ErrorCode);
            AssertEqual(0, state.Value);
        }

        static void Test_Command_Undo_ReversesMutations()
        {
            var bus = new CommandBus();
            var state = new TestState();
            var ctx = CommandContext.CreateLocalAdmin(state);

            bus.Execute(new AddValueCommand(15), ctx);
            AssertEqual(15, state.Value);

            var undoRes = bus.Undo(ctx);
            AssertTrue(undoRes.Success);
            AssertEqual(0, state.Value);
            AssertEqual(0, bus.UndoCount);
            AssertEqual(1, bus.RedoCount);
        }

        static void Test_Command_UndoEmpty_Fails()
        {
            var bus = new CommandBus();
            var ctx = CommandContext.CreateLocalAdmin(new TestState());
            var res = bus.Undo(ctx);
            AssertFalse(res.Success);
            AssertEqual("STACK_EMPTY", res.ErrorCode);
        }

        static void Test_Command_Redo_ReApplies()
        {
            var bus = new CommandBus();
            var state = new TestState();
            var ctx = CommandContext.CreateLocalAdmin(state);

            bus.Execute(new AddValueCommand(25), ctx);
            bus.Undo(ctx);
            AssertEqual(0, state.Value);

            var redoRes = bus.Redo(ctx);
            AssertTrue(redoRes.Success);
            AssertEqual(25, state.Value);
            AssertEqual(1, bus.UndoCount);
            AssertEqual(0, bus.RedoCount);
        }

        static void Test_Command_NewActionAfterUndo_ClearsRedo()
        {
            var bus = new CommandBus();
            var state = new TestState();
            var ctx = CommandContext.CreateLocalAdmin(state);

            bus.Execute(new AddValueCommand(10), ctx);
            bus.Undo(ctx);
            AssertEqual(1, bus.RedoCount);

            bus.Execute(new AddValueCommand(50), ctx);
            AssertEqual(0, bus.RedoCount);
            AssertEqual(50, state.Value);
        }

        static void Test_Command_MultipleCommands_OperatesLIFO()
        {
            var bus = new CommandBus();
            var state = new TestState();
            var ctx = CommandContext.CreateLocalAdmin(state);

            bus.Execute(new AddValueCommand(1), ctx);
            bus.Execute(new AddValueCommand(2), ctx);
            bus.Execute(new AddValueCommand(3), ctx);
            AssertEqual(6, state.Value);

            bus.Undo(ctx);
            AssertEqual(3, state.Value);
            bus.Undo(ctx);
            AssertEqual(1, state.Value);
            bus.Redo(ctx);
            AssertEqual(3, state.Value);
            bus.Undo(ctx);
            AssertEqual(1, state.Value);
            bus.Undo(ctx);
            AssertEqual(0, state.Value);
        }

        static void Test_Command_NonUndoable_BypassesUndo()
        {
            var bus = new CommandBus();
            var state = new TestState();
            var ctx = CommandContext.CreateLocalAdmin(state);

            var res = bus.Execute(new PermanentLogCommand("Boot sequence"), ctx);
            AssertTrue(res.Success);
            AssertEqual(0, bus.UndoCount);
            AssertEqual(1, bus.History.Count);
        }

        static void Test_Command_MaxHistorySize_TrimsOldest()
        {
            var bus = new CommandBus(maxHistorySize: 3);
            var state = new TestState();
            var ctx = CommandContext.CreateLocalAdmin(state);

            for (int i = 1; i <= 5; i++)
            {
                bus.Execute(new AddValueCommand(i), ctx);
            }

            AssertEqual(3, bus.UndoCount);
            bus.Undo(ctx);
            bus.Undo(ctx);
            bus.Undo(ctx);
            AssertEqual(0, bus.UndoCount);
            AssertEqual(3, state.Value);
        }

        static void Test_Command_ClearHistory_EmptiesAll()
        {
            var bus = new CommandBus();
            var state = new TestState();
            var ctx = CommandContext.CreateLocalAdmin(state);

            bus.Execute(new AddValueCommand(10), ctx);
            bus.Undo(ctx);
            bus.ClearHistory();

            AssertEqual(0, bus.UndoCount);
            AssertEqual(0, bus.RedoCount);
            AssertEqual(0, bus.History.Count);
            AssertFalse(bus.CanUndo);
            AssertFalse(bus.CanRedo);
        }

        static void Test_Command_RoleAuthorization_MaskEvaluation()
        {
            AssertTrue(CommandBus.IsAuthorized(PlayerRole.All, PlayerRole.AgencyDirector));
            AssertTrue(CommandBus.IsAuthorized(PlayerRole.LeadEngineer, PlayerRole.LeadEngineer));
            AssertFalse(CommandBus.IsAuthorized(PlayerRole.ScienceDirector, PlayerRole.LeadEngineer));

            PlayerRole combo = PlayerRole.AgencyDirector | PlayerRole.LeadEngineer;
            AssertTrue(CommandBus.IsAuthorized(PlayerRole.AgencyDirector, combo));
            AssertTrue(CommandBus.IsAuthorized(PlayerRole.LeadEngineer, combo));
            AssertFalse(CommandBus.IsAuthorized(PlayerRole.ProcurementDirector, combo));

            AssertTrue(CommandBus.IsAuthorized(PlayerRole.None, PlayerRole.None));
            AssertTrue(CommandBus.IsAuthorized(PlayerRole.ProcurementDirector, PlayerRole.None));
        }

        static void Test_Command_RecordAuditTrail()
        {
            var bus = new CommandBus();
            var state = new TestState();
            var ctx = new CommandContext("commander-shepard", PlayerRole.AgencyDirector, tick: 777, state: state);
            var cmd = new AddValueCommand(100, PlayerRole.AgencyDirector);

            bus.Execute(cmd, ctx);
            AssertEqual(1, bus.History.Count);

            CommandRecord r = bus.History[0];
            AssertEqual(1UL, r.SequenceNumber);
            AssertEqual(cmd.Id, r.CommandId);
            AssertEqual("AddValueCommand", r.CommandType);
            AssertEqual(PlayerRole.AgencyDirector, r.RequiredRoles);
            AssertEqual(PlayerRole.AgencyDirector, r.ActorRole);
            AssertEqual("commander-shepard", r.PlayerId);
            AssertEqual(777L, r.Tick);
        }

        // --- M1-03 Stat Tests ---

        static void Test_Stat_BaseValueOnly()
        {
            var stat = new Stat(StatType.DryMass, baseValue: 10.0);
            AssertApproxEqual(10.0, stat.Value);
        }

        static void Test_Stat_FlatModifiers()
        {
            var stat = new Stat(StatType.DryMass, baseValue: 10.0);
            stat.AddModifier(StatModifier.CreateFlat(StatType.DryMass, 2.5, sourceName: "Fairing"));
            stat.AddModifier(StatModifier.CreateFlat(StatType.DryMass, -1.0, sourceName: "Lightweight Adapter"));
            AssertApproxEqual(11.5, stat.Value);
        }

        static void Test_Stat_PercentAddModifiers()
        {
            var stat = new Stat(StatType.SpecificImpulse, baseValue: 100.0);
            stat.AddModifier(StatModifier.CreatePercentAdd(StatType.SpecificImpulse, 0.10, sourceName: "Tech A"));
            stat.AddModifier(StatModifier.CreatePercentAdd(StatType.SpecificImpulse, 0.20, sourceName: "Tech B"));
            AssertApproxEqual(130.0, stat.Value);
        }

        static void Test_Stat_PercentMultModifiers()
        {
            var stat = new Stat(StatType.Reliability, baseValue: 1.0);
            stat.AddModifier(StatModifier.CreatePercentMult(StatType.Reliability, -0.20, sourceName: "Radiation"));
            stat.AddModifier(StatModifier.CreatePercentMult(StatType.Reliability, -0.10, sourceName: "Dust Storm"));
            AssertApproxEqual(0.72, stat.Value);
        }

        static void Test_Stat_FullPipeline()
        {
            // Base = 10, Flat = +2 -> 12, PercentAdd = +50% -> 18, PercentMult = -10% -> 16.2
            var stat = new Stat(StatType.DryMass, baseValue: 10.0);
            stat.AddModifier(StatModifier.CreatePercentMult(StatType.DryMass, -0.10, sourceName: "Optimization"));
            stat.AddModifier(StatModifier.CreateFlat(StatType.DryMass, 2.0, sourceName: "Structure"));
            stat.AddModifier(StatModifier.CreatePercentAdd(StatType.DryMass, 0.50, sourceName: "Avionics"));
            AssertApproxEqual(16.2, stat.Value);
        }

        static void Test_Stat_Clamping()
        {
            var stat = new Stat(StatType.Reliability, baseValue: 0.95, minValue: 0.0, maxValue: 1.0);
            stat.AddModifier(StatModifier.CreateFlat(StatType.Reliability, 0.20));
            AssertApproxEqual(1.0, stat.Value);

            stat.ClearModifiers();
            stat.AddModifier(StatModifier.CreateFlat(StatType.Reliability, -1.5));
            AssertApproxEqual(0.0, stat.Value);
        }

        static void Test_Stat_RemoveModifier()
        {
            var stat = new Stat(StatType.Cost, baseValue: 50.0);
            var mod = StatModifier.CreateFlat(StatType.Cost, 15.0, sourceName: "Sensor");
            stat.AddModifier(mod);
            AssertApproxEqual(65.0, stat.Value);

            bool removed = stat.RemoveModifier(mod.Id);
            AssertTrue(removed);
            AssertApproxEqual(50.0, stat.Value);
        }

        static void Test_Stat_RemoveModifiersFromSource()
        {
            var stat = new Stat(StatType.DryMass, baseValue: 20.0);
            string supplier = "Helion Dynamics";
            stat.AddModifier(StatModifier.CreateFlat(StatType.DryMass, 3.0, source: supplier));
            stat.AddModifier(StatModifier.CreatePercentAdd(StatType.DryMass, 0.05, source: supplier));
            stat.AddModifier(StatModifier.CreateFlat(StatType.DryMass, 1.0, source: "Nordvik"));

            int count = stat.RemoveModifiersFromSource(supplier);
            AssertEqual(2, count);
            AssertApproxEqual(21.0, stat.Value);
        }

        static void Test_StatBreakdown_AccuratelyTracksNetContributions()
        {
            var stat = new Stat(StatType.Thrust, baseValue: 100.0);
            stat.AddModifier(StatModifier.CreateFlat(StatType.Thrust, 20.0, sourceName: "Nozzle Upgrade"));
            stat.AddModifier(StatModifier.CreatePercentAdd(StatType.Thrust, 0.10, sourceName: "Supplier Calibration"));
            stat.AddModifier(StatModifier.CreatePercentMult(StatType.Thrust, -0.10, sourceName: "Backpressure"));

            StatBreakdown breakdown = stat.GetBreakdown();
            AssertApproxEqual(100.0, breakdown.BaseValue);
            AssertApproxEqual(118.8, breakdown.FinalValue);
            AssertEqual(3, breakdown.Entries.Count);

            AssertEqual("Nozzle Upgrade", breakdown.Entries[0].SourceName);
            AssertApproxEqual(20.0, breakdown.Entries[0].NetContribution);

            AssertEqual("Supplier Calibration", breakdown.Entries[1].SourceName);
            AssertApproxEqual(12.0, breakdown.Entries[1].NetContribution);

            AssertEqual("Backpressure", breakdown.Entries[2].SourceName);
            AssertApproxEqual(-13.2, breakdown.Entries[2].NetContribution);
        }

        static void Test_StatCollection_GetOrCreateAndBatchModifiers()
        {
            var collection = new StatCollection();
            collection.SetBaseValue(StatType.DryMass, 10.0);
            collection.SetBaseValue(StatType.Cost, 25.0);

            var modifiers = new[]
            {
                StatModifier.CreateFlat(StatType.DryMass, 1.5, sourceName: "Armor"),
                StatModifier.CreatePercentAdd(StatType.Cost, 0.20, sourceName: "Contract Overhead")
            };

            collection.AddModifiers(modifiers);
            AssertApproxEqual(11.5, collection.GetValue(StatType.DryMass));
            AssertApproxEqual(30.0, collection.GetValue(StatType.Cost));
        }

        static void Test_StatCollection_Events()
        {
            var collection = new StatCollection();
            bool eventFired = false;
            StatType changedType = StatType.Custom;

            collection.OnStatChanged += (type, stat) =>
            {
                eventFired = true;
                changedType = type;
            };

            collection.SetBaseValue(StatType.PowerGeneration, 5.0);
            AssertTrue(eventFired);
            AssertEqual(StatType.PowerGeneration, changedType);
        }

        // --- UI Tests (M2-01) ---

        private class DummyViewModel : ViewModelBase
        {
            private string _title;
            public string Title
            {
                get => _title;
                set => SetProperty(ref _title, value);
            }
        }

        private class DummyDisposable : IDisposable
        {
            public bool Disposed { get; private set; }
            public void Dispose() => Disposed = true;
        }

        private class DummyScreen : IScreenController
        {
            public ScreenId ScreenId { get; }
            public VisualElement RootElement => null;
            public bool IsVisible { get; private set; }
            public int EnterCount { get; private set; }
            public object LastContext { get; private set; }

            public DummyScreen(ScreenId id) => ScreenId = id;

            public void Initialize(VisualElement parent) { }
            public void OnEnter(object context = null)
            {
                IsVisible = true;
                EnterCount++;
                LastContext = context;
            }
            public void OnExit() => IsVisible = false;
            public void Dispose() { }
        }

        static void Test_UI_ViewModel_ObservableProperty_Notification()
        {
            var vm = new DummyViewModel();
            string changedProperty = null;
            vm.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

            vm.Title = "Apollo";
            AssertEqual(nameof(DummyViewModel.Title), changedProperty);

            changedProperty = null;
            vm.Title = "Apollo"; // same value, should not fire
            AssertEqual(null, changedProperty);
        }

        static void Test_UI_ViewModel_RegisterDisposable_Lifecycle()
        {
            var vm = new DummyViewModel();
            var disp = new DummyDisposable();
            vm.RegisterDisposable(disp);
            AssertEqual(false, disp.Disposed);

            vm.Dispose();
            AssertEqual(true, disp.Disposed);
        }

        static void Test_UI_ObservableValue_SubscribeAndUnsubscribe()
        {
            var obs = new ObservableValue<int>(10);
            var values = new List<int>();

            var sub = obs.Subscribe(v => values.Add(v), notifyImmediately: true);
            obs.Value = 25;
            sub.Dispose();
            obs.Value = 50;

            AssertEqual(2, values.Count);
            AssertEqual(10, values[0]);
            AssertEqual(25, values[1]);
        }

        static void Test_UI_ViewActionCommand_ExecutionAndCanExecute()
        {
            bool executed = false;
            var cmd = new ViewActionCommand(() => executed = true, () => true);

            AssertEqual(true, cmd.CanExecute());
            cmd.Execute();
            AssertEqual(true, executed);

            bool blockedExecuted = false;
            var blockedCmd = new ViewActionCommand(() => blockedExecuted = true, () => false);
            AssertEqual(false, blockedCmd.CanExecute());
            blockedCmd.Execute();
            AssertEqual(false, blockedExecuted);
        }

        static void Test_UI_ScreenNavigator_ScreenTransitionsAndHistory()
        {
            var nav = new ScreenNavigator();
            var workshop = new DummyScreen(ScreenId.Workshop);
            var review = new DummyScreen(ScreenId.DesignReview);

            nav.RegisterScreen(workshop);
            nav.RegisterScreen(review);

            ScreenId lastFrom = ScreenId.None;
            ScreenId lastTo = ScreenId.None;
            nav.OnScreenChanged += (from, to) =>
            {
                lastFrom = from;
                lastTo = to;
            };

            bool res = nav.GoTo(ScreenId.Workshop);
            AssertEqual(true, res);
            AssertEqual(ScreenId.Workshop, nav.CurrentScreenId);
            AssertEqual(ScreenId.None, lastFrom);
            AssertEqual(ScreenId.Workshop, lastTo);
            AssertEqual(true, workshop.IsVisible);

            nav.GoTo(ScreenId.DesignReview);
            AssertEqual(ScreenId.DesignReview, nav.CurrentScreenId);
            AssertEqual(ScreenId.Workshop, lastFrom);
            AssertEqual(ScreenId.DesignReview, lastTo);
            AssertEqual(false, workshop.IsVisible);
            AssertEqual(true, review.IsVisible);
            AssertEqual(true, nav.CanGoBack);
        }

        static void Test_UI_ScreenNavigator_BackStack_Popping()
        {
            var nav = new ScreenNavigator();
            var workshop = new DummyScreen(ScreenId.Workshop);
            var review = new DummyScreen(ScreenId.DesignReview);

            nav.RegisterScreen(workshop);
            nav.RegisterScreen(review);

            nav.GoTo(ScreenId.Workshop);
            nav.GoTo(ScreenId.DesignReview);

            bool backRes = nav.GoBack();
            AssertEqual(true, backRes);
            AssertEqual(ScreenId.Workshop, nav.CurrentScreenId);
            AssertEqual(false, nav.CanGoBack);

            AssertEqual(false, nav.GoBack()); // nothing left to go back to
        }

        static void Test_UI_TooltipData_And_TooltipService_Events()
        {
            var service = new TooltipService();
            TooltipData received = null;
            bool dismissed = false;

            service.OnTooltipRequested += (data, pos) => received = data;
            service.OnTooltipDismissed += () => dismissed = true;

            var data = TooltipData.CreateWithReason("Menzil", "12,000 m/s", "Ay iniş gereksinimi", TooltipSeverity.Success);
            service.ShowTooltip(data, UnityEngine.Vector2.zero);

            AssertEqual(true, service.IsVisible);
            AssertEqual("Menzil", received.Title);
            AssertEqual("Ay iniş gereksinimi", received.Reason);
            AssertEqual(TooltipSeverity.Success, received.Severity);

            service.HideTooltip();
            AssertEqual(false, service.IsVisible);
            AssertEqual(true, dismissed);
        }

        private class RunnerBlueprintHolder : IBlueprintHolder
        {
            public Blueprint ActiveBlueprint { get; set; }
            public RunnerBlueprintHolder(Blueprint initial) { ActiveBlueprint = initial; }
        }

        private static Blueprint CreateRunnerTestBlueprint()
        {
            var bus = new DesignComponent("bus-test", "bus", "Test Bus", dryMass: 0.5, cost: 5.0, reliability: 0.95);
            var payload = new PayloadDesign("payload-test", "Test Payload", bus: bus);

            var engine1 = new DesignComponent("eng-1", "engine", "Engine 1", dryMass: 2.0, cost: 8.0, reliability: 0.95, isp: 310, thrust: 800);
            var tank1 = new DesignComponent("tank-1", "tank", "Tank 1", dryMass: 3.0, cost: 3.0, reliability: 0.99);
            var stage1 = new StageDesign("stage-1", "Stage 1", engine1, tank1, propellantMass: 50.0);

            return new Blueprint("bp-test", "Test Vehicle", version: 1, payload: payload, stages: new[] { stage1 });
        }

        static void Test_Slot_Definitions_MetadataValid()
        {
            var slots = VehicleSlotDefinition.All;
            AssertEqual(true, slots.Count >= 10);

            var busDef = VehicleSlotDefinition.Get(VehicleSlotId.PayloadBus);
            AssertEqual("bus", busDef.CategoryTag);
            AssertEqual(true, busDef.IsRequired);

            var engDef = VehicleSlotDefinition.Get(VehicleSlotId.Stage1Engine);
            AssertEqual("engine", engDef.CategoryTag);
            AssertEqual(true, engDef.IsRequired);
        }

        static void Test_Blueprint_WithSlotComponent_And_GetComponentInSlot()
        {
            var bp = CreateRunnerTestBlueprint();

            var initialBus = bp.GetComponentInSlot(VehicleSlotId.PayloadBus);
            AssertEqual(true, initialBus != null);
            AssertEqual("bus-test", initialBus.Id);

            var initialPower = bp.GetComponentInSlot(VehicleSlotId.PayloadPower);
            AssertEqual(true, initialPower == null);

            var powerComp = new DesignComponent("power-solar", "power", "Solar Panel", dryMass: 0.1, cost: 2.0, powerGeneration: 1.5);
            var updatedBp = bp.WithSlotComponent(VehicleSlotId.PayloadPower, powerComp);

            AssertEqual(true, updatedBp.GetComponentInSlot(VehicleSlotId.PayloadPower) != null);
            AssertEqual("power-solar", updatedBp.GetComponentInSlot(VehicleSlotId.PayloadPower).Id);
            AssertEqual(true, bp.GetComponentInSlot(VehicleSlotId.PayloadPower) == null);
        }

        static void Test_Command_SetSlotComponent_ExecutesAndUndoes_ViaCommandBus()
        {
            var bp = CreateRunnerTestBlueprint();
            var holder = new RunnerBlueprintHolder(bp);
            var bus = new CommandBus();

            var newEngine = new DesignComponent("eng-heavy", "engine", "Heavy Engine", dryMass: 3.5, cost: 14.0, reliability: 0.96, isp: 330, thrust: 1600);
            var command = new Apogee.Design.Commands.SetSlotComponentCommand(VehicleSlotId.Stage1Engine, newEngine);

            var adminContext = CommandContext.CreateLocalAdmin(holder);

            var execResult = bus.Execute(command, adminContext);
            AssertEqual(true, execResult.Success);
            AssertEqual("eng-heavy", holder.ActiveBlueprint.GetComponentInSlot(VehicleSlotId.Stage1Engine).Id);
            AssertEqual("eng-1", command.PreviousComponent.Id);
            AssertEqual(true, bus.CanUndo);

            var undoResult = bus.Undo(adminContext);
            AssertEqual(true, undoResult.Success);
            AssertEqual("eng-1", holder.ActiveBlueprint.GetComponentInSlot(VehicleSlotId.Stage1Engine).Id);
            AssertEqual(true, bus.CanRedo);

            var redoResult = bus.Redo(adminContext);
            AssertEqual(true, redoResult.Success);
            AssertEqual("eng-heavy", holder.ActiveBlueprint.GetComponentInSlot(VehicleSlotId.Stage1Engine).Id);
        }

        static void Test_Command_SetSlotComponent_RoleAuthorization_RejectsUnauthorized()
        {
            var bp = CreateRunnerTestBlueprint();
            var holder = new RunnerBlueprintHolder(bp);
            var bus = new CommandBus();

            var newEngine = new DesignComponent("eng-heavy", "engine", "Heavy Engine", dryMass: 3.5);
            var command = new Apogee.Design.Commands.SetSlotComponentCommand(VehicleSlotId.Stage1Engine, newEngine);

            var scienceContext = new CommandContext("player-1", PlayerRole.ScienceDirector, state: holder);

            var execResult = bus.Execute(command, scienceContext);
            AssertEqual(false, execResult.Success);
            AssertEqual("eng-1", holder.ActiveBlueprint.GetComponentInSlot(VehicleSlotId.Stage1Engine).Id);
        }

        static void Test_Design_GhostDelta_ComputesDifferentialAndSummary()
        {
            var bp = CreateRunnerTestBlueprint();
            var candidateEngine = new DesignComponent("eng-adv", "engine", "Advanced Engine", dryMass: 3.0, cost: 12.0, reliability: 0.98, isp: 350, thrust: 1200);

            var ghostDelta = GhostDelta.Compute(bp, VehicleSlotId.Stage1Engine, candidateEngine);

            AssertEqual(true, ghostDelta.HasChanges);
            AssertEqual(true, ghostDelta.DeltaDeltaV > 0.0);
            AssertEqual(true, ghostDelta.DeltaWetMassTons > 0.0);
            AssertEqual(true, ghostDelta.DeltaCostMillions > 0.0);
            AssertEqual(true, ghostDelta.DeltaReliability > 0.0);

            var summary = ghostDelta.GetSummaryText();
            AssertEqual(true, summary.Contains("menzil"));
        }

        static void Test_UI_VehicleSlotViewModel_EquippedStateAndSelection()
        {
            var def = VehicleSlotDefinition.Get(VehicleSlotId.PayloadBus);
            bool selectedInvoked = false;
            var vm = new Apogee.UI.Screens.Workshop.VehicleSlotViewModel(def, () => selectedInvoked = true);

            // Check equipped name contains Zorunlu
            if (!vm.EquippedName.Value.Contains("Zorunlu"))
                throw new Exception($"EquippedName was '{vm.EquippedName.Value}', did not contain 'Zorunlu'");

            var comp = new DesignComponent("bus-light", "bus", "Hafif Şasi", dryMass: 0.35, cost: 5.5, reliability: 0.96);
            vm.SetEquipped(comp);

            AssertEqual("Hafif Şasi", vm.EquippedName.Value);

            if (!vm.SpecsSummary.Value.Contains("0.35t") && !vm.SpecsSummary.Value.Contains("0,35t"))
                throw new Exception($"SpecsSummary was '{vm.SpecsSummary.Value}', did not contain 0.35t or 0,35t");

            vm.SelectSlotCommand.Execute();
            AssertEqual(true, selectedInvoked);
        }

        static void Test_UI_ModuleOptionViewModel_LockedAndHoverCommands()
        {
            var comp = new DesignComponent("eng-adv", "engine", "Gelişmiş Motor", dryMass: 2.0);
            bool hovered = false;
            bool selected = false;

            var unlockedOption = new Apogee.UI.Screens.Workshop.ModuleOptionViewModel(
                comp,
                isLocked: false,
                onHover: () => hovered = true,
                onSelect: () => selected = true
            );

            AssertEqual(true, unlockedOption.SelectCommand.CanExecute());
            unlockedOption.OnHover?.Invoke();
            AssertEqual(true, hovered);

            unlockedOption.SelectCommand.Execute();
            AssertEqual(true, selected);

            var lockedOption = new Apogee.UI.Screens.Workshop.ModuleOptionViewModel(
                comp,
                isLocked: true,
                lockReason: "Kilitli"
            );
            AssertEqual(false, lockedOption.SelectCommand.CanExecute());
        }

        static void Test_UI_GhostDeltaViewModel_ApplyAndClear()
        {
            var ghostVm = new Apogee.UI.Screens.Workshop.GhostDeltaViewModel();
            AssertEqual(false, ghostVm.IsActive.Value);

            var baseBp = Apogee.UI.Screens.Workshop.WorkshopViewModel.CreateDefaultLunarRoverBlueprint();
            var candidate = new DesignComponent("eng-heavy", "engine", "Ağır Motor", dryMass: 4.0, cost: 15.0, isp: 330);
            var delta = GhostDelta.Compute(baseBp, VehicleSlotId.Stage1Engine, candidate);

            ghostVm.ApplyDelta(delta);
            AssertEqual(true, ghostVm.IsActive.Value);
            AssertEqual(true, Math.Abs(ghostVm.DeltaDeltaV.Value) > 0.0);

            ghostVm.Clear();
            AssertEqual(false, ghostVm.IsActive.Value);
            AssertEqual(0.0, ghostVm.DeltaDeltaV.Value);
        }

        static void Test_UI_WorkshopViewModel_SlotSelection_PopulatesCompatibleOptions()
        {
            var vm = new Apogee.UI.Screens.Workshop.WorkshopViewModel();
            AssertEqual(true, vm.Slots.Count > 0);

            vm.SelectSlot(VehicleSlotId.PayloadBus);

            AssertEqual(true, vm.SelectedSlot.Value != null);
            AssertEqual(VehicleSlotId.PayloadBus, vm.SelectedSlot.Value.SlotId);
            AssertEqual(true, vm.AvailableOptions.Value.Count > 0);

            foreach (var opt in vm.AvailableOptions.Value)
            {
                AssertEqual("bus", opt.Category);
            }
        }

        static void Test_UI_WorkshopViewModel_HoverAndUnhover_TogglesGhostDelta()
        {
            var vm = new Apogee.UI.Screens.Workshop.WorkshopViewModel();
            vm.SelectSlot(VehicleSlotId.Stage1Engine);

            var candidate = System.Linq.Enumerable.First(vm.AvailableOptions.Value, o => o.Id != vm.SelectedSlot.Value.EquippedComponent.Value?.Id);

            candidate.OnHover?.Invoke();
            AssertEqual(true, vm.GhostDelta.IsActive.Value);
            AssertEqual(true, Math.Abs(vm.GhostDelta.DeltaDeltaV.Value) > 0.0);

            candidate.OnUnhover?.Invoke();
            AssertEqual(false, vm.GhostDelta.IsActive.Value);
        }

        static void Test_UI_WorkshopViewModel_EquipOption_ExecutesCommand_UndoRedo()
        {
            var vm = new Apogee.UI.Screens.Workshop.WorkshopViewModel();
            vm.SelectSlot(VehicleSlotId.Stage1Engine);

            var initialEngineId = vm.SelectedSlot.Value.EquippedComponent.Value?.Id;
            var differentCandidate = System.Linq.Enumerable.First(vm.AvailableOptions.Value, o => o.Id != initialEngineId);

            AssertEqual(false, vm.CanUndo.Value);

            differentCandidate.SelectCommand.Execute();

            var newEngineId = vm.SelectedSlot.Value.EquippedComponent.Value?.Id;
            AssertEqual(differentCandidate.Id, newEngineId);
            AssertEqual(true, initialEngineId != newEngineId);
            AssertEqual(true, vm.CanUndo.Value);

            vm.UndoCommand.Execute();
            AssertEqual(initialEngineId, vm.SelectedSlot.Value.EquippedComponent.Value?.Id);
            AssertEqual(true, vm.CanRedo.Value);

            vm.RedoCommand.Execute();
            AssertEqual(differentCandidate.Id, vm.SelectedSlot.Value.EquippedComponent.Value?.Id);
        }

        static void Test_CrossSection_ThreeStageRocketAutoAssembledAndSnapped()
        {
            var supplierNordvik = new SupplierSpec("supplier-nordvik", "Nordvik Propulsion", 1.0, 1.0, 1.0);
            var supplierHelion = new SupplierSpec("supplier-helion", "Helion Dynamics", 0.9, 0.8, 0.95);
            var supplierKestrel = new SupplierSpec("supplier-kestrel", "Kestrel Aerospace", 1.2, 1.2, 0.99);

            var s3Engine = new DesignComponent("eng-s3", "engine", "S3 Engine", 0.8, 2.0, 30, 0.98, isp: 350, thrust: 45, supplier: supplierNordvik);
            var s3Tank = new DesignComponent("tank-s3", "tank", "S3 Tank", 1.2, 1.5, 20, 0.99, supplier: supplierNordvik);
            var stage3 = new StageDesign("s3", "Stage 3", s3Engine, s3Tank, 12.0, stageSupplier: supplierNordvik);

            var s2Engine = new DesignComponent("eng-s2", "engine", "S2 Engine", 1.5, 3.5, 45, 0.97, isp: 330, thrust: 200, supplier: supplierHelion);
            var s2Tank = new DesignComponent("tank-s2", "tank", "S2 Tank", 3.0, 2.5, 35, 0.99, supplier: supplierHelion);
            var stage2 = new StageDesign("s2", "Stage 2", s2Engine, s2Tank, 35.0, stageSupplier: supplierHelion);

            var s1Engine = new DesignComponent("eng-s1", "engine", "S1 Engine", 4.0, 6.0, 60, 0.96, isp: 290, thrust: 850, supplier: supplierKestrel);
            var s1Tank = new DesignComponent("tank-s1", "tank", "S1 Tank", 8.0, 5.0, 50, 0.99, supplier: supplierKestrel);
            var stage1 = new StageDesign("s1", "Stage 1", s1Engine, s1Tank, 120.0, stageSupplier: supplierKestrel);

            var bus = new DesignComponent("bus-rover", "bus", "Rover Bus", 0.4, 3.0, 30, 0.98);
            var payload = new PayloadDesign("payload-rover", "Moon Explorer", bus: bus);

            var blueprint = new Blueprint("bp-saturn", "Saturn Class", payload: payload, stages: new[] { stage1, stage2, stage3 });

            var layout = CraftCrossSectionLayout.Build(blueprint);

            AssertEqual(3, layout.StageCount);
            AssertEqual(true, layout.HasPayload);
            AssertEqual(true, layout.Nodes.Count > 10);

            // Top-down sequence check
            AssertEqual(CrossSectionPartType.FairingNose, layout.Nodes[0].PartType);
            AssertEqual(CrossSectionPartType.AeroFins, layout.Nodes[layout.Nodes.Count - 1].PartType);

            // Snap connections check
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                if (i > 0)
                {
                    AssertEqual(true, layout.Nodes[i].TopSnap.IsConnected);
                }
                if (i < layout.Nodes.Count - 1)
                {
                    AssertEqual(true, layout.Nodes[i].BottomSnap.IsConnected);
                }
            }
        }

        static void Test_CrossSection_EmptySlotHandling()
        {
            var s1Engine = new DesignComponent("eng-1", "engine", "S1 Engine", 2.0, 3.0, 30, 0.95);
            var stage1 = new StageDesign("s1", "Stage 1", engine: s1Engine, tank: null);
            var blueprint = new Blueprint("bp-part", "Partial", stages: new[] { stage1 });

            var layout = CraftCrossSectionLayout.Build(blueprint);

            var engNode = System.Linq.Enumerable.FirstOrDefault(layout.Nodes, n => n.SlotId == VehicleSlotId.Stage1Engine);
            AssertEqual(true, engNode != null);
            AssertEqual(false, engNode.IsEmptySlot);

            var tankNode = System.Linq.Enumerable.FirstOrDefault(layout.Nodes, n => n.SlotId == VehicleSlotId.Stage1Tank);
            AssertEqual(true, tankNode != null);
            AssertEqual(true, tankNode.IsEmptySlot);
        }

        static void Test_CrossSection_SupplierColorResolution()
        {
            AssertEqual(CraftCrossSectionLayout.ColorHelion, CraftCrossSectionLayout.ResolveSupplierColor("supplier-helion"));
            AssertEqual(CraftCrossSectionLayout.ColorKestrel, CraftCrossSectionLayout.ResolveSupplierColor("supplier-kestrel"));
            AssertEqual(CraftCrossSectionLayout.ColorNordvik, CraftCrossSectionLayout.ResolveSupplierColor("supplier-nordvik"));
            AssertEqual(CraftCrossSectionLayout.ColorTarsus, CraftCrossSectionLayout.ResolveSupplierColor("supplier-tarsus"));
            AssertEqual(CraftCrossSectionLayout.ColorMeridian, CraftCrossSectionLayout.ResolveSupplierColor("supplier-meridian"));
            AssertEqual(CraftCrossSectionLayout.ColorAurelia, CraftCrossSectionLayout.ResolveSupplierColor("supplier-aurelia"));
            AssertEqual(CraftCrossSectionLayout.ColorDefault, CraftCrossSectionLayout.ResolveSupplierColor("unknown"));
        }

        static void Test_UI_CrossSectionViewModel_LayoutAndTwoWaySync()
        {
            var s1Engine = new DesignComponent("eng-1", "engine", "S1 Engine", 2.0, 3.0, 30, 0.95);
            var stage1 = new StageDesign("s1", "Stage 1", s1Engine);
            var blueprint = new Blueprint("bp-ui-sync", "Sync Test", stages: new[] { stage1 });

            var vm = new Apogee.UI.Screens.Workshop.RocketCrossSectionViewModel(blueprint);

            AssertEqual(true, vm.Nodes.Value.Count > 0);
            AssertEqual(1, vm.StageCount.Value);

            VehicleSlotId? callbackSlot = null;
            vm.SlotSelected += s => callbackSlot = s;

            var target = System.Linq.Enumerable.First(vm.Nodes.Value, n => n.SlotId == VehicleSlotId.Stage1Engine);
            vm.RequestSelectPart(target);

            AssertEqual(VehicleSlotId.Stage1Engine, vm.SelectedSlotId.Value);
            AssertEqual(VehicleSlotId.Stage1Engine, callbackSlot);
            AssertEqual(target.Id, vm.SelectedNode.Value?.Id);
        }

        static void Test_UI_WorkshopViewModel_CrossSection_LiveIntegration()
        {
            var workshopVm = new Apogee.UI.Screens.Workshop.WorkshopViewModel();

            AssertEqual(true, workshopVm.CrossSection != null);
            AssertEqual(true, workshopVm.CrossSection.Nodes.Value.Count > 0);

            // Selecting a slot in Workshop synchronizes cross-section
            workshopVm.SelectSlot(VehicleSlotId.Stage1Engine);
            AssertEqual(VehicleSlotId.Stage1Engine, workshopVm.CrossSection.SelectedSlotId.Value);

            // Selecting a part in CrossSection selects slot in Workshop
            var tankNode = System.Linq.Enumerable.First(workshopVm.CrossSection.Nodes.Value, n => n.SlotId == VehicleSlotId.Stage1Tank);
            workshopVm.CrossSection.RequestSelectPart(tankNode);

            AssertEqual(VehicleSlotId.Stage1Tank, workshopVm.SelectedSlot.Value.SlotId);
            AssertEqual(true, workshopVm.SelectedSlot.Value.IsSelected.Value);
        }

        static void Test_ResultsPanel_ResultBarViewModel_ClampingAndStatuses()
        {
            var bar = new ResultBarViewModel("test", "Test Bar");
            bar.FillRatio = -0.5f;
            AssertEqual(0.0f, bar.FillRatio);
            bar.FillRatio = 1.5f;
            AssertEqual(1.0f, bar.FillRatio);
            bar.FillRatio = 0.42f;
            AssertEqual(0.42f, bar.FillRatio);

            var good = ResultBarViewModel.Good("g", "Good", 0.9f, "val", "reason");
            var warn = ResultBarViewModel.Warn("w", "Warn", 0.5f, "val", "reason");
            var err = ResultBarViewModel.Error("e", "Err", 0.1f, "val", "reason");

            AssertEqual(ResultBarStatus.Good, good.Status);
            AssertEqual(ResultBarStatus.Warning, warn.Status);
            AssertEqual(ResultBarStatus.Error, err.Status);
            AssertEqual("reason", good.ReasonText);
        }

        static void Test_ResultsPanel_BuildSevenBars_AllKeysAndValuesPresent()
        {
            var sc = new StatCollection();
            sc.SetBaseValue(StatType.WetMass, 45.0);
            sc.SetBaseValue(StatType.DryMass, 15.0);
            sc.SetBaseValue(StatType.PropellantMass, 30.0);
            sc.SetBaseValue(StatType.Reliability, 0.95);
            sc.SetBaseValue(StatType.PowerGeneration, 2.5);
            sc.SetBaseValue(StatType.PowerConsumption, 1.2);
            sc.SetBaseValue(StatType.PowerMargin, 1.3);
            sc.SetBaseValue(StatType.ThermalDissipation, 2.0);
            sc.SetBaseValue(StatType.ThermalLoad, 1.0);
            sc.SetBaseValue(StatType.ThermalMargin, 1.0);
            sc.SetBaseValue(StatType.Cost, 35.0);
            sc.SetBaseValue(StatType.BuildTime, 120);

            var stats = new BlueprintStats(sc);
            var bars = ResultsPanelViewModel.Build(stats);

            AssertEqual(7, bars.Count);
            foreach (var b in bars)
            {
                AssertEqual(true, !string.IsNullOrEmpty(b.Key));
                AssertEqual(true, !string.IsNullOrEmpty(b.Label));
                AssertEqual(true, !string.IsNullOrEmpty(b.ValueText));
                AssertEqual(true, !string.IsNullOrEmpty(b.ReasonText));
                AssertEqual(true, b.FillRatio >= 0.0f && b.FillRatio <= 1.0f);
            }
        }

        static void Test_ResultsPanel_Thresholds_GoodWarnErrorMapping()
        {
            var sc = new StatCollection();
            sc.SetBaseValue(StatType.Reliability, 0.75); // Below 0.85 -> Error
            sc.SetBaseValue(StatType.PowerMargin, -0.5); // Deficit -> Error
            sc.SetBaseValue(StatType.ThermalMargin, -0.2); // Deficit -> Error

            var stats = new BlueprintStats(sc);
            var bars = ResultsPanelViewModel.Build(stats);

            var relBar = System.Linq.Enumerable.First(bars, b => b.Key == "reliability");
            var powerBar = System.Linq.Enumerable.First(bars, b => b.Key == "power");
            var thermalBar = System.Linq.Enumerable.First(bars, b => b.Key == "thermal");

            AssertEqual(ResultBarStatus.Error, relBar.Status);
            AssertEqual(ResultBarStatus.Error, powerBar.Status);
            AssertEqual(ResultBarStatus.Error, thermalBar.Status);
        }

        static void Test_ResultsPanel_ValidationReportIntegration_ReflectsValidatorFindings()
        {
            var defaultBp = Apogee.UI.Screens.Workshop.WorkshopViewModel.CreateDefaultLunarRoverBlueprint();
            var context = new ValidationContext(
                missionProfile: new MissionProfile("moon-impossible", requiredDeltaV: 60000)
            );

            var report = BlueprintValidator.Validate(defaultBp, context);
            var bars = ResultsPanelViewModel.Build(report);

            var dvBar = System.Linq.Enumerable.First(bars, b => b.Key == "delta_v");
            AssertEqual(ResultBarStatus.Error, dvBar.Status);
            AssertEqual(true, dvBar.ReasonText.Length > 0);
        }

        static void Test_UI_ReviewViewModel_ValidationPipelineAndBars()
        {
            var defaultBp = Apogee.UI.Screens.Workshop.WorkshopViewModel.CreateDefaultLunarRoverBlueprint();
            var context = new ValidationContext(
                missionProfile: new MissionProfile("moon-easy", requiredDeltaV: 5000, minReliabilityThreshold: 0.50)
            );

            var reviewVm = new Apogee.UI.Screens.Review.ReviewViewModel(null, defaultBp, context);

            AssertEqual(true, reviewVm.ResultBars.Count == 7);
            AssertEqual(true, reviewVm.IsValid.Value);
            AssertEqual(0, reviewVm.ErrorCount.Value);
            AssertEqual(true, reviewVm.StatusSummary.Value.Length > 0);

            // Test re-evaluating with strict threshold producing blocking errors
            var strictContext = new ValidationContext(
                missionProfile: new MissionProfile("moon-strict", requiredDeltaV: 5000, minReliabilityThreshold: 0.99)
            );
            reviewVm.RunValidation(defaultBp, strictContext);
            AssertEqual(false, reviewVm.IsValid.Value);
            AssertEqual(true, reviewVm.ErrorCount.Value > 0);
            AssertEqual(true, reviewVm.StatusSummary.Value.Contains("engel var"));
        }

        static void Test_UI_ReviewViewModel_TrafficLightCounts_And_Filters()
        {
            var defaultBp = Apogee.UI.Screens.Workshop.WorkshopViewModel.CreateDefaultLunarRoverBlueprint();
            var strictContext = new ValidationContext(
                missionProfile: new MissionProfile("moon-strict", requiredDeltaV: 20000, minReliabilityThreshold: 0.999),
                launchPad: LaunchPadInfo.Level1Small
            );

            var vm = new Apogee.UI.Screens.Review.ReviewViewModel(null, defaultBp, strictContext);

            // Test traffic-light counts
            AssertEqual(true, vm.ErrorCount.Value > 0);
            AssertEqual(true, vm.Issues.Count > 0);
            AssertEqual(true, vm.PassCount.Value >= 0);
            AssertEqual("ENGEL VAR (NO-GO)", vm.GoNoGoBadgeText.Value);

            // Test Filter: All
            vm.SetFilter(Apogee.UI.Screens.Review.ReviewFilterMode.All);
            AssertEqual(vm.Issues.Count, vm.FilteredIssues.Count);

            // Test Filter: Errors (Kırmızı)
            vm.SetFilter(Apogee.UI.Screens.Review.ReviewFilterMode.Errors);
            AssertEqual(vm.ErrorCount.Value, vm.FilteredIssues.Count);
            foreach (var issue in vm.FilteredIssues)
            {
                AssertEqual(true, issue.IsError);
            }

            // Test Filter: Warnings (Sarı)
            vm.SetFilter(Apogee.UI.Screens.Review.ReviewFilterMode.Warnings);
            AssertEqual(vm.WarningCount.Value, vm.FilteredIssues.Count);
            foreach (var issue in vm.FilteredIssues)
            {
                AssertEqual(true, issue.IsWarning);
            }

            // Test Filter: Passed (Yeşil)
            vm.SetFilter(Apogee.UI.Screens.Review.ReviewFilterMode.Passed);
            AssertEqual(vm.PassCount.Value, vm.FilteredIssues.Count);
            foreach (var issue in vm.FilteredIssues)
            {
                AssertEqual(true, issue.IsPass);
            }
        }

        static void Test_UI_ReviewIssueItem_NedenAndHint_Integrity()
        {
            var item = new Apogee.UI.Screens.Review.ReviewIssueItem(
                title:       "Menzil Açığı (Yetersiz Δv)",
                description: "Verimli motor seçin.",
                reason:      "Aracın menzili (9200 m/s), gereken menzilden (13500 m/s) 4300 m/s daha az.",
                severity:    "Error",
                ruleId:      ValidationRuleId.DeltaV,
                hint:        "Daha yüksek Isp değerine sahip bir motor seçin veya yakıtı artırın.",
                currentValue: 9200,
                targetValue:  13500
            );

            AssertEqual("Menzil Açığı (Yetersiz Δv)", item.Title);
            AssertEqual("Aracın menzili (9200 m/s), gereken menzilden (13500 m/s) 4300 m/s daha az.", item.Reason);
            AssertEqual("Daha yüksek Isp değerine sahip bir motor seçin veya yakıtı artırın.", item.Hint);
            AssertEqual("Error", item.Severity);
            AssertEqual(ValidationRuleId.DeltaV, item.RuleId);
            AssertEqual(true, item.IsError);
            AssertEqual(false, item.IsWarning);
            AssertEqual(false, item.IsPass);
            AssertEqual("ENGEL", item.SeverityBadgeText);
            AssertEqual("⛔", item.SeveritySymbol);
        }

        static void Test_UI_ReviewViewModel_NavigationArgs_And_BackToWorkshopCommand()
        {
            var nav = new Apogee.UI.Core.Navigation.ScreenNavigator();
            var workshop = new DummyScreen(Apogee.UI.Core.Navigation.ScreenId.Workshop);
            var review = new DummyReviewScreen();

            nav.RegisterScreen(workshop);
            nav.RegisterScreen(review);

            nav.GoTo(Apogee.UI.Core.Navigation.ScreenId.Workshop);
            AssertEqual(Apogee.UI.Core.Navigation.ScreenId.Workshop, nav.CurrentScreenId);

            var defaultBp = Apogee.UI.Screens.Workshop.WorkshopViewModel.CreateDefaultLunarRoverBlueprint();
            var context = new ValidationContext(
                missionProfile: new MissionProfile("moon-demo", requiredDeltaV: 10000, minReliabilityThreshold: 0.60)
            );
            var args = new Apogee.UI.Screens.Review.ReviewNavigationArgs(defaultBp, context);

            nav.GoTo(Apogee.UI.Core.Navigation.ScreenId.DesignReview, args);
            AssertEqual(Apogee.UI.Core.Navigation.ScreenId.DesignReview, nav.CurrentScreenId);
            AssertEqual(true, review.ReceivedContext is Apogee.UI.Screens.Review.ReviewNavigationArgs);

            var reviewVm = new Apogee.UI.Screens.Review.ReviewViewModel(nav);
            reviewVm.RunValidation(args.Blueprint, args.Context);

            AssertEqual(true, reviewVm.ActiveBlueprint == defaultBp);
            AssertEqual(7, reviewVm.ResultBars.Count);
            AssertEqual(true, reviewVm.Issues.Count > 0);

            // Test BackToWorkshopCommand
            reviewVm.BackToWorkshopCommand.Execute();
            AssertEqual(Apogee.UI.Core.Navigation.ScreenId.Workshop, nav.CurrentScreenId);
        }

        static void Test_UI_WorkshopViewModel_GoToReviewCommand_TransfersBlueprintAndContext()
        {
            var nav = new Apogee.UI.Core.Navigation.ScreenNavigator();
            var workshop = new DummyScreen(Apogee.UI.Core.Navigation.ScreenId.Workshop);
            var review = new DummyReviewScreen();

            nav.RegisterScreen(workshop);
            nav.RegisterScreen(review);

            var workshopVm = new Apogee.UI.Screens.Workshop.WorkshopViewModel(nav);

            nav.GoTo(Apogee.UI.Core.Navigation.ScreenId.Workshop);
            AssertEqual(Apogee.UI.Core.Navigation.ScreenId.Workshop, nav.CurrentScreenId);

            workshopVm.GoToReviewCommand.Execute();

            AssertEqual(Apogee.UI.Core.Navigation.ScreenId.DesignReview, nav.CurrentScreenId);
            AssertEqual(true, review.ReceivedContext is Apogee.UI.Screens.Review.ReviewNavigationArgs);

            var args = (Apogee.UI.Screens.Review.ReviewNavigationArgs)review.ReceivedContext;
            AssertEqual(workshopVm.ActiveBlueprint.Id, args.Blueprint.Id);
            AssertEqual(true, args.Context != null);
            AssertEqual(workshopVm.TargetDeltaV.Value, args.Context.MissionProfile.RequiredDeltaV);
        }

        private class DummyReviewScreen : Apogee.UI.Core.Navigation.IScreenController
        {
            public Apogee.UI.Core.Navigation.ScreenId ScreenId => Apogee.UI.Core.Navigation.ScreenId.DesignReview;
            public UnityEngine.UIElements.VisualElement RootElement => null;
            public bool IsVisible { get; private set; }
            public object ReceivedContext { get; private set; }

            public void Initialize(UnityEngine.UIElements.VisualElement parentContainer) { }

            public void OnEnter(object context = null)
            {
                IsVisible = true;
                ReceivedContext = context;
            }

            public void OnExit()
            {
                IsVisible = false;
            }

            public void Dispose() { }
        }

        // ── M2-06 helpers & tests (inside Program) ───────────────────────────────────

    static Blueprint MakeSimpleBlueprint(string id = "bp-test", string name = "Test Blueprint")
    {
        var catalog = Apogee.UI.Screens.Workshop.WorkshopViewModel.CreateDefaultCatalog();
        var busComp = catalog.Find(c => c.Id == "bus-rover-light");
        var powerComp = catalog.Find(c => c.Id == "power-solar-array-basic");
        var commsComp = catalog.Find(c => c.Id == "comms-omni-antenna");
        var payload = new PayloadDesign("payload-test", "Test Yük", bus: busComp, power: powerComp, comms: commsComp);
        var engine = catalog.Find(c => c.Id == "engine-kerosene-lox-mk1");
        var tank = catalog.Find(c => c.Id == "tank-kerosene-heavy");
        var stage = new StageDesign("stage-1", "S1", engine, tank, propellantMass: 50.0);
        return new Blueprint(id, name, targetDestinationId: "moon", targetMissionTypeId: "rover",
            payload: payload, stages: new[] { stage });
    }

    static DesignTemplate MakeTemplate(string id = "tpl-1", string name = "Şablon 1")
    {
        var bp = MakeSimpleBlueprint(id + "-bp", name + " BP");
        return new DesignTemplate(id, name, bp, description: "Test şablonu");
    }

    static void Test_Template_DesignTemplate_CreateAndClone()
    {
        var tpl = MakeTemplate("tpl-a", "Alpha");
        AssertEqual("tpl-a", tpl.Id, "Id mismatch");
        AssertEqual("Alpha", tpl.Name, "Name mismatch");
        AssertTrue(tpl.Blueprint != null, "Blueprint null");

        var clone = tpl.Clone("tpl-b", "Beta");
        AssertEqual("tpl-b", clone.Id, "Cloned Id mismatch");
        AssertEqual("Beta", clone.Name, "Cloned Name mismatch");
        AssertTrue(clone.Blueprint != null, "Cloned blueprint null");

        var renamed = tpl.WithName("Alpha Renamed");
        AssertEqual("tpl-a", renamed.Id, "Renamed Id should not change");
        AssertEqual("Alpha Renamed", renamed.Name, "Renamed Name mismatch");
    }

    static void Test_Template_Repository_CRUD()
    {
        var repo = new InMemoryTemplateRepository();
        var tpl = MakeTemplate("tpl-1", "Şablon 1");

        AssertFalse(repo.Contains("tpl-1"), "Repository should start empty");
        repo.Save(tpl);
        AssertTrue(repo.Contains("tpl-1"), "Should contain after save");

        var retrieved = repo.GetById("tpl-1");
        AssertTrue(retrieved != null, "GetById returned null");
        AssertEqual("Şablon 1", retrieved.Name, "Retrieved name mismatch");
        AssertEqual(1, repo.GetAll().Count, "GetAll should return 1");

        bool deleted = repo.Delete("tpl-1");
        AssertTrue(deleted, "Delete should return true");
        AssertFalse(repo.Contains("tpl-1"), "Should not contain after delete");
        AssertFalse(repo.Delete("nonexistent"), "Delete missing should return false");
    }

    static void Test_Template_Repository_DuplicateAndRename()
    {
        var repo = new InMemoryTemplateRepository();
        repo.Save(MakeTemplate("tpl-src", "Kaynak"));

        var cloned = repo.Duplicate("tpl-src", "tpl-copy", "Kopya");
        AssertTrue(cloned != null, "Duplicate returned null");
        AssertEqual("tpl-copy", cloned.Id, "Cloned id mismatch");
        AssertEqual("Kopya", cloned.Name, "Cloned name mismatch");
        AssertTrue(repo.Contains("tpl-copy"), "Repo should contain cloned");
        AssertTrue(repo.Contains("tpl-src"), "Source should remain");

        AssertTrue(repo.Rename("tpl-src", "Kaynak Yeni"), "Rename should return true");
        AssertEqual("Kaynak Yeni", repo.GetById("tpl-src").Name, "Renamed name mismatch");
        AssertFalse(repo.Rename("nonexistent", "X"), "Rename nonexistent should return false");
    }

    static void Test_Template_Repository_OnChanged_Fires()
    {
        var repo = new InMemoryTemplateRepository();
        int fireCount = 0;
        repo.OnTemplatesChanged += () => fireCount++;

        repo.Save(MakeTemplate("t1"));
        AssertEqual(1, fireCount, "Save should fire OnChanged");
        repo.Rename("t1", "Yeni");
        AssertEqual(2, fireCount, "Rename should fire OnChanged");
        repo.Duplicate("t1", "t2", "Kopya");
        AssertEqual(3, fireCount, "Duplicate should fire OnChanged");
        repo.Delete("t1");
        AssertEqual(4, fireCount, "Delete should fire OnChanged");
    }

    static void Test_Template_Command_SaveAndUndo()
    {
        var repo = new InMemoryTemplateRepository();
        var tpl = MakeTemplate("tpl-save", "Kaydet");
        var cmd = new SaveTemplateCommand(repo, tpl);
        var bus2 = new CommandBus();
        var ctx2 = CommandContext.CreateLocalAdmin(null);

        var res = bus2.Execute(cmd, ctx2);
        AssertTrue(res.Success, "SaveTemplate Execute failed: " + res.Message);
        AssertTrue(repo.Contains("tpl-save"), "Template should exist after save");

        var undo = bus2.Undo(ctx2);
        AssertTrue(undo.Success, "Undo failed: " + undo.Message);
        AssertFalse(repo.Contains("tpl-save"), "Template should be removed after undo");
    }

    static void Test_Template_Command_DeleteAndUndo()
    {
        var repo = new InMemoryTemplateRepository();
        var tpl = MakeTemplate("tpl-del", "Sil");
        repo.Save(tpl);
        var bus2 = new CommandBus();
        var cmd = new DeleteTemplateCommand(repo, "tpl-del");
        var ctx2 = CommandContext.CreateLocalAdmin(null);

        var res = bus2.Execute(cmd, ctx2);
        AssertTrue(res.Success, "Delete failed: " + res.Message);
        AssertFalse(repo.Contains("tpl-del"), "Should not exist after delete");

        var undo = bus2.Undo(ctx2);
        AssertTrue(undo.Success, "Undo failed");
        AssertTrue(repo.Contains("tpl-del"), "Template should be restored after undo");
    }

    static void Test_Template_Command_RenameAndUndo()
    {
        var repo = new InMemoryTemplateRepository();
        repo.Save(MakeTemplate("tpl-ren", "Orijinal"));
        var bus2 = new CommandBus();
        var cmd = new RenameTemplateCommand(repo, "tpl-ren", "Yeniden Adlandırılmış");
        var ctx2 = CommandContext.CreateLocalAdmin(null);

        var res = bus2.Execute(cmd, ctx2);
        AssertTrue(res.Success, "Rename failed");
        AssertEqual("Yeniden Adlandırılmış", repo.GetById("tpl-ren").Name, "Renamed name mismatch");

        bus2.Undo(ctx2);
        AssertEqual("Orijinal", repo.GetById("tpl-ren").Name, "Undo rename mismatch");
    }

    static void Test_Template_Command_DuplicateAndUndo()
    {
        var repo = new InMemoryTemplateRepository();
        repo.Save(MakeTemplate("tpl-orig", "Orijinal"));
        var bus2 = new CommandBus();
        var cmd = new DuplicateTemplateCommand(repo, "tpl-orig", "tpl-dup", "Kopya");
        var ctx2 = CommandContext.CreateLocalAdmin(null);

        var res = bus2.Execute(cmd, ctx2);
        AssertTrue(res.Success, "Duplicate failed");
        AssertTrue(repo.Contains("tpl-dup"), "Duplicate should exist");
        AssertTrue(repo.Contains("tpl-orig"), "Source should remain");

        bus2.Undo(ctx2);
        AssertFalse(repo.Contains("tpl-dup"), "Duplicate should be removed after undo");
        AssertTrue(repo.Contains("tpl-orig"), "Source should still remain");
    }

    static void Test_Template_Command_AuthorizationCheck()
    {
        var repo = new InMemoryTemplateRepository();
        repo.Save(MakeTemplate("tpl-auth"));
        var bus2 = new CommandBus();
        var wrongCtx = new CommandContext("unauthorized-user", PlayerRole.ScienceDirector);

        var saveRes = bus2.Execute(new SaveTemplateCommand(repo, MakeTemplate("tpl-new")), wrongCtx);
        AssertFalse(saveRes.Success, "Unauthorized save should fail");

        var delRes = bus2.Execute(new DeleteTemplateCommand(repo, "tpl-auth"), wrongCtx);
        AssertFalse(delRes.Success, "Unauthorized delete should fail");
    }

    static void Test_MissionCode_Encode_Decode_RoundTrip()
    {
        var bp = MakeSimpleBlueprint("bp-code", "Kod Testi");
        string code = MissionCodeCodec.Encode(bp, seed: 12345);
        AssertTrue(code.StartsWith("APG1-"), "Code prefix missing");

        bool ok = MissionCodeCodec.TryDecode(code, out var data, out var error);
        AssertTrue(ok, "Decode failed: " + error);
        AssertTrue(data != null, "Decoded data null");
        AssertEqual("Kod Testi", data.BlueprintName, "Blueprint name round-trip mismatch");
        AssertEqual((ulong)12345, data.Seed, "Seed round-trip mismatch");
        AssertEqual("moon", data.DestinationId, "Destination round-trip mismatch");
    }

    static void Test_MissionCode_InvalidCode()
    {
        bool ok1 = MissionCodeCodec.TryDecode("", out _, out var err1);
        AssertFalse(ok1, "Empty code should fail");
        AssertTrue(err1 != null && err1.Length > 0, "Error message expected");

        bool ok2 = MissionCodeCodec.TryDecode("INVALID-CODE", out _, out _);
        AssertFalse(ok2, "Bad prefix should fail");
    }

    static void Test_BlueprintDiff_StatComparisons()
    {
        var bpA = MakeSimpleBlueprint("bp-a", "A");
        var catalog = Apogee.UI.Screens.Workshop.WorkshopViewModel.CreateDefaultCatalog();
        var advPanel = catalog.Find(c => c.Id == "power-solar-array-advanced");
        var bpB = bpA.WithSlotComponent(VehicleSlotId.PayloadPower, advPanel);

        var diff = BlueprintComparator.Compare(bpA, bpB, requiredDeltaV: 13500);
        AssertTrue(diff.StatComparisons.Count > 0, "StatComparisons empty");

        StatComparisonItem powerItem = null;
        foreach (var s in diff.StatComparisons)
            if (s.Key == "PowerMargin") { powerItem = s; break; }

        AssertTrue(powerItem != null, "PowerMargin stat not found");
        AssertTrue(powerItem.Delta > 0, "Advanced panel should improve power margin");
        AssertTrue(powerItem.IsBetter == true, "Power margin delta should be flagged better");
    }

    static void Test_BlueprintDiff_SlotComparisons()
    {
        var bpA = MakeSimpleBlueprint("bp-a", "A");
        var catalog = Apogee.UI.Screens.Workshop.WorkshopViewModel.CreateDefaultCatalog();
        var dish = catalog.Find(c => c.Id == "comms-dish-highgain");
        var bpB = bpA.WithSlotComponent(VehicleSlotId.PayloadComms, dish);

        var diff = BlueprintComparator.Compare(bpA, bpB, requiredDeltaV: 13500);
        AssertTrue(diff.SlotComparisons.Count > 0, "SlotComparisons empty");
        AssertTrue(diff.ChangedSlotsCount >= 1, "Should have at least 1 changed slot");

        SlotComparisonItem commsItem = null;
        foreach (var s in diff.SlotComparisons)
            if (s.SlotId == VehicleSlotId.PayloadComms) { commsItem = s; break; }

        AssertTrue(commsItem != null, "Comms slot not found");
        AssertEqual(SlotComparisonStatus.Changed, commsItem.Status, "Comms slot should be Changed");
    }

    static void Test_UI_ComparisonViewModel_Apply()
    {
        var catalog = Apogee.UI.Screens.Workshop.WorkshopViewModel.CreateDefaultCatalog();
        var bpA = MakeSimpleBlueprint("bp-cmp-a", "Tasarım A");
        var dish = catalog.Find(c => c.Id == "comms-dish-highgain");
        var bpB = bpA.WithSlotComponent(VehicleSlotId.PayloadComms, dish).WithId("bp-cmp-b");

        var vm = new ComparisonViewModel();
        var args = new ComparisonNavigationArgs(bpA, bpB, "A Etiketi", "B Etiketi");
        vm.ApplyComparison(args);

        AssertEqual("A Etiketi", vm.LabelA.Value, "Label A mismatch");
        AssertEqual("B Etiketi", vm.LabelB.Value, "Label B mismatch");
        AssertTrue(vm.StatComparisons.Value.Count > 0, "Stat comparisons should be populated");
        AssertTrue(vm.ChangedSlotsCount.Value >= 1, "ChangedSlotsCount should be >=1");
        AssertTrue(vm.SummaryText.Value.Length > 0, "Summary should not be empty");
        vm.Dispose();
    }

    static void Test_UI_TemplateManagerViewModel_SaveAndRefresh()
    {
        var repo = new InMemoryTemplateRepository();
        var bus2 = new CommandBus();
        var vm = new TemplateManagerViewModel(repo, bus2);
        int idCounter = 0;
        var bp = MakeSimpleBlueprint("bp-mgr", "Yönetici Testi");
        vm.GetActiveBlueprint = () => bp;
        vm.GenerateNewTemplateId = () => "tpl-mgr-" + (++idCounter);

        AssertEqual(0, vm.Templates.Value.Count, "Start with 0 templates");
        AssertFalse(vm.HasTemplates.Value, "HasTemplates should be false initially");

        vm.SaveCurrentCommand.Execute();
        AssertEqual(1, vm.Templates.Value.Count, "Should have 1 template after save");
        AssertTrue(vm.HasTemplates.Value, "HasTemplates should be true");
        AssertTrue(repo.Contains("tpl-mgr-1"), "Repo should contain the saved template");

        vm.SelectTemplate("tpl-mgr-1");
        AssertTrue(vm.SelectedTemplate.Value != null, "Selection should not be null");

        vm.DuplicateSelectedCommand.Execute();
        AssertEqual(2, vm.Templates.Value.Count, "Should have 2 templates after duplicate");

        vm.SelectTemplate("tpl-mgr-1");
        vm.DeleteSelectedCommand.Execute();
        AssertEqual(1, vm.Templates.Value.Count, "Should have 1 template after delete");

        vm.Dispose();
    }

    // ── M2-07 Vertical Slice Content Tests ──────────────────────────────────────────

    static void Test_VerticalSlice_MoonDestination_CatalogIntegrity()
    {
        // Moon destination: target body with surface landing
        AssertEqual("moon", LunarRoverVerticalSlice.DestinationId, "DestinationId mismatch");
        AssertEqual("Ay (Lunar Surface)", LunarRoverVerticalSlice.DestinationName, "DestinationName mismatch");
        AssertTrue(LunarRoverVerticalSlice.TargetDeltaV >= 13000.0, "Moon target Delta-V should be >= 13,000 m/s");

        var profile = LunarRoverVerticalSlice.CreateMissionProfile();
        AssertEqual("moon", profile.TargetDestinationId, "Mission profile destination should be moon");
        AssertEqual("rover", profile.TargetMissionTypeId, "Mission profile type should be rover");
        AssertTrue(profile.RequiresEdl, "Moon rover must require EDL");
    }

    static void Test_VerticalSlice_RoverMissionType_CatalogIntegrity()
    {
        AssertEqual("rover", LunarRoverVerticalSlice.MissionTypeId, "MissionTypeId mismatch");
        AssertEqual("Ay Gezgini (Rover)", LunarRoverVerticalSlice.MissionTypeName, "MissionTypeName mismatch");
        AssertEqual(0.50, LunarRoverVerticalSlice.MinReliability, "Rover min reliability should be 0.50");

        var profile = LunarRoverVerticalSlice.CreateMissionProfile();
        AssertTrue(profile.RequiredModuleCategories.Contains("bus"), "Must require bus");
        AssertTrue(profile.RequiredModuleCategories.Contains("power"), "Must require power");
        AssertTrue(profile.RequiredModuleCategories.Contains("edl"), "Must require edl");
        AssertFalse(profile.RequiresLifeSupport, "Rover should not require life support");
    }

    static void Test_VerticalSlice_Materials_CatalogIntegrity()
    {
        var materials = LunarRoverVerticalSlice.Materials;
        AssertEqual(3, materials.Count, "Should provide 3 structural materials");

        var al = materials[0];
        AssertEqual("al-alloy", al.Id, "Al-alloy id mismatch");
        AssertEqual(1.0, al.MassMultiplier, "Al-alloy mass should be 1.0");
        AssertEqual(1.0, al.CostMultiplier, "Al-alloy cost should be 1.0");

        var alLi = materials[1];
        AssertEqual("al-li", alLi.Id, "Al-Li id mismatch");
        AssertTrue(alLi.MassMultiplier < 1.0, "Al-Li should be lighter than 1.0");
        AssertTrue(alLi.CostMultiplier > 1.0, "Al-Li should cost more than 1.0");

        var carbon = materials[2];
        AssertEqual("carbon-composite", carbon.Id, "Carbon-composite id mismatch");
        AssertTrue(carbon.MassMultiplier < alLi.MassMultiplier, "Carbon should be lighter than Al-Li");
        AssertTrue(carbon.CostMultiplier > alLi.CostMultiplier, "Carbon should cost more than Al-Li");
    }

    static void Test_VerticalSlice_ModulesAndEngines_Counts()
    {
        var modules = LunarRoverVerticalSlice.Modules;
        AssertTrue(modules.Count >= 12, "Should provide at least ~12 modules");

        // 3 engines
        var engines = new List<DesignComponent>();
        foreach (var m in modules)
        {
            if (m.Category == "engine") engines.Add(m);
        }
        AssertEqual(3, engines.Count, "Should provide exactly 3 distinct propulsion engines");

        // Engine 1: Kerosene Mk-1
        AssertEqual("engine-kerosene-lox-mk1", LunarRoverVerticalSlice.EngineKeroseneMk1.Id, "Kerosene Mk-1 ID mismatch");
        AssertEqual(310.0, LunarRoverVerticalSlice.EngineKeroseneMk1.Isp, "Kerosene Mk-1 Isp mismatch");

        // Engine 2: Heavy Kerosene
        AssertEqual("engine-kerosene-lox-heavy", LunarRoverVerticalSlice.EngineKeroseneHeavy.Id, "Heavy Kerosene ID mismatch");
        AssertEqual(320.0, LunarRoverVerticalSlice.EngineKeroseneHeavy.Isp, "Heavy Kerosene Isp mismatch");

        // Engine 3: Hydrolox Upper
        AssertEqual("engine-hydrolox-upper-mk1", LunarRoverVerticalSlice.EngineHydroloxUpper.Id, "Hydrolox Upper ID mismatch");
        AssertEqual(445.0, LunarRoverVerticalSlice.EngineHydroloxUpper.Isp, "Hydrolox Upper Isp mismatch");
    }

    static void Test_VerticalSlice_HelionSupplier_Specs()
    {
        var helion = LunarRoverVerticalSlice.HelionSupplier;
        AssertEqual("helion-dynamics", helion.Id, "Supplier id mismatch");
        AssertEqual("Helion Dynamics", helion.Name, "Supplier name mismatch");
        AssertEqual(0.85, helion.CostMultiplier, "Helion cost multiplier should be 0.85 (-15%)");
        AssertEqual(0.90, helion.DeliveryMultiplier, "Helion delivery multiplier should be 0.90 (-10% days)");
        AssertEqual(0.96, helion.ReliabilityMultiplier, "Helion reliability multiplier should be 0.96");
    }

    static void Test_VerticalSlice_MissingEdlBlueprint_FailsReview()
    {
        var blueprint = LunarRoverVerticalSlice.CreateMissingEdlBlueprint();
        var context = LunarRoverVerticalSlice.CreateValidationContext();

        var report = BlueprintValidator.Validate(blueprint, context);
        AssertFalse(report.IsValid, "Missing EDL blueprint must not pass review");
        AssertTrue(report.ErrorCount > 0, "Missing EDL blueprint must have blocking error issues");

        bool hasEdlIssue = false;
        foreach (var issue in report.Issues)
        {
            if (issue.RuleId == ValidationRuleId.MandatoryModules &&
                (issue.Reason.IndexOf("EDL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 issue.Title.IndexOf("EDL", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                hasEdlIssue = true;
                break;
            }
        }
        AssertTrue(hasEdlIssue, "Validation report should specifically flag missing mandatory 'edl' module");
    }

    static void Test_VerticalSlice_EquippingEdl_ConvertsToPassingReview()
    {
        var failingBlueprint = LunarRoverVerticalSlice.CreateMissingEdlBlueprint();
        var context = LunarRoverVerticalSlice.CreateValidationContext();

        // Initially fails
        var initialReport = BlueprintValidator.Validate(failingBlueprint, context);
        AssertFalse(initialReport.IsValid, "Initially failing blueprint should fail");

        // Player equips EDL Skycrane via slot mutation
        var edlComponent = LunarRoverVerticalSlice.EdlLunarSkycrane;
        var correctedBlueprint = failingBlueprint.WithSlotComponent(VehicleSlotId.PayloadEdl, edlComponent);

        // Re-validate
        var resolvedReport = BlueprintValidator.Validate(correctedBlueprint, context);
        AssertTrue(resolvedReport.IsValid, "Equipping EDL skycrane must resolve blocking issue and pass review");
        AssertEqual(0, resolvedReport.ErrorCount, "Error count must be 0 for passing review");
    }

    static void Test_VerticalSlice_PassingBlueprint_AllEightValidatorsPass()
    {
        var blueprint = LunarRoverVerticalSlice.CreatePassingBlueprint();
        var context = LunarRoverVerticalSlice.CreateValidationContext();

        var report = BlueprintValidator.Validate(blueprint, context);
        AssertTrue(report.IsValid, "Standard passing lunar rover blueprint must pass review");
        AssertEqual(0, report.ErrorCount, "ErrorCount must be 0");

        // Verify key engineering metrics
        var stats = BlueprintEvaluator.Evaluate(blueprint);
        AssertTrue(stats.NetDeltaV >= context.MissionProfile.RequiredDeltaV,
            $"Net Delta-V ({stats.NetDeltaV:0} m/s) must meet mission requirement ({context.MissionProfile.RequiredDeltaV:0} m/s)");
        AssertTrue(stats.Reliability >= context.MissionProfile.MinReliabilityThreshold,
            $"Reliability ({stats.Reliability:P1}) must meet threshold ({context.MissionProfile.MinReliabilityThreshold:P1})");
        AssertTrue(stats.PowerMargin >= 0.0,
            $"Power margin ({stats.PowerMargin:0.##} kW) must be non-negative");
        AssertTrue(stats.DryMass > 0.0, "Total mass must be positive");
    }

    static void Test_VerticalSlice_MaterialSwitch_ChangesMassAndDeltaV()
    {
        var bpAlloy = LunarRoverVerticalSlice.CreatePassingBlueprint(LunarRoverVerticalSlice.AluminumAlloy);
        var bpCarbon = LunarRoverVerticalSlice.CreatePassingBlueprint(LunarRoverVerticalSlice.CarbonComposite);

        var statsAlloy = BlueprintEvaluator.Evaluate(bpAlloy);
        var statsCarbon = BlueprintEvaluator.Evaluate(bpCarbon);

        // Carbon composite is lighter (x0.75) but more expensive (x2.50)
        AssertTrue(statsCarbon.DryMass < statsAlloy.DryMass,
            $"Carbon composite dry mass ({statsCarbon.DryMass:0.##}t) must be less than Al-Alloy ({statsAlloy.DryMass:0.##}t)");
        AssertTrue(statsCarbon.NetDeltaV > statsAlloy.NetDeltaV,
            $"Carbon composite Delta-V ({statsCarbon.NetDeltaV:0} m/s) must exceed Al-Alloy Delta-V ({statsAlloy.NetDeltaV:0} m/s)");
        AssertTrue(statsCarbon.Cost > statsAlloy.Cost,
            $"Carbon composite cost ({statsCarbon.Cost:0.##}M$) must exceed Al-Alloy cost ({statsAlloy.Cost:0.##}M$)");
    }

    static void Test_VerticalSlice_Workshop_GoToReview_PassesValidLunarRoverContext()
    {
        var nav = new Apogee.UI.Core.Navigation.ScreenNavigator();
        var bus = new CommandBus();
        var bp = LunarRoverVerticalSlice.CreatePassingBlueprint();
        var vm = new Apogee.UI.Screens.Workshop.WorkshopViewModel(nav, bus, bp, LunarRoverVerticalSlice.Modules);

        var dummyReview = new DummyScreen(ScreenId.DesignReview);
        nav.RegisterScreen(dummyReview);

        vm.GoToReviewCommand.Execute();

        AssertTrue(dummyReview.LastContext is Apogee.UI.Screens.Review.ReviewNavigationArgs, "GoToReviewCommand must transfer ReviewNavigationArgs");
        var capturedArgs = (Apogee.UI.Screens.Review.ReviewNavigationArgs)dummyReview.LastContext;
        AssertEqual(bp.Id, capturedArgs.Blueprint.Id, "Blueprint ID must match");
        AssertEqual("moon", capturedArgs.Context.MissionProfile.TargetDestinationId, "Destination must be moon");
        AssertEqual("rover", capturedArgs.Context.MissionProfile.TargetMissionTypeId, "Mission type must be rover");
        AssertTrue(capturedArgs.Context.MissionProfile.RequiresEdl, "RequiresEdl must be true");

        // Verify review viewmodel can be constructed from these transferred args
        var reviewVm = new Apogee.UI.Screens.Review.ReviewViewModel(nav, capturedArgs.Blueprint, capturedArgs.Context);
        AssertTrue(reviewVm.IsValid.Value, "Review screen must show passing status for standard passing blueprint");
        AssertEqual(0, reviewVm.ErrorCount.Value, "Error count must be 0 on review screen");

        reviewVm.Dispose();
        vm.Dispose();
    }

    // ── M3-01 Mission Composer Catalog Tests ──────────────────────────────────────

    static void Test_MissionComposer_ObjectivesCatalog_LoadsAllTargetsWithCompleteRequirements()
    {
        // This catches an omitted or un-loadable objective catalog, which would leave
        // the Mission Composer unable to derive requirements for a player selection.
        string catalogPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Assets", "_Project", "Data", "Catalog", "Json", "objectives.json");
        AssertTrue(File.Exists(catalogPath), "M3-01 must provide objectives.json.");

        Type objectiveType = typeof(CatalogLoader).Assembly.GetType("Apogee.Data.ObjectiveRecord");
        AssertTrue(objectiveType != null, "M3-01 must expose ObjectiveRecord through the data assembly.");

        MethodInfo loadMethod = typeof(CatalogLoader)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Load" && method.IsGenericMethodDefinition);
        object catalog = loadMethod.MakeGenericMethod(objectiveType)
            .Invoke(null, new object[] { File.ReadAllText(catalogPath), null });
        object validation = catalog.GetType().GetProperty("Validation").GetValue(catalog);
        bool isValid = (bool)validation.GetType().GetProperty("IsValid").GetValue(validation);
        var errors = ((IEnumerable)validation.GetType().GetProperty("Errors").GetValue(validation))
            .Cast<object>()
            .Select(error => error.ToString());
        AssertTrue(isValid,
            "Objective catalog must pass generic catalog validation: " +
            string.Join("; ", errors));
        var objectives = ((IEnumerable)catalog.GetType().GetProperty("All").GetValue(catalog))
            .Cast<object>()
            .ToList();
        AssertEqual(17, objectives.Count, "GDD §5.2 defines 17 initial objectives across five destinations.");

        var expectedDestinationIds = new[] { "leo", "moon", "mars", "venus", "europa" };
        foreach (string destinationId in expectedDestinationIds)
        {
            AssertTrue(objectives.Any(objective =>
                ((IEnumerable)objectiveType.GetProperty("DestinationIds").GetValue(objective))
                .Cast<object>()
                .Any(id => string.Equals(id.ToString(), destinationId, StringComparison.Ordinal))),
                $"Objective catalog must contain at least one objective for '{destinationId}'.");
        }

        var lifeSearch = objectives.SingleOrDefault(objective => string.Equals(
            (string)objectiveType.GetProperty("Id").GetValue(objective),
            "mars-life-signatures",
            StringComparison.Ordinal));
        AssertTrue(lifeSearch != null, "Mars life-signature survey objective must be present.");
        var instruments = ((IEnumerable)objectiveType.GetProperty("RequiredInstrumentCategories").GetValue(lifeSearch))
            .Cast<object>()
            .Select(category => category.ToString());
        AssertTrue(instruments.Contains("spectrometer"),
            "Mars life-signature survey must require a spectrometer.");
        AssertTrue((double)objectiveType.GetProperty("MinimumDataGb").GetValue(lifeSearch) > 0.0,
            "Temporary data requirements must remain positive until source values replace TODO-VERİ placeholders.");
        AssertTrue((int)objectiveType.GetProperty("MinimumDurationDays").GetValue(lifeSearch) > 0,
            "Temporary duration requirements must remain positive until source values replace TODO-VERİ placeholders.");
        string sourceNote = (string)objectiveType.GetProperty("DataSourceNote").GetValue(lifeSearch);
        AssertTrue(!string.IsNullOrWhiteSpace(sourceNote) && sourceNote.Contains("TODO-VERİ"),
            "Provisional objective values must be explicitly marked TODO-VERİ.");
    }

    // ── M3-02 Mission Composer Engine Tests ────────────────────────────────────────

    static void Test_MissionComposer_MarsCrewed_Vs_MarsRover_DistinctlyDifferentOutputs()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var destinations = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objectives = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));

        var mars = destinations.GetById("mars");
        var roverType = types.GetById("rover");
        var crewedType = types.GetById("crewed");

        var roverObjective = objectives.GetById("mars-rover-exploration");
        var crewedObjectives = new[] { objectives.GetById("mars-atmosphere-sampling"), objectives.GetById("mars-life-signatures") };

        var roverProfile = MissionComposer.Compose(mars, roverType, new[] { roverObjective }, missionName: "Mars Rover Mission");
        var crewedProfile = MissionComposer.Compose(mars, crewedType, crewedObjectives, missionName: "Mars Crewed Expedition");

        // Mars Rover = 13,600 m/s; Mars Crewed = 19,200 m/s
        AssertApproxEqual(13600.0, roverProfile.RequiredDeltaV, 50.0, "Mars rover Δv should match surface landing budget.");
        AssertApproxEqual(19200.0, crewedProfile.RequiredDeltaV, 50.0, "Mars crewed Δv should include round-trip return budget.");
        AssertTrue(crewedProfile.RequiredDeltaV > roverProfile.RequiredDeltaV + 5000.0, "Crewed mission must demand substantially more Δv.");

        // Life support and EDL
        AssertFalse(roverProfile.RequiresLifeSupport, "Rover mission must not require life support.");
        AssertTrue(crewedProfile.RequiresLifeSupport, "Crewed mission must require life support.");
        AssertTrue(roverProfile.RequiresEdl, "Rover mission must require EDL.");
        AssertTrue(crewedProfile.RequiresEdl, "Crewed mission must require EDL.");

        // Mandatory module categories
        AssertTrue(roverProfile.RequiredModuleCategories.Contains("edl"), "Rover must require EDL.");
        AssertTrue(roverProfile.RequiredModuleCategories.Contains("instrument"), "Rover must require instrument.");
        AssertFalse(roverProfile.RequiredModuleCategories.Contains("cabin"), "Rover must not require cabin.");

        AssertTrue(crewedProfile.RequiredModuleCategories.Contains("cabin"), "Crewed must require cabin.");
        AssertTrue(crewedProfile.RequiredModuleCategories.Contains("life-support"), "Crewed must require life support.");
        AssertTrue(crewedProfile.RequiredModuleCategories.Contains("abort-system"), "Crewed must require abort system.");
        AssertTrue(crewedProfile.RequiredModuleCategories.Contains("radiation-shielding"), "Crewed Mars must require radiation shielding.");

        // Reliability thresholds
        AssertApproxEqual(0.50, roverProfile.MinReliabilityThreshold, 0.01, "Rover threshold must match calibrated level.");
        AssertApproxEqual(0.98, crewedProfile.MinReliabilityThreshold, 0.01, "Crewed threshold must require >= 0.98.");

        // Launch pad level
        AssertEqual(3, roverProfile.RequiredLaunchPadLevel, "Rover requires Level 3 launch pad.");
        AssertEqual(5, crewedProfile.RequiredLaunchPadLevel, "Crewed Mars requires Level 5 Super Heavy launch pad.");

        // Durations
        AssertEqual(180, roverProfile.TransitDaysMin);
        AssertEqual(270, roverProfile.TransitDaysMax);
        AssertEqual(360, crewedProfile.TransitDaysMin);
        AssertEqual(540, crewedProfile.TransitDaysMax);
        AssertTrue(crewedProfile.MinDurationDays > roverProfile.MinDurationDays, "Crewed mission must span longer round-trip duration.");

        // Difficulty & Claim
        AssertTrue(crewedProfile.DifficultyScore > roverProfile.DifficultyScore, "Crewed mission difficulty must exceed rover.");
        AssertEqual("Çok Yüksek", crewedProfile.ClaimLevel, "Mars crewed claim level should be Çok Yüksek.");
    }

    static void Test_MissionComposer_LeoOrbiter_DerivesBaselineRequirements()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var destinations = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objectives = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));

        var leo = destinations.GetById("leo");
        var orbiter = types.GetById("orbiter");
        var objective = objectives.GetById("leo-global-imaging");

        var profile = MissionComposer.Compose(leo, orbiter, new[] { objective });

        AssertApproxEqual(9400.0, profile.RequiredDeltaV, 10.0, "LEO orbiter Δv must be 9400 m/s.");
        AssertFalse(profile.RequiresEdl, "Orbiter does not require EDL.");
        AssertFalse(profile.RequiresLifeSupport, "Robotic orbiter does not require life support.");
        AssertEqual(1, profile.RequiredLaunchPadLevel, "LEO requires Level 1 launch pad.");
        AssertEqual(0, profile.WindowPeriodDays, "LEO launch window is continuous (period = 0).");
        AssertApproxEqual(0.80, profile.MinReliabilityThreshold, 0.01);
        AssertTrue(profile.RequiredModuleCategories.Contains("comms"), "Orbiter requires comms.");
        AssertTrue(profile.RequiredInstrumentCategories.Contains("camera"), "Global imaging requires camera.");
        AssertEqual(1, profile.SuccessCriteria.Count, "One objective should produce one success criterion.");
        AssertEqual("data-delivered-gb", profile.SuccessCriteria[0].CriterionType);
        AssertEqual(12.0, profile.SuccessCriteria[0].Threshold);
    }

    static void Test_MissionComposer_MoonLander_WithObjectives_DerivesCompleteProfile()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var destinations = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objectives = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));

        var moon = destinations.GetById("moon");
        var lander = types.GetById("lander");
        var obj1 = objectives.GetById("moon-polar-ice-survey");
        var obj2 = objectives.GetById("moon-surface-experiment");

        var profile = MissionComposer.Compose(moon, lander, new[] { obj1, obj2 }, windowIndex: 1, budgetCeiling: 250.0);

        AssertApproxEqual(15100.0, profile.RequiredDeltaV, 20.0, "Moon lander Δv must equal 9400 + 3100 + 900 + 1700 = 15,100 m/s.");
        AssertTrue(profile.RequiresEdl, "Lander requires EDL.");
        AssertFalse(profile.RequiresLifeSupport, "Robotic lander does not require life support.");
        AssertApproxEqual(0.85, profile.MinReliabilityThreshold, 0.01);
        AssertEqual(3, profile.RequiredLaunchPadLevel, "Moon lander requires Level 3 launch pad.");
        AssertEqual(1, profile.SelectedWindowIndex, "Selected window index must be preserved.");
        AssertEqual(250.0, profile.BudgetCeiling.Value, "Budget ceiling must be preserved.");

        AssertTrue(profile.RequiredInstrumentCategories.Contains("radar"), "Survey requires radar.");
        AssertTrue(profile.RequiredInstrumentCategories.Contains("spectrometer"), "Survey requires spectrometer.");
        AssertTrue(profile.RequiredInstrumentCategories.Contains("surface-science"), "Experiment requires surface science.");

        AssertEqual(2, profile.SuccessCriteria.Count);
        AssertTrue(profile.SuccessCriteria.Any(c => c.CriterionType == "data-delivered-gb" && c.Threshold == 15.0));
        AssertTrue(profile.SuccessCriteria.Any(c => c.CriterionType == "operation-days" && c.Threshold == 14.0));
    }

    static void Test_MissionComposer_EuropaOrbiter_DerivesExtremeEnvironmentAndResearch()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var destinations = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objectives = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));

        var europa = destinations.GetById("europa");
        var orbiter = types.GetById("orbiter");
        var objective = objectives.GetById("europa-ice-radar-sounding");

        var profile = MissionComposer.Compose(europa, orbiter, new[] { objective });

        AssertApproxEqual(16700.0, profile.RequiredDeltaV, 50.0, "Europa orbiter Δv must equal 9400 + 5500 + 1800 = 16,700 m/s.");
        AssertEqual(4, profile.RequiredLaunchPadLevel, "Europa orbiter requires Level 4 pad.");

        AssertTrue(profile.RequiredResearchIds.Contains("power-rtg"), "Europa requires RTG.");
        AssertTrue(profile.RequiredResearchIds.Contains("comms-laser"), "Europa requires laser comms.");
        AssertTrue(profile.RequiredResearchIds.Contains("edl-heatshield-2"), "Europa requires heatshield research.");
        AssertTrue(profile.RequiredModuleCategories.Contains("radiation-shielding"), "Europa requires radiation shielding.");

        AssertTrue(profile.CommDelaySecondsMin > 2000.0, "Europa comm delay must exceed 2000 seconds.");
        AssertTrue(profile.TransitDaysMin > 1800, "Europa cruise must exceed 1800 days.");
    }

    static void Test_MissionComposer_SampleReturn_RoundTripDeltaVAndCriteria()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var destinations = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objectives = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));

        var moon = destinations.GetById("moon");
        var sampleReturn = types.GetById("sample-return");
        var objective = objectives.GetById("moon-sample-collection");

        var profile = MissionComposer.Compose(moon, sampleReturn, new[] { objective });

        AssertApproxEqual(17500.0, profile.RequiredDeltaV, 50.0, "Moon sample-return Δv must include landing (1700) and return (2400) = 17,500 m/s.");
        AssertApproxEqual(0.90, profile.MinReliabilityThreshold, 0.01);
        AssertEqual(4, profile.RequiredLaunchPadLevel);
        AssertTrue(profile.RequiredModuleCategories.Contains("sample-container"), "Sample return must require sample container.");
        AssertTrue(profile.SuccessCriteria.Any(c => c.CriterionType == "sample-returned"), "Must have sample-returned criterion.");
    }

    // â”€â”€ M3-03 Difficulty Calculator and Claim Level Tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    static void Test_Difficulty_ClaimLevel_BelowFive_IsLow()
    {
        var (level, label) = MissionDifficultyCalculator.ClassifyClaimLevel(3.0);
        AssertTrue(level == MissionClaimLevel.Low, "Score 3.0 must be Low.");
        AssertTrue(!string.IsNullOrEmpty(label));
    }

    static void Test_Difficulty_ClaimLevel_AtFive_IsMedium()
    {
        var (level, label) = MissionDifficultyCalculator.ClassifyClaimLevel(5.0);
        AssertTrue(level == MissionClaimLevel.Medium, "Score 5.0 must be Medium.");
        AssertEqual("Orta", label);
    }

    static void Test_Difficulty_ClaimLevel_AtThirteen_IsHigh()
    {
        var (level, label) = MissionDifficultyCalculator.ClassifyClaimLevel(13.0);
        AssertTrue(level == MissionClaimLevel.High, "Score 13.0 must be High.");
        AssertTrue(!string.IsNullOrEmpty(label));
    }

    static void Test_Difficulty_ClaimLevel_AtTwentyFive_IsExtreme()
    {
        var (level, label) = MissionDifficultyCalculator.ClassifyClaimLevel(25.0);
        AssertTrue(level == MissionClaimLevel.Extreme, "Score 25.0 must be Extreme.");
        AssertTrue(!string.IsNullOrEmpty(label));
    }

    static void Test_Difficulty_DestinationMultiplier_LEO()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        AssertApproxEqual(1.0, MissionDifficultyCalculator.GetDestinationMultiplier(dests.GetById("leo")), 0.001);
    }

    static void Test_Difficulty_DestinationMultiplier_Mars()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        AssertApproxEqual(2.0, MissionDifficultyCalculator.GetDestinationMultiplier(dests.GetById("mars")), 0.001);
    }

    static void Test_Difficulty_DestinationMultiplier_Europa()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        AssertApproxEqual(2.5, MissionDifficultyCalculator.GetDestinationMultiplier(dests.GetById("europa")), 0.001);
    }

    static void Test_Difficulty_LEOOrbiter_NoObjectives_IsLow()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var eval = MissionDifficultyCalculator.Evaluate(dests.GetById("leo"), types.GetById("orbiter"), null);
        AssertTrue(eval.ClaimLevel == MissionClaimLevel.Low, "LEO+Orbiter no objectives must be Low.");
        AssertTrue(eval.DifficultyScore >= 0.9 && eval.DifficultyScore <= 1.5, "Baseline score should be ~1.0.");
        AssertTrue(eval.RewardMultiplier >= 1.0, "Reward must be >= 1.0.");
        AssertTrue(eval.RiskMultiplier >= 1.0, "Risk must be >= 1.0.");
    }

    static void Test_Difficulty_MarsRover_OneObjective_YieldsHigh()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));
        var eval = MissionDifficultyCalculator.Evaluate(dests.GetById("mars"), types.GetById("rover"), new[] { objs.GetById("mars-rover-exploration") });
        // 6 Ã— 2.0 Ã— 1.4 = 16.8 â†’ High
        AssertApproxEqual(16.8, eval.DifficultyScore, 0.15, "Mars Rover exploration score mismatch.");
        AssertTrue(eval.ClaimLevel == MissionClaimLevel.High, "Mars Rover should be High.");
    }

    static void Test_Difficulty_MarsCrewed_TwoObjectives_YieldsExtreme()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));
        var eval = MissionDifficultyCalculator.Evaluate(
            dests.GetById("mars"), types.GetById("crewed"),
            new[] { objs.GetById("mars-life-signatures"), objs.GetById("mars-sample-preparation") });
        // (7+7) Ã— 2.0 Ã— 2.5 = 70.0 â†’ Extreme
        AssertTrue(eval.DifficultyScore > 25.0, "Crewed Mars 2-obj must exceed 25.");
        AssertTrue(eval.ClaimLevel == MissionClaimLevel.Extreme, "Must be Extreme.");
    }

    static void Test_Difficulty_EuropaOrbiter_HigherMultipliers()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));
        var orbiter = types.GetById("orbiter");
        var evalLEO = MissionDifficultyCalculator.Evaluate(dests.GetById("leo"), orbiter, null);
        var evalEuropa = MissionDifficultyCalculator.Evaluate(dests.GetById("europa"), orbiter, new[] { objs.GetById("europa-ice-radar-sounding") });
        AssertTrue(evalEuropa.RewardMultiplier > evalLEO.RewardMultiplier, "Europa reward > LEO reward.");
        AssertTrue(evalEuropa.RiskMultiplier > evalLEO.RiskMultiplier, "Europa risk > LEO risk.");
    }

    static void Test_Difficulty_RewardScalesMonotonically()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));
        var orbiter = types.GetById("orbiter");
        var crewed = types.GetById("crewed");
        var evalA = MissionDifficultyCalculator.Evaluate(dests.GetById("leo"), orbiter, null);
        var evalB = MissionDifficultyCalculator.Evaluate(dests.GetById("leo"), orbiter, new[] { objs.GetById("leo-climate-measurement") });
        var evalC = MissionDifficultyCalculator.Evaluate(dests.GetById("mars"), orbiter, new[] { objs.GetById("mars-surface-mapping") });
        var evalD = MissionDifficultyCalculator.Evaluate(dests.GetById("mars"), crewed, new[] { objs.GetById("mars-life-signatures") });
        AssertTrue(evalA.RewardMultiplier <= evalB.RewardMultiplier, "Reward monotonic: A<=B.");
        AssertTrue(evalB.RewardMultiplier <= evalC.RewardMultiplier, "Reward monotonic: B<=C.");
        AssertTrue(evalC.RewardMultiplier <= evalD.RewardMultiplier, "Reward monotonic: C<=D.");
    }

    static void Test_Difficulty_RiskScalesMonotonically()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));
        var evalA = MissionDifficultyCalculator.Evaluate(dests.GetById("leo"), types.GetById("orbiter"), null);
        var evalB = MissionDifficultyCalculator.Evaluate(dests.GetById("mars"), types.GetById("orbiter"), new[] { objs.GetById("mars-atmosphere-sampling") });
        var evalC = MissionDifficultyCalculator.Evaluate(dests.GetById("mars"), types.GetById("crewed"), new[] { objs.GetById("mars-life-signatures") });
        AssertTrue(evalA.RiskMultiplier <= evalB.RiskMultiplier, "Risk monotonic: A<=B.");
        AssertTrue(evalB.RiskMultiplier <= evalC.RiskMultiplier, "Risk monotonic: B<=C.");
    }

    static void Test_Difficulty_SummaryStrings_NonEmpty()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var eval = MissionDifficultyCalculator.Evaluate(dests.GetById("leo"), types.GetById("orbiter"), null);
        AssertTrue(!string.IsNullOrWhiteSpace(eval.RewardSummary), "RewardSummary must not be blank.");
        AssertTrue(!string.IsNullOrWhiteSpace(eval.RiskSummary), "RiskSummary must not be blank.");
        AssertTrue(!string.IsNullOrWhiteSpace(eval.CombinedSummary), "CombinedSummary must not be blank.");
    }

    static void Test_Difficulty_Europa_RiskSummary_ContainsRadiation()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));
        var eval = MissionDifficultyCalculator.Evaluate(dests.GetById("europa"), types.GetById("orbiter"), new[] { objs.GetById("europa-ice-radar-sounding") });
        AssertTrue(eval.RiskSummary.Contains("Radyasyon"), "Europa risk summary must contain 'Radyasyon'.");
    }

    static void Test_Difficulty_Compose_Profile_CarriesFullEval()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));
        var profile = MissionComposer.Compose(dests.GetById("mars"), types.GetById("rover"), new[] { objs.GetById("mars-rover-exploration") }, missionName: "TestRover");
        AssertTrue(profile.ClaimLevelType != MissionClaimLevel.Low, "Mars Rover must not be Low.");
        AssertTrue(profile.RewardMultiplier > 1.0, "RewardMultiplier > 1.");
        AssertTrue(profile.RiskMultiplier > 1.0, "RiskMultiplier > 1.");
        AssertTrue(!string.IsNullOrWhiteSpace(profile.RewardSummary), "RewardSummary must not be blank.");
        AssertTrue(!string.IsNullOrWhiteSpace(profile.RiskSummary), "RiskSummary must not be blank.");
    }

    static void Test_Difficulty_Compose_LEO_IsLowAndConsistent()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var leo = dests.GetById("leo"); var orbiter = types.GetById("orbiter");
        var profile = MissionComposer.Compose(leo, orbiter, null, missionName: "LEO Baseline");
        var eval = MissionDifficultyCalculator.Evaluate(leo, orbiter, null);
        AssertTrue(profile.ClaimLevelType == MissionClaimLevel.Low, "LEO Baseline must be Low.");
        AssertTrue(!string.IsNullOrEmpty(profile.ClaimLevel));
        AssertApproxEqual(eval.DifficultyScore, profile.DifficultyScore, 0.01, "Profile difficulty must match Evaluate().");
    }

    static void Test_Difficulty_Compose_ChangesWhenObjectivesAdded()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));
        var moon = dests.GetById("moon"); var orbiter = types.GetById("orbiter");
        var pEmpty = MissionComposer.Compose(moon, orbiter, null);
        var pWithObj = MissionComposer.Compose(moon, orbiter, new[] { objs.GetById("moon-surface-mapping") });
        AssertTrue(pWithObj.DifficultyScore > pEmpty.DifficultyScore, "Adding objectives must increase difficulty.");
        AssertTrue(pWithObj.RewardMultiplier > pEmpty.RewardMultiplier, "Adding objectives must increase reward.");
    }

    static void Test_Difficulty_Formula_MoonRover2Obj()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));
        var eval = MissionDifficultyCalculator.Evaluate(
            dests.GetById("moon"), types.GetById("rover"),
            new[] { objs.GetById("moon-polar-ice-survey"), objs.GetById("moon-surface-experiment") });
        // (4+4) Ã— 1.3 Ã— 1.4 = 14.56 â†’ High
        AssertApproxEqual(14.6, eval.DifficultyScore, 0.15, "Moon Rover 2-obj score mismatch.");
        AssertTrue(eval.ClaimLevel == MissionClaimLevel.High, "Moon Rover 2-obj must be High.");
    }

    // â”€â”€ M3-03 Claim ViewModel & Presentation Tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    static void Test_UI_ClaimViewModel_InitialState()
    {
        using var vm = new MissionClaimViewModel();
        AssertTrue(vm.ClaimLevelEnum.Value == MissionClaimLevel.Low, "Default claim level must be Low.");
        AssertTrue(vm.BadgeCssClass.Value == "claim-badge--low", "Default badge class must be claim-badge--low.");
        AssertTrue(vm.DifficultyScore.Value >= 1.0, "Score >= 1.0.");
        AssertTrue(vm.RewardMultiplier.Value >= 1.0, "RewardMultiplier >= 1.0.");
        AssertTrue(vm.RiskMultiplier.Value >= 1.0, "RiskMultiplier >= 1.0.");
        AssertTrue(vm.CurrentEvaluation.Value != null, "CurrentEvaluation must not be null.");
    }

    static void Test_UI_ClaimViewModel_SetDestination()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));

        using var vm = new MissionClaimViewModel(initialDestination: dests.GetById("leo"), initialMissionType: types.GetById("orbiter"));
        double leoScore = vm.DifficultyScore.Value;

        int notified = 0;
        using var sub = vm.DifficultyScore.Subscribe(_ => notified++, notifyImmediately: false);

        vm.SetDestination(dests.GetById("mars"));

        AssertTrue(notified == 1, "Changing destination must notify subscriber.");
        AssertTrue(vm.DifficultyScore.Value > leoScore, "Mars difficulty must exceed LEO.");
        AssertTrue(vm.Destination.Value.Id == "mars", "Destination.Value must be mars.");
    }

    static void Test_UI_ClaimViewModel_SetMissionType()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));

        using var vm = new MissionClaimViewModel(initialDestination: dests.GetById("moon"), initialMissionType: types.GetById("orbiter"));
        double orbiterReward = vm.RewardMultiplier.Value;
        double orbiterRisk = vm.RiskMultiplier.Value;

        vm.SetMissionType(types.GetById("rover"));

        AssertTrue(vm.RewardMultiplier.Value > orbiterReward, "Rover reward must exceed orbiter.");
        AssertTrue(vm.RiskMultiplier.Value > orbiterRisk, "Rover risk must exceed orbiter.");
    }

    static void Test_UI_ClaimViewModel_ToggleObjective()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));

        using var vm = new MissionClaimViewModel(initialDestination: dests.GetById("mars"), initialMissionType: types.GetById("rover"));
        double baselineScore = vm.DifficultyScore.Value;
        var obj = objs.GetById("mars-rover-exploration");

        // Toggle on
        vm.ToggleObjective(obj);
        AssertTrue(vm.SelectedObjectives.Value.Count == 1, "Count must be 1.");
        AssertTrue(vm.DifficultyScore.Value > baselineScore, "Score must increase.");
        AssertTrue(vm.ClaimLevelEnum.Value == MissionClaimLevel.High, "Mars Rover exploration should be High.");
        AssertTrue(vm.BadgeCssClass.Value == "claim-badge--high", "Badge must be claim-badge--high.");

        // Toggle off
        vm.ToggleObjective(obj);
        AssertTrue(vm.SelectedObjectives.Value.Count == 0, "Count must be 0.");
        AssertApproxEqual(baselineScore, vm.DifficultyScore.Value, 0.05, "Score must return to baseline.");
    }

    static void Test_UI_ClaimViewModel_FullReactiveLifecycle()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));

        using var vm = new MissionClaimViewModel(initialDestination: dests.GetById("leo"), initialMissionType: types.GetById("orbiter"));
        AssertTrue(vm.ClaimLevelEnum.Value == MissionClaimLevel.Low, "Starts low.");

        vm.SetDestination(dests.GetById("mars"));
        vm.SetMissionType(types.GetById("crewed"));
        var obj1 = objs.GetById("mars-life-signatures");
        var obj2 = objs.GetById("mars-sample-preparation");
        vm.AddObjective(obj1);
        vm.AddObjective(obj2);

        AssertTrue(vm.ClaimLevelEnum.Value == MissionClaimLevel.Extreme, "Mars crewed with 2 hard objectives must be Extreme.");
        AssertTrue(vm.BadgeCssClass.Value == "claim-badge--extreme", "Badge class must be claim-badge--extreme.");
        AssertTrue(vm.RewardMultiplier.Value > 4.0, "RewardMultiplier must scale high.");

        vm.RemoveObjective(obj2.Id);
        AssertTrue(vm.SelectedObjectives.Value.Count == 1, "Count is 1.");

        vm.ClearObjectives();
        AssertTrue(vm.SelectedObjectives.Value.Count == 0, "Cleared count is 0.");
        AssertTrue(vm.DifficultyScore.Value < 25.0, "Score drops when objectives cleared.");
    }

    // ── M3-04 Mission Composer Wizard Tests ─────────────────────────────────

    static void Test_UI_ComposerViewModel_InitialState_Defaults()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));

        using var vm = new MissionComposerViewModel(destinations: dests.All, missionTypes: types.All, objectives: objs.All);

        AssertTrue(vm.CurrentStep.Value == ComposerStep.Destination, "Default step must be Destination.");
        AssertTrue(vm.CurrentStepIndex.Value == 0, "Default step index must be 0.");
        AssertTrue(!vm.CanGoPrevious.Value, "CanGoPrevious must be false on step 0.");
        AssertTrue(vm.CanGoNext.Value, "CanGoNext must be true when destination is selected.");
        AssertTrue(vm.SelectedDestination.Value != null, "Default destination should not be null.");
        AssertTrue(vm.SelectedMissionType.Value != null, "Default mission type should not be null.");
        AssertTrue(vm.SelectedDestination.Value.Id == "moon", "Default destination should be moon.");
        AssertTrue(vm.SelectedMissionType.Value.Id == "rover", "Default mission type should be rover.");
        AssertTrue(vm.CanFinalize.Value, "CanFinalize should be true with default valid selections.");
    }

    static void Test_UI_ComposerViewModel_StepNavigation_ForwardAndBackward()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));

        using var vm = new MissionComposerViewModel(destinations: dests.All, missionTypes: types.All, objectives: objs.All);

        AssertTrue(vm.CurrentStep.Value == ComposerStep.Destination, "Starts at Destination.");

        vm.NextStep();
        AssertTrue(vm.CurrentStep.Value == ComposerStep.MissionType, "Advanced to MissionType.");
        AssertTrue(vm.CurrentStepIndex.Value == 1, "Index is 1.");
        AssertTrue(vm.CanGoPrevious.Value, "Can go previous from step 1.");

        vm.NextStep();
        AssertTrue(vm.CurrentStep.Value == ComposerStep.Objectives, "Advanced to Objectives.");
        AssertTrue(vm.CurrentStepIndex.Value == 2, "Index is 2.");

        vm.NextStep();
        AssertTrue(vm.CurrentStep.Value == ComposerStep.ScheduleAndBudget, "Advanced to ScheduleAndBudget.");
        AssertTrue(vm.CurrentStepIndex.Value == 3, "Index is 3.");
        AssertTrue(!vm.CanGoNext.Value, "CanGoNext is false on last step.");
        AssertTrue(vm.CanFinalize.Value, "CanFinalize is true on last step.");

        vm.PreviousStep();
        AssertTrue(vm.CurrentStep.Value == ComposerStep.Objectives, "Stepped back to Objectives.");

        vm.GoToStep(ComposerStep.Destination);
        AssertTrue(vm.CurrentStep.Value == ComposerStep.Destination, "Jumped back to Destination.");
        AssertTrue(vm.CurrentStepIndex.Value == 0, "Index is 0.");
    }

    static void Test_UI_ComposerViewModel_SelectDestination_DerivesEstimates()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));

        using var vm = new MissionComposerViewModel(destinations: dests.All, missionTypes: types.All, objectives: objs.All);
        var leo = dests.GetById("leo");
        vm.SelectDestination(leo);
        double leoDv = vm.EstimatedDeltaV.Value;

        var mars = dests.GetById("mars");
        vm.SelectDestination(mars);

        AssertTrue(vm.SelectedDestination.Value.Id == "mars", "Mars selected.");
        AssertTrue(vm.ClaimViewModel.Destination.Value.Id == "mars", "Claim destination updated.");
        AssertTrue(vm.EstimatedDeltaV.Value > leoDv, "Mars delta-v must exceed LEO.");
        AssertTrue(vm.EstimatedTransitRange.Value.Contains("180"), "Transit range reflects Mars.");
        AssertTrue(vm.MissionName.Value.Contains("MARS"), "Default name reflects Mars.");
    }

    static void Test_UI_ComposerViewModel_SelectMissionType_CrewedVsRover()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));

        using var vm = new MissionComposerViewModel(destinations: dests.All, missionTypes: types.All, objectives: objs.All);
        var mars = dests.GetById("mars");
        vm.SelectDestination(mars);

        var roverType = types.GetById("rover");
        vm.SelectMissionType(roverType);
        double roverDv = vm.EstimatedDeltaV.Value;

        var crewedType = types.GetById("crewed");
        vm.SelectMissionType(crewedType);
        double crewedDv = vm.EstimatedDeltaV.Value;

        AssertTrue(vm.SelectedMissionType.Value.Id == "crewed", "Crewed type selected.");
        AssertApproxEqual(19200.0, crewedDv, 50.0, "Mars crewed requires 19,200 m/s.");
        AssertTrue(crewedDv > roverDv + 5000.0, "Crewed must exceed rover delta-v by >5000 m/s.");
        AssertTrue(vm.EstimatedReliabilityThreshold.Value >= 0.98, "Crewed reliability threshold must be >= 98%.");
        AssertTrue(vm.EstimatedLaunchPadLevel.Value == 5, "Mars crewed requires pad level 5.");
        AssertTrue(vm.ClaimViewModel.RewardMultiplier.Value > 2.0, "Reward multiplier scales high.");
    }

    static void Test_UI_ComposerViewModel_Objectives_FilteringAndPruning()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));

        using var vm = new MissionComposerViewModel(destinations: dests.All, missionTypes: types.All, objectives: objs.All);
        var mars = dests.GetById("mars");
        var roverType = types.GetById("rover");
        vm.SelectDestination(mars);
        vm.SelectMissionType(roverType);

        var compatible = vm.CompatibleObjectives.Value;
        AssertTrue(compatible.Any(o => o.Id == "mars-rover-exploration"), "Compatible must include mars-rover-exploration.");

        var marsObj = compatible.First(o => o.Id == "mars-rover-exploration");
        vm.ToggleObjective(marsObj);
        AssertTrue(vm.SelectedObjectives.Value.Count == 1, "Selected count is 1.");
        AssertTrue(vm.SelectedObjectives.Value[0].Id == "mars-rover-exploration", "Selected obj matches.");

        var leo = dests.GetById("leo");
        vm.SelectDestination(leo);

        AssertTrue(vm.SelectedObjectives.Value.Count == 0, "Mars objective must be pruned when switched to LEO.");
        AssertTrue(!vm.CompatibleObjectives.Value.Any(o => o.Id == "mars-rover-exploration"), "Mars obj not compatible with LEO.");
    }

    static void Test_UI_ComposerViewModel_Objectives_SelectAllAndClear()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));

        using var vm = new MissionComposerViewModel(destinations: dests.All, missionTypes: types.All, objectives: objs.All);
        var moon = dests.GetById("moon");
        var roverType = types.GetById("rover");
        vm.SelectDestination(moon);
        vm.SelectMissionType(roverType);

        int count = vm.CompatibleObjectives.Value.Count;
        AssertTrue(count > 0, "Moon rover must have compatible objectives.");

        vm.SelectAllCompatibleObjectives();
        AssertTrue(vm.SelectedObjectives.Value.Count == count, "SelectAll selects all compatible.");

        vm.ClearSelectedObjectives();
        AssertTrue(vm.SelectedObjectives.Value.Count == 0, "Clear empties selection.");
    }

    static void Test_UI_ComposerViewModel_ScheduleAndBudget_CustomSettings()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));

        using var vm = new MissionComposerViewModel(destinations: dests.All, missionTypes: types.All, objectives: objs.All);
        var moon = dests.GetById("moon");
        vm.SelectDestination(moon);

        vm.SetWindowIndex(1);
        AssertTrue(vm.SelectedWindowIndex.Value == 1, "Window index is 1.");
        AssertTrue(vm.LaunchWindowText.Value.Contains("Pencere 1"), "Launch window text contains Pencere 1.");

        vm.SetBudgetCeiling(75.5);
        AssertTrue(vm.BudgetCeiling.Value == 75.5, "Budget ceiling is 75.5.");

        vm.SetMissionName("LUNA-EXPLORER-X");
        AssertTrue(vm.MissionName.Value == "LUNA-EXPLORER-X", "Mission name updated.");
    }

    static void Test_UI_ComposerViewModel_ComposeMissionProfile_ProducesValidProfile()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));

        using var vm = new MissionComposerViewModel(destinations: dests.All, missionTypes: types.All, objectives: objs.All);
        var moon = dests.GetById("moon");
        var roverType = types.GetById("rover");
        vm.SelectDestination(moon);
        vm.SelectMissionType(roverType);
        vm.SelectAllCompatibleObjectives();

        MissionProfile fired = null;
        vm.OnMissionComposed += p => fired = p;

        var profile = vm.ComposeMissionProfile();

        AssertTrue(profile != null, "Profile must not be null.");
        AssertTrue(object.ReferenceEquals(profile, fired), "Event must fire with composed profile.");
        AssertTrue(object.ReferenceEquals(profile, vm.ComposedProfile.Value), "ComposedProfile must hold profile.");
        AssertApproxEqual(15100.0, profile.RequiredDeltaV, 50.0, "Moon rover requires ~15,100 m/s.");
        AssertTrue(profile.RequiresEdl, "Rover requires EDL.");
        AssertTrue(profile.RequiredModuleCategories.Contains("edl"), "Required modules contains edl.");
        AssertTrue(profile.SuccessCriteria.Count > 0, "Success criteria must be generated.");
    }

    static void Test_UI_ComposerViewModel_ComposeMissionProfile_ThrowsWhenIncomplete()
    {
        using var vm = new MissionComposerViewModel(
            destinations: Array.Empty<DestinationRecord>(),
            missionTypes: Array.Empty<MissionTypeRecord>(),
            objectives: Array.Empty<ObjectiveRecord>()
        );

        bool threw = false;
        try
        {
            vm.ComposeMissionProfile();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        AssertTrue(threw, "Composing without destination must throw InvalidOperationException.");
    }

    static void Test_UI_ComposerViewModel_NavigationIntegration_DispatchesArgs()
    {
        string jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
        var dests = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
        var types = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
        var objs = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));

        var nav = new ScreenNavigator();
        using var vm = new MissionComposerViewModel(navigator: nav, destinations: dests.All, missionTypes: types.All, objectives: objs.All);

        bool dispatched = false;
        object receivedArgs = null;

        var dummy = new TestDummyScreen(args =>
        {
            dispatched = true;
            receivedArgs = args;
        });
        nav.RegisterScreen(dummy);

        vm.ComposeMissionProfile();

        AssertTrue(dispatched, "Screen transition must occur.");
        AssertTrue(receivedArgs is MissionCompositionResultArgs, "Args must be MissionCompositionResultArgs.");
        var rArgs = (MissionCompositionResultArgs)receivedArgs;
        AssertTrue(rArgs.Profile != null, "Profile must not be null.");
        AssertTrue(rArgs.Destination.Id == "moon", "Destination is moon.");
        AssertTrue(rArgs.MissionType.Id == "rover", "Type is rover.");
    }

    private sealed class TestDummyScreen : IScreenController
    {
        private readonly Action<object> _callback;
        public ScreenId ScreenId => ScreenId.MissionBrief;
        public VisualElement RootElement => null;
        public bool IsVisible => true;

        public TestDummyScreen(Action<object> callback) => _callback = callback;
        public void Initialize(VisualElement parent) { }
        public void OnEnter(object context = null) => _callback?.Invoke(context);
        public void OnExit() { }
        public void Dispose() { }
    }
}
}