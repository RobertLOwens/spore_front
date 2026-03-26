// ============================================================================
// FILE: Visual/MatchmakingPanel.cs
// PURPOSE: Queue-based matchmaking UI — faction select, queue search, ready-up.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sporefront.Engine;
using Sporefront.Models;

namespace Sporefront.Visual
{
    public class MatchmakingPanel : MonoBehaviour
    {
        // ================================================================
        // Events
        // ================================================================

        public event Action<MatchResult> OnGameReady;
        public event Action OnBack;

        // ================================================================
        // State
        // ================================================================

        private enum MatchmakingScreen { FactionSelect, Searching, MatchFound }
        private MatchmakingScreen currentScreen = MatchmakingScreen.FactionSelect;

        private FactionType selectedFaction = FactionType.Morel;
        private MatchResult? pendingMatch;
        private float readyTimer;
        private bool localReady;
        private bool opponentReady;
        private const float ReadyTimeout = 30.0f;

        // ================================================================
        // UI References
        // ================================================================

        private GameObject panel;
        private GameObject factionSelectContainer;
        private GameObject searchingContainer;
        private GameObject matchFoundContainer;

        // Faction select
        private List<Button> factionButtons = new List<Button>();
        private List<Image> factionBorders = new List<Image>();
        private int selectedFactionIndex = 0;
        private Text factionInfoLabel;

        // Searching
        private Text searchStatusLabel;
        private Text searchTimerLabel;

        // Match found
        private Text opponentNameLabel;
        private Text opponentFactionLabel;
        private Text readyStatusLabel;
        private Button readyButton;

        // Hover gradient (matches GameSetupPanel style)
        private Texture2D hoverGradientTexture;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize(Transform canvasTransform)
        {
            // Full-screen panel
            panel = UIHelper.CreatePanel(canvasTransform, "MatchmakingPanel",
                SporefrontColors.ParchmentMid, cornerRadius: 0);
            var panelRT = panel.GetComponent<RectTransform>();
            UIHelper.StretchFull(panelRT);

            // Parchment overlay
            UIHelper.AddParchmentOverlay(panel.transform, 0.25f);

            // Build hover gradient texture
            hoverGradientTexture = new Texture2D(1, 64, TextureFormat.RGBA32, false);
            hoverGradientTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[64];
            for (int y = 0; y < 64; y++)
            {
                float alpha;
                if (y < 4) alpha = Mathf.Lerp(0f, 0.12f, y / 3f);
                else if (y < 60) alpha = 0.12f;
                else alpha = Mathf.Lerp(0.12f, 0f, (y - 60) / 3f);
                pixels[y] = new Color(SporefrontColors.InkDark.r, SporefrontColors.InkDark.g,
                    SporefrontColors.InkDark.b, alpha);
            }
            hoverGradientTexture.SetPixels(pixels);
            hoverGradientTexture.Apply();

            BuildFactionSelectScreen();
            BuildSearchingScreen();
            BuildMatchFoundScreen();

            panel.SetActive(false);
            ShowScreen(MatchmakingScreen.FactionSelect);
        }

        // ================================================================
        // Faction Select Screen
        // ================================================================

