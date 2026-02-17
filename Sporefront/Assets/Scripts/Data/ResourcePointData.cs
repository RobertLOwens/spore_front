using System;
using System.Collections.Generic;
using Sporefront.Models;

namespace Sporefront.Data
{
    [System.Serializable]
    public class ResourcePointData
    {
        public Guid id;
        public ResourcePointType resourceType;
        public HexCoordinate coordinate;
        public int remainingAmount;
        public double currentHealth;

        // Gathering state
        public HashSet<Guid> assignedVillagerGroupIDs = new HashSet<Guid>();
        public int totalVillagersGathering;

        public const int MaxVillagersPerTile = 20;

        public ResourcePointData(HexCoordinate coordinate, ResourcePointType resourceType)
        {
            this.id = Guid.NewGuid();
            this.coordinate = coordinate;
            this.resourceType = resourceType;
            this.remainingAmount = resourceType.InitialAmount();
            this.currentHealth = resourceType.MaxHealth();
            this.totalVillagersGathering = 0;
        }

        // Amount Management

        public void SetRemainingAmount(int amount)
        {
            remainingAmount = Math.Max(0, amount);
        }

        public int Gather(int amount)
        {
            int gathered = Math.Min(amount, remainingAmount);
            remainingAmount = Math.Max(0, remainingAmount - gathered);
            return gathered;
        }

        public bool IsDepleted() => remainingAmount <= 0;

        // Health Management (for huntable animals)

        public bool TakeDamage(double damage)
        {
            if (!resourceType.IsHuntable()) return false;
            currentHealth = Math.Max(0, currentHealth - damage);
            return currentHealth <= 0;
        }

        public bool IsAlive() => currentHealth > 0;

        // Villager Assignment

        public bool AssignVillagerGroup(Guid groupID, int villagerCount)
        {
            if (!CanAddVillagers(villagerCount)) return false;
            if (assignedVillagerGroupIDs.Contains(groupID)) return false;

            assignedVillagerGroupIDs.Add(groupID);
            totalVillagersGathering += villagerCount;
            return true;
        }

        public void UnassignVillagerGroup(Guid groupID, int villagerCount)
        {
            if (!assignedVillagerGroupIDs.Contains(groupID)) return;

            assignedVillagerGroupIDs.Remove(groupID);
            totalVillagersGathering = Math.Max(0, totalVillagersGathering - villagerCount);
        }

        public bool CanAddVillagers(int count)
        {
            return totalVillagersGathering + count <= MaxVillagersPerTile;
        }

        public double GetCurrentGatherRate(double researchMultiplier = 1.0)
        {
            double perVillagerRate = 0.2;
            double baseRate = totalVillagersGathering * perVillagerRate;
            return baseRate * researchMultiplier;
        }

        public ResourcePointData CreateCarcassData()
        {
            var carcassType = resourceType.CarcassType();
            if (!carcassType.HasValue) return null;
            return new ResourcePointData(coordinate, carcassType.Value);
        }

        public string GetDescription()
        {
            string desc = $"{resourceType.DisplayName()}\n";
            desc += $"Remaining: {remainingAmount}/{resourceType.InitialAmount()}\n";
            desc += $"Yields: {resourceType.ResourceYield().DisplayName()}";

            if (resourceType.IsHuntable())
            {
                desc += $"\n\nHealth: {(int)currentHealth}/{(int)resourceType.MaxHealth()}";
                desc += $"\nAttack: {(int)resourceType.AttackPower()}";
                desc += $"\nDefense: {(int)resourceType.DefensePower()}";
            }
            else if (resourceType.IsGatherable())
            {
                desc += $"\n\nVillagers: {totalVillagersGathering}/{MaxVillagersPerTile}";
            }

            return desc;
        }
    }
}
