// ============================================================================
// FILE: Visual/DisplayNamePanel.cs
// PURPOSE: Full-screen panel for username claiming after sign-up.
//          Parchment-themed to match AuthPanel. Debounced availability check.
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sporefront.Engine;

namespace Sporefront.Visual
{
    public class DisplayNamePanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnUsernameSet;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private InputField usernameInput;
        private Text availabilityLabel;
        private Button claimButton;
        private Text statusLabel;

        private string lastCheckedName = "";
        private float checkTimer;
        private const float CheckDebounceSeconds = 0.5f;
        private bool isAvailable;
        private bool isChecking;
        private bool isClaiming;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Full-screen parchment panel (matches AuthPanel)
            panel = UIHelper.CreatePanel(canvasTransform, "DisplayNamePanel",
                SporefrontColors.ParchmentMid, cornerRadius: 0);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.StretchFull(panelRT);

            // Mycelium corner tendrils
            PopupTendrilDecorator.Attach(panelRT, seed: 88);

            // Parchment overlay
            UIHelper.AddParchmentOverlay(panel.transform, 0.25f);

            BuildContent();
            panel.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show()
        {
            isClaiming = false;
            isAvailable = false;
            isChecking = false;
            lastCheckedName = "";
            if (usernameInput != null) usernameInput.text = "";
            if (availabilityLabel != null) availabilityLabel.text = "";
            if (statusLabel != null) statusLabel.text = "";
            UpdateClaimButton();
            panel.SetActive(true);
        }

        public void Hide()
        {
            panel.SetActive(false);
        }

        public bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Update — debounced availability check
        // ================================================================

        private void Update()
        {
            if (!IsVisible || usernameInput == null) return;

            string current = usernameInput.text?.Trim() ?? "";

            if (current != lastCheckedName && !isChecking)
            {
                checkTimer += Time.deltaTime;
                if (checkTimer >= CheckDebounceSeconds)
                {
                    checkTimer = 0f;
                    CheckAvailability(current);
                }
            }
        }

        // ================================================================
        // Build Content
        // ================================================================

        private void BuildContent()
        {
            // Center column — matches AuthPanel layout
            var centerColumn = new GameObject("CenterColumn", typeof(RectTransform));
            centerColumn.transform.SetParent(panel.transform, false);
            var columnRT = centerColumn.GetComponent<RectTransform>();
            columnRT.anchorMin = new Vector2(0.5f, 0f);
            columnRT.anchorMax = new Vector2(0.5f, 1f);
            columnRT.pivot = new Vector2(0.5f, 0.5f);
            float columnWidth = 420f;
            columnRT.sizeDelta = new Vector2(columnWidth, 0f);
            columnRT.offsetMin = new Vector2(-columnWidth / 2f, 0f);
            columnRT.offsetMax = new Vector2(columnWidth / 2f, 0f);

            var vlg = centerColumn.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(30, 30, 0, 0);

            // Top spacer
            AddSpacer(centerColumn.transform, 0f, true);

            // Title
            var title = UIHelper.CreateLabel(centerColumn.transform, "Choose a Display Name",
                UIConstants.FontTitle, SporefrontColors.InkDark, TextAnchor.MiddleCenter, true);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            var titleLE = title.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 60f;

            // Subtitle / rules
            var subtitle = UIHelper.CreateLabel(centerColumn.transform,
                "3-20 characters, letters, numbers, underscores",
                UIConstants.FontBody, SporefrontColors.InkMid, TextAnchor.MiddleCenter);
            var subtitleLE = subtitle.gameObject.AddComponent<LayoutElement>();
            subtitleLE.preferredHeight = 28f;

            AddSpacer(centerColumn.transform, 24f);

            // ── Form card ──
            var cardGO = UIHelper.CreatePanel(centerColumn.transform, "FormCard",
                new Color(SporefrontColors.ParchmentDark.r,
                          SporefrontColors.ParchmentDark.g,
                          SporefrontColors.ParchmentDark.b, 0.50f), cornerRadius: 0);
            var cardOutline = cardGO.GetComponent<Outline>();
            if (cardOutline != null) UnityEngine.Object.Destroy(cardOutline);

            var cardVLG = cardGO.AddComponent<VerticalLayoutGroup>();
            cardVLG.spacing = 4f;
            cardVLG.childAlignment = TextAnchor.UpperCenter;
            cardVLG.childForceExpandWidth = true;
            cardVLG.childForceExpandHeight = false;
            cardVLG.childControlWidth = true;
            cardVLG.childControlHeight = true;
            cardVLG.padding = new RectOffset(24, 24, 20, 20);

            var cardCSF = cardGO.AddComponent<ContentSizeFitter>();
            cardCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Username label
            var nameLabel = UIHelper.CreateLabel(cardGO.transform, "Display Name",
                UIConstants.FontBody, SporefrontColors.InkDark, TextAnchor.MiddleLeft);
            var nameLabelLE = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLabelLE.preferredHeight = 26f;

            // Username input (parchment-themed)
            usernameInput = CreateStyledInputField(cardGO.transform, "Enter display name...");
            usernameInput.characterLimit = 20;
            usernameInput.onValueChanged.AddListener((_) => { checkTimer = 0f; });

            AddSpacer(cardGO.transform, 4f);

            // Availability label
            availabilityLabel = UIHelper.CreateLabel(cardGO.transform, "",
                UIConstants.FontSmall, SporefrontColors.InkFaded, TextAnchor.MiddleLeft);
            var availLE = availabilityLabel.gameObject.AddComponent<LayoutElement>();
            availLE.preferredHeight = 22f;

            AddSpacer(cardGO.transform, 4f);

            // Status label
            statusLabel = UIHelper.CreateLabel(cardGO.transform, "",
                UIConstants.FontSmall, SporefrontColors.SporeRed, TextAnchor.MiddleCenter);
            var statusLE = statusLabel.gameObject.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 22f;

            AddSpacer(cardGO.transform, 8f);

            // Claim button
            claimButton = UIHelper.CreateButton(cardGO.transform, "Claim Name",
                SporefrontColors.InkDark, SporefrontColors.ParchmentLight,
                UIConstants.FontBody + 2, OnClaimClicked);
            var claimLE = claimButton.gameObject.AddComponent<LayoutElement>();
            claimLE.preferredHeight = 48f;
            claimButton.interactable = false;

            // ── End card ──

            // Bottom spacer
            AddSpacer(centerColumn.transform, 0f, true);
        }

