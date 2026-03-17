// ============================================================================
// FILE: Visual/SelectedEntitiesPanel.cs
// PURPOSE: Bottom-left panel showing currently selected entities with type,
//          count, and status info. Visible during single or multi-select.
//          Cards are clickable to focus a single entity from a group.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class SelectedEntitiesPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        /// <summary>
        /// Fired when a card is clicked. (entityID, isArmy)
        /// </summary>
        public event Action<Guid, bool> OnEntityCardClicked;

        // ================================================================
        // Row Tracking
        // ================================================================

        private struct RowInfo
        {
            public GameObject go;
            public Guid entityID;
            public bool isArmy;
            public Image background;
        }

        // ================================================================
        // State
        // ================================================================

        private GameObject panelRoot;
        private RectTransform contentParent;
        private List<RowInfo> rows = new List<RowInfo>();
        private Guid localPlayerID;
        private bool initialized;
        private Guid? highlightedEntityID;

        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Panel root — anchored bottom-left, above menu bar
            panelRoot = UIHelper.CreatePanel(canvasTransform, "SelectedEntitiesPanel",
                new Color(SporefrontColors.ParchmentMid.r, SporefrontColors.ParchmentMid.g,
                    SporefrontColors.ParchmentMid.b, 0.95f),
                UIHelper.SmallCornerRadius);

            var rt = panelRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(10, 65);
            rt.sizeDelta = new Vector2(320, 0); // height auto-fitted

            // Auto-height
            var csf = panelRoot.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Vertical layout
            var vlg = panelRoot.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.spacing = 6;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            contentParent = panelRoot.GetComponent<RectTransform>();

            panelRoot.SetActive(false);
            initialized = true;
        }

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
        }

        // ================================================================
        // Public API
        // ================================================================

        public void UpdateSelection(GameState gameState, Guid? singleEntityID, bool singleIsArmy,
            List<(Guid id, bool isArmy)> multiEntities)
        {
            if (!initialized || panelRoot == null) return;

            ClearRows();
            highlightedEntityID = null;

            if (multiEntities != null && multiEntities.Count > 0)
            {
                // Multi-select mode
                foreach (var entity in multiEntities)
                {
                    var card = FormatEntityCard(gameState, entity.id, entity.isArmy);
                    if (card.HasValue) AddRow(card.Value.main, card.Value.detail, entity.id, entity.isArmy);
                }
            }
            else if (singleEntityID.HasValue)
            {
                // Single-select mode
                var card = FormatEntityCard(gameState, singleEntityID.Value, singleIsArmy);
                if (card.HasValue) AddRow(card.Value.main, card.Value.detail, singleEntityID.Value, singleIsArmy);
            }

            bool hasContent = rows.Count > 0;
            panelRoot.SetActive(hasContent);
        }

        public void SetHighlight(Guid? entityID)
        {
            highlightedEntityID = entityID;
            UpdateRowHighlights();
        }

        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        // ================================================================
        // Row Building
        // ================================================================

        private (string main, string detail)? FormatEntityCard(GameState gameState, Guid entityID, bool isArmy)
        {
            if (isArmy)
            {
                var army = gameState.GetArmy(entityID);
                if (army == null) return null;

                int totalUnits = army.GetTotalUnits();
                string topUnit = GetTopUnitType(army);
                string status = UIHelper.FormatArmyStatus(army);
                string mainLine = $"Army: {totalUnits} units";
                if (!string.IsNullOrEmpty(topUnit))
                    mainLine += $" ({topUnit})";
                if (!string.IsNullOrEmpty(status))
                    mainLine += $" {status}";

                string detailLine;
                if (army.commanderID.HasValue)
                {
                    var commander = gameState.GetCommander(army.commanderID.Value);
                    if (commander != null)
                    {
                        int staminaPct = (int)Math.Round(commander.stamina);
                        detailLine = $"Cmdr Lv.{commander.level} \u2014 {commander.specialty.DisplayName()} | Sta: {staminaPct}%";
                    }
                    else
                        detailLine = "No Commander";
                }
                else
                {
                    detailLine = "No Commander";
                }

                return (mainLine, detailLine);
            }
            else
            {
                var vg = gameState.GetVillagerGroup(entityID);
                if (vg == null) return null;

                string mainLine = $"Villagers: {vg.villagerCount}";
                string task = vg.currentTask.IsIdle ? "Idle" : vg.currentTask.DisplayName;
                string detailLine = $"Task: {task}";

                return (mainLine, detailLine);
            }
        }

        private string GetTopUnitType(ArmyData army)
        {
            string topName = null;
            int topCount = 0;
            foreach (var kvp in army.militaryComposition)
            {
                if (kvp.Value > topCount)
                {
                    topCount = kvp.Value;
                    topName = kvp.Key.DisplayName();
                }
            }
            return topName;
        }

        private void AddRow(string mainLine, string detailLine, Guid entityID, bool isArmy)
        {
            var rowGO = new GameObject("EntityRow");
            rowGO.transform.SetParent(contentParent, false);

            // Background image for highlight and button interaction
            var bgImage = rowGO.AddComponent<Image>();
            bgImage.color = Color.clear;

            var le = rowGO.AddComponent<LayoutElement>();
            le.preferredHeight = 44;
            le.flexibleWidth = 1;

            var vlg = rowGO.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.spacing = 2;
            vlg.padding = new RectOffset(6, 6, 4, 4);

            UIHelper.CreateLabel(rowGO.transform, mainLine, 14, UIHelper.InkMutedText);
            UIHelper.CreateLabel(rowGO.transform, detailLine, UIConstants.FontCaption, UIHelper.InkMutedText);

            // Make card clickable
            var btn = rowGO.AddComponent<Button>();
            btn.targetGraphic = bgImage;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = UIHelper.CardButtonColors(Color.clear);

            var capturedID = entityID;
            var capturedIsArmy = isArmy;
            btn.onClick.AddListener(() =>
            {
                highlightedEntityID = capturedID;
                UpdateRowHighlights();
                OnEntityCardClicked?.Invoke(capturedID, capturedIsArmy);
            });

            rows.Add(new RowInfo
            {
                go = rowGO,
                entityID = entityID,
                isArmy = isArmy,
                background = bgImage
            });
        }

        private void UpdateRowHighlights()
        {
            foreach (var row in rows)
            {
                if (row.background == null) continue;

                bool isHighlighted = highlightedEntityID.HasValue && row.entityID == highlightedEntityID.Value;
                Color bgColor = isHighlighted
                    ? new Color(SporefrontColors.SporeTeal.r, SporefrontColors.SporeTeal.g,
                        SporefrontColors.SporeTeal.b, 0.25f)
                    : Color.clear;

                row.background.color = bgColor;

                // Update button colors to match new base
                var btn = row.go.GetComponent<Button>();
                if (btn != null)
                    btn.colors = UIHelper.CardButtonColors(bgColor);
            }
        }

        private void ClearRows()
        {
            foreach (var row in rows)
            {
                if (row.go != null) Destroy(row.go);
            }
            rows.Clear();
        }

        // ================================================================
        // Cleanup
        // ================================================================

        private void OnDestroy()
        {
            ClearRows();
        }
    }
}
