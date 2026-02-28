using System;
using NUnit.Framework;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.Tests
{
    [TestFixture]
    public class BuildingDataTests
    {
        // ================================================================
        // Health Initialization
        // ================================================================

        [Test]
        public void Constructor_Wall_HasCorrectHealth()
        {
            var b = new BuildingData(BuildingType.Wall, new HexCoordinate(0, 0));
            Assert.AreEqual(GameConfig.BuildingHealth.Wall, b.maxHealth);
            Assert.AreEqual(b.maxHealth, b.health);
        }

        [Test]
        public void Constructor_Gate_HasCorrectHealth()
        {
            var b = new BuildingData(BuildingType.Gate, new HexCoordinate(0, 0));
            Assert.AreEqual(GameConfig.BuildingHealth.Gate, b.maxHealth);
        }

        [Test]
        public void Constructor_MilitaryBuilding_HasCorrectHealth()
        {
            var b = new BuildingData(BuildingType.Barracks, new HexCoordinate(0, 0));
            Assert.AreEqual(GameConfig.BuildingHealth.Military, b.maxHealth);
        }

        [Test]
        public void Constructor_CivilianBuilding_HasCorrectHealth()
        {
            var b = new BuildingData(BuildingType.Farm, new HexCoordinate(0, 0));
            Assert.AreEqual(GameConfig.BuildingHealth.Civilian, b.maxHealth);
        }

        [Test]
        public void GetBaseHealth_MatchesConstructor()
        {
            // Verify the static helper returns the same value the constructor uses
            Assert.AreEqual(GameConfig.BuildingHealth.Wall, BuildingData.GetBaseHealth(BuildingType.Wall));
            Assert.AreEqual(GameConfig.BuildingHealth.Gate, BuildingData.GetBaseHealth(BuildingType.Gate));
            Assert.AreEqual(GameConfig.BuildingHealth.Military, BuildingData.GetBaseHealth(BuildingType.Barracks));
            Assert.AreEqual(GameConfig.BuildingHealth.Civilian, BuildingData.GetBaseHealth(BuildingType.Farm));
        }

        // ================================================================
        // Construction
        // ================================================================

        [Test]
        public void StartConstruction_SetsState()
        {
            var b = new BuildingData(BuildingType.Farm, new HexCoordinate(0, 0));
            b.StartConstruction(0.0, 2);

            Assert.AreEqual(BuildingState.Constructing, b.state);
            Assert.AreEqual(2, b.buildersAssigned);
            Assert.AreEqual(0.0, b.constructionProgress);
        }

        [Test]
        public void UpdateConstruction_ProgressesOverTime()
        {
            var b = new BuildingData(BuildingType.Farm, new HexCoordinate(0, 0));
            b.StartConstruction(0.0, 1);
            b.UpdateConstruction(5.0);

            Assert.Greater(b.constructionProgress, 0.0);
        }

        [Test]
        public void CompleteConstruction_SetsCompleted()
        {
            var b = new BuildingData(BuildingType.Farm, new HexCoordinate(0, 0));
            b.StartConstruction(0.0, 1);
            b.CompleteConstruction();

            Assert.AreEqual(BuildingState.Completed, b.state);
            Assert.AreEqual(1.0, b.constructionProgress);
            Assert.AreEqual(b.maxHealth, b.health);
        }

        // ================================================================
        // Demolition
        // ================================================================

        [Test]
        public void GetDemolitionTime_UsesConfigMultiplier()
        {
            var b = new BuildingData(BuildingType.Farm, new HexCoordinate(0, 0));
            double expected = BuildingType.Farm.BuildTime() * GameConfig.Demolition.TimeMultiplier;
            Assert.AreEqual(expected, b.GetDemolitionTime(), 0.001);
        }

        [Test]
        public void GetDemolitionRefund_UsesConfigMultiplier()
        {
            var b = new BuildingData(BuildingType.Farm, new HexCoordinate(0, 0));
            var refund = b.GetDemolitionRefund();
            var buildCost = BuildingType.Farm.BuildCost();
            foreach (var kvp in refund)
            {
                int expected = (int)(buildCost[kvp.Key] * GameConfig.Demolition.RefundMultiplier);
                Assert.AreEqual(expected, kvp.Value);
            }
        }

        [Test]
        public void StartDemolition_SetsState()
        {
            var b = new BuildingData(BuildingType.Farm, new HexCoordinate(0, 0));
            b.state = BuildingState.Completed;
            b.StartDemolition(0.0, 2);

            Assert.AreEqual(BuildingState.Demolishing, b.state);
            Assert.AreEqual(2, b.demolishersAssigned);
        }

        [Test]
        public void CanDemolish_CityCenter_IsFalse()
        {
            var b = new BuildingData(BuildingType.CityCenter, new HexCoordinate(0, 0));
            b.state = BuildingState.Completed;
            Assert.IsFalse(b.CanDemolish);
        }

        // ================================================================
        // Garrison
        // ================================================================

        [Test]
        public void AddToGarrison_IncreasesCount()
        {
            var b = new BuildingData(BuildingType.Barracks, new HexCoordinate(0, 0));
            b.AddToGarrison(MilitaryUnitType.Swordsman, 10);
            Assert.AreEqual(10, b.GetTotalGarrisonedUnits());
        }

        [Test]
        public void RemoveFromGarrison_DecreasesCount()
        {
            var b = new BuildingData(BuildingType.Barracks, new HexCoordinate(0, 0));
            b.AddToGarrison(MilitaryUnitType.Swordsman, 10);
            int removed = b.RemoveFromGarrison(MilitaryUnitType.Swordsman, 3);
            Assert.AreEqual(3, removed);
            Assert.AreEqual(7, b.GetTotalGarrisonedUnits());
        }

        [Test]
        public void RemoveFromGarrison_ClampsToAvailable()
        {
            var b = new BuildingData(BuildingType.Barracks, new HexCoordinate(0, 0));
            b.AddToGarrison(MilitaryUnitType.Swordsman, 5);
            int removed = b.RemoveFromGarrison(MilitaryUnitType.Swordsman, 100);
            Assert.AreEqual(5, removed);
            Assert.AreEqual(0, b.GetTotalGarrisonedUnits());
        }

        // ================================================================
        // Upgrades
        // ================================================================

        [Test]
        public void ApplyBuildingHPBonus_UsesGetBaseHealth()
        {
            var b = new BuildingData(BuildingType.Tower, new HexCoordinate(0, 0));
            b.level = 2;
            b.health = b.maxHealth;
            b.ApplyBuildingHPBonus();

            double expected = GameConfig.BuildingHealth.Military
                * (1.0 + (2 - 1) * GameConfig.Defense.HPBonusPerLevel);
            Assert.AreEqual(expected, b.maxHealth, 0.01);
        }

        [Test]
        public void CanUpgrade_CompletedBelowMax_IsTrue()
        {
            var b = new BuildingData(BuildingType.CityCenter, new HexCoordinate(0, 0));
            b.state = BuildingState.Completed;
            b.level = 1;
            Assert.IsTrue(b.CanUpgrade);
        }
    }
}
