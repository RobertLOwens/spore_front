// ============================================================================
// FILE: Visual/AccountPanel.cs
// PURPOSE: Modal panel for account management — display name change, password
//          change, lifetime stats, recent games, sign out, delete account.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sporefront.Data;
using Sporefront.Engine;

namespace Sporefront.Visual
{
    public class AccountPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action OnClose;
        public event Action OnSignedOut;
        public event Action OnAccountDeleted;

        // ================================================================
        // State
        // ================================================================

        private GameObject backdrop;
        private GameObject panel;
        private RectTransform contentRT;

        // Inline editing state
        private bool isChangingName;
        private bool isChangingPassword;
        private bool isDeletingAccount;

        // Inline input fields
        private InputField nameInput;
        private Text nameAvailabilityLabel;
        private InputField currentPasswordInput;
        private InputField newPasswordInput;
        private InputField deletePasswordInput;

        // Debounce for name availability
        private string lastCheckedName = "";
        private float nameCheckTimer;
        private bool nameIsAvailable;
        private bool nameIsChecking;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Semi-transparent backdrop
            backdrop = UIHelper.CreatePanel(canvasTransform, "AccountBackdrop",
                new Color(0, 0, 0, 0.5f));
            var bdRT = backdrop.GetComponent<RectTransform>();
            UIHelper.StretchFull(bdRT);
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // Main panel
            panel = UIHelper.CreatePanel(backdrop.transform, "AccountPanel", UIHelper.PanelParchmentBg);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.SetFixedSize(panelRT, 440, 600);
            PopupTendrilDecorator.Attach(panelRT);

            // Header bar
            var headerBar = UIHelper.CreatePanel(panel.transform, "HeaderBar",
                SporefrontColors.ParchmentDeep);
            var headerBarRT = headerBar.GetComponent<RectTransform>();
            headerBarRT.anchorMin = new Vector2(0, 1);
            headerBarRT.anchorMax = new Vector2(1, 1);
            headerBarRT.pivot = new Vector2(0.5f, 1);
            headerBarRT.offsetMin = new Vector2(0, -48);
            headerBarRT.offsetMax = Vector2.zero;

            var headerHLG = headerBar.AddComponent<HorizontalLayoutGroup>();
            headerHLG.spacing = 6f;
            headerHLG.padding = new RectOffset(12, 10, 6, 6);
            headerHLG.childForceExpandWidth = false;
            headerHLG.childForceExpandHeight = true;
            headerHLG.childControlWidth = false;
            headerHLG.childControlHeight = true;

