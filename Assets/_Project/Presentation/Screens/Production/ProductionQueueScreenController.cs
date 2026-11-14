using System;
using Apogee.Strategy;
using Apogee.UI.Core.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Production
{
    /// <summary>
    /// UI Toolkit screen controller for Strategic Production and Testing Queue (ScreenId.ProductionQueue = 12).
    /// Complies with GDD §9.3, §14.1 (#11 Üretim/Test Kuyruğu), and M9-05.
    /// Provides active and queued job lists, factory/test facility utilization, and job commission actions.
    /// </summary>
    public sealed class ProductionQueueScreenController : ScreenControllerBase<ProductionQueueViewModel>
    {
        public override ScreenId ScreenId => ScreenId.ProductionQueue;

        // Top HUD
        private Label _titleLabel;
        private Label _budgetLabel;
        private Label _factoryCapLabel;
        private Label _testCapLabel;
        private Button _backBtn;

        // Jobs ScrollView
        private ScrollView _jobsList;

        // Quick Actions
        private Button _quickBuildBtn;
        private Button _quickTestBtn;

        // Footer
        private Label _statusLabel;

        public ProductionQueueScreenController(ProductionQueueViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("screen-root");
            root.AddToClassList("screen-production-queue");
            root.style.backgroundColor = new Color(0.04f, 0.06f, 0.10f, 1f);
            root.style.flexDirection = FlexDirection.Column;
            root.style.justifyContent = Justify.SpaceBetween;

            // ── TOP HEADER ──────────────────────────────────────────────────
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            header.style.paddingLeft = 16;
            header.style.paddingRight = 16;
            header.style.backgroundColor = new Color(0.07f, 0.10f, 0.16f, 0.98f);
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.18f, 0.25f, 0.38f, 0.8f);

            var leftHeader = new VisualElement();
            leftHeader.style.flexDirection = FlexDirection.Row;
            leftHeader.style.alignItems = Align.Center;

            _backBtn = new Button(() => ViewModel.BackCommand.Execute()) { text = "<- Geri (Harita)"};
            _backBtn.style.marginRight = 16;
            _backBtn.style.paddingLeft = 12;
            _backBtn.style.paddingRight = 12;
            _backBtn.style.paddingTop = 6;
            _backBtn.style.paddingBottom = 6;
            _backBtn.style.backgroundColor = new Color(0.15f, 0.20f, 0.30f);
            _backBtn.style.color = Color.white;
            leftHeader.Add(_backBtn);

            _titleLabel = new Label("ÜRETİM VE TEST KUYRUĞU");
            _titleLabel.style.fontSize = 16;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new Color(0.9f, 0.95f, 1f);
            leftHeader.Add(_titleLabel);
            header.Add(leftHeader);

            var rightHeader = new VisualElement();
            rightHeader.style.flexDirection = FlexDirection.Row;
            rightHeader.style.alignItems = Align.Center;

            _budgetLabel = CreatePill("0 M$", new Color(0.4f, 0.9f, 0.5f));
            _factoryCapLabel = CreatePill("Fabrika: 0/0", new Color(0.35f, 0.85f, 1f));
            _testCapLabel = CreatePill("Test Standı: 0/0", new Color(0.8f, 0.65f, 1f));

            rightHeader.Add(_budgetLabel);
            rightHeader.Add(_factoryCapLabel);
            rightHeader.Add(_testCapLabel);
            header.Add(rightHeader);

            root.Add(header);

            // ── MAIN BODY: JOBS & ACTION PANEL ──────────────────────────────
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;

            // Left: Jobs ScrollView
            var leftCol = new VisualElement();
            leftCol.style.flexGrow = 1;
            leftCol.style.paddingLeft = 16;
            leftCol.style.paddingRight = 16;
            leftCol.style.paddingTop = 14;
            leftCol.style.paddingBottom = 14;

            var listHeader = new Label("AKTİF VE PLANLANAN İŞ EMİRLERİ");
            listHeader.style.fontSize = 12;
            listHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            listHeader.style.color = new Color(0.4f, 0.8f, 1f);
            listHeader.style.marginBottom = 10;
            leftCol.Add(listHeader);

            _jobsList = new ScrollView();
            _jobsList.style.flexGrow = 1;
            leftCol.Add(_jobsList);
            body.Add(leftCol);

            // Right: Quick Actions Panel
            var rightCol = new VisualElement();
            rightCol.style.width = 320;
            rightCol.style.backgroundColor = new Color(0.07f, 0.10f, 0.16f, 0.98f);
            rightCol.style.borderLeftWidth = 1;
            rightCol.style.borderLeftColor = new Color(0.18f, 0.25f, 0.38f, 0.8f);
            rightCol.style.paddingLeft = 16;
            rightCol.style.paddingRight = 16;
            rightCol.style.paddingTop = 14;
            rightCol.style.paddingBottom = 14;
            rightCol.style.justifyContent = Justify.SpaceBetween;

            var actionTop = new VisualElement();

            var actionTitle = new Label("HIZLI İŞ EMRİ VER");
            actionTitle.style.fontSize = 12;
            actionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            actionTitle.style.color = new Color(0.4f, 0.8f, 1f);
            actionTitle.style.marginBottom = 12;
            actionTop.Add(actionTitle);

            var actionDesc = new Label("Atölyede tasarlanan araçları veya temel roket modellerini fabrikada montaja alabilir, ya da test tesisinde güvenilirlik kalifikasyonu başlatabilirsiniz.");
            actionDesc.style.fontSize = 11;
            actionDesc.style.color = new Color(0.75f, 0.85f, 0.95f);
            actionDesc.style.whiteSpace = WhiteSpace.Normal;
            actionDesc.style.marginBottom = 16;
            actionTop.Add(actionDesc);

            _quickBuildBtn = new Button(() => ViewModel.QuickBuildRocketCommand.Execute())
            {
                text = "Araç Montajı Başlat (40 M$, 20g)"
            };
            _quickBuildBtn.style.height = 36;
            _quickBuildBtn.style.backgroundColor = new Color(0.18f, 0.45f, 0.75f);
            _quickBuildBtn.style.color = Color.white;
            _quickBuildBtn.style.fontSize = 11;
            _quickBuildBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _quickBuildBtn.style.marginBottom = 10;
            actionTop.Add(_quickBuildBtn);

            _quickTestBtn = new Button(() => ViewModel.QuickStartTestCommand.Execute())
            {
                text = "Sistem Testi Başlat (10 M$, 25g)"
            };
            _quickTestBtn.style.height = 36;
            _quickTestBtn.style.backgroundColor = new Color(0.45f, 0.30f, 0.70f);
            _quickTestBtn.style.color = Color.white;
            _quickTestBtn.style.fontSize = 11;
            _quickTestBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            actionTop.Add(_quickTestBtn);

            rightCol.Add(actionTop);
            body.Add(rightCol);
            root.Add(body);

            // ── FOOTER ──────────────────────────────────────────────────────
            var footer = new VisualElement();
            footer.style.paddingTop = 6;
            footer.style.paddingBottom = 6;
            footer.style.paddingLeft = 16;
            footer.style.paddingRight = 16;
            footer.style.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new Color(0.15f, 0.20f, 0.30f);

            _statusLabel = new Label("Hazır.");
            _statusLabel.style.fontSize = 11;
            _statusLabel.style.color = new Color(0.4f, 0.8f, 1f);
            footer.Add(_statusLabel);
            root.Add(footer);

            return root;
        }

        protected override void OnInitialized()
        {
            RegisterObservables();
        }

        protected override void OnShown(object context)
        {
            if (context is CampaignState campaign)
            {
                ViewModel?.BindCampaign(campaign);
            }
            else
            {
                ViewModel?.Refresh();
            }

            RenderJobs();
        }

        private void RegisterObservables()
        {
            if (ViewModel == null) return;

            ViewModel.Budget.Subscribe(b =>
            {
                if (_budgetLabel != null) _budgetLabel.text = $"{b:F1} M$";
            });

            ViewModel.FactoryCapacityText.Subscribe(cap =>
            {
                if (_factoryCapLabel != null) _factoryCapLabel.text = $"Fabrika: {cap}";
            });

            ViewModel.TestCapacityText.Subscribe(cap =>
            {
                if (_testCapLabel != null) _testCapLabel.text = $"Test: {cap}";
            });

            ViewModel.Jobs.Subscribe(_ => RenderJobs());

            ViewModel.StatusMessage.Subscribe(msg =>
            {
                if (_statusLabel != null) _statusLabel.text = msg ?? string.Empty;
            });
        }

        private void RenderJobs()
        {
            if (_jobsList == null || ViewModel == null) return;
            _jobsList.Clear();

            var jobs = ViewModel.Jobs.Value;
            if (jobs == null || jobs.Count == 0)
            {
                var emptyLabel = new Label("Üretim veya test kuyruğunda herhangi bir iş emri bulunmamaktadır.");
                emptyLabel.style.color = new Color(0.6f, 0.7f, 0.8f);
                _jobsList.Add(emptyLabel);
                return;
            }

            foreach (var job in jobs)
            {
                var card = CreateJobCard(job);
                _jobsList.Add(card);
            }
        }

        private VisualElement CreateJobCard(ProductionJobItemViewModel job)
        {
            var card = new VisualElement();
            card.style.flexDirection = FlexDirection.Row;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.alignItems = Align.Center;
            card.style.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.style.marginBottom = 8;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;

            var left = new VisualElement();
            left.style.flexGrow = 1;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            var nameLabel = new Label(job.Name);
            nameLabel.style.fontSize = 13;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new Color(0.95f, 0.98f, 1f);
            nameLabel.style.marginRight = 8;
            titleRow.Add(nameLabel);

            var typeBadge = new Label(job.JobTypeName);
            typeBadge.style.fontSize = 10;
            typeBadge.style.color = new Color(0.5f, 0.8f, 1f);
            titleRow.Add(typeBadge);
            left.Add(titleRow);

            var meta = new Label($"{job.StatusName}  ·  {job.WorkedDays}/{job.TotalWorkDays} gün  ·  Kalan: {job.RemainingDays}g  ·  Kapasite: {job.RequiredCapacity:F0}  ·  Maliyet: {job.CostMUSD:F1} M$");
            meta.style.fontSize = 11;
            meta.style.color = new Color(0.6f, 0.75f, 0.9f);
            meta.style.marginTop = 2;
            meta.style.marginBottom = 4;
            left.Add(meta);

            // Progress bar
            var barTrack = new VisualElement();
            barTrack.style.height = 6;
            barTrack.style.backgroundColor = new Color(0.12f, 0.16f, 0.24f);
            barTrack.style.borderTopLeftRadius = 3;
            barTrack.style.borderTopRightRadius = 3;
            barTrack.style.borderBottomLeftRadius = 3;
            barTrack.style.borderBottomRightRadius = 3;

            var barFill = new VisualElement();
            barFill.style.height = Length.Percent(100);
            barFill.style.width = Length.Percent(job.ProgressFraction * 100f);
            barFill.style.backgroundColor = job.Status == ProductionJobStatus.Completed ? new Color(0.35f, 0.95f, 0.55f) :
                                            job.Status == ProductionJobStatus.InProgress ? new Color(0.35f, 0.85f, 1f) :
                                            new Color(0.8f, 0.65f, 0.35f);
            barTrack.Add(barFill);
            left.Add(barTrack);

            card.Add(left);

            var right = new VisualElement();
            right.style.marginLeft = 16;
            right.style.justifyContent = Justify.Center;

            if (job.CanCancel)
            {
                var cancelBtn = new Button(() => ViewModel.CancelJob(job.Id)) { text = "İptal" };
                cancelBtn.style.backgroundColor = new Color(0.40f, 0.15f, 0.15f);
                cancelBtn.style.color = new Color(1f, 0.7f, 0.7f);
                cancelBtn.style.paddingLeft = 8;
                cancelBtn.style.paddingRight = 8;
                cancelBtn.style.paddingTop = 4;
                cancelBtn.style.paddingBottom = 4;
                cancelBtn.style.fontSize = 11;
                right.Add(cancelBtn);
            }
            card.Add(right);

            return card;
        }

        private static Label CreatePill(string text, Color color)
        {
            var label = new Label(text);
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = color;
            label.style.marginLeft = 10;
            label.style.paddingLeft = 8;
            label.style.paddingRight = 8;
            label.style.paddingTop = 4;
            label.style.paddingBottom = 4;
            label.style.backgroundColor = new Color(0.04f, 0.07f, 0.12f, 0.85f);
            label.style.borderTopLeftRadius = 10;
            label.style.borderTopRightRadius = 10;
            label.style.borderBottomLeftRadius = 10;
            label.style.borderBottomRightRadius = 10;
            return label;
        }
    }
}
