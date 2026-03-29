// ============================================================================
// FILE: Data/ScoutData.cs
// PURPOSE: Mycelium Scout — fast, fragile scouting entity with extended vision
//          and stamina. Trained from City Center, player starts with one.
// ============================================================================

using System;
using System.Collections.Generic;
using Sporefront.Models;
using Sporefront.Engine;

namespace Sporefront.Data
{
    [System.Serializable]
    public class ScoutData
    {
        public Guid id;
        public Guid? ownerID;
        public HexCoordinate coordinate;

        // Movement
        public List<HexCoordinate> currentPath;
        public int pathIndex;
        public double movementProgress;
        [System.NonSerialized] public double movementSpeed;

        // Stats
        public double hp;
        public double maxHp;
        public double stamina;
        public double maxStamina;
        public int visionRange;

        public ScoutData(HexCoordinate coordinate, Guid? ownerID = null)
        {
            this.id = Guid.NewGuid();
            this.ownerID = ownerID;
            this.coordinate = coordinate;
            this.currentPath = new List<HexCoordinate>();
            this.pathIndex = 0;
            this.movementProgress = 0.0;
            this.hp = GameConfig.Scout.MaxHp;
            this.maxHp = GameConfig.Scout.MaxHp;
            this.stamina = GameConfig.Scout.MaxStamina;
            this.maxStamina = GameConfig.Scout.MaxStamina;
            this.visionRange = GameConfig.Scout.VisionRange;
        }

        // ================================================================
        // Path Management
        // ================================================================

        public void SetPath(List<HexCoordinate> path)
        {
            currentPath = path ?? new List<HexCoordinate>();
            pathIndex = 0;
            movementProgress = 0.0;
        }

        public void ClearPath()
        {
            currentPath.Clear();
            pathIndex = 0;
            movementProgress = 0.0;
        }

        public bool HasPath()
        {
            return currentPath != null && pathIndex < currentPath.Count;
        }

        // ================================================================
        // Stamina
        // ================================================================

        public bool HasEnoughStamina()
        {
            return stamina >= GameConfig.Scout.StaminaCostPerTile;
        }

        public void ConsumeStamina(double amount)
        {
            stamina = Math.Max(0, stamina - amount);
        }

        public void RegenerateStamina(double amount)
        {
            stamina = Math.Min(maxStamina, stamina + amount);
        }
    }
}
