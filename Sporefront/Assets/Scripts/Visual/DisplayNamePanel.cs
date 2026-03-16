// ============================================================================
// FILE: Visual/DisplayNamePanel.cs
// PURPOSE: Full-screen panel for username claiming after sign-up.
//          Debounced availability check, validation rules display.
// ============================================================================

using System;
using UnityEngine;
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
            panel = UIHelper.CreatePanel(canvasTransform, "DisplayNamePanel", SporefrontColors.ParchmentDeep);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.StretchFull(panelRT);
            PopupTendrilDecorator.Attach(panelRT);

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
            // Center column
            var centerColumn = UIHelper.CreatePanel(panel.transform, "CenterColumn", Color.clear);
            var columnRT = centerColumn.GetComponent<RectTransform>();
            columnRT.anchorMin = new Vector2(0.5f, 0f);
            columnRT.anchorMax = new Vector2(0.5f, 1f);
            columnRT.pivot = new Vector2(0.5f, 0.5f);
            float columnWidth = 340f;
            columnRT.sizeDelta = new Vector2(columnWidth, 0f);
            columnRT.offsetMin = new Vector2(-columnWidth / 2f, 0f);
            columnRT.offsetMax = new Vector2(columnWidth / 2f, 0f);

            var vlg = centerColumn.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.padding = new RectOffset(20, 20, 0, 0);

            // Top spacer
            AddSpacer(centerColumn.transform, 0f, true);

            // Title
            var title = UIHelper.CreateLabel(centerColumn.transform, "Choose a Display Name",
                UIConstants.FontHeader, SporefrontColors.ParchmentLight, TextAnchor.MiddleCenter, true);
            var titleLE = title.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 50f;

            // Subtitle
            var subtitle = UIHelper.CreateLabel(centerColumn.transform,
                "3-20 characters, letters, numbers, underscores",
                UIConstants.FontCaption, UIHelper.InkMutedText, TextAnchor.MiddleCenter);
            var subtitleLE = subtitle.gameObject.AddComponent<LayoutElement>();
            subtitleLE.preferredHeight = 24f;

            AddSpacer(centerColumn.transform, 16f);

            // Username field
            UIHelper.CreateLabel(centerColumn.transform, "Display Name",
                UIConstants.FontSmall, UIHelper.InkBodyText, TextAnchor.MiddleLeft);
            usernameInput = CreateInputField(centerColumn.transform, "Enter display name...");
            usernameInput.characterLimit = 20;
            usernameInput.onValueChanged.AddListener((_) => { checkTimer = 0f; });

            AddSpacer(centerColumn.transform, 4f);

            // Availability label
            availabilityLabel = UIHelper.CreateLabel(centerColumn.transform, "",
                UIConstants.FontCaption, UIHelper.InkMutedText, TextAnchor.MiddleLeft);
            var availLE = availabilityLabel.gameObject.AddComponent<LayoutElement>();
            availLE.preferredHeight = 20f;

            AddSpacer(centerColumn.transform, 4f);

            // Status label
            statusLabel = UIHelper.CreateLabel(centerColumn.transform, "",
                UIConstants.FontCaption, SporefrontColors.SporeRed, TextAnchor.MiddleCenter);
            var statusLE = statusLabel.gameObject.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 20f;

            AddSpacer(centerColumn.transform, 10f);

            // Claim button
            claimButton = UIHelper.CreateButton(centerColumn.transform, "Claim Name",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor,
                UIHelper.DefaultBodyFontSize + 2, OnClaimClicked);
            var claimLE = claimButton.gameObject.AddComponent<LayoutElement>();
            claimLE.preferredHeight = 44f;
            claimButton.interactable = false;

            // Bottom spacer
            AddSpacer(centerColumn.transform, 0f, true);
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
            availabilityLabel.color = UIHelper.InkMutedText;

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
            statusLabel.color = UIHelper.InkMutedText;

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

        private InputField CreateInputField(Transform parent, string placeholder)
        {
            var inputBG = UIHelper.CreatePanel(parent, "InputBG", UIHelper.HudBg);
            var inputBGLE = inputBG.AddComponent<LayoutElement>();
            inputBGLE.preferredHeight = 36f;

            var input = inputBG.AddComponent<InputField>();

            var placeholderText = UIHelper.CreateLabel(inputBG.transform, placeholder,
                13, UIHelper.InkMutedText, TextAnchor.MiddleLeft);
            var placeholderRT = placeholderText.GetComponent<RectTransform>();
            UIHelper.StretchFull(placeholderRT);
            placeholderRT.offsetMin = new Vector2(8, 0);

            var textComponent = UIHelper.CreateLabel(inputBG.transform, "",
                13, UIHelper.HudTextColor, TextAnchor.MiddleLeft);
            var textRT = textComponent.GetComponent<RectTransform>();
            UIHelper.StretchFull(textRT);
            textRT.offsetMin = new Vector2(8, 0);

            input.textComponent = textComponent;
            input.placeholder = placeholderText;

            return input;
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
