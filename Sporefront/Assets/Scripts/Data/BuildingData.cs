using System;
using System.Collections.Generic;
using Sporefront.Models;
using Sporefront.Engine;

namespace Sporefront.Data
{
    [System.Serializable]
    public class BuildingData
    {
        // Identity
        public Guid id;
        public BuildingType buildingType;
        public HexCoordinate coordinate;
        public Guid? ownerID;
        public int rotation;

        // State
        public BuildingState state = BuildingState.Planning;
        public int level = 1;
        public double health;
        public double maxHealth;

        // Construction
        public double constructionProgress;
        public double? constructionStartTime;
        public int buildersAssigned;
        public double constructionHP;
        public double? lastConstructionUpdateTime;

        // Upgrade
        public double upgradeProgress;
        public double? upgradeStartTime;
        [System.NonSerialized] public bool pendingUpgrade;

        // Demolition
        public double demolitionProgress;
        public double? demolitionStartTime;
        public int demolishersAssigned;

        // Garrison
        public Dictionary<MilitaryUnitType, int> garrison = new Dictionary<MilitaryUnitType, int>();
        public int villagerGarrison;

        // Training Queues
        public List<TrainingQueueEntry> trainingQueue = new List<TrainingQueueEntry>();
        public List<VillagerTrainingEntry> villagerTrainingQueue = new List<VillagerTrainingEntry>();

        // Computed Properties
        public int MaxLevel => buildingType.MaxLevel();
        public bool IsOperational => state == BuildingState.Completed || state == BuildingState.Upgrading;
        public bool CanUpgrade => state == BuildingState.Completed && level < MaxLevel;

        public List<HexCoordinate> OccupiedCoordinates => buildingType.GetOccupiedCoordinates(coordinate, rotation);

        public bool Occupies(HexCoordinate coord)
        {
            return OccupiedCoordinates.Contains(coord);
        }

        public BuildingData(BuildingType buildingType, HexCoordinate coordinate, Guid? ownerID = null, int rotation = 0)
        {
            this.id = Guid.NewGuid();
            this.buildingType = buildingType;
            this.coordinate = coordinate;
            this.ownerID = ownerID;
            this.rotation = rotation;

            switch (buildingType)
            {
                case BuildingType.Wall:
                    this.maxHealth = 600.0;
                    break;
                case BuildingType.Gate:
                    this.maxHealth = 400.0;
                    break;
                default:
                    this.maxHealth = buildingType.Category() == BuildingCategory.Military ? 500.0 : 200.0;
                    break;
            }
            this.health = maxHealth;
        }

        // Construction Logic

        public void StartConstruction(double currentTime, int builders = 1)
        {
            state = BuildingState.Constructing;
            constructionStartTime = currentTime;
            buildersAssigned = Math.Max(1, builders);
            constructionProgress = 0.0;
            constructionHP = 0.0;
            lastConstructionUpdateTime = currentTime;
        }

        public bool UpdateConstruction(double currentTime)
        {
            if (state != BuildingState.Constructing) return false;
            if (buildersAssigned <= 0) return false;

            double lastUpdate = lastConstructionUpdateTime ?? constructionStartTime ?? currentTime;
            double delta = currentTime - lastUpdate;
            if (delta <= 0) return false;

            double baseHPRate = maxHealth / buildingType.BuildTime();
            double effective = GameConfig.Construction.EffectiveBuilders(buildersAssigned);
            double hpGain = baseHPRate * effective * delta;
            constructionHP = Math.Min(maxHealth, constructionHP + hpGain);
            constructionProgress = constructionHP / maxHealth;
            lastConstructionUpdateTime = currentTime;

            if (constructionProgress >= 1.0)
            {
                CompleteConstruction();
                return true;
            }
            return false;
        }

        public void CompleteConstruction()
        {
            if (state != BuildingState.Constructing) return;
            state = BuildingState.Completed;
            constructionProgress = 1.0;
            health = maxHealth;
            constructionStartTime = null;
            lastConstructionUpdateTime = null;
        }

