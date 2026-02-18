// ============================================================================
// FILE: Visual/GameSetupArenaSection.cs
// PURPOSE: Arena configuration UI — partial class extension of GameSetupPanel.
//          Presets, terrain, building, entrenchment, stacking, commander,
//          army composition, garrison, sim run count, play/auto-sim buttons.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public partial class GameSetupPanel
    {
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

            BuildPresetsSection(arenaSection.transform);
            UIHelper.CreateDivider(arenaSection.transform);

            BuildTerrainSection(arenaSection.transform);
            UIHelper.CreateDivider(arenaSection.transform);

            BuildBuildingSection(arenaSection.transform);
            UIHelper.CreateDivider(arenaSection.transform);

            BuildEntrenchmentSection(arenaSection.transform);
            UIHelper.CreateDivider(arenaSection.transform);

            BuildStackingSection(arenaSection.transform);
            UIHelper.CreateDivider(arenaSection.transform);

            BuildCommanderSection(arenaSection.transform);
            UIHelper.CreateDivider(arenaSection.transform);

            BuildArmySection(arenaSection.transform, "Player Army", true);
            UIHelper.CreateDivider(arenaSection.transform);

            BuildArmySection(arenaSection.transform, "Enemy Army", false);
            UIHelper.CreateDivider(arenaSection.transform);

            BuildArenaGarrisonSection(arenaSection.transform);
            UIHelper.CreateDivider(arenaSection.transform);

            BuildSimRunSection(arenaSection.transform);
            UIHelper.CreateDivider(arenaSection.transform);

            BuildArenaButtons(arenaSection.transform);
        }

        // ================================================================
        // Arena Presets
        // ================================================================

        private void BuildPresetsSection(Transform parent)
        {
            var label = UIHelper.CreateLabel(parent, "Presets",
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 24;

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
                        UIConstants.FontCaption, null);
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
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 24;

            var row = UIHelper.CreateHorizontalRow(parent, 32f, 4f);

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
            }
        }

        // ================================================================
        // Arena: Building
        // ================================================================

        private void BuildBuildingSection(Transform parent)
        {
            var label = UIHelper.CreateLabel(parent, "Enemy Building",
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
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
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
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
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 24;

            var row = UIHelper.CreateHorizontalRow(parent, 32f, 4f);

            string[] names = { "Single", "Stacked", "Adjacent" };
            int currentMode = 0;
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
                        if (idx == 0) { arenaScenario.enemyArmyCount = 1; arenaScenario.playerArmyCount = 1; }
                        else if (idx == 1) { arenaScenario.enemyArmyCount = 2; arenaScenario.playerArmyCount = 2; }
                        else { arenaScenario.enemyArmyCount = -2; arenaScenario.playerArmyCount = -2; }
                        selectedPreset = ArenaPreset.Custom;
                        Rebuild();
                    });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 80;
                btnLE.preferredHeight = 32;
            }

            if (currentMode > 0)
            {
                var countRow = UIHelper.CreateHorizontalRow(parent, 28f, 4f);
                var countLabel = UIHelper.CreateLabel(countRow.transform,
                    $"Army Count: {Mathf.Abs(arenaScenario.enemyArmyCount)}", 12);
                var countLabelLE = countLabel.gameObject.AddComponent<LayoutElement>();
                countLabelLE.preferredWidth = 120;

                for (int i = 0; i < 4; i++)
                {
                    int count = i + 2;
                    int currentCount = Mathf.Abs(arenaScenario.enemyArmyCount);
                    bool isCountSelected = currentCount == count;

                    var countBtn = UIHelper.CreateButton(countRow.transform, count.ToString(),
                        isCountSelected ? SporefrontColors.SporeAmber : UIHelper.ButtonBg,
                        isCountSelected ? UIHelper.HudTextColor : UIHelper.ButtonText,
                        UIConstants.FontSmall, () =>
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
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 24;

            var playerLabel = UIHelper.CreateLabel(parent, "Player Commander", 12, SporefrontColors.InkMid);
            var playerLabelLE = playerLabel.gameObject.AddComponent<LayoutElement>();
            playerLabelLE.preferredHeight = 20;
            BuildCommanderPicker(parent, true);

            var enemyLabel = UIHelper.CreateLabel(parent, "Enemy Commander", 12, SporefrontColors.InkMid);
            var enemyLabelLE = enemyLabel.gameObject.AddComponent<LayoutElement>();
            enemyLabelLE.preferredHeight = 20;
            BuildCommanderPicker(parent, false);

            BuildCommanderLevelSection(parent);
        }

        private void BuildCommanderPicker(Transform parent, bool isPlayer)
        {
            var specialties = (CommanderSpecialty[])Enum.GetValues(typeof(CommanderSpecialty));
            var currentSpec = isPlayer ? arenaScenario.playerCommanderSpecialty : arenaScenario.enemyCommanderSpecialty;

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
                        if (isPlayer) arenaScenario.playerCommanderSpecialty = capturedSpec;
                        else arenaScenario.enemyCommanderSpecialty = capturedSpec;
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
                    UIConstants.FontCaption, () =>
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
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 24;

            var unitTypes = ArenaArmyConfiguration.AllUnitTypes;
            var army = isPlayer ? arenaArmy.playerArmy : arenaArmy.enemyArmy;
            var tierDict = isPlayer ? arenaScenario.playerUnitTiers : arenaScenario.enemyUnitTiers;

            foreach (var unitType in unitTypes)
            {
                var row = UIHelper.CreateHorizontalRow(parent, 28f, 4f);

                var nameLabel = UIHelper.CreateLabel(row.transform, unitType.DisplayName(), 12);
                var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
                nameLE.preferredWidth = 100;

                int currentCount = 0;
                if (army.ContainsKey(unitType)) currentCount = army[unitType];

                var slider = UIHelper.CreateSlider(row.transform, 0, ArenaArmyConfiguration.MaxUnitsPerType,
                    true, null);
                slider.value = currentCount;
                var sliderLE = slider.gameObject.AddComponent<LayoutElement>();
                sliderLE.flexibleWidth = 1;
                sliderLE.preferredHeight = 20;

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
                    if (isPlayer) arenaArmy.playerArmy[capturedType] = val;
                    else arenaArmy.enemyArmy[capturedType] = val;
                    capturedLabel.text = val.ToString();
                });

                int currentTier = 0;
                if (tierDict.ContainsKey(unitType)) currentTier = tierDict[unitType];

                for (int t = 0; t < 3; t++)
                {
                    int tier = t;
                    bool isTierSelected = currentTier == tier;

                    var tierBtn = UIHelper.CreateButton(row.transform, tier.ToString(),
                        isTierSelected ? SporefrontColors.SporeAmber : SporefrontColors.ParchmentDark,
                        isTierSelected ? UIHelper.HudTextColor : UIHelper.ButtonText,
                        UIConstants.FontCaption, () =>
                        {
                            if (isPlayer) arenaScenario.playerUnitTiers[capturedType] = tier;
                            else arenaScenario.enemyUnitTiers[capturedType] = tier;
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

        private void BuildArenaGarrisonSection(Transform parent)
        {
            var label = UIHelper.CreateLabel(parent, "Garrison Archers",
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
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
                UIConstants.FontSubheader, UIHelper.HeaderTextColor,
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
                SporefrontColors.SporeGreen, UIHelper.HudTextColor, UIConstants.FontBody, () =>
                {
                    OnPlayArena?.Invoke(GetCurrentArenaConfig());
                });
            var playBtnLE = playBtn.gameObject.AddComponent<LayoutElement>();
            playBtnLE.preferredWidth = 140;
            playBtnLE.preferredHeight = 44;

            var simBtn = UIHelper.CreateButton(row.transform, "Auto-Sim",
                SporefrontColors.SporePurple, UIHelper.HudTextColor, UIConstants.FontBody, () =>
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
    }
}
