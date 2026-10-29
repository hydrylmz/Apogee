using System;
using System.Collections.Generic;
using System.Text;

namespace Apogee.Design
{
    /// <summary>
    /// Compact representation of vehicle design, target, and seed (GDD §7.5 & §17).
    /// Used for shareable mission codes.
    /// </summary>
    public sealed class MissionCodeData
    {
        public string BlueprintName { get; set; } = string.Empty;
        public string DestinationId { get; set; } = string.Empty;
        public string MissionTypeId { get; set; } = string.Empty;
        public ulong Seed { get; set; }
        public Dictionary<VehicleSlotId, string> SlotComponents { get; set; } = new();
        public List<double> StagePropellants { get; set; } = new();
    }

    /// <summary>
    /// Encodes and decodes shareable mission codes into deterministic text format (GDD §7.5).
    /// Safe for WebGL, no reflection or engine dependencies.
    /// </summary>
    public static class MissionCodeCodec
    {
        private const string CodePrefix = "APG1-";

        public static string Encode(Blueprint blueprint, ulong seed = 42)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));

            var data = new MissionCodeData
            {
                BlueprintName = blueprint.Name,
                DestinationId = blueprint.TargetDestinationId ?? string.Empty,
                MissionTypeId = blueprint.TargetMissionTypeId ?? string.Empty,
                Seed = seed
            };

            foreach (VehicleSlotId slotId in Enum.GetValues(typeof(VehicleSlotId)))
            {
                var comp = blueprint.GetComponentInSlot(slotId);
                if (comp != null)
                {
                    data.SlotComponents[slotId] = comp.Id;
                }
            }

            foreach (var stage in blueprint.Stages)
            {
                data.StagePropellants.Add(stage.PropellantMass);
            }

            return Encode(data);
        }

        public static string Encode(MissionCodeData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var sb = new StringBuilder();
            sb.Append(data.BlueprintName).Append(';');
            sb.Append(data.DestinationId).Append(';');
            sb.Append(data.MissionTypeId).Append(';');
            sb.Append(data.Seed).Append(';');

            // Slots
            sb.Append(data.SlotComponents.Count).Append(';');
            foreach (var kvp in data.SlotComponents)
            {
                sb.Append((int)kvp.Key).Append(':').Append(kvp.Value).Append(',');
            }
            sb.Append(';');

            // Propellants
            sb.Append(data.StagePropellants.Count).Append(';');
            foreach (var prop in data.StagePropellants)
            {
                sb.Append(prop.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            }

            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            string base64 = Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            return CodePrefix + base64;
        }

        public static bool TryDecode(string code, out MissionCodeData data, out string errorMessage)
        {
            data = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(code))
            {
                errorMessage = "Kod boş olamaz.";
                return false;
            }

            if (!code.StartsWith(CodePrefix, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Geçersiz kod öneki. 'APG1-' bekleniyor.";
                return false;
            }

            try
            {
                string rawBase64 = code.Substring(CodePrefix.Length)
                    .Replace('-', '+')
                    .Replace('_', '/');

                switch (rawBase64.Length % 4)
                {
                    case 2: rawBase64 += "=="; break;
                    case 3: rawBase64 += "="; break;
                }

                byte[] bytes = Convert.FromBase64String(rawBase64);
                string text = Encoding.UTF8.GetString(bytes);
                string[] parts = text.Split(';');

                if (parts.Length < 7)
                {
                    errorMessage = "Eksik kod bölümleri.";
                    return false;
                }

                data = new MissionCodeData
                {
                    BlueprintName = parts[0],
                    DestinationId = parts[1],
                    MissionTypeId = parts[2],
                    Seed = ulong.Parse(parts[3])
                };

                // Slots
                int slotCount = int.Parse(parts[4]);
                if (slotCount > 0 && !string.IsNullOrWhiteSpace(parts[5]))
                {
                    string[] slotPairs = parts[5].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var pair in slotPairs)
                    {
                        string[] kv = pair.Split(':');
                        if (kv.Length == 2 && int.TryParse(kv[0], out int slotInt))
                        {
                            data.SlotComponents[(VehicleSlotId)slotInt] = kv[1];
                        }
                    }
                }

                // Propellants
                int propCount = int.Parse(parts[6]);
                if (propCount > 0 && parts.Length > 7 && !string.IsNullOrWhiteSpace(parts[7]))
                {
                    string[] propStrs = parts[7].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var p in propStrs)
                    {
                        if (double.TryParse(p, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val))
                        {
                            data.StagePropellants.Add(val);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Kod çözülemedi: {ex.Message}";
                return false;
            }
        }
    }
}
