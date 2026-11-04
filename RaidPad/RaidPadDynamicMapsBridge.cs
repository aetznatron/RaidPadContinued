using System;
using System.Reflection;
using UnityEngine;

namespace RaidPad
{
    /// <summary>
    /// Optional bridge to "Dynamic Maps" (BepInEx GUID <c>com.mpstark.dynamicmaps</c>,
    /// <c>DynamicMaps.dll</c>). The mod owns the map UI: a peeking overlay in raid
    /// (<c>MapPeekComponent</c>) and the map view itself (<c>ModdedMapScreen</c>/<c>MapView</c>).
    ///
    /// Nothing is referenced at compile time. The mod stays optional: without it the map combo
    /// does nothing and one info line is logged.
    ///
    /// Two quirks of that mod matter here:
    /// - peek is "hold to keep open" by default (<c>HoldForPeek</c>), so its own Update() would close
    ///   a peek we opened with a button press on the next frame. The component flag is switched to
    ///   toggle mode before opening.
    /// - opening a peek is not just Show(): it saves the main map position, ends the mini map and
    ///   passes the previous mini map state to BeginPeek(), which is reproduced here.
    /// </summary>
    internal static class RaidPadDynamicMapsBridge
    {
        private const string PeekTypeName = "DynamicMaps.UI.Components.MapPeekComponent";
        private const string ScreenTypeName = "DynamicMaps.UI.ModdedMapScreen";
        private const string ViewTypeName = "DynamicMaps.UI.Components.MapView";

        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool resolved;
        private static bool missingLogged;
        private static bool failureLogged;

        private static Type peekType;
        private static Type screenType;
        private static Component cachedPeek;
        private static float nextPeekScan;

        private static PropertyInfo isPeeking;
        private static PropertyInfo showingMiniMap;
        private static PropertyInfo wasMiniMapActive;
        private static PropertyInfo holdForPeek;
        private static PropertyInfo mapScreen;
        private static MethodInfo beginPeek;
        private static MethodInfo endPeek;
        private static MethodInfo endMiniMap;

        private static PropertyInfo mapView;
        private static MethodInfo saveMainMapPos;
        private static MethodInfo centerOnPlayer;

        private static PropertyInfo zoomMain;
        private static PropertyInfo selectedLevel;
        private static MethodInfo scaledShiftMap;
        private static MethodInfo clampToMapBounds;
        private static MethodInfo incrementalZoomInto;
        private static MethodInfo selectTopLevel;

