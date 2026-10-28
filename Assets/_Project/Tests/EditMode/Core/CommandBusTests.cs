using System.Collections.Generic;
using NUnit.Framework;
using Apogee.Core;

namespace Apogee.Tests
{
    [TestFixture]
    public class CommandBusTests
    {
        // Sample mutable state for testing
        public class TestState
        {
            public int Counter { get; set; }
            public List<string> Log { get; } = new List<string>();
        }

        // Test command that increments a counter
        private class AddCountCommand : CommandBase<TestState>
        {
            private readonly int _amount;

            public override PlayerRole RequiredRoles { get; }

            public AddCountCommand(int amount, PlayerRole requiredRoles = PlayerRole.All)
            {
                _amount = amount;
                RequiredRoles = requiredRoles;
            }

            protected override CommandValidationResult CanExecute(TestState state, CommandContext context)
            {
                if (_amount == 0)
                {
                    return CommandValidationResult.Fail("Amount cannot be zero.");
                }
                return CommandValidationResult.Success();
            }

            protected override CommandExecutionResult Execute(TestState state, CommandContext context)
            {
                state.Counter += _amount;
                state.Log.Add($"Added {_amount}");
                return CommandExecutionResult.Succeeded($"Counter is now {state.Counter}");
            }

            protected override CommandExecutionResult Undo(TestState state, CommandContext context)
            {
                state.Counter -= _amount;
                state.Log.Add($"Undid {_amount}");
                return CommandExecutionResult.Succeeded($"Reverted counter to {state.Counter}");
            }
        }

        // Test command that cannot be undone
        private class PermanentLogCommand : CommandBase<TestState>
        {
            private readonly string _entry;
            public override bool CanUndo => false;

            public PermanentLogCommand(string entry)
            {
                _entry = entry;
            }

            protected override CommandValidationResult CanExecute(TestState state, CommandContext context)
            {
                return CommandValidationResult.Success();
            }

            protected override CommandExecutionResult Execute(TestState state, CommandContext context)
            {
                state.Log.Add(_entry);
                return CommandExecutionResult.Succeeded();
            }
        }

        private CommandBus _bus;
        private TestState _state;
        private CommandContext _adminContext;

        [SetUp]
        public void SetUp()
        {
            _bus = new CommandBus();
            _state = new TestState();
            _adminContext = CommandContext.CreateLocalAdmin(_state, tick: 42);
        }

        [Test]
        public void Execute_AuthorizedRole_ExecutesSuccessfullyAndMutatesState()
        {
            var cmd = new AddCountCommand(5, PlayerRole.LeadEngineer);
            var engineerContext = new CommandContext("player1", PlayerRole.LeadEngineer, tick: 10, state: _state);

            CommandExecutionResult result = _bus.Execute(cmd, engineerContext);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(5, _state.Counter);
            Assert.AreEqual(1, _bus.UndoCount);
            Assert.AreEqual(1, _bus.History.Count);
        }

        [Test]
        public void Execute_UnauthorizedRole_RejectsCommandWithReasonAndDoesNotMutateState()
        {
            var cmd = new AddCountCommand(10, PlayerRole.AgencyDirector);
            var scienceContext = new CommandContext("player2", PlayerRole.ScienceDirector, tick: 10, state: _state);

            bool rejectedFired = false;
            string rejectReason = null;
            _bus.CommandRejected += (c, reason) =>
            {
                rejectedFired = true;
                rejectReason = reason;
            };

            CommandExecutionResult result = _bus.Execute(cmd, scienceContext);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("UNAUTHORIZED", result.ErrorCode);
            Assert.IsTrue(rejectedFired);
            Assert.IsNotNull(rejectReason);
            Assert.AreEqual(0, _state.Counter);
            Assert.AreEqual(0, _bus.UndoCount);
            Assert.AreEqual(0, _bus.History.Count);
        }

