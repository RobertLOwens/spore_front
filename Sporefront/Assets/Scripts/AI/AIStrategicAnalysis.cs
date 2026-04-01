// ============================================================================
// FILE: AI/AIStrategicAnalysis.cs
// PURPOSE: Static utility class providing map analysis — chokepoint detection,
//          map strategy classification, and faction counter-strategies.
//          Consumed by all AI planners for smarter decision-making.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Sporefront.Data;
using Sporefront.Models;
using Sporefront.Engine;

namespace Sporefront.AI
{
    // ================================================================
    // Map Strategy
    // ================================================================

    public enum MapStrategy
    {
        Highland,   // >40% mountain/hill — favor defensive play, ranged units
        Woodland,   // >40% forest tiles — favor infantry, ambush positioning
        Open,       // >30% plains/desert — favor cavalry, aggressive expansion
        Balanced    // Mixed terrain
    }

    // ================================================================
    // Chokepoint Data
    // ================================================================

    [System.Serializable]
    public struct ChokepointData
    {
        public HexCoordinate center;
        public int width;               // Number of tiles in the chokepoint cluster
        public HexCoordinate direction;  // General direction the chokepoint faces

        public ChokepointData(HexCoordinate center, int width, HexCoordinate direction)
        {
            this.center = center;
            this.width = width;
            this.direction = direction;
        }
    }

    // ================================================================
    // Strategic Analysis
    // ================================================================

    public static class AIStrategicAnalysis
    {
        // ================================================================
        // Chokepoint Detection
        // ================================================================