        /// <summary>True while the Dynamic Maps peek overlay is open.</summary>
        public static bool IsPeeking()
        {
            if (!Resolve())
            {
                return false;
            }

            var component = FindPeek();
            if (component == null)
            {
                return false;
            }

            try
            {
                return (bool)isPeeking.GetValue(component);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Opens or closes the peek overlay.</summary>
        public static void TogglePeek()
        {
            if (!Resolve())
            {
                return;
            }

            var component = FindPeek();
            if (component == null)
            {
                RaidPadPlugin.Log.LogInfo("RaidPad: Dynamic Maps peek is only available in raid");
                return;
            }

            try
            {
                // the mod's Update() closes the peek again unless the key is held, so force toggle mode
                if (holdForPeek != null)
                {
                    holdForPeek.SetValue(component, false);
                }

                if (!(bool)isPeeking.GetValue(component))
                {
                    var wasMiniMap = (bool)showingMiniMap.GetValue(component);
                    wasMiniMapActive.SetValue(component, wasMiniMap);

                    var screen = mapScreen.GetValue(component);
                    saveMainMapPos.Invoke(screen, null);
                    endMiniMap.Invoke(component, null);
                    beginPeek.Invoke(component, new object[] { wasMiniMap });
                }
                else
                {
                    endPeek.Invoke(component, null);
                }
            }
            catch (Exception ex)
            {
                LogFailure("toggle the peek overlay", ex);
            }
        }

        /// <summary>Shifts the map by one step in map space.</summary>
        public static void Pan(float x, float y)
        {
            var view = GetMapView();
            if (view == null)
            {
                return;
            }

            try
            {
                scaledShiftMap.Invoke(view, new object[] { new Vector2(x, y), 0.25f, false });
                clampToMapBounds.Invoke(view, null);
            }
            catch (Exception ex)
            {
                LogFailure("pan the map", ex);
            }
        }

        /// <summary>Zooms in (1) or out (-1) around the map centre.</summary>
        public static void Zoom(int direction)
        {
            var view = GetMapView();
            if (view == null)
            {
                return;
            }

            try
            {
                var currentZoom = (float)zoomMain.GetValue(view);
                incrementalZoomInto.Invoke(view, new object[] { currentZoom * direction * 0.15f, Vector2.zero, 0f });
            }
            catch (Exception ex)
            {
                LogFailure("zoom the map", ex);
            }
        }

        /// <summary>Changes the map level (floor) by one.</summary>
        public static void ChangeLevel(int direction)
        {
            var view = GetMapView();
            if (view == null)
            {
                return;
            }

            try
            {
                var level = (int)selectedLevel.GetValue(view);
                selectTopLevel.Invoke(view, new object[] { level + direction });
            }
            catch (Exception ex)
            {
                LogFailure("change the map level", ex);
            }
        }

        /// <summary>Centres the map on the player, using the mod's own routine.</summary>
        public static void CenterOnPlayer()
        {
            if (!Resolve())
            {
                return;
            }

            var component = FindPeek();
            if (component == null)
            {
                return;
            }

            try
            {
                var screen = mapScreen.GetValue(component);
                centerOnPlayer.Invoke(screen, new object[] { false });
            }
            catch (Exception ex)
            {
                LogFailure("centre the map", ex);
            }
        }

        /// <summary>
        /// Cached lookup: FindObjectOfType() walks every object in the scene and must not run per
        /// frame (it cost ~90 % of the frame rate when called from Update). The component lives for
        /// the whole raid, and Unity's == null also reports a destroyed object, so a rescan only
        /// happens after the raid ends and the object is gone - at most once a second.
        /// </summary>
        private static Component FindPeek()
        {
            if (cachedPeek != null)
            {
                return cachedPeek;
            }

            if (Time.unscaledTime < nextPeekScan)
            {
                return null;
            }

            nextPeekScan = Time.unscaledTime + 1f;

            try
            {
                cachedPeek = UnityEngine.Object.FindObjectOfType(peekType) as Component;
            }
            catch
            {
                cachedPeek = null;
            }

            return cachedPeek;
        }

        private static object GetMapView()
        {
            if (!Resolve())
            {
                return null;
            }

            var component = FindPeek();
            if (component == null)
            {
                return null;
            }

            try
            {
                var screen = mapScreen.GetValue(component);
                return screen == null ? null : mapView.GetValue(screen);
            }
            catch
            {
                return null;
            }
        }

        private static bool Resolve()
        {
            if (resolved)
            {
                return peekType != null;
            }

            resolved = true;

            peekType = FindType(PeekTypeName);
            screenType = FindType(ScreenTypeName);
            var viewType = FindType(ViewTypeName);

            if (peekType == null || screenType == null || viewType == null)
            {
                if (!missingLogged)
                {
                    missingLogged = true;
                    RaidPadPlugin.Log.LogInfo("RaidPad: Dynamic Maps (com.mpstark.dynamicmaps) is not installed - the map combo does nothing");
                }

                peekType = null;
                return false;
            }

            isPeeking = peekType.GetProperty("IsPeeking", Any);
            showingMiniMap = peekType.GetProperty("ShowingMiniMap", Any);
            wasMiniMapActive = peekType.GetProperty("WasMiniMapActive", Any);
            holdForPeek = peekType.GetProperty("HoldForPeek", Any);
            mapScreen = peekType.GetProperty("MapScreen", Any);
            beginPeek = peekType.GetMethod("BeginPeek", Any);
            endPeek = peekType.GetMethod("EndPeek", Any);
            endMiniMap = peekType.GetMethod("EndMiniMap", Any);

            saveMainMapPos = screenType.GetMethod("SaveMainMapPos", Any);
            mapView = screenType.GetProperty("MapView", Any);
            centerOnPlayer = screenType.GetMethod("CenterOnPlayer", Any);

            zoomMain = viewType.GetProperty("ZoomMain", Any);
            selectedLevel = viewType.GetProperty("SelectedLevel", Any);
            scaledShiftMap = viewType.GetMethod("ScaledShiftMap", Any);
            clampToMapBounds = viewType.GetMethod("ClampToMapBounds", Any);
            incrementalZoomInto = viewType.GetMethod("IncrementalZoomInto", Any);
            selectTopLevel = viewType.GetMethod("SelectTopLevel", Any);

            if (isPeeking == null || mapScreen == null || beginPeek == null || endPeek == null ||
                saveMainMapPos == null || mapView == null || scaledShiftMap == null ||
                incrementalZoomInto == null || selectTopLevel == null || centerOnPlayer == null)
            {
                RaidPadPlugin.Log.LogWarning(
                    "RaidPad: Dynamic Maps was found but its API does not match what RaidPad expects - map combo disabled");
                peekType = null;
                return false;
            }

            RaidPadPlugin.Log.LogInfo("RaidPad: map combo bridged to Dynamic Maps");
            return true;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                    // reflection-only / dynamic assemblies can throw, skip them
                }
            }

            return null;
        }

        private static void LogFailure(string what, Exception ex)
        {
            if (failureLogged)
            {
                return;
            }

            failureLogged = true;
            RaidPadPlugin.Log.LogError($"RaidPad: could not {what}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
