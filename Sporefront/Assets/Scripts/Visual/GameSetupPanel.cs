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

    public class GameSetupPanel : MonoBehaviour
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

        // ================================================================
        // Arena Section
        // ================================================================

        private void BuildArenaSection()
        {
            arenaSection = UIHelper.CreatePanel(contentRT, "ArenaSection", Color.clear);
            var sectionVLG = arenaSection.AddComponent<VerticalLayoutGroup>();
            sectionVLG.spacing = 4f;
            sectionVLG.childForceExpandWidth = true;
            sectionVLG.childForceExpandHeight = false;
            sectionVLG.childControlWidth = true;
            sectionVLG.childControlHeight = false;
            sectionVLG.padding = new RectOffset(0, 0, 4, 4);

            var sectionCSF = arenaSection.AddComponent<ContentSizeFitter>();
            sectionCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Arena header
            var arenaHeader = UIHelper.CreateLabel(arenaSection.transform, "Arena Configuration",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleCenter, true);
            var arenaHeaderLE = arenaHeader.gameObject.AddComponent<LayoutElement>();
            arenaHeaderLE.preferredHeight = 32;

            UIHelper.CreateDivider(arenaSection.transform, SporefrontColors.SporeAmber, 2f);

            // Presets
            BuildPresetsSection(arenaSection.transform);
            UIHelper.CreateDivider(arenaSection.transform);

            // Terrain
            BuildTerrainSection(arenaSection.transform);
            UIHelper.CreateDivider(arenaSection.transform);

            // Building
            BuildBuildingSection(arenaSection.transform);
            UIHelper.CreateDivider(arenaSection.transform);

            // Entrenchment
            BuildEntrenchmentSection(arenaSection.transform);
            UIHelper.CreateDivider(arenaSection.transform);

            // Stacking
            BuildStackingSection(arenaSection.transform);
            UIHelper.CreateDivider(arenaSection.transform);

            // Commander
            BuildCommanderSection(arenaSection.transform);
            UIHelper.CreateDivider(arenaSection.transform);

            // Player Army
            BuildArmySection(arenaSection.transform, "Player Army", true);
            UIHelper.CreateDivider(arenaSection.transform);

            // Enemy Army
            BuildArmySection(arenaSection.transform, "Enemy Army", false);
            UIHelper.CreateDivider(arenaSection.transform);

            // Garrison
            BuildGarrisonSection(arenaSection.transform);
            UIHelper.CreateDivider(arenaSection.transform);

            // Sim Run Count
            BuildSimRunSection(arenaSection.transform);
            UIHelper.CreateDivider(arenaSection.transform);

            // Play / Auto-Sim buttons
            BuildArenaButtons(arenaSection.transform);
        }

        // ================================================================
        // Arena Presets
        // ================================================================

        private void BuildPresetsSection(Transform parent)
        {
            var label = UIHelper.CreateLabel(parent, "Presets",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 24;

            // Create rows of preset buttons (6 per row)
            var presets = ArenaPresetExtensions.AllValues;
            int buttonsPerRow = 6;
            int rowCount = Mathf.CeilToInt(presets.Length / (float)buttonsPerRow);

            for (int r = 0; r < rowCount; r++)
            {
                var row = UIHelper.CreateHorizontalRow(parent, 30f, 3f);
                var rowLE = row.gameObject.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 30;

                for (int c = 0; c < buttonsPerRow; c++)
                {
                    int idx = r * buttonsPerRow + c;
                    if (idx >= presets.Length) break;

                    var preset = presets[idx];
                    bool isSelected = preset == selectedPreset;

                    var btn = UIHelper.CreateButton(row.transform, preset.DisplayName(),
                        isSelected ? SporefrontColors.SporeAmber : SporefrontColors.ParchmentDark,
                        isSelected ? UIHelper.HudTextColor : UIHelper.ButtonText,
                        10, null);
                    var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                    btnLE.preferredWidth = 110;
                    btnLE.preferredHeight = 30;

                    var capturedPreset = preset;
                    btn.onClick.AddListener(() => ApplyPreset(capturedPreset));
                }
            }
        }

        private void ApplyPreset(ArenaPreset preset)
        {
            selectedPreset = preset;
            if (preset != ArenaPreset.Custom)
            {
                arenaScenario = preset.ToConfig();
            }
            Rebuild();
        }

        // ================================================================
        // Arena: Terrain
        // ================================================================

        private void BuildTerrainSection(Transform parent)
        {
            var label = UIHelper.CreateLabel(parent, "Enemy Terrain",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 24;

            var row = UIHelper.CreateHorizontalRow(parent, 32f, 4f);
            var buttons = new List<Button>();

            TerrainType[] terrains = { TerrainType.Plains, TerrainType.Hill, TerrainType.Mountain, TerrainType.Desert };
            for (int i = 0; i < terrains.Length; i++)
            {
                int idx = i;
                var terrain = terrains[i];
                bool isSelected = terrain == arenaScenario.enemyTerrain;

                var btn = UIHelper.CreateButton(row.transform, terrain.DisplayName(),
                    isSelected ? SporefrontColors.SporeAmber : UIHelper.ButtonBg,
                    isSelected ? UIHelper.HudTextColor : UIHelper.ButtonText,
                    12, () =>
                    {
                        arenaScenario.enemyTerrain = terrains[idx];
                        selectedPreset = ArenaPreset.Custom;
                        Rebuild();
                    });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 80;
                btnLE.preferredHeight = 32;
                buttons.Add(btn);
            }
        }

        // ================================================================
        // Arena: Building
        // ================================================================

        private void BuildBuildingSection(Transform parent)
        {
            var label = UIHelper.CreateLabel(parent, "Enemy Building",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 24;

            var row = UIHelper.CreateHorizontalRow(parent, 32f, 4f);

            BuildingType?[] options = { null, BuildingType.Tower, BuildingType.WoodenFort, BuildingType.Castle };
            string[] names = { "None", "Tower", "Fort", "Castle" };

            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                bool isSelected = arenaScenario.enemyBuilding == options[i];

                var btn = UIHelper.CreateButton(row.transform, names[i],
                    isSelected ? SporefrontColors.SporeAmber : UIHelper.ButtonBg,
                    isSelected ? UIHelper.HudTextColor : UIHelper.ButtonText,
                    12, () =>
                    {
                        arenaScenario.enemyBuilding = options[idx];
                        selectedPreset = ArenaPreset.Custom;
                        Rebuild();
                    });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 80;
                btnLE.preferredHeight = 32;
            }
        }

        // ================================================================
        // Arena: Entrenchment
        // ================================================================

        private void BuildEntrenchmentSection(Transform parent)
        {
            var label = UIHelper.CreateLabel(parent, "Enemy Entrenched",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 24;

            var row = UIHelper.CreateHorizontalRow(parent, 32f, 4f);

            string[] names = { "No", "Yes" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                bool isSelected = (idx == 0 && !arenaScenario.enemyEntrenched) ||
                                  (idx == 1 && arenaScenario.enemyEntrenched);

                var btn = UIHelper.CreateButton(row.transform, names[i],
                    isSelected ? SporefrontColors.SporeAmber : UIHelper.ButtonBg,
                    isSelected ? UIHelper.HudTextColor : UIHelper.ButtonText,
                    12, () =>
                    {
                        arenaScenario.enemyEntrenched = idx == 1;
                        selectedPreset = ArenaPreset.Custom;
                        Rebuild();
                    });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 80;
                btnLE.preferredHeight = 32;
            }
        }

        // ================================================================
        // Arena: Stacking
        // ================================================================

        private void BuildStackingSection(Transform parent)
        {
            var label = UIHelper.CreateLabel(parent, "Army Stacking",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 24;

            var row = UIHelper.CreateHorizontalRow(parent, 32f, 4f);

            // Stacking options: Single, Stacked (2-5), Adjacent (2-5)
            string[] names = { "Single", "Stacked", "Adjacent" };
            int currentMode = 0; // 0=single, 1=stacked, 2=adjacent
            if (arenaScenario.enemyArmyCount > 1) currentMode = 1;
            else if (arenaScenario.enemyArmyCount < -1) currentMode = 2;

            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                bool isSelected = idx == currentMode;

                var btn = UIHelper.CreateButton(row.transform, names[i],
                    isSelected ? SporefrontColors.SporeAmber : UIHelper.ButtonBg,
                    isSelected ? UIHelper.HudTextColor : UIHelper.ButtonText,
                    12, () =>
                    {
                        if (idx == 0)
                        {
                            arenaScenario.enemyArmyCount = 1;
                            arenaScenario.playerArmyCount = 1;
                        }
                        else if (idx == 1)
                        {
                            arenaScenario.enemyArmyCount = 2;
                            arenaScenario.playerArmyCount = 2;
                        }
                        else
                        {
                            arenaScenario.enemyArmyCount = -2;
                            arenaScenario.playerArmyCount = -2;
                        }
                        selectedPreset = ArenaPreset.Custom;
                        Rebuild();
                    });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 80;
                btnLE.preferredHeight = 32;
            }

            // Army count slider (when stacked or adjacent)
            if (currentMode > 0)
            {
                var countRow = UIHelper.CreateHorizontalRow(parent, 28f, 4f);

                var countLabel = UIHelper.CreateLabel(countRow.transform,
                    $"Army Count: {Mathf.Abs(arenaScenario.enemyArmyCount)}", 12);
                var countLabelLE = countLabel.gameObject.AddComponent<LayoutElement>();
                countLabelLE.preferredWidth = 120;

                string[] countNames = { "2", "3", "4", "5" };
                for (int i = 0; i < countNames.Length; i++)
                {
                    int count = i + 2;
                    int currentCount = Mathf.Abs(arenaScenario.enemyArmyCount);
                    bool isCountSelected = currentCount == count;

                    var countBtn = UIHelper.CreateButton(countRow.transform, countNames[i],
                        isCountSelected ? SporefrontColors.SporeAmber : UIHelper.ButtonBg,
                        isCountSelected ? UIHelper.HudTextColor : UIHelper.ButtonText,
                        11, () =>
                        {
                            int sign = arenaScenario.enemyArmyCount < 0 ? -1 : 1;
                            arenaScenario.enemyArmyCount = count * sign;
                            arenaScenario.playerArmyCount = count * (arenaScenario.playerArmyCount < 0 ? -1 : 1);
                            selectedPreset = ArenaPreset.Custom;
                            Rebuild();
                        });
                    var countBtnLE = countBtn.gameObject.AddComponent<LayoutElement>();
                    countBtnLE.preferredWidth = 36;
                    countBtnLE.preferredHeight = 28;
                }
            }
        }

        // ================================================================
        // Arena: Commander
        // ================================================================

        private void BuildCommanderSection(Transform parent)
        {
            var label = UIHelper.CreateLabel(parent, "Commander Specialty",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 24;

            // Player commander
            var playerLabel = UIHelper.CreateLabel(parent, "Player Commander", 12,
                SporefrontColors.InkMid);
            var playerLabelLE = playerLabel.gameObject.AddComponent<LayoutElement>();
            playerLabelLE.preferredHeight = 20;

            BuildCommanderPicker(parent, true);

            // Enemy commander
            var enemyLabel = UIHelper.CreateLabel(parent, "Enemy Commander", 12,
                SporefrontColors.InkMid);
            var enemyLabelLE = enemyLabel.gameObject.AddComponent<LayoutElement>();
            enemyLabelLE.preferredHeight = 20;

            BuildCommanderPicker(parent, false);

            // Commander level
            BuildCommanderLevelSection(parent);
        }

        private void BuildCommanderPicker(Transform parent, bool isPlayer)
        {
            var specialties = (CommanderSpecialty[])Enum.GetValues(typeof(CommanderSpecialty));
            var currentSpec = isPlayer ? arenaScenario.playerCommanderSpecialty : arenaScenario.enemyCommanderSpecialty;

            // Two rows of 5
            for (int rowIdx = 0; rowIdx < 2; rowIdx++)
            {
                var row = UIHelper.CreateHorizontalRow(parent, 28f, 3f);
                var rowLE = row.gameObject.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 28;

                for (int c = 0; c < 5; c++)
                {
                    int idx = rowIdx * 5 + c;
                    if (idx >= specialties.Length) break;

                    var spec = specialties[idx];
                    bool isSelected = spec == currentSpec;

                    var btn = UIHelper.CreateButton(row.transform, spec.DisplayName(),
                        isSelected ? SporefrontColors.SporeAmber : UIHelper.ButtonBg,
                        isSelected ? UIHelper.HudTextColor : UIHelper.ButtonText,
                        9, null);
                    var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                    btnLE.preferredWidth = 110;
                    btnLE.preferredHeight = 28;

                    var capturedSpec = spec;
                    btn.onClick.AddListener(() =>
                    {
                        if (isPlayer)
                            arenaScenario.playerCommanderSpecialty = capturedSpec;
                        else
                            arenaScenario.enemyCommanderSpecialty = capturedSpec;
                        selectedPreset = ArenaPreset.Custom;
                        Rebuild();
                    });
                }
            }
        }

        private void BuildCommanderLevelSection(Transform parent)
        {
            var row = UIHelper.CreateHorizontalRow(parent, 28f, 4f);

            var label = UIHelper.CreateLabel(row.transform, "Commander Level", 12);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 120;

            int[] levels = { 1, 5, 10, 15, 20, 25 };
            foreach (var level in levels)
            {
                bool isPlayerSel = arenaScenario.playerCommanderLevel == level;
                var btn = UIHelper.CreateButton(row.transform, level.ToString(),
                    isPlayerSel ? SporefrontColors.SporeAmber : UIHelper.ButtonBg,
                    isPlayerSel ? UIHelper.HudTextColor : UIHelper.ButtonText,
                    10, () =>
                    {
                        arenaScenario.playerCommanderLevel = level;
                        arenaScenario.enemyCommanderLevel = level;
                        selectedPreset = ArenaPreset.Custom;
                        Rebuild();
                    });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 36;
                btnLE.preferredHeight = 28;
            }
        }

        // ================================================================
        // Arena: Army Composition
        // ================================================================

        private void BuildArmySection(Transform parent, string title, bool isPlayer)
        {
            var sectionLabel = UIHelper.CreateLabel(parent, title,
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            var unitTypes = ArenaArmyConfiguration.AllUnitTypes;
            var army = isPlayer ? arenaArmy.playerArmy : arenaArmy.enemyArmy;
            var tierDict = isPlayer ? arenaScenario.playerUnitTiers : arenaScenario.enemyUnitTiers;

            foreach (var unitType in unitTypes)
            {
                var row = UIHelper.CreateHorizontalRow(parent, 28f, 4f);

                // Unit name
                var nameLabel = UIHelper.CreateLabel(row.transform, unitType.DisplayName(), 12);
                var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                nameLE.preferredWidth = 100;

                // Count slider
                int currentCount = 0;
                if (army.ContainsKey(unitType)) currentCount = army[unitType];

                var slider = UIHelper.CreateSlider(row.transform, 0, ArenaArmyConfiguration.MaxUnitsPerType,
                    true, null);
                slider.value = currentCount;
                var sliderLE = slider.gameObject.AddComponent<LayoutElement>();
                sliderLE.flexibleWidth = 1;
                sliderLE.preferredHeight = 20;

                // Count label
                var countLabel = UIHelper.CreateLabel(row.transform, currentCount.ToString(), 12,
                    UIHelper.BodyTextColor, TextAnchor.MiddleRight);
                var countLE = countLabel.gameObject.AddComponent<LayoutElement>();
                countLE.preferredWidth = 30;

                if (isPlayer) playerUnitLabels[unitType] = countLabel;
                else enemyUnitLabels[unitType] = countLabel;

                var capturedType = unitType;
                var capturedLabel = countLabel;
                slider.onValueChanged.AddListener((v) =>
                {
                    int val = Mathf.RoundToInt(v);
                    if (isPlayer)
                        arenaArmy.playerArmy[capturedType] = val;
                    else
                        arenaArmy.enemyArmy[capturedType] = val;
                    capturedLabel.text = val.ToString();
                });

                // Tier controls [0|1|2]
                int currentTier = 0;
                if (tierDict.ContainsKey(unitType)) currentTier = tierDict[unitType];

                for (int t = 0; t < 3; t++)
                {
                    int tier = t;
                    bool isTierSelected = currentTier == tier;

                    var tierBtn = UIHelper.CreateButton(row.transform, tier.ToString(),
                        isTierSelected ? SporefrontColors.SporeAmber : SporefrontColors.ParchmentDark,
                        isTierSelected ? UIHelper.HudTextColor : UIHelper.ButtonText,
                        10, () =>
                        {
                            if (isPlayer)
                                arenaScenario.playerUnitTiers[capturedType] = tier;
                            else
                                arenaScenario.enemyUnitTiers[capturedType] = tier;
                            selectedPreset = ArenaPreset.Custom;
                            Rebuild();
                        });
                    var tierBtnLE = tierBtn.gameObject.AddComponent<LayoutElement>();
                    tierBtnLE.preferredWidth = 24;
                    tierBtnLE.preferredHeight = 24;
                }
            }
        }

        // ================================================================
        // Arena: Garrison
        // ================================================================

        private void BuildGarrisonSection(Transform parent)
        {
            var label = UIHelper.CreateLabel(parent, "Garrison Archers",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 24;

            var row = UIHelper.CreateHorizontalRow(parent, 28f, 4f);

            var slider = UIHelper.CreateSlider(row.transform, 0, 20, true, null);
            slider.value = arenaScenario.garrisonArchers;
            var sliderLE = slider.gameObject.AddComponent<LayoutElement>();
            sliderLE.flexibleWidth = 1;
            sliderLE.preferredHeight = 20;

            var countLabel = UIHelper.CreateLabel(row.transform,
                arenaScenario.garrisonArchers.ToString(), 12,
                UIHelper.BodyTextColor, TextAnchor.MiddleRight);
            var countLE = countLabel.gameObject.AddComponent<LayoutElement>();
            countLE.preferredWidth = 30;

            slider.onValueChanged.AddListener((v) =>
            {
                arenaScenario.garrisonArchers = Mathf.RoundToInt(v);
                countLabel.text = arenaScenario.garrisonArchers.ToString();
            });
        }

        // ================================================================
        // Arena: Sim Run Count
        // ================================================================

        private void BuildSimRunSection(Transform parent)
        {
            var label = UIHelper.CreateLabel(parent, "Simulation Runs",
                UIHelper.DefaultHeaderFontSize - 2, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 24;

            var row = UIHelper.CreateHorizontalRow(parent, 28f, 4f);

            var slider = UIHelper.CreateSlider(row.transform, 1, 50, true, null);
            slider.value = simRunCount;
            var sliderLE = slider.gameObject.AddComponent<LayoutElement>();
            sliderLE.flexibleWidth = 1;
            sliderLE.preferredHeight = 20;

            simRunLabel = UIHelper.CreateLabel(row.transform, simRunCount.ToString(), 12,
                UIHelper.BodyTextColor, TextAnchor.MiddleRight);
            var countLE = simRunLabel.gameObject.AddComponent<LayoutElement>();
            countLE.preferredWidth = 30;

            slider.onValueChanged.AddListener((v) =>
            {
                simRunCount = Mathf.RoundToInt(v);
                simRunLabel.text = simRunCount.ToString();
            });
        }

        // ================================================================
        // Arena: Play / Auto-Sim Buttons
        // ================================================================

        private void BuildArenaButtons(Transform parent)
        {
            var spacer = new GameObject("ArenaBtnSpacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = 8;

            var row = UIHelper.CreateHorizontalRow(parent, 44f, 8f);
            var rowLE = row.gameObject.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 44;
            row.childAlignment = TextAnchor.MiddleCenter;

            var playBtn = UIHelper.CreateButton(row.transform, "Play Arena",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor, 14, () =>
                {
                    OnPlayArena?.Invoke(GetCurrentArenaConfig());
                });
            var playBtnLE = playBtn.gameObject.AddComponent<LayoutElement>();
            playBtnLE.preferredWidth = 140;
            playBtnLE.preferredHeight = 44;

            var simBtn = UIHelper.CreateButton(row.transform, "Auto-Sim",
                SporefrontColors.SporePurple, UIHelper.HudTextColor, 14, () =>
                {
                    OnAutoSim?.Invoke(GetCurrentArenaConfig(), simRunCount);
                });
            var simBtnLE = simBtn.gameObject.AddComponent<LayoutElement>();
            simBtnLE.preferredWidth = 140;
            simBtnLE.preferredHeight = 44;

            var bottomSpacer = new GameObject("BottomSpacer", typeof(RectTransform), typeof(LayoutElement));
            bottomSpacer.transform.SetParent(parent, false);
            bottomSpacer.GetComponent<LayoutElement>().preferredHeight = 20;
        }

        // ================================================================
        // Arena Section Visibility
        // ================================================================

        private void UpdateArenaSectionVisibility()
        {
            bool isArena = selectedMapType == MapType.Arena;
            if (arenaSection != null) arenaSection.SetActive(isArena);
            if (standardStartButton != null) standardStartButton.SetActive(!isArena);
        }

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

                var colors = buttons[i].colors;
                Color bg = isSel ? SporefrontColors.SporeAmber : UIHelper.ButtonBg;
                colors.normalColor = bg;
                colors.highlightedColor = Color.Lerp(bg, Color.white, 0.15f);
                colors.pressedColor = Color.Lerp(bg, Color.black, 0.1f);
                buttons[i].colors = colors;
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
