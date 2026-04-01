// ============================================================================
// FILE: Visual/VSLoadingPanel.cs
// PURPOSE: Full-screen "Player vs Opponent" loading splash shown before each
//          game. Displays faction matchup, map info, and game mode with an
//          animated tendril divider down the center.
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class VSLoadingPanel : SporefrontPanel
    {
        // ================================================================
        // Config
        // ================================================================

        private const float DisplayDuration = 5.0f;
        private const float FadeOutDuration = 0.5f;
        private const float TendrilGrowDuration = 2.5f;
        private const float ContentFadeInDelay = 0.6f;
        private const float ContentFadeInDuration = 0.8f;

        // ================================================================
        // State
        // ================================================================

        private UITendrilRenderer tendrilRenderer;
        private CanvasGroup panelCanvasGroup;
        private CanvasGroup contentGroup;
        private new RectTransform panelRT;
        private Action onComplete;

        private readonly List<BranchTiming> branchTimings = new List<BranchTiming>();
        private float animationTime;
        private bool animating;

        private struct BranchTiming
        {
            public UITendrilRenderer.TendrilBranch branch;
            public float startDelay;
            public float duration;
        }

        // ================================================================
        // Static Factory
        // ================================================================

        /// <summary>
        /// Creates and shows the VS loading panel on the given canvas.
        /// Calls onComplete when the display duration elapses and the panel fades out.
        /// </summary>
        public static VSLoadingPanel Show(
            Transform canvasTransform,
            string playerName,
            FactionType playerFaction,
            string playerColorHex,
            string opponentName,
            FactionType opponentFaction,
            string opponentColorHex,
            string mapLabel,
            string gameModeLabel,
            Action onComplete)
        {
            // Root panel — full screen, on top of everything
            var go = new GameObject("VSLoadingPanel", typeof(RectTransform), typeof(CanvasRenderer));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvasTransform, false);
            UIHelper.StretchFull(rt);
            go.transform.SetAsLastSibling();

            // Override sorting so it renders above all other UI
            var overrideCanvas = go.AddComponent<Canvas>();
            overrideCanvas.overrideSorting = true;
            overrideCanvas.sortingOrder = 200;
            go.AddComponent<GraphicRaycaster>();

            // Background
            var bgImage = go.AddComponent<Image>();
            bgImage.color = SporefrontColors.BgDeep;

            // Canvas group for fade-out
            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            var panel = go.AddComponent<VSLoadingPanel>();
            panel.panelRT = rt;
            panel.panelCanvasGroup = cg;
            panel.onComplete = onComplete;

            panel.BuildLayout(
                playerName, playerFaction, playerColorHex,
                opponentName, opponentFaction, opponentColorHex,
                mapLabel, gameModeLabel);

            panel.StartCoroutine(panel.RunSequence());
            return panel;
        }

        // ================================================================
        // Layout
        // ================================================================

        private void BuildLayout(
            string playerName, FactionType playerFaction, string playerColorHex,
            string opponentName, FactionType opponentFaction, string opponentColorHex,
            string mapLabel, string gameModeLabel)
        {
            // Content container (fades in with delay)
            var contentGO = new GameObject("Content", typeof(RectTransform));
            var contentRT = (RectTransform)contentGO.transform;
            contentRT.SetParent(panelRT, false);
            UIHelper.StretchFull(contentRT);
            contentGroup = contentGO.AddComponent<CanvasGroup>();
            contentGroup.alpha = 0f;

            Color playerColor = SporefrontColors.ParsePlayerColor(playerColorHex);
            Color opponentColor = SporefrontColors.ParsePlayerColor(opponentColorHex);

            // ---- Left side: Player ----
            BuildPlayerCard(contentRT, true, playerName, playerFaction, playerColor);

            // ---- Right side: Opponent ----
            BuildPlayerCard(contentRT, false, opponentName, opponentFaction, opponentColor);

            // ---- Center: VS text ----
            BuildVSBadge(contentRT);

            // ---- Bottom strip: map + mode info ----
            BuildInfoStrip(contentRT, mapLabel, gameModeLabel);

            // ---- Center tendril divider (behind content, but on top of BG) ----
            BuildTendrilDivider();
        }

        private void BuildPlayerCard(RectTransform parent, bool isLeft,
            string playerName, FactionType faction, Color playerColor)
        {
            var cardGO = new GameObject(isLeft ? "LeftCard" : "RightCard", typeof(RectTransform));
            var cardRT = (RectTransform)cardGO.transform;
            cardRT.SetParent(parent, false);

            // Position: left or right half, vertically centered
            cardRT.anchorMin = isLeft ? new Vector2(0.02f, 0.25f) : new Vector2(0.52f, 0.25f);
            cardRT.anchorMax = isLeft ? new Vector2(0.48f, 0.85f) : new Vector2(0.98f, 0.85f);
            cardRT.offsetMin = Vector2.zero;
            cardRT.offsetMax = Vector2.zero;

            // Vertical layout
            var vlg = cardGO.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = isLeft ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            vlg.spacing = 12f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            // Color accent bar
            var barGO = new GameObject("ColorBar", typeof(RectTransform), typeof(Image));
            barGO.transform.SetParent(cardRT, false);
            var barImg = barGO.GetComponent<Image>();
            barImg.color = new Color(playerColor.r, playerColor.g, playerColor.b, 0.7f);
            var barLE = barGO.AddComponent<LayoutElement>();
            barLE.preferredHeight = 6f;

            // Player name
            var nameLabel = UIHelper.CreateLabel(cardRT, playerName,
                UIConstants.FontTitle, SporefrontColors.ParchmentLight,
                isLeft ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, true);
            var nameLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 44f;

            // Faction name
            var factionLabel = UIHelper.CreateLabel(cardRT, faction.DisplayName(),
                UIConstants.FontHeader,
                new Color(playerColor.r, playerColor.g, playerColor.b, 0.9f),
                isLeft ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, true);
            var factionLE = factionLabel.gameObject.AddComponent<LayoutElement>();
            factionLE.preferredHeight = 36f;

            // Faction description
            var descLabel = UIHelper.CreateLabel(cardRT, faction.Description(),
                UIConstants.FontSmall, SporefrontColors.ParchmentDeep,
                isLeft ? TextAnchor.UpperRight : TextAnchor.UpperLeft, false);
            descLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            descLabel.verticalOverflow = VerticalWrapMode.Truncate;
            var descLE = descLabel.gameObject.AddComponent<LayoutElement>();
            descLE.preferredHeight = 80f;

            // Faction bonuses
            var bonusLabel = UIHelper.CreateLabel(cardRT, faction.StartingBonusDescription(),
                UIConstants.FontCaption, SporefrontColors.ParchmentDark,
                isLeft ? TextAnchor.UpperRight : TextAnchor.UpperLeft, false);
            bonusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            bonusLabel.verticalOverflow = VerticalWrapMode.Truncate;
            var bonusLE = bonusLabel.gameObject.AddComponent<LayoutElement>();
            bonusLE.preferredHeight = 70f;
        }

        private void BuildVSBadge(RectTransform parent)
        {
            var vsGO = new GameObject("VSBadge", typeof(RectTransform));
            var vsRT = (RectTransform)vsGO.transform;
            vsRT.SetParent(parent, false);
            vsRT.anchorMin = new Vector2(0.4f, 0.42f);
            vsRT.anchorMax = new Vector2(0.6f, 0.62f);
            vsRT.offsetMin = Vector2.zero;
            vsRT.offsetMax = Vector2.zero;

            // Dark circle behind VS text
            var circleImg = vsGO.AddComponent<Image>();
            circleImg.color = new Color(SporefrontColors.BgDeep.r, SporefrontColors.BgDeep.g,
                SporefrontColors.BgDeep.b, 0.85f);

            var vsLabel = UIHelper.CreateLabel(vsRT, "VS",
                40, SporefrontColors.SporeRed,
                TextAnchor.MiddleCenter, true);
        }

        private void BuildInfoStrip(RectTransform parent, string mapLabel, string gameModeLabel)
        {
            var stripGO = new GameObject("InfoStrip", typeof(RectTransform));
            var stripRT = (RectTransform)stripGO.transform;
            stripRT.SetParent(parent, false);
            stripRT.anchorMin = new Vector2(0.1f, 0.08f);
            stripRT.anchorMax = new Vector2(0.9f, 0.20f);
            stripRT.offsetMin = Vector2.zero;
            stripRT.offsetMax = Vector2.zero;

            // Horizontal layout for info items
            var hlg = stripGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 40f;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            // Map info
            var mapInfoLabel = UIHelper.CreateLabel(stripRT, mapLabel,
                UIConstants.FontSubheader, SporefrontColors.ParchmentMid,
                TextAnchor.MiddleCenter, false);

            // Separator
            var sepGO = new GameObject("Sep", typeof(RectTransform), typeof(Image));
            sepGO.transform.SetParent(stripRT, false);
            var sepImg = sepGO.GetComponent<Image>();
            sepImg.color = SporefrontColors.InkFaded;
            var sepLE = sepGO.AddComponent<LayoutElement>();
            sepLE.preferredWidth = 2f;
            sepLE.preferredHeight = 24f;

            // Game mode
            var modeLabel = UIHelper.CreateLabel(stripRT, gameModeLabel,
                UIConstants.FontSubheader, SporefrontColors.ParchmentMid,
                TextAnchor.MiddleCenter, false);
        }

        // ================================================================
        // Tendril Divider — animated vertical growth down the center
        // ================================================================

        private void BuildTendrilDivider()
        {
            var tendrilGO = new GameObject("VSTendrils", typeof(RectTransform), typeof(CanvasRenderer));
            var tendrilRT = (RectTransform)tendrilGO.transform;
            tendrilRT.SetParent(panelRT, false);
            UIHelper.StretchFull(tendrilRT);
            tendrilRT.SetSiblingIndex(1); // Behind content, above BG

            var layout = tendrilGO.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;

            tendrilRenderer = tendrilGO.AddComponent<UITendrilRenderer>();
            tendrilRenderer.raycastTarget = false;

            var rect = panelRT.rect;
            float w = rect.width > 0 ? rect.width : Screen.width;
            float h = rect.height > 0 ? rect.height : Screen.height;

            float centerX = w * 0.5f;

            // Main trunk — vertical line from top to bottom
            var trunkPoints = new List<Vector2>();
            int segments = 10;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float y = h * (1f - t); // top to bottom
                float wobble = Mathf.Sin(t * Mathf.PI * 2.5f) * 12f;
                trunkPoints.Add(new Vector2(centerX + wobble, y));
            }

            var trunkStrandsRed = new List<UITendrilRenderer.StrandParams>
            {
                new UITendrilRenderer.StrandParams { width = 3.5f, alpha = 0.85f, waveFrequency = 1.2f, wavePhase = 0f },
                new UITendrilRenderer.StrandParams { width = 2.0f, alpha = 0.65f, waveFrequency = 2.0f, wavePhase = 1.5f }
            };
            var trunkBranchRed = tendrilRenderer.AddBranch(trunkPoints, trunkStrandsRed, 8f, 0.1f);
            trunkBranchRed.branchColor = SporefrontColors.InkRed;
            trunkBranchRed.glowAlpha = 0.15f;
            trunkBranchRed.growthProgress = 0f;
            branchTimings.Add(new BranchTiming { branch = trunkBranchRed, startDelay = 0f, duration = TendrilGrowDuration });

            var trunkStrandsGreen = new List<UITendrilRenderer.StrandParams>
            {
                new UITendrilRenderer.StrandParams { width = 3.0f, alpha = 0.80f, waveFrequency = 1.5f, wavePhase = 0.8f },
                new UITendrilRenderer.StrandParams { width = 1.8f, alpha = 0.60f, waveFrequency = 2.5f, wavePhase = 2.2f }
            };
            var trunkBranchGreen = tendrilRenderer.AddBranch(trunkPoints, trunkStrandsGreen, 8f, 0.1f);
            trunkBranchGreen.branchColor = SporefrontColors.InkGreen;
            trunkBranchGreen.glowAlpha = 0.12f;
            trunkBranchGreen.growthProgress = 0f;
            branchTimings.Add(new BranchTiming { branch = trunkBranchGreen, startDelay = 0.15f, duration = TendrilGrowDuration });

            // Side branches — grow outward from trunk at intervals
            float[] branchFractions = { 0.15f, 0.30f, 0.45f, 0.60f, 0.75f, 0.90f };
            for (int i = 0; i < branchFractions.Length; i++)
            {
                float frac = branchFractions[i];
                float branchY = h * (1f - frac);
                float baseX = centerX + Mathf.Sin(frac * Mathf.PI * 2.5f) * 12f;
                bool goLeft = (i % 2 == 0);
                float sign = goLeft ? -1f : 1f;

                // Main limb
                var limbPoints = new List<Vector2>();
                int limbSegs = 6;
                for (int j = 0; j <= limbSegs; j++)
                {
                    float lt = (float)j / limbSegs;
                    float lx = baseX + sign * lt * w * 0.22f;
                    float ly = branchY + Mathf.Sin(lt * Mathf.PI) * 25f * (goLeft ? 1f : -1f);
                    limbPoints.Add(new Vector2(lx, ly));
                }

                var limbStrands = new List<UITendrilRenderer.StrandParams>
                {
                    new UITendrilRenderer.StrandParams { width = 2.2f, alpha = 0.7f, waveFrequency = 1.8f, wavePhase = i * 0.7f }
                };
                Color limbColor = (i % 2 == 0) ? SporefrontColors.InkRed : SporefrontColors.InkGreen;
                var limb = tendrilRenderer.AddBranch(limbPoints, limbStrands, 6f, 0.2f);
                limb.branchColor = limbColor;
                limb.glowAlpha = 0.08f;
                limb.growthProgress = 0f;

                float limbDelay = frac * TendrilGrowDuration * 0.8f;
                branchTimings.Add(new BranchTiming { branch = limb, startDelay = limbDelay, duration = 1.2f });

                // Sub-tendrils off each limb
                for (int s = 0; s < 3; s++)
                {
                    float st = 0.3f + s * 0.25f;
                    int ptIdx = Mathf.Clamp(Mathf.RoundToInt(st * limbSegs), 0, limbSegs);
                    Vector2 origin = limbPoints[ptIdx];
                    float subSign = (s % 2 == 0) ? 1f : -1f;

                    var subPoints = new List<Vector2>();
                    int subSegs = 4;
                    for (int k = 0; k <= subSegs; k++)
                    {
                        float kt = (float)k / subSegs;
                        float sx = origin.x + sign * kt * w * 0.06f;
                        float sy = origin.y + subSign * kt * 30f;
                        subPoints.Add(new Vector2(sx, sy));
                    }

                    var subStrands = new List<UITendrilRenderer.StrandParams>
                    {
                        new UITendrilRenderer.StrandParams { width = 1.4f, alpha = 0.5f, waveFrequency = 2.5f, wavePhase = s * 1.2f }
                    };
                    var sub = tendrilRenderer.AddBranch(subPoints, subStrands, 4f, 0.25f);
                    sub.branchColor = limbColor;
                    sub.growthProgress = 0f;

                    branchTimings.Add(new BranchTiming
                    {
                        branch = sub,
                        startDelay = limbDelay + 0.5f + s * 0.2f,
                        duration = 0.8f
                    });
                }
            }

            tendrilRenderer.MarkDirty();
            animating = true;
        }

        // ================================================================
        // Animation
        // ================================================================

        private void Update()
        {
            if (!animating) return;

            animationTime += Time.unscaledDeltaTime;
            bool allDone = true;

            for (int i = 0; i < branchTimings.Count; i++)
            {
                var bt = branchTimings[i];
                float elapsed = animationTime - bt.startDelay;
                if (elapsed < 0f)
                {
                    bt.branch.growthProgress = 0f;
                    allDone = false;
                }
                else if (elapsed < bt.duration)
                {
                    float t = elapsed / bt.duration;
                    bt.branch.growthProgress = 1f - (1f - t) * (1f - t); // ease-out
                    allDone = false;
                }
                else
                {
                    bt.branch.growthProgress = 1f;
                }

                // Idle pulse
                bt.branch.idlePulsePhase = animationTime * 0.5f;
            }

            if (tendrilRenderer != null)
                tendrilRenderer.MarkDirty();

            if (allDone)
                animating = false;
        }

        private IEnumerator RunSequence()
        {
            // Fade in content after tendrils start growing
            float fadeInElapsed = 0f;
            while (fadeInElapsed < ContentFadeInDelay + ContentFadeInDuration)
            {
                fadeInElapsed += Time.unscaledDeltaTime;
                if (fadeInElapsed > ContentFadeInDelay)
                {
                    float t = (fadeInElapsed - ContentFadeInDelay) / ContentFadeInDuration;
                    contentGroup.alpha = Mathf.Clamp01(t);
                }
                yield return null;
            }
            contentGroup.alpha = 1f;

            // Wait remaining display time
            float remaining = DisplayDuration - ContentFadeInDelay - ContentFadeInDuration;
            if (remaining > 0f)
                yield return new WaitForSecondsRealtime(remaining);

            // Fade out entire panel
            float fadeOut = 0f;
            while (fadeOut < FadeOutDuration)
            {
                fadeOut += Time.unscaledDeltaTime;
                panelCanvasGroup.alpha = 1f - Mathf.Clamp01(fadeOut / FadeOutDuration);
                yield return null;
            }
            panelCanvasGroup.alpha = 0f;

            onComplete?.Invoke();
            Destroy(gameObject);
        }

        // ================================================================
        // Helper — format game mode display name
        // ================================================================

        public static string FormatGameMode(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Conquest: return "Conquest";
                case GameMode.Domination: return "Domination";
                case GameMode.CrookedDomination: return "Crooked Domination";
                case GameMode.Ring: return "Ring";
                default: return mode.ToString();
            }
        }

        public static string FormatMapLabel(string mapType, string mapSize)
        {
            return mapType + "  \u2022  " + mapSize;
        }
    }
}
