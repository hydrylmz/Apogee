using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Represents the spacecraft / payload assembly mounted atop the final rocket stage.
    /// Includes bus structure, subsystems, scientific payload, instruments, and EDL/life support.
    /// Complies with GDD §7.2, §8.1, and §19.11.
    /// </summary>
    public sealed class PayloadDesign
    {
        private readonly List<DesignComponent> _instruments = new List<DesignComponent>();
        private readonly List<DesignComponent> _extraModules = new List<DesignComponent>();

        public string Id { get; }
        public string Name { get; }

        public DesignComponent Bus { get; }
        public DesignComponent Power { get; }
        public DesignComponent Thermal { get; }
        public DesignComponent Comms { get; }
        public DesignComponent Adcs { get; }
        public DesignComponent Edl { get; }
        public DesignComponent LifeSupport { get; }

        public IReadOnlyList<DesignComponent> Instruments => _instruments;
        public IReadOnlyList<DesignComponent> ExtraModules => _extraModules;

        public IEnumerable<DesignComponent> AllComponents
        {
            get
            {
                if (Bus != null) yield return Bus;
                if (Power != null) yield return Power;
                if (Thermal != null) yield return Thermal;
                if (Comms != null) yield return Comms;
                if (Adcs != null) yield return Adcs;
                if (Edl != null) yield return Edl;
                if (LifeSupport != null) yield return LifeSupport;

                foreach (var inst in _instruments)
                {
                    yield return inst;
                }
                foreach (var mod in _extraModules)
                {
                    yield return mod;
                }
            }
        }

        public double DryMass
        {
            get
            {
                double total = 0.0;
                foreach (var c in AllComponents)
                {
                    total += c.EffectiveDryMass;
                }
                return total;
            }
        }

        public double Cost
        {
            get
            {
                double total = 0.0;
                foreach (var c in AllComponents)
                {
                    total += c.EffectiveCost;
                }
                return total;
            }
        }

        public int BuildTimeDays
        {
            get
            {
                int max = 0;
                foreach (var c in AllComponents)
                {
                    max = Math.Max(max, c.EffectiveBuildTime);
                }
                return max;
            }
        }

        public double Reliability
        {
            get
            {
                double r = 1.0;
                foreach (var c in AllComponents)
                {
                    r *= c.EffectiveReliability;
                }
                return r;
            }
        }

        public double PowerGeneration
        {
            get
            {
                double total = 0.0;
                foreach (var c in AllComponents)
                {
                    total += c.PowerGeneration;
                }
                return total;
            }
        }

        public double PowerConsumption
        {
            get
            {
                double total = 0.0;
                foreach (var c in AllComponents)
                {
                    total += c.PowerConsumption;
                }
                return total;
            }
        }

        public double ThermalLoad
        {
            get
            {
                double total = 0.0;
                foreach (var c in AllComponents)
                {
                    total += c.ThermalLoad;
                }
                return total;
            }
        }

        public double ThermalDissipation
        {
            get
            {
                double total = 0.0;
                foreach (var c in AllComponents)
                {
                    total += c.ThermalDissipation;
                }
                return total;
            }
        }

        public double DataRate
        {
            get
            {
                double total = 0.0;
                foreach (var c in AllComponents)
                {
                    total += c.DataRate;
                }
                return total;
            }
        }

        public double DataStorage
        {
            get
            {
                double total = 0.0;
                foreach (var c in AllComponents)
                {
                    total += c.DataStorage;
                }
                return total;
            }
        }

        public double DownlinkRate
        {
            get
            {
                double total = 0.0;
                foreach (var c in AllComponents)
                {
                    total += c.DownlinkRate;
                }
                return total;
            }
        }

        public PayloadDesign(
            string id,
            string name = null,
            DesignComponent bus = null,
            DesignComponent power = null,
            DesignComponent thermal = null,
            DesignComponent comms = null,
            DesignComponent adcs = null,
            DesignComponent edl = null,
            DesignComponent lifeSupport = null,
            IEnumerable<DesignComponent> instruments = null,
            IEnumerable<DesignComponent> extraModules = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Payload ID cannot be empty.", nameof(id));

            Id = id;
            Name = name ?? id;
            Bus = bus;
            Power = power;
            Thermal = thermal;
            Comms = comms;
            Adcs = adcs;
            Edl = edl;
            LifeSupport = lifeSupport;

            if (instruments != null)
            {
                _instruments.AddRange(instruments);
            }
            if (extraModules != null)
            {
                _extraModules.AddRange(extraModules);
            }
        }

        public PayloadDesign WithBus(DesignComponent bus) =>
            new PayloadDesign(Id, Name, bus, Power, Thermal, Comms, Adcs, Edl, LifeSupport, _instruments, _extraModules);

        public PayloadDesign WithPower(DesignComponent power) =>
            new PayloadDesign(Id, Name, Bus, power, Thermal, Comms, Adcs, Edl, LifeSupport, _instruments, _extraModules);

        public PayloadDesign WithThermal(DesignComponent thermal) =>
            new PayloadDesign(Id, Name, Bus, Power, thermal, Comms, Adcs, Edl, LifeSupport, _instruments, _extraModules);

        public PayloadDesign WithComms(DesignComponent comms) =>
            new PayloadDesign(Id, Name, Bus, Power, Thermal, comms, Adcs, Edl, LifeSupport, _instruments, _extraModules);

        public PayloadDesign WithAdcs(DesignComponent adcs) =>
            new PayloadDesign(Id, Name, Bus, Power, Thermal, Comms, adcs, Edl, LifeSupport, _instruments, _extraModules);

        public PayloadDesign WithEdl(DesignComponent edl) =>
            new PayloadDesign(Id, Name, Bus, Power, Thermal, Comms, Adcs, edl, LifeSupport, _instruments, _extraModules);

        public PayloadDesign WithLifeSupport(DesignComponent lifeSupport) =>
            new PayloadDesign(Id, Name, Bus, Power, Thermal, Comms, Adcs, Edl, lifeSupport, _instruments, _extraModules);

        public PayloadDesign WithInstrument(int index, DesignComponent instrument)
        {
            var newInstruments = new List<DesignComponent>(_instruments);
            while (newInstruments.Count <= index)
            {
                newInstruments.Add(null);
            }
            newInstruments[index] = instrument;
            // Trim nulls from end
            while (newInstruments.Count > 0 && newInstruments[newInstruments.Count - 1] == null)
            {
                newInstruments.RemoveAt(newInstruments.Count - 1);
            }
            return new PayloadDesign(Id, Name, Bus, Power, Thermal, Comms, Adcs, Edl, LifeSupport, newInstruments, _extraModules);
        }

        public override string ToString() => $"[Payload: {Id} Mass: {DryMass:0.##}t Cost: {Cost:0.##}M$ Instruments: {_instruments.Count}]";
    }
}
