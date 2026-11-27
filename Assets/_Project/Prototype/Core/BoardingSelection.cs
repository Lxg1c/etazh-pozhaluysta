using System.Collections.Generic;

namespace Etazh.Prototype
{
    internal static class BoardingSelection
    {
        internal static IReadOnlyList<int> Choose(IReadOnlyList<PassengerCall> queue, int freeWeight, int freeSpace)
        {
            var selected = new List<int>();
            if (freeWeight == 0 || freeSpace == 0 || queue.Count == 0)
                return selected;

            int totalWeight = 0;
            int totalSpace = 0;
            foreach (PassengerCall passenger in queue)
            {
                totalWeight += passenger.WeightKg;
                totalSpace += passenger.SpaceUnits;
            }

            if (totalWeight <= freeWeight && totalSpace <= freeSpace)
            {
                for (int index = 0; index < queue.Count; index++)
                    selected.Add(index);

                return selected;
            }

            // Maximum boarding count for each suffix of the queue and remaining capacity.
            var best = new int[queue.Count + 1, freeWeight + 1, freeSpace + 1];
            for (int index = queue.Count - 1; index >= 0; index--)
            {
                PassengerCall passenger = queue[index];
                for (int weight = 1; weight <= freeWeight; weight++)
                {
                    for (int space = 1; space <= freeSpace; space++)
                    {
                        int count = best[index + 1, weight, space];
                        if (passenger.WeightKg <= weight && passenger.SpaceUnits <= space)
                        {
                            int withPassenger = 1 + best[index + 1, weight - passenger.WeightKg, space - passenger.SpaceUnits];
                            if (withPassenger > count)
                                count = withPassenger;
                        }

                        best[index, weight, space] = count;
                    }
                }
            }

            for (int index = 0; index < queue.Count && best[index, freeWeight, freeSpace] > 0; index++)
            {
                PassengerCall passenger = queue[index];
                if (passenger.WeightKg > freeWeight || passenger.SpaceUnits > freeSpace)
                    continue;

                int withPassenger = 1 + best[index + 1, freeWeight - passenger.WeightKg, freeSpace - passenger.SpaceUnits];
                if (withPassenger != best[index, freeWeight, freeSpace])
                    continue;

                // Keep the earliest waiter when including them preserves the maximum count.
                selected.Add(index);
                freeWeight -= passenger.WeightKg;
                freeSpace -= passenger.SpaceUnits;
            }

            return selected;
        }
    }
}
