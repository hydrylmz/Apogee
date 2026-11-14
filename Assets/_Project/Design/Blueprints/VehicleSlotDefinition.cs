using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Metadata definition for a vehicle slot, describing its functional category,
    /// friendly name, group classification, and requirement flags (GDD §7.2, §14.3).
    /// </summary>
    public sealed class VehicleSlotDefinition
    {
        public VehicleSlotId Id { get; }
        public string CategoryTag { get; }
        public string DisplayName { get; }
        public string GroupName { get; }
        public bool IsRequired { get; }
        public string Description { get; }

        public VehicleSlotDefinition(
            VehicleSlotId id,
            string categoryTag,
            string displayName,
            string groupName,
            bool isRequired,
            string description = null)
        {
            Id = id;
            CategoryTag = categoryTag ?? throw new ArgumentNullException(nameof(categoryTag));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            GroupName = groupName ?? "Genel";
            IsRequired = isRequired;
            Description = description;
        }

        private static readonly Dictionary<VehicleSlotId, VehicleSlotDefinition> Definitions = new()
        {
            [VehicleSlotId.PayloadBus] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadBus, "bus", "Gövde Yapısı (Bus)", "Uzay Aracı / Gezgin", true,
                "Aracın omurgası: tüm alt sistemleri bir arada tutan ana yapı. Güç, veri ve mekanik bağlantıların dağıtıldığı merkezi gövdedir."),
            [VehicleSlotId.PayloadPower] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadPower, "power", "Güç Sistemi (Power)", "Uzay Aracı / Gezgin", true,
                "Güneş panelleri ve bataryalarla elektrik üretir ve depolar. Tüm sistemlerin çalışması için sürekli güç sağlar."),
            [VehicleSlotId.PayloadThermal] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadThermal, "thermal", "Isıl Kontrol (Thermal)", "Uzay Aracı / Gezgin", true,
                "Aracı uzayın aşırı sıcak ve soğukundan korur. Radyatör, ısıtıcı ve yalıtım ile ekipmanı ideal sıcaklıkta tutar."),
            [VehicleSlotId.PayloadComms] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadComms, "comms", "Haberleşme (Comms)", "Uzay Aracı / Gezgin", true,
                "Yer istasyonuyla veri alışverişi yapar: telemetri gönderir, komut alır ve bilimsel verileri Dünya'ya iletir."),
            [VehicleSlotId.PayloadAdcs] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadAdcs, "adcs", "Yönelim Kontrol (ADCS)", "Uzay Aracı / Gezgin", true,
                "Aracın uzaydaki yönünü belirler ve kontrol eder. Antenlerin ve sensörlerin doğru noktaya dönmesini sağlar."),
            [VehicleSlotId.PayloadEdl] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadEdl, "edl", "İniş Sistemi (EDL)", "Uzay Aracı / Gezgin", false,
                "Girme, iniş ve açılma (Entry, Descent, Landing) sistemi: atmosfere giriş sırasında yavaşlayarak yükün güvenle yere inmesini sağlar."),
            [VehicleSlotId.PayloadLifeSupport] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadLifeSupport, "life-support", "Yaşam Destek (ECLSS)", "İnsanlı Sistemler", false,
                "Mürettebat için oksijen üretir, CO₂ temizler, su geri kazanır ve kabin basıncını düzenler."),
            [VehicleSlotId.PayloadCabin] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadCabin, "cabin", "Mürettebat Kabini", "İnsanlı Sistemler", false,
                "Mürettebatın yolculuk sırasında yaşadığı basınçlı yaşam alanı. İnsanlı görevler için zorunludur."),
            [VehicleSlotId.PayloadAbortSystem] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadAbortSystem, "abort-system", "Fırlatma Kaçış / Abort", "İnsanlı Sistemler", false,
                "Fırlatma sırasında acil durumda mürettebat kapsülünü roketten hızla uzaklaştıran kurtarma sistemi."),
            [VehicleSlotId.PayloadInstrument1] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadInstrument1, "instrument", "Bilimsel Alet 1", "Bilimsel Yük", false,
                "Görevin bilimsel amaçlarını gerçekleştiren alet: kamera, spektrometre veya sensör gibi veri toplayan yük."),
            [VehicleSlotId.PayloadInstrument2] = new VehicleSlotDefinition(
                VehicleSlotId.PayloadInstrument2, "instrument", "Bilimsel Alet 2", "Bilimsel Yük", false,
                "İkinci bilimsel yük: farklı bir ölçüm veya gözlem yaparak görevin bilimsel değerini artırır."),

            [VehicleSlotId.Stage1Engine] = new VehicleSlotDefinition(
                VehicleSlotId.Stage1Engine, "engine", "Aşama 1 Motoru", "Fırlatıcı Aşama 1", true,
                "Kalkışta en büyük itkiyi sağlayan ana motor. Aracı yerden kaldırıp ilk aşama yakıtını tüketene dek hızlandırır."),
            [VehicleSlotId.Stage1Tank] = new VehicleSlotDefinition(
                VehicleSlotId.Stage1Tank, "tank", "Aşama 1 Yakıt Tankı", "Fırlatıcı Aşama 1", true,
                "İlk aşama motorunun yakıtını ve oksitleyicisini depolar. Kalkış sırasında en büyük kütleyi oluşturan yapıdır."),
            [VehicleSlotId.Stage2Engine] = new VehicleSlotDefinition(
                VehicleSlotId.Stage2Engine, "engine", "Aşama 2 Motoru", "Fırlatıcı Aşama 2", false,
                "İlk aşama ayrıldıktan sonra devreye girer; aracı yörüngeye yaklaşım hızına taşır."),
            [VehicleSlotId.Stage2Tank] = new VehicleSlotDefinition(
                VehicleSlotId.Stage2Tank, "tank", "Aşama 2 Yakıt Tankı", "Fırlatıcı Aşama 2", false,
                "İkinci aşama motorunun yakıtını depolar; ilk aşama bırakıldığında yanar."),
            [VehicleSlotId.Stage3Engine] = new VehicleSlotDefinition(
                VehicleSlotId.Stage3Engine, "engine", "Aşama 3 Motoru", "Fırlatıcı Aşama 3", false,
                "Son aşama motoru: yörünge içi manevralar, yörünge dairesel hale getirme veya hedefe transit giriş yakıtını sağlar."),
            [VehicleSlotId.Stage3Tank] = new VehicleSlotDefinition(
                VehicleSlotId.Stage3Tank, "tank", "Aşama 3 Yakıt Tankı", "Fırlatıcı Aşama 3", false,
                "Son aşama motorunun yakıtını depolar. Yörünge manevraları ve hedefe yolculuk için kullanılır."),
        };

        public static VehicleSlotDefinition Get(VehicleSlotId slotId)
        {
            if (Definitions.TryGetValue(slotId, out var def))
            {
                return def;
            }
            return new VehicleSlotDefinition(slotId, "generic", slotId.ToString(), "Diğer", false,
                "Bu yuva için tanım bulunamadı.");
        }

        public static IReadOnlyCollection<VehicleSlotDefinition> All => Definitions.Values;
    }
}
