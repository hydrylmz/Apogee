using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core;
using Apogee.Design;
using Apogee.Design.Commands;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;
using Apogee.UI.Screens.Review;
using Apogee.UI.Screens.Templates;
using Apogee.UI.Screens.Comparison;

namespace Apogee.UI.Screens.Workshop
{
    /// <summary>
    /// ViewModel for the Design Workshop screen (GDD §7 & §14.3).
    /// Implements <see cref="IBlueprintHolder"/> for Core CommandBus integration (Undo/Redo),
    /// manages reactive slot list, module picker, and real-time hayalet delta telemetry preview.
    /// </summary>
    public class WorkshopViewModel : ViewModelBase, IBlueprintHolder
    {
        private readonly IScreenNavigator _navigator;
        private readonly CommandBus _commandBus;
        private readonly List<DesignComponent> _moduleCatalog = new();
        private Blueprint _activeBlueprint;

        // ── IBlueprintHolder ──────────────────────────────────────────────────────────

        public Blueprint ActiveBlueprint
        {
            get => _activeBlueprint;
            set
            {
                _activeBlueprint = value;
                RefreshTelemetry();
            }
        }

        // ── Telemetry Observables ───────────────────────────────────────────────────

        public ObservableValue<string> BlueprintName { get; } = new("Ay Gezgini (Lunar Rover Mk-1)");
        public ObservableValue<string> TargetDestination { get; } = new("Ay (Lunar Surface)");
        public ObservableValue<double> TotalDeltaV { get; } = new(0.0);
        public ObservableValue<double> TargetDeltaV { get; } = new(13500.0);
        public ObservableValue<double> TotalMassTons { get; } = new(0.0);
        public ObservableValue<double> TotalCostMillions { get; } = new(0.0);
        public ObservableValue<double> ReliabilityPercent { get; } = new(0.0);
        public ObservableValue<double> PowerMarginKw { get; } = new(0.0);
        public ObservableValue<int> MaxBuildDays { get; } = new(0);

        // ── Undo / Redo Observables ─────────────────────────────────────────────────

        public ObservableValue<bool> CanUndo { get; } = new(false);
        public ObservableValue<bool> CanRedo { get; } = new(false);

        // ── Slots & Picker ──────────────────────────────────────────────────────────

        private readonly List<VehicleSlotViewModel> _slots = new();
        public IReadOnlyList<VehicleSlotViewModel> Slots => _slots;

        public ObservableValue<VehicleSlotViewModel> SelectedSlot { get; } = new(null);
        public ObservableValue<IReadOnlyList<ModuleOptionViewModel>> AvailableOptions { get; } =
            new(Array.Empty<ModuleOptionViewModel>());

        public GhostDeltaViewModel GhostDelta { get; } = new();

        public RocketCrossSectionViewModel CrossSection { get; }

        public ITemplateRepository TemplateRepository { get; }
        public TemplateManagerViewModel Templates { get; }

        // ── Navigation & Actions ────────────────────────────────────────────────────

        public ViewActionCommand GoToReviewCommand { get; }
        public ViewActionCommand UndoCommand { get; }
        public ViewActionCommand RedoCommand { get; }
        public ViewActionCommand OpenComparisonCommand { get; }

