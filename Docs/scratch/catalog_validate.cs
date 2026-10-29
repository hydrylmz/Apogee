// Standalone validation for CatalogLoader logic (M1-05).
// Top-level statements MUST come before namespace declarations in C# 9+.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Apogee.Data;

// ── Test harness ─────────────────────────────────────────────────────────────

int pass = 0, fail = 0;
void Check(string name, bool ok, string detail = "") {
    if (ok) { pass++; Console.WriteLine($"  ✓ {name}"); }
    else    { fail++; Console.WriteLine($"  ✗ {name}  [{detail}]"); }
}

Console.WriteLine("=== APOGEE M1-05 CatalogLoader Standalone Validation ===\n");

const string validModules = @"{""modules"":[{""id"":""engine-mk1"",""category"":""engine"",""dryMassTons"":2.5,""isp"":311},{""id"":""tank-small"",""category"":""tank""}]}";
const string validDests   = @"{""destinations"":[{""id"":""leo"",""dvAscent"":9400},{""id"":""mars"",""dvAscent"":9400,""dvTransfer"":2700,""dvArrival"":900,""dvLanding"":600}]}";

// 1. Valid wrapped object
{ var r = CatalogLoader.Load<ModuleRecord>(validModules); Check("1. Valid wrapped JSON → IsValid", r.Validation.IsValid, string.Join(";", r.Validation.Errors)); Check("1b. Count=2", r.Count == 2, $"count={r.Count}"); }

// 2. Valid bare array
{ var r = CatalogLoader.Load<ModuleRecord>(@"[{""id"":""a""},{""id"":""b""}]"); Check("2. Bare array → IsValid", r.Validation.IsValid); Check("2b. Count=2", r.Count == 2); }

// 3. Field deserialization
{ var r = CatalogLoader.Load<ModuleRecord>(validModules); var e = r.GetById("engine-mk1"); Check("3a. GetById engine", e != null); Check("3b. Isp=311", e != null && Math.Abs(e.Isp-311)<1e-9, $"isp={e?.Isp}"); Check("3c. DryMass=2.5", e != null && Math.Abs(e.DryMassTons-2.5)<1e-9); }

// 4. Destination DvTotal
{ var r = CatalogLoader.Load<DestinationRecord>(validDests); var mars = r.GetById("mars"); Check("4. Mars DvTotal=13600", mars != null && Math.Abs(mars.DvTotal-13600)<1e-9, $"dvTotal={mars?.DvTotal}"); }

// 5. GetById unknown → null
{ var r = CatalogLoader.Load<ModuleRecord>(validModules); Check("5. GetById unknown → null", r.GetById("nope") == null); }

// 6. Null JSON → error
{ var r = CatalogLoader.Load<ModuleRecord>(null); Check("6. Null JSON → error", !r.Validation.IsValid && r.Validation.Errors[0].Contains("null or empty")); }

// 7. Broken JSON → parse error
{ var r = CatalogLoader.Load<ModuleRecord>("{{{{ broken"); Check("7. Broken JSON → parse error", !r.Validation.IsValid && r.Validation.Errors[0].StartsWith("JSON parse error")); }

// 8. Scalar root → error
{ var r = CatalogLoader.Load<ModuleRecord>("42"); Check("8. Scalar root → error", !r.Validation.IsValid && r.Validation.Errors[0].Contains("Unexpected JSON root type")); }

// 9. Object with no array → error
{ var r = CatalogLoader.Load<ModuleRecord>(@"{""foo"":""bar""}"); Check("9. Object no array → error", !r.Validation.IsValid && r.Validation.Errors[0].Contains("no array property")); }

// 10. Null id → error
{ var r = CatalogLoader.Load<ModuleRecord>(@"[{""id"":null}]"); Check("10. Null id → error", !r.Validation.IsValid && r.Validation.Errors[0].Contains("null or empty 'id'")); }

// 11. Empty string id → error
{ var r = CatalogLoader.Load<ModuleRecord>(@"[{""id"":""""}]"); Check("11. Empty id → error", !r.Validation.IsValid && r.Validation.Errors[0].Contains("null or empty 'id'")); }

// 12. Duplicate ids → error
{ var r = CatalogLoader.Load<ModuleRecord>(@"[{""id"":""dup""},{""id"":""dup""}]"); Check("12a. Duplicate id → error", !r.Validation.IsValid && r.Validation.Errors[0].Contains("Duplicate id")); Check("12b. First record still accessible", r.GetById("dup") != null); }

// 13. Empty array → valid
{ var r = CatalogLoader.Load<ModuleRecord>("[]"); Check("13. Empty array → valid", r.Validation.IsValid); }

// 14. Extra unknown fields → ignored, no error
{ var r = CatalogLoader.Load<ModuleRecord>(@"[{""id"":""x"",""unknownFutureProp"":99}]"); Check("14. Unknown fields ignored", r.Validation.IsValid); }

// 15. Mixed valid+invalid → correct error count
{ var r = CatalogLoader.Load<ModuleRecord>(@"[{""id"":""ok1""},{""id"":""""},{""id"":""ok2""}]"); Check("15a. Mixed: invalid", !r.Validation.IsValid); Check("15b. Mixed: 1 error", r.Validation.Errors.Count == 1, $"errors={r.Validation.Errors.Count}"); Check("15c. Valid records accessible", r.GetById("ok1")!=null && r.GetById("ok2")!=null); }

