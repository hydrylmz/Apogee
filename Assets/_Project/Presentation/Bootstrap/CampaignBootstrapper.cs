#pragma warning disable CS0649
using System;
using System.IO;
using Apogee.Data;
using Apogee.Strategy;
using Apogee.Strategy.Campaign;
using Apogee.Strategy.Persistence;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Managers;
using UnityEngine;

namespace Apogee.UI.Bootstrap
{
    /// <summary>
    /// Master Scene Bootstrapper for APOGEE.
    /// Responsible for initializing or restoring CampaignState on scene start,
    /// loading catalog-backed managers (Research, Suppliers, Events, Milestones),
    /// and injecting the active campaign into ApogeeUIManager and screen controllers.
    ///
    /// GDD §4.1, §6.1, §14.1, §21 M9-02.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Apogee/Campaign/Campaign Bootstrapper")]
    public sealed class CampaignBootstrapper : MonoBehaviour, ICampaignLifecycleHandler
    {
        public static CampaignBootstrapper Instance { get; private set; }

        [Header("UI Manager Reference")]
        [SerializeField] private ApogeeUIManager _uiManager;

        [Header("Default Campaign Settings")]
        [SerializeField] private string _agencyName = "Birleşik Yörünge Ajansı";
        [SerializeField] private string _startingRegionId = "cape_peninsula";
        [SerializeField] private ulong _seed = 42;
        [SerializeField] private double _startingBudget = 500.0;
        [SerializeField] private double _startingReputation = 30.0;
        [SerializeField] private bool _autoStartOnAwake = true;

        [Header("Optional Catalog Overrides (e.g. for WebGL / Inspector)")]
        [SerializeField] private TextAsset _researchTreeJson;
        [SerializeField] private TextAsset _suppliersJson;
        [SerializeField] private TextAsset _campaignEventsJson;
        [SerializeField] private TextAsset _milestonesJson;
        [SerializeField] private TextAsset _localeTrJson;
        [SerializeField] private TextAsset _localeEnJson;

        public CampaignState CurrentCampaign { get; private set; }
        public bool IsInitialized => CurrentCampaign != null;

        public event Action<CampaignState> OnCampaignStarted;
        public event Action<CampaignState> OnCampaignLoaded;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            if (_uiManager == null)
            {
                _uiManager = GetComponent<ApogeeUIManager>() ?? FindObjectOfType<ApogeeUIManager>();
            }

            if (_autoStartOnAwake && CurrentCampaign == null)
            {
                StartNewCampaign();
            }
        }

        private void Start()
        {
            BindToUI();
        }

        /// <summary>
        /// Starts a fresh campaign session and wires it to all UI controllers.
        /// </summary>
        public CampaignState StartNewCampaign(CampaignSetupArgs args = null)
        {
            args ??= new CampaignSetupArgs
            {
                AgencyName = _agencyName,
                StartingRegionId = _startingRegionId,
                Seed = _seed,
                StartingBudget = _startingBudget,
                StartingReputation = _startingReputation
            };

            CurrentCampaign = CampaignFactory.CreateDefault(args);
            LoadCatalogs(CurrentCampaign);

            BindToUI();
            OnCampaignStarted?.Invoke(CurrentCampaign);

            return CurrentCampaign;
        }

        /// <summary>
        /// Restores a saved campaign session from a JSON string.
        /// </summary>
        public CampaignState LoadCampaign(string jsonSaveData)
        {
            if (string.IsNullOrWhiteSpace(jsonSaveData))
                throw new ArgumentNullException(nameof(jsonSaveData));

            var saveData = CampaignSaveSerializer.FromJson(jsonSaveData);
            CurrentCampaign = new CampaignState(saveData.Seed, saveData.CampaignId);
            LoadCatalogs(CurrentCampaign);
            CampaignSaveSerializer.RestoreFromSaveData(CurrentCampaign, saveData);

            BindToUI();
            OnCampaignLoaded?.Invoke(CurrentCampaign);

            return CurrentCampaign;
        }

