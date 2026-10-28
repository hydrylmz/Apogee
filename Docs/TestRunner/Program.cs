using System;
using System.Collections.Generic;
using Apogee.Core;
using Apogee.Design;

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
    }
}