        /// <summary>
        /// Analyzes the map between the AI's city center and the map center to find
        /// chokepoints — narrow passages where few walkable tiles connect regions.
        /// Tiles with ≤2 walkable neighbors are candidates; adjacent candidates are
        /// clustered into chokepoint groups.
        /// </summary>
        public static List<ChokepointData> AnalyzeChokepoints(GameState gameState, Guid playerID)
        {
            var chokepoints = new List<ChokepointData>();
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return chokepoints;

            var mapData = gameState.mapData;
            var ccCoord = cityCenter.coordinate;
            var mapCenter = new HexCoordinate(mapData.width / 2, mapData.height / 2);

            // Scan a band of tiles between CC and map center (±4 tiles perpendicular)
            int scanRadius = Math.Max(ccCoord.Distance(mapCenter), 10);
            var lineTiles = ccCoord.LineTo(mapCenter);

            // Collect all tiles within 4 hexes of the CC-to-center line
            var scanArea = new HashSet<HexCoordinate>();
            foreach (var tile in lineTiles)
            {
                var nearby = tile.CoordinatesWithinRange(4);
                foreach (var coord in nearby)
                {
                    if (mapData.IsValidCoordinate(coord) && mapData.IsWalkable(coord))
                        scanArea.Add(coord);
                }
            }

            // Find chokepoint candidates: walkable tiles with ≤2 walkable neighbors
            var candidates = new HashSet<HexCoordinate>();
            foreach (var coord in scanArea)
            {
                int walkableNeighbors = 0;
                foreach (var neighbor in coord.Neighbors())
                {
                    if (mapData.IsValidCoordinate(neighbor) && mapData.IsWalkable(neighbor))
                        walkableNeighbors++;
                }

                if (walkableNeighbors <= 2)
                    candidates.Add(coord);
            }

            // Cluster adjacent candidates into chokepoint groups
            var visited = new HashSet<HexCoordinate>();
            foreach (var candidate in candidates)
            {
                if (visited.Contains(candidate)) continue;

                var cluster = new List<HexCoordinate>();
                var queue = new Queue<HexCoordinate>();
                queue.Enqueue(candidate);
                visited.Add(candidate);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    cluster.Add(current);

                    foreach (var neighbor in current.Neighbors())
                    {
                        if (candidates.Contains(neighbor) && !visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                // Only consider clusters of 1-6 tiles as chokepoints
                // (larger clusters are open areas, not chokepoints)
                if (cluster.Count >= 1 && cluster.Count <= 6)
                {
                    // Center of cluster
                    int sumQ = 0, sumR = 0;
                    foreach (var c in cluster) { sumQ += c.q; sumR += c.r; }
                    var center = new HexCoordinate(sumQ / cluster.Count, sumR / cluster.Count);

                    // Snap center to nearest actual cluster tile
                    var nearestCluster = cluster.OrderBy(c => c.Distance(center)).First();

                    // Direction: from CC toward the chokepoint
                    chokepoints.Add(new ChokepointData(nearestCluster, cluster.Count, mapCenter));
                }
            }

            // Sort by distance from CC (closest first — most relevant for defense)
            chokepoints.Sort((a, b) => a.center.Distance(ccCoord).CompareTo(b.center.Distance(ccCoord)));

            // Limit to the 5 most relevant chokepoints
            if (chokepoints.Count > 5)
                chokepoints = chokepoints.GetRange(0, 5);

            return chokepoints;
        }

        // ================================================================
        // Map Strategy Classification
        // ================================================================

        /// <summary>
        /// Examines terrain distribution around the AI's city center to determine
        /// the best overall strategy. Considers terrain types within 10 tiles and
        /// adjusts for game mode (zone-based modes favor aggression).
        /// </summary>
        public static MapStrategy DetermineMapStrategy(GameState gameState, Guid playerID)
        {
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null) return MapStrategy.Balanced;

            var mapData = gameState.mapData;
            var ccCoord = cityCenter.coordinate;

            // Count terrain types within 10 tiles of CC
            int totalTiles = 0;
            int highlandTiles = 0;  // Mountain + Hill
            int forestTiles = 0;    // Tiles with tree resource points
            int openTiles = 0;      // Plains + Desert

            var tilesInRange = ccCoord.CoordinatesWithinRange(10);
            foreach (var coord in tilesInRange)
            {
                if (!mapData.IsValidCoordinate(coord)) continue;
                var terrain = mapData.GetTerrain(coord);
                if (!terrain.HasValue) continue;

                totalTiles++;

                switch (terrain.Value)
                {
                    case TerrainType.Mountain:
                    case TerrainType.Hill:
                        highlandTiles++;
                        break;
                    case TerrainType.Plains:
                    case TerrainType.Desert:
                        openTiles++;
                        break;
                }

                // Check for forest (tree resource points indicate forest terrain)
                var rp = gameState.GetResourcePoint(coord);
                if (rp != null && rp.resourceType == ResourcePointType.Trees)
                    forestTiles++;
            }

            if (totalTiles == 0) return MapStrategy.Balanced;

            double highlandRatio = (double)highlandTiles / totalTiles;
            double forestRatio = (double)forestTiles / totalTiles;
            double openRatio = (double)openTiles / totalTiles;

            // Classify based on dominant terrain
            if (highlandRatio > 0.40) return MapStrategy.Highland;
            if (forestRatio > 0.40) return MapStrategy.Woodland;
            if (openRatio > 0.30 && highlandRatio < 0.15 && forestRatio < 0.15)
                return MapStrategy.Open;

            return MapStrategy.Balanced;
        }

        // ================================================================
        // Chokepoint Proximity Check (for defense planner)
        // ================================================================

        /// <summary>
        /// Returns the bonus score a tile should receive for being near a chokepoint.
        /// Tiles within 2 hexes of a chokepoint center get +30, within 1 hex get +40.
        /// Returns 0 if not near any chokepoint.
        /// </summary>
        public static double GetChokepointBonus(HexCoordinate tile, List<ChokepointData> chokepoints)
        {
            if (chokepoints == null || chokepoints.Count == 0) return 0;

            double bestBonus = 0;
            foreach (var choke in chokepoints)
            {
                int dist = tile.Distance(choke.center);
                if (dist <= 1)
                    bestBonus = Math.Max(bestBonus, 40.0);
                else if (dist <= 2)
                    bestBonus = Math.Max(bestBonus, 30.0);
            }

            return bestBonus;
        }

        // ================================================================
        // Faction Counter-Strategy
        // ================================================================

        /// <summary>
        /// Returns strategic adjustments for countering a specific enemy faction.
        /// These adjustments modify training priorities, aggression, and combat terrain preference.
        /// </summary>
        public static FactionStrategy GetFactionCounterStrategy(FactionType enemyFaction)
        {
            switch (enemyFaction)
            {
                case FactionType.Morel:
                    // Morel: infantry/stealth specialists — counter with ranged + cavalry
                    return new FactionStrategy
                    {
                        infantryAdjustment = -0.20,
                        rangedAdjustment = 0.15,
                        cavalryAdjustment = 0.15,
                        siegeAdjustment = -0.10,
                        aggressionModifier = 0.1, // Slightly more aggressive — flush them from forests
                        alertRadiusBonus = 2,     // Morel has extended vision, widen our alert
                        preferOpenTerrain = true,  // Avoid fighting in forests where they have camouflage
                        description = "vs Morel: ranged+cavalry, avoid forests, wider alert radius"
                    };

                case FactionType.Muscaria:
                    // Muscaria: poison/mountain specialists — counter with infantry + siege, avoid uphill
                    return new FactionStrategy
                    {
                        infantryAdjustment = 0.10,
                        rangedAdjustment = -0.05,
                        cavalryAdjustment = -0.15,  // Cavalry takes more poison damage (fewer units)
                        siegeAdjustment = 0.10,
                        aggressionModifier = -0.15, // Play more defensively — avoid mountain engagement
                        alertRadiusBonus = 0,
                        preferOpenTerrain = true,    // Avoid mountain/hill where they get combat bonuses
                        description = "vs Muscaria: infantry+siege, avoid mountains, play defensive"
                    };

                default:
                    return FactionStrategy.Neutral;
            }
        }

        // ================================================================
        // Strategy Description (for debug logging)
        // ================================================================

        public static string DescribeStrategy(MapStrategy strategy)
        {
            switch (strategy)
            {
                case MapStrategy.Highland: return "Highland (defensive, favor ranged)";
                case MapStrategy.Woodland: return "Woodland (infantry, ambush)";
                case MapStrategy.Open: return "Open (cavalry, aggressive)";
                case MapStrategy.Balanced: return "Balanced (mixed)";
                default: return strategy.ToString();
            }
        }

        // ================================================================
        // Feature 2: Enemy Strategy Reading (Greed Detection)
        // ================================================================

        /// <summary>
        /// Analyzes visible enemy buildings and army positions to determine the enemy's
        /// current strategy: Greedy (all economy, no military), Turtle (heavy defense),
        /// Passive (military but not attacking), or Balanced.
        /// </summary>
        public static EnemyStrategyRead AnalyzeEnemyStrategy(GameState gameState, Guid playerID)
        {
            var visibleBuildings = gameState.GetVisibleEnemyBuildings(playerID);
            if (visibleBuildings.Count == 0) return EnemyStrategyRead.Unknown;

            int economicBuildings = 0;  // Farm, LumberCamp, MiningCamp
            int militaryBuildings = 0;  // Barracks, ArcheryRange, Stable, SiegeWorkshop
            int defensiveBuildings = 0; // Tower, Fort, Castle
            HexCoordinate? enemyCCCoord = null;

            foreach (var building in visibleBuildings)
            {
                switch (building.buildingType)
                {
                    case BuildingType.Farm:
                    case BuildingType.LumberCamp:
                    case BuildingType.MiningCamp:
                        economicBuildings++;
                        break;
                    case BuildingType.Barracks:
                    case BuildingType.ArcheryRange:
                    case BuildingType.Stable:
                    case BuildingType.SiegeWorkshop:
                        militaryBuildings++;
                        break;
                    case BuildingType.Tower:
                    case BuildingType.WoodenFort:
                    case BuildingType.Castle:
                        defensiveBuildings++;
                        break;
                    case BuildingType.CityCenter:
                        enemyCCCoord = building.coordinate;
                        break;
                }
            }

            // Greedy: lots of economy, no military production visible
            if (economicBuildings >= 4 && militaryBuildings == 0)
                return EnemyStrategyRead.Greedy;

            // Turtle: multiple defensive structures
            if (defensiveBuildings >= 2)
                return EnemyStrategyRead.Turtle;

            // Passive: has military buildings but all visible enemy armies near their CC
            if (militaryBuildings > 0 && enemyCCCoord.HasValue)
            {
                bool allNearCC = true;
                bool hasVisibleArmies = false;
                foreach (var army in gameState.armies.Values)
                {
                    if (!army.ownerID.HasValue || army.ownerID.Value == playerID) continue;
                    var status = gameState.GetDiplomacyStatus(playerID, army.ownerID.Value);
                    if (status != DiplomacyStatus.Enemy) continue;

                    // Only consider armies the AI can see
                    var player = gameState.GetPlayer(playerID);
                    if (player == null || !player.IsVisible(army.coordinate)) continue;

                    hasVisibleArmies = true;
                    if (army.coordinate.Distance(enemyCCCoord.Value) > 5)
                    {
                        allNearCC = false;
                        break;
                    }
                }

                if (hasVisibleArmies && allNearCC)
                    return EnemyStrategyRead.Passive;
            }

            return EnemyStrategyRead.Balanced;
        }

        // ================================================================
        // Feature 4: Game Position Assessment (Comeback Mechanics)
        // ================================================================

        /// <summary>
        /// Compares the AI's current position against the enemy to determine if
        /// we're winning, even, behind, or critically behind. Used to adapt
        /// strategy — turtling when losing, aggression when winning.
        /// </summary>
        public static GamePosition AssessGamePosition(GameState gameState, Guid playerID)
        {
            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null)
                return GamePosition.CriticallyBehind; // Lost CC = critical

            double ourStrength = gameState.GetWeightedMilitaryStrength(playerID);
            int ourBuildingCount = gameState.GetBuildingsForPlayer(playerID).Count;

            // Analyze enemy strength and building count
            double enemyStrength = 0;
            int enemyBuildingCount = 0;

            // Use visible enemy buildings for building count
            var visibleEnemyBuildings = gameState.GetVisibleEnemyBuildings(playerID);
            enemyBuildingCount = visibleEnemyBuildings.Count;

            // Use enemy composition analysis for strength
            var enemyAnalysis = gameState.AnalyzeEnemyComposition(playerID);
            if (enemyAnalysis.HasValue)
                enemyStrength = enemyAnalysis.Value.weightedStrength;

            // If we can't see the enemy at all, assume even
            if (enemyBuildingCount == 0 && enemyStrength == 0)
                return GamePosition.Even;

            // Compare military strength
            double strengthRatio = enemyStrength > 0 ? ourStrength / enemyStrength : 2.0;

            // Compare building counts (our total vs what we can see of enemy)
            int buildingDelta = ourBuildingCount - enemyBuildingCount;

            // CriticallyBehind: enemy is overwhelmingly stronger
            if (strengthRatio < 0.4 || (strengthRatio < 0.6 && buildingDelta < -3))
                return GamePosition.CriticallyBehind;

            // Behind: enemy is significantly stronger or has many more buildings
            if (strengthRatio < 0.67 || buildingDelta < -3)
                return GamePosition.Behind;

            // Winning: we're significantly stronger and have more buildings
            if (strengthRatio > 1.5 && buildingDelta >= 0)
                return GamePosition.Winning;

            return GamePosition.Even;
        }
        // ================================================================
        // Feature 5: Counter-Composition Generation
        // ================================================================