        public WorkshopViewModel(
            IScreenNavigator navigator = null,
            CommandBus commandBus = null,
            Blueprint initialBlueprint = null,
            IEnumerable<DesignComponent> catalog = null,
            ITemplateRepository templateRepository = null)
        {
            _navigator = navigator;
            _commandBus = commandBus ?? new CommandBus();
            TemplateRepository = templateRepository ?? new InMemoryTemplateRepository();

            Templates = new TemplateManagerViewModel(TemplateRepository, _commandBus, _navigator)
            {
                GetActiveBlueprint = () => ActiveBlueprint,
                LoadBlueprintCallback = bp => ActiveBlueprint = bp
            };
            RegisterDisposable(Templates);

            // Populate catalog
            if (catalog != null)
            {
                _moduleCatalog.AddRange(catalog);
            }
            else
            {
                _moduleCatalog.AddRange(CreateDefaultCatalog());
            }

            // Setup Navigation & Toolbar Commands
            GoToReviewCommand = new ViewActionCommand(() =>
            {
                var args = new ReviewNavigationArgs(ActiveBlueprint, CreateValidationContext());
                _navigator?.GoTo(ScreenId.DesignReview, args);
            });

            UndoCommand = new ViewActionCommand(
                PerformUndo,
                () => CanUndo.Value
            );

            RedoCommand = new ViewActionCommand(
                PerformRedo,
                () => CanRedo.Value
            );

            OpenComparisonCommand = new ViewActionCommand(() =>
            {
                var all = TemplateRepository.GetAll();
                var targetTemplate = Templates.SelectedTemplate.Value != null
                    ? TemplateRepository.GetById(Templates.SelectedTemplate.Value.TemplateId)
                    : (all.Count > 0 ? all[0] : null);

                var targetBlueprint = targetTemplate != null
                    ? targetTemplate.Blueprint
                    : CreateDefaultLunarRoverBlueprint().WithSlotComponent(
                        VehicleSlotId.PayloadPower,
                        _moduleCatalog.FirstOrDefault(c => c.Id == "power-solar-array-advanced"));

                var targetLabel = targetTemplate != null ? targetTemplate.Name : "Alternatif (Gelişmiş Panel)";

                var args = new ComparisonNavigationArgs(ActiveBlueprint, targetBlueprint, ActiveBlueprint.Name, targetLabel, TargetDeltaV.Value);
                _navigator?.GoTo(ScreenId.Comparison, args);
            });

            // Initialize Slots
            InitializeSlots();

            // Initialize Cross-Section Layout ViewModel
            CrossSection = new RocketCrossSectionViewModel();
            CrossSection.SlotSelected += SelectSlot;

            // Initialize Blueprint
            _activeBlueprint = initialBlueprint ?? CreateDefaultLunarRoverBlueprint();
            RefreshTelemetry();
            UpdateUndoRedoState();
        }

        /// <summary>
        /// Creates a validation context corresponding to the workshop's active mission target (e.g. Moon rover).
        /// </summary>
        public ValidationContext CreateValidationContext()
        {
            return new ValidationContext(
                missionProfile: new MissionProfile(
                    id: "lunar-rover-mission",
                    requiredDeltaV: TargetDeltaV.Value,
                    minReliabilityThreshold: 0.50,
                    targetDestinationId: "moon",
                    targetMissionTypeId: "rover",
                    requiresEdl: true
                ),
                launchPad: LaunchPadInfo.Level2Medium
            );
        }

        private void InitializeSlots()
        {
            var slotDefinitions = new[]
            {
                VehicleSlotDefinition.Get(VehicleSlotId.PayloadBus),
                VehicleSlotDefinition.Get(VehicleSlotId.PayloadPower),
                VehicleSlotDefinition.Get(VehicleSlotId.PayloadThermal),
                VehicleSlotDefinition.Get(VehicleSlotId.PayloadComms),
                VehicleSlotDefinition.Get(VehicleSlotId.PayloadAdcs),
                VehicleSlotDefinition.Get(VehicleSlotId.PayloadEdl),
                VehicleSlotDefinition.Get(VehicleSlotId.PayloadInstrument1),
                VehicleSlotDefinition.Get(VehicleSlotId.Stage1Engine),
                VehicleSlotDefinition.Get(VehicleSlotId.Stage1Tank),
                VehicleSlotDefinition.Get(VehicleSlotId.Stage2Engine),
                VehicleSlotDefinition.Get(VehicleSlotId.Stage2Tank)
            };

            foreach (var def in slotDefinitions)
            {
                var slotDef = def;
                var vm = new VehicleSlotViewModel(slotDef, onSelected: () => SelectSlot(slotDef.Id));
                _slots.Add(vm);
            }
        }