        [Test]
        public void Execute_FailedValidation_RejectsCommandWithValidationReason()
        {
            var cmd = new AddCountCommand(0); // 0 amount fails validation

            CommandExecutionResult result = _bus.Execute(cmd, _adminContext);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("VALIDATION_FAILED", result.ErrorCode);
            Assert.AreEqual("Amount cannot be zero.", result.Message);
            Assert.AreEqual(0, _state.Counter);
            Assert.AreEqual(0, _bus.UndoCount);
        }

        [Test]
        public void Undo_ExecutedCommand_ReversesMutationsSymmetrically()
        {
            var cmd = new AddCountCommand(7);
            _bus.Execute(cmd, _adminContext);
            Assert.AreEqual(7, _state.Counter);

            CommandExecutionResult undoResult = _bus.Undo(_adminContext);

            Assert.IsTrue(undoResult.Success);
            Assert.AreEqual(0, _state.Counter);
            Assert.AreEqual(0, _bus.UndoCount);
            Assert.AreEqual(1, _bus.RedoCount);
            Assert.IsTrue(_bus.CanRedo);
            Assert.IsFalse(_bus.CanUndo);
        }

        [Test]
        public void Undo_EmptyStack_ReturnsFailedResult()
        {
            CommandExecutionResult result = _bus.Undo(_adminContext);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("STACK_EMPTY", result.ErrorCode);
        }

        [Test]
        public void Redo_UndoneCommand_ReAppliesMutations()
        {
            var cmd = new AddCountCommand(15);
            _bus.Execute(cmd, _adminContext);
            _bus.Undo(_adminContext);
            Assert.AreEqual(0, _state.Counter);

            CommandExecutionResult redoResult = _bus.Redo(_adminContext);

            Assert.IsTrue(redoResult.Success);
            Assert.AreEqual(15, _state.Counter);
            Assert.AreEqual(1, _bus.UndoCount);
            Assert.AreEqual(0, _bus.RedoCount);
        }

        [Test]
        public void Redo_NewCommandAfterUndo_ClearsRedoStack()
        {
            _bus.Execute(new AddCountCommand(10), _adminContext);
            _bus.Undo(_adminContext);
            Assert.AreEqual(1, _bus.RedoCount);

            // Executing a new command branch must purge the forward redo history
            _bus.Execute(new AddCountCommand(20), _adminContext);

            Assert.AreEqual(0, _bus.RedoCount);
            Assert.IsFalse(_bus.CanRedo);
            Assert.AreEqual(20, _state.Counter);
        }

        [Test]
        public void UndoRedo_MultipleCommandsInSequence_OperatesInExactLIFOOrder()
        {
            _bus.Execute(new AddCountCommand(1), _adminContext);
            _bus.Execute(new AddCountCommand(2), _adminContext);
            _bus.Execute(new AddCountCommand(3), _adminContext);

            Assert.AreEqual(6, _state.Counter);
            Assert.AreEqual(3, _bus.UndoCount);

            // Undo 3
            _bus.Undo(_adminContext);
            Assert.AreEqual(3, _state.Counter);

            // Undo 2
            _bus.Undo(_adminContext);
            Assert.AreEqual(1, _state.Counter);

            // Redo 2
            _bus.Redo(_adminContext);
            Assert.AreEqual(3, _state.Counter);

            // Undo 2
            _bus.Undo(_adminContext);
            Assert.AreEqual(1, _state.Counter);

            // Undo 1
            _bus.Undo(_adminContext);
            Assert.AreEqual(0, _state.Counter);
        }

        [Test]
        public void CommandRecord_AuditTrail_StoresExpectedMetadata()
        {
            var context = new CommandContext("player-alice", PlayerRole.ProcurementDirector, tick: 105, state: _state);
            var cmd = new AddCountCommand(50, PlayerRole.ProcurementDirector);

            _bus.Execute(cmd, context);

            Assert.AreEqual(1, _bus.History.Count);
            CommandRecord record = _bus.History[0];

            Assert.AreEqual(1UL, record.SequenceNumber);
            Assert.AreEqual(cmd.Id, record.CommandId);
            Assert.AreEqual("AddCountCommand", record.CommandType);
            Assert.AreEqual(PlayerRole.ProcurementDirector, record.RequiredRoles);
            Assert.AreEqual(PlayerRole.ProcurementDirector, record.ActorRole);
            Assert.AreEqual("player-alice", record.PlayerId);
            Assert.AreEqual(105, record.Tick);
        }

