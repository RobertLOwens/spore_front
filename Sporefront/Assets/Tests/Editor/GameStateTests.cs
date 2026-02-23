using System;
using NUnit.Framework;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Tests
{
    [TestFixture]
    public class GameStateTests
    {
        // ================================================================
        // Player Management
        // ================================================================

        [Test]
        public void AddPlayer_PlayerIsRetrievable()
        {
            var state = GameStateFactory.CreateMinimal();
            var player = new PlayerState("Alice", "3A5E8B");
            state.AddPlayer(player);

            Assert.IsNotNull(state.GetPlayer(player.id));
            Assert.AreEqual("Alice", state.GetPlayer(player.id).name);
        }

        [Test]
        public void RemovePlayer_PlayerIsGone()
        {
            var state = GameStateFactory.CreateMinimal();
            var player = new PlayerState("Alice", "3A5E8B");
            state.AddPlayer(player);
            state.RemovePlayer(player.id);

            Assert.IsNull(state.GetPlayer(player.id));
        }

        [Test]
        public void GetPlayer_NonexistentID_ReturnsNull()
        {
            var state = GameStateFactory.CreateMinimal();
            Assert.IsNull(state.GetPlayer(Guid.NewGuid()));
        }

        // ================================================================
        // Building Management
        // ================================================================

        [Test]
        public void AddBuilding_BuildingIsRetrievable()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();
            var building = new BuildingData(BuildingType.Farm, new HexCoordinate(3, 3), player.id);
            state.AddBuilding(building);

            Assert.IsNotNull(state.GetBuilding(building.id));
            Assert.AreEqual(BuildingType.Farm, state.GetBuilding(building.id).buildingType);
        }

        [Test]
        public void AddBuilding_RegistersWithPlayer()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();
            var building = new BuildingData(BuildingType.Barracks, new HexCoordinate(3, 3), player.id);
            state.AddBuilding(building);

            Assert.IsTrue(player.ownedBuildingIDs.Contains(building.id));
        }

        [Test]
        public void RemoveBuilding_BuildingIsGone()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();
            var building = new BuildingData(BuildingType.Farm, new HexCoordinate(3, 3), player.id);
            state.AddBuilding(building);
            state.RemoveBuilding(building.id);

            Assert.IsNull(state.GetBuilding(building.id));
            Assert.IsFalse(player.ownedBuildingIDs.Contains(building.id));
        }

        // ================================================================
        // Army Management
        // ================================================================

        [Test]
        public void AddArmy_ArmyIsRetrievable()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();
            var army = new ArmyData("Alpha", new HexCoordinate(5, 5), player.id);
            state.AddArmy(army);

            Assert.IsNotNull(state.GetArmy(army.id));
        }

        [Test]
        public void RemoveArmy_ArmyIsGone()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();
            var army = new ArmyData("Alpha", new HexCoordinate(5, 5), player.id);
            state.AddArmy(army);
            state.RemoveArmy(army.id);

            Assert.IsNull(state.GetArmy(army.id));
            Assert.IsFalse(player.ownedArmyIDs.Contains(army.id));
        }

        [Test]
        public void GetArmies_ReturnsByCoordinate()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();
            var coord = new HexCoordinate(5, 5);
            var army1 = new ArmyData("A1", coord, player.id);
            var army2 = new ArmyData("A2", coord, player.id);
            state.AddArmy(army1);
            state.AddArmy(army2);

            var armies = state.GetArmies(coord);
            Assert.AreEqual(2, armies.Count);
        }

        // ================================================================
        // Population Stats
        // ================================================================

        [Test]
        public void GetPopulationStats_CountsVillagers()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();
            var group = new VillagerGroupData("V1", new HexCoordinate(3, 3), 5, player.id);
            state.AddVillagerGroup(group);

            int current, capacity;
            state.GetPopulationStats(player.id, out current, out capacity);
            Assert.AreEqual(5, current);
        }

        [Test]
        public void GetPopulationStats_CapacityFromBuildings()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();
            // CityCenter is already placed, check it provides capacity
            int current, capacity;
            state.GetPopulationStats(player.id, out current, out capacity);
            Assert.Greater(capacity, 0, "CityCenter should provide population capacity");
        }
    }
}
