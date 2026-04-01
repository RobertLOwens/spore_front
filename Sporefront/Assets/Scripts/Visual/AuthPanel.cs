// ============================================================================
// FILE: Visual/AuthPanel.cs
// PURPOSE: Full-screen sign in / register panel with email + password fields.
//          Parchment-themed UI matching GameSetupPanel style with tendrils,
//          warm input fields, and elegant layout.
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sporefront.Engine;

namespace Sporefront.Visual
{
    public class AuthPanel : SporefrontPanel
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnAuthSuccess;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;

        private InputField emailInput;
        private InputField passwordInput;
        private InputField confirmPasswordInput;
        private GameObject confirmPasswordGroup;
        private Text statusLabel;
        private Button submitButton;
        private Text submitButtonLabel;
        private Button toggleButton;
        private Text toggleLabel;
        private Button forgotPasswordButton;
        private GameObject forgotPasswordRow;
        private Button googleButton;

        private bool isRegisterMode;
        private bool isProcessing;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Full-screen parchment panel (same base as GameSetupPanel)
            panel = UIHelper.CreatePanel(canvasTransform, "AuthPanel",
                SporefrontColors.ParchmentMid, cornerRadius: 0);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.StretchFull(panelRT);

            // Mycelium corner tendrils (same decorator used on modals)
            PopupTendrilDecorator.Attach(panelRT, seed: 77);

            // Parchment overlay on top of tendrils, below content
            UIHelper.AddParchmentOverlay(panel.transform, 0.25f);

            BuildContent();
            panel.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public override void Show()
        {
            isRegisterMode = false;
            isProcessing = false;
            ClearFields();
            UpdateModeDisplay();
            panel.SetActive(true);
        }

        public override void Hide()
        {
            panel.SetActive(false);
        }

        public new bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Build Content
        // ================================================================