        public void ApplyBuildingHPBonus(double hpMultiplier = 1.0)
        {
            double baseHealth;
            switch (buildingType)
            {
                case BuildingType.Wall: baseHealth = 600.0; break;
                case BuildingType.Gate: baseHealth = 400.0; break;
                default:
                    baseHealth = buildingType.Category() == BuildingCategory.Military ? 500.0 : 200.0;
                    break;
            }

            double levelMultiplier = 1.0;
            if (buildingType == BuildingType.Tower || buildingType == BuildingType.WoodenFort || buildingType == BuildingType.Castle)
                levelMultiplier = 1.0 + (level - 1) * GameConfig.Defense.HPBonusPerLevel;

            maxHealth = baseHealth * levelMultiplier * hpMultiplier;
            health = Math.Min(health, maxHealth);
        }

        public double? GetRemainingConstructionTime(double currentTime)
        {
            if (state != BuildingState.Constructing) return null;
            if (buildersAssigned <= 0) return null;

            double remainingHP = maxHealth - constructionHP;
            double baseHPRate = maxHealth / buildingType.BuildTime();
            double effective = GameConfig.Construction.EffectiveBuilders(buildersAssigned);
            double currentRate = baseHPRate * effective;
            if (currentRate <= 0) return null;
            return Math.Max(0, remainingHP / currentRate);
        }

        // Upgrade Logic

        public Dictionary<ResourceType, int> GetUpgradeCost()
        {
            return buildingType.UpgradeCost(level);
        }

        public double? GetUpgradeTime()
        {
            return buildingType.UpgradeTime(level);
        }

        public double? GetRemainingUpgradeTime(double currentTime)
        {
            if (state != BuildingState.Upgrading || !upgradeStartTime.HasValue) return null;
            double? upgradeTime = GetUpgradeTime();
            if (!upgradeTime.HasValue) return null;
            double elapsed = currentTime - upgradeStartTime.Value;
            return Math.Max(0, upgradeTime.Value - elapsed);
        }

        public void StartUpgrade(double currentTime)
        {
            if (!CanUpgrade || state != BuildingState.Completed) return;
            state = BuildingState.Upgrading;
            upgradeStartTime = currentTime;
            upgradeProgress = 0.0;
        }

        public bool UpdateUpgrade(double currentTime)
        {
            if (state != BuildingState.Upgrading) return false;
            if (!upgradeStartTime.HasValue) return false;
            double? upgradeTime = GetUpgradeTime();
            if (!upgradeTime.HasValue) return false;

            double elapsed = currentTime - upgradeStartTime.Value;
            upgradeProgress = Math.Min(1.0, elapsed / upgradeTime.Value);

            if (upgradeProgress >= 1.0)
            {
                CompleteUpgrade();
                return true;
            }
            return false;
        }

        public void CompleteUpgrade()
        {
            if (state != BuildingState.Upgrading) return;
            level++;
            state = BuildingState.Completed;
            upgradeProgress = 0.0;
            upgradeStartTime = null;
            health = maxHealth;
        }

        public Dictionary<ResourceType, int> CancelUpgrade()
        {
            if (state != BuildingState.Upgrading) return null;
            var refund = GetUpgradeCost();
            state = BuildingState.Completed;
            upgradeProgress = 0.0;
            upgradeStartTime = null;
            return refund;
        }

        // Demolition Logic

        public double GetDemolitionTime() => buildingType.BuildTime() * 0.5;

        public Dictionary<ResourceType, int> GetDemolitionRefund()
        {
            var refund = new Dictionary<ResourceType, int>();
            foreach (var kvp in buildingType.BuildCost())
                refund[kvp.Key] = (int)(kvp.Value * 0.25);
            return refund;
        }

        public bool CanDemolish => buildingType != BuildingType.CityCenter && state == BuildingState.Completed;

        public void StartDemolition(double currentTime, int demolishers = 1)
        {
            if (!CanDemolish) return;
            state = BuildingState.Demolishing;
            demolitionStartTime = currentTime;
            demolishersAssigned = Math.Max(1, demolishers);
            demolitionProgress = 0.0;
        }

        public bool UpdateDemolition(double currentTime)
        {
            if (state != BuildingState.Demolishing || !demolitionStartTime.HasValue) return false;
            double elapsed = currentTime - demolitionStartTime.Value;
            double demolisherMultiplier = 1.0 + (demolishersAssigned - 1) * 0.5;
            double effectiveTime = GetDemolitionTime() / demolisherMultiplier;
            demolitionProgress = Math.Min(1.0, elapsed / effectiveTime);
            return demolitionProgress >= 1.0;
        }

