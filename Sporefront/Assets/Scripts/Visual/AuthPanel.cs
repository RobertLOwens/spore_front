// ============================================================================
// FILE: Visual/AuthPanel.cs
// PURPOSE: Full-screen sign in / register panel with email + password fields.
//          Two modes toggled by a link-style button.
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Engine;

namespace Sporefront.Visual
{
    public class AuthPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnAuthSuccess;

        // ================================================================
        // State
        // ================================================================

        private GameObject panel;
        private RectTransform contentRT;

        private InputField emailInput;
        private InputField passwordInput;
        private InputField confirmPasswordInput;
        private GameObject confirmPasswordRow;
        private Text statusLabel;
        private Button submitButton;
        private Text submitButtonLabel;
        private Button toggleButton;
        private Text toggleLabel;
        private Button forgotPasswordButton;
        private GameObject forgotPasswordRow;

        private bool isRegisterMode;
        private bool isProcessing;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            panel = UIHelper.CreatePanel(canvasTransform, "AuthPanel", SporefrontColors.ParchmentDeep);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.StretchFull(panelRT);

            BuildContent();
            panel.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show()
        {
            isRegisterMode = false;
            isProcessing = false;
            ClearFields();
            UpdateModeDisplay();
            panel.SetActive(true);
        }

        public void Hide()
        {
            panel.SetActive(false);
        }

        public bool IsVisible => panel != null && panel.activeSelf;

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
            var title = UIHelper.CreateLabel(centerColumn.transform, "SPOREFRONT",
                32, SporefrontColors.ParchmentLight, TextAnchor.MiddleCenter, true);
            var titleLE = title.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 60f;

            // Subtitle
            var subtitle = UIHelper.CreateLabel(centerColumn.transform, "Sign in to play",
                16, SporefrontColors.ParchmentShadow, TextAnchor.MiddleCenter);
            var subtitleLE = subtitle.gameObject.AddComponent<LayoutElement>();
            subtitleLE.preferredHeight = 28f;

            AddSpacer(centerColumn.transform, 20f);

            // Email field
            UIHelper.CreateLabel(centerColumn.transform, "Email",
                UIConstants.FontSmall, UIHelper.BodyTextColor, TextAnchor.MiddleLeft);
            emailInput = CreateInputField(centerColumn.transform, "Enter email...");
            emailInput.contentType = InputField.ContentType.EmailAddress;

            AddSpacer(centerColumn.transform, 4f);

            // Password field
            UIHelper.CreateLabel(centerColumn.transform, "Password",
                UIConstants.FontSmall, UIHelper.BodyTextColor, TextAnchor.MiddleLeft);
            passwordInput = CreateInputField(centerColumn.transform, "Enter password...");
            passwordInput.contentType = InputField.ContentType.Password;

            // Confirm password (register only)
            confirmPasswordRow = new GameObject("ConfirmPasswordRow", typeof(RectTransform));
            confirmPasswordRow.transform.SetParent(centerColumn.transform, false);
            var cpVLG = confirmPasswordRow.AddComponent<VerticalLayoutGroup>();
            cpVLG.spacing = 4f;
            cpVLG.childForceExpandWidth = true;
            cpVLG.childForceExpandHeight = false;
            cpVLG.childControlWidth = true;
            cpVLG.childControlHeight = false;
            var cpLE = confirmPasswordRow.AddComponent<LayoutElement>();
            cpLE.preferredHeight = 56f;

            AddSpacer(confirmPasswordRow.transform, 4f);
            UIHelper.CreateLabel(confirmPasswordRow.transform, "Confirm Password",
                UIConstants.FontSmall, UIHelper.BodyTextColor, TextAnchor.MiddleLeft);
            confirmPasswordInput = CreateInputField(confirmPasswordRow.transform, "Confirm password...");
            confirmPasswordInput.contentType = InputField.ContentType.Password;

            AddSpacer(centerColumn.transform, 6f);

            // Status label
            statusLabel = UIHelper.CreateLabel(centerColumn.transform, "",
                UIConstants.FontCaption, SporefrontColors.SporeRed, TextAnchor.MiddleCenter);
            var statusLE = statusLabel.gameObject.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 20f;

            AddSpacer(centerColumn.transform, 6f);

            // Submit button
            submitButton = UIHelper.CreateButton(centerColumn.transform, "Sign In",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor,
                UIHelper.DefaultBodyFontSize + 2, OnSubmitClicked);
            var submitLE = submitButton.gameObject.AddComponent<LayoutElement>();
            submitLE.preferredHeight = 44f;
            submitButtonLabel = submitButton.GetComponentInChildren<Text>();

            AddSpacer(centerColumn.transform, 4f);

            // Forgot password (sign in only)
            forgotPasswordRow = new GameObject("ForgotPasswordRow", typeof(RectTransform));
            forgotPasswordRow.transform.SetParent(centerColumn.transform, false);
            var fpLE = forgotPasswordRow.AddComponent<LayoutElement>();
            fpLE.preferredHeight = 24f;

            forgotPasswordButton = UIHelper.CreateButton(forgotPasswordRow.transform, "Forgot Password?",
                Color.clear, SporefrontColors.SporeTeal, UIConstants.FontCaption, OnForgotPasswordClicked);
            var fpBtnRT = forgotPasswordButton.GetComponent<RectTransform>();
            UIHelper.StretchFull(fpBtnRT);

            AddSpacer(centerColumn.transform, 10f);
            UIHelper.CreateDivider(centerColumn.transform, SporefrontColors.ParchmentShadow, 1f);
            AddSpacer(centerColumn.transform, 10f);

            // Toggle button
            toggleButton = UIHelper.CreateButton(centerColumn.transform, "Don't have an account? Register",
                Color.clear, SporefrontColors.ParchmentShadow, UIConstants.FontSmall, OnToggleClicked);
            toggleLabel = toggleButton.GetComponentInChildren<Text>();
            var toggleLE = toggleButton.gameObject.AddComponent<LayoutElement>();
            toggleLE.preferredHeight = 30f;

            // Bottom spacer
            AddSpacer(centerColumn.transform, 0f, true);
        }

        // ================================================================
        // Mode Switching
        // ================================================================

        private void UpdateModeDisplay()
        {
            if (isRegisterMode)
            {
                confirmPasswordRow.SetActive(true);
                forgotPasswordRow.SetActive(false);
                submitButtonLabel.text = "Register";
                toggleLabel.text = "Already have an account? Sign In";
            }
            else
            {
                confirmPasswordRow.SetActive(false);
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
                ShowStatus("Creating account...", SporefrontColors.ParchmentShadow);

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
                ShowStatus("Signing in...", SporefrontColors.ParchmentShadow);

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

            ShowStatus("Sending reset email...", SporefrontColors.ParchmentShadow);
            AuthService.Instance.SendPasswordReset(email, (success, error) =>
            {
                if (success)
                    ShowStatus("Reset email sent! Check your inbox.", SporefrontColors.SporeGreen);
                else
                    ShowStatus(error ?? "Failed to send reset email.", SporefrontColors.SporeRed);
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

        private InputField CreateInputField(Transform parent, string placeholder)
        {
            var inputBG = UIHelper.CreatePanel(parent, "InputBG", UIHelper.HudBg);
            var inputBGLE = inputBG.AddComponent<LayoutElement>();
            inputBGLE.preferredHeight = 36f;

            var input = inputBG.AddComponent<InputField>();

            var placeholderText = UIHelper.CreateLabel(inputBG.transform, placeholder,
                13, SporefrontColors.ParchmentShadow, TextAnchor.MiddleLeft);
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
