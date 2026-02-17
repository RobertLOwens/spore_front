// ============================================================================
// FILE: Data/AICommandData.cs
// PURPOSE: Serializable envelope for AI commands in online sessions
//          C# port of AICommandData.swift
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Sporefront.Data;
using Sporefront.Models;
using Sporefront.Engine;
using Sporefront.AI.Commands;

namespace Sporefront.Data
{
    // ================================================================
    // AI Command Type
    // ================================================================

    public enum AICommandType
    {
        AIBuild,
        AITrainMilitary,
        AITrainVillager,
        AIDeployArmy,
        AIDeployVillagers,
        AIGather,
        AIMove,
        AIStartResearch,
        AIEntrench,
        AIUpgradeUnit
    }

    // ================================================================
    // AI Command Parameter Structs
    // ================================================================

    [Serializable]
    public class AIBuildParams
    {
        public string buildingType;
        public int coordinateQ;
        public int coordinateR;
        public int rotation;
    }

    [Serializable]
    public class AITrainMilitaryParams
    {
        public string buildingID;
        public string unitType;
        public int quantity;
    }

    [Serializable]
    public class AITrainVillagerParams
    {
        public string buildingID;
        public int quantity;
    }

    [Serializable]
    public class AIDeployArmyParams
    {
        public string buildingID;
        // JsonUtility cannot serialize dictionaries; use parallel arrays
        public string[] compositionKeys;
        public int[] compositionValues;
    }

    [Serializable]
    public class AIDeployVillagersParams
    {
        public string buildingID;
        public int quantity;
    }

    [Serializable]
    public class AIGatherParams
    {
        public string villagerGroupID;
        public string resourcePointID;
    }

    [Serializable]
    public class AIMoveParams
    {
        public string entityID;
        public int destinationQ;
        public int destinationR;
        public bool isArmy;
    }

    [Serializable]
    public class AIStartResearchParams
    {
        public string researchType;
    }

    [Serializable]
    public class AIEntrenchParams
    {
        public string armyID;
    }

    [Serializable]
    public class AIUpgradeUnitParams
    {
        public string upgradeType;
        public string buildingID;
    }

    // ================================================================
    // AI Command Envelope
    // ================================================================

    [Serializable]
    public class AICommandEnvelope
    {
        public int aiCommandType; // AICommandType as int for JsonUtility
        public string commandID;
        public string playerID;
        public double timestamp;
        public string parametersJson; // JSON-encoded parameter struct

        // ================================================================
        // Serialize from BaseEngineCommand
        // ================================================================

        public static AICommandEnvelope From(BaseEngineCommand command)
        {
            AICommandType aiType;
            string paramJson;

            if (command is AIBuildCommand buildCmd)
            {
                aiType = AICommandType.AIBuild;
                paramJson = JsonUtility.ToJson(new AIBuildParams
                {
                    buildingType = buildCmd.buildingType.ToString(),
                    coordinateQ = buildCmd.coordinate.q,
                    coordinateR = buildCmd.coordinate.r,
                    rotation = buildCmd.rotation
                });
            }
            else if (command is AITrainMilitaryCommand trainMilCmd)
            {
                aiType = AICommandType.AITrainMilitary;
                paramJson = JsonUtility.ToJson(new AITrainMilitaryParams
                {
                    buildingID = trainMilCmd.buildingID.ToString(),
                    unitType = trainMilCmd.unitType.ToString(),
                    quantity = trainMilCmd.quantity
                });
            }
            else if (command is AITrainVillagerCommand trainVilCmd)
            {
                aiType = AICommandType.AITrainVillager;
                paramJson = JsonUtility.ToJson(new AITrainVillagerParams
                {
                    buildingID = trainVilCmd.buildingID.ToString(),
                    quantity = trainVilCmd.quantity
                });
            }
            else if (command is AIDeployArmyCommand deployArmyCmd)
            {
                aiType = AICommandType.AIDeployArmy;
                var keys = new List<string>();
                var values = new List<int>();
                foreach (var kvp in deployArmyCmd.composition)
                {
                    keys.Add(kvp.Key.ToString());
                    values.Add(kvp.Value);
                }
                paramJson = JsonUtility.ToJson(new AIDeployArmyParams
                {
                    buildingID = deployArmyCmd.buildingID.ToString(),
                    compositionKeys = keys.ToArray(),
                    compositionValues = values.ToArray()
                });
            }
            else if (command is AIDeployVillagersCommand deployVilCmd)
            {
                aiType = AICommandType.AIDeployVillagers;
                paramJson = JsonUtility.ToJson(new AIDeployVillagersParams
                {
                    buildingID = deployVilCmd.buildingID.ToString(),
                    quantity = deployVilCmd.quantity
                });
            }
            else if (command is AIGatherCommand gatherCmd)
            {
                aiType = AICommandType.AIGather;
                paramJson = JsonUtility.ToJson(new AIGatherParams
                {
                    villagerGroupID = gatherCmd.villagerGroupID.ToString(),
                    resourcePointID = gatherCmd.resourcePointID.ToString()
                });
            }
            else if (command is AIMoveCommand moveCmd)
            {
                aiType = AICommandType.AIMove;
                paramJson = JsonUtility.ToJson(new AIMoveParams
                {
                    entityID = moveCmd.entityID.ToString(),
                    destinationQ = moveCmd.destination.q,
                    destinationR = moveCmd.destination.r,
                    isArmy = moveCmd.isArmy
                });
            }
            else if (command is AIStartResearchCommand researchCmd)
            {
                aiType = AICommandType.AIStartResearch;
                paramJson = JsonUtility.ToJson(new AIStartResearchParams
                {
                    researchType = researchCmd.researchType.ToString()
                });
            }
            else if (command is AIEntrenchCommand entrenchCmd)
            {
                aiType = AICommandType.AIEntrench;
                paramJson = JsonUtility.ToJson(new AIEntrenchParams
                {
                    armyID = entrenchCmd.armyID.ToString()
                });
            }
            else if (command is AIUpgradeUnitCommand upgradeCmd)
            {
                aiType = AICommandType.AIUpgradeUnit;
                paramJson = JsonUtility.ToJson(new AIUpgradeUnitParams
                {
                    upgradeType = upgradeCmd.upgradeType.ToString(),
                    buildingID = upgradeCmd.buildingID.ToString()
                });
            }
            else
            {
                DebugLog.Log(string.Format("Unknown AI command type: {0}", command.GetType().Name));
                return null;
            }

            return new AICommandEnvelope
            {
                aiCommandType = (int)aiType,
                commandID = command.Id.ToString(),
                playerID = command.PlayerID.ToString(),
                timestamp = command.Timestamp,
                parametersJson = paramJson
            };
        }