        private void LoadCatalogs(CampaignState campaign)
        {
            // 1. TextAsset overrides (WebGL / inspector assigned)
            string researchJson = _researchTreeJson != null ? _researchTreeJson.text : null;
            string suppliersJson = _suppliersJson != null ? _suppliersJson.text : null;
            string eventsJson = _campaignEventsJson != null ? _campaignEventsJson.text : null;
            string milestonesJson = _milestonesJson != null ? _milestonesJson.text : null;
            string trJson = _localeTrJson != null ? _localeTrJson.text : null;
            string enJson = _localeEnJson != null ? _localeEnJson.text : null;

            // 2. Direct catalog directory load in Editor / Standalone
            try
            {
                string catalogDir = Path.Combine(Application.dataPath, "_Project", "Data", "Catalog", "Json");
                if (Directory.Exists(catalogDir))
                {
                    researchJson ??= ReadCatalogFile(catalogDir, "research_tree.json");
                    suppliersJson ??= ReadCatalogFile(catalogDir, "suppliers.json");
                    eventsJson ??= ReadCatalogFile(catalogDir, "campaign_events.json");
                    milestonesJson ??= ReadCatalogFile(catalogDir, "milestones.json");
                    trJson ??= ReadCatalogFile(catalogDir, "locale_tr.json");
                    enJson ??= ReadCatalogFile(catalogDir, "locale_en.json");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CampaignBootstrapper] Could not read catalog directory: {ex.Message}");
            }

            CampaignFactory.LoadCatalogsFromJson(campaign, researchJson, suppliersJson, eventsJson, milestonesJson);

            // 3. Load localization tables into singleton
            if (!string.IsNullOrEmpty(trJson))
            {
                Apogee.Core.Localization.LocalizationManager.Instance.LoadLocale(Apogee.Core.Localization.Language.TR, trJson);
            }
            if (!string.IsNullOrEmpty(enJson))
            {
                Apogee.Core.Localization.LocalizationManager.Instance.LoadLocale(Apogee.Core.Localization.Language.EN, enJson);
            }
        }

        private static string ReadCatalogFile(string dir, string filename)
        {
            try
            {
                string nameWithoutExt = Path.GetFileNameWithoutExtension(filename);
                var res = Resources.Load<TextAsset>("Catalog/Json/" + nameWithoutExt);
                if (res != null && !string.IsNullOrEmpty(res.text)) return res.text;
            }
            catch {}

            if (!string.IsNullOrEmpty(dir))
            {
                string path = Path.Combine(dir, filename);
                if (File.Exists(path)) return File.ReadAllText(path);
            }
            return null;
        }

        /// <summary>
        /// Injects the active campaign state into ApogeeUIManager and registered screen controllers.
        /// </summary>
        public void BindToUI()
        {
            if (_uiManager == null)
            {
                _uiManager = GetComponent<ApogeeUIManager>() ?? FindObjectOfType<ApogeeUIManager>();
            }

            if (_uiManager == null || CurrentCampaign == null) return;

            // Bind to Main Menu
            _uiManager.MainMenuController?.ViewModel?.BindLifecycleHandler(this);

            // Bind to Campaign Map & Solar Map
            _uiManager.CampaignMapController?.ViewModel?.BindCampaign(CurrentCampaign);
            _uiManager.SolarMapController?.ViewModel?.BindCampaign(CurrentCampaign);

            // Bind to Milestone Board
            _uiManager.MilestoneBoardController?.BindCampaign(CurrentCampaign);

            // Bind to Campaign End (do not OnEnter, only bind campaign data)
            _uiManager.CampaignEndController?.BindCampaign(CurrentCampaign);

            // Bind to Strategy Panels (M9-05)
            _uiManager.ContractsController?.ViewModel?.BindCampaign(CurrentCampaign);
            _uiManager.ResearchTreeController?.ViewModel?.BindCampaign(CurrentCampaign);
            _uiManager.BaseManagementController?.ViewModel?.BindCampaign(CurrentCampaign);
            _uiManager.ProductionQueueController?.ViewModel?.BindCampaign(CurrentCampaign);
        }
    }
}
