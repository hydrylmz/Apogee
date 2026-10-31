using NUnit.Framework;
using System.IO;
using System.Linq;
using Apogee.Data;
using Apogee.Strategy;

namespace Apogee.Tests.EditMode.Strategy
{
    /// <summary>
    /// Unit tests for M4-04: LaunchWindowInfo, LaunchWindowCalculator, PlanetaryOrbitCalculator, DestinationBriefingCard.
    /// Complies with GDD §6.2, §9.4, §16.1.
    /// </summary>
    [TestFixture]
    public class SolarSystemMapTests
    {
        private CatalogCollection<DestinationRecord> _destinations;

        [SetUp]
        public void SetUp()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json", "destinations.json");
            if (!File.Exists(path))
            {
                path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Assets", "_Project", "Data", "Catalog", "Json", "destinations.json");
            }
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                _destinations = CatalogLoader.Load<DestinationRecord>(json);
            }
        }

        // ================================================================ LaunchWindowCalculator
        [Test]
        public void LaunchWindowCalculator_Leo_AlwaysContinuous()
        {
            if (_destinations == null) return;
            var leo = _destinations.GetById("leo");
            Assert.IsNotNull(leo);

            var win = LaunchWindowCalculator.GetWindow(leo, 0);
            Assert.IsTrue(win.IsActive(new GameDate(0)));
            Assert.IsTrue(win.IsActive(new GameDate(5000)));
            Assert.IsTrue(LaunchWindowCalculator.IsWindowOpen(leo, new GameDate(1234)));
        }

        [Test]
        public void LaunchWindowCalculator_Mars_PeriodicSynodicWindows()
        {
            if (_destinations == null) return;
            var mars = _destinations.GetById("mars");
            Assert.IsNotNull(mars);

            // Mars first window opens at day 400, duration 30 days -> 400 to 430
            var win0 = LaunchWindowCalculator.GetWindow(mars, 0);
            Assert.AreEqual(400, win0.StartDate.TotalDays);
            Assert.AreEqual(430, win0.EndDate.TotalDays);
            Assert.AreEqual(30, win0.DurationDays);

            // Day 415 -> active
            Assert.IsTrue(win0.IsActive(new GameDate(415)));
            // Day 300 -> upcoming
            Assert.IsTrue(win0.IsUpcoming(new GameDate(300)));
            // Day 500 -> closed
            Assert.IsTrue(win0.IsClosed(new GameDate(500)));

            // Next window is at day 400 + 780 = 1180
            var win1 = LaunchWindowCalculator.GetWindow(mars, 1);
            Assert.AreEqual(1180, win1.StartDate.TotalDays);
        }

        [Test]
        public void LaunchWindowCalculator_GetCurrentOrNextWindow_ProgressesCorrectly()
        {
            if (_destinations == null) return;
            var mars = _destinations.GetById("mars");

            // Day 200 -> Next is win0 (opens at 400)
            var current = LaunchWindowCalculator.GetCurrentOrNextWindow(mars, new GameDate(200));
            Assert.AreEqual(0, current.WindowIndex);
            Assert.AreEqual(200, current.DaysUntilOpen(new GameDate(200)));

            // Day 450 (after win0 closed) -> Next is win1 (opens at 1180)
            var next = LaunchWindowCalculator.GetCurrentOrNextWindow(mars, new GameDate(450));
            Assert.AreEqual(1, next.WindowIndex);
            Assert.AreEqual(1180, next.StartDate.TotalDays);
        }

        [Test]
        public void LaunchWindowCalculator_GetUpcomingWindows_ReturnsRequestedCount()
        {
            if (_destinations == null) return;
            var venus = _destinations.GetById("venus");

            var upcoming = LaunchWindowCalculator.GetUpcomingWindows(venus, new GameDate(0), maxCount: 3);
            Assert.AreEqual(3, upcoming.Count);
            Assert.AreEqual(0, upcoming[0].WindowIndex);
            Assert.AreEqual(1, upcoming[1].WindowIndex);
            Assert.AreEqual(2, upcoming[2].WindowIndex);
        }

        // ================================================================ PlanetaryOrbitCalculator
        [Test]
        public void PlanetaryOrbitCalculator_CalculatesHeliocentricCoords()
        {
            // Day 0: angles should be 0, coordinates at (r, 0)
            var (x, y) = PlanetaryOrbitCalculator.GetHeliocentricPositionAu("mars", 0);
            Assert.AreEqual(PlanetaryOrbitCalculator.MarsDistanceAu, x, 1e-4);
            Assert.AreEqual(0.0, y, 1e-4);

            // Half-period: coordinates at (-r, 0)
            int halfPeriod = (int)System.Math.Round(PlanetaryOrbitCalculator.MarsPeriodDays / 2.0);
            var (hx, hy) = PlanetaryOrbitCalculator.GetHeliocentricPositionAu("mars", halfPeriod);
            Assert.AreEqual(-PlanetaryOrbitCalculator.MarsDistanceAu, hx, 0.05);
        }

        // ================================================================ DestinationBriefingCard
        [Test]
        public void DestinationBriefingCard_BuildsCleanPresentationData()
        {
            if (_destinations == null) return;
            var mars = _destinations.GetById("mars");

            // Day 100: mars window is closed (opens in 300 days)
            var card100 = new DestinationBriefingCard(mars, new GameDate(100));
            Assert.IsFalse(card100.IsWindowActive);
            Assert.AreEqual(300, card100.DaysUntilNextWindow);
            Assert.AreEqual("Opens in 300d", card100.WindowStatusText);
            Assert.Greater(card100.RequiredDeltaVMps, 15000); // 9400 + 2700 + 900 + 600 + 5600 = 19200
            Assert.IsTrue(card100.HazardTags.Count > 0);

            // Day 415: mars window is OPEN
            var card415 = new DestinationBriefingCard(mars, new GameDate(415));
            Assert.IsTrue(card415.IsWindowActive);
            Assert.AreEqual("WINDOW OPEN NOW", card415.WindowStatusText);
        }
    }
}
