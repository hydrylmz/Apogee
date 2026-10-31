using NUnit.Framework;
using Apogee.Core;
using Apogee.Strategy;

namespace Apogee.Tests.EditMode.Strategy
{
    /// <summary>
    /// Unit tests for M4-01: CampaignState, CampaignTime, GameDate, AdvanceTimeCommand, SetTimeSpeedCommand.
    /// All tests are deterministic and engine-free.
    /// </summary>
    [TestFixture]
    public class CampaignTimeTests
    {
        // ================================================================ GameDate
        [Test]
        public void GameDate_EpochIsDay0()
        {
            var d = new GameDate(0);
            Assert.AreEqual(2031, d.Year);
            Assert.AreEqual(1,    d.Month);
            Assert.AreEqual(1,    d.Day);
            Assert.AreEqual(0,    d.CampaignYear);
        }

        [Test]
        public void GameDate_AddDays_365_Reaches2032()
        {
            var d = new GameDate(0).AddDays(365);
            // 2031 is not a leap year → 365 days later = 2032-01-01
            Assert.AreEqual(2032, d.Year);
            Assert.AreEqual(1,    d.Month);
        }

        [Test]
        public void GameDate_FromCalendar_RoundTrip()
        {
            var d = GameDate.FromCalendar(2035, 6, 15);
            Assert.AreEqual(2035, d.Year);
            Assert.AreEqual(6,    d.Month);
            Assert.AreEqual(15,   d.Day);
        }

        [Test]
        public void GameDate_Comparison_Operators()
        {
            var d1 = new GameDate(0);
            var d2 = new GameDate(1);
            Assert.IsTrue(d1 < d2);
            Assert.IsTrue(d2 > d1);
            Assert.IsTrue(d1 <= d1);
            Assert.IsTrue(d1 == new GameDate(0));
            Assert.IsTrue(d1 != d2);
        }

        [Test]
        public void GameDate_DaysUntil_IsCorrect()
        {
            var d1 = new GameDate(0);
            var d2 = new GameDate(100);
            Assert.AreEqual(100, d1.DaysUntil(d2));
            Assert.AreEqual(-100, d2.DaysUntil(d1));
        }

        [Test]
        public void GameDate_ToString_IsIsoFormat()
        {
            var d = GameDate.FromCalendar(2041, 3, 7);
            Assert.AreEqual("2041-03-07", d.ToString());
        }

        [Test]
        public void GameDate_AddMonths_CrossesYearBoundary()
        {
            var d = GameDate.FromCalendar(2031, 11, 1).AddMonths(3);
            Assert.AreEqual(2032, d.Year);
            Assert.AreEqual(2,    d.Month);
        }

        [Test]
        public void GameDate_NegativeDays_ClampsToZero()
        {
            var d = new GameDate(-10);
            Assert.AreEqual(0, d.TotalDays);
        }

        // ================================================================ CampaignTime
        [Test]
        public void CampaignTime_StartsAtEpochAndPaused()
        {
            var time = new CampaignTime();
            Assert.AreEqual(new GameDate(0), time.CurrentDate);
            Assert.AreEqual(0L, time.Tick);
            Assert.AreEqual(TimeSpeed.Paused, time.Speed);
            Assert.IsFalse(time.IsExpired);
        }

        [Test]
        public void CampaignTime_AdvanceDays_UpdatesDateAndTick()
        {
            var time = new CampaignTime();
            time.AdvanceDays(10);
            Assert.AreEqual(10, time.CurrentDate.TotalDays);
            Assert.AreEqual(10L, time.Tick);
        }

        [Test]
        public void CampaignTime_AdvanceDays_ClampsToEndDate()
        {
            // Start 5 days before end
            int endTotalDays = CampaignTime.EndDate.TotalDays;
            var time = new CampaignTime(new GameDate(endTotalDays - 5), endTotalDays - 5, TimeSpeed.Paused);
            time.AdvanceDays(100); // Try to go way past end
            Assert.AreEqual(CampaignTime.EndDate, time.CurrentDate);
        }

        [Test]
        public void CampaignTime_IsExpired_AfterReachingEndDate()
        {
            int endDays = CampaignTime.EndDate.TotalDays;
            var time    = new CampaignTime(CampaignTime.EndDate, endDays, TimeSpeed.Paused);
            Assert.IsTrue(time.IsExpired);
        }

        [Test]
        public void CampaignTime_SetSpeed_ChangesSpeed()
        {
            var time = new CampaignTime();
            time.SetSpeed(TimeSpeed.Fast);
            Assert.AreEqual(TimeSpeed.Fast, time.Speed);
        }

        [Test]
        public void CampaignTime_UndoAdvance_RestoresPreviousState()
        {
            var time   = new CampaignTime();
            var before = time.CurrentDate;
            long beforeTick = time.Tick;

            time.AdvanceDays(30);
            time.UndoAdvance(before, beforeTick);

            Assert.AreEqual(before, time.CurrentDate);
            Assert.AreEqual(beforeTick, time.Tick);
        }

        // ================================================================ CampaignState
        [Test]
        public void CampaignState_Constructor_InitializesDefaults()
        {
            var state = new CampaignState(seed: 42);
            Assert.IsNotNull(state.CampaignId);
            Assert.AreEqual(42UL, state.Seed);
            Assert.AreEqual(new GameDate(0), state.Time.CurrentDate);
            Assert.Greater(state.Budget, 0.0);
            Assert.Greater(state.Reputation, 0.0);
        }