        /// <summary>
        /// Generates a TargetComposition that specifically counters the observed enemy composition.
        /// Used to dynamically adjust training priorities based on what the enemy is building.
        /// </summary>
        public static TargetComposition GenerateCounterComposition(EnemyCompositionAnalysis enemy, MapStrategy strategy, AIState state)
        {
            // Start from base composition
            var baseComp = TargetComposition.ForStrategy(strategy, state);
            double inf = baseComp.infantry;
            double rng = baseComp.ranged;
            double cav = baseComp.cavalry;
            double sig = baseComp.siege;

            // Counter adjustments based on enemy composition
            if (enemy.cavalryRatio > 0.5)
            {
                // Heavy cavalry → need lots of infantry (pikemen)
                inf = Math.Max(inf, 0.50);
                cav = Math.Max(0.05, cav - 0.15);
            }
            else if (enemy.cavalryRatio > 0.3)
            {
                inf += 0.10;
                cav = Math.Max(0.05, cav - 0.05);
            }

            if (enemy.rangedRatio > 0.5)
            {
                // Heavy ranged → need cavalry to rush them
                cav = Math.Max(cav, 0.40);
                rng = Math.Max(0.05, rng - 0.10);
            }
            else if (enemy.rangedRatio > 0.3)
            {
                cav += 0.10;
            }

            if (enemy.infantryRatio > 0.5)
            {
                // Heavy infantry → need ranged to pick them off
                rng = Math.Max(rng, 0.40);
                inf = Math.Max(0.05, inf - 0.10);
            }
            else if (enemy.infantryRatio > 0.3)
            {
                rng += 0.10;
            }

            if (enemy.siegeRatio > 0.2)
            {
                // Has siege → boost cavalry to rush siege units
                cav += 0.10;
                sig = Math.Max(0.05, sig - 0.05);
            }

            // Normalize to 1.0
            double total = inf + rng + cav + sig;
            if (total > 0)
            {
                inf /= total;
                rng /= total;
                cav /= total;
                sig /= total;
            }

            return new TargetComposition(inf, rng, cav, sig);
        }

