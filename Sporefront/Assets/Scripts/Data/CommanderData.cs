using System;
using System.Collections.Generic;
using Sporefront.Models;
using Sporefront.Engine;

namespace Sporefront.Data
{
    [System.Serializable]
    public class CommanderData
    {
        public Guid id;
        public string name;
        public Guid? ownerID;
        public Guid? assignedArmyID;

        public int level = 1;
        public int experience;

        public CommanderSpecialty specialty;
        public CommanderRank rank = CommanderRank.Recruit;

        // Base stats
        public int baseLeadership;
        public int baseTactics;
        public int baseLogistics;
        public int baseRationing;
        public int baseEndurance;

        // Stamina
        public double stamina = 100.0;
        public double lastStaminaUpdateTime;

        public const double MaxStamina = 100.0;
        public const double StaminaCostPerCommand = 5.0;
        public const double StaminaRegenPerSecond = 1.0 / 60.0;

        public string portraitColorHex = "#0000FF";

        public CommanderData(string name, CommanderSpecialty specialty, Guid? ownerID = null,
            int? baseLeadership = null, int? baseTactics = null,
            int? baseLogistics = null, int? baseRationing = null, int? baseEndurance = null)
        {
            var profile = specialty.StatProfile();
            this.id = Guid.NewGuid();
            this.name = name;
            this.specialty = specialty;
            this.ownerID = ownerID;
            this.baseLeadership = baseLeadership ?? profile.baseLeadership;
            this.baseTactics = baseTactics ?? profile.baseTactics;
            this.baseLogistics = baseLogistics ?? profile.baseLogistics;
            this.baseRationing = baseRationing ?? profile.baseRationing;
            this.baseEndurance = baseEndurance ?? profile.baseEndurance;
        }

        // Computed Stats

        public int Leadership
        {
            get
            {
                var profile = specialty.StatProfile();
                return baseLeadership + (level - 1) * profile.leadershipPerLevel + rank.Index() * profile.leadershipPerRank;
            }
        }

        public int Tactics
        {
            get
            {
                var profile = specialty.StatProfile();
                return baseTactics + (level - 1) * profile.tacticsPerLevel + rank.Index() * profile.tacticsPerRank;
            }
        }

        public int Logistics
        {
            get
            {
                var profile = specialty.StatProfile();
                return baseLogistics + (level - 1) * profile.logisticsPerLevel + rank.Index() * profile.logisticsPerRank;
            }
        }

        public int Rationing
        {
            get
            {
                var profile = specialty.StatProfile();
                return baseRationing + (level - 1) * profile.rationingPerLevel + rank.Index() * profile.rationingPerRank;
            }
        }

        public int Endurance
        {
            get
            {
                var profile = specialty.StatProfile();
                return baseEndurance + (level - 1) * profile.endurancePerLevel + rank.Index() * profile.endurancePerRank;
            }
        }

        // Stamina Management

        public bool HasEnoughStamina(double cost = StaminaCostPerCommand) => stamina >= cost;

        public bool ConsumeStamina(double cost = StaminaCostPerCommand)
        {
            if (!HasEnoughStamina(cost)) return false;
            stamina = Math.Max(0, stamina - cost);
            return true;
        }

        public void RegenerateStamina(double currentTime)
        {
            if (lastStaminaUpdateTime <= 0)
            {
                lastStaminaUpdateTime = currentTime;
                return;
            }

            double elapsed = currentTime - lastStaminaUpdateTime;
            double enduranceMultiplier = 1.0 + Endurance * GameConfig.Commander.EnduranceRegenScaling;
            double regenAmount = elapsed * StaminaRegenPerSecond * enduranceMultiplier;

            if (stamina < MaxStamina)
                stamina = Math.Min(MaxStamina, stamina + regenAmount);

            lastStaminaUpdateTime = currentTime;
        }

        // Experience and Leveling

        public void AddExperience(int amount)
        {
            experience += amount;
            CheckLevelUp();
        }

        private void CheckLevelUp()
        {
            int requiredXP = level * 100;
            if (experience >= requiredXP)
            {
                level++;
                experience -= requiredXP;
                CheckRankPromotion();
            }
        }

        private void CheckRankPromotion()
        {
            var newRank = CommanderRankExtensions.RankForLevel(level);
            if (newRank.Index() > rank.Index())
                rank = newRank;
        }

        // Combat Bonuses

        public double GetAttackBonus(UnitCategory category)
        {
            double specialtyAttackBonus = specialty.AttackBonus(category);
            double levelBonus = level * 0.01;
            return specialtyAttackBonus * 0.1 + levelBonus;
        }

        public double GetDefenseBonus()
        {
            double armorBonus = specialty.ArmorBonus();
            double levelBonus = level * 0.01;
            return armorBonus * 0.1 + levelBonus;
        }

        public double GetSpeedBonus()
        {
            return 1.0 + Logistics * GameConfig.Commander.LogisticsSpeedScaling;
        }

        // Static Helpers

        public static string RandomName()
        {
            string[] names = {
                "Marcus", "Elena", "Darius", "Sable", "Aldric",
                "Freya", "Cassian", "Rhea", "Theron", "Isolde",
                "Gideon", "Astrid", "Balthazar", "Cora", "Fenris",
                "Helena", "Kael", "Lyra", "Orin", "Petra"
            };
            return names[new System.Random().Next(names.Length)];
        }

        public static CommanderSpecialty SpecialtyForComposition(Dictionary<MilitaryUnitType, int> composition)
        {
            var categoryCounts = new Dictionary<UnitCategory, int>();
            foreach (var kvp in composition)
            {
                var cat = kvp.Key.Category();
                if (categoryCounts.ContainsKey(cat))
                    categoryCounts[cat] += kvp.Value;
                else
                    categoryCounts[cat] = kvp.Value;
            }

            if (categoryCounts.Count == 0) return CommanderSpecialty.InfantryAggressive;

            UnitCategory dominant = UnitCategory.Infantry;
            int maxCount = 0;
            foreach (var kvp in categoryCounts)
            {
                if (kvp.Value > maxCount) { dominant = kvp.Key; maxCount = kvp.Value; }
            }

            switch (dominant)
            {
                case UnitCategory.Infantry: return CommanderSpecialty.InfantryAggressive;
                case UnitCategory.Cavalry: return CommanderSpecialty.CavalryAggressive;
                case UnitCategory.Ranged: return CommanderSpecialty.RangedAggressive;
                case UnitCategory.Siege: return CommanderSpecialty.SiegeAggressive;
                default: return CommanderSpecialty.InfantryAggressive;
            }
        }
    }
}
