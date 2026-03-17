// ============================================================================
// FILE: Visual/TendrilWheelHUD.cs
// PURPOSE: Dual tendril wheel HUD — replaces the flat MenuBarPanel with two
//          quarter-circle arc button clusters anchored to bottom corners.
//          Right wheel = dual-ring (3 inner + 3 outer) action buttons,
//          Left wheel = 3 info buttons.
//          Includes keyboard shortcuts, tendril decorations, and bottom border.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sporefront.Visual
{
    public class TendrilWheelHUD : MonoBehaviour
    {
        // ================================================================
        // Events (same interface as MenuBarPanel)
        // ================================================================

        public event Action OnEntitiesClicked;
        public event Action OnBuildingsClicked;
        public event Action OnCommandersClicked;
        public event Action OnMilitaryClicked;
        public event Action OnResourcesClicked;
        public event Action OnResearchClicked;
        public event Action OnTrainingClicked;
        public event Action OnCombatClicked;

        // ================================================================
        // State
        // ================================================================

        private GameObject rootPanel;
        private WheelTendrilAnimator tendrilAnimator;

        // Right wheel — inner ring (Economy/Management)
        private WheelButton entitiesBtn;
        private WheelButton researchBtn;
        private WheelButton trainingBtn;
        private readonly List<WheelButton> rightInnerButtons = new List<WheelButton>();

        // Right wheel — outer ring (Military/Combat)
        private WheelButton commandersBtn;
        private WheelButton militaryBtn;
        private WheelButton combatBtn;
        private readonly List<WheelButton> rightOuterButtons = new List<WheelButton>();

        // Combined right list for toggle/badge management
        private readonly List<WheelButton> rightButtons = new List<WheelButton>();

        // Left wheel (info)
        private WheelButton resourcesBtn;
        private WheelButton armyBtn;
        private WheelButton buildingsBtn;
        private readonly List<WheelButton> leftButtons = new List<WheelButton>();

        // Active tracking
        private WheelButton activeRightButton;
        private WheelButton activeLeftButton;
        private bool keyBadgesVisible;

        // Button → external event mapping
        private readonly Dictionary<WheelButton, Action> buttonEventMap
            = new Dictionary<WheelButton, Action>();

        // ================================================================
        // Button Definitions — Dual Ring
        // ================================================================

        // Inner ring: Economy / Management
        private static readonly WheelButtonConfig[] RightInnerConfigs = new[]
        {
            new WheelButtonConfig { name = "Entities",  shortcutLabel = "E", iconResourcePath = "Icons/Action/entities",  isActionButton = true },
            new WheelButtonConfig { name = "Research",  shortcutLabel = "S", iconResourcePath = "Icons/Action/research",  isActionButton = true },
            new WheelButtonConfig { name = "Training",  shortcutLabel = "T", iconResourcePath = "Icons/Action/training",  isActionButton = true },
        };

        // Outer ring: Military / Combat
        private static readonly WheelButtonConfig[] RightOuterConfigs = new[]
        {
            new WheelButtonConfig { name = "Commanders", shortcutLabel = "C", iconResourcePath = "Icons/Action/commanders", isActionButton = true },
            new WheelButtonConfig { name = "Military",   shortcutLabel = "M", iconResourcePath = "Icons/Action/military",   isActionButton = true },
            new WheelButtonConfig { name = "Combat",     shortcutLabel = "X", iconResourcePath = "Icons/Action/combat",     isActionButton = true },
        };

        private static readonly WheelButtonConfig[] LeftConfigs = new[]
        {
            new WheelButtonConfig { name = "Resources", shortcutLabel = "R", iconResourcePath = "Icons/Info/resources", isActionButton = false },
            new WheelButtonConfig { name = "Army",      shortcutLabel = "A", iconResourcePath = "Icons/Info/army",      isActionButton = false },
            new WheelButtonConfig { name = "Buildings",  shortcutLabel = "V", iconResourcePath = "Icons/Info/buildings", isActionButton = false },
        };

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Root container — full screen overlay, no background
            rootPanel = new GameObject("TendrilWheelHUD", typeof(RectTransform));
            rootPanel.transform.SetParent(canvasTransform, false);
            var rootRT = rootPanel.GetComponent<RectTransform>();
            UIHelper.StretchFull(rootRT);

            // No CanvasGroup on root — buttons receive raycasts via their own
            // Images (raycastTarget=true), while tendril renderers and labels
            // already have raycastTarget=false so clicks pass through empty areas.

            // ---- Bottom Tendril Border ----
            var borderGO = CreateTendrilLayer(rootPanel.transform, "BottomBorder");
            var borderRT = borderGO.GetComponent<RectTransform>();
            borderRT.anchorMin = new Vector2(0f, 0f);
            borderRT.anchorMax = new Vector2(1f, 0f);
            borderRT.pivot = new Vector2(0.5f, 0f);
            borderRT.offsetMin = new Vector2(0f, 0f);
            borderRT.offsetMax = new Vector2(0f, UIConstants.WheelBorderHeight);
            var borderRenderer = borderGO.GetComponent<UITendrilRenderer>();

            // ---- Right Wheel Container (Actions — Dual Ring) ----
            float containerSize = UIConstants.WheelContainerSize;
            float padH = UIConstants.WheelCornerPaddingH;
            float padV = UIConstants.WheelCornerPaddingV;

            var rightContainerGO = new GameObject("RightWheel", typeof(RectTransform));
            rightContainerGO.transform.SetParent(rootPanel.transform, false);
            var rightContainerRT = rightContainerGO.GetComponent<RectTransform>();
            rightContainerRT.anchorMin = new Vector2(1f, 0f);
            rightContainerRT.anchorMax = new Vector2(1f, 0f);
            rightContainerRT.pivot = new Vector2(1f, 0f);
            rightContainerRT.sizeDelta = new Vector2(containerSize, containerSize);
            rightContainerRT.anchoredPosition = new Vector2(-padH, padV);

            // Tendril layer for right cluster
            var rightTendrilGO = CreateTendrilLayer(rightContainerGO.transform, "RightTendrils");
            var rightTendrilRT = rightTendrilGO.GetComponent<RectTransform>();
            UIHelper.StretchFull(rightTendrilRT);
            rightTendrilRT.pivot = new Vector2(0f, 0f); // Origin at bottom-left to match button coords
            var rightRenderer = rightTendrilGO.GetComponent<UITendrilRenderer>();

            // Right wheel buttons — hub is bottom-right corner of container
            Vector2 rightHub = new Vector2(containerSize, 0f);

            // -- Outer ring: Commanders, Military, Combat --
            var rightOuterPositions = new List<Vector2>();
            for (int i = 0; i < RightOuterConfigs.Length; i++)
            {
                Vector2 pos = GetButtonPosition(i, UIConstants.WheelRightOuterCount,
                    UIConstants.WheelRightOuterRadius,
                    UIConstants.WheelRightOuterStartAngle,
                    UIConstants.WheelRightOuterEndAngle,
                    rightHub);
                rightOuterPositions.Add(pos);

                var btn = CreateWheelButton(rightContainerGO.transform, RightOuterConfigs[i],
                    pos, labelOnLeft: true);
                rightOuterButtons.Add(btn);
                rightButtons.Add(btn);
            }

            // -- Inner ring: Entities, Research, Training --
            var rightInnerPositions = new List<Vector2>();
            for (int i = 0; i < RightInnerConfigs.Length; i++)
            {
                Vector2 pos = GetButtonPosition(i, UIConstants.WheelRightInnerCount,
                    UIConstants.WheelRightInnerRadius,
                    UIConstants.WheelRightInnerStartAngle,
                    UIConstants.WheelRightInnerEndAngle,
                    rightHub);
                rightInnerPositions.Add(pos);

                var btn = CreateWheelButton(rightContainerGO.transform, RightInnerConfigs[i],
                    pos, labelOnLeft: true);
                rightInnerButtons.Add(btn);
                rightButtons.Add(btn);
            }

            // Assign named button references
            commandersBtn = rightOuterButtons[0];
            militaryBtn   = rightOuterButtons[1];
            combatBtn     = rightOuterButtons[2];
            entitiesBtn   = rightInnerButtons[0];
            researchBtn   = rightInnerButtons[1];
            trainingBtn   = rightInnerButtons[2];

            // ---- Left Wheel Container (Info) ----
            var leftContainerGO = new GameObject("LeftWheel", typeof(RectTransform));
            leftContainerGO.transform.SetParent(rootPanel.transform, false);
            var leftContainerRT = leftContainerGO.GetComponent<RectTransform>();
            leftContainerRT.anchorMin = new Vector2(0f, 0f);
            leftContainerRT.anchorMax = new Vector2(0f, 0f);
            leftContainerRT.pivot = new Vector2(0f, 0f);
            leftContainerRT.sizeDelta = new Vector2(containerSize, containerSize);
            leftContainerRT.anchoredPosition = new Vector2(padH, padV);

            // Tendril layer for left cluster
            var leftTendrilGO = CreateTendrilLayer(leftContainerGO.transform, "LeftTendrils");
            var leftTendrilRT = leftTendrilGO.GetComponent<RectTransform>();
            UIHelper.StretchFull(leftTendrilRT);
            leftTendrilRT.pivot = new Vector2(0f, 0f); // Origin at bottom-left to match button coords
            var leftRenderer = leftTendrilGO.GetComponent<UITendrilRenderer>();

            // Left wheel buttons
            Vector2 leftHub = new Vector2(0f, 0f); // bottom-left of container
            var leftPositions = new List<Vector2>();

            for (int i = 0; i < LeftConfigs.Length; i++)
            {
                Vector2 pos = GetButtonPosition(i, UIConstants.WheelLeftButtonCount,
                    UIConstants.WheelLeftRadius,
                    UIConstants.WheelLeftStartAngle,
                    UIConstants.WheelLeftEndAngle,
                    leftHub);
                leftPositions.Add(pos);

                var btn = CreateWheelButton(leftContainerGO.transform, LeftConfigs[i],
                    pos, labelOnLeft: false);
                leftButtons.Add(btn);
            }

            resourcesBtn = leftButtons[0];
            armyBtn      = leftButtons[1];
            buildingsBtn = leftButtons[2];

            // ---- Side Labels ----
            CreateSideLabel(rootPanel.transform, "Actions", isRight: true);
            CreateSideLabel(rootPanel.transform, "Info", isRight: false);

            // ---- Tendril Animator ----
            var animatorGO = new GameObject("WheelTendrilAnimator", typeof(RectTransform));
            animatorGO.transform.SetParent(rootPanel.transform, false);
            tendrilAnimator = animatorGO.AddComponent<WheelTendrilAnimator>();
            tendrilAnimator.Initialize(rightRenderer, leftRenderer, borderRenderer,
                rightContainerRT, leftContainerRT);

            // ---- Wire Button Events ----
            // Map each button to its external event. Fired when button becomes active.
            buttonEventMap[entitiesBtn]   = () => OnEntitiesClicked?.Invoke();
            buttonEventMap[commandersBtn] = () => OnCommandersClicked?.Invoke();
            buttonEventMap[militaryBtn]   = () => OnMilitaryClicked?.Invoke();
            buttonEventMap[researchBtn]   = () => OnResearchClicked?.Invoke();
            buttonEventMap[trainingBtn]   = () => OnTrainingClicked?.Invoke();
            buttonEventMap[combatBtn]     = () => OnCombatClicked?.Invoke();
            buttonEventMap[resourcesBtn]  = () => OnResourcesClicked?.Invoke();
            buttonEventMap[armyBtn]       = () => OnMilitaryClicked?.Invoke();
            buttonEventMap[buildingsBtn]  = () => OnBuildingsClicked?.Invoke();

            // Wire button clicks to the toggle handler
            foreach (var btn in rightButtons)
            {
                var capturedBtn = btn;
                btn.OnClicked += () => ToggleButton(capturedBtn);
            }
            foreach (var btn in leftButtons)
            {
                var capturedBtn = btn;
                btn.OnClicked += () => ToggleButton(capturedBtn);
            }

            // Start hidden
            rootPanel.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show()
        {
            rootPanel.SetActive(true);

            // Get screen width for bottom border
            var canvasRT = rootPanel.transform.parent as RectTransform;
            float screenW = canvasRT != null ? canvasRT.rect.width : Screen.width;

            // Compute positions for tendril animator
            float containerSize = UIConstants.WheelContainerSize;
            Vector2 rightHub = new Vector2(containerSize, 0f);
            Vector2 leftHub = new Vector2(0f, 0f);

            // Right outer ring positions
            var rightOuterPositions = new List<Vector2>();
            for (int i = 0; i < RightOuterConfigs.Length; i++)
            {
                rightOuterPositions.Add(GetButtonPosition(i, UIConstants.WheelRightOuterCount,
                    UIConstants.WheelRightOuterRadius,
                    UIConstants.WheelRightOuterStartAngle,
                    UIConstants.WheelRightOuterEndAngle,
                    rightHub));
            }

            // Right inner ring positions
            var rightInnerPositions = new List<Vector2>();
            for (int i = 0; i < RightInnerConfigs.Length; i++)
            {
                rightInnerPositions.Add(GetButtonPosition(i, UIConstants.WheelRightInnerCount,
                    UIConstants.WheelRightInnerRadius,
                    UIConstants.WheelRightInnerStartAngle,
                    UIConstants.WheelRightInnerEndAngle,
                    rightHub));
            }

            // Left positions
            var leftPositions = new List<Vector2>();
            for (int i = 0; i < LeftConfigs.Length; i++)
            {
                leftPositions.Add(GetButtonPosition(i, UIConstants.WheelLeftButtonCount,
                    UIConstants.WheelLeftRadius,
                    UIConstants.WheelLeftStartAngle,
                    UIConstants.WheelLeftEndAngle,
                    leftHub));
            }

            // Start tendril growth
            if (tendrilAnimator != null)
            {
                if (tendrilAnimator.IsGrown)
                    tendrilAnimator.SnapToGrown();
                else
                    tendrilAnimator.BuildAll(rightOuterPositions, rightInnerPositions,
                        rightHub, leftPositions, leftHub, screenW);
            }

            // Pop-in animation for buttons — outer ring first, then inner ring
            for (int i = 0; i < rightOuterButtons.Count; i++)
                rightOuterButtons[i].PlayPopIn(i * UIConstants.WheelPopStagger);
            float innerDelay = rightOuterButtons.Count * UIConstants.WheelPopStagger + 0.04f;
            for (int i = 0; i < rightInnerButtons.Count; i++)
                rightInnerButtons[i].PlayPopIn(innerDelay + i * UIConstants.WheelPopStagger);

            for (int i = 0; i < leftButtons.Count; i++)
                leftButtons[i].PlayPopIn(i * UIConstants.WheelPopStagger);
        }

        public void Hide()
        {
            if (rootPanel != null) rootPanel.SetActive(false);
            DeactivateAll();
        }

        public bool IsVisible => rootPanel != null && rootPanel.activeSelf;

        /// <summary>
        /// Deactivates all buttons. Called when the player clicks on the game map.
        /// </summary>
        public void DeactivateAll()
        {
            if (activeRightButton != null)
            {
                activeRightButton.SetActive(false);
                activeRightButton = null;
            }
            if (activeLeftButton != null)
            {
                activeLeftButton.SetActive(false);
                activeLeftButton = null;
            }
        }

        // ================================================================
        // Update — Keyboard Shortcuts
        // ================================================================

        private void Update()
        {
            if (!IsVisible) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            // Tab hold — show/hide key badges
            if (kb.tabKey.wasPressedThisFrame)
            {
                keyBadgesVisible = true;
                SetAllKeyBadges(true);
            }
            if (kb.tabKey.wasReleasedThisFrame)
            {
                keyBadgesVisible = false;
                SetAllKeyBadges(false);
            }

            // Escape — deactivate all
            if (kb.escapeKey.wasPressedThisFrame)
            {
                DeactivateAll();
                return;
            }

            // Action shortcuts — inner ring (Economy)
            if (kb.eKey.wasPressedThisFrame) ToggleButton(entitiesBtn);
            if (kb.sKey.wasPressedThisFrame) ToggleButton(researchBtn);
            if (kb.tKey.wasPressedThisFrame) ToggleButton(trainingBtn);

            // Action shortcuts — outer ring (Military)
            if (kb.cKey.wasPressedThisFrame) ToggleButton(commandersBtn);
            if (kb.mKey.wasPressedThisFrame) ToggleButton(militaryBtn);
            if (kb.xKey.wasPressedThisFrame) ToggleButton(combatBtn);

            // Info shortcuts (left wheel)
            if (kb.rKey.wasPressedThisFrame) ToggleButton(resourcesBtn);
            if (kb.aKey.wasPressedThisFrame) ToggleButton(armyBtn);
            if (kb.vKey.wasPressedThisFrame) ToggleButton(buildingsBtn);
        }

        // ================================================================
        // Toggle / Active State
        // ================================================================

        private void ToggleButton(WheelButton btn)
        {
            if (btn == null) return;

            bool becameActive;

            if (btn.IsActionButton)
            {
                if (activeRightButton == btn)
                {
                    btn.SetActive(false);
                    activeRightButton = null;
                    becameActive = false;
                }
                else
                {
                    activeRightButton?.SetActive(false);
                    btn.SetActive(true);
                    activeRightButton = btn;
                    becameActive = true;
                }
            }
            else
            {
                if (activeLeftButton == btn)
                {
                    btn.SetActive(false);
                    activeLeftButton = null;
                    becameActive = false;
                }
                else
                {
                    activeLeftButton?.SetActive(false);
                    btn.SetActive(true);
                    activeLeftButton = btn;
                    becameActive = true;
                }
            }

            // Fire the external event when button becomes active (opens panel)
            if (becameActive && buttonEventMap.TryGetValue(btn, out var action))
            {
                action?.Invoke();
            }
        }

        // ================================================================
        // Button Factory
        // ================================================================

        private WheelButton CreateWheelButton(Transform parent, WheelButtonConfig config,
            Vector2 position, bool labelOnLeft)
        {
            float btnSize = UIConstants.WheelButtonSize;

            var btnGO = new GameObject($"Btn_{config.name}", typeof(RectTransform));
            btnGO.transform.SetParent(parent, false);

            var btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0f, 0f);
            btnRT.anchorMax = new Vector2(0f, 0f);
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.sizeDelta = new Vector2(btnSize, btnSize);
            btnRT.anchoredPosition = position;

            var btn = btnGO.AddComponent<WheelButton>();
            btn.Initialize(config, labelOnLeft);

            return btn;
        }

        // ================================================================
        // Side Labels
        // ================================================================

        private void CreateSideLabel(Transform parent, string text, bool isRight)
        {
            var labelGO = new GameObject($"SideLabel_{text}", typeof(RectTransform), typeof(Text));
            labelGO.transform.SetParent(parent, false);

            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(isRight ? 1f : 0f, 0f);
            labelRT.anchorMax = new Vector2(isRight ? 1f : 0f, 0f);
            labelRT.pivot = new Vector2(isRight ? 1f : 0f, 0f);
            labelRT.sizeDelta = new Vector2(80f, 16f);
            labelRT.anchoredPosition = new Vector2(isRight ? -14f : 14f, 8f);

            var label = labelGO.GetComponent<Text>();
            label.text = text.ToUpper();
            label.font = UIHelper.HeaderFont;
            label.fontSize = 11;
            label.color = new Color(
                SporefrontColors.InkFaded.r,
                SporefrontColors.InkFaded.g,
                SporefrontColors.InkFaded.b,
                0.4f);
            label.alignment = isRight ? TextAnchor.LowerRight : TextAnchor.LowerLeft;
            label.raycastTarget = false;
        }

        // ================================================================
        // Tendril Layer Factory
        // ================================================================

        private GameObject CreateTendrilLayer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<UITendrilRenderer>();
            renderer.raycastTarget = false;
            return go;
        }

        // ================================================================
        // Arc Positioning
        // ================================================================

        /// <summary>
        /// Computes the position of a button along a quarter-circle arc.
        /// Angles are in degrees measured counter-clockwise from the positive X axis.
        /// Y is up in Unity UI coordinates.
        /// </summary>
        private static Vector2 GetButtonPosition(int index, int count, float radius,
            float startAngle, float endAngle, Vector2 hub)
        {
            float t = count <= 1 ? 0.5f : (float)index / (count - 1);
            float angle = startAngle + t * (endAngle - startAngle);
            float rad = angle * Mathf.Deg2Rad;
            return new Vector2(
                hub.x + radius * Mathf.Cos(rad),
                hub.y + radius * Mathf.Sin(rad)
            );
        }

        // ================================================================
        // Key Badges
        // ================================================================

        private void SetAllKeyBadges(bool show)
        {
            foreach (var btn in rightButtons) btn.ShowKeyBadge(show);
            foreach (var btn in leftButtons) btn.ShowKeyBadge(show);
        }
    }
}
