using System;
using NUnit.Framework;
using Sporefront.Commands;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Tests
{
    [TestFixture]
    public class CommandValidationTests
    {
        // ================================================================
        // MoveCommand Validation
        // ================================================================

        [Test]
        public void MoveCommand_InvalidDestination_Fails()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();
            var army = CreateArmyWithCommander(state, player, new HexCoordinate(3, 3));

            var cmd = new MoveCommand(player.id, army.id, new HexCoordinate(99, 99), true);
            var result = cmd.Validate(state);
            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.FailureReason.Contains("not a valid coordinate"));
        }

        [Test]
        public void MoveCommand_ArmyNotFound_Fails()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();
            var cmd = new MoveCommand(player.id, Guid.NewGuid(), new HexCoordinate(5, 5), true);
            var result = cmd.Validate(state);
            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.FailureReason.Contains("not found"));
        }

        [Test]
        public void MoveCommand_NotOwnedArmy_Fails()
        {
            var (state, p1, p2) = GameStateFactory.CreateWithTwoPlayers();
            var enemyArmy = CreateArmyWithCommander(state, p2, new HexCoordinate(5, 5));

            var cmd = new MoveCommand(p1.id, enemyArmy.id, new HexCoordinate(6, 6), true);
            var result = cmd.Validate(state);
            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.FailureReason.Contains("not owned"));
        }

        [Test]
        public void MoveCommand_ArmyInCombat_Fails()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();
            var army = CreateArmyWithCommander(state, player, new HexCoordinate(3, 3));
            army.isInCombat = true;

            var cmd = new MoveCommand(player.id, army.id, new HexCoordinate(5, 5), true);
            var result = cmd.Validate(state);
            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.FailureReason.Contains("combat"));
        }

        [Test]
        public void MoveCommand_NoCommander_Fails()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();

            // Create army without commander
            var army = new ArmyData("Test Army", new HexCoordinate(3, 3), player.id);
            army.AddMilitaryUnits(MilitaryUnitType.Swordsman, 5);
            state.AddArmy(army);

            var cmd = new MoveCommand(player.id, army.id, new HexCoordinate(5, 5), true);
            var result = cmd.Validate(state);
            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.FailureReason.Contains("commander"));
        }

        [Test]
        public void MoveCommand_ValidArmy_Succeeds()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();
            var army = CreateArmyWithCommander(state, player, new HexCoordinate(3, 3));

            var cmd = new MoveCommand(player.id, army.id, new HexCoordinate(5, 5), true);
            var result = cmd.Validate(state);
            Assert.IsTrue(result.Succeeded, result.FailureReason);
        }

        [Test]
        public void MoveCommand_ValidVillagerGroup_Succeeds()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();
            var group = new VillagerGroupData("Test Villagers", new HexCoordinate(3, 3), 5, player.id);
            state.AddVillagerGroup(group);

            var cmd = new MoveCommand(player.id, group.id, new HexCoordinate(5, 5), false);
            var result = cmd.Validate(state);
            Assert.IsTrue(result.Succeeded, result.FailureReason);
        }

        // ================================================================
        // BuildCommand Validation
        // ================================================================

        [Test]
        public void BuildCommand_OnOccupiedTile_Fails()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();
            // Place idle villagers
            var villagers = new VillagerGroupData("Builders", new HexCoordinate(2, 2), 3, player.id);
            state.AddVillagerGroup(villagers);

            // CityCenter is at (0,0), try building there
            var cmd = new BuildCommand(player.id, BuildingType.Farm, new HexCoordinate(0, 0));
            var result = cmd.Validate(state);
            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.FailureReason.Contains("already exists"));
        }

        [Test]
        public void BuildCommand_InvalidCoordinate_Fails()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();
            var villagers = new VillagerGroupData("Builders", new HexCoordinate(2, 2), 3, player.id);
            state.AddVillagerGroup(villagers);

            var cmd = new BuildCommand(player.id, BuildingType.Farm, new HexCoordinate(99, 99));
            var result = cmd.Validate(state);
            Assert.IsFalse(result.Succeeded);
        }

        [Test]
        public void BuildCommand_ValidPlacement_Succeeds()
        {
            var (state, player) = GameStateFactory.CreateWithPlayer();
            // Place idle villagers
            var villagers = new VillagerGroupData("Builders", new HexCoordinate(2, 2), 3, player.id);
            state.AddVillagerGroup(villagers);

            var cmd = new BuildCommand(player.id, BuildingType.Farm, new HexCoordinate(3, 3));
            var result = cmd.Validate(state);
            Assert.IsTrue(result.Succeeded, result.FailureReason);
        }

        // ================================================================
        // Helpers
        // ================================================================

        private ArmyData CreateArmyWithCommander(GameState state, PlayerState player, HexCoordinate coord)
        {
            var army = new ArmyData("Test Army", coord, player.id);
            army.AddMilitaryUnits(MilitaryUnitType.Swordsman, 5);

            var commander = new CommanderData("Test Commander", CommanderSpecialty.InfantryAggressive, player.id);
            army.commanderID = commander.id;
            state.AddCommander(commander);

            // Home base: use existing city center
            foreach (var b in state.GetBuildingsForPlayer(player.id))
            {
                if (b.buildingType == BuildingType.CityCenter)
                {
                    army.homeBaseID = b.id;
                    break;
                }
            }

            state.AddArmy(army);
            return army;
        }
    }
}
