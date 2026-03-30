// ============================================================================
// FILE: Data/PlayerCommandRegistry.cs
// PURPOSE: Serialize and deserialize player commands for online streaming.
//          Follows the same pattern as AICommandEnvelope in AICommandData.cs:
//          param structs + switch-based serialization/deserialization.
//          JsonUtility cannot handle Dictionary<> or Guid? fields, so we use
//          parallel arrays and string representations respectively.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Sporefront.Engine;
using Sporefront.Models;
using Sporefront.Commands;

namespace Sporefront.Data
{
    // ================================================================
    // Parameter Structs — one per command type
    // ================================================================

    [Serializable]
    public class AttackParams
    {
        public string armyID;
        public int targetQ;
        public int targetR;
    }

    [Serializable]
    public class BuildParams
    {
        public string buildingType;
        public int coordinateQ;
        public int coordinateR;
        public int rotation;
        public string assignedVillagerGroupID; // "" for null
    }

    [Serializable]
    public class CancelReinforcementParams
    {
        public string reinforcementID;
    }

    [Serializable]
    public class DemolishParams
    {
        public string buildingID;
    }

    [Serializable]
    public class DeployArmyParams
    {
        public string buildingID;
        // JsonUtility cannot serialize Dictionary; use parallel arrays
        public string[] compositionKeys;
        public int[] compositionValues;
    }

    [Serializable]
    public class DeployVillagersParams
    {
        public string buildingID;
        public int count;
    }

    [Serializable]
    public class EntrenchParams
    {
        public string armyID;
    }

    [Serializable]
    public class GarrisonArmyParams
    {
        public string armyID;
        public string buildingID;
    }

    [Serializable]
    public class GatherParams
    {
        public string villagerGroupID;
        public string resourcePointID;
    }

    [Serializable]
    public class StopGatheringParams
    {
        public string villagerGroupID;
    }

    [Serializable]
    public class HuntParams
    {
        public string villagerGroupID;
        public string resourcePointID;
    }

    [Serializable]
    public class JoinVillagerGroupParams
    {
        public string buildingID;
        public string targetVillagerGroupID;
        public int count;
    }

    [Serializable]
    public class MarketTradeParams
    {
        public string buildingID;
        // JsonUtility cannot serialize Dictionary; use parallel arrays
        public string[] inputResourceKeys;
        public int[] inputResourceValues;
        public string outputType;
    }

    [Serializable]
    public class MoveParams
    {
        public string entityID;
        public int destinationQ;
        public int destinationR;
        public bool isArmy;
    }

    [Serializable]
    public class RecruitCommanderParams
    {
        public string specialty;
    }

    [Serializable]
    public class ReinforceArmyParams
    {
        public string buildingID;
        public string armyID;
        // JsonUtility cannot serialize Dictionary; use parallel arrays
        public string[] unitKeys;
        public int[] unitValues;
    }

    [Serializable]
    public class ResearchParams
    {
        public string researchTypeRawValue;
        public string buildingID;
    }

    // CancelResearchCommand has no fields beyond playerID

    [Serializable]
    public class RetreatParams
    {
        public string armyID;
    }

    [Serializable]
    public class StopMovementParams
    {
        public string armyID;
    }

    // SurrenderCommand has no fields beyond playerID

    [Serializable]
    public class TrainMilitaryParams
    {
        public string buildingID;
        public string unitType;
        public int quantity;
    }

    [Serializable]
    public class TrainVillagerParams
    {
        public string buildingID;
        public int quantity;
    }

    [Serializable]
    public class UpgradeParams
    {
        public string buildingID;
        public string assignedVillagerGroupID; // "" for null
    }

    [Serializable]
    public class UpgradeUnitParams
    {
        public string upgradeTypeRawValue;
        public string buildingID;
    }

    [Serializable]
    public class MoveScoutParams
    {
        public string scoutID;
        public int destinationQ;
        public int destinationR;
    }

    [Serializable]
    public class TrainScoutParams
    {
        public string buildingID;
    }

    // ================================================================
    // Player Command Registry
    // ================================================================