        private void BuildContent()
        {
            // Center column — same pattern as GameSetupPanel mode select
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

            // Top flexible spacer
            AddSpacer(centerColumn.transform, 0f, true);

            // ── Title ──
            var title = UIHelper.CreateLabel(centerColumn.transform, "S P O R E F R O N T",
                48, SporefrontColors.InkDark, TextAnchor.MiddleCenter, true);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            var titleLE = title.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 70f;

            // Subtitle
            var subtitle = UIHelper.CreateLabel(centerColumn.transform, "Sign in to play",
                UIConstants.FontSubheader, SporefrontColors.InkMid, TextAnchor.MiddleCenter);
            var subtitleLE = subtitle.gameObject.AddComponent<LayoutElement>();
            subtitleLE.preferredHeight = 30f;

            AddSpacer(centerColumn.transform, 16f);

            // ── Form card with semi-transparent backdrop ──
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

            // Email label
            var emailLabel = UIHelper.CreateLabel(cardGO.transform, "Email",
                UIConstants.FontBody, SporefrontColors.InkDark, TextAnchor.MiddleLeft);
            var emailLabelLE = emailLabel.gameObject.AddComponent<LayoutElement>();
            emailLabelLE.preferredHeight = 26f;

            // Email input
            emailInput = CreateStyledInputField(cardGO.transform, "Enter email...");
            emailInput.contentType = InputField.ContentType.EmailAddress;

            AddSpacer(cardGO.transform, 8f);

            // Password label
            var passLabel = UIHelper.CreateLabel(cardGO.transform, "Password",
                UIConstants.FontBody, SporefrontColors.InkDark, TextAnchor.MiddleLeft);
            var passLabelLE = passLabel.gameObject.AddComponent<LayoutElement>();
            passLabelLE.preferredHeight = 26f;

            // Password input
            passwordInput = CreateStyledInputField(cardGO.transform, "Enter password...");
            passwordInput.contentType = InputField.ContentType.Password;

            // ── Confirm password group (register mode only) ──
            confirmPasswordGroup = new GameObject("ConfirmPasswordGroup", typeof(RectTransform));
            confirmPasswordGroup.transform.SetParent(cardGO.transform, false);
            var cpVLG = confirmPasswordGroup.AddComponent<VerticalLayoutGroup>();
            cpVLG.spacing = 4f;
            cpVLG.childForceExpandWidth = true;
            cpVLG.childForceExpandHeight = false;
            cpVLG.childControlWidth = true;
            cpVLG.childControlHeight = true;
            var cpCSF = confirmPasswordGroup.AddComponent<ContentSizeFitter>();
            cpCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddSpacer(confirmPasswordGroup.transform, 8f);

            var confirmLabel = UIHelper.CreateLabel(confirmPasswordGroup.transform, "Confirm Password",
                UIConstants.FontBody, SporefrontColors.InkDark, TextAnchor.MiddleLeft);
            var confirmLabelLE = confirmLabel.gameObject.AddComponent<LayoutElement>();
            confirmLabelLE.preferredHeight = 26f;

            confirmPasswordInput = CreateStyledInputField(confirmPasswordGroup.transform, "Confirm password...");
            confirmPasswordInput.contentType = InputField.ContentType.Password;

            AddSpacer(cardGO.transform, 6f);

            // ── Status label ──
            statusLabel = UIHelper.CreateLabel(cardGO.transform, "",
                UIConstants.FontSmall, SporefrontColors.SporeRed, TextAnchor.MiddleCenter);
            var statusLE = statusLabel.gameObject.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 22f;

            AddSpacer(cardGO.transform, 4f);

            // ── Submit button ──
            submitButton = UIHelper.CreateButton(cardGO.transform, "Sign In",
                SporefrontColors.InkDark, SporefrontColors.ParchmentLight,
                UIConstants.FontBody + 2, OnSubmitClicked);
            var submitLE = submitButton.gameObject.AddComponent<LayoutElement>();
            submitLE.preferredHeight = 48f;
            submitButtonLabel = submitButton.GetComponentInChildren<Text>();

            // ── Forgot password (sign in only) ──
            forgotPasswordRow = new GameObject("ForgotPasswordRow", typeof(RectTransform));
            forgotPasswordRow.transform.SetParent(cardGO.transform, false);
            var fpLE = forgotPasswordRow.AddComponent<LayoutElement>();
            fpLE.preferredHeight = 36f;

            forgotPasswordButton = UIHelper.CreateButton(forgotPasswordRow.transform, "Forgot Password?",
                Color.clear, SporefrontColors.InkBlack, UIConstants.FontBody, OnForgotPasswordClicked);
            var fpBtnRT = forgotPasswordButton.GetComponent<RectTransform>();
            UIHelper.StretchFull(fpBtnRT);
            // Underline-style: make text clearly clickable
            var fpText = forgotPasswordButton.GetComponentInChildren<Text>();
            fpText.fontStyle = FontStyle.BoldAndItalic;
            AddTextHover(forgotPasswordButton.gameObject, fpText,
                SporefrontColors.InkBlack, SporefrontColors.SporeRed);

            // ── End of card ──

            AddSpacer(centerColumn.transform, 12f);

            // ── "or" divider ──
            BuildOrDivider(centerColumn.transform);

            AddSpacer(centerColumn.transform, 12f);

            // ── Google Sign-In button (mobile only) ──
            googleButton = UIHelper.CreateButton(centerColumn.transform,
                "Sign in with Google",
                SporefrontColors.ParchmentDark, SporefrontColors.InkDark,
                UIConstants.FontBody, OnGoogleSignInClicked);
            var googleLE = googleButton.gameObject.AddComponent<LayoutElement>();
            googleLE.preferredHeight = 48f;

            // Hide on desktop/editor — Firebase federated auth requires mobile platform
            if (Application.platform != RuntimePlatform.Android &&
                Application.platform != RuntimePlatform.IPhonePlayer)
            {
                googleButton.gameObject.SetActive(false);
            }

            AddSpacer(centerColumn.transform, 12f);

            // ── Divider ──
            UIHelper.CreateDivider(centerColumn.transform,
                new Color(SporefrontColors.InkFaded.r,
                          SporefrontColors.InkFaded.g,
                          SporefrontColors.InkFaded.b, 0.3f), 1f);

            AddSpacer(centerColumn.transform, 8f);

            // ── Toggle sign in / register ──
            toggleButton = UIHelper.CreateButton(centerColumn.transform,
                "Don't have an account? Register",
                SporefrontColors.ParchmentDark, SporefrontColors.InkBlack,
                UIConstants.FontBody + 2, OnToggleClicked);
            toggleLabel = toggleButton.GetComponentInChildren<Text>();
            toggleLabel.fontStyle = FontStyle.Bold;
            var toggleLE = toggleButton.gameObject.AddComponent<LayoutElement>();
            toggleLE.preferredHeight = 44f;
            AddTextHover(toggleButton.gameObject, toggleLabel,
                SporefrontColors.InkBlack, SporefrontColors.SporeRed);

            // Bottom flexible spacer
            AddSpacer(centerColumn.transform, 0f, true);
        }

