// ============================================================================
// FILE: Visual/GenomeSelectionPanel.cs
// PURPOSE: Modal panel for selecting two AI genomes to spectate in AI-vs-AI
//          match. Port of GenomeSelectionViewController.swift
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
    public class GenomeSelectionPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<string, string> OnStartSpectating;  // genome1Name, genome2Name
        public event Action OnClose;

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private GameObject panel;
        private RectTransform contentRT;

        private List<AIGenome> genomes = new List<AIGenome>();
        private AIGenome selectedGenome1;
        private AIGenome selectedGenome2;
        private int currentSlot = 1;  // 1 or 2

        // Cached UI references
        private Text slot1Label;
        private Text slot2Label;
        private Text instructionLabel;
        private Button startButton;
        private RectTransform genomeListContentRT;

        // Colors
        private static readonly Color Slot1Color = new Color(0.4f, 0.6f, 1.0f, 1.0f);   // blue
        private static readonly Color Slot2Color = new Color(1.0f, 0.4f, 0.4f, 1.0f);   // red
        private static readonly Color UnselectedColor = SporefrontColors.InkFaded;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Semi-transparent backdrop
            backdrop = UIHelper.CreatePanel(canvasTransform, "GenomeSelectionBackdrop",
                new Color(0, 0, 0, 0.5f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(() => OnClose?.Invoke());

            // Centered panel 420x600
            panel = UIHelper.CreatePanel(backdrop.transform, "GenomeSelectionPanel", UIHelper.PanelBg);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(panelRT, 420, 600);

            BuildContent();
            backdrop.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show()
        {
            selectedGenome1 = null;
            selectedGenome2 = null;
            currentSlot = 1;
            genomes = GenomeLibrary.Instance.ListAll();
            Rebuild();
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
            // Inner vertical layout
            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Title
            var title = UIHelper.CreateLabel(panel.transform, "Spectate AI",
                22, UIHelper.HeaderTextColor, TextAnchor.MiddleCenter, true);
            var titleLE = title.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 36;

            UIHelper.CreateDivider(panel.transform, SporefrontColors.ParchmentShadow, 2f);

            // ---- Slot Container ----
            var slotContainer = UIHelper.CreatePanel(panel.transform, "SlotContainer", SporefrontColors.ParchmentDark);
            var slotContainerLE = slotContainer.AddComponent<LayoutElement>();
            slotContainerLE.preferredHeight = 65;

            // Slot layout: use horizontal row
            var slotRow = UIHelper.CreateHorizontalRow(slotContainer.transform, 60f, 4f);
            var slotRowRT = slotRow.GetComponent<RectTransform>();
            UIHelper.StretchFull(slotRowRT);
            slotRow.padding = new RectOffset(12, 12, 4, 4);
            slotRow.childAlignment = TextAnchor.MiddleCenter;

            // Slot 1 column
            var slot1Col = UIHelper.CreatePanel(slotRow.transform, "Slot1Col", Color.clear);
            var slot1ColLE = slot1Col.AddComponent<LayoutElement>();
            slot1ColLE.flexibleWidth = 1;
            slot1ColLE.preferredHeight = 55;
            var slot1VLG = slot1Col.AddComponent<VerticalLayoutGroup>();
            slot1VLG.spacing = 2;
            slot1VLG.childForceExpandWidth = true;
            slot1VLG.childForceExpandHeight = false;
            slot1VLG.childControlWidth = true;
            slot1VLG.childControlHeight = false;

            var slot1Header = UIHelper.CreateLabel(slot1Col.transform, "GENOME 1 (BLUE)",
                11, Slot1Color, TextAnchor.MiddleLeft);
            slot1Header.fontStyle = FontStyle.Bold;
            var slot1HeaderLE = slot1Header.gameObject.AddComponent<LayoutElement>();
            slot1HeaderLE.preferredHeight = 16;

            slot1Label = UIHelper.CreateLabel(slot1Col.transform, "[tap to select]",
                13, UnselectedColor, TextAnchor.MiddleLeft);
            var slot1LabelLE = slot1Label.gameObject.AddComponent<LayoutElement>();
            slot1LabelLE.preferredHeight = 20;

            // VS label
            var vsLabel = UIHelper.CreateLabel(slotRow.transform, "vs",
                14, SporefrontColors.InkFaded, TextAnchor.MiddleCenter);
            vsLabel.fontStyle = FontStyle.Bold;
            var vsLE = vsLabel.gameObject.AddComponent<LayoutElement>();
            vsLE.preferredWidth = 30;

            // Slot 2 column
            var slot2Col = UIHelper.CreatePanel(slotRow.transform, "Slot2Col", Color.clear);
            var slot2ColLE = slot2Col.AddComponent<LayoutElement>();
            slot2ColLE.flexibleWidth = 1;
            slot2ColLE.preferredHeight = 55;
            var slot2VLG = slot2Col.AddComponent<VerticalLayoutGroup>();
            slot2VLG.spacing = 2;
            slot2VLG.childForceExpandWidth = true;
            slot2VLG.childForceExpandHeight = false;
            slot2VLG.childControlWidth = true;
            slot2VLG.childControlHeight = false;

            var slot2Header = UIHelper.CreateLabel(slot2Col.transform, "GENOME 2 (RED)",
                11, Slot2Color, TextAnchor.MiddleRight);
            slot2Header.fontStyle = FontStyle.Bold;
            var slot2HeaderLE = slot2Header.gameObject.AddComponent<LayoutElement>();
            slot2HeaderLE.preferredHeight = 16;

            slot2Label = UIHelper.CreateLabel(slot2Col.transform, "[tap to select]",
                13, UnselectedColor, TextAnchor.MiddleRight);
            var slot2LabelLE = slot2Label.gameObject.AddComponent<LayoutElement>();
            slot2LabelLE.preferredHeight = 20;

            // Instruction label
            instructionLabel = UIHelper.CreateLabel(panel.transform, "Tap a genome to fill Slot 1",
                12, SporefrontColors.InkLight, TextAnchor.MiddleCenter);
            var instrLE = instructionLabel.gameObject.AddComponent<LayoutElement>();
            instrLE.preferredHeight = 22;

            // ---- Genome List ----
            var listPanel = UIHelper.CreatePanel(panel.transform, "GenomeListArea", SporefrontColors.ParchmentDark);
            var listPanelLE = listPanel.AddComponent<LayoutElement>();
            listPanelLE.flexibleHeight = 1;
            listPanelLE.minHeight = 200;

            var genomeScroll = UIHelper.CreateScrollView(listPanel.transform, "GenomePickScroll", out genomeListContentRT);
            var genomeScrollRT = genomeScroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(genomeScrollRT);

            // ---- Start Button ----
            startButton = UIHelper.CreateButton(panel.transform, "Start Spectating",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor, 16, () =>
                {
                    if (selectedGenome1 != null && selectedGenome2 != null)
                        OnStartSpectating?.Invoke(selectedGenome1.name, selectedGenome2.name);
                });
            startButton.interactable = false;
            var startLE = startButton.gameObject.AddComponent<LayoutElement>();
            startLE.preferredHeight = 50;

            // Close button
            var closeBtn = UIHelper.CreateButton(panel.transform, "Cancel",
                SporefrontColors.ParchmentDark, UIHelper.ButtonText, 13, () => OnClose?.Invoke());
            var closeLE = closeBtn.gameObject.AddComponent<LayoutElement>();
            closeLE.preferredHeight = 36;
        }

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild()
        {
            UpdateSlotLabels();
            UpdateStartButton();
            RebuildGenomeList();
        }

        private void RebuildGenomeList()
        {
            if (genomeListContentRT == null) return;

            // Clear
            for (int i = genomeListContentRT.childCount - 1; i >= 0; i--)
                Destroy(genomeListContentRT.GetChild(i).gameObject);

            if (genomes.Count == 0)
            {
                var emptyLabel = UIHelper.CreateLabel(genomeListContentRT, "No genomes saved.",
                    13, SporefrontColors.InkFaded, TextAnchor.MiddleCenter);
                var emptyLE = emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 40;
                return;
            }

            foreach (var genome in genomes)
            {
                BuildGenomePickRow(genome);
            }
        }

        private void BuildGenomePickRow(AIGenome genome)
        {
            UIHelper.CreateDivider(genomeListContentRT, null, 1f);

            // Determine selection state
            bool isSelected1 = selectedGenome1 != null && selectedGenome1.id == genome.id;
            bool isSelected2 = selectedGenome2 != null && selectedGenome2.id == genome.id;

            Color rowBg = Color.clear;
            string checkmark = "";
            if (isSelected1 && isSelected2)
                checkmark = " [1+2]";
            else if (isSelected1)
                checkmark = " [1]";
            else if (isSelected2)
                checkmark = " [2]";

            Color nameColor = isSelected1 ? Slot1Color : (isSelected2 ? Slot2Color : UIHelper.BodyTextColor);

            var row = UIHelper.CreatePanel(genomeListContentRT, "GenomePickRow", rowBg);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 48;

            var rowVLG = row.AddComponent<VerticalLayoutGroup>();
            rowVLG.spacing = 2;
            rowVLG.padding = new RectOffset(8, 8, 4, 4);
            rowVLG.childForceExpandWidth = true;
            rowVLG.childForceExpandHeight = false;
            rowVLG.childControlWidth = true;
            rowVLG.childControlHeight = false;

            // Name line
            string displayName = string.IsNullOrEmpty(genome.name) ? "Unnamed" : genome.name;
            var nameLabel = UIHelper.CreateLabel(rowVLG.transform, displayName + checkmark,
                13, nameColor, TextAnchor.MiddleLeft);
            nameLabel.fontStyle = FontStyle.Bold;
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 20;

            // Detail line
            string detail = string.Format("Gen {0} | Fitness: {1:F2} | WR: {2}%",
                genome.generation, genome.fitness, genome.WinRatePercent);
            var detailLabel = UIHelper.CreateLabel(rowVLG.transform, detail,
                11, SporefrontColors.InkFaded, TextAnchor.MiddleLeft);
            var detailLE = detailLabel.gameObject.AddComponent<LayoutElement>();
            detailLE.preferredHeight = 16;

            // Tap handler
            var rowBtn = row.AddComponent<Button>();
            rowBtn.transition = Selectable.Transition.ColorTint;
            row.GetComponent<Image>().color = rowBg;
            var capturedGenome = genome;
            rowBtn.onClick.AddListener(() => SelectGenome(capturedGenome));
        }

        // ================================================================
        // Selection Logic
        // ================================================================

        private void SelectGenome(AIGenome genome)
        {
            if (currentSlot == 1)
            {
                selectedGenome1 = genome;
                currentSlot = 2;
            }
            else
            {
                selectedGenome2 = genome;
                currentSlot = 1;
            }

            UpdateSlotLabels();
            UpdateStartButton();
            RebuildGenomeList();
        }

        private void UpdateSlotLabels()
        {
            if (slot1Label != null)
            {
                slot1Label.text = selectedGenome1 != null ? selectedGenome1.name : "[tap to select]";
                slot1Label.color = selectedGenome1 != null ? UIHelper.BodyTextColor : UnselectedColor;
            }

            if (slot2Label != null)
            {
                slot2Label.text = selectedGenome2 != null ? selectedGenome2.name : "[tap to select]";
                slot2Label.color = selectedGenome2 != null ? UIHelper.BodyTextColor : UnselectedColor;
            }

            if (instructionLabel != null)
                instructionLabel.text = $"Tap a genome to fill Slot {currentSlot}";
        }

        private void UpdateStartButton()
        {
            bool ready = selectedGenome1 != null && selectedGenome2 != null;
            if (startButton != null)
                startButton.interactable = ready;
        }
    }
}