        // ================================================================
        // Feature 7: Expansion Site Scoring
        // ================================================================

        /// <summary>
        /// Scores a potential expansion site for a second City Center.
        /// Considers nearby resources, distance from main CC, distance from enemy, and defensibility.
        /// </summary>
        public static double ScoreExpansionSite(HexCoordinate site, GameState gameState, Guid playerID,
            HexCoordinate ccCoord, List<HexCoordinate> knownEnemyBases, List<ChokepointData> chokepoints)
        {
            double score = 0;

            // Nearby resource nodes within 3 tiles
            int resourceCount = 0;
            var nearby = site.CoordinatesWithinRange(3);
            foreach (var coord in nearby)
            {
                var rp = gameState.GetResourcePoint(coord);
                if (rp != null && rp.remainingAmount > 0 && rp.resourceType.IsGatherable())
                    resourceCount++;
            }
            score += resourceCount * 10.0; // Each nearby resource is valuable

            // Distance from main CC (want 8-15 range, penalize closer or farther)
            int distFromCC = site.Distance(ccCoord);
            if (distFromCC < GameConfig.AI.Expansion.MinDistFromCC) score -= 20.0;
            else if (distFromCC > GameConfig.AI.Expansion.MaxDistFromCC) score -= 15.0;
            else score += 10.0; // Good range

            // Distance from enemy (farther = safer)
            if (knownEnemyBases != null && knownEnemyBases.Count > 0)
            {
                int minEnemyDist = int.MaxValue;
                foreach (var enemyBase in knownEnemyBases)
                {
                    int d = site.Distance(enemyBase);
                    if (d < minEnemyDist) minEnemyDist = d;
                }
                score += Math.Min(15.0, minEnemyDist * 1.5); // Farther from enemy = better, capped
            }

            // Near chokepoint = defensible
            if (chokepoints != null)
            {
                double chokeBonus = GetChokepointBonus(site, chokepoints);
                score += chokeBonus * 0.5; // Half the normal bonus
            }

            // Terrain: prefer walkable, non-mountain
            if (!gameState.mapData.IsWalkable(site)) score -= 100.0;
            var terrain = gameState.mapData.GetTerrain(site);
            if (terrain.HasValue && terrain.Value == TerrainType.Mountain) score -= 10.0; // Higher build cost

            return score;
        }

