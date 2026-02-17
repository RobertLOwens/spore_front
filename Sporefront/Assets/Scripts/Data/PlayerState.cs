using System;
using System.Collections.Generic;
using Sporefront.Models;

namespace Sporefront.Data
{
    public enum VisibilityLevel
    {
        Unexplored,
        Explored,
        Visible
    }

    [System.Serializable]
    public class PlayerState
    {
        public Guid id;
        public string name;
        public string colorHex;
        public bool isAI;

        // Resources
        public Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>
        {
            { ResourceType.Wood, 1000 },
            { ResourceType.Food, 1000 },
            { ResourceType.Stone, 1000 },
            { ResourceType.Ore, 1000 }
        };

        public Dictionary<ResourceType, double> collectionRates = new Dictionary<ResourceType, double>
        {
            { ResourceType.Wood, 0 },
            { ResourceType.Food, 0 },
            { ResourceType.Stone, 0 },
            { ResourceType.Ore, 0 }
        };

        private Dictionary<ResourceType, double> resourceAccumulators = new Dictionary<ResourceType, double>
        {
            { ResourceType.Wood, 0.0 },
            { ResourceType.Food, 0.0 },
            { ResourceType.Stone, 0.0 },
            { ResourceType.Ore, 0.0 }
        };

        // Owned Entities
        public HashSet<Guid> ownedBuildingIDs = new HashSet<Guid>();
        public HashSet<Guid> ownedArmyIDs = new HashSet<Guid>();
        public HashSet<Guid> ownedVillagerGroupIDs = new HashSet<Guid>();
        public HashSet<Guid> ownedCommanderIDs = new HashSet<Guid>();

        // Diplomacy
        public Dictionary<Guid, DiplomacyStatus> diplomacyRelations = new Dictionary<Guid, DiplomacyStatus>();

        // Vision
        public HashSet<HexCoordinate> visibleCoordinates = new HashSet<HexCoordinate>();
        public HashSet<HexCoordinate> exploredCoordinates = new HashSet<HexCoordinate>();

        // Time Tracking
        private double? lastUpdateTime;
        private double foodConsumptionAccumulator;

        // Research Tracking
        public HashSet<string> completedResearch = new HashSet<string>();
        public string activeResearchType;
        public double? activeResearchStartTime;
        private Dictionary<string, double> cachedResearchBonuses = new Dictionary<string, double>();

        // Unit Upgrade Tracking
        public HashSet<string> completedUnitUpgrades = new HashSet<string>();
        public string activeUnitUpgrade;
        public double? activeUnitUpgradeStartTime;
        public Guid? activeUnitUpgradeBuildingID;
        private Dictionary<string, UnitUpgradeBonusData> cachedUnitUpgradeBonuses = new Dictionary<string, UnitUpgradeBonusData>();

        // Constants
        public const double FoodConsumptionPerPop = 0.1;

        public PlayerState(string name, string colorHex, bool isAI = false)
        {
            this.id = Guid.NewGuid();
            this.name = name;
            this.colorHex = colorHex;
            this.isAI = isAI;
        }

        // Resource Management

        public int GetResource(ResourceType type)
        {
            return resources.ContainsKey(type) ? resources[type] : 0;
        }

        public double GetCollectionRate(ResourceType type)
        {
            return collectionRates.ContainsKey(type) ? collectionRates[type] : 0.0;
        }

        public void SetResource(ResourceType type, int amount)
        {
            resources[type] = Math.Max(0, amount);
        }

        public void SetCollectionRate(ResourceType type, double rate)
        {
            collectionRates[type] = rate;
        }

        public int AddResource(ResourceType type, int amount, int storageCapacity)
        {
            int current = GetResource(type);
            int availableSpace = Math.Max(0, storageCapacity - current);
            int actualAmount = Math.Min(amount, availableSpace);
            if (actualAmount > 0)
                resources[type] = current + actualAmount;
            return actualAmount;
        }

        public bool RemoveResource(ResourceType type, int amount)
        {
            int current = GetResource(type);
            if (current >= amount)
            {
                resources[type] = current - amount;
                return true;
            }
            return false;
        }

