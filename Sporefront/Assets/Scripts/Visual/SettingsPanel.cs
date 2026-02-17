// ============================================================================
// FILE: Visual/SettingsPanel.cs
// PURPOSE: Full-screen modal for game settings — notification toggles, push
//          notification toggles, and gameplay options. Backed by PlayerPrefs.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class SettingsPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnClose;
        public event Action<string, bool> OnSettingChanged;

        // ================================================================
        // Settings Keys
        // ================================================================

        private const string TutorialHintsKey = "gameplay_tutorial_hints";
        private const string ConfirmDestructiveKey = "gameplay_confirm_destructive";

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private GameObject panel;
        private RectTransform contentRT;
        private Guid localPlayerID;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform, Guid playerID)
        {
            localPlayerID = playerID;

            // Semi-transparent backdrop
            backdrop = UIHelper.CreatePanel(canvasTransform, "SettingsBackdrop",
                new Color(0, 0, 0, 0.5f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // Main panel — centered 420x560
            panel = UIHelper.CreatePanel(backdrop.transform, "SettingsPanel", UIHelper.PanelBg);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(panelRT, 420, 560);

            // Header bar
            var headerBar = UIHelper.CreatePanel(panel.transform, "HeaderBar",
                SporefrontColors.ParchmentDark);
            var headerBarRT = headerBar.GetComponent<RectTransform>();
            headerBarRT.anchorMin = new Vector2(0, 1);
            headerBarRT.anchorMax = new Vector2(1, 1);
            headerBarRT.pivot = new Vector2(0.5f, 1);
            headerBarRT.offsetMin = new Vector2(0, -40);
            headerBarRT.offsetMax = Vector2.zero;

            var headerHLG = headerBar.AddComponent<HorizontalLayoutGroup>();
            headerHLG.spacing = 4f;
            headerHLG.padding = new RectOffset(12, 8, 4, 4);
            headerHLG.childForceExpandWidth = false;
            headerHLG.childForceExpandHeight = true;
            headerHLG.childControlWidth = false;
            headerHLG.childControlHeight = true;

            var titleLabel = UIHelper.CreateLabel(headerBar.transform, "Settings",
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var titleLE = titleLabel.gameObject.AddComponent<LayoutElement>();
            titleLE.flexibleWidth = 1;

            var doneBtn = UIHelper.CreateButton(headerBar.transform, "Done",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor, 12, Hide);
            var doneBtnLE = doneBtn.gameObject.AddComponent<LayoutElement>();
            doneBtnLE.preferredWidth = 60;

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "SettingsScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = Vector2.zero;
            scrollRT.offsetMax = new Vector2(0, -44);

            backdrop.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show()
        {
            Rebuild();
            backdrop.SetActive(true);
        }

        public void Hide()
        {
            backdrop.SetActive(false);
            OnClose?.Invoke();
        }

        public void Refresh()
        {
            if (!backdrop.activeSelf) return;
            Rebuild();
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild()
        {
            // Clear
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            // Section: In-Game Notifications
            BuildSectionHeader("Notifications");
            BuildToggleRow("Combat Alerts", "Alerts when armies or villagers are attacked",
                NotificationSettings.CombatAlertsEnabled, (val) =>
                {
                    NotificationSettings.CombatAlertsEnabled = val;
                    OnSettingChanged?.Invoke("notification_combat_alerts", val);
                });
            BuildToggleRow("Enemy Sightings", "Alerts when enemy units are spotted",
                NotificationSettings.EnemySightingsEnabled, (val) =>
                {
                    NotificationSettings.EnemySightingsEnabled = val;
                    OnSettingChanged?.Invoke("notification_enemy_sightings", val);
                });
            BuildToggleRow("Building Complete", "Alerts when construction or upgrades finish",
                NotificationSettings.BuildingUpdatesEnabled, (val) =>
                {
                    NotificationSettings.BuildingUpdatesEnabled = val;
                    OnSettingChanged?.Invoke("notification_building_updates", val);
                });
            BuildToggleRow("Training Complete", "Alerts when unit training finishes",
                NotificationSettings.TrainingUpdatesEnabled, (val) =>
                {
                    NotificationSettings.TrainingUpdatesEnabled = val;
                    OnSettingChanged?.Invoke("notification_training_updates", val);
                });
            BuildToggleRow("Resource Alerts", "Alerts for gathering, depletion, and full storage",
                NotificationSettings.ResourceAlertsEnabled, (val) =>
                {
                    NotificationSettings.ResourceAlertsEnabled = val;
                    OnSettingChanged?.Invoke("notification_resource_alerts", val);
                });
            BuildToggleRow("Research Complete", "Alerts when research finishes",
                NotificationSettings.ResearchUpdatesEnabled, (val) =>
                {
                    NotificationSettings.ResearchUpdatesEnabled = val;
                    OnSettingChanged?.Invoke("notification_research_updates", val);
                });

            UIHelper.CreateDivider(contentRT, null, 2);

            // Section: Push Notifications
            BuildSectionHeader("Push Notifications");
            BuildToggleRow("Push Notifications", "Enable push notifications when away",
                NotificationSettings.PushNotificationsEnabled, (val) =>
                {
                    NotificationSettings.PushNotificationsEnabled = val;
                    OnSettingChanged?.Invoke("push_notifications_enabled", val);
                    Rebuild(); // Rebuild to show/hide sub-toggles
                });

            if (NotificationSettings.PushNotificationsEnabled)
            {
                BuildToggleRow("  Combat Alerts", "Push when attacked",
                    NotificationSettings.PushCombatAlertsEnabled, (val) =>
                    {
                        NotificationSettings.PushCombatAlertsEnabled = val;
                        OnSettingChanged?.Invoke("push_combat_alerts", val);
                    });
                BuildToggleRow("  Enemy Sightings", "Push when enemies spotted",
                    NotificationSettings.PushEnemySightingsEnabled, (val) =>
                    {
                        NotificationSettings.PushEnemySightingsEnabled = val;
                        OnSettingChanged?.Invoke("push_enemy_sightings", val);
                    });
                BuildToggleRow("  Building Updates", "Push when buildings complete",
                    NotificationSettings.PushBuildingUpdatesEnabled, (val) =>
                    {
                        NotificationSettings.PushBuildingUpdatesEnabled = val;
                        OnSettingChanged?.Invoke("push_building_updates", val);
                    });
                BuildToggleRow("  Training Updates", "Push when training completes",
                    NotificationSettings.PushTrainingUpdatesEnabled, (val) =>
                    {
                        NotificationSettings.PushTrainingUpdatesEnabled = val;
                        OnSettingChanged?.Invoke("push_training_updates", val);
                    });
                BuildToggleRow("  Research Updates", "Push when research completes",
                    NotificationSettings.PushResearchUpdatesEnabled, (val) =>
                    {
                        NotificationSettings.PushResearchUpdatesEnabled = val;
                        OnSettingChanged?.Invoke("push_research_updates", val);
                    });
                BuildToggleRow("  Resource Alerts", "Push for resource warnings",
                    NotificationSettings.PushResourceAlertsEnabled, (val) =>
                    {
                        NotificationSettings.PushResourceAlertsEnabled = val;
                        OnSettingChanged?.Invoke("push_resource_alerts", val);
                    });
            }

            UIHelper.CreateDivider(contentRT, null, 2);

            // Section: Gameplay
            BuildSectionHeader("Gameplay");
            BuildToggleRow("Tutorial Hints", "Show helpful hints during gameplay",
                GetBool(TutorialHintsKey, true), (val) =>
                {
                    SetBool(TutorialHintsKey, val);
                    OnSettingChanged?.Invoke(TutorialHintsKey, val);
                });
            BuildToggleRow("Confirm Destructive Actions", "Ask for confirmation before demolish or disband",
                GetBool(ConfirmDestructiveKey, true), (val) =>
                {
                    SetBool(ConfirmDestructiveKey, val);
                    OnSettingChanged?.Invoke(ConfirmDestructiveKey, val);
                });
        }

        // ================================================================
        // Section Header
        // ================================================================

        private void BuildSectionHeader(string title)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, title,
                UIHelper.DefaultHeaderFontSize, UIHelper.HeaderTextColor,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 30;

            UIHelper.CreateDivider(contentRT);
        }

        // ================================================================
        // Toggle Row
        // ================================================================

        private void BuildToggleRow(string title, string subtitle, bool isOn, Action<bool> onChanged)
        {
            var row = UIHelper.CreatePanel(contentRT, "ToggleRow", Color.clear);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 48;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            // Text column
            var textCol = UIHelper.CreatePanel(row.transform, "TextCol", Color.clear);
            var textColLE = textCol.AddComponent<LayoutElement>();
            textColLE.flexibleWidth = 1;

            var textVLG = textCol.AddComponent<VerticalLayoutGroup>();
            textVLG.spacing = 1;
            textVLG.childForceExpandWidth = true;
            textVLG.childForceExpandHeight = false;
            textVLG.childControlWidth = true;
            textVLG.childControlHeight = false;

            var titleLabel = UIHelper.CreateLabel(textCol.transform, title, 13, UIHelper.BodyTextColor);
            var titleLE = titleLabel.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 20;

            var subtitleLabel = UIHelper.CreateLabel(textCol.transform, subtitle, 10,
                SporefrontColors.InkLight);
            var subtitleLE = subtitleLabel.gameObject.AddComponent<LayoutElement>();
            subtitleLE.preferredHeight = 16;

            // Toggle button
            Color toggleBg = isOn ? SporefrontColors.SporeGreen : SporefrontColors.InkFaded;
            string toggleText = isOn ? "ON" : "OFF";
            Color toggleTextColor = isOn ? UIHelper.HudTextColor : SporefrontColors.InkLight;

            var toggleBtn = UIHelper.CreateButton(row.transform, toggleText,
                toggleBg, toggleTextColor, 11, () =>
                {
                    bool newValue = !isOn;
                    onChanged?.Invoke(newValue);
                    Rebuild();
                });
            var toggleBtnLE = toggleBtn.gameObject.AddComponent<LayoutElement>();
            toggleBtnLE.preferredWidth = 50;
        }

        // ================================================================
        // PlayerPrefs Helpers
        // ================================================================

        private static bool GetBool(string key, bool defaultValue = true)
        {
            return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
        }

        private static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
