using System;
using System.Collections.Generic;
using Sporefront.Models;

namespace Sporefront.Data
{
    [System.Serializable]
    public class VillagerGroupData
    {
        public Guid id;
        public string name;
        public Guid? ownerID;
        public HexCoordinate coordinate;
        public int villagerCount;

        public VillagerTask currentTask;
        public HexCoordinate? taskTargetCoordinate;
        public Guid? taskTargetID;

        // Movement
        public List<HexCoordinate> currentPath;
        public int pathIndex;
        public double movementProgress;

        // Gathering state
        public double gatheringAccumulator;
        public Guid? assignedResourcePointID;

        // Combat stats
        public const double HpPerVillager = 25.0;
        public const double MeleeAttackPerVillager = 1.0;
        public const double MeleeArmorPerVillager = 1.0;

        public VillagerGroupData(string name, HexCoordinate coordinate, int villagerCount = 0, Guid? ownerID = null)
        {
            this.id = Guid.NewGuid();
            this.name = name;
            this.coordinate = coordinate;
            this.villagerCount = Math.Max(0, villagerCount);
            this.ownerID = ownerID;
            this.currentTask = new IdleTask();
            this.pathIndex = 0;
            this.movementProgress = 0.0;
            this.gatheringAccumulator = 0.0;
        }

        // Villager Management

        public void AddVillagers(int count)
        {
            villagerCount += count;
        }

        public int RemoveVillagers(int count)
        {
            int toRemove = Math.Min(villagerCount, count);
            villagerCount -= toRemove;
            return toRemove;
        }

        public void SetVillagerCount(int count)
        {
            villagerCount = Math.Max(0, count);
        }

        public bool HasVillagers() => villagerCount > 0;
        public bool IsEmpty() => villagerCount == 0;

        // Task Management

        public void AssignTask(VillagerTask task, HexCoordinate? targetCoordinate = null, Guid? targetID = null)
        {
            currentTask = task;
            taskTargetCoordinate = targetCoordinate;
            taskTargetID = targetID;
        }

        public void ClearTask()
        {
            currentTask = new IdleTask();
            taskTargetCoordinate = null;
            taskTargetID = null;
            gatheringAccumulator = 0.0;
        }

        public bool IsGathering()
        {
            return currentTask is GatheringTask || currentTask is GatheringResourceTask;
        }

        public bool IsHunting()
        {
            return currentTask is HuntingTask;
        }

        public bool IsBuilding()
        {
            return currentTask is BuildingTask;
        }

        // Movement

        public void SetPath(List<HexCoordinate> path)
        {
            currentPath = path;
            pathIndex = 0;
            movementProgress = 0.0;
        }

        public void ClearPath()
        {
            currentPath = null;
            pathIndex = 0;
            movementProgress = 0.0;
        }

        public bool HasPath()
        {
            return currentPath != null && pathIndex < currentPath.Count;
        }

        // Merging

        public void Merge(VillagerGroupData otherGroup)
        {
            AddVillagers(otherGroup.villagerCount);
        }

        // Splitting

        public VillagerGroupData Split(int count, string newName = null)
        {
            if (count <= 0 || count >= villagerCount) return null;

            var newGroup = new VillagerGroupData(
                newName ?? $"{name} (Split)",
                coordinate,
                count,
                ownerID
            );

            RemoveVillagers(count);
            return newGroup;
        }

        // Combat stats

        public double TotalHP => villagerCount * HpPerVillager;
        public double TotalMeleeAttack => villagerCount * MeleeAttackPerVillager;
        public double TotalMeleeArmor => villagerCount * MeleeArmorPerVillager;

        // Description

        public string GetDescription()
        {
            if (!HasVillagers()) return $"{name} (Empty)";
            string taskDesc = currentTask.IsIdle ? "Idle" : $"Working: {currentTask.DisplayName}";
            return $"{name} ({villagerCount} villagers)\n{taskDesc}";
        }
    }
}