        [Test]
        public void NonUndoableCommand_ExecutesWithoutEnteringUndoStack()
        {
            var cmd = new PermanentLogCommand("System initialized");

            CommandExecutionResult result = _bus.Execute(cmd, _adminContext);

            Assert.IsTrue(result.Success);
            Assert.Contains("System initialized", _state.Log);
            Assert.AreEqual(0, _bus.UndoCount);
            Assert.AreEqual(1, _bus.History.Count);
        }

        [Test]
        public void MaxHistorySize_TrimsOldestCommandsWhenLimitExceeded()
        {
            var smallBus = new CommandBus(maxHistorySize: 3);

            for (int i = 1; i <= 5; i++)
            {
                smallBus.Execute(new AddCountCommand(i), _adminContext);
            }

            Assert.AreEqual(3, smallBus.UndoCount);
            // Should be able to undo only the 3 latest commands (5, 4, 3)
            smallBus.Undo(_adminContext); // undo 5
            smallBus.Undo(_adminContext); // undo 4
            smallBus.Undo(_adminContext); // undo 3

            Assert.AreEqual(0, smallBus.UndoCount);
            Assert.AreEqual(3, _state.Counter); // 1 + 2 remain
        }

        [Test]
        public void ClearHistory_EmptiesAllStacksAndRecords()
        {
            _bus.Execute(new AddCountCommand(10), _adminContext);
            _bus.Execute(new AddCountCommand(20), _adminContext);
            _bus.Undo(_adminContext);

            _bus.ClearHistory();

            Assert.AreEqual(0, _bus.UndoCount);
            Assert.AreEqual(0, _bus.RedoCount);
            Assert.AreEqual(0, _bus.History.Count);
            Assert.IsFalse(_bus.CanUndo);
            Assert.IsFalse(_bus.CanRedo);
        }

        [Test]
        public void IsAuthorized_MaskCombinations_EvaluatesAccurately()
        {
            // PlayerRole.All should satisfy any single requirement
            Assert.IsTrue(CommandBus.IsAuthorized(PlayerRole.All, PlayerRole.AgencyDirector));
            Assert.IsTrue(CommandBus.IsAuthorized(PlayerRole.All, PlayerRole.LeadEngineer));

            // Single role matches its own requirement
            Assert.IsTrue(CommandBus.IsAuthorized(PlayerRole.LeadEngineer, PlayerRole.LeadEngineer));

            // Disjoint roles do not match
            Assert.IsFalse(CommandBus.IsAuthorized(PlayerRole.ScienceDirector, PlayerRole.LeadEngineer));

            // Combined requirement (either role A or B)
            PlayerRole either = PlayerRole.AgencyDirector | PlayerRole.LeadEngineer;
            Assert.IsTrue(CommandBus.IsAuthorized(PlayerRole.AgencyDirector, either));
            Assert.IsTrue(CommandBus.IsAuthorized(PlayerRole.LeadEngineer, either));
            Assert.IsFalse(CommandBus.IsAuthorized(PlayerRole.ProcurementDirector, either));

            // PlayerRole.None required means open to all
            Assert.IsTrue(CommandBus.IsAuthorized(PlayerRole.None, PlayerRole.None));
            Assert.IsTrue(CommandBus.IsAuthorized(PlayerRole.ScienceDirector, PlayerRole.None));
        }

        [Test]
        public void CommandBase_MismatchedState_ReturnsValidationFailure()
        {
            var wrongContext = new CommandContext("local", PlayerRole.All, 0, state: "some string");
            var cmd = new AddCountCommand(10);

            CommandExecutionResult result = _bus.Execute(cmd, wrongContext);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("VALIDATION_FAILED", result.ErrorCode);
            Assert.IsTrue(result.Message.Contains("Target state is not of type"));
        }
    }
}