        public void SelectSlot(VehicleSlotId slotId)
        {
            var target = _slots.FirstOrDefault(s => s.SlotId == slotId);
            if (target == null) return;

            foreach (var s in _slots)
            {
                s.IsSelected.Value = (s == target);
            }

            SelectedSlot.Value = target;
            CrossSection?.SetSelectedSlot(slotId);
            PopulateOptionsForSlot(target);
        }

        private void PopulateOptionsForSlot(VehicleSlotViewModel slot)
        {
            if (slot == null)
            {
                AvailableOptions.Value = Array.Empty<ModuleOptionViewModel>();
                return;
            }

            var category = slot.CategoryTag;
            var compatible = _moduleCatalog.Where(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();

            var currentEquipped = _activeBlueprint.GetComponentInSlot(slot.SlotId);

            var optionVms = compatible.Select(comp =>
            {
                var optionVm = new ModuleOptionViewModel(
                    comp,
                    isLocked: false,
                    lockReason: null,
                    onHover: () => HoverModuleOption(slot.SlotId, comp),
                    onUnhover: UnhoverModuleOption,
                    onSelect: () => EquipModuleOption(slot.SlotId, comp)
                );

                optionVm.IsCurrentlyEquipped.Value = currentEquipped != null && currentEquipped.Id == comp.Id;
                return optionVm;
            }).ToList();

            AvailableOptions.Value = optionVms;
        }

        // ── Ghost Delta (Hayalet Delta) ─────────────────────────────────────────────

        public void HoverModuleOption(VehicleSlotId slotId, DesignComponent candidate)
        {
            if (_activeBlueprint == null || candidate == null) return;

            var delta = Apogee.Design.GhostDelta.Compute(
                _activeBlueprint,
                slotId,
                candidate,
                lossProfile: null,
                requiredDeltaV: TargetDeltaV.Value);

            GhostDelta.ApplyDelta(delta);
        }

        public void UnhoverModuleOption()
        {
            GhostDelta.Clear();
        }

        // ── Command-based Selection ─────────────────────────────────────────────────

        public void EquipModuleOption(VehicleSlotId slotId, DesignComponent component)
        {
            if (component == null) return;

            var command = new SetSlotComponentCommand(slotId, component);
            var context = CommandContext.CreateLocalAdmin(this);

            var result = _commandBus.Execute(command, context);
            if (result.Success)
            {
                GhostDelta.Clear();
                UpdateUndoRedoState();
                if (SelectedSlot.Value != null)
                {
                    PopulateOptionsForSlot(SelectedSlot.Value);
                }
            }
        }

        public void PerformUndo()
        {
            var context = CommandContext.CreateLocalAdmin(this);
            var result = _commandBus.Undo(context);
            if (result.Success)
            {
                GhostDelta.Clear();
                UpdateUndoRedoState();
                if (SelectedSlot.Value != null)
                {
                    PopulateOptionsForSlot(SelectedSlot.Value);
                }
            }
        }

        public void PerformRedo()
        {
            var context = CommandContext.CreateLocalAdmin(this);
            var result = _commandBus.Redo(context);
            if (result.Success)
            {
                GhostDelta.Clear();
                UpdateUndoRedoState();
                if (SelectedSlot.Value != null)
                {
                    PopulateOptionsForSlot(SelectedSlot.Value);
                }
            }
        }

        private void UpdateUndoRedoState()
        {
            CanUndo.Value = _commandBus.CanUndo;
            CanRedo.Value = _commandBus.CanRedo;
            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        }

        // ── Telemetry Refresh ───────────────────────────────────────────────────────

        public void RefreshTelemetry()
        {
            if (_activeBlueprint == null) return;

            BlueprintName.Value = _activeBlueprint.Name;

            var stats = BlueprintEvaluator.Evaluate(_activeBlueprint, requiredDeltaV: TargetDeltaV.Value);

            TotalDeltaV.Value = stats.NetDeltaV;
            TotalMassTons.Value = stats.WetMass;
            TotalCostMillions.Value = stats.Cost;
            ReliabilityPercent.Value = stats.Reliability * 100.0;
            PowerMarginKw.Value = stats.PowerMargin;
            MaxBuildDays.Value = stats.BuildTimeDays;

            // Sync slots equipped state
            foreach (var slot in _slots)
            {
                var equipped = _activeBlueprint.GetComponentInSlot(slot.SlotId);
                slot.SetEquipped(equipped);
            }

            // Sync Cross-Section View Model
            CrossSection?.UpdateLayout(_activeBlueprint);
        }

        // ── Defaults & Factories ────────────────────────────────────────────────────

        public static List<DesignComponent> CreateDefaultCatalog()
        {
            return new List<DesignComponent>
            {
                // Engines
                new DesignComponent("engine-kerosene-lox-mk1", "engine", "Kerosen Motoru (Mk-1)", dryMass: 1.5, cost: 7.5, buildTime: 45, reliability: 0.95, isp: 310, thrust: 850),
                new DesignComponent("engine-kerosene-lox-heavy", "engine", "Ağır Kerosen Motoru", dryMass: 2.8, cost: 14.0, buildTime: 60, reliability: 0.94, isp: 320, thrust: 1900),
                new DesignComponent("engine-hydrolox-upper-mk1", "engine", "Hidrojen Üst Aşama Motoru", dryMass: 0.7, cost: 11.0, buildTime: 55, reliability: 0.93, isp: 445, thrust: 140),
                new DesignComponent("engine-solid-booster-mk1", "engine", "Katı Yakıt Güçlendirici", dryMass: 2.2, cost: 4.0, buildTime: 25, reliability: 0.97, isp: 265, thrust: 1300),

                // Tanks
                new DesignComponent("tank-kerosene-heavy", "tank", "Ağır Kerosen Tankı (65t)", dryMass: 4.0, cost: 3.5, buildTime: 30, reliability: 0.99),
                new DesignComponent("tank-kerosene-medium", "tank", "Orta Kerosen Tankı (25t)", dryMass: 2.0, cost: 2.0, buildTime: 20, reliability: 0.99),
                new DesignComponent("tank-hydrolox-upper", "tank", "Hidrojen Üst Tankı (12t)", dryMass: 1.2, cost: 2.8, buildTime: 28, reliability: 0.98),

                // Bus
                new DesignComponent("bus-rover-light", "bus", "Hafif Gezgin Şasisi", dryMass: 0.35, cost: 5.5, buildTime: 35, reliability: 0.96, powerConsumption: 0.25, thermalLoad: 0.15),
                new DesignComponent("bus-rover-heavy", "bus", "Ağır Görev Gezgin Şasisi", dryMass: 0.85, cost: 10.5, buildTime: 50, reliability: 0.98, powerConsumption: 0.55, thermalLoad: 0.30),

                // Power
                new DesignComponent("power-solar-array-basic", "power", "Katlanabilir Güneş Paneli", dryMass: 0.12, cost: 2.2, buildTime: 15, reliability: 0.94, powerGeneration: 1.1),
                new DesignComponent("power-solar-array-advanced", "power", "Gelişmiş Güneş Paneli", dryMass: 0.18, cost: 4.5, buildTime: 25, reliability: 0.97, powerGeneration: 2.4),
                new DesignComponent("power-rtg-compact", "power", "Kompakt RTG Nükleer Üreteç", dryMass: 0.05, cost: 16.0, buildTime: 90, reliability: 0.99, powerGeneration: 0.6, thermalLoad: 0.4),

                // Thermal
                new DesignComponent("thermal-insulation-mli", "thermal", "MLI Çok Katmanlı İzolasyon", dryMass: 0.06, cost: 1.0, buildTime: 10, reliability: 0.99, thermalDissipation: 0.6),
                new DesignComponent("thermal-active-heatpipe", "thermal", "Aktif Isı Borusu & Radyatör", dryMass: 0.15, cost: 2.6, buildTime: 20, reliability: 0.96, thermalDissipation: 1.8, powerConsumption: 0.10),

                // Comms
                new DesignComponent("comms-omni-antenna", "comms", "Düşük Kazançlı Omni Anten", dryMass: 0.03, cost: 0.9, buildTime: 12, reliability: 0.99, downlinkRate: 1.5, powerConsumption: 0.08),
                new DesignComponent("comms-dish-highgain", "comms", "Yüksek Kazançlı Çanak Anten", dryMass: 0.12, cost: 4.0, buildTime: 30, reliability: 0.96, downlinkRate: 28.0, powerConsumption: 0.35),

                // ADCS
                new DesignComponent("adcs-reaction-wheels", "adcs", "Tepki Tekerlekleri & Yıldız İzler", dryMass: 0.06, cost: 1.8, buildTime: 18, reliability: 0.97, powerConsumption: 0.12),

                // EDL
                new DesignComponent("edl-lunar-skycrane", "edl", "Ay Gökyüzü Vinci (Skycrane)", dryMass: 0.65, cost: 8.5, buildTime: 45, reliability: 0.93, powerConsumption: 0.15),
                new DesignComponent("edl-airbag-cushion", "edl", "Darbe Sönümleyici Hava Yastığı", dryMass: 0.30, cost: 3.8, buildTime: 22, reliability: 0.88),

                // Instruments
                new DesignComponent("inst-panoramic-camera", "instrument", "Panoramik Kamera Paketi", dryMass: 0.04, cost: 2.8, buildTime: 25, reliability: 0.97, dataRate: 3.2, powerConsumption: 0.06),
                new DesignComponent("inst-alpha-spectrometer", "instrument", "Alfa X-Işını Spektrometresi", dryMass: 0.07, cost: 4.8, buildTime: 35, reliability: 0.95, dataRate: 1.4, powerConsumption: 0.11),
                new DesignComponent("inst-subsurface-radar", "instrument", "Yüzey Altı Radarı", dryMass: 0.16, cost: 8.0, buildTime: 45, reliability: 0.92, dataRate: 9.0, powerConsumption: 0.28)
            };
        }

        public static Blueprint CreateDefaultLunarRoverBlueprint()
        {
            var catalog = CreateDefaultCatalog();
            var bus = catalog.First(c => c.Id == "bus-rover-light");
            var power = catalog.First(c => c.Id == "power-solar-array-basic");
            var thermal = catalog.First(c => c.Id == "thermal-insulation-mli");
            var comms = catalog.First(c => c.Id == "comms-omni-antenna");
            var adcs = catalog.First(c => c.Id == "adcs-reaction-wheels");
            var edl = catalog.First(c => c.Id == "edl-lunar-skycrane");
            var camera = catalog.First(c => c.Id == "inst-panoramic-camera");

            var payload = new PayloadDesign(
                "payload-lunar-rover",
                "Ay Yüzey Gezgini",
                bus: bus,
                power: power,
                thermal: thermal,
                comms: comms,
                adcs: adcs,
                edl: edl,
                instruments: new[] { camera }
            );

            var s1Engine = catalog.First(c => c.Id == "engine-kerosene-lox-heavy");
            var s1Tank = catalog.First(c => c.Id == "tank-kerosene-heavy");
            var stage1 = new StageDesign("stage-1", "Aşama 1 (Kalkış)", s1Engine, s1Tank, propellantMass: 140.0);

            var s2Engine = catalog.First(c => c.Id == "engine-hydrolox-upper-mk1");
            var s2Tank = catalog.First(c => c.Id == "tank-hydrolox-upper");
            var stage2 = new StageDesign("stage-2", "Aşama 2 (Transfer)", s2Engine, s2Tank, propellantMass: 35.0);

            return new Blueprint(
                "blueprint-lunar-rover-mk1",
                "Ay Gezgini (Rover Mk-1)",
                version: 1,
                targetDestinationId: "moon",
                targetMissionTypeId: "rover",
                payload: payload,
                stages: new[] { stage1, stage2 }
            );
        }
    }
}