        public void CancelDemolition()
        {
            if (state != BuildingState.Demolishing) return;
            state = BuildingState.Completed;
            demolitionProgress = 0.0;
            demolitionStartTime = null;
            demolishersAssigned = 0;
        }

        // Training Logic

        public double GetTrainingSpeedMultiplier()
        {
            if (buildingType.Category() != BuildingCategory.Military) return 1.0;
            return 1.0 + (level - 1) * GameConfig.Training.BuildingLevelSpeedBonusPerLevel;
        }

        public bool CanTrain(MilitaryUnitType unitType)
        {
            return state == BuildingState.Completed && unitType.TrainingBuilding() == buildingType;
        }

        public bool CanTrainVillagers()
        {
            return state == BuildingState.Completed &&
                   (buildingType == BuildingType.CityCenter || buildingType == BuildingType.Neighborhood);
        }

        public void StartTraining(MilitaryUnitType unitType, int quantity, double time)
        {
            if (!CanTrain(unitType)) return;
            trainingQueue.Add(new TrainingQueueEntry(unitType, quantity, time));
        }

        public void StartVillagerTraining(int quantity, double time)
        {
            if (!CanTrainVillagers()) return;
            villagerTrainingQueue.Add(new VillagerTrainingEntry(quantity, time));
        }

        public List<TrainingQueueEntry> UpdateTraining(double currentTime, double researchMultiplier = 1.0)
        {
            if (trainingQueue.Count == 0) return new List<TrainingQueueEntry>();

            var completed = new List<TrainingQueueEntry>();
            var completedIndices = new List<int>();
            double buildingSpeedMultiplier = GetTrainingSpeedMultiplier();

            for (int i = 0; i < trainingQueue.Count; i++)
            {
                double combinedMultiplier = researchMultiplier * buildingSpeedMultiplier;
                double progress = trainingQueue[i].GetProgress(currentTime, combinedMultiplier);
                trainingQueue[i].progress = progress;

                if (progress >= 1.0)
                {
                    AddToGarrison(trainingQueue[i].unitType, trainingQueue[i].quantity);
                    completed.Add(trainingQueue[i]);
                    completedIndices.Add(i);
                }
            }

            for (int i = completedIndices.Count - 1; i >= 0; i--)
                trainingQueue.RemoveAt(completedIndices[i]);

            return completed;
        }

        public List<VillagerTrainingEntry> UpdateVillagerTraining(double currentTime)
        {
            if (villagerTrainingQueue.Count == 0) return new List<VillagerTrainingEntry>();

            var completed = new List<VillagerTrainingEntry>();
            var completedIndices = new List<int>();

            for (int i = 0; i < villagerTrainingQueue.Count; i++)
            {
                double progress = villagerTrainingQueue[i].GetProgress(currentTime);
                villagerTrainingQueue[i].progress = progress;

                if (progress >= 1.0)
                {
                    AddVillagersToGarrison(villagerTrainingQueue[i].quantity);
                    completed.Add(villagerTrainingQueue[i]);
                    completedIndices.Add(i);
                }
            }

            for (int i = completedIndices.Count - 1; i >= 0; i--)
                villagerTrainingQueue.RemoveAt(completedIndices[i]);

            return completed;
        }

        // Garrison Logic

        public void AddToGarrison(MilitaryUnitType unitType, int quantity)
        {
            if (garrison.ContainsKey(unitType))
                garrison[unitType] += quantity;
            else
                garrison[unitType] = quantity;
        }

        public int RemoveFromGarrison(MilitaryUnitType unitType, int quantity)
        {
            if (!garrison.ContainsKey(unitType)) return 0;
            int current = garrison[unitType];
            int toRemove = Math.Min(current, quantity);
            int remaining = current - toRemove;
            if (remaining > 0)
                garrison[unitType] = remaining;
            else
                garrison.Remove(unitType);
            return toRemove;
        }

        public void AddVillagersToGarrison(int quantity) { villagerGarrison += quantity; }

        public int RemoveVillagersFromGarrison(int quantity)
        {
            int toRemove = Math.Min(villagerGarrison, quantity);
            villagerGarrison -= toRemove;
            return toRemove;
        }

        public int GetTotalGarrisonedUnits()
        {
            int total = 0;
            foreach (var kvp in garrison) total += kvp.Value;
            return total;
        }

