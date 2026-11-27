using System.Linq;
using NUnit.Framework;

namespace Etazh.Prototype.Tests
{
    public sealed class ShiftModelTests
    {
        [Test]
        public void ApproachingPassengerAutoBoardsAtTheParkedLiftAndDestinationAppears()
        {
            var shift = new ShiftModel(new[] { new PassengerCall(20, 1, 5) });

            Assert.That(shift.ApproachProgressAt(1), Is.Zero);
            Assert.That(shift.WaitingAt(1), Is.Zero);
            Assert.That(shift.RidingTo(5), Is.Zero);
            Advance(shift, 10);
            Assert.That(shift.ApproachProgressAt(1), Is.EqualTo(.5));

            Advance(shift, 10);
            Assert.That(shift.ApproachProgressAt(1), Is.EqualTo(-1));
            Assert.That(shift.WaitingAt(1), Is.Zero);
            Assert.That(shift.RidingTo(5), Is.EqualTo(1));
            Assert.That(shift.State, Is.EqualTo(LiftState.DoorsOpen));
            Assert.That(shift.TransferAt(1).Boarded, Is.EqualTo(1));

            Advance(shift, ShiftRules.StopTicks);
            Assert.That(shift.TrySelectFloor(5), Is.True);
            Assert.That(shift.TrySelectFloor(2), Is.False);
            Advance(shift, 4 * ShiftRules.TravelTicksPerFloor);
            Assert.That(shift.Delivered, Is.EqualTo(1));
            Assert.That(shift.Score, Is.EqualTo(10));
            Assert.That(shift.TransferAt(5).Delivered, Is.EqualTo(1));
            Assert.That(shift.RidingTo(5), Is.Zero);

            Advance(shift, ShiftRules.StopTicks);
            Assert.That(shift.TrySelectFloor(5), Is.True);
            Assert.That(shift.Score, Is.EqualTo(10));
        }

        [Test]
        public void PatienceStartsOnlyWhenPassengerReachesTheWaitingArea()
        {
            const int callTick = 20;
            var shift = new ShiftModel(new[] { new PassengerCall(callTick, 2, 4, 100, 2) });
            Advance(shift, callTick - 1);
            Assert.That(shift.WaitingAt(2), Is.Zero);
            Assert.That(shift.PatienceRemainingAt(2), Is.Zero);

            shift.Advance();
            Assert.That(shift.WaitingLoadAt(2, 0), Is.EqualTo((100, 2, 20)));
            Assert.That(shift.PatienceRemainingAt(2), Is.EqualTo(ShiftRules.PatienceTicks));
            Advance(shift, ShiftRules.PatienceTicks - 1);
            Assert.That(shift.PatienceRemainingAt(2), Is.EqualTo(1));

            shift.Advance();
            Advance(shift, 15);
            Assert.That(shift.WaitingAt(2), Is.Zero);
            Assert.That(shift.LeftByStairs, Is.EqualTo(1));
            Assert.That(shift.Score, Is.EqualTo(-2));
        }

        [Test]
        public void ChoosesThreeLightPassengersInsteadOfOneHeavyAtTheSameWeight()
        {
            PassengerCall[] calls =
            {
                new(0, 1, 5, 250, 1),
                new(0, 2, 4, 150, 2),
                new(0, 2, 3),
                new(0, 2, 3),
                new(0, 2, 5)
            };
            var shift = new ShiftModel(calls);
            Advance(shift, ShiftRules.StopTicks);
            Assert.That(shift.TrySelectFloor(2), Is.True);
            Advance(shift, ShiftRules.TravelTicksPerFloor);

            Assert.That(shift.OnBoardCount, Is.EqualTo(4));
            Assert.That(shift.TransferAt(2).Boarded, Is.EqualTo(3));
            Assert.That(shift.WeightKg, Is.EqualTo(400));
            Assert.That(shift.UsedSpace, Is.EqualTo(4));
            Assert.That(shift.WaitingAt(2), Is.EqualTo(1));
            Assert.That(shift.WaitingLoadAt(2, 0), Is.EqualTo((150, 2, 35)));
            Assert.That(shift.RidingTo(4), Is.Zero);
        }