        private void BuildFactionSelectScreen()
        {
            factionSelectContainer = new GameObject("FactionSelectContainer", typeof(RectTransform));
            factionSelectContainer.transform.SetParent(panel.transform, false);
            var containerRT = factionSelectContainer.GetComponent<RectTransform>();
            containerRT.anchorMin = new Vector2(0.5f, 0f);
            containerRT.anchorMax = new Vector2(0.5f, 1f);
            containerRT.pivot = new Vector2(0.5f, 0.5f);
            float columnWidth = 380f;
            containerRT.sizeDelta = new Vector2(columnWidth, 0f);
            containerRT.offsetMin = new Vector2(-columnWidth / 2f, 0f);
            containerRT.offsetMax = new Vector2(columnWidth / 2f, 0f);

            var vlg = factionSelectContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(30, 30, 0, 0);

            // Top spacer
            AddFlexSpacer(factionSelectContainer.transform);

            // Title
            var title = UIHelper.CreateLabel(factionSelectContainer.transform,
                "O N L I N E   M A T C H",
                42, SporefrontColors.InkDark, TextAnchor.MiddleCenter, true);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;

            AddFixedSpacer(factionSelectContainer.transform, 24);

            // "Select Your Faction" label
            var selectLabel = UIHelper.CreateLabel(factionSelectContainer.transform,
                "Select Your Faction",
                UIConstants.FontSubheader, UIHelper.InkHeaderText,
                TextAnchor.MiddleCenter, true);
            selectLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            AddFixedSpacer(factionSelectContainer.transform, 8);

            // Faction buttons
            var row = UIHelper.CreateHorizontalRow(factionSelectContainer.transform, 44f, UIConstants.SpaceXS);
            factionButtons.Clear();
            factionBorders.Clear();

            var factions = new FactionType[] { FactionType.Morel, FactionType.Muscaria };
            for (int i = 0; i < factions.Length; i++)
            {
                int idx = i;
                var faction = factions[i];
                var btn = UIHelper.CreateButton(row.transform, faction.DisplayName(),
                    null, null, UIConstants.FontCaption, () =>
                {
                    selectedFaction = factions[idx];
                    selectedFactionIndex = idx;
                    UpdateFactionSelection();
                });
                var btnLE = btn.gameObject.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 160;
                btnLE.preferredHeight = 44;
                factionButtons.Add(btn);

                // Track border for selection highlight
                var border = btn.GetComponent<Image>();
                if (border != null) factionBorders.Add(border);
            }

            UpdateFactionSelection();

            AddFixedSpacer(factionSelectContainer.transform, 8);

            // Faction info
            factionInfoLabel = UIHelper.CreateLabel(factionSelectContainer.transform, "",
                UIConstants.FontCaption, UIHelper.InkSubText, TextAnchor.MiddleCenter);
            factionInfoLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            UpdateFactionInfo();

            AddFixedSpacer(factionSelectContainer.transform, 24);

            // Backdrop for buttons
            var backdropGO = UIHelper.CreatePanel(factionSelectContainer.transform, "ButtonBackdrop",
                new Color(SporefrontColors.ParchmentDark.r, SporefrontColors.ParchmentDark.g,
                    SporefrontColors.ParchmentDark.b, 0.50f), cornerRadius: 0);
            var backdropOutline = backdropGO.GetComponent<Outline>();
            if (backdropOutline != null) UnityEngine.Object.Destroy(backdropOutline);
            var backdropLE = backdropGO.AddComponent<LayoutElement>();
            backdropLE.preferredWidth = 200f;
            var backdropVLG = backdropGO.AddComponent<VerticalLayoutGroup>();
            backdropVLG.spacing = 10f;
            backdropVLG.childAlignment = TextAnchor.UpperCenter;
            backdropVLG.childForceExpandWidth = true;
            backdropVLG.childForceExpandHeight = false;
            backdropVLG.childControlWidth = true;
            backdropVLG.childControlHeight = false;
            backdropVLG.padding = new RectOffset(15, 15, 10, 10);

            // Find Game button
            CreateMenuItem(backdropGO.transform, "Find Game", SporefrontColors.InkDark, OnFindGameClicked);

            // Back button
            CreateMenuItem(backdropGO.transform, "Back", SporefrontColors.InkFaded, () => OnBack?.Invoke());

            // Bottom spacer
            AddFlexSpacer(factionSelectContainer.transform);
        }

        // ================================================================
        // Searching Screen
        // ================================================================

