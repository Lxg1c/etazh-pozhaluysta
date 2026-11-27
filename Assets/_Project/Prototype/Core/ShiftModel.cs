using System;
using System.Collections.Generic;
using System.Linq;

namespace Etazh.Prototype
{
    public enum LiftState
    {
        Idle,
        Moving,
        DoorsOpen
    }

    public sealed class ShiftModel
    {
        private readonly IReadOnlyList<PassengerCall> _schedule;
        private readonly List<PassengerCall>[] _waiting = new List<PassengerCall>[ShiftRules.FloorCount];
        private readonly List<(PassengerCall Passenger, int BoardedAt, int QueueIndex)> _onBoard = new();
        private readonly List<(PassengerCall Passenger, int Tick, int Seat, int CabinCount)>[] _alighted =
            new List<(PassengerCall, int, int, int)>[ShiftRules.FloorCount];
        private readonly int[] _ridingTo = new int[ShiftRules.FloorCount];
        private readonly int[] _lastDeparture = new int[ShiftRules.FloorCount];
        private readonly PassengerCall[] _departed = new PassengerCall[ShiftRules.FloorCount];
        private readonly (int Tick, int Boarded, int Delivered)[] _transfers =
            new (int, int, int)[ShiftRules.FloorCount];

        private int _nextCall;
        private int _movementStart;
        private int _nextLiftEvent;

        public ShiftModel(IReadOnlyList<PassengerCall> schedule)
        {
            if (schedule == null)
                throw new ArgumentNullException(nameof(schedule));

            _schedule = schedule.OrderBy(call => call.Tick).ToArray();
            for (int index = 0; index < _waiting.Length; index++)
            {
                _waiting[index] = new List<PassengerCall>();
                _alighted[index] = new List<(PassengerCall, int, int, int)>();
                _lastDeparture[index] = -1;
                _transfers[index] = (-1, 0, 0);
            }

            AddScheduledCalls();
            TryBoardWhileStopped();
        }

        public int Tick { get; private set; }
        public int CurrentFloor { get; private set; } = 1;
        public int TargetFloor { get; private set; } = 1;
        public int Delivered { get; private set; }
        public int LeftByStairs { get; private set; }
        public int DeliveredPoints { get; private set; }
        public int Satisfaction { get; private set; } = ShiftRules.InitialSatisfaction;
        public int WeightKg { get; private set; }
        public int UsedSpace { get; private set; }
        public int OnBoardCount => _onBoard.Count;
        public LiftState State { get; private set; }
        public int DoorsOpenedAt { get; private set; }
        public int DoorsCloseAt => State == LiftState.DoorsOpen ? _nextLiftEvent : 0;
        public int Score => DeliveredPoints - LeftByStairs * ShiftRules.StairPenalty;
        public bool Finished => Tick >= ShiftRules.ShiftTicks;

        public int WaitingAt(int floor) => _waiting[floor - 1].Count;

        public int RidingTo(int floor) => _ridingTo[floor - 1];

        public int LastDepartureAt(int floor) => _lastDeparture[floor - 1];

        public PassengerCall DepartedPassengerAt(int floor) => _departed[floor - 1];

        public PassengerCall WaitingPassengerAt(int floor, int index) => _waiting[floor - 1][index];

        public PassengerCall RidingPassengerAt(int index) => _onBoard[index].Passenger;

        public (int Tick, int QueueIndex) BoardingAt(int index)
            => (_onBoard[index].BoardedAt, _onBoard[index].QueueIndex);

        public IReadOnlyList<(PassengerCall Passenger, int Tick, int Seat, int CabinCount)> AlightingsAt(int floor)
            => _alighted[floor - 1];

        public (int Tick, int Boarded, int Delivered) TransferAt(int floor) => _transfers[floor - 1];

        public (int WeightKg, int SpaceUnits, int Reward) WaitingLoadAt(int floor, int index)
        {
            PassengerCall passenger = _waiting[floor - 1][index];
            return (passenger.WeightKg, passenger.SpaceUnits, passenger.Reward);
        }

        public double ApproachProgressAt(int floor)
            => TryGetApproach(floor, out _, out double progress) ? progress : -1;

        public bool PartyApproachingAt(int floor)
        {
            for (int i = _nextCall; i < _schedule.Count && _schedule[i].Tick - Tick <= ShiftRules.PartyNoticeTicks; i++)
            {
                var call = _schedule[i];
                if (call.Origin == floor && call.Kind == PassengerKind.PartyGuest)
                    return true;
            }
            return false;
        }

        public bool TryGetApproach(int floor, out PassengerCall passenger, out double progress)
        {
            for (int index = _nextCall; index < _schedule.Count; index++)
            {
                PassengerCall call = _schedule[index];
                int ticksLeft = call.Tick - Tick;
                if (ticksLeft > ShiftRules.ApproachTicks)
                    break;

                if (call.Origin == floor)
                {
                    passenger = call;
                    progress = 1d - (double)ticksLeft / ShiftRules.ApproachTicks;
                    return true;
                }
            }

            passenger = default;
            progress = 0;
            return false;
        }

        public int PatienceRemainingAt(int floor, int index = 0)
        {
            List<PassengerCall> queue = _waiting[floor - 1];
            return index < 0 || index >= queue.Count
                ? 0
                : Math.Max(0, queue[index].PatienceDeadline - Tick);
        }