        // ================================================================
        // Styled Input Field (parchment-themed)
        // ================================================================

        private InputField CreateStyledInputField(Transform parent, string placeholder)
        {
            // Warm, slightly recessed input field — parchment tones instead of dark HUD
            var bgColor = new Color(
                SporefrontColors.ParchmentDeep.r * 0.85f,
                SporefrontColors.ParchmentDeep.g * 0.85f,
                SporefrontColors.ParchmentDeep.b * 0.85f,
                0.70f);

            var inputBG = UIHelper.CreatePanel(parent, "InputBG", bgColor, UIHelper.SmallCornerRadius);
            var inputBGLE = inputBG.AddComponent<LayoutElement>();
            inputBGLE.preferredHeight = 44f;

            // Tweak the outline to a warm subtle border
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

            // Placeholder text
            var placeholderText = UIHelper.CreateLabel(inputBG.transform, placeholder,
                UIConstants.FontBody, SporefrontColors.InkFaded, TextAnchor.MiddleLeft);
            var placeholderRT = placeholderText.GetComponent<RectTransform>();
            UIHelper.StretchFull(placeholderRT);
            placeholderRT.offsetMin = new Vector2(12, 0);
            placeholderRT.offsetMax = new Vector2(-12, 0);

            // Input text (dark ink on parchment)
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
        // Mode Switching
        // ================================================================

        private void UpdateModeDisplay()
        {
            if (isRegisterMode)
            {
                confirmPasswordGroup.SetActive(true);
                forgotPasswordRow.SetActive(false);
                submitButtonLabel.text = "Register";
                toggleLabel.text = "Already have an account? Sign In";
            }
            else
            {
                confirmPasswordGroup.SetActive(false);
                forgotPasswordRow.SetActive(true);
                submitButtonLabel.text = "Sign In";
                toggleLabel.text = "Don't have an account? Register";
            }
            statusLabel.text = "";
        }

        // ================================================================
        // Actions
        // ================================================================

        private void OnSubmitClicked()
        {
            if (isProcessing) return;

            string email = emailInput.text?.Trim();
            string password = passwordInput.text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ShowStatus("Please fill in all fields.", SporefrontColors.SporeRed);
                return;
            }

            if (isRegisterMode)
            {
                string confirmPassword = confirmPasswordInput.text;
                if (password != confirmPassword)
                {
                    ShowStatus("Passwords do not match.", SporefrontColors.SporeRed);
                    return;
                }
                if (password.Length < 6)
                {
                    ShowStatus("Password must be at least 6 characters.", SporefrontColors.SporeRed);
                    return;
                }

                isProcessing = true;
                submitButton.interactable = false;
                ShowStatus("Creating account...", SporefrontColors.InkFaded);

                AuthService.Instance.SignUp(email, password, (success, error) =>
                {
                    isProcessing = false;
                    submitButton.interactable = true;

                    if (success)
                    {
                        OnAuthSuccess?.Invoke();
                    }
                    else
                    {
                        ShowStatus(error ?? "Registration failed.", SporefrontColors.SporeRed);
                    }
                });
            }
            else
            {
                isProcessing = true;
                submitButton.interactable = false;
                ShowStatus("Signing in...", SporefrontColors.InkFaded);

                AuthService.Instance.SignIn(email, password, (success, error) =>
                {
                    isProcessing = false;
                    submitButton.interactable = true;

                    if (success)
                    {
                        OnAuthSuccess?.Invoke();
                    }
                    else
                    {
                        ShowStatus(error ?? "Sign in failed.", SporefrontColors.SporeRed);
                    }
                });
            }
        }