        private void BuildSearchingScreen()
        {
            searchingContainer = new GameObject("SearchingContainer", typeof(RectTransform));
            searchingContainer.transform.SetParent(panel.transform, false);
            var containerRT = searchingContainer.GetComponent<RectTransform>();
            containerRT.anchorMin = new Vector2(0.5f, 0f);
            containerRT.anchorMax = new Vector2(0.5f, 1f);
            containerRT.pivot = new Vector2(0.5f, 0.5f);
            float columnWidth = 380f;
            containerRT.sizeDelta = new Vector2(columnWidth, 0f);
            containerRT.offsetMin = new Vector2(-columnWidth / 2f, 0f);
            containerRT.offsetMax = new Vector2(columnWidth / 2f, 0f);

            var vlg = searchingContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(30, 30, 0, 0);

            AddFlexSpacer(searchingContainer.transform);

            // Title
            var title = UIHelper.CreateLabel(searchingContainer.transform,
                "S E A R C H I N G",
                42, SporefrontColors.InkDark, TextAnchor.MiddleCenter, true);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;

            AddFixedSpacer(searchingContainer.transform, 24);

            // Status label
            searchStatusLabel = UIHelper.CreateLabel(searchingContainer.transform,
                "Looking for an opponent...",
                UIConstants.FontBody, UIHelper.InkBodyText, TextAnchor.MiddleCenter);
            searchStatusLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;

            // Timer label
            searchTimerLabel = UIHelper.CreateLabel(searchingContainer.transform, "0:00",
                UIConstants.FontSubheader, UIHelper.InkSubText, TextAnchor.MiddleCenter);
            searchTimerLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;

            AddFixedSpacer(searchingContainer.transform, 24);

            // Cancel button backdrop
            var backdropGO = UIHelper.CreatePanel(searchingContainer.transform, "CancelBackdrop",
                new Color(SporefrontColors.ParchmentDark.r, SporefrontColors.ParchmentDark.g,
                    SporefrontColors.ParchmentDark.b, 0.50f), cornerRadius: 0);
            var backdropOutline = backdropGO.GetComponent<Outline>();
            if (backdropOutline != null) UnityEngine.Object.Destroy(backdropOutline);
            backdropGO.AddComponent<LayoutElement>().preferredWidth = 200f;
            var backdropVLG = backdropGO.AddComponent<VerticalLayoutGroup>();
            backdropVLG.spacing = 10f;
            backdropVLG.childAlignment = TextAnchor.UpperCenter;
            backdropVLG.childForceExpandWidth = true;
            backdropVLG.childForceExpandHeight = false;
            backdropVLG.childControlWidth = true;
            backdropVLG.childControlHeight = false;
            backdropVLG.padding = new RectOffset(15, 15, 10, 10);

            CreateMenuItem(backdropGO.transform, "Cancel", SporefrontColors.InkFaded, OnCancelSearchClicked);

            AddFlexSpacer(searchingContainer.transform);

            searchingContainer.SetActive(false);
        }

        // ================================================================
        // Match Found Screen
        // ================================================================

        private void BuildMatchFoundScreen()
        {
            matchFoundContainer = new GameObject("MatchFoundContainer", typeof(RectTransform));
            matchFoundContainer.transform.SetParent(panel.transform, false);
            var containerRT = matchFoundContainer.GetComponent<RectTransform>();
            containerRT.anchorMin = new Vector2(0.5f, 0f);
            containerRT.anchorMax = new Vector2(0.5f, 1f);
            containerRT.pivot = new Vector2(0.5f, 0.5f);
            float columnWidth = 420f;
            containerRT.sizeDelta = new Vector2(columnWidth, 0f);
            containerRT.offsetMin = new Vector2(-columnWidth / 2f, 0f);
            containerRT.offsetMax = new Vector2(columnWidth / 2f, 0f);

            var vlg = matchFoundContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(30, 30, 0, 0);

            AddFlexSpacer(matchFoundContainer.transform);

            // Title
            var title = UIHelper.CreateLabel(matchFoundContainer.transform,
                "O P P O N E N T   F O U N D",
                36, SporefrontColors.InkDark, TextAnchor.MiddleCenter, true);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;

            AddFixedSpacer(matchFoundContainer.transform, 16);

            // Opponent info card (CreateLedgerCard returns a VerticalLayoutGroup)
            var infoCardVLG = UIHelper.CreateLedgerCard(matchFoundContainer.transform, "OpponentInfoCard");
            infoCardVLG.gameObject.AddComponent<LayoutElement>().minHeight = 80;
            infoCardVLG.spacing = 6f;
            infoCardVLG.childAlignment = TextAnchor.UpperCenter;
            infoCardVLG.childForceExpandWidth = true;
            infoCardVLG.childForceExpandHeight = false;
            infoCardVLG.childControlWidth = true;
            infoCardVLG.childControlHeight = true;
            infoCardVLG.padding = new RectOffset(16, 16, 12, 12);

            opponentNameLabel = UIHelper.CreateLabel(infoCardVLG.transform, "Opponent",
                UIConstants.FontSubheader, UIHelper.InkHeaderText, TextAnchor.MiddleCenter, true);
            opponentNameLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            opponentFactionLabel = UIHelper.CreateLabel(infoCardVLG.transform, "Faction: Morel",
                UIConstants.FontBody, UIHelper.InkBodyText, TextAnchor.MiddleCenter);
            opponentFactionLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

            AddFixedSpacer(matchFoundContainer.transform, 12);

            // Ready status
            readyStatusLabel = UIHelper.CreateLabel(matchFoundContainer.transform,
                "Press Ready when prepared",
                UIConstants.FontBody, UIHelper.InkSubText, TextAnchor.MiddleCenter);
            readyStatusLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            AddFixedSpacer(matchFoundContainer.transform, 16);

            // Button backdrop
            var backdropGO = UIHelper.CreatePanel(matchFoundContainer.transform, "ReadyBackdrop",
                new Color(SporefrontColors.ParchmentDark.r, SporefrontColors.ParchmentDark.g,
                    SporefrontColors.ParchmentDark.b, 0.50f), cornerRadius: 0);
            var backdropOutline = backdropGO.GetComponent<Outline>();
            if (backdropOutline != null) UnityEngine.Object.Destroy(backdropOutline);
            backdropGO.AddComponent<LayoutElement>().preferredWidth = 200f;
            var backdropVLG = backdropGO.AddComponent<VerticalLayoutGroup>();
            backdropVLG.spacing = 10f;
            backdropVLG.childAlignment = TextAnchor.UpperCenter;
            backdropVLG.childForceExpandWidth = true;
            backdropVLG.childForceExpandHeight = false;
            backdropVLG.childControlWidth = true;
            backdropVLG.childControlHeight = false;
            backdropVLG.padding = new RectOffset(15, 15, 10, 10);

            CreateMenuItem(backdropGO.transform, "Ready", SporefrontColors.InkDark, OnReadyClicked);
            CreateMenuItem(backdropGO.transform, "Cancel", SporefrontColors.InkFaded, OnCancelMatchClicked);

            AddFlexSpacer(matchFoundContainer.transform);

            matchFoundContainer.SetActive(false);
        }

