using System;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Tests
{
    /// <summary>
    /// Creates minimal valid GameState instances for unit testing.
    /// All maps are filled with Plains tiles so IsWalkable returns true.
    /// </summary>
    public static class GameStateFactory
    {
        public static GameState CreateMinimal(int width = 10, int height = 10)
        {
            var state = new GameState(width, height);
            FillPlains(state, width, height);
            return state;
        }

        public static (GameState state, PlayerState player) CreateWithPlayer(int width = 10, int height = 10)
        {
            var state = CreateMinimal(width, height);
            var player = new PlayerState("TestPlayer", "3A5E8B", false);
            state.AddPlayer(player);
            state.localPlayerID = player.id;

            // Place city center so CC level requirements pass
            var cc = new BuildingData(BuildingType.CityCenter, new HexCoordinate(0, 0), player.id);
            cc.state = BuildingState.Completed;
            cc.health = cc.maxHealth;
            state.AddBuilding(cc);

            return (state, player);
        }

        public static (GameState state, PlayerState player1, PlayerState player2) CreateWithTwoPlayers(
            int width = 10, int height = 10)
        {
            var state = CreateMinimal(width, height);

            var player1 = new PlayerState("Player 1", "3A5E8B", false);
            var player2 = new PlayerState("Player 2", "8B3A3A", true);
            state.AddPlayer(player1);
            state.AddPlayer(player2);
            state.localPlayerID = player1.id;

            // Set diplomacy
            player1.SetDiplomacyStatus(player2.id, DiplomacyStatus.Enemy);
            player2.SetDiplomacyStatus(player1.id, DiplomacyStatus.Enemy);

            // City centers
            var cc1 = new BuildingData(BuildingType.CityCenter, new HexCoordinate(0, 0), player1.id);
            cc1.state = BuildingState.Completed;
            cc1.health = cc1.maxHealth;
            state.AddBuilding(cc1);

            var cc2 = new BuildingData(BuildingType.CityCenter, new HexCoordinate(9, 9), player2.id);
            cc2.state = BuildingState.Completed;
            cc2.health = cc2.maxHealth;
            state.AddBuilding(cc2);

            return (state, player1, player2);
        }

        private static void FillPlains(GameState state, int width, int height)
        {
            for (int r = 0; r < height; r++)
            {
                for (int q = 0; q < width; q++)
                {
                    state.mapData.SetTile(new TileData(new HexCoordinate(q, r), TerrainType.Plains, 0));
                }
            }
        }
    }
}
