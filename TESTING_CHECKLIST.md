# Sporefront Online Integration — Testing Checklist

## Prerequisites
- Firebase project created with Auth (Email/Password enabled) and Firestore
- `FIREBASE_AUTH;FIREBASE_FIRESTORE` defined in Player Settings (already set for Standalone)
- `google-services.json` (Android) or `GoogleService-Info.plist` (iOS) in project if testing mobile
- Two test accounts (create via Register flow)


# To Fix!
- Font on the home screen for forgot password and the register is the same font as the backgtound, cant see. 
- In game menu not displaying
- Testing notes say to test play/pause and game speed, are those things we implemented? I don't think so. But I could be wrong. 
- Not seeing any warning for starvation. 
- After winning/losing, delete game and don't allow user to resume. 
- Online "Find Game dones't do anything

[MatchmakingPanel] Failed to enter queue: Missing or insufficient permissions.
UnityEngine.Debug:LogWarning (object)
Sporefront.Visual.MatchmakingPanel:<OnFindGameClicked>b__39_0 (bool,string) (at Assets/Scripts/Visual/MatchmakingPanel.cs:480)
Sporefront.Engine.MatchmakingService/<>c__DisplayClass41_0:<EnterQueue>b__0 (System.Threading.Tasks.Task) (at Assets/Scripts/Engine/MatchmakingService.cs:132)
Firebase.Extensions.TaskExtension/<>c__DisplayClass0_1:<ContinueWithOnMainThread>b__1 () (at /Users/runner/work/firebase-unity-sdk/firebase-unity-sdk/app/task_extension/TaskExtension.cs:45)
Firebase.Dispatcher/<>c__DisplayClass5_0`1<bool>:<RunAsync>b__0 () (at /Users/runner/work/firebase-unity-sdk/firebase-unity-sdk/app/platform/Dispatcher.cs:77)
Firebase.ExceptionAggregator:Wrap (System.Action) (at /Users/runner/work/firebase-unity-sdk/firebase-unity-sdk/app/platform/ExceptionAggregator.cs:112)
Firebase.Dispatcher:PollJobs () (at /Users/runner/work/firebase-unity-sdk/firebase-unity-sdk/app/platform/Dispatcher.cs:123)
Firebase.Platform.FirebaseHandler:Update () (at /Users/runner/work/firebase-unity-sdk/firebase-unity-sdk/app/platform/Unity/FirebaseHandler.cs:207)
Firebase.Platform.FirebaseMonoBehaviour:Update () (at /Users/runner/work/firebase-unity-sdk/firebase-unity-sdk/app/platform/Unity/FirebaseMonoBehaviour.cs:45)

- "Back" on match making does not go back to the main menu. Just black screen. 


---

## 1. Auth — Registration & Sign-In

### Email/Password Registration
- [x] Open game, auth panel appears
- [x] Click "Don't have an account? Register" toggle — form switches to Register mode
- [x] Confirm password field appears, submit button says "Register"
- [x] Register with valid email + password (6+ chars) — account created, proceeds to display name
- [x] Register with mismatched passwords — error "Passwords do not match"
- [x] Register with short password (<6 chars) — error "Password must be at least 6 characters"
- [x] Register with already-used email — error message from Firebase

### Email/Password Sign-In
- [x] Toggle back to "Sign In" mode — confirm password field hides, submit says "Sign In"
- [x] Sign in with valid credentials — proceeds to display name (if new) or main menu
- [x] Sign in with wrong password — error message
- [x] Sign in with non-existent email — error message

### Display Name
- [x] After first registration, DisplayNamePanel appears
- [x] Enter a display name, submit — name saved, proceeds to main menu
- [x] Display name persists on subsequent sign-ins

### Google Sign-In (Desktop)
- [x] Google sign-in button is **hidden** in Unity Editor / Standalone builds
- [ ] (Mobile only) Google button visible and functional on iOS/Android

### Account Panel
- [x] Open account panel after sign-in — shows parchment theme, current user info
- [x] Sign out works — returns to auth panel

### Forgot Password
- [x] Click "Forgot Password?" in sign-in mode — sends reset email (check Firebase Console)

---

## 2. Offline Game — Regression Check

### Verify existing functionality still works
- [x] Start a new local game (vs AI) — game loads and plays normally
- [ ] In-game menu: save/load buttons **visible** in offline game
- [ ] In-game menu: surrender button **hidden** in offline game
- [ ] Pause/resume works in offline game
- [ ] Game speed change works in offline game
- [ ] Save game, quit, load game — state restored correctly

---

## 3. Win Conditions (Testable Offline)

### City Center Destruction
- [x] Destroy AI's city center — GameOverChange fires with `CityCenterDestroyed` reason
- [x] Winner sees victory message, loser sees defeat message (perspective-correct text)
- [ x] `isGameOver` flag prevents further engine updates

### Starvation
- [x] Drain a player's food to 0 for 60s — GameOverChange fires with `Starvation` reason
- [x] Timer resets if food goes above 0 before threshold

### Grace Period
- [x] In first 30 seconds of a new game, no win conditions fire even if conditions are met

### Game Over Guard
- [x] After game over, engine `Update()` returns early (no further combat/movement/resource ticks)

---

## 4. Online Game — Matchmaking

### Enter Queue
- [ ] Sign in on Device A, click "Online Match"
- [ ] MatchmakingPanel opens — select faction, click "Find Match"
- [ ] Status shows searching, cancel button available
- [ ] Cancel search — returns to faction select, leaves Firestore queue

### Match Found
- [ ] Sign in on Device B, enter queue with different account
- [ ] Both devices receive match found notification
- [ ] Ready-up screen appears with 30s countdown
- [ ] Both click Ready — game starts

### Queue Timeout
- [ ] Enter queue alone, wait 2 minutes — auto-cancels with "No match found" message

### Ready Timeout
- [ ] Match found, one player doesn't click Ready within 30s — match cancelled for both

---

## 5. Online Game — Core Gameplay

### Command Streaming
- [ ] Device A builds a building — Device B sees it appear
- [ ] Device B trains units — Device A sees garrison count update
- [ ] Both players can move armies and see opponent movements
- [ ] Deploy army on Device A — Device B sees the army

### AI Commands (Host)
- [ ] AI hunts animals — non-host sees villagers move to hunt
- [ ] AI upgrades buildings — non-host sees upgrade start/complete
- [ ] AI trains units, deploys armies, gathers resources — all visible on non-host
- [ ] AI research — non-host sees research complete notifications

### Game Speed & Pause (Online Locks)
- [ ] Attempt to change game speed in online mode — no effect (locked to 1.0x)
- [ ] Attempt to pause in online mode — no effect (blocked)

### In-Game Menu (Online Mode)
- [ ] Open in-game menu — surrender button visible (red)
- [ ] Save/load buttons hidden in online game

---

## 6. Online Game — Surrender

- [ ] Click surrender button — "Are you sure?" confirmation appears
- [ ] Cancel confirmation — returns to game
- [ ] Confirm surrender — GameOverChange with `Resignation` reason
- [ ] Surrendering player sees defeat, opponent sees victory
- [ ] Firestore session status updates to `Finished`

---

## 7. Online Game — Disconnect & Reconnect

### Disconnect Detection
- [ ] During online game, kill Device B's network (airplane mode / kill app)
- [ ] Device A shows disconnect banner with countdown timer after ~60s
- [ ] Timer counts down from 3:00 (AbandonTimeoutSeconds = 180)
- [ ] Banner shows "Leave Game" button

### Reconnection
- [ ] Restore Device B's network within timeout
- [ ] Device A: banner disappears, game resumes
- [ ] Device B: can rejoin the game

### Abandon (Timeout Expires)
- [ ] Keep Device B disconnected past 3 minute timeout
- [ ] Device A: auto-win triggered — "Your opponent has abandoned the game"
- [ ] Firestore session updated to Finished

### Leave Game Button
- [ ] Click "Leave Game" on disconnect banner — awards victory, ends game

### Rejoin Flow
- [ ] After disconnect, Device B returns to main menu
- [ ] "Rejoin Online Game" button appears
- [ ] Click rejoin — loads latest snapshot, replays pending commands, game resumes
- [ ] If rejoin fails after 5 retries — error message, state cleaned up, button hidden

### Rejoin After Sign-In
- [ ] Close and reopen app on Device B while game is active
- [ ] Sign in — `CheckForActiveOnlineGame` finds the session
- [ ] "Rejoin Online Game" button appears automatically

---

## 8. Online Game — Command Retry & Desync

### Command Retry
- [ ] (Simulated) Command submit fails — retries with exponential backoff (1s, 2s, 4s)
- [ ] After 3 failed retries — `OnCommandSubmitFailed` event fires
- [ ] Error message shown to player: "Network Error: A command failed to sync"

### Desync Detection
- [ ] Every 10th command includes a `stateHash` in the Firestore command doc
- [ ] If hashes mismatch — log says "DESYNC DETECTED", warning shown to player
- [ ] Warning includes message about potential resync

### Self-Echo Prevention
- [ ] Execute a command locally — it does NOT execute a second time when the Firestore listener echoes it back
- [ ] Verify by checking logs: no "duplicate command" warnings

---

## 9. Online Game — Snapshots

### Periodic Snapshots
- [ ] After 100 commands OR 5 minutes — snapshot auto-created in Firestore
- [ ] Check `games/{gameID}/snapshots/` collection — snapshot doc exists
- [ ] Snapshot includes `gameVersion` field matching `Application.version`

### Snapshot Retention
- [ ] After 4th snapshot — only 3 most recent remain (oldest pruned)

### Snapshot Load Timeout
- [ ] If snapshot load hangs for 15s — timeout fires, triggers retry

---

## 10. Security

### Sender Validation
- [ ] Remote command with empty `senderUID` — rejected (check logs)
- [ ] Remote command with unknown `senderUID` — rejected (check logs)
- [ ] AI command from non-host sender — rejected (check logs)
- [ ] Player command where `senderUID`'s playerID doesn't match command's `playerID` — rejected

---

## 11. Serialization Robustness

### Malformed Data
- [ ] Malformed JSON in a player command — graceful null return, no crash (SafeFromJson)
- [ ] Malformed JSON in an AI command — graceful null return, no crash (null checks)
- [ ] Parallel array length mismatch (compositionKeys vs compositionValues) — handled gracefully
- [ ] Invalid enum values — TryParse returns null, command ignored

### Faction Persistence
- [ ] Create online game, both players select factions
- [ ] Check Firestore `games/{id}` — both players have `faction` field (never missing)
- [ ] Rejoin game — faction bonuses still active

### Seed Integrity
- [ ] Create online game — seed stored as hex string in Firestore (not truncated long)
- [ ] Both players generate identical maps from the same seed

---

## 12. UI Theme Consistency

- [x] Auth panel: parchment background, themed buttons, readable text
- [ ] Account panel: matches auth panel theme
- [ ] Display name panel: matches theme
- [ ] Disconnect banner: dark background, clear countdown text, "Leave Game" button styled red
- [ ] Matchmaking panel: consistent with game's visual style

---

## 13. Performance & Cleanup

### Memory / Event Leaks
- [ ] Play an online game to completion — return to main menu
- [ ] Start a second online game — no duplicate event handlers (check for doubled commands)
- [ ] `desyncWarningShown` resets between games (desync warning shows again if needed)

### State Cleanup on Failure
- [ ] Force CreateGame failure (e.g., no internet) — `gameStarted`, `isOnlineGame`, `onlineGameID` all reset
- [ ] Can start a new offline game after failed online game creation

### PruneLocalCommandIDs
- [ ] After snapshot creation, old command IDs are cleared (prevents unbounded HashSet growth)

---

## Quick Smoke Test (Minimum Viable)
If time is limited, test these core flows:

1. [x] Register a new account with email/password
2. [x] Start a local game vs AI — verify it still works
3. [x] Destroy AI city center — game over triggers correctly
4. [ ] Start an online game between two devices via matchmaking
5. [ ] Verify commands stream both ways (build, move, train)
6. [ ] One player surrenders — both see correct result
7. [ ] Disconnect one device — banner appears on other, rejoin works