        [Test]
        public void SpaceCanBeFullWhileWeightRemainsAndTieFavorsLongestWaiting()
        {
            PassengerCall[] calls =
            {
                new(0, 2, 3, 100, 2),
                new(1, 2, 4, 100, 2),
                new(2, 2, 5, 50, 1)
            };
            var shift = new ShiftModel(calls);
            Assert.That(shift.TrySelectFloor(2), Is.True);
            Advance(shift, ShiftRules.TravelTicksPerFloor);

            Assert.That(shift.WeightKg, Is.EqualTo(200));
            Assert.That(shift.UsedSpace, Is.EqualTo(4));
            Assert.That(shift.WaitingAt(2), Is.EqualTo(1));
            Assert.That(shift.RidingTo(3), Is.EqualTo(1));
            Assert.That(shift.RidingTo(4), Is.EqualTo(1));
            Assert.That(shift.RidingTo(5), Is.Zero);
        }

        [Test]
        public void WeightCanBeFullWhileSpaceRemains()
        {
            PassengerCall[] calls =
            {
                new(0, 1, 2, 150, 1),
                new(0, 1, 3, 150, 1),
                new(0, 1, 4, 100, 1),
                new(0, 1, 5, 50, 1)
            };
            var shift = new ShiftModel(calls);

            Assert.That(shift.WeightKg, Is.EqualTo(400));
            Assert.That(shift.UsedSpace, Is.EqualTo(3));
            Assert.That(shift.WaitingAt(1), Is.EqualTo(1));
            Assert.That(shift.RidingTo(5), Is.Zero);
        }

        [Test]
        public void UnloadingFreesBothLimitsBeforeTheNextGroupBoards()
        {
            PassengerCall[] calls =
            {
                new(0, 1, 2, 150, 2),
                new(0, 1, 3, 150, 2),
                new(0, 2, 4, 100, 2)
            };
            var shift = new ShiftModel(calls);
            Advance(shift, ShiftRules.StopTicks);
            Assert.That(shift.TrySelectFloor(2), Is.True);
            Advance(shift, ShiftRules.TravelTicksPerFloor);

            Assert.That(shift.Score, Is.EqualTo(35));
            Assert.That(shift.WeightKg, Is.EqualTo(250));
            Assert.That(shift.UsedSpace, Is.EqualTo(4));
            Assert.That(shift.RidingTo(4), Is.EqualTo(1));
            Assert.That(shift.TransferAt(2), Is.EqualTo((shift.Tick, 1, 1)));

            int arrival = shift.Tick;
            Assert.That(shift.AlightingsAt(2), Has.Count.EqualTo(1));
            Assert.That(shift.AlightingsAt(2)[0], Is.EqualTo((calls[0], arrival, 1, 2)));
            Assert.That(shift.RidingPassengerAt(0), Is.EqualTo(calls[1]));
            Assert.That(shift.RidingPassengerAt(1), Is.EqualTo(calls[2]));
            Assert.That(shift.BoardingAt(1), Is.EqualTo((arrival, 0)));

            Advance(shift, ShiftRules.StopTicks);
            Assert.That(shift.TrySelectFloor(2), Is.True);
            Assert.That(shift.AlightingsAt(2)[0].Tick, Is.EqualTo(arrival));
        }

        [Test]
        public void PassengerCanAutoBoardWhileDoorsAreAlreadyOpen()
        {
            PassengerCall[] calls = { new(0, 1, 2), new(3, 1, 5) };
            var shift = new ShiftModel(calls);
            Advance(shift, 3);
            Assert.That(shift.OnBoardCount, Is.EqualTo(2));
            Assert.That(shift.RidingTo(5), Is.EqualTo(1));
            Advance(shift, ShiftRules.StopTicks - 1);
            Assert.That(shift.State, Is.EqualTo(LiftState.DoorsOpen));
            shift.Advance();
            Assert.That(shift.State, Is.EqualTo(LiftState.Idle));
        }