        public bool TrySelectFloor(int floor)
        {
            if (Finished || State != LiftState.Idle || floor < 1 || floor > ShiftRules.FloorCount)
                return false;

            TargetFloor = floor;
            if (CurrentFloor == TargetFloor)
            {
                Arrive();
            }
            else
            {
                State = LiftState.Moving;
                _movementStart = Tick;
                _nextLiftEvent = Tick + Math.Abs(TargetFloor - CurrentFloor) * ShiftRules.TravelTicksPerFloor;
            }

            return true;
        }

        public double LiftPosition(double fractionalTick)
        {
            if (State != LiftState.Moving)
                return CurrentFloor;

            double time = Finished ? Tick : Tick + fractionalTick;
            double travelFraction = Math.Min(1, (time - _movementStart) / (_nextLiftEvent - _movementStart));
            return CurrentFloor + (TargetFloor - CurrentFloor) * travelFraction;
        }

        public void Advance()
        {
            if (Finished)
                return;

            Tick++;
            if (Finished)
                return;

            int previousDelivered = Delivered;
            int previousDepartures = LeftByStairs;
            bool newCallHere = AddScheduledCalls();
            if (State == LiftState.Moving && Tick == _nextLiftEvent)
            {
                Arrive();
            }
            else if (State != LiftState.Moving)
            {
                if (State == LiftState.DoorsOpen && Tick == _nextLiftEvent)
                    State = LiftState.Idle;

                if (newCallHere)
                    TryBoardWhileStopped();
            }

            ExpireWaitingCalls();
            int satisfactionChange = (Delivered - previousDelivered) * ShiftRules.SatisfactionPerDelivery
                - (LeftByStairs - previousDepartures) * ShiftRules.SatisfactionLostPerDeparture;
            Satisfaction = Math.Clamp(Satisfaction + satisfactionChange, 0, ShiftRules.MaximumSatisfaction);
        }

        private bool AddScheduledCalls()
        {
            bool newCallHere = false;
            while (_nextCall < _schedule.Count && _schedule[_nextCall].Tick == Tick)
            {
                PassengerCall call = _schedule[_nextCall++];
                _waiting[call.Origin - 1].Add(call);
                newCallHere |= call.Origin == CurrentFloor;
            }

            return newCallHere;
        }

        private void Arrive()
        {
            CurrentFloor = TargetFloor;
            int alighted = UnloadCurrentFloor();
            int boarded = BoardWaitingPassengers();
            RegisterTransfer(boarded, alighted);
            HoldDoorsOpen();
        }

        private int UnloadCurrentFloor()
        {
            int floorIndex = CurrentFloor - 1;
            int alighted = _ridingTo[floorIndex];
            if (alighted == 0)
                return 0;

            var record = _alighted[floorIndex];
            record.Clear();
            int cabinCount = _onBoard.Count;
            for (int index = _onBoard.Count - 1; index >= 0; index--)
            {
                PassengerCall passenger = _onBoard[index].Passenger;
                if (passenger.Destination != CurrentFloor)
                    continue;

                record.Add((passenger, Tick, index, cabinCount));
                DeliveredPoints += passenger.Reward;
                WeightKg -= passenger.WeightKg;
                UsedSpace -= passenger.SpaceUnits;
                _onBoard.RemoveAt(index);
            }

            Delivered += alighted;
            _ridingTo[floorIndex] = 0;
            return alighted;
        }

        private int BoardWaitingPassengers()
        {
            List<PassengerCall> queue = _waiting[CurrentFloor - 1];
            IReadOnlyList<int> selected = BoardingSelection.Choose(
                queue, ShiftRules.MaximumWeightKg - WeightKg, ShiftRules.MaximumSpaceUnits - UsedSpace);

            for (int index = selected.Count - 1; index >= 0; index--)
            {
                int queueIndex = selected[index];
                PassengerCall passenger = queue[queueIndex];
                _onBoard.Add((passenger, Tick, queueIndex));
                _ridingTo[passenger.Destination - 1]++;
                WeightKg += passenger.WeightKg;
                UsedSpace += passenger.SpaceUnits;
                queue.RemoveAt(queueIndex);
            }

            return selected.Count;
        }

        private void TryBoardWhileStopped()
        {
            int boarded = BoardWaitingPassengers();
            if (boarded == 0)
                return;

            RegisterTransfer(boarded, 0);
            HoldDoorsOpen();
        }

        private void HoldDoorsOpen()
        {
            if (State != LiftState.DoorsOpen)
                DoorsOpenedAt = Tick;
            State = LiftState.DoorsOpen;
            _nextLiftEvent = Tick + ShiftRules.StopTicks;
        }

        private void RegisterTransfer(int boarded, int alighted)
        {
            if (boarded > 0 || alighted > 0)
                _transfers[CurrentFloor - 1] = (Tick, boarded, alighted);
        }

        private void ExpireWaitingCalls()
        {
            for (int index = 0; index < _waiting.Length; index++)
            {
                List<PassengerCall> queue = _waiting[index];
                int kept = 0;
                for (int position = 0; position < queue.Count; position++)
                {
                    PassengerCall passenger = queue[position];
                    if (Tick >= passenger.PatienceDeadline)
                    {
                        if (kept == position)
                            _departed[index] = passenger;
                        continue;
                    }

                    if (kept != position)
                        queue[kept] = passenger;
                    kept++;
                }

                if (kept == queue.Count)
                    continue;

                LeftByStairs += queue.Count - kept;
                _lastDeparture[index] = Tick;
                queue.RemoveRange(kept, queue.Count - kept);
            }
        }
    }
}
