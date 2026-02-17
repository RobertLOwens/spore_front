using System;
using Sporefront.Models;

namespace Sporefront.Data
{
    [System.Serializable]
    public class TrainingQueueEntry
    {
        public Guid id;
        public MilitaryUnitType unitType;
        public int quantity;
        public double startTime;
        public double progress;

        public TrainingQueueEntry(MilitaryUnitType unitType, int quantity, double startTime)
        {
            this.id = Guid.NewGuid();
            this.unitType = unitType;
            this.quantity = quantity;
            this.startTime = startTime;
            this.progress = 0.0;
        }

        public double GetProgress(double currentTime, double trainingSpeedMultiplier = 1.0)
        {
            double elapsed = currentTime - startTime;
            double baseTime = unitType.TrainingTime() * quantity;
            double totalTime = baseTime / trainingSpeedMultiplier;
            return Math.Min(1.0, elapsed / totalTime);
        }
    }

    [System.Serializable]
    public class VillagerTrainingEntry
    {
        public Guid id;
        public int quantity;
        public double startTime;
        public double progress;

        public const double TrainingTimePerVillager = 10.0;

        public VillagerTrainingEntry(int quantity, double startTime)
        {
            this.id = Guid.NewGuid();
            this.quantity = quantity;
            this.startTime = startTime;
            this.progress = 0.0;
        }

        public double GetProgress(double currentTime)
        {
            double elapsed = currentTime - startTime;
            double totalTime = TrainingTimePerVillager * quantity;
            return Math.Min(1.0, elapsed / totalTime);
        }
    }
}