            var titleLabel = UIHelper.CreateLabel(headerBar.transform, "Account",
                UIHelper.DefaultHeaderFontSize, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var titleLE = titleLabel.gameObject.AddComponent<LayoutElement>();
            titleLE.flexibleWidth = 1;

            var doneBtn = UIHelper.CreateButton(headerBar.transform, "Done",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor, UIConstants.FontSmall, Hide);
            var doneBtnLE = doneBtn.gameObject.AddComponent<LayoutElement>();
            doneBtnLE.preferredWidth = 68;

            // ScrollView
            var scroll = UIHelper.CreateScrollView(panel.transform, "AccountScroll", out contentRT);
            var scrollRT = scroll.GetComponent<RectTransform>();
            UIHelper.StretchFull(scrollRT);
            scrollRT.offsetMin = Vector2.zero;
            scrollRT.offsetMax = new Vector2(0, -52);

            backdrop.SetActive(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show()
        {
            isChangingName = false;
            isChangingPassword = false;
            isDeletingAccount = false;
            Rebuild();
            backdrop.SetActive(true);
        }

        public void Hide()
        {
            backdrop.SetActive(false);
            OnClose?.Invoke();
        }

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Update — name availability debounce
        // ================================================================

        private void Update()
        {
            if (!IsVisible || nameInput == null || !isChangingName) return;

            string current = nameInput.text?.Trim() ?? "";
            if (current != lastCheckedName && !nameIsChecking)
            {
                nameCheckTimer += Time.deltaTime;
                if (nameCheckTimer >= 0.5f)
                {
                    nameCheckTimer = 0f;
                    CheckNameAvailability(current);
                }
            }
        }

        // ================================================================
        // Rebuild
        // ================================================================

        private void Rebuild()
        {
            // Clear content
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);

            var auth = AuthService.Instance;

            // Section: Account Info
            BuildSectionHeader("Account Info");

            BuildInfoRow("Email", auth.CurrentEmail ?? "—");
            BuildInfoRow("Display Name", auth.CurrentDisplayName ?? "—");

            AddSpacer(contentRT, 8f);

            // Change Display Name
            if (isChangingName)
                BuildChangeNameInline();
            else
            {
                var changeNameBtn = UIHelper.CreateButton(contentRT, "Change Display Name",
                    UIHelper.ButtonBg, UIHelper.ButtonText, UIConstants.FontSmall, () =>
                    {
                        isChangingName = true;
                        Rebuild();
                    });
                var cnLE = changeNameBtn.gameObject.AddComponent<LayoutElement>();
                cnLE.preferredHeight = 36f;
            }

            AddSpacer(contentRT, 4f);

            // Change Password
            if (isChangingPassword)
                BuildChangePasswordInline();
            else
            {
                var changePwBtn = UIHelper.CreateButton(contentRT, "Change Password",
                    UIHelper.ButtonBg, UIHelper.ButtonText, UIConstants.FontSmall, () =>
                    {
                        isChangingPassword = true;
                        Rebuild();
                    });
                var cpLE = changePwBtn.gameObject.AddComponent<LayoutElement>();
                cpLE.preferredHeight = 36f;
            }

            UIHelper.CreateDivider(contentRT, null, 2);

            // Section: Statistics
            BuildSectionHeader("Lifetime Statistics");
            BuildStatsSection();

            UIHelper.CreateDivider(contentRT, null, 2);

            // Section: Recent Games
            BuildSectionHeader("Recent Games");
            BuildRecentGamesSection();

            UIHelper.CreateDivider(contentRT, null, 2);

            // Section: Account Actions
            AddSpacer(contentRT, 10f);

            var signOutBtn = UIHelper.CreateButton(contentRT, "Sign Out",
                SporefrontColors.SporeAmber, UIHelper.HudTextColor, UIConstants.FontSmall, () =>
                {
                    AuthService.Instance.SignOut();
                    Hide();
                    OnSignedOut?.Invoke();
                });
            var soLE = signOutBtn.gameObject.AddComponent<LayoutElement>();
            soLE.preferredHeight = 40f;

            AddSpacer(contentRT, 8f);

            // Delete Account
            if (isDeletingAccount)
                BuildDeleteAccountInline();
            else
            {
                var deleteBtn = UIHelper.CreateButton(contentRT, "Delete Account",
                    SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontSmall, () =>
                    {
                        isDeletingAccount = true;
                        Rebuild();
                    });
                var dLE = deleteBtn.gameObject.AddComponent<LayoutElement>();
                dLE.preferredHeight = 36f;
            }

            AddSpacer(contentRT, 20f);
        }

        // ================================================================
        // Inline Change Display Name
        // ================================================================

        private void BuildChangeNameInline()
        {
            var card = UIHelper.CreateSectionCard(contentRT, "ChangeNameCard", "Change Display Name");

            nameInput = CreateInputField(card.transform, "New display name...");
            nameInput.characterLimit = 20;
            nameInput.onValueChanged.AddListener((_) => { nameCheckTimer = 0f; });

            nameAvailabilityLabel = UIHelper.CreateLabel(card.transform, "",
                UIConstants.FontCaption, UIHelper.InkMutedText, TextAnchor.MiddleLeft);
            var availLE = nameAvailabilityLabel.gameObject.AddComponent<LayoutElement>();
            availLE.preferredHeight = 18f;

            var btnRow = UIHelper.CreateHorizontalRow(card.transform, 34f);

            UIHelper.CreateButton(btnRow.transform, "Cancel",
                UIHelper.ButtonBg, UIHelper.ButtonText, UIConstants.FontSmall, () =>
                {
                    isChangingName = false;
                    Rebuild();
                });

            UIHelper.CreateButton(btnRow.transform, "Save",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor, UIConstants.FontSmall, () =>
                {
                    string newName = nameInput.text?.Trim();
                    if (!AuthService.IsValidUsername(newName))
                    {
                        nameAvailabilityLabel.text = "Invalid name format";
                        nameAvailabilityLabel.color = SporefrontColors.SporeRed;
                        return;
                    }
                    if (!nameIsAvailable)
                    {
                        nameAvailabilityLabel.text = "Name not available";
                        nameAvailabilityLabel.color = SporefrontColors.SporeRed;
                        return;
                    }

                    nameAvailabilityLabel.text = "Saving...";
                    nameAvailabilityLabel.color = UIHelper.InkMutedText;

                    AuthService.Instance.ChangeUsername(newName, (success, error) =>
                    {
                        if (success)
                        {
                            isChangingName = false;
                            Rebuild();
                        }
                        else
                        {
                            nameAvailabilityLabel.text = error ?? "Failed to change name";
                            nameAvailabilityLabel.color = SporefrontColors.SporeRed;
                        }
                    });
                });
        }

        private void CheckNameAvailability(string name)
        {
            lastCheckedName = name;

            if (string.IsNullOrEmpty(name) || !AuthService.IsValidUsername(name))
            {
                nameIsAvailable = false;
                if (nameAvailabilityLabel != null)
                {
                    nameAvailabilityLabel.text = string.IsNullOrEmpty(name) ? ""
                        : "Invalid: 3-20 chars, letters/numbers/underscores";
                    nameAvailabilityLabel.color = SporefrontColors.SporeRed;
                }
                return;
            }

            nameIsChecking = true;
            if (nameAvailabilityLabel != null)
            {
                nameAvailabilityLabel.text = "Checking...";
                nameAvailabilityLabel.color = UIHelper.InkMutedText;
            }

            AuthService.Instance.CheckUsernameAvailability(name, (available) =>
            {
                nameIsChecking = false;
                if (lastCheckedName != name) return;

                nameIsAvailable = available;
                if (nameAvailabilityLabel != null)
                {
                    nameAvailabilityLabel.text = available ? "Available!" : "Already taken";
                    nameAvailabilityLabel.color = available
                        ? SporefrontColors.SporeGreen : SporefrontColors.SporeRed;
                }
            });
        }

        // ================================================================
        // Inline Change Password
        // ================================================================

        private void BuildChangePasswordInline()
        {
            var card = UIHelper.CreateSectionCard(contentRT, "ChangePasswordCard", "Change Password");

            UIHelper.CreateLabel(card.transform, "Current Password",
                UIConstants.FontCaption, UIHelper.InkBodyText, TextAnchor.MiddleLeft);
            currentPasswordInput = CreateInputField(card.transform, "Current password...");
            currentPasswordInput.contentType = InputField.ContentType.Password;

            UIHelper.CreateLabel(card.transform, "New Password",
                UIConstants.FontCaption, UIHelper.InkBodyText, TextAnchor.MiddleLeft);
            newPasswordInput = CreateInputField(card.transform, "New password...");
            newPasswordInput.contentType = InputField.ContentType.Password;

            var statusLabel = UIHelper.CreateLabel(card.transform, "",
                UIConstants.FontCaption, SporefrontColors.SporeRed, TextAnchor.MiddleCenter);
            var statusLE = statusLabel.gameObject.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 18f;

            var btnRow = UIHelper.CreateHorizontalRow(card.transform, 34f);

            UIHelper.CreateButton(btnRow.transform, "Cancel",
                UIHelper.ButtonBg, UIHelper.ButtonText, UIConstants.FontSmall, () =>
                {
                    isChangingPassword = false;
                    Rebuild();
                });

            UIHelper.CreateButton(btnRow.transform, "Save",
                SporefrontColors.SporeGreen, UIHelper.HudTextColor, UIConstants.FontSmall, () =>
                {
                    string currentPw = currentPasswordInput.text;
                    string newPw = newPasswordInput.text;

                    if (string.IsNullOrEmpty(currentPw) || string.IsNullOrEmpty(newPw))
                    {
                        statusLabel.text = "Please fill in both fields.";
                        return;
                    }
                    if (newPw.Length < 6)
                    {
                        statusLabel.text = "New password must be at least 6 characters.";
                        return;
                    }

                    statusLabel.text = "Updating...";
                    statusLabel.color = UIHelper.InkMutedText;

                    AuthService.Instance.ChangePassword(currentPw, newPw, (success, error) =>
                    {
                        if (success)
                        {
                            isChangingPassword = false;
                            Rebuild();
                        }
                        else
                        {
                            statusLabel.text = error ?? "Failed to change password";
                            statusLabel.color = SporefrontColors.SporeRed;
                        }
                    });
                });
        }

        // ================================================================
        // Inline Delete Account
        // ================================================================

        private void BuildDeleteAccountInline()
        {
            var card = UIHelper.CreateSectionCard(contentRT, "DeleteAccountCard", "Delete Account");

            UIHelper.CreateLabel(card.transform, "This action is permanent and cannot be undone.",
                UIConstants.FontCaption, SporefrontColors.SporeRed, TextAnchor.MiddleCenter);

            AddSpacer(card.transform, 4f);

            UIHelper.CreateLabel(card.transform, "Enter your password to confirm:",
                UIConstants.FontCaption, UIHelper.InkBodyText, TextAnchor.MiddleLeft);
            deletePasswordInput = CreateInputField(card.transform, "Password...");
            deletePasswordInput.contentType = InputField.ContentType.Password;

            var statusLabel = UIHelper.CreateLabel(card.transform, "",
                UIConstants.FontCaption, SporefrontColors.SporeRed, TextAnchor.MiddleCenter);
            var statusLE = statusLabel.gameObject.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 18f;

            var btnRow = UIHelper.CreateHorizontalRow(card.transform, 34f);

            UIHelper.CreateButton(btnRow.transform, "Cancel",
                UIHelper.ButtonBg, UIHelper.ButtonText, UIConstants.FontSmall, () =>
                {
                    isDeletingAccount = false;
                    Rebuild();
                });

            UIHelper.CreateButton(btnRow.transform, "Delete Forever",
                SporefrontColors.SporeRed, UIHelper.HudTextColor, UIConstants.FontSmall, () =>
                {
                    string password = deletePasswordInput.text;
                    if (string.IsNullOrEmpty(password))
                    {
                        statusLabel.text = "Please enter your password.";
                        return;
                    }

                    statusLabel.text = "Deleting account...";
                    statusLabel.color = UIHelper.InkMutedText;

                    AuthService.Instance.DeleteAccount(password, (success, error) =>
                    {
                        if (success)
                        {
                            Hide();
                            OnAccountDeleted?.Invoke();
                        }
                        else
                        {
                            statusLabel.text = error ?? "Failed to delete account";
                            statusLabel.color = SporefrontColors.SporeRed;
                        }
                    });
                });
        }

        // ================================================================
        // Stats Section
        // ================================================================

        private void BuildStatsSection()
        {
            var loadingLabel = UIHelper.CreateLabel(contentRT, "Loading stats...",
                UIConstants.FontCaption, UIHelper.InkMutedText, TextAnchor.MiddleCenter);
            var loadingLE = loadingLabel.gameObject.AddComponent<LayoutElement>();
            loadingLE.preferredHeight = 24f;

            string uid = AuthService.Instance.CurrentUID;
            if (string.IsNullOrEmpty(uid)) return;

            UserStatsService.Instance.FetchStats(uid, (stats) =>
            {
                if (!IsVisible) return;
                if (loadingLabel != null) Destroy(loadingLabel.gameObject);

                BuildStatRow("Games Played", stats.gamesPlayed.ToString());
                BuildStatRow("Wins / Losses", $"{stats.gamesWon} / {stats.gamesLost}");
                BuildStatRow("Total Play Time", FormatTime(stats.totalPlayTime));
                BuildStatRow("Battles Won / Lost", $"{stats.battlesWon} / {stats.battlesLost}");
                BuildStatRow("Units Killed / Lost", $"{stats.unitsKilled} / {stats.unitsLost}");
                BuildStatRow("Buildings Built", stats.buildingsBuilt.ToString());
                BuildStatRow("Resources Gathered", stats.totalResourcesGathered.ToString());
                BuildStatRow("Highest Population", stats.highestPopulation.ToString());
            });
        }

        // ================================================================
        // Recent Games Section
        // ================================================================

        private void BuildRecentGamesSection()
        {
            var loadingLabel = UIHelper.CreateLabel(contentRT, "Loading history...",
                UIConstants.FontCaption, UIHelper.InkMutedText, TextAnchor.MiddleCenter);
            var loadingLE = loadingLabel.gameObject.AddComponent<LayoutElement>();
            loadingLE.preferredHeight = 24f;

            string uid = AuthService.Instance.CurrentUID;
            if (string.IsNullOrEmpty(uid)) return;

            UserStatsService.Instance.FetchRecentGames(uid, 10, (entries) =>
            {
                if (!IsVisible) return;
                if (loadingLabel != null) Destroy(loadingLabel.gameObject);

                if (entries.Count == 0)
                {
                    var noGames = UIHelper.CreateLabel(contentRT, "No games yet.",
                        UIConstants.FontCaption, UIHelper.InkMutedText, TextAnchor.MiddleCenter);
                    var noLE = noGames.gameObject.AddComponent<LayoutElement>();
                    noLE.preferredHeight = 24f;
                    return;
                }

                foreach (var entry in entries)
                {
                    string result = entry.isVictory ? "Victory" : "Defeat";
                    Color resultColor = entry.isVictory
                        ? SporefrontColors.SporeGreen : SporefrontColors.SporeRed;
                    string duration = FormatTime(entry.duration);
                    string summary = $"{result} — {entry.reason ?? "unknown"} — {duration}";

                    var label = UIHelper.CreateLabel(contentRT, summary,
                        UIConstants.FontCaption, resultColor, TextAnchor.MiddleLeft);
                    var labelLE = label.gameObject.AddComponent<LayoutElement>();
                    labelLE.preferredHeight = 22f;
                }
            });
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void BuildSectionHeader(string title)
        {
            var sectionLabel = UIHelper.CreateLabel(contentRT, title,
                UIHelper.DefaultHeaderFontSize, UIHelper.InkHeaderText,
                TextAnchor.MiddleLeft, true);
            var sectionLE = sectionLabel.gameObject.AddComponent<LayoutElement>();
            sectionLE.preferredHeight = 30;

            UIHelper.CreateDivider(contentRT);
        }

        private void BuildInfoRow(string label, string value)
        {
            var row = UIHelper.CreateHorizontalRow(contentRT, 26f);

            var labelText = UIHelper.CreateLabel(row.transform, label,
                UIConstants.FontSmall, UIHelper.InkBodyText, TextAnchor.MiddleLeft);
            var labelLE = labelText.gameObject.AddComponent<LayoutElement>();
            labelLE.flexibleWidth = 1;

            var valueText = UIHelper.CreateLabel(row.transform, value,
                UIConstants.FontSmall, UIHelper.InkMutedText, TextAnchor.MiddleRight);
            var valueLE = valueText.gameObject.AddComponent<LayoutElement>();
            valueLE.preferredWidth = 200;
        }

        private void BuildStatRow(string label, string value)
        {
            var row = UIHelper.CreateHorizontalRow(contentRT, 22f);

            var labelText = UIHelper.CreateLabel(row.transform, label,
                UIConstants.FontCaption, UIHelper.InkBodyText, TextAnchor.MiddleLeft);
            var labelLE = labelText.gameObject.AddComponent<LayoutElement>();
            labelLE.flexibleWidth = 1;

            var valueText = UIHelper.CreateLabel(row.transform, value,
                UIConstants.FontCaption, UIHelper.InkMutedText, TextAnchor.MiddleRight);
            var valueLE = valueText.gameObject.AddComponent<LayoutElement>();
            valueLE.preferredWidth = 120;
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

        private void AddSpacer(Transform parent, float height)
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);
            var le = spacer.GetComponent<LayoutElement>();
            le.preferredHeight = height;
        }

        private string FormatTime(double totalSeconds)
        {
            int hours = (int)(totalSeconds / 3600);
            int minutes = (int)((totalSeconds % 3600) / 60);
            int seconds = (int)(totalSeconds % 60);

            if (hours > 0)
                return $"{hours}h {minutes:D2}m {seconds:D2}s";
            return $"{minutes}m {seconds:D2}s";
        }
    }
}