        // ================================================================
        // Screen Transitions
        // ================================================================

        private void ShowScreen(MatchmakingScreen screen)
        {
            currentScreen = screen;
            factionSelectContainer.SetActive(screen == MatchmakingScreen.FactionSelect);
            searchingContainer.SetActive(screen == MatchmakingScreen.Searching);
            matchFoundContainer.SetActive(screen == MatchmakingScreen.MatchFound);
        }

        // ================================================================
        // Public API
        // ================================================================

        public void Show()
        {
            panel.SetActive(true);
            ShowScreen(MatchmakingScreen.FactionSelect);
            localReady = false;
            opponentReady = false;
            pendingMatch = null;
        }

        public void Hide()
        {
            // Clean up if we're still in queue
            if (MatchmakingService.Instance.IsInQueue || MatchmakingService.Instance.IsMatched)
            {
                MatchmakingService.Instance.LeaveQueue(null);
            }
            MatchmakingService.Instance.StopListeningForReady();
            panel.SetActive(false);
        }

        public bool IsVisible => panel != null && panel.activeSelf;

        // ================================================================
        // Update
        // ================================================================

        private void Update()
        {
            if (!IsVisible) return;

            if (currentScreen == MatchmakingScreen.Searching)
            {
                MatchmakingService.Instance.Update(Time.deltaTime);

                // Update timer display
                float elapsed = MatchmakingService.Instance.QueueElapsedTime;
                int minutes = (int)(elapsed / 60f);
                int seconds = (int)(elapsed % 60f);
                if (searchTimerLabel != null)
                    searchTimerLabel.text = string.Format("{0}:{1:00}", minutes, seconds);
            }
            else if (currentScreen == MatchmakingScreen.MatchFound && !localReady)
            {
                // Ready timeout (only counts down if we haven't readied yet)
                readyTimer += Time.deltaTime;
                if (readyTimer >= ReadyTimeout)
                {
                    OnReadyTimeout();
                }
            }
        }

        // ================================================================
        // Button Handlers
        // ================================================================

