// ============================================================================
// FILE: Visual/EntityListPanel.cs
// PURPOSE: Left-side panel listing entities on the selected tile (#14)
//          Quick-action buttons per entity type
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;
using Sporefront.Commands;

namespace Sporefront.Visual
{
    public class EntityListPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<Guid> OnBuildingDetailRequested;
        public event Action<Guid> OnArmyDetailRequested;
        public event Action<Guid> OnMoveArmyRequested;
        public event Action<Guid> OnAttackRequested;
        public event Action<Guid> OnMoveVillagerRequested;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private RectTransform contentRT;
        private HexCoordinate? currentCoord;
        private Guid localPlayerID;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Left-anchored panel
            panel = UIHelper.CreatePanel(canvasTransform, "EntityListPanel", UIHelper.PanelBg);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.offsetMin = new Vector2(0, 50);
            rt.offsetMax = new Vector2(UIConstants.SidePanelWidth, -70);

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "EntityScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);

            panel.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show(HexCoordinate coord, GameState gameState, Guid playerID)
        {
            currentCoord = coord;
            localPlayerID = playerID;
            Rebuild(gameState);

            // Only show if there are entities
            bool hasEntities = contentRT.childCount > 0;
            panel.SetActive(hasEntities);
        }

        public void Hide()
        {
            currentCoord = null;
            panel.SetActive(false);
        }

        public void Refresh(GameState gameState)
        {
            if (!currentCoord.HasValue || !panel.activeSelf) return;
            Rebuild(gameState);
        }

        public bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild(GameState gameState)
        {
            if (!currentCoord.HasValue) return;
            var coord = currentCoord.Value;

            // Clear
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            // Header
            var header = UIHelper.CreateLabel(contentRT, "Entities",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 28;

            // Building
            var building = gameState.GetBuilding(coord);
            if (building != null)
                BuildBuildingRow(building);

            // Armies
            var armies = gameState.GetArmies(coord);
            if (armies != null)
            {
                foreach (var army in armies)
                    BuildArmyRow(army, gameState);
            }

            // Villager groups
            var villagers = gameState.GetVillagerGroups(coord);
            if (villagers != null)
            {
                foreach (var vg in villagers)
                    BuildVillagerRow(vg);
            }
        }

        // ================================================================
        // Entity Rows
        // ================================================================

        private void BuildBuildingRow(BuildingData building)
        {
            bool isOwned = building.ownerID.HasValue && building.ownerID.Value == localPlayerID;

            UIHelper.CreateDivider(contentRT);

            var row = UIHelper.CreateHorizontalRow(contentRT, 28f, 4f);

            var nameLabel = UIHelper.CreateLabel(row.transform,
                $"{building.buildingType.DisplayName()} Lv.{building.level}", 12);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;
            nameLE.preferredHeight = 28;

            if (isOwned && building.IsOperational)
            {
                var openBtn = CreateSmallButton(row.transform, "Open",
                    () => OnBuildingDetailRequested?.Invoke(building.id));
                // Garrison button for military buildings
                if (building.GetGarrisonCapacity() > 0)
                {
                    // Garrison info shown in building detail
                }
            }
        }

        private void BuildArmyRow(ArmyData army, GameState gameState)
        {
            bool isOwned = army.ownerID.HasValue && army.ownerID.Value == localPlayerID;

            UIHelper.CreateDivider(contentRT);

            // Name row
            var nameRow = UIHelper.CreateHorizontalRow(contentRT, 24f, 4f);
            string status = UIHelper.FormatArmyStatus(army);
            var nameLabel = UIHelper.CreateLabel(nameRow.transform,
                $"{army.name}{status}", 12);
            nameLabel.supportRichText = true;
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            // Unit count
            var countLabel = UIHelper.CreateLabel(nameRow.transform,
                $"{army.GetTotalUnits()} units", 11, SporefrontColors.InkLight);
            var countLE = countLabel.gameObject.AddComponent<LayoutElement>();
            countLE.preferredWidth = 60;

            if (!isOwned) return;

            // Action buttons row
            var btnRow = UIHelper.CreateHorizontalRow(contentRT, 28f, 3f);

            // Entrench
            if (!army.isEntrenched && !army.isEntrenching && !army.isInCombat)
            {
                CreateSmallButton(btnRow.transform, "Dig In", () =>
                {
                    var cmd = new EntrenchCommand(localPlayerID, army.id);
                    GameEngine.Instance.ExecuteCommand(cmd);
                });
            }

            // Retreat
            if (army.isInCombat)
            {
                CreateSmallButton(btnRow.transform, "Retreat", () =>
                {
                    var cmd = new RetreatCommand(localPlayerID, army.id);
                    GameEngine.Instance.ExecuteCommand(cmd);
                });
            }

            // Move
            if (!army.isInCombat)
            {
                CreateSmallButton(btnRow.transform, "Move", () =>
                    OnMoveArmyRequested?.Invoke(army.id));
            }

            // Attack (if not in combat and enemies nearby)
            if (!army.isInCombat && !army.isRetreating)
            {
                CreateSmallButton(btnRow.transform, "Attack", () =>
                    OnAttackRequested?.Invoke(army.id));
            }

            // Info
            CreateSmallButton(btnRow.transform, "Info", () =>
                OnArmyDetailRequested?.Invoke(army.id));
        }

        private void BuildVillagerRow(VillagerGroupData group)
        {
            bool isOwned = group.ownerID.HasValue && group.ownerID.Value == localPlayerID;

            UIHelper.CreateDivider(contentRT);

            var nameRow = UIHelper.CreateHorizontalRow(contentRT, 24f, 4f);
            string task = group.currentTask != null ? group.currentTask.DisplayName : "Idle";
            var nameLabel = UIHelper.CreateLabel(nameRow.transform,
                $"Villagers x{group.villagerCount} — {task}", 12);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            if (!isOwned) return;

            // Action buttons
            var btnRow = UIHelper.CreateHorizontalRow(contentRT, 28f, 3f);

            // Move
            CreateSmallButton(btnRow.transform, "Move", () =>
                OnMoveVillagerRequested?.Invoke(group.id));

            // Cancel task
            if (group.currentTask != null && !group.currentTask.IsIdle)
            {
                CreateSmallButton(btnRow.transform, "Cancel", () =>
                {
                    var cmd = new StopGatheringCommand(localPlayerID, group.id);
                    GameEngine.Instance.ExecuteCommand(cmd);
                });
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private Button CreateSmallButton(Transform parent, string text, Action onClick)
        {
            var btn = UIHelper.CreateButton(parent, text,
                SporefrontColors.InkMid, UIHelper.HudTextColor, 11, onClick);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 52;
            le.preferredHeight = 28;
            return btn;
        }
    }
}
