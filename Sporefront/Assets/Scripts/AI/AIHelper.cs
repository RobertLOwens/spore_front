using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Data;
using Sporefront.Engine;

namespace Sporefront.AI
{
    public static class AIHelper
    {
        /// <summary>
        /// Returns true if enough time has elapsed since lastTime, and updates lastTime.
        /// Use as a guard: if (!AIHelper.ShouldExecute(ref aiState.lastXTime, currentTime, interval)) return;
        /// </summary>
        public static bool ShouldExecute(ref double lastTime, double currentTime, double interval)
        {
            if (currentTime - lastTime < interval)
                return false;
            lastTime = currentTime;
            return true;
        }

        /// <summary>
        /// Checks all villager groups with hunting tasks — if they have arrived at the animal,
        /// execute instant combat and convert to carcass gathering.
        /// Shared between AIController and SimulationAIController.
        /// </summary>
        public static void ProcessHuntArrivals(GameState state, ResourceEngine resourceEngine)
        {
            // Defensive copy: RemoveVillagerGroup may modify the collection during iteration
            var groups = state.villagerGroups.Values.ToList();

            foreach (var group in groups)
            {
                var huntTask = group.currentTask as HuntingTask;
                if (huntTask == null) continue;
                if (group.currentPath != null) continue; // Still en route

                var resourcePointID = huntTask.ResourcePointID;
                var resource = state.GetResourcePoint(resourcePointID);
                if (resource == null)
                {
                    group.ClearTask();
                    continue;
                }

                if (!group.coordinate.Equals(resource.coordinate)) continue;

                // At target — execute hunt combat
                double villagerAttack = group.villagerCount * GameConfig.AI.Hunting.VillagerAttackPower;
                double damageToAnimal = Math.Max(1.0, villagerAttack - resource.resourceType.DefensePower());
                resource.TakeDamage(damageToAnimal);

                double animalAttack = resource.resourceType.AttackPower();
                double damageToVillagers = Math.Max(0.0, animalAttack - group.villagerCount * GameConfig.AI.Hunting.VillagerDefenseFactor);
                int villagersLost = (int)(damageToVillagers / GameConfig.AI.Hunting.VillagerDeathThreshold);
                if (villagersLost > 0) group.RemoveVillagers(villagersLost);

                if (resource.currentHealth <= 0)
                {
                    // Animal killed — create carcass and start gathering
                    var carcass = resource.CreateCarcassData();
                    if (carcass != null)
                    {
                        state.RemoveResourcePoint(resource.id);
                        state.AddResourcePoint(carcass);

                        bool registered = resourceEngine.StartGathering(
                            group.id, carcass.id);
                        if (registered)
                        {
                            resourceEngine.UpdateCollectionRates(
                                group.ownerID ?? Guid.Empty);
                        }
                        else
                        {
                            group.ClearTask();
                        }
                    }
                    else
                    {
                        group.ClearTask();
                    }
                }
                else
                {
                    // Animal survived — clear task, AI will retry next cycle
                    group.ClearTask();
                }

                if (group.villagerCount <= 0)
                {
                    resourceEngine.StopGathering(group.id);
                    state.RemoveVillagerGroup(group.id);
                }
            }
        }
    }
}