        // ================================================================ AdvanceTimeCommand
        [Test]
        public void AdvanceTimeCommand_AdvancesDateBySpecifiedDays()
        {
            var state   = new CampaignState(seed: 1);
            var command = new AdvanceTimeCommand(days: 30);

            var result = state.Execute(command);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(30, state.Time.CurrentDate.TotalDays);
            Assert.AreEqual(30L, state.Time.Tick);
        }

        [Test]
        public void AdvanceTimeCommand_FailsForZeroDays()
        {
            var state   = new CampaignState(seed: 1);
            var command = new AdvanceTimeCommand(days: 0);

            var result = state.Execute(command);

            Assert.IsFalse(result.Success);
            Assert.That(result.Message, Does.Contain("≥ 1").Or.Contain("1"));
        }

        [Test]
        public void AdvanceTimeCommand_FailsForNegativeDays()
        {
            var state   = new CampaignState(seed: 1);
            var command = new AdvanceTimeCommand(days: -5);

            var result = state.Execute(command);

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void AdvanceTimeCommand_FailsWhenCampaignExpired()
        {
            // Manually put time at end date
            int endDays = CampaignTime.EndDate.TotalDays;
            var time    = new CampaignTime(CampaignTime.EndDate, endDays, TimeSpeed.Paused);
            // CampaignState ctor doesn't accept CampaignTime directly — use helper to set via advance
            // Instead, create state and advance to end programmatically
            var state   = new CampaignState(seed: 2);
            // Force expired state by advancing a huge number of days
            state.Time.AdvanceDays(endDays + 100);

            var command = new AdvanceTimeCommand(days: 1);
            var result  = state.Execute(command);

            Assert.IsFalse(result.Success);
            Assert.That(result.Message, Does.Contain("end date").Or.Contain("2061"));
        }

        [Test]
        public void AdvanceTimeCommand_Undo_RestoresDate()
        {
            var state    = new CampaignState(seed: 3);
            var command  = new AdvanceTimeCommand(days: 15);
            var ctx      = CommandContext.CreateLocalAdmin(state, state.Time.Tick);

            state.CommandBus.Execute(command, ctx);
            Assert.AreEqual(15, state.Time.CurrentDate.TotalDays);

            state.CommandBus.Undo(ctx);
            Assert.AreEqual(0, state.Time.CurrentDate.TotalDays);
            Assert.AreEqual(0L, state.Time.Tick);
        }

        [Test]
        public void AdvanceTimeCommand_Deterministic_SameSeedSameResult()
        {
            var state1 = new CampaignState(seed: 99);
            var state2 = new CampaignState(seed: 99);

            state1.Execute(new AdvanceTimeCommand(30));
            state1.Execute(new AdvanceTimeCommand(15));

            state2.Execute(new AdvanceTimeCommand(30));
            state2.Execute(new AdvanceTimeCommand(15));

            Assert.AreEqual(state1.Time.CurrentDate, state2.Time.CurrentDate);
            Assert.AreEqual(state1.Time.Tick,        state2.Time.Tick);
        }

        [Test]
        public void AdvanceTimeCommand_MultipleSteps_TickMatchesTotalDays()
        {
            var state = new CampaignState(seed: 5);
            state.Execute(new AdvanceTimeCommand(10));
            state.Execute(new AdvanceTimeCommand(20));
            state.Execute(new AdvanceTimeCommand(5));

            Assert.AreEqual(35, state.Time.CurrentDate.TotalDays);
            Assert.AreEqual(35L, state.Time.Tick);
        }

        // ================================================================ SetTimeSpeedCommand
        [Test]
        public void SetTimeSpeedCommand_ChangesSpeed()
        {
            var state   = new CampaignState(seed: 1);
            var command = new SetTimeSpeedCommand(TimeSpeed.Fast);

            var result = state.Execute(command);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(TimeSpeed.Fast, state.Time.Speed);
        }

        [Test]
        public void SetTimeSpeedCommand_Undo_RestoresPreviousSpeed()
        {
            var state = new CampaignState(seed: 1);
            var ctx   = CommandContext.CreateLocalAdmin(state, state.Time.Tick);

            state.CommandBus.Execute(new SetTimeSpeedCommand(TimeSpeed.VeryFast), ctx);
            Assert.AreEqual(TimeSpeed.VeryFast, state.Time.Speed);

            state.CommandBus.Undo(ctx);
            Assert.AreEqual(TimeSpeed.Paused, state.Time.Speed);
        }

        [Test]
        public void SetTimeSpeedCommand_AllSpeedValues_Accepted()
        {
            var state = new CampaignState(seed: 1);
            foreach (TimeSpeed speed in System.Enum.GetValues(typeof(TimeSpeed)))
            {
                var result = state.Execute(new SetTimeSpeedCommand(speed));
                Assert.IsTrue(result.Success, $"Speed {speed} should be valid.");
                Assert.AreEqual(speed, state.Time.Speed);
            }
        }

        // ================================================================ determinism end-to-end
        [Test]
        public void CampaignState_SameCommandSequence_ProducesSameState()
        {
            CampaignState BuildState()
            {
                var s = new CampaignState(seed: 777);
                s.Execute(new SetTimeSpeedCommand(TimeSpeed.Normal));
                s.Execute(new AdvanceTimeCommand(365));
                s.Execute(new AdvanceTimeCommand(30));
                s.Execute(new SetTimeSpeedCommand(TimeSpeed.Paused));
                return s;
            }

            var a = BuildState();
            var b = BuildState();

            Assert.AreEqual(a.Time.CurrentDate, b.Time.CurrentDate);
            Assert.AreEqual(a.Time.Tick,        b.Time.Tick);
            Assert.AreEqual(a.Time.Speed,       b.Time.Speed);
        }
    }
}
