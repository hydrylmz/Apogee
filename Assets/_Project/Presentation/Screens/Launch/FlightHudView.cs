using System;
using Apogee.Core.Simulation.Launch;
using Apogee.UI.Core.Flight;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Launch
{
    /// <summary>
    /// Telemetry and mission header HUD overlay (GDD §3.4, §3.6).
    /// Displays mission/vehicle info, altitude, velocity, flight clock, and outcome/status badge.
    /// </summary>
    public sealed class FlightHudView : VisualElement
    {
        private readonly Label _missionLabel;
        private readonly Label _vehicleLabel;
        private readonly Label _weatherLabel;
        private readonly Label _statusBadge;

        private readonly Label _clockTitle;
        private readonly Label _clockLabel;
        private readonly Label _altitudeTitle;
        private readonly Label _altitudeLabel;
        private readonly Label _velocityTitle;
        private readonly Label _velocityLabel;

        public FlightHudView()
        {
            style.position = Position.Absolute;
            style.top = 0;
            style.left = 0;
            style.right = 0;
            style.paddingTop = 12;
            style.paddingBottom = 12;
            style.paddingLeft = 20;
            style.paddingRight = 20;
            style.backgroundColor = new Color(0.03f, 0.05f, 0.08f, 0.88f);
            style.borderBottomWidth = 1;
            style.borderBottomColor = new Color(0.12f, 0.18f, 0.28f, 0.8f);

            var topRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                    alignItems = Align.Center,
                    marginBottom = 8
                }
            };

            var missionCol = new VisualElement();
            _missionLabel = new Label("FIRLATMA OPERASYONU")
            {
                style =
                {
                    fontSize = 15,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.9f, 0.95f, 1f)
                }
            };
            missionCol.Add(_missionLabel);

            _vehicleLabel = new Label("Araç & Görev Yükleniyor...")
            {
                style =
                {
                    fontSize = 11,
                    color = new Color(0.4f, 0.75f, 0.95f)
                }
            };
            missionCol.Add(_vehicleLabel);
            topRow.Add(missionCol);

            var badgeCol = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            _weatherLabel = new Label("Hava: Nominal")
            {
                style =
                {
                    fontSize = 11,
                    color = new Color(0.7f, 0.85f, 0.95f),
                    backgroundColor = new Color(0.08f, 0.14f, 0.22f, 0.8f),
                    paddingTop = 4,
                    paddingBottom = 4,
                    paddingLeft = 8,
                    paddingRight = 8,
                    marginRight = 10,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };
            badgeCol.Add(_weatherLabel);

            _statusBadge = new Label("HAZIR")
            {
                style =
                {
                    fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = Color.white,
                    backgroundColor = new Color(0.12f, 0.35f, 0.5f),
                    paddingTop = 4,
                    paddingBottom = 4,
                    paddingLeft = 10,
                    paddingRight = 10,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };
            badgeCol.Add(_statusBadge);
            topRow.Add(badgeCol);
            Add(topRow);

            // Metrics bar
            var metricsRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceAround,
                    alignItems = Align.Center,
                    paddingTop = 6,
                    paddingBottom = 6,
                    backgroundColor = new Color(0.05f, 0.08f, 0.13f, 0.75f),
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };

            // T+ Clock / Metric 1
            var clockBox = new VisualElement { style = { alignItems = Align.Center } };
            _clockTitle = new Label("GÖREV ZAMANI (MET)") { style = { fontSize = 9, color = new Color(0.45f, 0.6f, 0.75f) } };
            _clockLabel = new Label("T+00:00") { style = { fontSize = 20, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.9f, 0.95f, 1f) } };
            clockBox.Add(_clockTitle);
            clockBox.Add(_clockLabel);
            metricsRow.Add(clockBox);

            metricsRow.Add(CreateDivider());

            // Altitude / Metric 2
            var altBox = new VisualElement { style = { alignItems = Align.Center } };
            _altitudeTitle = new Label("İRTİFA (ALTITUDE)") { style = { fontSize = 9, color = new Color(0.45f, 0.6f, 0.75f) } };
            _altitudeLabel = new Label("0.0 km") { style = { fontSize = 20, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.35f, 0.9f, 1f) } };
            altBox.Add(_altitudeTitle);
            altBox.Add(_altitudeLabel);
            metricsRow.Add(altBox);

            metricsRow.Add(CreateDivider());

            // Velocity / Metric 3
            var velBox = new VisualElement { style = { alignItems = Align.Center } };
            _velocityTitle = new Label("HIZ (VELOCITY)") { style = { fontSize = 9, color = new Color(0.45f, 0.6f, 0.75f) } };
            _velocityLabel = new Label("0 m/s") { style = { fontSize = 20, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.35f, 0.95f, 0.65f) } };
            velBox.Add(_velocityTitle);
            velBox.Add(_velocityLabel);
            metricsRow.Add(velBox);

            Add(metricsRow);
        }

        private static VisualElement CreateDivider()
        {
            return new VisualElement
            {
                style =
                {
                    width = 1,
                    height = 28,
                    backgroundColor = new Color(0.18f, 0.25f, 0.38f, 0.7f)
                }
            };
        }

        public void SetMissionInfo(string missionName, string vehicleName, string weatherSummary)
        {
            _missionLabel.text = string.IsNullOrEmpty(missionName) ? "GÖREV OPERASYONU" : missionName;
            _vehicleLabel.text = string.IsNullOrEmpty(vehicleName) ? "Taşıyıcı Araç" : vehicleName;
            if (!string.IsNullOrEmpty(weatherSummary))
            {
                _weatherLabel.text = weatherSummary;
                _weatherLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _weatherLabel.style.display = DisplayStyle.None;
            }
        }

        public void SetWeatherVisible(bool visible)
        {
            _weatherLabel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetMetricTitles(string metric1, string metric2, string metric3)
        {
            if (_clockTitle != null && !string.IsNullOrEmpty(metric1)) _clockTitle.text = metric1;
            if (_altitudeTitle != null && !string.IsNullOrEmpty(metric2)) _altitudeTitle.text = metric2;
            if (_velocityTitle != null && !string.IsNullOrEmpty(metric3)) _velocityTitle.text = metric3;
        }

        public void SetMetricValues(string val1, string val2, string val3)
        {
            if (_clockLabel != null) _clockLabel.text = val1 ?? string.Empty;
            if (_altitudeLabel != null) _altitudeLabel.text = val2 ?? string.Empty;
            if (_velocityLabel != null) _velocityLabel.text = val3 ?? string.Empty;
        }

        public void UpdateTelemetry(FlightBeat beat)
        {
            if (beat == null) return;

            string phase = beat.PhaseId?.ToUpperInvariant() ?? string.Empty;
            if (phase.Contains("CRUISE"))
            {
                SetMetricTitles("GÖREV GÜNÜ (MET)", "İLERLEME (PROGRESS)", "DURUM (STATUS)");
                int day = Math.Max(1, (int)(beat.CruiseProgress * 30.0 + 1));
                _clockLabel.text = $"Gün {day}";
                _altitudeLabel.text = $"%{beat.CruiseProgress * 100.0:0.#}";
                _velocityLabel.text = beat.StatusId;
            }
            else if (phase.Contains("EDL") || phase.Contains("ENTRY") || phase.Contains("DESCENT") || phase.Contains("TOUCHDOWN"))
            {
                SetMetricTitles("İNİŞ ZAMANI (MET)", "İRTİFA (ALTITUDE)", beat.HeatShieldIntegrity > 0 ? "ISI KALKANI (SHIELD)" : "HIZ (VELOCITY)");
                int totalSec = (int)beat.TimestampSeconds;
                _clockLabel.text = $"T+{totalSec}s";
                _altitudeLabel.text = $"{beat.AltitudeKm:0.#} km";
                _velocityLabel.text = beat.HeatShieldIntegrity > 0
                    ? $"%{beat.HeatShieldIntegrity * 100.0:0.#}"
                    : $"{beat.VelocityMps:0} m/s";
            }
            else if (phase.Contains("SCIENCE"))
            {
                SetMetricTitles("BİLİM GÜNÜ (SCI DAY)", "DURUM (STATUS)", "VERİ AKIŞI (DOWNLINK)");
                _clockLabel.text = $"Gün {(int)Math.Max(1, beat.TimestampSeconds)}";
                _altitudeLabel.text = beat.StatusId;
                _velocityLabel.text = "Aktif";
            }
            else
            {
                SetMetricTitles("GÖREV ZAMANI (MET)", "İRTİFA (ALTITUDE)", "HIZ (VELOCITY)");
                int totalSec = (int)beat.TimestampSeconds;
                int minutes = totalSec / 60;
                int seconds = totalSec % 60;
                _clockLabel.text = $"T+{minutes:D2}:{seconds:D2}";
                _altitudeLabel.text = $"{beat.AltitudeKm:0.#} km";
                _velocityLabel.text = $"{beat.VelocityMps:0} m/s";
            }
        }

        public void SetStatusBadge(string text, Color? bgColor = null)
        {
            _statusBadge.text = text ?? "HAZIR";
            if (bgColor.HasValue)
            {
                _statusBadge.style.backgroundColor = bgColor.Value;
            }
        }

        public void SetOutcomeBadge(LaunchOutcome outcome, string customLabel = null)
        {
            _statusBadge.text = customLabel ?? outcome.ToString().ToUpperInvariant();

            if (outcome == LaunchOutcome.Success)
            {
                _statusBadge.style.backgroundColor = new Color(0.10f, 0.60f, 0.40f);
            }
            else if (outcome == LaunchOutcome.PartialOrbit || outcome == LaunchOutcome.WeatherScrub)
            {
                _statusBadge.style.backgroundColor = new Color(0.85f, 0.55f, 0.10f);
            }
            else
            {
                _statusBadge.style.backgroundColor = new Color(0.80f, 0.20f, 0.20f);
            }
        }
    }
}