        private void OnForgotPasswordClicked()
        {
            string email = emailInput.text?.Trim();
            if (string.IsNullOrEmpty(email))
            {
                ShowStatus("Enter your email above, then tap Forgot Password.", SporefrontColors.SporeRed);
                return;
            }

            ShowStatus("Sending reset email...", SporefrontColors.InkFaded);
            AuthService.Instance.SendPasswordReset(email, (success, error) =>
            {
                if (success)
                    ShowStatus("Reset email sent! Check your inbox.", SporefrontColors.SporeGreen);
                else
                    ShowStatus(error ?? "Failed to send reset email.", SporefrontColors.SporeRed);
            });
        }

        private void OnGoogleSignInClicked()
        {
            if (isProcessing) return;

            // Google Sign-In via Firebase federated auth only works on mobile (iOS/Android).
            // On desktop/Editor, show a helpful message directing users to email/password.
            if (Application.platform != RuntimePlatform.Android &&
                Application.platform != RuntimePlatform.IPhonePlayer)
            {
                ShowStatus("Google sign-in is only available on mobile. Please use email/password.",
                    SporefrontColors.InkFaded);
                return;
            }

            isProcessing = true;
            submitButton.interactable = false;
            googleButton.interactable = false;
            ShowStatus("Signing in with Google...", SporefrontColors.InkFaded);

            AuthService.Instance.SignInWithGoogle((success, error) =>
            {
                isProcessing = false;
                submitButton.interactable = true;
                googleButton.interactable = true;

                if (success)
                {
                    OnAuthSuccess?.Invoke();
                }
                else
                {
                    ShowStatus(error ?? "Google sign in failed.", SporefrontColors.SporeRed);
                }
            });
        }

        private void OnToggleClicked()
        {
            isRegisterMode = !isRegisterMode;
            UpdateModeDisplay();
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void ShowStatus(string message, Color color)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
                statusLabel.color = color;
            }
        }

        private void ClearFields()
        {
            if (emailInput != null) emailInput.text = "";
            if (passwordInput != null) passwordInput.text = "";
            if (confirmPasswordInput != null) confirmPasswordInput.text = "";
            if (statusLabel != null) statusLabel.text = "";
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

        private void BuildOrDivider(Transform parent)
        {
            var dividerColor = new Color(
                SporefrontColors.InkFaded.r,
                SporefrontColors.InkFaded.g,
                SporefrontColors.InkFaded.b, 0.3f);

            var row = new GameObject("OrDivider", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            var rowHLG = row.GetComponent<HorizontalLayoutGroup>();
            rowHLG.spacing = 12f;
            rowHLG.childAlignment = TextAnchor.MiddleCenter;
            rowHLG.childForceExpandWidth = false;
            rowHLG.childForceExpandHeight = false;
            rowHLG.childControlWidth = true;
            rowHLG.childControlHeight = true;
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 20f;

            // Left line (CreateDivider already adds LayoutElement with flexibleWidth)
            UIHelper.CreateDivider(row.transform, dividerColor, 1f);

            // "or" label
            var orLabel = UIHelper.CreateLabel(row.transform, "or",
                UIConstants.FontSmall, SporefrontColors.InkFaded, TextAnchor.MiddleCenter);
            var orLE = orLabel.gameObject.AddComponent<LayoutElement>();
            orLE.preferredWidth = 24f;
            orLE.preferredHeight = 20f;

            // Right line
            UIHelper.CreateDivider(row.transform, dividerColor, 1f);
        }

        private void AddTextHover(GameObject target, Text label, Color normal, Color hover)
        {
            var trigger = target.AddComponent<EventTrigger>();
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((_) => { if (label != null) label.color = hover; });
            trigger.triggers.Add(enterEntry);
            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((_) => { if (label != null) label.color = normal; });
            trigger.triggers.Add(exitEntry);
        }

    }
}