        public int GetGarrisonPopulation()
        {
            int pop = 0;
            foreach (var kvp in garrison)
                pop += kvp.Key.PopSpace() * kvp.Value;
            return pop;
        }

        public int GetTotalGarrisonCount() => GetTotalGarrisonedUnits() + villagerGarrison;

        public int GetGarrisonCapacity()
        {
            return buildingType.Category() == BuildingCategory.Military ? 500 : 100;
        }

        public bool HasGarrisonSpace(int count) => GetTotalGarrisonCount() + count <= GetGarrisonCapacity();

        // Combat Logic

        public void TakeDamage(double amount)
        {
            if (state != BuildingState.Completed && state != BuildingState.Damaged) return;
            health = Math.Max(0, health - amount);
            if (health <= 0)
                state = BuildingState.Destroyed;
            else if (health < maxHealth / 2)
                state = BuildingState.Damaged;
        }

        public void Repair(double amount)
        {
            if (state != BuildingState.Damaged) return;
            health = Math.Min(maxHealth, health + amount);
            if (health >= maxHealth / 2)
                state = BuildingState.Completed;
        }

        // Garrison Defense

        public bool CanProvideGarrisonDefense
        {
            get
            {
                return buildingType == BuildingType.Castle ||
                       buildingType == BuildingType.WoodenFort ||
                       buildingType == BuildingType.Tower;
            }
        }

        public int GarrisonDefenseRange => CanProvideGarrisonDefense ? 1 : 0;

        public double GetGarrisonPierceDamage(double pierceDamageMultiplier = 1.0)
        {
            if (!CanProvideGarrisonDefense || !IsOperational) return 0;
            double pierceDamage = 0;
            if (garrison.ContainsKey(MilitaryUnitType.Archer))
                pierceDamage += garrison[MilitaryUnitType.Archer] * GameConfig.GarrisonDefense.ArcherDamage;
            if (garrison.ContainsKey(MilitaryUnitType.Crossbow))
                pierceDamage += garrison[MilitaryUnitType.Crossbow] * GameConfig.GarrisonDefense.CrossbowDamage;
            return pierceDamage * pierceDamageMultiplier;
        }

        public double GetGarrisonBludgeonDamage()
        {
            if (!CanProvideGarrisonDefense || !IsOperational) return 0;
            double bludgeonDamage = 0;
            if (garrison.ContainsKey(MilitaryUnitType.Mangonel))
                bludgeonDamage += garrison[MilitaryUnitType.Mangonel] * GameConfig.GarrisonDefense.MangonelDamage;
            if (garrison.ContainsKey(MilitaryUnitType.Trebuchet))
                bludgeonDamage += garrison[MilitaryUnitType.Trebuchet] * GameConfig.GarrisonDefense.TrebuchetDamage;
            return bludgeonDamage;
        }

        public bool HasDefensiveGarrison()
        {
            if (!CanProvideGarrisonDefense || !IsOperational) return false;
            int count = 0;
            if (garrison.ContainsKey(MilitaryUnitType.Archer)) count += garrison[MilitaryUnitType.Archer];
            if (garrison.ContainsKey(MilitaryUnitType.Crossbow)) count += garrison[MilitaryUnitType.Crossbow];
            if (garrison.ContainsKey(MilitaryUnitType.Mangonel)) count += garrison[MilitaryUnitType.Mangonel];
            if (garrison.ContainsKey(MilitaryUnitType.Trebuchet)) count += garrison[MilitaryUnitType.Trebuchet];
            return count > 0;
        }

        // Population Capacity

        public int GetPopulationCapacity()
        {
            return buildingType.PopulationCapacityForLevel(level);
        }

        // Army Home Base Capacity

        public int? GetArmyHomeBaseCapacity()
        {
            switch (buildingType)
            {
                case BuildingType.CityCenter: return null; // Unlimited
                case BuildingType.Castle:
                    return GameConfig.Defense.CastleBaseArmyCapacity + (level - 1) * GameConfig.Defense.CastleArmyCapacityPerLevel;
                case BuildingType.WoodenFort:
                    return GameConfig.Defense.FortBaseArmyCapacity + (level - 1) * GameConfig.Defense.FortArmyCapacityPerLevel;
                default: return 0;
            }
        }
    }
}
