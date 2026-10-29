using Apogee.UI.Core.Tooltips;
using NUnit.Framework;
using UnityEngine;

namespace Apogee.Tests.UI
{
    [TestFixture]
    public class TooltipTests
    {
        [Test]
        public void TooltipData_Creation_PopulatesAllFields()
        {
            var data = new TooltipData(
                "Menzil",
                "Toplam Delta-V: 12,000 m/s",
                "Yörüngeye ulaşmak için gereken hız değişimi.",
                "+500 m/s",
                true,
                TooltipSeverity.Success);

            Assert.AreEqual("Menzil", data.Title);
            Assert.AreEqual("Toplam Delta-V: 12,000 m/s", data.Content);
            Assert.AreEqual("Yörüngeye ulaşmak için gereken hız değişimi.", data.Reason);
            Assert.AreEqual("+500 m/s", data.DeltaText);
            Assert.AreEqual(true, data.IsPositiveDelta);
            Assert.AreEqual(TooltipSeverity.Success, data.Severity);
        }

        [Test]
        public void TooltipService_ShowTooltip_FiresRequestedEvent()
        {
            var service = new TooltipService();
            TooltipData receivedData = null;
            Vector2 receivedPos = Vector2.zero;

            service.OnTooltipRequested += (data, pos) =>
            {
                receivedData = data;
                receivedPos = pos;
            };

            var payload = TooltipData.CreateSimple("Test", "Test Content");
            service.ShowTooltip(payload, new Vector2(100f, 200f));

            Assert.IsTrue(service.IsVisible);
            Assert.AreEqual(payload, service.CurrentData);
            Assert.AreEqual(payload, receivedData);
            Assert.AreEqual(new Vector2(100f, 200f), receivedPos);
        }

        [Test]
        public void TooltipService_HideTooltip_FiresDismissedEvent()
        {
            var service = new TooltipService();
            bool dismissedFired = false;
            service.OnTooltipDismissed += () => dismissedFired = true;

            service.ShowTooltip(TooltipData.CreateSimple("Test", "Content"), Vector2.zero);
            Assert.IsTrue(service.IsVisible);

            service.HideTooltip();
            Assert.IsFalse(service.IsVisible);
            Assert.IsNull(service.CurrentData);
            Assert.IsTrue(dismissedFired);
        }

        [Test]
        public void TooltipService_NullData_HidesTooltip()
        {
            var service = new TooltipService();
            service.ShowTooltip(TooltipData.CreateSimple("A", "B"), Vector2.zero);
            Assert.IsTrue(service.IsVisible);

            service.ShowTooltip(null, Vector2.zero);
            Assert.IsFalse(service.IsVisible);
        }
    }
}