        // ================================================================
        // Feature 3: Contested Resource Identification
        // ================================================================

        /// <summary>
        /// Identifies resource points in the contested zone between AI and enemy bases.
        /// Resources roughly equidistant between the two CCs are contested territory.
        /// </summary>
        public static List<(HexCoordinate coord, double score)> IdentifyContestedResources(
            GameState gameState, Guid playerID, List<HexCoordinate> knownEnemyBases)
        {
            var results = new List<(HexCoordinate coord, double score)>();

            var cityCenter = gameState.GetCityCenter(playerID);
            if (cityCenter == null || knownEnemyBases.Count == 0) return results;

            var ccCoord = cityCenter.coordinate;
            var enemyCC = knownEnemyBases[0];
            int baseDist = ccCoord.Distance(enemyCC);
            if (baseDist < 6) return results; // Too close, everything is contested

            double contestRatio = GameConfig.AI.MapControl.ContestDistanceRatio;

            // Check all explored resource points
            var player = gameState.GetPlayer(playerID);
            if (player == null) return results;

            // Pre-collect our camp buildings for coverage checks
            var ourCamps = gameState.GetBuildingsForPlayer(playerID)
                .Where(b => (b.buildingType == BuildingType.LumberCamp || b.buildingType == BuildingType.MiningCamp))
                .ToList();

            var exploredResources = gameState.GetExploredResourcePoints(playerID);
            foreach (var resource in exploredResources)
            {
                if (resource.remainingAmount <= 0 || !resource.resourceType.IsGatherable()) continue;

                int distToUs = resource.coordinate.Distance(ccCoord);
                int distToEnemy = resource.coordinate.Distance(enemyCC);

                // "Contested" = roughly equidistant (within contestRatio band)
                double ratio = distToEnemy > 0 ? (double)distToUs / distToEnemy : 2.0;
                if (ratio < contestRatio || ratio > (1.0 / contestRatio)) continue;

                // Check if we already have a camp covering this resource
                bool alreadyCovered = false;
                foreach (var camp in ourCamps)
                {
                    if (camp.coordinate.Distance(resource.coordinate) <= 3)
                    {
                        alreadyCovered = true;
                        break;
                    }
                }
                if (alreadyCovered) continue;

                double score = 30.0;
                // Closer to us = better
                score += (baseDist - distToUs) * 2.0;
                // Farther from enemy = safer
                score += distToEnemy * 1.0;

                results.Add((resource.coordinate, score));
            }

            results.Sort((a, b) => b.score.CompareTo(a.score));
            return results;
        }
    }