        [Test]
        public void RewardsAreEarnedOnDeliveryAndDependOnWeight()
        {
            PassengerCall[] calls =
            {
                new(0, 1, 2, 50, 1),
                new(0, 1, 3, 100, 1),
                new(0, 1, 4, 150, 2)
            };
            var shift = new ShiftModel(calls);
            Assert.That(shift.Score, Is.Zero);

            for (int floor = 2; floor <= 4; floor++)
            {
                Advance(shift, ShiftRules.StopTicks);
                Assert.That(shift.TrySelectFloor(floor), Is.True);
                Advance(shift, ShiftRules.TravelTicksPerFloor);
                int[] expectedScore = { 10, 30, 65 };
                Assert.That(shift.Score, Is.EqualTo(expectedScore[floor - 2]));
            }
        }

        [Test]
        public void PickupWinsTieWithPatienceDeadline()
        {
            var shift = new ShiftModel(new[] { new PassengerCall(0, 2, 5) });
            Advance(shift, ShiftRules.PatienceTicks - ShiftRules.TravelTicksPerFloor);
            Assert.That(shift.TrySelectFloor(2), Is.True);
            Advance(shift, ShiftRules.TravelTicksPerFloor);

            Assert.That(shift.RidingTo(5), Is.EqualTo(1));
            Assert.That(shift.LeftByStairs, Is.Zero);
        }

        [Test]
        public void ShiftDeadlineWinsWhenDeliveryWouldFinishOnLastTick()
        {
            var shift = new ShiftModel(new[] { new PassengerCall(0, 1, 2) });
            Advance(shift, ShiftRules.ShiftTicks - ShiftRules.TravelTicksPerFloor);
            Assert.That(shift.TrySelectFloor(2), Is.True);
            Advance(shift, ShiftRules.TravelTicksPerFloor - 1);
            Assert.That(shift.Finished, Is.False);

            shift.Advance();
            Advance(shift, 30);

            Assert.That(shift.Finished, Is.True);
            Assert.That(shift.Tick, Is.EqualTo(ShiftRules.ShiftTicks));
            Assert.That(shift.Delivered, Is.Zero);
            Assert.That(shift.OnBoardCount, Is.EqualTo(1));
            Assert.That(shift.Score, Is.Zero);
            Assert.That(shift.TrySelectFloor(2), Is.False);
        }

        [Test]
        public void ScheduleRepeatsWithLargerWavesAcrossDistinctFloors()
        {
            var schedule = ShiftRules.CreateSchedule();
            CollectionAssert.AreEqual(schedule, ShiftRules.CreateSchedule());
            int[] intervals = { 0, 7, 5, 4 };
            foreach (TrafficPhase phase in System.Enum.GetValues(typeof(TrafficPhase)))
            {
                var waves = schedule.Where(call => call.Kind == PassengerKind.Resident && ShiftRules.PhaseAt(call.Tick) == phase)
                    .GroupBy(call => call.Tick).ToArray();
                Assert.That(waves.Length, Is.GreaterThan(1));
                for (int index = 0; index < waves.Length; index++)
                {
                    var wave = waves[index];
                    Assert.That(wave.Count(), Is.EqualTo((int)phase + 1));
                    Assert.That(wave.Select(call => call.Origin).Distinct().Count(), Is.EqualTo(wave.Count()));
                    Assert.That(wave.All(call => call.Origin != call.Destination), Is.True);
                    if (index > 0 && phase != TrafficPhase.Quiet)
                        Assert.That(wave.Key - waves[index - 1].Key, Is.EqualTo(intervals[(int)phase] * ShiftRules.TicksPerSecond));
                }
            }

            Assert.That(ShiftRules.PhaseAt(549), Is.EqualTo(TrafficPhase.Quiet));
            Assert.That(ShiftRules.PhaseAt(550), Is.EqualTo(TrafficPhase.Building));
            Assert.That(ShiftRules.PhaseAt(1349), Is.EqualTo(TrafficPhase.Building));
            Assert.That(ShiftRules.PhaseAt(1350), Is.EqualTo(TrafficPhase.Rush));
            Assert.That(ShiftRules.PhaseAt(2199), Is.EqualTo(TrafficPhase.Rush));
            Assert.That(ShiftRules.PhaseAt(2200), Is.EqualTo(TrafficPhase.FinalRush));
        }

