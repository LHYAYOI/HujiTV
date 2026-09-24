using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Laboratory.ReplayCapture
{
    public static class ReplaySaveInput
    {
#if ENABLE_INPUT_SYSTEM
        private static readonly Dictionary<KeyCode, Key> Keys = new Dictionary<KeyCode, Key>();

        private static Key ConvertKey(KeyCode code)
        {
            if (Keys.TryGetValue(code, out var key)) return key;
            string name = code.ToString();
            if (name.StartsWith("Alpha")) name = "Digit" + name.Substring(5);
            else if (name.StartsWith("Keypad")) name = "Numpad" + name.Substring(6);
            switch (code)
            {
                case KeyCode.Return: name = "Enter"; break;
                case KeyCode.LeftControl: name = "LeftCtrl"; break;
                case KeyCode.RightControl: name = "RightCtrl"; break;
                case KeyCode.Numlock: name = "NumLock"; break;
                case KeyCode.Print: name = "PrintScreen"; break;
            }
            if (!Enum.TryParse(name, true, out key)) key = Key.None;
            Keys[code] = key;
            return key;
        }
#endif
        public static bool WasPressed(KeyCode code)
        {
            if (code == KeyCode.None) return false;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            var key = ConvertKey(code);
            return keyboard != null && key != Key.None && keyboard[key].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(code);
#else
            return false;
#endif
        }
    }
}