    // ================================================================
    // Faction Strategy
    // ================================================================

    public struct FactionStrategy
    {
        public double infantryAdjustment;
        public double rangedAdjustment;
        public double cavalryAdjustment;
        public double siegeAdjustment;
        public double aggressionModifier;  // -0.2 to +0.2 — affects attack thresholds
        public int alertRadiusBonus;       // Extra tiles for alert detection
        public bool preferOpenTerrain;     // Avoid forest/mountain engagement
        public string description;

        /// <summary>
        /// Neutral strategy — no adjustments.
        /// </summary>
        public static FactionStrategy Neutral => new FactionStrategy
        {
            infantryAdjustment = 0, rangedAdjustment = 0,
            cavalryAdjustment = 0, siegeAdjustment = 0,
            aggressionModifier = 0, alertRadiusBonus = 0,
            preferOpenTerrain = false, description = "Neutral (no adaptation)"
        };

        /// <summary>
        /// Applies training adjustments to a target composition.
        /// </summary>
        public TargetComposition AdjustComposition(TargetComposition baseComp)
        {
            double inf = Math.Max(0.05, Math.Min(0.7, baseComp.infantry + infantryAdjustment));
            double rng = Math.Max(0.05, Math.Min(0.7, baseComp.ranged + rangedAdjustment));
            double cav = Math.Max(0.05, Math.Min(0.7, baseComp.cavalry + cavalryAdjustment));
            double sig = Math.Max(0.05, Math.Min(0.7, baseComp.siege + siegeAdjustment));

            // Normalize to 1.0
            double total = inf + rng + cav + sig;
            if (total > 0)
            {
                inf /= total;
                rng /= total;
                cav /= total;
                sig /= total;
            }

            return new TargetComposition(inf, rng, cav, sig);
        }
    }
}
