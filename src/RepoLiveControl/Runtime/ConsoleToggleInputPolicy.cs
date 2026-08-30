using System;

namespace RepoLiveControl.Runtime
{
    internal enum ConsoleInputAction
    {
        Toggle,
        Close,
        AcceptCompletion,
        SelectPrevious,
        SelectNext,
        Submit
    }

    internal sealed class ConsoleInputGate
    {
        private readonly int[] lastAcceptedFrames;

        internal ConsoleInputGate()
        {
            lastAcceptedFrames = new int[Enum.GetValues(typeof(ConsoleInputAction)).Length];
            for (int index = 0; index < lastAcceptedFrames.Length; index++)
                lastAcceptedFrames[index] = -1;
        }

        internal bool TryAccept(
            ConsoleInputAction action,
            int frame,
            bool legacyPressedThisFrame,
            bool inputSystemPressedThisFrame,
            bool guiPressedThisFrame)
        {
            if (!legacyPressedThisFrame &&
                !inputSystemPressedThisFrame &&
                !guiPressedThisFrame)
            {
                return false;
            }

            int actionIndex = (int)action;
            if (actionIndex < 0 || actionIndex >= lastAcceptedFrames.Length)
                throw new ArgumentOutOfRangeException("action");

            if (frame == lastAcceptedFrames[actionIndex])
                return false;

            lastAcceptedFrames[actionIndex] = frame;
            return true;
        }
    }

    internal static class ConsoleToggleKeyMapping
    {
        internal static string ToInputSystemKeyName(string legacyKeyName)
        {
            string name = legacyKeyName ?? string.Empty;
            if (name.StartsWith("Alpha", StringComparison.Ordinal) &&
                name.Length == "Alpha0".Length &&
                char.IsDigit(name[name.Length - 1]))
            {
                return "Digit" + name[name.Length - 1];
            }

            if (name.StartsWith("Keypad", StringComparison.Ordinal))
                return "Numpad" + name.Substring("Keypad".Length);

            switch (name)
            {
                case "Return":
                    return "Enter";
                case "LeftControl":
                    return "LeftCtrl";
                case "RightControl":
                    return "RightCtrl";
                case "LeftApple":
                case "LeftCommand":
                case "LeftWindows":
                    return "LeftMeta";
                case "RightApple":
                case "RightCommand":
                case "RightWindows":
                    return "RightMeta";
                case "Print":
                case "SysReq":
                    return "PrintScreen";
                case "Break":
                    return "Pause";
                default:
                    return name;
            }
        }
    }
}
