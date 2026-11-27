using System;
using System.Collections.Generic;
using System.Linq;

namespace Etazh.Prototype
{
    public enum TrafficPhase
    {
        Quiet,
        Building,
        Rush,
        FinalRush
    }

    public enum PassengerKind
    {
        Resident,
        Mover,
        PartyGuest,
        Courier
    }

    public static class ShiftRules
    {
        public const int FloorCount = 5;
        public const int TicksPerSecond = 10;
        public const int ShiftTicks = 300 * TicksPerSecond;
        public const int ApproachTicks = 2 * TicksPerSecond;
        public const int PatienceTicks = 25 * TicksPerSecond;
        public const int CourierPatienceTicks = 12 * TicksPerSecond;
        public const int PartyNoticeTicks = 10 * TicksPerSecond;
        public const int TravelTicksPerFloor = 12;
        public const int StopTicks = 7;
        public const int MaximumWeightKg = 400;
        public const int MaximumSpaceUnits = 4;
        public const int StairPenalty = 2;
        public const int InitialSatisfaction = 50;
        public const int MaximumSatisfaction = 100;
        public const int SatisfactionPerDelivery = 3;
        public const int SatisfactionLostPerDeparture = 6;

        private static readonly (int StartSecond, int IntervalSeconds, int Passengers)[] Waves =
        {
            (0, 10, 1),
            (55, 7, 2),
            (135, 5, 3),
            (220, 4, 4)
        };

        public static TrafficPhase PhaseAt(int tick)
        {
            for (int phase = Waves.Length - 1; phase > 0; phase--)
            {
                if (tick >= Waves[phase].StartSecond * TicksPerSecond)
                    return (TrafficPhase)phase;
            }

            return TrafficPhase.Quiet;
        }

        public static IReadOnlyList<PassengerCall> CreateSchedule()
        {
            var calls = new List<PassengerCall>
            {
                new(20, 1, 5),
                new(40, 3, 2, 150, 2),
                new(90, 2, 5, 100, 1),
                new(160, 5, 1, 50, 2),
                new(110 * TicksPerSecond, 1, 4, 100, 3, PassengerKind.Mover),
                new(157 * TicksPerSecond, 4, 1, 50, 1, PassengerKind.PartyGuest),
                new(160 * TicksPerSecond, 4, 1, 100, 1, PassengerKind.PartyGuest),
                new(163 * TicksPerSecond, 4, 1, 50, 1, PassengerKind.PartyGuest),
                new(197 * TicksPerSecond, 1, 5, 50, 1, PassengerKind.Courier)
            };
            var route = new Random(125);
            var load = new Random(526);
            for (int phase = 0; phase < Waves.Length; phase++)
            {
                var wave = Waves[phase];
                int startTick = (phase == 0 ? 26 : wave.StartSecond) * TicksPerSecond;
                int endTick = phase + 1 < Waves.Length ? Waves[phase + 1].StartSecond * TicksPerSecond : ShiftTicks;
                int interval = wave.IntervalSeconds * TicksPerSecond;
                for (int tick = startTick; tick < endTick; tick += interval)
                {
                    int firstFloor = route.Next(FloorCount);
                    for (int passenger = 0; passenger < wave.Passengers; passenger++)
                    {
                        int origin = (firstFloor + passenger) % FloorCount + 1;
                        int destination = route.Next(1, FloorCount);
                        if (destination >= origin)
                            destination++;

                        int weightKg = load.Next(1, 4) * 50;
                        int spaceUnits = load.Next(1, 3);
                        calls.Add(new PassengerCall(tick, origin, destination, weightKg, spaceUnits));
                    }
                }
            }

            return Array.AsReadOnly(calls.OrderBy(call => call.Tick).ToArray());
        }
    }

    public readonly struct PassengerCall
    {
        public PassengerCall(int tick, int origin, int destination, int weightKg = 50, int spaceUnits = 1,
            PassengerKind kind = PassengerKind.Resident)
        {
            if (tick < 0 || tick >= ShiftRules.ShiftTicks)
                throw new ArgumentOutOfRangeException(nameof(tick));
            if (origin < 1 || origin > ShiftRules.FloorCount)
                throw new ArgumentOutOfRangeException(nameof(origin));
            if (destination < 1 || destination > ShiftRules.FloorCount || destination == origin)
                throw new ArgumentOutOfRangeException(nameof(destination));
            if (weightKg < 1 || weightKg > ShiftRules.MaximumWeightKg)
                throw new ArgumentOutOfRangeException(nameof(weightKg));
            if (spaceUnits < 1 || spaceUnits > ShiftRules.MaximumSpaceUnits)
                throw new ArgumentOutOfRangeException(nameof(spaceUnits));
            if (kind < PassengerKind.Resident || kind > PassengerKind.Courier)
                throw new ArgumentOutOfRangeException(nameof(kind));

            Tick = tick;
            Origin = origin;
            Destination = destination;
            WeightKg = weightKg;
            SpaceUnits = spaceUnits;
            Kind = kind;
        }

        public int Tick { get; }
        public int Origin { get; }
        public int Destination { get; }
        public int WeightKg { get; }
        public int SpaceUnits { get; }
        public PassengerKind Kind { get; }
        public int PatienceTicks => Kind == PassengerKind.Courier ? ShiftRules.CourierPatienceTicks : ShiftRules.PatienceTicks;
        internal int PatienceDeadline => Tick + PatienceTicks;
        public int Reward => Kind switch
        {
            PassengerKind.Mover => 45,
            PassengerKind.Courier => 30,
            _ => WeightKg >= 150 ? 35 : WeightKg >= 100 ? 20 : 10
        };
    }
}