// 16. Supplier multiplier deserialization
{ var json = @"{""suppliers"":[{""id"":""helion"",""relationshipStart"":0.5,""costMultiplierByCategory"":{""engine"":0.80},""strengthTags"":[""low-cost""]}]}";
  var r = CatalogLoader.Load<SupplierRecord>(json); Check("16a. Supplier IsValid", r.Validation.IsValid); var s = r.GetById("helion"); Check("16b. RelStart=0.5", s!=null && Math.Abs(s.RelationshipStart-0.5)<1e-9); Check("16c. CostMult engine=0.80", s?.CostMultiplierByCategory?.ContainsKey("engine")==true && Math.Abs(s.CostMultiplierByCategory["engine"]-0.80)<1e-9); Check("16d. StrengthTag count=1", s?.StrengthTags?.Count==1); }

// 17. Validate() null list
{ var v = CatalogLoader.Validate<ModuleRecord>(null); Check("17. Validate null → error", !v.IsValid); }

// 18. TryGetById
{ var r = CatalogLoader.Load<ModuleRecord>(validModules); bool ok = r.TryGetById("engine-mk1", out var rec); Check("18a. TryGet found=true", ok); Check("18b. TryGet null id = false", !r.TryGetById(null, out _)); }

// 19. CatalogValidationResult.Ok
{ Check("19. Ok.IsValid=true", CatalogValidationResult.Ok.IsValid); }

// 20. Real destinations.json file
{
    var path = @"c:\Users\bmiko\Cloud Repositories\Project Nexir\Assets\_Project\Data\Catalog\Json\destinations.json";
    if (File.Exists(path)) {
        var json = File.ReadAllText(path);
        var r = CatalogLoader.Load<DestinationRecord>(json);
        Check("20a. destinations.json IsValid", r.Validation.IsValid, string.Join(";", r.Validation.Errors));
        Check("20b. destinations.json count=5", r.Count == 5, $"count={r.Count}");
        Check("20c. LEO exists", r.GetById("leo") != null);
        Check("20d. Europa exists", r.GetById("europa") != null);
    } else Check("20. destinations.json (skip)", true);
}

// 21. Real suppliers.json file
{
    var path = @"c:\Users\bmiko\Cloud Repositories\Project Nexir\Assets\_Project\Data\Catalog\Json\suppliers.json";
    if (File.Exists(path)) {
        var json = File.ReadAllText(path);
        var r = CatalogLoader.Load<SupplierRecord>(json);
        Check("21a. suppliers.json IsValid", r.Validation.IsValid, string.Join(";", r.Validation.Errors));
        Check("21b. suppliers.json count=6", r.Count == 6, $"count={r.Count}");
        var kestrel = r.GetById("kestrel-aerospace");
        Check("21c. Kestrel crewed-cert", kestrel?.SpecialCapabilities?.Contains("crewed-certification") == true);
    } else Check("21. suppliers.json (skip)", true);
}

Console.WriteLine($"\n=== Results: {pass} PASS  {fail} FAIL ===");
if (fail > 0) Environment.Exit(1);

// ── Namespace declarations (must come after top-level statements) ─────────────

namespace Apogee.Data
{
    public interface ICatalogRecord { string Id { get; } }

