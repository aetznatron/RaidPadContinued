using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace RaidPad
{
    /// <summary>
    /// Optional bridge to the "Pause Continued" client mod (BepInEx GUID <c>com.rejzbihai.pause</c>,
    /// assembly <c>rejzbihai.Pause.dll</c>). That mod owns the pause logic; RaidPad only asks it to
    /// toggle, so both keep a single source of truth for <c>IsPaused</c>.
    ///
    /// Nothing is referenced at compile time — the type is looked up at runtime and the mod stays
    /// optional: without it, the combo does nothing and one info line is logged.
    ///
    /// The pause mod toggles inside <c>PauseController.Update()</c>:
    /// <code>
    /// IsPaused = !IsPaused;
    /// if (IsPaused) Pause(); else { Unpause(); _fovFixCoroutine = StartCoroutine(CoFixFovAfterUnpause()); }
    /// </code>
    /// which is reproduced here over reflection.
    /// </summary>
    internal static class RaidPadPauseBridge
    {
        private const string ControllerTypeName = "Pause.PauseController";

        private static bool resolved;
        private static bool missingLogged;
        private static bool readyLogged;

        private static Type controllerType;
        private static PropertyInfo isPausedProperty;
        private static FieldInfo isPausedBackingField;
        private static MethodInfo pauseMethod;
        private static MethodInfo unpauseMethod;
        private static MethodInfo fixFovMethod;
        private static FieldInfo fixFovCoroutineField;

        /// <summary>Pauses the raid, or resumes it when it is already paused.</summary>
        public static void Toggle()
        {
            if (!Resolve())
            {
                return;
            }

            MonoBehaviour controller = null;
            try
            {
                var found = UnityEngine.Object.FindObjectOfType(controllerType);
                controller = found as MonoBehaviour;
            }
            catch (Exception ex)
            {
                RaidPadPlugin.Log.LogError($"RaidPad: could not look up the pause controller: {ex.Message}");
                return;
            }

            if (controller == null)
            {
                // no raid running - the pause mod creates its controller with the game world
                return;
            }

            try
            {
                if (!GetPaused())
                {
                    SetPaused(true);
                    pauseMethod.Invoke(controller, null);
                }
                else
                {
                    SetPaused(false);
                    unpauseMethod.Invoke(controller, null);
                    StartFovFix(controller);
                }
            }
            catch (Exception ex)
            {
                RaidPadPlugin.Log.LogError($"RaidPad: pause toggle failed: {ex}");
            }
        }

        private static bool Resolve()
        {
            if (resolved)
            {
                return controllerType != null;
            }

            resolved = true;

            const BindingFlags any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = null;
                try
                {
                    type = assembly.GetType(ControllerTypeName, false);
                }
                catch
                {
                    // dynamic/reflection-only assemblies can throw, skip them
                }

                if (type == null)
                {
                    continue;
                }

                controllerType = type;
                isPausedProperty = type.GetProperty("IsPaused", any);
                isPausedBackingField = type.GetField("<IsPaused>k__BackingField", any);
                pauseMethod = type.GetMethod("Pause", any);
                unpauseMethod = type.GetMethod("Unpause", any);
                fixFovMethod = type.GetMethod("CoFixFovAfterUnpause", any);
                fixFovCoroutineField = type.GetField("_fovFixCoroutine", any);

                if (pauseMethod == null || unpauseMethod == null || (isPausedProperty == null && isPausedBackingField == null))
                {
                    RaidPadPlugin.Log.LogWarning(
                        "RaidPad: Pause Continued was found but its API does not match what RaidPad expects - pause combo disabled");
                    controllerType = null;
                    return false;
                }

                if (!readyLogged)
                {
                    readyLogged = true;
                    RaidPadPlugin.Log.LogInfo($"RaidPad: pause combo bridged to Pause Continued ({assembly.GetName().Name})");
                }

                return true;
            }

            if (!missingLogged)
            {
                missingLogged = true;
                RaidPadPlugin.Log.LogInfo("RaidPad: Pause Continued (com.rejzbihai.pause) is not installed - RB+Back does nothing");
            }

            return false;
        }

        private static bool GetPaused()
        {
            if (isPausedProperty != null)
            {
                return (bool)isPausedProperty.GetValue(null);
            }

            return (bool)isPausedBackingField.GetValue(null);
        }

        private static void SetPaused(bool value)
        {
            if (isPausedProperty != null)
            {
                isPausedProperty.SetValue(null, value);
                return;
            }

            isPausedBackingField.SetValue(null, value);
        }

        /// <summary>
        /// The pause mod restores the camera FOV through a coroutine after unpausing; starting it
        /// through the same field keeps the mod's own state consistent.
        /// </summary>
        private static void StartFovFix(MonoBehaviour controller)
        {
            if (fixFovMethod == null || fixFovCoroutineField == null)
            {
                return;
            }

            if (!(fixFovMethod.Invoke(controller, null) is IEnumerator routine))
            {
                return;
            }

            fixFovCoroutineField.SetValue(controller, controller.StartCoroutine(routine));
        }
    }
}
