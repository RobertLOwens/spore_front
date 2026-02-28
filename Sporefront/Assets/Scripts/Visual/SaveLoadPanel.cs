// ============================================================================
// FILE: Visual/SaveLoadPanel.cs
// PURPOSE: Save/Load game UI panel with save slot listing
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Engine;

namespace Sporefront.Visual
{
    public class SaveLoadPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<string> OnLoadRequested;  // saveID
        public event Action<string> OnSaveRequested;  // saveName
        public event Action OnClose;

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private GameObject panel;
        private Transform slotContainer;
        private InputField saveNameInput;
        private bool isLoadMode;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Semi-transparent backdrop
            backdrop = UIHelper.CreatePanel(canvasTransform, "SaveLoadBackdrop",
                new Color(0, 0, 0, 0.4f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Close);

            // Center panel
            panel = UIHelper.CreatePanel(backdrop.transform, "SaveLoadPanel", UIHelper.PanelBg);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(panelRT, UIConstants.ModalMediumW, UIConstants.ModalMediumH);

            BuildContent();
            backdrop.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void ShowSave()
        {
            isLoadMode = false;
            RefreshSlots();
            if (saveNameInput != null)
                saveNameInput.gameObject.SetActive(true);
            backdrop.SetActive(true);
        }

        public void ShowLoad()
        {
            isLoadMode = true;
            RefreshSlots();
            if (saveNameInput != null)
                saveNameInput.gameObject.SetActive(false);
            backdrop.SetActive(true);
        }

        public void Hide()
        {
            backdrop.SetActive(false);
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Build Content
        // ================================================================

        private void BuildContent()
        {
            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.padding = new RectOffset(16, 16, 16, 16);

            // Title
            var title = UIHelper.CreateLabel(panel.transform, "Save / Load Game",
                20, UIHelper.HeaderTextColor, TextAnchor.MiddleCenter, true);
            var titleLE = title.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 36;

            UIHelper.CreateDivider(panel.transform, SporefrontColors.ParchmentShadow, 2f);

            // Save name input (only visible in save mode)
            var inputRow = new GameObject("InputRow", typeof(RectTransform), typeof(LayoutElement));
            inputRow.transform.SetParent(panel.transform, false);
            inputRow.GetComponent<LayoutElement>().preferredHeight = 36;

            var hlg = inputRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childForceExpandWidth = false;
            hlg.childControlWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlHeight = true;

            var inputBG = UIHelper.CreatePanel(inputRow.transform, "InputBG", UIHelper.HudBg);
            var inputBGLE = inputBG.AddComponent<LayoutElement>();
            inputBGLE.flexibleWidth = 1;
            saveNameInput = inputBG.AddComponent<InputField>();
            var placeholder = UIHelper.CreateLabel(inputBG.transform, "Enter save name...",
                13, SporefrontColors.InkFaded, TextAnchor.MiddleLeft);
            var placeholderRT = placeholder.GetComponent<RectTransform>();
            UIHelper.StretchFull(placeholderRT);
            placeholderRT.offsetMin = new Vector2(8, 0);
            var textComponent = UIHelper.CreateLabel(inputBG.transform, "",
                13, UIHelper.HudTextColor, TextAnchor.MiddleLeft);
            var textRT = textComponent.GetComponent<RectTransform>();
            UIHelper.StretchFull(textRT);
            textRT.offsetMin = new Vector2(8, 0);
            saveNameInput.textComponent = textComponent;
            saveNameInput.placeholder = placeholder;

            var saveBtn = UIHelper.CreateButton(inputRow.transform, "Save",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor, 13, OnSaveClicked);
            var saveBtnLE = saveBtn.gameObject.AddComponent<LayoutElement>();
            saveBtnLE.preferredWidth = 80;

            // Scroll area for save slots
            var scrollArea = new GameObject("ScrollArea", typeof(RectTransform), typeof(LayoutElement));
            scrollArea.transform.SetParent(panel.transform, false);
            scrollArea.GetComponent<LayoutElement>().flexibleHeight = 1;
            scrollArea.GetComponent<LayoutElement>().preferredHeight = 200;

            var mask = scrollArea.AddComponent<RectMask2D>();

            slotContainer = new GameObject("SlotContainer", typeof(RectTransform)).transform;
            slotContainer.SetParent(scrollArea.transform, false);
            var containerRT = slotContainer.GetComponent<RectTransform>();
            UIHelper.StretchFull(containerRT);

            var containerVLG = slotContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            containerVLG.spacing = 4;
            containerVLG.childForceExpandWidth = true;
            containerVLG.childForceExpandHeight = false;
            containerVLG.childControlWidth = true;
            containerVLG.childControlHeight = false;
            containerVLG.childAlignment = TextAnchor.UpperCenter;

            var csf = slotContainer.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Close button
            var closeBtn = UIHelper.CreateButton(panel.transform, "Close",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 14, Close);
            var closeBtnLE = closeBtn.gameObject.AddComponent<LayoutElement>();
            closeBtnLE.preferredHeight = 36;
        }

        // ================================================================
        // Slot Management
        // ================================================================

        private void RefreshSlots()
        {
            // Clear existing slots
            for (int i = slotContainer.childCount - 1; i >= 0; i--)
                Destroy(slotContainer.GetChild(i).gameObject);

            var saves = SaveManager.ListSaves();

            if (saves.Count == 0)
            {
                var emptyLabel = UIHelper.CreateLabel(slotContainer, "No saved games.",
                    13, SporefrontColors.InkFaded, TextAnchor.MiddleCenter);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 40;
                return;
            }

            foreach (var save in saves)
            {
                CreateSlot(save);
            }
        }

        private void CreateSlot(SaveSlotInfo info)
        {
            var slot = UIHelper.CreatePanel(slotContainer, "Slot_" + info.saveID, UIHelper.HudBg);
            var slotLE = slot.AddComponent<LayoutElement>();
            slotLE.preferredHeight = 52;

            var hlg = slot.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childForceExpandWidth = false;
            hlg.childControlWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlHeight = true;
            hlg.padding = new RectOffset(8, 8, 4, 4);

            // Info column
            var infoCol = new GameObject("Info", typeof(RectTransform), typeof(LayoutElement));
            infoCol.transform.SetParent(slot.transform, false);
            infoCol.GetComponent<LayoutElement>().flexibleWidth = 1;

            var infoVLG = infoCol.AddComponent<VerticalLayoutGroup>();
            infoVLG.childForceExpandHeight = false;
            infoVLG.childControlHeight = false;

            var nameLabel = UIHelper.CreateLabel(infoCol.transform, info.saveName,
                14, UIHelper.HudTextColor, TextAnchor.MiddleLeft, true);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 22;

            // Format date
            string dateDisplay = info.modifiedAt;
            if (DateTime.TryParse(info.modifiedAt, out var dt))
                dateDisplay = dt.ToLocalTime().ToString("MMM dd, yyyy HH:mm");

            string details = $"{dateDisplay}  |  {info.mapWidth}x{info.mapHeight}";
            var detailLabel = UIHelper.CreateLabel(infoCol.transform, details,
                11, SporefrontColors.InkFaded, TextAnchor.MiddleLeft);
            var detailLE = detailLabel.gameObject.AddComponent<LayoutElement>();
            detailLE.preferredHeight = 18;

            if (isLoadMode)
            {
                // Load button
                string capturedID = info.saveID;
                var loadBtn = UIHelper.CreateButton(slot.transform, "Load",
                    SporefrontColors.SporeGreen, UIHelper.HudTextColor, 13,
                    () => { Hide(); OnLoadRequested?.Invoke(capturedID); });
                var loadBtnLE = loadBtn.gameObject.AddComponent<LayoutElement>();
                loadBtnLE.preferredWidth = 70;
            }

            // Delete button
            string capturedDeleteID = info.saveID;
            var delBtn = UIHelper.CreateButton(slot.transform, "Del",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 12,
                () => { SaveManager.Delete(capturedDeleteID); RefreshSlots(); });
            var delBtnLE = delBtn.gameObject.AddComponent<LayoutElement>();
            delBtnLE.preferredWidth = 50;
        }

        // ================================================================
        // Actions
        // ================================================================

        private void OnSaveClicked()
        {
            string name = saveNameInput != null ? saveNameInput.text : "";
            if (string.IsNullOrWhiteSpace(name))
                name = $"Save {DateTime.Now:MMM dd HH:mm}";
            OnSaveRequested?.Invoke(name);
            Hide();
        }

        private void Close()
        {
            Hide();
            OnClose?.Invoke();
        }
    }
}
