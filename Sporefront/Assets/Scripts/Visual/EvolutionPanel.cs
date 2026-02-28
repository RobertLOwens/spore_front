// ============================================================================
// FILE: Visual/EvolutionPanel.cs
// PURPOSE: Full-screen panel for AI genome evolution configuration, monitoring,
//          and saved genome management. Port of EvolutionViewController.swift
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
    public class EvolutionPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<int, int, int> OnStartEvolution;   // popSize, gamesPerEval, maxGen
        public event Action OnStopEvolution;
        public event Action<string> OnApplyGenome;              // genomeName
        public event Action OnClose;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private RectTransform contentRT;

        private int populationSize = 32;
        private int gamesPerEval = 6;
        private int maxGenerations = 100;
        private bool isRunning;

        // Cached UI references
        private Text populationValueLabel;
        private Text gamesValueLabel;
        private Text generationsValueLabel;
        private Slider populationSlider;
        private Slider gamesSlider;
        private Slider generationsSlider;
        private Button startButton;
        private Button stopButton;
        private Button applyButton;
        private Text progressLabel;
        private Text historyText;
        private Image progressBarFill;
        private RectTransform genomeListContentRT;
        private Text genomeEmptyLabel;
        private List<AIGenome> savedGenomes = new List<AIGenome>();

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Full-screen panel
            panel = UIHelper.CreatePanel(canvasTransform, "EvolutionPanel", UIHelper.PanelBg);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.StretchFull(panelRT);

            // ScrollView fills entire panel
            var scroll = UIHelper.CreateScrollView(panel.transform, "EvolutionScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = new Vector2(0, 50); // space for close button
            scrollRT.offsetMax = Vector2.zero;

            // Close button at bottom
            var closeBtn = UIHelper.CreateButton(panel.transform, "< Back",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 14, () => OnClose?.Invoke());
            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0, 0);
            closeBtnRT.anchorMax = new Vector2(1, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.offsetMin = new Vector2(8, 6);
            closeBtnRT.offsetMax = new Vector2(-8, 44);

            BuildContent();
            panel.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show()
        {
            RefreshGenomeList();
            UpdateApplyButton();
            panel.SetActive(true);
        }

        public void Hide()
        {
            panel.SetActive(false);
        }

        public bool IsVisible => panel != null && panel.activeSelf;

        /// <summary>
        /// Called by the manager when a generation completes.
        /// </summary>
        public void UpdateProgress(string message)
        {
            if (progressLabel != null)
                progressLabel.text = message;
        }

        /// <summary>
        /// Append a line to the generation history text area.
        /// </summary>
        public void AppendHistory(int generation, double bestFitness, double winRate, int populationCount)
        {
            if (historyText == null) return;

            string line = string.Format("Gen {0,3} | Fitness: {1:F2} | Win: {2:F0}% | Pop: {3}",
                generation + 1, bestFitness, winRate * 100, populationCount);

            if (historyText.text == "No data yet.")
                historyText.text = line;
            else
                historyText.text += "\n" + line;
        }

        /// <summary>
        /// Update progress bar fill (0.0 to 1.0).
        /// </summary>
        public void SetProgress(float progress)
        {
            if (progressBarFill != null)
            {
                var fillRT = progressBarFill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(Mathf.Clamp01(progress), 1);
            }
        }

        /// <summary>
        /// Called when evolution completes.
        /// </summary>
        public void OnEvolutionComplete(string message)
        {
            UpdateProgress(message);
            SetRunningState(false);
            RefreshGenomeList();
            UpdateApplyButton();
        }

        /// <summary>
        /// Refresh the saved genomes list.
        /// </summary>
        public void RefreshGenomeList()
        {
            savedGenomes = GenomeLibrary.Instance.ListAll();
            RebuildGenomeList();
        }

        // ================================================================
        // Build Content
        // ================================================================

        private void BuildContent()
        {
            // Title
            var title = UIHelper.CreateLabel(contentRT, "Evolve AI",
                24, UIHelper.HeaderTextColor, TextAnchor.MiddleCenter, true);
            var titleLE = title.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 40;

            UIHelper.CreateDivider(contentRT, SporefrontColors.ParchmentShadow, 2f);

            // ---- Configuration Section ----
            AddSectionHeader("CONFIGURATION");

            // Population Size slider row
            BuildSliderRow("Population Size", 10, 100, populationSize,
                out populationSlider, out populationValueLabel,
                (val) => { populationSize = (int)val; populationValueLabel.text = populationSize.ToString(); });

            // Games per Evaluation slider row
            BuildSliderRow("Games/Evaluation", 1, 20, gamesPerEval,
                out gamesSlider, out gamesValueLabel,
                (val) => { gamesPerEval = (int)val; gamesValueLabel.text = gamesPerEval.ToString(); });

            // Max Generations slider row
            BuildSliderRow("Max Generations", 5, 100, maxGenerations,
                out generationsSlider, out generationsValueLabel,
                (val) => { maxGenerations = (int)val; generationsValueLabel.text = maxGenerations.ToString(); });

            UIHelper.CreateDivider(contentRT);

            // ---- Controls Section ----
            AddSectionHeader("CONTROLS");

            var buttonRow = UIHelper.CreateHorizontalRow(contentRT, 44f, 12f);

            startButton = UIHelper.CreateButton(buttonRow.transform, "Start",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor, 14, () =>
                {
                    SetRunningState(true);
                    historyText.text = "No data yet.";
                    OnStartEvolution?.Invoke(populationSize, gamesPerEval, maxGenerations);
                });
            var startLE = startButton.gameObject.AddComponent<LayoutElement>();
            startLE.flexibleWidth = 1;
            startLE.preferredHeight = 44;

            stopButton = UIHelper.CreateButton(buttonRow.transform, "Stop",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, 14, () =>
                {
                    progressLabel.text = "Stopping...";
                    OnStopEvolution?.Invoke();
                });
            stopButton.interactable = false;
            var stopLE = stopButton.gameObject.AddComponent<LayoutElement>();
            stopLE.flexibleWidth = 1;
            stopLE.preferredHeight = 44;

            // Apply Best Genome button
            applyButton = UIHelper.CreateButton(contentRT, "Apply Best Genome",
                SporefrontColors.SporeTeal, UIHelper.HudTextColor, 14, () =>
                {
                    var best = AIGenome.LoadBest("arabia");
                    if (best != null)
                        OnApplyGenome?.Invoke(best.name);
                });
            var applyLE = applyButton.gameObject.AddComponent<LayoutElement>();
            applyLE.preferredHeight = 40;

            UIHelper.CreateDivider(contentRT);

            // ---- Progress Section ----
            AddSectionHeader("PROGRESS");

            progressLabel = UIHelper.CreateLabel(contentRT, "Ready to start evolution.",
                13, SporefrontColors.InkLight, TextAnchor.MiddleLeft);
            var progressLabelLE = progressLabel.gameObject.AddComponent<LayoutElement>();
            progressLabelLE.preferredHeight = 22;

            // Progress bar
            var (bgImg, fillImg) = UIHelper.CreateProgressBar(contentRT, 16f,
                SporefrontColors.InkFaded, SporefrontColors.SporeAmber);
            progressBarFill = fillImg;
            var barLE = bgImg.gameObject.AddComponent<LayoutElement>();
            barLE.preferredHeight = 16;

            UIHelper.CreateDivider(contentRT);

            // ---- Generation History Section ----
            AddSectionHeader("GENERATION HISTORY");

            // History text area (scrollable label in a panel)
            var historyPanel = UIHelper.CreatePanel(contentRT, "HistoryPanel", SporefrontColors.ParchmentDark);
            var historyPanelLE = historyPanel.AddComponent<LayoutElement>();
            historyPanelLE.preferredHeight = 200;

            var historyScroll = UIHelper.CreateScrollView(historyPanel.transform, "HistoryScroll", out var historyContentRT);
            var historyScrollRT = historyScroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(historyScrollRT);

            historyText = UIHelper.CreateLabel(historyContentRT, "No data yet.",
                12, SporefrontColors.InkMid, TextAnchor.UpperLeft);
            historyText.horizontalOverflow = HorizontalWrapMode.Overflow;
            historyText.verticalOverflow = VerticalWrapMode.Overflow;
            var historyTextLE = historyText.gameObject.AddComponent<LayoutElement>();
            historyTextLE.flexibleWidth = 1;
            historyTextLE.flexibleHeight = 1;
            historyTextLE.minHeight = 190;
            var historyFitter = historyText.gameObject.AddComponent<ContentSizeFitter>();
            historyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            UIHelper.CreateDivider(contentRT);

            // ---- Saved Genomes Section ----
            AddSectionHeader("SAVED GENOMES");

            genomeEmptyLabel = UIHelper.CreateLabel(contentRT, "No genomes saved. Run evolution first.",
                13, SporefrontColors.InkFaded, TextAnchor.MiddleCenter);
            var emptyLE = genomeEmptyLabel.gameObject.AddComponent<LayoutElement>();
            emptyLE.preferredHeight = 30;

            // Genome list scroll area
            var genomeListPanel = UIHelper.CreatePanel(contentRT, "GenomeListPanel", SporefrontColors.ParchmentDark);
            var genomeListPanelLE = genomeListPanel.AddComponent<LayoutElement>();
            genomeListPanelLE.preferredHeight = 250;

            var genomeScroll = UIHelper.CreateScrollView(genomeListPanel.transform, "GenomeScroll", out genomeListContentRT);
            var genomeScrollRT = genomeScroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(genomeScrollRT);

            // Bottom spacer
            var bottomSpacer = new GameObject("BottomSpacer", typeof(RectTransform), typeof(LayoutElement));
            bottomSpacer.transform.SetParent(contentRT, false);
            bottomSpacer.GetComponent<LayoutElement>().preferredHeight = 20;
        }

        // ================================================================
        // Slider Row Builder
        // ================================================================

        private void BuildSliderRow(string label, float min, float max, int initialValue,
            out Slider slider, out Text valueLabel, Action<float> onChange)
        {
            var row = UIHelper.CreateHorizontalRow(contentRT, 30f, 8f);

            // Label
            var nameLabel = UIHelper.CreateLabel(row.transform, label, 13, UIHelper.BodyTextColor);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 140;
            nameLE.preferredHeight = 30;

            // Slider (don't pass onChange yet — value label must exist first)
            slider = UIHelper.CreateSlider(row.transform, min, max, true, null);
            var sliderLE = slider.gameObject.AddComponent<LayoutElement>();
            sliderLE.flexibleWidth = 1;
            sliderLE.preferredHeight = 30;

            // Value label (must exist before onChange fires)
            valueLabel = UIHelper.CreateLabel(row.transform, initialValue.ToString(),
                13, SporefrontColors.SporeAmber, TextAnchor.MiddleRight);
            var valLE = valueLabel.gameObject.AddComponent<LayoutElement>();
            valLE.preferredWidth = 40;
            valLE.preferredHeight = 30;

            // Now safe to set value and add listener
            slider.value = initialValue;
            if (onChange != null)
                slider.onValueChanged.AddListener((v) => onChange(v));
        }

        // ================================================================
        // Genome List
        // ================================================================

        private void RebuildGenomeList()
        {
            if (genomeListContentRT == null) return;

            // Clear existing rows
            for (int i = genomeListContentRT.childCount - 1; i >= 0; i--)
                Destroy(genomeListContentRT.GetChild(i).gameObject);

            bool hasGenomes = savedGenomes.Count > 0;
            if (genomeEmptyLabel != null)
                genomeEmptyLabel.gameObject.SetActive(!hasGenomes);

            foreach (var genome in savedGenomes)
            {
                BuildGenomeRow(genome);
            }
        }

        private void BuildGenomeRow(AIGenome genome)
        {
            UIHelper.CreateDivider(genomeListContentRT, null, 1f);

            var row = UIHelper.CreatePanel(genomeListContentRT, "GenomeRow", Color.clear);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 48;

            var vlg = row.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.padding = new RectOffset(8, 8, 4, 4);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Name line
            string displayName = string.IsNullOrEmpty(genome.name) ? "Unnamed Genome" : genome.name;
            var nameLabel = UIHelper.CreateLabel(vlg.transform, displayName,
                13, UIHelper.BodyTextColor, TextAnchor.MiddleLeft);
            nameLabel.fontStyle = FontStyle.Bold;
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 20;

            // Detail line
            string detail = string.Format("Gen {0} | Fitness: {1:F2} | WR: {2}%",
                genome.generation, genome.fitness, genome.WinRatePercent);
            var detailLabel = UIHelper.CreateLabel(vlg.transform, detail,
                11, SporefrontColors.InkFaded, TextAnchor.MiddleLeft);
            var detailLE = detailLabel.gameObject.AddComponent<LayoutElement>();
            detailLE.preferredHeight = 16;

            // Make row tappable for rename
            var rowBtn = row.AddComponent<Button>();
            rowBtn.transition = Selectable.Transition.ColorTint;
            var img = row.GetComponent<Image>();
            img.color = Color.clear;
            var capturedGenome = genome;
            rowBtn.onClick.AddListener(() => ShowRenameDialog(capturedGenome));
        }

        // ================================================================
        // Rename Dialog
        // ================================================================

        private void ShowRenameDialog(AIGenome genome)
        {
            // Simple rename: cycle through a few auto-generated names or apply a timestamp
            string newName = GenomeLibrary.Instance.AutoName(genome);
            if (genome.name == newName)
            {
                // Add timestamp to differentiate
                newName = genome.name + " " + DateTime.UtcNow.ToString("HH:mm");
            }
            GenomeLibrary.Instance.Rename(genome.id, newName);
            RefreshGenomeList();
        }

        // ================================================================
        // Running State
        // ================================================================

        private void SetRunningState(bool running)
        {
            isRunning = running;

            if (startButton != null) startButton.interactable = !running;
            if (stopButton != null) stopButton.interactable = running;
            if (populationSlider != null) populationSlider.interactable = !running;
            if (gamesSlider != null) gamesSlider.interactable = !running;
            if (generationsSlider != null) generationsSlider.interactable = !running;
        }

        private void UpdateApplyButton()
        {
            if (applyButton == null) return;
            bool hasGenome = AIGenome.LoadBest("arabia") != null;
            applyButton.interactable = hasGenome;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void AddSectionHeader(string title)
        {
            var label = UIHelper.CreateLabel(contentRT, title,
                11, SporefrontColors.InkFaded, TextAnchor.MiddleLeft);
            label.fontStyle = FontStyle.Bold;
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 22;
        }
    }
}
