using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.Strategy.Campaign;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Production
{
    /// <summary>
    /// ViewModel for an individual job card in the production queue (GDD §9.3).
    /// </summary>
    public sealed class ProductionJobItemViewModel
    {
        public string Id { get; }
        public string Name { get; }
        public ProductionJobType JobType { get; }
        public string JobTypeName { get; }
        public ProductionJobStatus Status { get; }
        public string StatusName { get; }
        public float ProgressFraction { get; }
        public int WorkedDays { get; }
        public int TotalWorkDays { get; }
        public int RemainingDays { get; }
        public double CostMUSD { get; }
        public double RequiredCapacity { get; }
        public bool CanCancel { get; }

        public ProductionJobItemViewModel(ProductionJob job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));

            Id = job.Id;
            Name = job.Name;
            JobType = job.JobType;
            JobTypeName = GetJobTypeName(job.JobType);
            Status = job.Status;
            StatusName = GetStatusName(job.Status);
            ProgressFraction = (float)job.ProgressFraction;
            WorkedDays = (int)job.WorkedDays;
            TotalWorkDays = job.TotalWorkDays;
            RemainingDays = job.RemainingDays;
            CostMUSD = job.CostMUSD;
            RequiredCapacity = job.RequiredCapacity;
            CanCancel = job.Status == ProductionJobStatus.InProgress || job.Status == ProductionJobStatus.Queued;
        }

        public static string GetJobTypeName(ProductionJobType type) => type switch
        {
            ProductionJobType.VehicleAssembly => "Araç Montajı",
            ProductionJobType.ComponentFabrication => "Parça İmalatı",
            ProductionJobType.SystemTesting => "Kalifikasyon Testi",
            _ => type.ToString()
        };

        public static string GetStatusName(ProductionJobStatus status) => status switch
        {
            ProductionJobStatus.Queued => "Kuyrukta",
            ProductionJobStatus.InProgress => "> Üretimde",
            ProductionJobStatus.Paused => "Duraklatıldı",
            ProductionJobStatus.Completed => "OK Tamamlandı",
            ProductionJobStatus.Cancelled => "X İptal Edildi",
            _ => status.ToString()
        };
    }

    /// <summary>
    /// ViewModel for the Strategic Production & Testing Queue Screen (ScreenId.ProductionQueue = 12).
    /// Complies with GDD §9.3, §14.1 (#11 Üretim/Test Kuyruğu), and M9-05.
    /// Exposes active and queued manufacturing jobs, factory/test capacity, and dispatches StartProduction / Testing commands.
    /// </summary>
    public sealed class ProductionQueueViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;
        private CampaignState _campaign;

        public ObservableValue<double> Budget { get; } = new(0.0);
        public ObservableValue<string> FactoryCapacityText { get; } = new("0 / 0");
        public ObservableValue<float> FactoryCapacityFraction { get; } = new(0f);
        public ObservableValue<string> TestCapacityText { get; } = new("0 / 0");
        public ObservableValue<float> TestCapacityFraction { get; } = new(0f);
        public ObservableValue<int> ActiveJobsCount { get; } = new(0);
        public ObservableValue<int> QueuedJobsCount { get; } = new(0);

        public ObservableValue<IReadOnlyList<ProductionJobItemViewModel>> Jobs { get; } =
            new(Array.Empty<ProductionJobItemViewModel>());

        public ObservableValue<string> StatusMessage { get; } = new("Hazır.");

        public ViewActionCommand BackCommand { get; }
        public ViewActionCommand QuickBuildRocketCommand { get; }
        public ViewActionCommand QuickStartTestCommand { get; }

        public CampaignState Campaign => _campaign;

        public ProductionQueueViewModel(IScreenNavigator navigator, CampaignState campaign = null)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

            BackCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.CampaignMap));
            QuickBuildRocketCommand = new ViewActionCommand(() => StartQuickVehicleProduction());
            QuickStartTestCommand = new ViewActionCommand(() => StartQuickTesting());

            if (campaign != null)
            {
                BindCampaign(campaign);
            }
        }

        public void BindCampaign(CampaignState campaign)
        {
            if (_campaign?.Production != null)
            {
                _campaign.Production.OnJobEnqueued -= HandleJobUpdated;
                _campaign.Production.OnJobStarted -= HandleJobUpdated;
                _campaign.Production.OnJobCompleted -= HandleJobUpdated;
                _campaign.Production.OnJobCancelled -= HandleJobUpdated;
            }

            _campaign = campaign;

            if (_campaign?.Production != null)
            {
                _campaign.Production.OnJobEnqueued += HandleJobUpdated;
                _campaign.Production.OnJobStarted += HandleJobUpdated;
                _campaign.Production.OnJobCompleted += HandleJobUpdated;
                _campaign.Production.OnJobCancelled += HandleJobUpdated;
            }

            Refresh();
        }

        private void HandleJobUpdated(ProductionJob job) => Refresh();

        public void Refresh()
        {
            if (_campaign == null)
            {
                Budget.Value = 0.0;
                FactoryCapacityText.Value = "0 / 0";
                TestCapacityText.Value = "0 / 0";
                ActiveJobsCount.Value = 0;
                QueuedJobsCount.Value = 0;
                Jobs.Value = Array.Empty<ProductionJobItemViewModel>();
                return;
            }

            Budget.Value = _campaign.Budget;

            if (_campaign.Production != null)
            {
                double totalFactCap = _campaign.Production.GetTotalFactoryCapacity(_campaign);
                double usedFactCap = _campaign.Production.GetActiveFactoryCapacityUsage();
                FactoryCapacityText.Value = $"{usedFactCap:F0} / {totalFactCap:F0}";
                FactoryCapacityFraction.Value = totalFactCap > 0 ? (float)Math.Clamp(usedFactCap / totalFactCap, 0.0, 1.0) : 0f;

                double totalTestCap = _campaign.Production.GetTotalTestCapacity(_campaign);
                double usedTestCap = _campaign.Production.AllJobs
                    .Where(j => j.Status == ProductionJobStatus.InProgress && j.JobType == ProductionJobType.SystemTesting)
                    .Sum(j => j.RequiredCapacity);
                TestCapacityText.Value = $"{usedTestCap:F0} / {totalTestCap:F0}";
                TestCapacityFraction.Value = totalTestCap > 0 ? (float)Math.Clamp(usedTestCap / totalTestCap, 0.0, 1.0) : 0f;

                var list = _campaign.Production.AllJobs
                    .Select(j => new ProductionJobItemViewModel(j))
                    .OrderBy(j => j.Status == ProductionJobStatus.InProgress ? 0 : j.Status == ProductionJobStatus.Queued ? 1 : 2)
                    .ThenBy(j => j.RemainingDays)
                    .ToList();

                Jobs.Value = list;
                ActiveJobsCount.Value = list.Count(j => j.Status == ProductionJobStatus.InProgress);
                QueuedJobsCount.Value = list.Count(j => j.Status == ProductionJobStatus.Queued);
            }
        }

        public bool StartVehicleProduction(
            string jobId,
            Blueprint blueprint,
            double? customCost = null,
            int? customBuildDays = null,
            double requiredCapacity = 5.0)
        {
            if (_campaign == null)
            {
                StatusMessage.Value = "Hata: Kampanya durumu bulunamadı.";
                return false;
            }

            var cmd = new StartVehicleProductionCommand(jobId, blueprint, customCost, customBuildDays, requiredCapacity);
            var result = _campaign.Execute(cmd, PlayerRole.ProcurementDirector);

            if (result.Success)
            {
                StatusMessage.Value = $"Araç montajı başlatıldı: {jobId}";
                Refresh();
                return true;
            }
            else
            {
                StatusMessage.Value = $"Başlatılamadı: {result.Message}";
                return false;
            }
        }

        public bool StartQuickVehicleProduction(string vehicleName = "Atlas-1 Roketi")
        {
            if (_campaign == null) return false;

            int count = (_campaign.Production?.AllJobs.Count ?? 0) + 1;
            string jobId = $"JOB-VEH-{count:D3}";
            var bp = new Blueprint($"bp-quick-{count}", vehicleName)
                .WithSlotComponent(VehicleSlotId.Stage1Engine, new DesignComponent(
                    "eng-basic", "engine", dryMass: 2.5, cost: 40.0, buildTime: 20, reliability: 0.90));

            return StartVehicleProduction(jobId, bp, customCost: 40.0, customBuildDays: 20, requiredCapacity: 5.0);
        }

        public bool StartTesting(
            string jobId,
            string testName,
            string blueprintId = null,
            int durationDays = 30,
            double costMUSD = 15.0,
            double reliabilityBonus = 0.05)
        {
            if (_campaign == null)
            {
                StatusMessage.Value = "Hata: Kampanya durumu bulunamadı.";
                return false;
            }

            var cmd = new StartTestingCommand(jobId, testName, blueprintId, durationDays, costMUSD, reliabilityBonus);
            var result = _campaign.Execute(cmd, PlayerRole.ProcurementDirector);

            if (result.Success)
            {
                StatusMessage.Value = $"Sistem testi başlatıldı: {testName}";
                Refresh();
                return true;
            }
            else
            {
                StatusMessage.Value = $"Test başlatılamadı: {result.Message}";
                return false;
            }
        }

        public bool StartQuickTesting(string testName = "Sistem Güvenilirlik Testi")
        {
            if (_campaign == null) return false;

            int count = (_campaign.Production?.AllJobs.Count ?? 0) + 1;
            string jobId = $"JOB-TST-{count:D3}";
            return StartTesting(jobId, testName, durationDays: 25, costMUSD: 10.0, reliabilityBonus: 0.05);
        }

        public bool CancelJob(string jobId)
        {
            if (_campaign == null) return false;

            var cmd = new CancelProductionJobCommand(jobId);
            var result = _campaign.Execute(cmd, PlayerRole.ProcurementDirector);

            if (result.Success)
            {
                StatusMessage.Value = $"İş emri iptal edildi: {jobId}";
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
            if (_campaign?.Production != null)
            {
                _campaign.Production.OnJobEnqueued -= HandleJobUpdated;
                _campaign.Production.OnJobStarted -= HandleJobUpdated;
                _campaign.Production.OnJobCompleted -= HandleJobUpdated;
                _campaign.Production.OnJobCancelled -= HandleJobUpdated;
            }
            base.Dispose();
        }
    }
}
