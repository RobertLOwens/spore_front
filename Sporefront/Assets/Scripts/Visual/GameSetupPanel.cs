// ============================================================================
// FILE: Visual/GameSetupPanel.cs
// PURPOSE: Full-screen game setup panel with map options, arena config, and
//          preset selection. Port of GameSetupViewController.swift
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Visual
{
    // ================================================================
    // Configuration Structs
    // ================================================================

    [Serializable]
    public struct GameSetupConfig
    {
        public MapType mapType;
        public MapSize mapSize;
        public ResourceDensity resourceDensity;
        public VisibilityMode visibilityMode;

        public static GameSetupConfig Default => new GameSetupConfig
        {
            mapType = MapType.Arabia,
            mapSize = MapSize.Medium,
            resourceDensity = ResourceDensity.Normal,
            visibilityMode = VisibilityMode.Normal
        };
    }

    public enum MapType { Arabia, Random, Arena }
    public enum MapSize { Small, Medium, Large, Huge }
    public enum ResourceDensity { Sparse, Normal, Abundant }
    public enum VisibilityMode { Normal, Full }

    [Serializable]
    public struct ArenaConfig
    {
        public ArenaScenarioConfig scenarioConfig;
        public ArenaArmyConfiguration armyConfig;
        public ArenaPreset selectedPreset;

        public static ArenaConfig Default => new ArenaConfig
        {
            scenarioConfig = ArenaScenarioConfig.Default,
            armyConfig = ArenaArmyConfiguration.Default,
            selectedPreset = ArenaPreset.Plains
        };
    }

    // ================================================================
    // Panel
    // ================================================================

    public partial class GameSetupPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<GameSetupConfig> OnStartGame;
        public event Action<ArenaConfig> OnPlayArena;
        public event Action<ArenaConfig, int> OnAutoSim;
        public event Action OnBack;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private RectTransform contentRT;

        // Current selections
        private MapType selectedMapType = MapType.Arabia;
        private MapSize selectedMapSize = MapSize.Medium;
        private ResourceDensity selectedDensity = ResourceDensity.Normal;
        private VisibilityMode selectedVisibility = VisibilityMode.Normal;

        // Arena config
        private ArenaScenarioConfig arenaScenario = ArenaScenarioConfig.Default;
        private ArenaArmyConfiguration arenaArmy = ArenaArmyConfiguration.Default;
        private ArenaPreset selectedPreset = ArenaPreset.Plains;
        private int simRunCount = 10;

        // UI references for arena section toggle
        private GameObject arenaSection;
        private GameObject standardStartButton;

        // Segmented button tracking
        private Dictionary<string, List<Button>> segmentGroups = new Dictionary<string, List<Button>>();
        private Dictionary<string, int> segmentSelections = new Dictionary<string, int>();

        // Arena unit slider labels
        private Dictionary<MilitaryUnitType, Text> playerUnitLabels = new Dictionary<MilitaryUnitType, Text>();
        private Dictionary<MilitaryUnitType, Text> enemyUnitLabels = new Dictionary<MilitaryUnitType, Text>();
        private Text simRunLabel;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Full-screen panel
            panel = UIHelper.CreatePanel(canvasTransform, "GameSetupPanel", UIHelper.PanelBg);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.StretchFull(panelRT);

            // Header bar
            var header = UIHelper.CreatePanel(panel.transform, "Header", SporefrontColors.ParchmentDark);
            var headerRT = header.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.sizeDelta = new Vector2(0, 50);

            var headerRow = UIHelper.CreateHorizontalRow(header.transform, 50f, 8f);
            var headerRowRT = headerRow.GetComponent<RectTransform>();
            UIHelper.StretchFull(headerRowRT);
            headerRow.padding = new RectOffset(12, 12, 0, 0);

            var backBtn = UIHelper.CreateButton(headerRow.transform, "Back",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 14,
                () => OnBack?.Invoke());
            var backBtnLE = backBtn.gameObject.AddComponent<LayoutElement>();
            backBtnLE.preferredWidth = 70;

            var titleLabel = UIHelper.CreateLabel(headerRow.transform, "Game Setup",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var titleLE = titleLabel.gameObject.AddComponent<LayoutElement>();
            titleLE.flexibleWidth = 1;

            // Invisible balance spacer
            var spacerGO = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacerGO.transform.SetParent(headerRow.transform, false);
            spacerGO.GetComponent<LayoutElement>().preferredWidth = 70;

            // Scroll view for content
            var scroll = UIHelper.CreateScrollView(panel.transform, "SetupScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(0, 0);
            scrollRT.offsetMax = new Vector2(0, -50); // Below header

            Rebuild();
            panel.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show()
        {
            panel.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
        }

        public void Hide()
        {
            panel.SetActive(false);
        }

        public bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Rebuild Content
        // ================================================================

        private void Rebuild()
        {
            // Clear
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(contentRT.GetChild(i).gameObject);

            segmentGroups.Clear();
            segmentSelections.Clear();
            playerUnitLabels.Clear();
            enemyUnitLabels.Clear();

            BuildMapTypeSection();
            UIHelper.CreateDivider(contentRT);
            BuildMapSizeSection();
            UIHelper.CreateDivider(contentRT);
            BuildResourceDensitySection();
            UIHelper.CreateDivider(contentRT);
            BuildVisibilitySection();
            UIHelper.CreateDivider(contentRT);

            // Standard start button (hidden when Arena)
            standardStartButton = BuildStartGameButton();

            // Arena config section
            BuildArenaSection();

            UpdateArenaSectionVisibility();
        }

        // ================================================================
        // Map Type
        // ================================================================

        private void BuildMapTypeSection()
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Map Type",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var row = UIHelper.CreateHorizontalRow(contentRT, 36f, 4f);
            var buttons = new List<Button>();

            string[] names = { "Arabia", "Random", "Arena" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var btn = UIHelper.CreateButton(row.transform, names[i], null, null, 13, () =>
                {
                    selectedMapType = (MapType)idx;
                    UpdateSegmentSelection("mapType", idx);
                    UpdateArenaSectionVisibility();
                });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 100;
                btnLE.preferredHeight = 36;
                buttons.Add(btn);
            }

            segmentGroups["mapType"] = buttons;
            segmentSelections["mapType"] = (int)selectedMapType;
            UpdateSegmentColors("mapType");
        }

        // ================================================================
        // Map Size
        // ================================================================

        private void BuildMapSizeSection()
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Map Size",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var row = UIHelper.CreateHorizontalRow(contentRT, 36f, 4f);
            var buttons = new List<Button>();

            string[] names = { "Small", "Medium", "Large", "Huge" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var btn = UIHelper.CreateButton(row.transform, names[i], null, null, 13, () =>
                {
                    selectedMapSize = (MapSize)idx;
                    UpdateSegmentSelection("mapSize", idx);
                });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 80;
                btnLE.preferredHeight = 36;
                buttons.Add(btn);
            }

            segmentGroups["mapSize"] = buttons;
            segmentSelections["mapSize"] = (int)selectedMapSize;
            UpdateSegmentColors("mapSize");
        }

        // ================================================================
        // Resource Density
        // ================================================================

        private void BuildResourceDensitySection()
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Resource Density",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var row = UIHelper.CreateHorizontalRow(contentRT, 36f, 4f);
            var buttons = new List<Button>();

            string[] names = { "Sparse", "Normal", "Abundant" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var btn = UIHelper.CreateButton(row.transform, names[i], null, null, 13, () =>
                {
                    selectedDensity = (ResourceDensity)idx;
                    UpdateSegmentSelection("density", idx);
                });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 100;
                btnLE.preferredHeight = 36;
                buttons.Add(btn);
            }

            segmentGroups["density"] = buttons;
            segmentSelections["density"] = (int)selectedDensity;
            UpdateSegmentColors("density");
        }

        // ================================================================
        // Visibility Mode
        // ================================================================

        private void BuildVisibilitySection()
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, "Visibility",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 28;

            var row = UIHelper.CreateHorizontalRow(contentRT, 36f, 4f);
            var buttons = new List<Button>();

            string[] names = { "Normal", "Full" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var btn = UIHelper.CreateButton(row.transform, names[i], null, null, 13, () =>
                {
                    selectedVisibility = (VisibilityMode)idx;
                    UpdateSegmentSelection("visibility", idx);
                });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 100;
                btnLE.preferredHeight = 36;
                buttons.Add(btn);
            }

            segmentGroups["visibility"] = buttons;
            segmentSelections["visibility"] = (int)selectedVisibility;
            UpdateSegmentColors("visibility");
        }

        // ================================================================
        // Start Game Button (for non-Arena modes)
        // ================================================================

        private GameObject BuildStartGameButton()
        {
            var spacer = new GameObject("StartSpacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(contentRT, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = 10;

            var container = UIHelper.CreatePanel(contentRT, "StartContainer", Color.clear);
            var containerLE = container.AddComponent<LayoutElement>();
            containerLE.preferredHeight = 50;

            var btn = UIHelper.CreateButton(container.transform, "Start Game",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor, 16, () =>
                {
                    var config = new GameSetupConfig
                    {
                        mapType = selectedMapType,
                        mapSize = selectedMapSize,
                        resourceDensity = selectedDensity,
                        visibilityMode = selectedVisibility
                    };
                    OnStartGame?.Invoke(config);
                });
            var btnRT = btn.GetComponent<RectTransform>();
            UIHelper.StretchFull(btnRT);
            btnRT.offsetMin = new Vector2(20, 4);
            btnRT.offsetMax = new Vector2(-20, -4);

            return container;
        }

        // Arena section methods are in GameSetupArenaSection.cs (partial class)

        // ================================================================
        // Segmented Button Helpers
        // ================================================================

        private void UpdateSegmentSelection(string group, int index)
        {
            segmentSelections[group] = index;
            UpdateSegmentColors(group);
        }

        private void UpdateSegmentColors(string group)
        {
            if (!segmentGroups.ContainsKey(group)) return;
            int selected = segmentSelections.ContainsKey(group) ? segmentSelections[group] : 0;
            var buttons = segmentGroups[group];

            for (int i = 0; i < buttons.Count; i++)
            {
                bool isSel = i == selected;
                var img = buttons[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = isSel ? SporefrontColors.SporeAmber : UIHelper.ButtonBg;
                }

                var label = buttons[i].GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.color = isSel ? UIHelper.HudTextColor : UIHelper.ButtonText;
                }

                Color bg = isSel ? SporefrontColors.SporeAmber : UIHelper.ButtonBg;
                buttons[i].colors = UIHelper.CardButtonColors(bg);
            }
        }

        // ================================================================
        // Config Helpers
        // ================================================================

        private ArenaConfig GetCurrentArenaConfig()
        {
            return new ArenaConfig
            {
                scenarioConfig = arenaScenario,
                armyConfig = arenaArmy,
                selectedPreset = selectedPreset
            };
        }
    }
}