        // ================================================================
        // Reconstruct to BaseEngineCommand
        // ================================================================

        public BaseEngineCommand ToEngineCommand()
        {
            Guid pid;
            if (!Guid.TryParse(playerID, out pid)) return null;

            var cmdType = (AICommandType)aiCommandType;

            switch (cmdType)
            {
                case AICommandType.AIBuild:
                {
                    var p = JsonUtility.FromJson<AIBuildParams>(parametersJson);
                    BuildingType bt;
                    if (!Enum.TryParse<BuildingType>(p.buildingType, out bt)) return null;
                    var coord = new HexCoordinate(p.coordinateQ, p.coordinateR);
                    return new AIBuildCommand(pid, bt, coord, p.rotation);
                }

                case AICommandType.AITrainMilitary:
                {
                    var p = JsonUtility.FromJson<AITrainMilitaryParams>(parametersJson);
                    Guid bID;
                    MilitaryUnitType ut;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    if (!Enum.TryParse<MilitaryUnitType>(p.unitType, out ut)) return null;
                    return new AITrainMilitaryCommand(pid, bID, ut, p.quantity);
                }

                case AICommandType.AITrainVillager:
                {
                    var p = JsonUtility.FromJson<AITrainVillagerParams>(parametersJson);
                    Guid bID;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    return new AITrainVillagerCommand(pid, bID, p.quantity);
                }

                case AICommandType.AIDeployArmy:
                {
                    var p = JsonUtility.FromJson<AIDeployArmyParams>(parametersJson);
                    Guid bID;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    var composition = new Dictionary<MilitaryUnitType, int>();
                    if (p.compositionKeys != null)
                    {
                        for (int i = 0; i < p.compositionKeys.Length; i++)
                        {
                            MilitaryUnitType ut;
                            if (Enum.TryParse<MilitaryUnitType>(p.compositionKeys[i], out ut))
                            {
                                composition[ut] = p.compositionValues[i];
                            }
                        }
                    }
                    return new AIDeployArmyCommand(pid, bID, composition);
                }

                case AICommandType.AIDeployVillagers:
                {
                    var p = JsonUtility.FromJson<AIDeployVillagersParams>(parametersJson);
                    Guid bID;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    return new AIDeployVillagersCommand(pid, bID, p.quantity);
                }

                case AICommandType.AIGather:
                {
                    var p = JsonUtility.FromJson<AIGatherParams>(parametersJson);
                    Guid vgID, rpID;
                    if (!Guid.TryParse(p.villagerGroupID, out vgID)) return null;
                    if (!Guid.TryParse(p.resourcePointID, out rpID)) return null;
                    return new AIGatherCommand(pid, vgID, rpID);
                }

                case AICommandType.AIMove:
                {
                    var p = JsonUtility.FromJson<AIMoveParams>(parametersJson);
                    Guid eID;
                    if (!Guid.TryParse(p.entityID, out eID)) return null;
                    var dest = new HexCoordinate(p.destinationQ, p.destinationR);
                    return new AIMoveCommand(pid, eID, dest, p.isArmy);
                }

                case AICommandType.AIStartResearch:
                {
                    var p = JsonUtility.FromJson<AIStartResearchParams>(parametersJson);
                    ResearchType rt;
                    if (!Enum.TryParse<ResearchType>(p.researchType, out rt)) return null;
                    return new AIStartResearchCommand(pid, rt);
                }

                case AICommandType.AIEntrench:
                {
                    var p = JsonUtility.FromJson<AIEntrenchParams>(parametersJson);
                    Guid aID;
                    if (!Guid.TryParse(p.armyID, out aID)) return null;
                    return new AIEntrenchCommand(pid, aID);
                }

                case AICommandType.AIUpgradeUnit:
                {
                    var p = JsonUtility.FromJson<AIUpgradeUnitParams>(parametersJson);
                    UnitUpgradeType ut;
                    Guid bID;
                    if (!Enum.TryParse<UnitUpgradeType>(p.upgradeType, out ut)) return null;
                    if (!Guid.TryParse(p.buildingID, out bID)) return null;
                    return new AIUpgradeUnitCommand(pid, ut, bID);
                }

                default:
                    DebugLog.Log(string.Format("Unknown AI command type: {0}", cmdType));
                    return null;
            }
        }
    }
}
