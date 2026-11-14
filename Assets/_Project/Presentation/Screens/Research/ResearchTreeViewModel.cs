using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core;
using Apogee.Data;
using Apogee.Strategy;
using Apogee.Strategy.Campaign;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Research
{
    /// <summary>
    /// ViewModel for an individual research tree node card (GDD §9.2).
    /// </summary>
    public sealed class ResearchNodeItemViewModel
    {
        public string Id { get; }
        public string Name { get; }
        public string Branch { get; }
        public int Tier { get; }
        public ResearchStatus Status { get; }
        public double ResearchPointCost { get; }
        public int DurationDays { get; }
        public double StartCostMUSD { get; }
        public IReadOnlyList<string> Prerequisites { get; }
        public IReadOnlyList<string> UnlockedModules { get; }
        public string Description { get; }
        public int ProgressDays { get; }
        public float ProgressFraction { get; }
        public bool CanStart { get; }

        public bool IsLocked => Status == ResearchStatus.Locked;
        public bool IsAvailable => Status == ResearchStatus.Available;
        public bool IsInProgress => Status == ResearchStatus.InProgress;
        public bool IsCompleted => Status == ResearchStatus.Completed;

        public ResearchNodeItemViewModel(
            ResearchNodeRecord record,
            ResearchNodeState state,
            double currentBudget)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            Id = record.Id;
            Name = !string.IsNullOrWhiteSpace(record.NameKey) ? record.NameKey : record.Id;
            Branch = record.Branch ?? "General";
            Tier = record.Tier;
            Status = state?.Status ?? ResearchStatus.Locked;
            ResearchPointCost = record.ResearchPointCost;
            DurationDays = Math.Max(1, record.BaseDurationDays);
            StartCostMUSD = record.StartCostMUSD;
            Prerequisites = record.Prerequisites ?? (IReadOnlyList<string>)Array.Empty<string>();
            UnlockedModules = record.UnlocksModuleIds ?? (IReadOnlyList<string>)Array.Empty<string>();
            Description = !string.IsNullOrWhiteSpace(record.DescKey) ? record.DescKey : $"{Branch} araştırma düğümü";

            ProgressDays = state?.ProgressDays ?? 0;
            ProgressFraction = DurationDays > 0 ? Math.Clamp((float)ProgressDays / DurationDays, 0f, 1f) : 0f;
            CanStart = Status == ResearchStatus.Available && currentBudget >= StartCostMUSD;
        }
    }

    /// <summary>
    /// ViewModel for the Strategic Research Tree Screen (ScreenId.ResearchTree = 10).
    /// Complies with GDD §9.2, §14.1 (#9 Araştırma Ağacı), and M9-05.
    /// Exposes research nodes by branch, active queue state, and dispatches StartResearchCommand.
    /// </summary>
    public sealed class ResearchTreeViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;
        private CampaignState _campaign;
        private readonly List<ResearchNodeItemViewModel> _allItems = new();

        public ObservableValue<double> ResearchPoints { get; } = new(0.0);
        public ObservableValue<double> KnowledgePoints { get; } = new(0.0);
        public ObservableValue<double> Budget { get; } = new(0.0);

        public ObservableValue<string> ActiveResearchTitle { get; } = new("Aktif Araştırma: Yok");
        public ObservableValue<string> ActiveResearchProgress { get; } = new("0 / 0 gün");
        public ObservableValue<float> ActiveResearchProgressFraction { get; } = new(0f);
        public ObservableValue<int> QueueCount { get; } = new(0);

        public ObservableValue<string> SelectedBranch { get; } = new("Tümü");
        public ObservableValue<ResearchNodeItemViewModel> SelectedNode { get; } = new(null);
        public ObservableValue<IReadOnlyList<ResearchNodeItemViewModel>> VisibleNodes { get; } = new(Array.Empty<ResearchNodeItemViewModel>());
        public ObservableValue<IReadOnlyList<string>> Branches { get; } = new(Array.Empty<string>());
        public ObservableValue<string> StatusMessage { get; } = new("Hazır.");

        public ViewActionCommand BackCommand { get; }
        public ViewActionCommand StartSelectedResearchCommand { get; }
        public ViewActionCommand CancelActiveResearchCommand { get; }

        public CampaignState Campaign => _campaign;

        public ResearchTreeViewModel(IScreenNavigator navigator, CampaignState campaign = null)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

            BackCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.CampaignMap));
            StartSelectedResearchCommand = new ViewActionCommand(StartSelectedNode);
            CancelActiveResearchCommand = new ViewActionCommand(() => CancelActiveResearch());

            if (campaign != null)
            {
                BindCampaign(campaign);
            }
        }

        public void BindCampaign(CampaignState campaign)
        {
            if (_campaign?.Research != null)
            {
                _campaign.Research.OnActiveResearchChanged -= HandleActiveResearchChanged;
                _campaign.Research.OnNodeCompleted -= HandleNodeCompleted;
                _campaign.Research.OnResearchProgressAdvanced -= HandleProgressAdvanced;
            }

            _campaign = campaign;

            if (_campaign?.Research != null)
            {
                _campaign.Research.OnActiveResearchChanged += HandleActiveResearchChanged;
                _campaign.Research.OnNodeCompleted += HandleNodeCompleted;
                _campaign.Research.OnResearchProgressAdvanced += HandleProgressAdvanced;
            }

            Refresh();
        }

        private void HandleActiveResearchChanged(string activeNodeId) => Refresh();
        private void HandleNodeCompleted(string nodeId, ResearchNodeRecord record) => Refresh();
        private void HandleProgressAdvanced(string nodeId, int currentProgress, int totalDuration) => Refresh();

        public void Refresh()
        {
            if (_campaign == null)
            {
                ResearchPoints.Value = 0.0;
                KnowledgePoints.Value = 0.0;
                Budget.Value = 0.0;
                ActiveResearchTitle.Value = "Aktif Araştırma: Kampanya Yok";
                VisibleNodes.Value = Array.Empty<ResearchNodeItemViewModel>();
                return;
            }

            ResearchPoints.Value = _campaign.ResearchPoints;
            KnowledgePoints.Value = _campaign.KnowledgePoints;
            Budget.Value = _campaign.Budget;

            _allItems.Clear();
            var branchSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Tümü" };

            if (_campaign.Research != null)
            {
                var tree = _campaign.Research.GetTreeByBranch();
                foreach (var kvp in tree)
                {
                    branchSet.Add(kvp.Key);
                    foreach (var pair in kvp.Value)
                    {
                        var item = new ResearchNodeItemViewModel(pair.Record, pair.State, _campaign.Budget);
                        _allItems.Add(item);
                    }
                }

                // Active queue summary
                string activeId = _campaign.Research.ActiveNodeId;
                QueueCount.Value = _campaign.Research.Queue.Count;

                if (!string.IsNullOrEmpty(activeId))
                {
                    var rec = _campaign.Research.GetRecord(activeId);
                    var state = _campaign.Research.GetNodeState(activeId);
                    string name = rec?.NameKey ?? activeId;
                    int dur = rec != null ? Math.Max(1, rec.BaseDurationDays) : 1;
                    int prog = state?.ProgressDays ?? 0;
                    ActiveResearchTitle.Value = $"{name} (Kuyruk: {_campaign.Research.Queue.Count})";
                    ActiveResearchProgress.Value = $"{prog} / {dur} gün";
                    ActiveResearchProgressFraction.Value = Math.Clamp((float)prog / dur, 0f, 1f);
                }
                else
                {
                    ActiveResearchTitle.Value = "Aktif Araştırma: Yok";
                    ActiveResearchProgress.Value = "—";
                    ActiveResearchProgressFraction.Value = 0f;
                }
            }

            Branches.Value = branchSet.OrderBy(b => b == "Tümü" ? "" : b).ToList();
            ApplyFilter();

            // Re-select or select first available
            if (SelectedNode.Value != null)
            {
                var refreshedSelected = _allItems.FirstOrDefault(i => i.Id == SelectedNode.Value.Id);
                SelectedNode.Value = refreshedSelected ?? _allItems.FirstOrDefault();
            }
            else
            {
                SelectedNode.Value = _allItems.FirstOrDefault(i => i.IsAvailable) ?? _allItems.FirstOrDefault();
            }
        }

        public void SelectBranch(string branch)
        {
            SelectedBranch.Value = string.IsNullOrWhiteSpace(branch) ? "Tümü" : branch;
            ApplyFilter();
        }

        public void SelectNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            var node = _allItems.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            if (node != null)
            {
                SelectedNode.Value = node;
            }
        }

        private void ApplyFilter()
        {
            string b = SelectedBranch.Value;
            if (string.IsNullOrEmpty(b) || b == "Tümü")
            {
                VisibleNodes.Value = _allItems.ToList();
            }
            else
            {
                VisibleNodes.Value = _allItems.Where(n => string.Equals(n.Branch, b, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        private void StartSelectedNode()
        {
            if (SelectedNode.Value == null) return;
            StartResearch(SelectedNode.Value.Id);
        }

        public bool StartResearch(string nodeId)
        {
            if (_campaign == null)
            {
                StatusMessage.Value = "Hata: Kampanya durumu bulunamadı.";
                return false;
            }

            var cmd = new StartResearchCommand(nodeId);
            var result = _campaign.Execute(cmd, PlayerRole.All);

            if (result.Success)
            {
                StatusMessage.Value = $"Araştırma başlatıldı: {nodeId}";
                Refresh();
                return true;
            }
            else
            {
                StatusMessage.Value = $"Başlatılamadı: {result.Message}";
                return false;
            }
        }

        public bool CancelActiveResearch()
        {
            if (_campaign?.Research == null || string.IsNullOrEmpty(_campaign.Research.ActiveNodeId))
            {
                StatusMessage.Value = "İptal edilecek aktif araştırma yok.";
                return false;
            }

            return CancelResearch(_campaign.Research.ActiveNodeId);
        }

        public bool CancelResearch(string nodeId)
        {
            if (_campaign == null) return false;

            var cmd = new CancelResearchCommand(nodeId);
            var result = _campaign.Execute(cmd, PlayerRole.All);

            if (result.Success)
            {
                StatusMessage.Value = $"Araştırma iptal edildi: {nodeId}";
                Refresh();
                return true;
            }
            else
            {
                StatusMessage.Value = $"İptal edilemedi: {result.Message}";
                return false;
            }
        }

        public override void Dispose()
        {
            if (_campaign?.Research != null)
            {
                _campaign.Research.OnActiveResearchChanged -= HandleActiveResearchChanged;
                _campaign.Research.OnNodeCompleted -= HandleNodeCompleted;
                _campaign.Research.OnResearchProgressAdvanced -= HandleProgressAdvanced;
            }
            base.Dispose();
        }
    }
}