        private void OnFindGameClicked()
        {
#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            var auth = AuthService.Instance;
            if (auth.CurrentState != AuthState.SignedIn)
            {
                Debug.LogWarning("[MatchmakingPanel] Not signed in");
                return;
            }

            Guid localPlayerID = Guid.NewGuid();

            // Subscribe to match found event
            MatchmakingService.Instance.OnMatchFound -= HandleMatchFound;
            MatchmakingService.Instance.OnMatchFound += HandleMatchFound;
            MatchmakingService.Instance.OnMatchCancelled -= HandleMatchCancelled;
            MatchmakingService.Instance.OnMatchCancelled += HandleMatchCancelled;

            MatchmakingService.Instance.EnterQueue(selectedFaction.ToString(), localPlayerID, (success, error) =>
            {
                if (success)
                {
                    ShowScreen(MatchmakingScreen.Searching);
                }
                else
                {
                    Debug.LogWarning(string.Format("[MatchmakingPanel] Failed to enter queue: {0}", error));
                }
            });
#else
            Debug.LogWarning("[MatchmakingPanel] Online matchmaking requires Firebase");
#endif
        }

        private void OnCancelSearchClicked()
        {
            MatchmakingService.Instance.OnMatchFound -= HandleMatchFound;
            MatchmakingService.Instance.OnMatchCancelled -= HandleMatchCancelled;
            MatchmakingService.Instance.LeaveQueue(success =>
            {
                if (success)
                {
                    ShowScreen(MatchmakingScreen.FactionSelect);
                }
                else
                {
                    Debug.LogWarning("[MatchmakingPanel] Failed to leave queue — retrying");
                    // Show faction select anyway to avoid UI deadlock, but log the issue
                    ShowScreen(MatchmakingScreen.FactionSelect);
                }
            });
        }

        private void OnReadyClicked()
        {
            if (localReady || !pendingMatch.HasValue) return;

            localReady = true;
            UpdateReadyStatus();

            MatchmakingService.Instance.ConfirmReady(pendingMatch.Value.gameID, success =>
            {
                if (!success)
                {
                    Debug.LogWarning("[MatchmakingPanel] Failed to confirm ready");
                    localReady = false;
                    UpdateReadyStatus();
                }
            });
        }

        private void OnCancelMatchClicked()
        {
            MatchmakingService.Instance.StopListeningForReady();
            MatchmakingService.Instance.OnMatchFound -= HandleMatchFound;
            MatchmakingService.Instance.OnMatchCancelled -= HandleMatchCancelled;
            MatchmakingService.Instance.LeaveQueue(success =>
            {
                pendingMatch = null;
                localReady = false;
                opponentReady = false;
                ShowScreen(MatchmakingScreen.FactionSelect);
            });
        }

        private void OnReadyTimeout()
        {
            Debug.Log("[MatchmakingPanel] Ready timeout — cancelling match");
            OnCancelMatchClicked();
        }

        // ================================================================
        // Event Handlers
        // ================================================================

        private void HandleMatchFound(MatchResult result)
        {
            pendingMatch = result;
            localReady = false;
            opponentReady = false;
            readyTimer = 0f;

            // Update UI
            if (opponentNameLabel != null)
                opponentNameLabel.text = result.opponentDisplayName;
            if (opponentFactionLabel != null)
                opponentFactionLabel.text = string.Format("Faction: {0}", result.opponentFaction);

            UpdateReadyStatus();
            ShowScreen(MatchmakingScreen.MatchFound);

            // Listen for ready-up changes on the game doc
            MatchmakingService.Instance.ListenForReady(result.gameID, HandleReadyPlayersChanged);
        }

        private void HandleMatchCancelled()
        {
            pendingMatch = null;
            localReady = false;
            opponentReady = false;
            ShowScreen(MatchmakingScreen.FactionSelect);
        }

        private void HandleReadyPlayersChanged(List<string> readyPlayers)
        {
            if (!pendingMatch.HasValue) return;

#if FIREBASE_AUTH && FIREBASE_FIRESTORE
            var uid = AuthService.Instance.CurrentUID;
            var oppUID = pendingMatch.Value.opponentUID;

            localReady = readyPlayers.Contains(uid);
            opponentReady = readyPlayers.Contains(oppUID);

            UpdateReadyStatus();

            // Both ready — launch game!
            if (localReady && opponentReady)
            {
                Debug.Log("[MatchmakingPanel] Both players ready — launching game!");

                // Unsubscribe from events
                MatchmakingService.Instance.OnMatchFound -= HandleMatchFound;
                MatchmakingService.Instance.OnMatchCancelled -= HandleMatchCancelled;
                MatchmakingService.Instance.StopListeningForReady();

                // Clean up matchmaking doc
                MatchmakingService.Instance.LeaveQueue(null);

                OnGameReady?.Invoke(pendingMatch.Value);
            }
#endif
        }