        [Test]
        public void EventScheduleHasVisibleApproachesAndThreeGuestsFromOneFloor()
        {
            var schedule = ShiftRules.CreateSchedule();
            var events = schedule.Where(call => call.Kind != PassengerKind.Resident).ToArray();
            Assert.That(events.Select(call => call.Kind), Is.EqualTo(new[]
            {
                PassengerKind.Mover, PassengerKind.PartyGuest, PassengerKind.PartyGuest,
                PassengerKind.PartyGuest, PassengerKind.Courier
            }));
            Assert.That(events.Where(call => call.Kind == PassengerKind.PartyGuest)
                .All(call => call.Origin == 4 && call.Destination == 1 && call.SpaceUnits == 1), Is.True);
            foreach (var call in events)
            {
                Assert.That(schedule.Count(other => other.Origin == call.Origin &&
                    System.Math.Abs(other.Tick - call.Tick) < ShiftRules.ApproachTicks), Is.EqualTo(1));
            }
        }

        [Test]
        public void CourierCanExpireWhileAnEarlierResidentStillWaits()
        {
            var shift = new ShiftModel(new[]
            {
                new PassengerCall(0, 2, 1),
                new PassengerCall(50, 2, 3, kind: PassengerKind.Courier)
            });
            Advance(shift, 50 + ShiftRules.CourierPatienceTicks - 1);
            Assert.That(shift.PatienceRemainingAt(2, 1), Is.EqualTo(1));
            Assert.That(shift.PatienceRemainingAt(2, 0), Is.GreaterThan(1));
            Advance(shift, 1);
            Assert.That(shift.WaitingAt(2), Is.EqualTo(1));
            Assert.That(shift.DepartedPassengerAt(2).Kind, Is.EqualTo(PassengerKind.Courier));
            Assert.That(shift.WaitingPassengerAt(2, 0).Kind, Is.EqualTo(PassengerKind.Resident));
            Assert.That(shift.Score, Is.EqualTo(-ShiftRules.StairPenalty));
        }

        [Test]
        public void MoveConsumesThreePlacesAndAwardsFortyFiveOnDelivery()
        {
            var shift = new ShiftModel(new[] { new PassengerCall(0, 1, 4, 100, 3, PassengerKind.Mover) });
            Assert.That(shift.WeightKg, Is.EqualTo(100));
            Assert.That(shift.UsedSpace, Is.EqualTo(3));
            Assert.That(shift.Score, Is.Zero);
            Advance(shift, ShiftRules.StopTicks);
            Assert.That(shift.TrySelectFloor(4), Is.True);
            Advance(shift, 3 * ShiftRules.TravelTicksPerFloor);
            Assert.That(shift.UsedSpace, Is.Zero);
            Assert.That(shift.Score, Is.EqualTo(45));
        }