    public sealed class CatalogValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public IReadOnlyList<string> Errors   { get; }
        public IReadOnlyList<string> Warnings { get; }
        public CatalogValidationResult(IReadOnlyList<string> e, IReadOnlyList<string> w)
        { Errors = e ?? new List<string>(); Warnings = w ?? new List<string>(); }
        public static readonly CatalogValidationResult Ok =
            new CatalogValidationResult(new List<string>(), new List<string>());
    }

    public sealed class CatalogCollection<T> where T : class, ICatalogRecord
    {
        private readonly Dictionary<string, T> _byId;
        public IReadOnlyList<T> All { get; }
        public CatalogValidationResult Validation { get; }
        public int Count => All.Count;
        public T GetById(string id) { if (id == null) return null; _byId.TryGetValue(id, out T r); return r; }
        public bool TryGetById(string id, out T r) { if (id == null) { r = null; return false; } return _byId.TryGetValue(id, out r); }
        internal CatalogCollection(IReadOnlyList<T> records, CatalogValidationResult v)
        {
            All = records; Validation = v;
            _byId = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (var rec in All)
                if (rec != null && !string.IsNullOrEmpty(rec.Id) && !_byId.ContainsKey(rec.Id))
                    _byId[rec.Id] = rec;
        }
    }

    public static class CatalogLoader
    {
        public static CatalogCollection<T> Load<T>(string json, JsonSerializerSettings settings = null)
            where T : class, ICatalogRecord
        {
            var errors = new List<string>(); var warnings = new List<string>();
            if (string.IsNullOrWhiteSpace(json)) { errors.Add("Catalog JSON is null or empty."); return Empty<T>(errors, warnings); }
            JToken root;
            try { root = JToken.Parse(json); }
            catch (JsonException ex) { errors.Add($"JSON parse error: {ex.Message}"); return Empty<T>(errors, warnings); }
            JArray array = ExtractArray(root, errors, warnings);
            if (array == null) return Empty<T>(errors, warnings);
            var s2 = JsonSerializer.CreateDefault(settings ?? new JsonSerializerSettings
                { MissingMemberHandling = MissingMemberHandling.Ignore, NullValueHandling = NullValueHandling.Ignore });
            var records = new List<T>(array.Count);
            for (int i = 0; i < array.Count; i++) {
                var el = array[i];
                if (el == null || el.Type == JTokenType.Null) { errors.Add($"Record at index {i} is null — every entry must be a JSON object."); continue; }
                T rec;
                try { rec = el.ToObject<T>(s2); }
                catch (JsonException ex) { errors.Add($"Record at index {i} failed to deserialize: {ex.Message}"); continue; }
                if (rec == null) { errors.Add($"Record at index {i} deserialized to null."); continue; }
                records.Add(rec);
            }
            ValidateIds(records, errors);
            return new CatalogCollection<T>(records, new CatalogValidationResult(errors, warnings));
        }
        public static CatalogValidationResult Validate<T>(IReadOnlyList<T> records) where T : class, ICatalogRecord
        {
            if (records == null) return new CatalogValidationResult(new[] { "Record list is null." }, Array.Empty<string>());
            var errors = new List<string>(); ValidateIds(records, errors);
            return new CatalogValidationResult(errors, new List<string>());
        }
        static JArray ExtractArray(JToken root, List<string> errors, List<string> warnings) {
            if (root.Type == JTokenType.Array) return (JArray)root;
            if (root.Type == JTokenType.Object) {
                JArray found = null; string foundKey = null;
                foreach (var p in ((JObject)root).Properties())
                    if (p.Value.Type == JTokenType.Array) {
                        if (found == null) { found = (JArray)p.Value; foundKey = p.Name; }
                        else { warnings.Add($"Multiple arrays; using '{foundKey}'."); break; }
                    }
                if (found != null) return found;
                errors.Add("Root JSON object contains no array property. Expected a bare array [ … ] or an object with one array property."); return null;
            }
            errors.Add($"Unexpected JSON root type '{root.Type}'. Expected array or object."); return null;
        }
        static void ValidateIds<T>(IReadOnlyList<T> records, List<string> errors) where T : class, ICatalogRecord {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < records.Count; i++) {
                var r = records[i]; if (r == null) continue;
                if (string.IsNullOrWhiteSpace(r.Id)) { errors.Add($"Record at index {i} (type {r.GetType().Name}) has a null or empty 'id'. Every catalog entry must have a unique non-empty id."); continue; }
                if (!seen.Add(r.Id)) errors.Add($"Duplicate id '{r.Id}' found at index {i}. Catalog ids must be unique within their collection.");
            }
        }
        static CatalogCollection<T> Empty<T>(List<string> e, List<string> w) where T : class, ICatalogRecord
            => new CatalogCollection<T>(new List<T>(), new CatalogValidationResult(e, w));
    }

    public sealed class ModuleRecord : ICatalogRecord
    {
        [JsonProperty("id")]           public string Id          { get; private set; }
        [JsonProperty("category")]     public string Category    { get; private set; }
        [JsonProperty("dryMassTons")]  public double DryMassTons { get; private set; }
        [JsonProperty("isp")]          public double Isp         { get; private set; }
    }

    public sealed class DestinationRecord : ICatalogRecord
    {
        [JsonProperty("id")]             public string Id         { get; private set; }
        [JsonProperty("bodyType")]       public string BodyType   { get; private set; }
        [JsonProperty("dvAscent")]       public double DvAscent   { get; private set; }
        [JsonProperty("dvTransfer")]     public double DvTransfer { get; private set; }
        [JsonProperty("dvArrival")]      public double DvArrival  { get; private set; }
        [JsonProperty("dvLanding")]      public double DvLanding  { get; private set; }
        [JsonProperty("dvReturn")]       public double DvReturn   { get; private set; }
        [JsonIgnore] public double DvTotal => DvAscent + DvTransfer + DvArrival + DvLanding + DvReturn;
    }

    public sealed class SupplierRecord : ICatalogRecord
    {
        [JsonProperty("id")]                        public string Id                 { get; private set; }
        [JsonProperty("relationshipStart")]         public double RelationshipStart  { get; private set; }
        [JsonProperty("costMultiplierByCategory")]  public Dictionary<string,double> CostMultiplierByCategory { get; private set; } = new();
        [JsonProperty("strengthTags")]              public List<string> StrengthTags { get; private set; } = new();
        [JsonProperty("specialCapabilities")]       public List<string> SpecialCapabilities { get; private set; } = new();
    }
}