    public static class PlayerCommandRegistry
    {
        // ================================================================
        // Serialize — IEngineCommand → JSON string
        // Handles Dictionary and Guid? fields that JsonUtility drops
        // ================================================================

        public static string Serialize(IEngineCommand command)
        {
            if (command is AttackCommand attack)
            {
                return JsonUtility.ToJson(new AttackParams
                {
                    armyID = attack.armyID.ToString(),
                    targetQ = attack.targetCoordinate.q,
                    targetR = attack.targetCoordinate.r
                });
            }
            if (command is BuildCommand build)
            {
                return JsonUtility.ToJson(new BuildParams
                {
                    buildingType = build.buildingType.ToString(),
                    coordinateQ = build.coordinate.q,
                    coordinateR = build.coordinate.r,
                    rotation = build.rotation,
                    assignedVillagerGroupID = build.assignedVillagerGroupID.HasValue
                        ? build.assignedVillagerGroupID.Value.ToString() : ""
                });
            }
            if (command is CancelReinforcementCommand cancelReinforce)
            {
                return JsonUtility.ToJson(new CancelReinforcementParams
                {
                    reinforcementID = cancelReinforce.reinforcementID.ToString()
                });
            }
            if (command is CancelDemolishCommand cancelDemolish)
            {
                return JsonUtility.ToJson(new DemolishParams
                {
                    buildingID = cancelDemolish.buildingID.ToString()
                });
            }
            if (command is DemolishCommand demolish)
            {
                return JsonUtility.ToJson(new DemolishParams
                {
                    buildingID = demolish.buildingID.ToString()
                });
            }
            if (command is DeployArmyCommand deployArmy)
            {
                var keys = new List<string>();
                var values = new List<int>();
                if (deployArmy.composition != null)
                {
                    foreach (var kvp in deployArmy.composition)
                    {
                        keys.Add(kvp.Key.ToString());
                        values.Add(kvp.Value);
                    }
                }
                return JsonUtility.ToJson(new DeployArmyParams
                {
                    buildingID = deployArmy.buildingID.ToString(),
                    compositionKeys = keys.ToArray(),
                    compositionValues = values.ToArray()
                });
            }
            if (command is DeployVillagersCommand deployVillagers)
            {
                return JsonUtility.ToJson(new DeployVillagersParams
                {
                    buildingID = deployVillagers.buildingID.ToString(),
                    count = deployVillagers.count
                });
            }
            if (command is EntrenchCommand entrench)
            {
                return JsonUtility.ToJson(new EntrenchParams
                {
                    armyID = entrench.armyID.ToString()
                });
            }
            if (command is GarrisonArmyCommand garrison)
            {
                return JsonUtility.ToJson(new GarrisonArmyParams
                {
                    armyID = garrison.armyID.ToString(),
                    buildingID = garrison.buildingID.ToString()
                });
            }
            if (command is StopGatheringCommand stopGather)
            {
                return JsonUtility.ToJson(new StopGatheringParams
                {
                    villagerGroupID = stopGather.villagerGroupID.ToString()
                });
            }
            if (command is GatherCommand gather)
            {
                return JsonUtility.ToJson(new GatherParams
                {
                    villagerGroupID = gather.villagerGroupID.ToString(),
                    resourcePointID = gather.resourcePointID.ToString()
                });
            }
            if (command is HuntCommand hunt)
            {
                return JsonUtility.ToJson(new HuntParams
                {
                    villagerGroupID = hunt.villagerGroupID.ToString(),
                    resourcePointID = hunt.resourcePointID.ToString()
                });
            }
            if (command is JoinVillagerGroupCommand joinGroup)
            {
                return JsonUtility.ToJson(new JoinVillagerGroupParams
                {
                    buildingID = joinGroup.buildingID.ToString(),
                    targetVillagerGroupID = joinGroup.targetVillagerGroupID.ToString(),
                    count = joinGroup.count
                });
            }
            if (command is MarketTradeCommand trade)
            {
                var keys = new List<string>();
                var values = new List<int>();
                if (trade.inputResources != null)
                {
                    foreach (var kvp in trade.inputResources)
                    {
                        keys.Add(kvp.Key.ToString());
                        values.Add(kvp.Value);
                    }
                }
                return JsonUtility.ToJson(new MarketTradeParams
                {
                    buildingID = trade.buildingID.ToString(),
                    inputResourceKeys = keys.ToArray(),
                    inputResourceValues = values.ToArray(),
                    outputType = trade.outputType.ToString()
                });
            }
            if (command is MoveCommand move)
            {
                return JsonUtility.ToJson(new MoveParams
                {
                    entityID = move.entityID.ToString(),
                    destinationQ = move.destination.q,
                    destinationR = move.destination.r,
                    isArmy = move.isArmy
                });
            }
            if (command is RecruitCommanderCommand recruit)
            {
                return JsonUtility.ToJson(new RecruitCommanderParams
                {
                    specialty = recruit.specialty.ToString()
                });
            }
            if (command is ReinforceArmyCommand reinforce)
            {
                var keys = new List<string>();
                var values = new List<int>();
                if (reinforce.units != null)
                {
                    foreach (var kvp in reinforce.units)
                    {
                        keys.Add(kvp.Key.ToString());
                        values.Add(kvp.Value);
                    }
                }
                return JsonUtility.ToJson(new ReinforceArmyParams
                {
                    buildingID = reinforce.buildingID.ToString(),
                    armyID = reinforce.armyID.ToString(),
                    unitKeys = keys.ToArray(),
                    unitValues = values.ToArray()
                });
            }
            if (command is CancelResearchCommand)
            {
                // No fields beyond playerID
                return "{}";
            }
            if (command is ResearchCommand research)
            {
                return JsonUtility.ToJson(new ResearchParams
                {
                    researchTypeRawValue = research.researchTypeRawValue,
                    buildingID = research.buildingID.ToString()
                });
            }
            if (command is RetreatCommand retreat)
            {
                return JsonUtility.ToJson(new RetreatParams
                {
                    armyID = retreat.armyID.ToString()
                });
            }
            if (command is StopMovementCommand stopMove)
            {
                return JsonUtility.ToJson(new StopMovementParams
                {
                    armyID = stopMove.armyID.ToString()
                });
            }
            if (command is SurrenderCommand)
            {
                // No fields beyond playerID
                return "{}";
            }
            if (command is TrainMilitaryCommand trainMil)
            {
                return JsonUtility.ToJson(new TrainMilitaryParams
                {
                    buildingID = trainMil.buildingID.ToString(),
                    unitType = trainMil.unitType.ToString(),
                    quantity = trainMil.quantity
                });
            }
            if (command is TrainVillagerCommand trainVil)
            {
                return JsonUtility.ToJson(new TrainVillagerParams
                {
                    buildingID = trainVil.buildingID.ToString(),
                    quantity = trainVil.quantity
                });
            }
            if (command is CancelUpgradeCommand cancelUpgrade)
            {
                return JsonUtility.ToJson(new DemolishParams
                {
                    buildingID = cancelUpgrade.buildingID.ToString()
                });
            }
            if (command is UpgradeCommand upgrade)
            {
                return JsonUtility.ToJson(new UpgradeParams
                {
                    buildingID = upgrade.buildingID.ToString(),
                    assignedVillagerGroupID = upgrade.assignedVillagerGroupID.HasValue
                        ? upgrade.assignedVillagerGroupID.Value.ToString() : ""
                });
            }
            if (command is UpgradeUnitCommand upgradeUnit)
            {
                return JsonUtility.ToJson(new UpgradeUnitParams
                {
                    upgradeTypeRawValue = upgradeUnit.upgradeTypeRawValue,
                    buildingID = upgradeUnit.buildingID.ToString()
                });
            }
            if (command is MoveScoutCommand moveScout)
            {
                return JsonUtility.ToJson(new MoveScoutParams
                {
                    scoutID = moveScout.scoutID.ToString(),
                    destinationQ = moveScout.destination.q,
                    destinationR = moveScout.destination.r
                });
            }
            if (command is TrainScoutCommand trainScout)
            {
                return JsonUtility.ToJson(new TrainScoutParams
                {
                    buildingID = trainScout.buildingID.ToString()
                });
            }

            DebugLog.Log(string.Format("PlayerCommandRegistry: Unknown command type: {0}",
                command.GetType().Name));
            // Fallback to raw JsonUtility (will miss Dictionary/nullable fields)
            return JsonUtility.ToJson(command);
        }