        public bool HasResource(ResourceType type, int amount) => GetResource(type) >= amount;

        public bool CanAfford(Dictionary<ResourceType, int> costs)
        {
            foreach (var kvp in costs)
            {
                if (!HasResource(kvp.Key, kvp.Value)) return false;
            }
            return true;
        }

        // Food Consumption

        public int ConsumeFood(double consumptionRate, double deltaTime)
        {
            double consumed = consumptionRate * deltaTime;
            foodConsumptionAccumulator += consumed;
            int wholeConsumption = (int)foodConsumptionAccumulator;
            if (wholeConsumption > 0)
            {
                int current = GetResource(ResourceType.Food);
                int actualConsumed = Math.Min(wholeConsumption, current);
                resources[ResourceType.Food] = current - actualConsumed;
                foodConsumptionAccumulator -= wholeConsumption;
                return actualConsumed;
            }
            return 0;
        }

        // Resource Update (per-tick)

        public Dictionary<ResourceType, int> UpdateResources(double currentTime, Func<ResourceType, int> getStorageCapacity)
        {
            if (!lastUpdateTime.HasValue)
            {
                lastUpdateTime = currentTime;
                return new Dictionary<ResourceType, int>();
            }

            double deltaTime = currentTime - lastUpdateTime.Value;
            var resourceChanges = new Dictionary<ResourceType, int>();

            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                double rate = GetCollectionRate(type);
                double generated = rate * deltaTime;

                if (!resourceAccumulators.ContainsKey(type))
                    resourceAccumulators[type] = 0.0;
                resourceAccumulators[type] += generated;

                int wholeAmount = (int)resourceAccumulators[type];
                if (wholeAmount > 0)
                {
                    int capacity = getStorageCapacity(type);
                    int added = AddResource(type, wholeAmount, capacity);
                    resourceAccumulators[type] -= wholeAmount;
                    if (added > 0) resourceChanges[type] = added;
                }
            }

            lastUpdateTime = currentTime;
            return resourceChanges;
        }

        // Entity Ownership

        public void AddOwnedBuilding(Guid buildingID) { ownedBuildingIDs.Add(buildingID); }
        public void RemoveOwnedBuilding(Guid buildingID) { ownedBuildingIDs.Remove(buildingID); }
        public void AddOwnedArmy(Guid armyID) { ownedArmyIDs.Add(armyID); }
        public void RemoveOwnedArmy(Guid armyID) { ownedArmyIDs.Remove(armyID); }
        public void AddOwnedVillagerGroup(Guid groupID) { ownedVillagerGroupIDs.Add(groupID); }
        public void RemoveOwnedVillagerGroup(Guid groupID) { ownedVillagerGroupIDs.Remove(groupID); }
        public void AddOwnedCommander(Guid commanderID) { ownedCommanderIDs.Add(commanderID); }
        public void RemoveOwnedCommander(Guid commanderID) { ownedCommanderIDs.Remove(commanderID); }

        // Diplomacy

        public DiplomacyStatus GetDiplomacyStatus(Guid otherPlayerID)
        {
            if (otherPlayerID == id) return DiplomacyStatus.Me;
            return diplomacyRelations.ContainsKey(otherPlayerID) ? diplomacyRelations[otherPlayerID] : DiplomacyStatus.Neutral;
        }

        public void SetDiplomacyStatus(Guid otherPlayerID, DiplomacyStatus status)
        {
            if (otherPlayerID != id) diplomacyRelations[otherPlayerID] = status;
        }

        // Vision

        public void SetVisibleCoordinates(HashSet<HexCoordinate> coords)
        {
            visibleCoordinates = coords;
            exploredCoordinates.UnionWith(coords);
        }

        public bool IsVisible(HexCoordinate coordinate) => visibleCoordinates.Contains(coordinate);
        public bool IsExplored(HexCoordinate coordinate) => exploredCoordinates.Contains(coordinate);

        public VisibilityLevel GetVisibilityLevel(HexCoordinate coordinate)
        {
            if (visibleCoordinates.Contains(coordinate)) return VisibilityLevel.Visible;
            if (exploredCoordinates.Contains(coordinate)) return VisibilityLevel.Explored;
            return VisibilityLevel.Unexplored;
        }