        [Test]
        public void CompetingCallsProduceDifferentScoresForDifferentFirstStops()
        {
            PassengerCall[] calls =
            {
                new(5, 1, 2),
                new(5, 5, 4),
                new(5, 5, 4)
            };
            int Route(int firstPickup, int delivery)
            {
                var shift = new ShiftModel(calls);
                Assert.That(shift.TrySelectFloor(3), Is.True);
                Advance(shift, 5 + ShiftRules.PatienceTicks - 2 * ShiftRules.TravelTicksPerFloor);
                Assert.That(shift.TrySelectFloor(firstPickup), Is.True);
                Advance(shift, 2 * ShiftRules.TravelTicksPerFloor + ShiftRules.StopTicks);
                Assert.That(shift.TrySelectFloor(delivery), Is.True);
                Advance(shift, ShiftRules.TravelTicksPerFloor);
                return shift.Score;
            }

            Assert.That(Route(1, 2), Is.EqualTo(6));
            Assert.That(Route(5, 4), Is.EqualTo(18));
        }

        [Test]
        public void SatisfactionRewardsEachDeliveryAndPenalizesEachDeparture()
        {
            PassengerCall[] calls =
            {
                new(0, 1, 2, 150, 2),
                new(0, 3, 4),
                new(0, 3, 5)
            };
            var shift = new ShiftModel(calls);
            Assert.That(shift.Satisfaction, Is.EqualTo(50));
            Advance(shift, ShiftRules.StopTicks);
            Assert.That(shift.TrySelectFloor(2), Is.True);
            Advance(shift, ShiftRules.TravelTicksPerFloor);
            Assert.That(shift.Score, Is.EqualTo(35));
            Assert.That(shift.Satisfaction, Is.EqualTo(53));

            Advance(shift, ShiftRules.PatienceTicks - shift.Tick);
            Assert.That(shift.LeftByStairs, Is.EqualTo(2));
            Assert.That(shift.Satisfaction, Is.EqualTo(41));
            Assert.That(new ShiftModel(System.Array.Empty<PassengerCall>()).Satisfaction, Is.EqualTo(50));
        }

        [Test]
        public void SatisfactionCombinesSimultaneousResultsBeforeClamping()
        {
            const int lastCall = 20;
            var calls = new System.Collections.Generic.List<PassengerCall>
            {
                new(0, 1, 2),
                new(lastCall, 4, 3)
            };
            for (int index = 0; index < 10; index++)
                calls.Add(new PassengerCall(0, 5, 4));

            var shift = new ShiftModel(calls);
            Advance(shift, ShiftRules.PatienceTicks);
            Assert.That(shift.Satisfaction, Is.Zero);
            int deadline = lastCall + ShiftRules.PatienceTicks;
            Advance(shift, deadline - shift.Tick - ShiftRules.TravelTicksPerFloor);
            Assert.That(shift.TrySelectFloor(2), Is.True);
            Advance(shift, ShiftRules.TravelTicksPerFloor);
            Assert.That(shift.Delivered, Is.EqualTo(1));
            Assert.That(shift.LeftByStairs, Is.EqualTo(11));
            Assert.That(shift.Satisfaction, Is.Zero);
        }

        [Test]
        public void ConsistentDeliveriesCannotOverfillSatisfaction()
        {
            const int tripCount = 18;
            int tripTicks = ShiftRules.StopTicks + ShiftRules.TravelTicksPerFloor;
            var calls = new PassengerCall[tripCount];
            for (int index = 0; index < calls.Length; index++)
            {
                int origin = index % 2 + 1;
                calls[index] = new PassengerCall(index * tripTicks, origin, 3 - origin);
            }

            var shift = new ShiftModel(calls);
            for (int trip = 0; trip < tripCount; trip++)
            {
                Advance(shift, ShiftRules.StopTicks);
                Assert.That(shift.TrySelectFloor(3 - shift.CurrentFloor), Is.True);
                Advance(shift, ShiftRules.TravelTicksPerFloor);
            }

            Assert.That(shift.Delivered, Is.EqualTo(tripCount));
            Assert.That(shift.Satisfaction, Is.EqualTo(100));
        }

        private static void Advance(ShiftModel shift, int ticks)
        {
            for (int tick = 0; tick < ticks; tick++)
                shift.Advance();
        }
    }
}