        // ================================================================
        // Styled Input Field (parchment-themed, matches AuthPanel)
        // ================================================================

        private InputField CreateStyledInputField(Transform parent, string placeholder)
        {
            var bgColor = new Color(
                SporefrontColors.ParchmentDeep.r * 0.85f,
                SporefrontColors.ParchmentDeep.g * 0.85f,
                SporefrontColors.ParchmentDeep.b * 0.85f,
                0.70f);

            var inputBG = UIHelper.CreatePanel(parent, "InputBG", bgColor, UIHelper.SmallCornerRadius);
            var inputBGLE = inputBG.AddComponent<LayoutElement>();
            inputBGLE.preferredHeight = 44f;

            var outline = inputBG.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(
                    SporefrontColors.InkFaded.r,
                    SporefrontColors.InkFaded.g,
                    SporefrontColors.InkFaded.b, 0.25f);
                outline.effectDistance = new Vector2(1f, -1f);
            }

            var input = inputBG.AddComponent<InputField>();

            var placeholderText = UIHelper.CreateLabel(inputBG.transform, placeholder,
                UIConstants.FontBody, SporefrontColors.InkFaded, TextAnchor.MiddleLeft);
            var placeholderRT = placeholderText.GetComponent<RectTransform>();
            UIHelper.StretchFull(placeholderRT);
            placeholderRT.offsetMin = new Vector2(12, 0);
            placeholderRT.offsetMax = new Vector2(-12, 0);

            var textComponent = UIHelper.CreateLabel(inputBG.transform, "",
                UIConstants.FontBody, SporefrontColors.InkBlack, TextAnchor.MiddleLeft);
            var textRT = textComponent.GetComponent<RectTransform>();
            UIHelper.StretchFull(textRT);
            textRT.offsetMin = new Vector2(12, 0);
            textRT.offsetMax = new Vector2(-12, 0);
            textComponent.raycastTarget = true;

            input.textComponent = textComponent;
            input.placeholder = placeholderText;

            return input;
        }

        // ================================================================
        // Availability Check
        // ================================================================

        private void CheckAvailability(string name)
        {
            lastCheckedName = name;

            if (string.IsNullOrEmpty(name))
            {
                availabilityLabel.text = "";
                isAvailable = false;
                UpdateClaimButton();
                return;
            }

            if (!AuthService.IsValidUsername(name))
            {
                availabilityLabel.text = "Invalid: use 3-20 chars, letters/numbers/underscores";
                availabilityLabel.color = SporefrontColors.SporeRed;
                isAvailable = false;
                UpdateClaimButton();
                return;
            }

            isChecking = true;
            availabilityLabel.text = "Checking...";
            availabilityLabel.color = SporefrontColors.InkFaded;

            AuthService.Instance.CheckUsernameAvailability(name, (available) =>
            {
                isChecking = false;

                // Only update if name hasn't changed since we started checking
                if (lastCheckedName != name) return;

                isAvailable = available;
                if (available)
                {
                    availabilityLabel.text = "Available!";
                    availabilityLabel.color = SporefrontColors.SporeGreen;
                }
                else
                {
                    availabilityLabel.text = "Already taken";
                    availabilityLabel.color = SporefrontColors.SporeRed;
                }
                UpdateClaimButton();
            });
        }

        // ================================================================
        // Claim
        // ================================================================

        private void OnClaimClicked()
        {
            if (isClaiming || !isAvailable) return;

            string name = usernameInput.text?.Trim();
            if (!AuthService.IsValidUsername(name)) return;

            isClaiming = true;
            claimButton.interactable = false;
            statusLabel.text = "Claiming name...";
            statusLabel.color = SporefrontColors.InkFaded;

            AuthService.Instance.ClaimUsername(name, (success, error) =>
            {
                isClaiming = false;

                if (success)
                {
                    OnUsernameSet?.Invoke();
                }
                else
                {
                    statusLabel.text = error ?? "Failed to claim name.";
                    statusLabel.color = SporefrontColors.SporeRed;
                    UpdateClaimButton();
                }
            });
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void UpdateClaimButton()
        {
            if (claimButton != null)
                claimButton.interactable = isAvailable && !isClaiming;
        }

        private void AddSpacer(Transform parent, float height, bool flexible = false)
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);
            var le = spacer.GetComponent<LayoutElement>();
            if (flexible)
                le.flexibleHeight = 1f;
            else
                le.preferredHeight = height;
        }
    }
}