        // Research Methods

        public void StartResearch(string typeRawValue, double time)
        {
            if (activeResearchType != null) return;
            activeResearchType = typeRawValue;
            activeResearchStartTime = time;
        }

        public void CompleteResearch(string typeRawValue)
        {
            completedResearch.Add(typeRawValue);
            if (activeResearchType == typeRawValue)
            {
                activeResearchType = null;
                activeResearchStartTime = null;
            }
            RecalculateResearchBonuses();
        }

        public void CancelActiveResearch()
        {
            activeResearchType = null;
            activeResearchStartTime = null;
        }

        public bool HasCompletedResearch(string typeRawValue) => completedResearch.Contains(typeRawValue);
        public bool IsResearchActive() => activeResearchType != null;

        public double GetResearchBonus(string bonusTypeRawValue)
        {
            return cachedResearchBonuses.ContainsKey(bonusTypeRawValue) ? cachedResearchBonuses[bonusTypeRawValue] : 0.0;
        }

        public double GetResearchBonusMultiplier(string bonusTypeRawValue)
        {
            return 1.0 + GetResearchBonus(bonusTypeRawValue);
        }

        public void RecalculateResearchBonuses()
        {
            cachedResearchBonuses.Clear();
            foreach (string researchRawValue in completedResearch)
            {
                ResearchType researchType;
                if (Enum.TryParse(researchRawValue, out researchType))
                {
                    foreach (var bonus in researchType.Bonuses())
                    {
                        string key = bonus.Type.ToString();
                        if (cachedResearchBonuses.ContainsKey(key))
                            cachedResearchBonuses[key] += bonus.Value;
                        else
                            cachedResearchBonuses[key] = bonus.Value;
                    }
                }
            }
        }

        // Unit Upgrade Methods

        public void StartUnitUpgrade(string typeRawValue, Guid buildingID, double time)
        {
            if (activeUnitUpgrade != null) return;
            activeUnitUpgrade = typeRawValue;
            activeUnitUpgradeStartTime = time;
            activeUnitUpgradeBuildingID = buildingID;
        }

        public void CompleteUnitUpgrade(string typeRawValue)
        {
            completedUnitUpgrades.Add(typeRawValue);
            if (activeUnitUpgrade == typeRawValue)
            {
                activeUnitUpgrade = null;
                activeUnitUpgradeStartTime = null;
                activeUnitUpgradeBuildingID = null;
            }
            RecalculateUnitUpgradeBonuses();
        }

        public void CancelActiveUnitUpgrade()
        {
            activeUnitUpgrade = null;
            activeUnitUpgradeStartTime = null;
            activeUnitUpgradeBuildingID = null;
        }

        public bool HasCompletedUnitUpgrade(string typeRawValue) => completedUnitUpgrades.Contains(typeRawValue);
        public bool IsUnitUpgradeActive() => activeUnitUpgrade != null;

        public UnitUpgradeBonusData GetUnitUpgradeBonus(MilitaryUnitType unitType)
        {
            string key = unitType.ToString();
            return cachedUnitUpgradeBonuses.ContainsKey(key)
                ? cachedUnitUpgradeBonuses[key]
                : new UnitUpgradeBonusData(0, 0, 0);
        }

        public int GetUnitUpgradeTier(MilitaryUnitType unitType)
        {
            return UnitUpgradeTypeExtensions.CurrentTier(unitType, completedUnitUpgrades);
        }

        public void RecalculateUnitUpgradeBonuses()
        {
            cachedUnitUpgradeBonuses.Clear();
            foreach (MilitaryUnitType unitType in Enum.GetValues(typeof(MilitaryUnitType)))
            {
                var bonus = UnitUpgradeTypeExtensions.CumulativeBonuses(unitType, completedUnitUpgrades);
                if (bonus.attackBonus > 0 || bonus.armorBonus > 0 || bonus.hpBonus > 0)
                    cachedUnitUpgradeBonuses[unitType.ToString()] = bonus;
            }
        }
    }
}
