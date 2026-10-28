namespace Apogee.Data
{
    /// <summary>
    /// Marker interface for all JSON catalog records.
    /// Every record type loaded from a catalog JSON file must implement this interface.
    /// The <c>Id</c> field is the unique identifier used for validation and lookup.
    ///
    /// GDD §17.3 — "İçerik (bileşen, şirket, olay, hedef, araştırma) kodda değil veri dosyalarında"
    /// GDD §19   — "Her kayıt: benzersiz id, görünen ad anahtarı, açıklama anahtarı, ikon, etiketler."
    /// </summary>
    public interface ICatalogRecord
    {
        /// <summary>
        /// Unique identifier for this catalog entry.
        /// Must be non-null, non-empty, and unique within its catalog collection.
        /// Convention: lower-kebab-case, e.g. "engine-kerosene-lox-mk1".
        /// </summary>
        string Id { get; }
    }
}
