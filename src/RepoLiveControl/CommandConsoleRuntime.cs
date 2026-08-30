using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Photon.Pun;
using RepoLiveControl.Commands;
using RepoLiveControl.Networking;
using RepoLiveControl.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RepoLiveControl
{
    internal sealed class CommandConsoleRuntime : IDisposable
    {
        private const string InputControlName = "RepoCommandConsole.Input";
        private const int WindowId = 198042;
        private const int SuggestionLimit = 8;

        private readonly Plugin plugin;
        private readonly ConfigEntry<KeyCode> toggleKey;
        private readonly ConfigEntry<int> networkEventCode;
        private readonly List<string> history = new List<string>();
        private readonly ConsoleInputGate inputGate = new ConsoleInputGate();

        private Rect windowRect;
        private string input = "/";
        private string result = "Ready. Type /help or use fuzzy autocomplete.";
        private IReadOnlyList<CompletionItem> suggestions = Array.AsReadOnly(new CompletionItem[0]);
        private CompletionCatalog catalog = CompletionCatalog.Empty;
        private int selectedSuggestion;
        private int pendingCaretPosition = -1;
        private int completionCaretPosition = 1;
        private bool open;
        private bool focusInput;
        private bool releaseGuiFocus;
        private bool stylesReady;
        private bool localPermissionKnown;
        private bool localPermissionGranted;
        private long observedPermissionSessionRevision;
        private float catalogRefreshAt;

        private GUIStyle windowStyle;
        private GUIStyle titleStyle;
        private GUIStyle hintStyle;
        private GUIStyle inputStyle;
        private GUIStyle suggestionStyle;
        private GUIStyle selectedSuggestionStyle;
        private GUIStyle resultStyle;
        private Texture2D windowBackground;
        private Texture2D selectedBackground;

        internal CommandConsoleRuntime(Plugin plugin)
        {
            this.plugin = plugin;
            toggleKey = plugin.Config.Bind(
                "Console",
                "ToggleKey",
                KeyCode.F2,
                "Key used to open and close the independent in-game command console.");
            networkEventCode = plugin.Config.Bind(
                "Networking",
                "PhotonEventCode",
                198,
                "Fixed Photon custom event code shared by all clients (3-199). Change only if another mod collides.");

            int configuredCode = Mathf.Clamp(networkEventCode.Value, 3, 199);
            if (configuredCode != networkEventCode.Value)
            {
                networkEventCode.Value = configuredCode;
                plugin.Config.Save();
            }

            Permissions = new PermissionService();
            observedPermissionSessionRevision = Permissions.SessionRevision;
            Network = new CommandNetworkRouter(
                (byte)configuredCode,
                Permissions,
                SetResult);
            windowRect = new Rect(0f, 90f, 860f, 510f);
        }

        internal PermissionService Permissions { get; private set; }

        internal CommandNetworkRouter Network { get; private set; }

        internal string ToggleKeyLabel { get { return toggleKey.Value.ToString(); } }

        internal void Update()
        {
            Network.Update(IsNetworkSessionSceneActive());
            Bridge.PublishPermissionSessionRevision(Permissions.SessionRevision);
            if (observedPermissionSessionRevision != Permissions.SessionRevision)
            {
                observedPermissionSessionRevision = Permissions.SessionRevision;
                localPermissionKnown = false;
                localPermissionGranted = false;
            }

            if (inputGate.TryAccept(
                ConsoleInputAction.Toggle,
                Time.frameCount,
                Input.GetKeyDown(toggleKey.Value),
                IsInputSystemKeyPressedThisFrame(toggleKey.Value),
                false))
            {
                SetOpen(!open);
                return;
            }

            if (!open)
                return;

            if (TryAcceptInputAction(ConsoleInputAction.Close, KeyCode.Escape))
            {
                SetOpen(false);
                return;
            }

            if (TryAcceptInputAction(ConsoleInputAction.AcceptCompletion, KeyCode.Tab))
                AcceptSelectedSuggestion(true);
            else if (TryAcceptInputAction(ConsoleInputAction.SelectPrevious, KeyCode.UpArrow) &&
                     suggestions.Count > 0)
            {
                selectedSuggestion =
                    (selectedSuggestion - 1 + suggestions.Count) % suggestions.Count;
            }
            else if (TryAcceptInputAction(ConsoleInputAction.SelectNext, KeyCode.DownArrow) &&
                     suggestions.Count > 0)
            {
                selectedSuggestion = (selectedSuggestion + 1) % suggestions.Count;
            }
            else if (TryAcceptInputAction(
                         ConsoleInputAction.Submit,
                         KeyCode.Return,
                         KeyCode.KeypadEnter))
            {
                SubmitInput();
            }

            try
            {
                SemiFunc.InputDisableMovement();
                SemiFunc.InputDisableAiming();
                SemiFunc.CursorUnlock(0.1f);
                if (MenuManager.instance != null)
                    MenuManager.instance.TextInputActive();
                if (PlayerController.instance != null)
                    PlayerController.instance.InputDisable(0.1f);
            }
            catch
            {
            }

            if (Time.realtimeSinceStartup >= catalogRefreshAt)
            {
                RefreshCatalog();
                catalogRefreshAt = Time.realtimeSinceStartup + 1f;
            }
        }

        internal void OnGUI()
        {
            ReleaseGuiFocusIfRequested();
            if (!open)
                return;

            EnsureStyles();
            float width = Mathf.Min(900f, Mathf.Max(540f, Screen.width - 40f));
            windowRect.width = width;
            // Eight completion rows plus the result/history panes need a little
            // more than 560 px. Keep the action row visible at the maximum
            // completion count instead of clipping it below the window.
            windowRect.height = Mathf.Min(600f, Mathf.Max(440f, Screen.height - 120f));
            windowRect.x = Mathf.Clamp(windowRect.x, 10f, Mathf.Max(10f, Screen.width - width - 10f));
            windowRect.y = Mathf.Clamp(windowRect.y, 10f, Mathf.Max(10f, Screen.height - windowRect.height - 10f));
            if (windowRect.x <= 0f)
                windowRect.x = (Screen.width - width) * 0.5f;

            HandleKeyboardEvent(Event.current);
            if (!open)
            {
                ReleaseGuiFocusIfRequested();
                return;
            }
            windowRect = GUI.Window(WindowId, windowRect, DrawWindow, string.Empty, windowStyle);
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("REPO COMMAND CONSOLE  •  " + RoleLabel(), titleStyle);
            GUILayout.Label(
                ToggleKeyLabel + " / Esc closes  •  ↑↓ selects  •  Tab accepts  •  Enter runs",
                hintStyle);
            GUILayout.Space(8f);

            GUI.SetNextControlName(InputControlName);
            string edited = GUILayout.TextField(input, inputStyle, GUILayout.Height(38f));
            bool inputChanged = !string.Equals(edited, input, StringComparison.Ordinal);
            if (inputChanged)
                input = edited;
            if (focusInput)
            {
                GUI.FocusControl(InputControlName);
                focusInput = false;
            }

            TextEditor editor = GetFocusedInputEditor();
            if (pendingCaretPosition >= 0 && editor != null &&
                Event.current.type == EventType.Repaint)
            {
                int caret = Mathf.Clamp(pendingCaretPosition, 0, input.Length);
                editor.cursorIndex = caret;
                editor.selectIndex = caret;
                pendingCaretPosition = -1;
            }

            int actualCaret = pendingCaretPosition >= 0
                ? Mathf.Clamp(pendingCaretPosition, 0, input.Length)
                : editor != null
                    ? Mathf.Clamp(editor.cursorIndex, 0, input.Length)
                    : Mathf.Clamp(completionCaretPosition, 0, input.Length);
            bool caretChanged = actualCaret != completionCaretPosition;
            completionCaretPosition = actualCaret;
            if (inputChanged || caretChanged)
            {
                selectedSuggestion = 0;
                RefreshSuggestions();
            }

            GUILayout.Space(6f);
            GUILayout.Label("FUZZY AUTOCOMPLETE", hintStyle);
            if (suggestions.Count == 0)
            {
                GUILayout.Label("No completion for the active argument.", hintStyle);
            }
            else
            {
                for (int index = 0; index < suggestions.Count; index++)
                {
                    CompletionItem suggestion = suggestions[index];
                    string prefix = index == selectedSuggestion ? "▶  " : "    ";
                    GUIStyle style = index == selectedSuggestion
                        ? selectedSuggestionStyle
                        : suggestionStyle;
                    if (GUILayout.Button(
                        prefix + suggestion.Value,
                        style,
                        GUILayout.Height(28f)))
                    {
                        selectedSuggestion = index;
                        AcceptSelectedSuggestion(true);
                    }
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("RESULT", hintStyle);
            GUILayout.Label(
                result,
                resultStyle,
                GUILayout.MinHeight(48f),
                GUILayout.MaxHeight(72f));
            if (history.Count > 0)
                GUILayout.Label(string.Join("\n", history.ToArray()), hintStyle, GUILayout.MaxHeight(72f));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Help", GUILayout.Height(30f)))
            {
                input = "/help";
                SubmitInput();
            }
            if (GUILayout.Button("Clear", GUILayout.Height(30f)))
            {
                input = "/";
                completionCaretPosition = input.Length;
                result = "Ready.";
                history.Clear();
                RefreshSuggestions();
                focusInput = true;
            }
            if (GUILayout.Button("Run", GUILayout.Height(30f)))
                SubmitInput();
            if (GUILayout.Button("Close", GUILayout.Height(30f)))
                SetOpen(false);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 44f));
        }

        private void HandleKeyboardEvent(Event current)
        {
            if (current == null || current.type != EventType.KeyDown)
                return;

            // Every action also has legacy and Input System paths in Update.
            // The per-action gate prevents this IMGUI fallback from firing the
            // same input twice when more than one backend sees the key edge.
            if (current.keyCode == toggleKey.Value)
            {
                if (inputGate.TryAccept(
                    ConsoleInputAction.Toggle,
                    Time.frameCount,
                    false,
                    false,
                    true))
                {
                    SetOpen(!open);
                }
                current.Use();
            }
            else if (current.keyCode == KeyCode.Escape)
            {
                if (AcceptGuiInput(ConsoleInputAction.Close))
                    SetOpen(false);
                current.Use();
            }
            else if (current.keyCode == KeyCode.Tab)
            {
                if (AcceptGuiInput(ConsoleInputAction.AcceptCompletion))
                    AcceptSelectedSuggestion(true);
                current.Use();
            }
            else if (current.keyCode == KeyCode.UpArrow && suggestions.Count > 0)
            {
                if (AcceptGuiInput(ConsoleInputAction.SelectPrevious))
                {
                    selectedSuggestion =
                        (selectedSuggestion - 1 + suggestions.Count) % suggestions.Count;
                }
                current.Use();
            }
            else if (current.keyCode == KeyCode.DownArrow && suggestions.Count > 0)
            {
                if (AcceptGuiInput(ConsoleInputAction.SelectNext))
                    selectedSuggestion = (selectedSuggestion + 1) % suggestions.Count;
                current.Use();
            }
            else if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
            {
                if (AcceptGuiInput(ConsoleInputAction.Submit))
                    SubmitInput();
                current.Use();
            }
        }

        private static bool IsNetworkSessionSceneActive()
        {
            RunManager runManager = RunManager.instance;
            if (runManager == null)
            {
                return NetworkSessionSceneActivationPolicy.ShouldActivate(
                    false, false, false, false, false, false);
            }

            Level current = runManager.levelCurrent;
            if (current == null)
            {
                return NetworkSessionSceneActivationPolicy.ShouldActivate(
                    true, false, false, false, false, false);
            }

            return NetworkSessionSceneActivationPolicy.ShouldActivate(
                true,
                true,
                current == runManager.levelLobby,
                ContainsLevel(runManager.levels, current),
                ContainsLevel(runManager.levelShop, current),
                ContainsLevel(runManager.levelArena, current));
        }

        private static bool ContainsLevel(IList<Level> levels, Level current)
        {
            return levels != null && levels.Contains(current);
        }

        private bool TryAcceptInputAction(
            ConsoleInputAction action,
            KeyCode primaryKey,
            KeyCode secondaryKey = KeyCode.None)
        {
            bool legacyPressed = Input.GetKeyDown(primaryKey) ||
                (secondaryKey != KeyCode.None && Input.GetKeyDown(secondaryKey));
            bool inputSystemPressed = IsInputSystemKeyPressedThisFrame(primaryKey) ||
                (secondaryKey != KeyCode.None &&
                 IsInputSystemKeyPressedThisFrame(secondaryKey));
            return inputGate.TryAccept(
                action,
                Time.frameCount,
                legacyPressed,
                inputSystemPressed,
                false);
        }

        private bool AcceptGuiInput(ConsoleInputAction action)
        {
            return inputGate.TryAccept(
                action,
                Time.frameCount,
                false,
                false,
                true);
        }

        private void SubmitInput()
        {
            string command = (input ?? string.Empty).Trim();
            CommandParseResult parseResult = SlashCommandParser.Parse(command);
            if (!parseResult.Success)
            {
                SetResult("ERROR " + parseResult.ErrorMessage);
                focusInput = true;
                return;
            }

            AddHistory("> " + command);
            result = "PENDING Sending command to " +
                     ((!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient) ? "host executor..." : "lobby host...");
            try
            {
                if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
                {
                    int actorNumber = PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null
                        ? PhotonNetwork.LocalPlayer.ActorNumber
                        : -1;
                    long requiredSessionRevision = Permissions.SessionRevision;
                    Bridge.Enqueue(new ControlRequest(
                        command,
                        CommandRequestSource.LocalConsole,
                        actorNumber,
                        SetResult,
                        requiredSessionRevision,
                        () => Permissions.SessionRevision == requiredSessionRevision));
                }
                else
                {
                    Network.SendRequest(command);
                }
            }
            catch (Exception exception)
            {
                SetResult("ERROR " + exception.Message);
            }
            focusInput = true;
        }

        private void SetResult(string value)
        {
            result = string.IsNullOrWhiteSpace(value) ? "ERROR Empty command response." : value;
            AddHistory(result);
            if (result.IndexOf("granted you", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                localPermissionKnown = true;
                localPermissionGranted = true;
            }
            else if (result.IndexOf("revoked your", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     result.IndexOf("has not granted", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                localPermissionKnown = true;
                localPermissionGranted = false;
            }
            if (Plugin.Log != null)
                Plugin.Log.LogInfo("Console result: " + result);
        }

        private void AddHistory(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            history.Insert(0, value.Length > 140 ? value.Substring(0, 140) + "…" : value);
            while (history.Count > 3)
                history.RemoveAt(history.Count - 1);
        }

        private void AcceptSelectedSuggestion(bool appendSpace)
        {
            if (suggestions.Count == 0)
                return;
            selectedSuggestion = Mathf.Clamp(selectedSuggestion, 0, suggestions.Count - 1);
            CompletionApplication applied = CommandCompletionEngine.ApplyCompletion(
                input,
                suggestions[selectedSuggestion],
                appendSpace);
            input = applied.Text;
            pendingCaretPosition = applied.CaretPosition;
            completionCaretPosition = applied.CaretPosition;
            selectedSuggestion = 0;
            RefreshSuggestions();
            focusInput = true;
        }

        private void RefreshCatalog()
        {
            var grantPlayers = new List<string>();
            var revokePlayers = new List<string>();
            bool canManagePermissions =
                !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
            if (canManagePermissions)
            {
                grantPlayers.AddRange(Permissions.GetGrantCandidates());
                revokePlayers.AddRange(Permissions.GetRevokeCandidates());
            }
            catalog = new CompletionCatalog(
                RuntimeTargetCatalog.GetSelectors(true),
                grantPlayers,
                revokePlayers,
                canManagePermissions);
            RefreshSuggestions();
        }

        private void RefreshSuggestions()
        {
            try
            {
                suggestions = CommandCompletionEngine.GetCompletions(
                    input,
                    Mathf.Clamp(completionCaretPosition, 0, input == null ? 0 : input.Length),
                    catalog,
                    SuggestionLimit);
            }
            catch (Exception exception)
            {
                suggestions = Array.AsReadOnly(new CompletionItem[0]);
                if (Plugin.Log != null)
                    Plugin.Log.LogWarning("Could not refresh command suggestions: " + exception.Message);
            }
            if (selectedSuggestion >= suggestions.Count)
                selectedSuggestion = 0;
        }

        private string RoleLabel()
        {
            if (!PhotonNetwork.InRoom)
                return "LOCAL HOST";
            if (PhotonNetwork.IsMasterClient)
                return "LOBBY HOST";
            if (!localPermissionKnown)
                return "CLIENT • PERMISSION UNKNOWN";
            return localPermissionGranted
                ? "CLIENT • PERMISSION GRANTED"
                : "CLIENT • NOT GRANTED";
        }

        private void SetOpen(bool value)
        {
            open = value;
            if (open)
            {
                if (string.IsNullOrWhiteSpace(input))
                    input = "/";
                windowRect.x = (Screen.width - windowRect.width) * 0.5f;
                windowRect.y = Mathf.Max(20f, Screen.height * 0.08f);
                focusInput = true;
                pendingCaretPosition = input.Length;
                completionCaretPosition = input.Length;
                releaseGuiFocus = false;
                RefreshCatalog();
                result = "Ready. Chat is not required; this console uses its own input path.";
            }
            else
            {
                releaseGuiFocus = true;
            }
            if (Plugin.Log != null)
                Plugin.Log.LogInfo("Command console " + (open ? "opened." : "closed."));
        }

        private static TextEditor GetFocusedInputEditor()
        {
            if (!string.Equals(
                GUI.GetNameOfFocusedControl(),
                InputControlName,
                StringComparison.Ordinal))
            {
                return null;
            }

            return GUIUtility.GetStateObject(
                typeof(TextEditor),
                GUIUtility.keyboardControl) as TextEditor;
        }

        private static bool IsInputSystemKeyPressedThisFrame(KeyCode keyCode)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            Key inputSystemKey;
            if (!Enum.TryParse(
                    ConsoleToggleKeyMapping.ToInputSystemKeyName(keyCode.ToString()),
                    true,
                    out inputSystemKey) ||
                inputSystemKey == Key.None)
            {
                return false;
            }

            return keyboard[inputSystemKey] != null &&
                   keyboard[inputSystemKey].wasPressedThisFrame;
        }

        private void ReleaseGuiFocusIfRequested()
        {
            if (!releaseGuiFocus)
                return;
            GUI.FocusControl(null);
            releaseGuiFocus = false;
        }

        private void EnsureStyles()
        {
            if (stylesReady)
                return;
            stylesReady = true;

            windowBackground = MakeTexture(new Color(0.035f, 0.045f, 0.055f, 0.97f));
            selectedBackground = MakeTexture(new Color(0.16f, 0.24f, 0.19f, 0.98f));

            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = windowBackground;
            windowStyle.padding = new RectOffset(20, 20, 16, 18);

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 22;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = new Color(1f, 0.86f, 0.12f);

            hintStyle = new GUIStyle(GUI.skin.label);
            hintStyle.fontSize = 13;
            hintStyle.wordWrap = true;
            hintStyle.normal.textColor = new Color(0.72f, 0.78f, 0.8f);

            inputStyle = new GUIStyle(GUI.skin.textField);
            inputStyle.fontSize = 20;
            inputStyle.padding = new RectOffset(10, 10, 7, 6);
            inputStyle.normal.textColor = Color.white;
            inputStyle.focused.textColor = Color.white;

            suggestionStyle = new GUIStyle(GUI.skin.button);
            suggestionStyle.alignment = TextAnchor.MiddleLeft;
            suggestionStyle.fontSize = 15;
            suggestionStyle.normal.textColor = new Color(0.86f, 0.9f, 0.91f);

            selectedSuggestionStyle = new GUIStyle(suggestionStyle);
            selectedSuggestionStyle.normal.background = selectedBackground;
            selectedSuggestionStyle.normal.textColor = new Color(0.35f, 1f, 0.56f);
            selectedSuggestionStyle.fontStyle = FontStyle.Bold;

            resultStyle = new GUIStyle(GUI.skin.box);
            resultStyle.alignment = TextAnchor.UpperLeft;
            resultStyle.fontSize = 14;
            resultStyle.wordWrap = true;
            resultStyle.padding = new RectOffset(10, 10, 8, 8);
            resultStyle.normal.textColor = Color.white;
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        public void Dispose()
        {
            Network.Dispose();
            Permissions.Reset();
            if (windowBackground != null)
                UnityEngine.Object.Destroy(windowBackground);
            if (selectedBackground != null)
                UnityEngine.Object.Destroy(selectedBackground);
        }
    }
}
