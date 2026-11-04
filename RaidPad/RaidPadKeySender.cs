using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Configuration;
using UnityEngine;

namespace RaidPad
{
    /// <summary>
    /// Sends a single key stroke to the focused window, used by the configurable
    /// LB+RB+Start action so it can drive another mod's hotkey.
    ///
    /// Unity's <see cref="KeyCode"/> values are not Windows virtual-key codes (KeyCode.A is 97,
    /// VK_A is 65), so the common ranges are translated explicitly and anything that already
    /// matches (digits, space, return, escape, ...) falls through unchanged.
    /// </summary>
    internal static class RaidPadKeySender
    {
        private const uint INPUT_KEYBOARD = 1u;
        private const uint KEYEVENTF_KEYUP = 2u;
        private const uint KEYEVENTF_EXTENDEDKEY = 1u;
        private const uint MAPVK_VK_TO_VSC = 0u;

        private static bool sending;

        /// <summary>Taps the shortcut: modifiers down, main key down/up, modifiers up.</summary>
        public static void Send(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None)
            {
                return;
            }

            if (sending)
            {
                return;
            }

            var mainKey = ToVirtualKey(shortcut.MainKey);
            if (mainKey == 0)
            {
                RaidPadPlugin.Log.LogWarning($"RaidPad: '{shortcut.MainKey}' has no known virtual-key code, keystroke not sent");
                return;
            }

            var modifiers = new List<byte>();
            foreach (var modifier in shortcut.Modifiers)
            {
                var vk = ToVirtualKey(modifier);
                if (vk != 0)
                {
                    modifiers.Add(vk);
                }
            }

            sending = true;

            // off the main thread: the delays keep the game from missing the tap
            Task.Run(() =>
            {
                try
                {
                    foreach (var modifier in modifiers)
                    {
                        SendKey(modifier, false);
                    }

                    SendKey(mainKey, false);
                    Thread.Sleep(40);
                    SendKey(mainKey, true);

                    for (var i = modifiers.Count - 1; i >= 0; i--)
                    {
                        SendKey(modifiers[i], true);
                    }
                }
                catch (Exception ex)
                {
                    RaidPadPlugin.Log.LogError($"RaidPad: could not send '{shortcut}': {ex.Message}");
                }
                finally
                {
                    sending = false;
                }
            });
        }

        private static byte ToVirtualKey(KeyCode key)
        {
            if (key >= KeyCode.A && key <= KeyCode.Z)
            {
                return (byte)(0x41 + (key - KeyCode.A));
            }

            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            {
                return (byte)(0x30 + (key - KeyCode.Alpha0));
            }

            if (key >= KeyCode.F1 && key <= KeyCode.F15)
            {
                return (byte)(0x70 + (key - KeyCode.F1));
            }

            switch (key)
            {
                case KeyCode.Backspace: return 0x08;
                case KeyCode.Tab: return 0x09;
                case KeyCode.Return: return 0x0D;
                case KeyCode.Escape: return 0x1B;
                case KeyCode.Space: return 0x20;
                case KeyCode.PageUp: return 0x21;
                case KeyCode.PageDown: return 0x22;
                case KeyCode.End: return 0x23;
                case KeyCode.Home: return 0x24;
                case KeyCode.LeftArrow: return 0x25;
                case KeyCode.UpArrow: return 0x26;
                case KeyCode.RightArrow: return 0x27;
                case KeyCode.DownArrow: return 0x28;
                case KeyCode.Insert: return 0x2D;
                case KeyCode.Delete: return 0x2E;
                case KeyCode.LeftShift: return 0xA0;
                case KeyCode.RightShift: return 0xA1;
                case KeyCode.LeftControl: return 0xA2;
                case KeyCode.RightControl: return 0xA3;
                case KeyCode.LeftAlt: return 0xA4;
                case KeyCode.RightAlt: return 0xA5;
                case KeyCode.LeftBracket: return 0xDB;
                case KeyCode.RightBracket: return 0xDD;
                case KeyCode.Backslash: return 0xDC;
                case KeyCode.Semicolon: return 0xBA;
                case KeyCode.Quote: return 0xDE;
                case KeyCode.Comma: return 0xBC;
                case KeyCode.Period: return 0xBE;
                case KeyCode.Slash: return 0xBF;
                case KeyCode.BackQuote: return 0xC0;
                case KeyCode.Minus: return 0xBD;
                case KeyCode.Equals: return 0xBB;
                default:
                    var value = (int)key;
                    return value > 0 && value < 0x100 ? (byte)value : (byte)0;
            }
        }

        private static void SendKey(byte vk, bool up)
        {
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = (ushort)MapVirtualKey(vk, MAPVK_VK_TO_VSC),
                dwFlags = (up ? KEYEVENTF_KEYUP : 0u) | (IsExtended(vk) ? KEYEVENTF_EXTENDEDKEY : 0u),
                time = 0u,
                dwExtraInfo = IntPtr.Zero
            };

            SendInput(1u, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private static bool IsExtended(byte vk)
        {
            switch (vk)
            {
                case 0x21:
                case 0x22:
                case 0x23:
                case 0x24:
                case 0x25:
                case 0x26:
                case 0x27:
                case 0x28:
                case 0x2D:
                case 0x2E:
                case 0xA3:
                case 0xA5:
                    return true;
                default:
                    return false;
            }
        }

        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);
    }
}