        // ================================================================
        // Safe FromJson — returns null instead of throwing on malformed input
        // ================================================================

        private static T SafeFromJson<T>(string json) where T : class
        {
            try
            {
                var result = JsonUtility.FromJson<T>(json);
                return result;
            }
            catch
            {
                return null;
            }
        }

        // ================================================================
        // Deserialize — JSON + metadata → IEngineCommand
        // ================================================================

        public static IEngineCommand Deserialize(string commandType, string json,
            string commandID, string playerIDStr, double timestamp)
        {
            Guid pid;
            if (!Guid.TryParse(playerIDStr, out pid)) return null;

            Guid cmdId;
            if (!Guid.TryParse(commandID, out cmdId))
                cmdId = Guid.NewGuid();

            switch (commandType)
            {
                case "AttackCommand":
                {
                    var p = SafeFromJson<AttackParams>(json);
                    if (p == null) return null;
                    Guid armyID;
                    if (!Guid.TryParse(p.armyID, out armyID)) return null;
                    var target = new HexCoordinate(p.targetQ, p.targetR);
                    return new AttackCommand(cmdId, pid, timestamp, armyID, target);
                }

                case "BuildCommand":
                {
                    var p = SafeFromJson<BuildParams>(json);
                    if (p == null) return null;
                    BuildingType bt;
                    if (!Enum.TryParse<BuildingType>(p.buildingType, out bt)) return null;
                    var coord = new HexCoordinate(p.coordinateQ, p.coordinateR);
                    Guid? vgID = null;
                    Guid parsedVg;
                    if (!string.IsNullOrEmpty(p.assignedVillagerGroupID) &&
                        Guid.TryParse(p.assignedVillagerGroupID, out parsedVg))
                        vgID = parsedVg;
                    return new BuildCommand(cmdId, pid, timestamp, bt, coord, p.rotation, vgID);
                }

                case "CancelReinforcementCommand":
                {
                    var p = SafeFromJson<CancelReinforcementParams>(json);
                    if (p == null) return null;
                    Guid rID;
                    if (!Guid.TryParse(p.reinforcementID, out rID)) return null;
                    return new CancelReinforcementCommand(cmdId, pid, timestamp, rID);
                }

                case "DemolishCommand":
                {
                    var p = SafeFromJson<DemolishParams>(json);
                    if (p == null) return null;
                    Guid bID;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    return new DemolishCommand(cmdId, pid, timestamp, bID);
                }

                case "CancelDemolishCommand":
                {
                    var p = SafeFromJson<DemolishParams>(json);
                    if (p == null) return null;
                    Guid bID;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    return new CancelDemolishCommand(cmdId, pid, timestamp, bID);
                }

                case "DeployArmyCommand":
                {
                    var p = SafeFromJson<DeployArmyParams>(json);
                    if (p == null) return null;
                    Guid bID;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    var composition = new Dictionary<MilitaryUnitType, int>();
                    if (p.compositionKeys != null && p.compositionValues != null
                        && p.compositionKeys.Length == p.compositionValues.Length)
                    {
                        for (int i = 0; i < p.compositionKeys.Length; i++)
                        {
                            MilitaryUnitType ut;
                            if (Enum.TryParse<MilitaryUnitType>(p.compositionKeys[i], out ut))
                                composition[ut] = p.compositionValues[i];
                        }
                    }
                    return new DeployArmyCommand(cmdId, pid, timestamp, bID, composition);
                }

                case "DeployVillagersCommand":
                {
                    var p = SafeFromJson<DeployVillagersParams>(json);
                    if (p == null) return null;
                    Guid bID;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    return new DeployVillagersCommand(cmdId, pid, timestamp, bID, p.count);
                }

                case "EntrenchCommand":
                {
                    var p = SafeFromJson<EntrenchParams>(json);
                    if (p == null) return null;
                    Guid aID;
                    if (!Guid.TryParse(p.armyID, out aID)) return null;
                    return new EntrenchCommand(cmdId, pid, timestamp, aID);
                }

                case "GarrisonArmyCommand":
                {
                    var p = SafeFromJson<GarrisonArmyParams>(json);
                    if (p == null) return null;
                    Guid aID, bID;
                    if (!Guid.TryParse(p.armyID, out aID)) return null;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    return new GarrisonArmyCommand(cmdId, pid, timestamp, aID, bID);
                }

                case "GatherCommand":
                {
                    var p = SafeFromJson<GatherParams>(json);
                    if (p == null) return null;
                    Guid vgID, rpID;
                    if (!Guid.TryParse(p.villagerGroupID, out vgID)) return null;
                    if (!Guid.TryParse(p.resourcePointID, out rpID)) return null;
                    return new GatherCommand(cmdId, pid, timestamp, vgID, rpID);
                }

                case "StopGatheringCommand":
                {
                    var p = SafeFromJson<StopGatheringParams>(json);
                    if (p == null) return null;
                    Guid vgID;
                    if (!Guid.TryParse(p.villagerGroupID, out vgID)) return null;
                    return new StopGatheringCommand(cmdId, pid, timestamp, vgID);
                }

                case "HuntCommand":
                {
                    var p = SafeFromJson<HuntParams>(json);
                    if (p == null) return null;
                    Guid vgID, rpID;
                    if (!Guid.TryParse(p.villagerGroupID, out vgID)) return null;
                    if (!Guid.TryParse(p.resourcePointID, out rpID)) return null;
                    return new HuntCommand(cmdId, pid, timestamp, vgID, rpID);
                }

                case "JoinVillagerGroupCommand":
                {
                    var p = SafeFromJson<JoinVillagerGroupParams>(json);
                    if (p == null) return null;
                    Guid bID, tID;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    if (!Guid.TryParse(p.targetVillagerGroupID, out tID)) return null;
                    return new JoinVillagerGroupCommand(cmdId, pid, timestamp, bID, tID, p.count);
                }

                case "MarketTradeCommand":
                {
                    var p = SafeFromJson<MarketTradeParams>(json);
                    if (p == null) return null;
                    Guid bID;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    ResourceType outType;
                    if (!Enum.TryParse<ResourceType>(p.outputType, out outType)) return null;
                    var inputResources = new Dictionary<ResourceType, int>();
                    if (p.inputResourceKeys != null && p.inputResourceValues != null
                        && p.inputResourceKeys.Length == p.inputResourceValues.Length)
                    {
                        for (int i = 0; i < p.inputResourceKeys.Length; i++)
                        {
                            ResourceType rt;
                            if (Enum.TryParse<ResourceType>(p.inputResourceKeys[i], out rt))
                                inputResources[rt] = p.inputResourceValues[i];
                        }
                    }
                    return new MarketTradeCommand(cmdId, pid, timestamp, bID, inputResources, outType);
                }

                case "MoveCommand":
                {
                    var p = SafeFromJson<MoveParams>(json);
                    if (p == null) return null;
                    Guid eID;
                    if (!Guid.TryParse(p.entityID, out eID)) return null;
                    var dest = new HexCoordinate(p.destinationQ, p.destinationR);
                    return new MoveCommand(cmdId, pid, timestamp, eID, dest, p.isArmy);
                }

                case "RecruitCommanderCommand":
                {
                    var p = SafeFromJson<RecruitCommanderParams>(json);
                    if (p == null) return null;
                    CommanderSpecialty spec;
                    if (!Enum.TryParse<CommanderSpecialty>(p.specialty, out spec)) return null;
                    return new RecruitCommanderCommand(cmdId, pid, timestamp, spec);
                }

                case "ReinforceArmyCommand":
                {
                    var p = SafeFromJson<ReinforceArmyParams>(json);
                    if (p == null) return null;
                    Guid bID, aID;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    if (!Guid.TryParse(p.armyID, out aID)) return null;
                    var units = new Dictionary<MilitaryUnitType, int>();
                    if (p.unitKeys != null && p.unitValues != null
                        && p.unitKeys.Length == p.unitValues.Length)
                    {
                        for (int i = 0; i < p.unitKeys.Length; i++)
                        {
                            MilitaryUnitType ut;
                            if (Enum.TryParse<MilitaryUnitType>(p.unitKeys[i], out ut))
                                units[ut] = p.unitValues[i];
                        }
                    }
                    return new ReinforceArmyCommand(cmdId, pid, timestamp, bID, aID, units);
                }

                case "ResearchCommand":
                {
                    var p = SafeFromJson<ResearchParams>(json);
                    if (p == null) return null;
                    Guid bID;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    return new ResearchCommand(cmdId, pid, timestamp, p.researchTypeRawValue, bID);
                }

                case "CancelResearchCommand":
                {
                    return new CancelResearchCommand(cmdId, pid, timestamp);
                }

                case "RetreatCommand":
                {
                    var p = SafeFromJson<RetreatParams>(json);
                    if (p == null) return null;
                    Guid aID;
                    if (!Guid.TryParse(p.armyID, out aID)) return null;
                    return new RetreatCommand(cmdId, pid, timestamp, aID);
                }

                case "StopMovementCommand":
                {
                    var p = SafeFromJson<StopMovementParams>(json);
                    if (p == null) return null;
                    Guid aID;
                    if (!Guid.TryParse(p.armyID, out aID)) return null;
                    return new StopMovementCommand(cmdId, pid, timestamp, aID);
                }

                case "SurrenderCommand":
                {
                    return new SurrenderCommand(cmdId, pid, timestamp);
                }

                case "TrainMilitaryCommand":
                {
                    var p = SafeFromJson<TrainMilitaryParams>(json);
                    if (p == null) return null;
                    Guid bID;
                    MilitaryUnitType ut;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    if (!Enum.TryParse<MilitaryUnitType>(p.unitType, out ut)) return null;
                    return new TrainMilitaryCommand(cmdId, pid, timestamp, bID, ut, p.quantity);
                }

                case "TrainVillagerCommand":
                {
                    var p = SafeFromJson<TrainVillagerParams>(json);
                    if (p == null) return null;
                    Guid bID;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    return new TrainVillagerCommand(cmdId, pid, timestamp, bID, p.quantity);
                }

                case "UpgradeCommand":
                {
                    var p = SafeFromJson<UpgradeParams>(json);
                    if (p == null) return null;
                    Guid bID;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    Guid? vgID = null;
                    Guid parsedVg;
                    if (!string.IsNullOrEmpty(p.assignedVillagerGroupID) &&
                        Guid.TryParse(p.assignedVillagerGroupID, out parsedVg))
                        vgID = parsedVg;
                    return new UpgradeCommand(cmdId, pid, timestamp, bID, vgID);
                }

                case "CancelUpgradeCommand":
                {
                    var p = SafeFromJson<DemolishParams>(json);
                    if (p == null) return null;
                    Guid bID;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    return new CancelUpgradeCommand(cmdId, pid, timestamp, bID);
                }

                case "UpgradeUnitCommand":
                {
                    var p = SafeFromJson<UpgradeUnitParams>(json);
                    if (p == null) return null;
                    Guid bID;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    return new UpgradeUnitCommand(cmdId, pid, timestamp, p.upgradeTypeRawValue, bID);
                }

                case "MoveScoutCommand":
                {
                    var p = SafeFromJson<MoveScoutParams>(json);
                    if (p == null) return null;
                    Guid sID;
                    if (!Guid.TryParse(p.scoutID, out sID)) return null;
                    var dest = new HexCoordinate(p.destinationQ, p.destinationR);
                    return new MoveScoutCommand(cmdId, pid, timestamp, sID, dest);
                }

                case "TrainScoutCommand":
                {
                    var p = SafeFromJson<TrainScoutParams>(json);
                    if (p == null) return null;
                    Guid bID;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    return new TrainScoutCommand(cmdId, pid, timestamp, bID);
                }

                default:
                    DebugLog.Log(string.Format(
                        "PlayerCommandRegistry: Unknown command type: {0}", commandType));
                    return null;
            }
        }
    }
}