        // ================================================================
        // UI Helpers
        // ================================================================

        private void UpdateFactionSelection()
        {
            for (int i = 0; i < factionButtons.Count; i++)
            {
                var colors = factionButtons[i].colors;
                if (i == selectedFactionIndex)
                {
                    colors.normalColor = SporefrontColors.InkDark;
                    var text = factionButtons[i].GetComponentInChildren<Text>();
                    if (text != null) text.color = SporefrontColors.ParchmentLight;
                }
                else
                {
                    colors.normalColor = Color.clear;
                    var text = factionButtons[i].GetComponentInChildren<Text>();
                    if (text != null) text.color = SporefrontColors.InkDark;
                }
                factionButtons[i].colors = colors;
            }
            UpdateFactionInfo();
        }

        private void UpdateFactionInfo()
        {
            if (factionInfoLabel == null) return;

            switch (selectedFaction)
            {
                case FactionType.Morel:
                    factionInfoLabel.text = "Infantry & Woodland Stealth";
                    break;
                case FactionType.Muscaria:
                    factionInfoLabel.text = "Aggressive Poison & Mountain";
                    break;
                default:
                    factionInfoLabel.text = "";
                    break;
            }
        }

        private void UpdateReadyStatus()
        {
            if (readyStatusLabel == null) return;

            if (localReady && opponentReady)
                readyStatusLabel.text = "Both players ready! Starting game...";
            else if (localReady)
                readyStatusLabel.text = "Waiting for opponent to ready up...";
            else if (opponentReady)
                readyStatusLabel.text = "Opponent is ready! Press Ready to start.";
            else
                readyStatusLabel.text = "Press Ready when prepared";
        }

        private void CreateMenuItem(Transform parent, string text, Color textColor, Action onClick)
        {
            var container = new GameObject("MenuItem_" + text, typeof(RectTransform), typeof(Image));
            container.transform.SetParent(parent, false);
            container.GetComponent<Image>().color = Color.clear;
            container.AddComponent<LayoutElement>().preferredHeight = 46f;

            var containerVLG = container.AddComponent<VerticalLayoutGroup>();
            containerVLG.spacing = 0f;
            containerVLG.childAlignment = TextAnchor.MiddleCenter;
            containerVLG.childForceExpandWidth = true;
            containerVLG.childForceExpandHeight = false;
            containerVLG.childControlWidth = true;
            containerVLG.childControlHeight = false;

            // Hover overlay
            var hoverGO = new GameObject("HoverOverlay", typeof(RectTransform), typeof(RawImage));
            hoverGO.transform.SetParent(container.transform, false);
            var hoverRT = hoverGO.GetComponent<RectTransform>();
            hoverRT.anchorMin = Vector2.zero;
            hoverRT.anchorMax = Vector2.one;
            hoverRT.offsetMin = Vector2.zero;
            hoverRT.offsetMax = Vector2.zero;
            var hoverRaw = hoverGO.GetComponent<RawImage>();
            hoverRaw.texture = hoverGradientTexture;
            hoverRaw.enabled = false;
            hoverRaw.raycastTarget = false;
            var hoverIgnore = hoverGO.AddComponent<LayoutElement>();
            hoverIgnore.ignoreLayout = true;

            // Label
            var label = UIHelper.CreateLabel(container.transform, text,
                24, textColor, TextAnchor.MiddleCenter, false);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;

            // Button behavior
            var btn = container.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());

            // Hover events (same pattern as GameSetupPanel)
            var trigger = container.AddComponent<EventTrigger>();
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((_) => hoverRaw.enabled = true);
            trigger.triggers.Add(enterEntry);
            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((_) => hoverRaw.enabled = false);
            trigger.triggers.Add(exitEntry);
        }

        private void AddFlexSpacer(Transform parent)
        {
            var spacer = new GameObject("FlexSpacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);
            spacer.GetComponent<LayoutElement>().flexibleHeight = 1;
        }

        private void AddFixedSpacer(Transform parent, float height)
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = height;
        }
    }
}
