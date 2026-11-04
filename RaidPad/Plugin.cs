using SPT.Reflection.Patching;
using BepInEx;
using BepInEx.Bootstrap;
using System.Reflection;
using UnityEngine;
using EFT.UI;
using EFT.UI.DragAndDrop;
using System.Linq;
using EFT.InventoryLogic;
using System.Collections.Generic;
using EFT;
using HarmonyLib;
using BepInEx.Configuration;
using UnityEngine.UI;
using System.Threading.Tasks;
using Sirenix.Utilities;
using EFT.InputSystem;
using UnityEngine.EventSystems;

namespace RaidPad
{
    [BepInPlugin("com.thablackunicorn.raidpad", "RaidPad", PluginInfo.Version)]
    // Targets a minimum of SPT 4.1.x
    [BepInDependency("com.SPT.core", "4.1.0")]
    public class RaidPadPlugin : BaseUnityPlugin
    {
        public static GameObject Hook;
        public static RaidPadClass RaidPadClassComponent;
        public static InputGetKeyDownLeftControlPatch LeftControl;
        public static BepInEx.Logging.ManualLogSource Log;

        // RaidPad
        public static ConfigEntry<int> UserIndex { get; set; }

        // Menu Cursor
        public static ConfigEntry<bool> MenuCursorEnabled { get; set; }
        public static ConfigEntry<bool> MenuCursorCrosshair { get; set; }
        public static ConfigEntry<bool> MenuCursorSwapTriggers { get; set; }
        public static ConfigEntry<float> MenuCursorSpeed { get; set; }
        public static ConfigEntry<float> MenuScrollSpeed { get; set; }
        public static ConfigEntry<KeyCode> MenuRotateKey { get; set; }

        // Keybinds
        public static ConfigEntry<KeyboardShortcut> ComboKey { get; set; }

        // Input
        public static ConfigEntry<float> LSDeadzone { get; set; }
        public static ConfigEntry<float> RSDeadzone { get; set; }
        public static ConfigEntry<float> LTDeadzone { get; set; }
        public static ConfigEntry<float> RTDeadzone { get; set; }
        public static ConfigEntry<float> DoubleClickDelay { get; set; }
        public static ConfigEntry<float> HoldDelay { get; set; }

        // Aim
        public static ConfigEntry<bool> HoldAim { get; set; }
        public static ConfigEntry<Vector2> Sensitivity { get; set; }
        public static ConfigEntry<Vector2> AimingSensitivity { get; set; }
        public static ConfigEntry<bool> InvertY { get; set; }
        public static ConfigEntry<float> AimDeadzone { get; set; }

        // Aim Assist
        public static ConfigEntry<bool> Magnetism { get; set; }
        public static ConfigEntry<float> Stickiness { get; set; }
        public static ConfigEntry<float> StickinessSmooth { get; set; }
        public static ConfigEntry<float> AutoAim { get; set; }
        public static ConfigEntry<float> AutoAimSmooth { get; set; }
        public static ConfigEntry<float> MagnetismRadius { get; set; }
        public static ConfigEntry<float> StickinessRadius { get; set; }
        public static ConfigEntry<float> AutoAimRadius { get; set; }
        public static ConfigEntry<float> Radius { get; set; }
        public static ConfigEntry<bool> ExcludeTeammates { get; set; }


        // Movement
        public static ConfigEntry<float> MovementDeadzone { get; set; }
        public static ConfigEntry<float> DeadzoneBuffer { get; set; }
        public static ConfigEntry<float> LeanSensitivity { get; set; }

        // UI
        public static ConfigEntry<bool> DualsenseIcons { get; set; }
        public static ConfigEntry<float> ScrollSensitivity { get; set; }
        public static ConfigEntry<ERaidPadUseStick> InterfaceStick { get; set; }
        public static ConfigEntry<ERaidPadUseStick> InterfaceSkipStick { get; set; }
        public static ConfigEntry<ERaidPadUseStick> ScrollStick { get; set; }
        public static ConfigEntry<ERaidPadUseStick> WindowStick { get; set; }
        public static ConfigEntry<float> InterfaceStickMoveDelay { get; set; }

        // UI Selected Box
        public static ConfigEntry<Color> SelectColor { get; set; }

        // UI Button Blocks
        public static ConfigEntry<Vector2> BlockPosition { get; set; }
        public static ConfigEntry<Vector2> BlockSize { get; set; }
        public static ConfigEntry<int> BlockSpacing { get; set; }
        public static ConfigEntry<int> BlockIconSpacing { get; set; }
        public static ConfigEntry<int> PressFontSize { get; set; }
        public static ConfigEntry<int> HoldDoubleClickFontSize { get; set; }

        private void Awake()
        {
            // RaidPadClass is static in places, so expose the plugin's log source statically.
            Log = Logger;

            var sptVersion = Chainloader.PluginInfos["com.SPT.core"].Metadata.Version;
            if (sptVersion.Major > 4 || (sptVersion.Major == 4 && sptVersion.Minor > 1))
            {
                Logger.LogError($"RaidPad requires SPT 4.1.x — found {sptVersion}. Mod will not load.");
                enabled = false;
                return;
            }

            // Host the controller component on the plugin's own (BepInEx-managed, persistent)
            // GameObject. A separately-created GameObject was being destroyed before Unity
            // could run its Start()/Update(), which killed the input loop on SPT 4.0.x.
            Hook = this.gameObject;
            RaidPadClassComponent = Hook.AddComponent<RaidPadClass>();
            DontDestroyOnLoad(Hook);
        }

        private void Start()
        {

            UserIndex = Config.Bind("RaidPad", "User Index", 0, new ConfigDescription("XInput slot. 0 = Auto (scan all slots, pick the first connected pad — recommended, and required for DS4Windows / Steam Input / ViGEm virtual pads that may land on any slot). 1-4 = pin a specific slot.", new AcceptableValueRange<int>(0, 4), new ConfigurationManagerAttributes { Order = 100 }));

            MenuCursorEnabled = Config.Bind("Menu Cursor", "Enabled", true, new ConfigDescription("Out of raid, drive the Windows mouse cursor with the pad so you can navigate menus (main menu, stash, traders, flea, settings). Right stick = move, left stick = scroll, left click = A or LT, right click = RT, X/Square = rotate held item, B = back/Escape. In-raid controls are unaffected.", null, new ConfigurationManagerAttributes { Order = 90 }));
            MenuCursorSwapTriggers = Config.Bind("Menu Cursor", "Swap Trigger Clicks", false, new ConfigDescription("Swap which trigger clicks which mouse button in menus. Off: LT = left click, RT = right click. On: LT = right click, RT = left click. A / Cross stays left click either way, and in-raid trigger binds are unaffected.", null, new ConfigurationManagerAttributes { Order = 88 }));
            MenuCursorCrosshair = Config.Bind("Menu Cursor", "Draw Crosshair Overlay", true, new ConfigDescription("Draw RaidPad's own crosshair at the cursor. EFT's native pointer is force-shown while the menu cursor is active, so once you confirm you can see the real cursor you can turn this off. Leave on as a fallback (e.g. exclusive fullscreen may hide the hardware cursor).", null, new ConfigurationManagerAttributes { Order = 85 }));
            MenuCursorSpeed = Config.Bind("Menu Cursor", "Cursor Speed", 1600f, new ConfigDescription("Cursor movement speed in pixels per second at full right-stick deflection.", new AcceptableValueRange<float>(200f, 5000f), new ConfigurationManagerAttributes { Order = 80 }));
            MenuScrollSpeed = Config.Bind("Menu Cursor", "Scroll Speed", 15f, new ConfigDescription("Scroll-wheel notches per second at full left-stick deflection.", new AcceptableValueRange<float>(1f, 60f), new ConfigurationManagerAttributes { Order = 70 }));
            MenuRotateKey = Config.Bind("Menu Cursor", "Rotate Key", KeyCode.R, new ConfigDescription("Keystroke sent when you press X / Square in menus to rotate the held item. Must match EFT's 'Rotate item' bind (default R).", null, new ConfigurationManagerAttributes { Order = 60 }));

            ComboKey = Config.Bind("Keybinds", "LB+RB+Start Key", new KeyboardShortcut(KeyCode.None), new ConfigDescription("Keystroke RaidPad sends when you press LB+RB+Start in raid. Set any key or combination (e.g. F4 or Ctrl+F4) to trigger another mod's hotkey. Leave empty to disable.", null, new ConfigurationManagerAttributes { Order = 200 }));

            LSDeadzone = Config.Bind("Inputs", "LSDeadzone", 0.25f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 150, IsAdvanced = true }));
            RSDeadzone = Config.Bind("Inputs", "RSDeadzone", 0.25f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 140, IsAdvanced = true }));
            LTDeadzone = Config.Bind("Inputs", "LTDeadzone", 0.25f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 130, IsAdvanced = true }));
            RTDeadzone = Config.Bind("Inputs", "RTDeadzone", 0.25f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 120, IsAdvanced = true }));
            DoubleClickDelay = Config.Bind("Inputs", "DoubleClickDelay", 0.25f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 110, IsAdvanced = true }));
            HoldDelay = Config.Bind("Inputs", "HoldDelay", 0.25f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 100, IsAdvanced = true }));

            HoldAim = Config.Bind("Aim", "HoldAim", false, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 150 }));
            Sensitivity = Config.Bind("Aim", "Sensitivity", new Vector2(20f, 12f), new ConfigDescription("EFT Mouse Sensivity affects this value", null, new ConfigurationManagerAttributes { Order = 140 }));
            AimingSensitivity = Config.Bind("Aim", "AimingSensitivity", new Vector2(20f, 12f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 130 }));
            InvertY = Config.Bind("Aim", "InvertY", false, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 120 }));
            AimDeadzone = Config.Bind("Aim", "AimDeadzone", 0.08f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 110 }));

            Magnetism = Config.Bind("Aim Assist", "Magnetism", false, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 180 }));
            Stickiness = Config.Bind("Aim Assist", "Stickiness", 0.3f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 170 }));
            AutoAim = Config.Bind("Aim Assist", "AutoAim", 0.25f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 160 }));
            StickinessSmooth = Config.Bind("Aim Assist", "StickinessSmooth", 10f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 150, IsAdvanced = true }));
            AutoAimSmooth = Config.Bind("Aim Assist", "AutoAimSmooth", 10f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 140, IsAdvanced = true }));
            MagnetismRadius = Config.Bind("Aim Assist", "MagnetismRadius", 0.1f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 130, IsAdvanced = true }));
            StickinessRadius = Config.Bind("Aim Assist", "StickinessRadius", 0.2f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 120, IsAdvanced = true }));
            AutoAimRadius = Config.Bind("Aim Assist", "AutoAimRadius", 0.5f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 110, IsAdvanced = true }));
            Radius = Config.Bind("Aim Assist", "Radius", 5f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 100, IsAdvanced = true }));
            ExcludeTeammates = Config.Bind("Aim Assist", "ExcludeTeammates", true, new ConfigDescription("Don't let aim assist lock onto your own group (e.g. Fika teammates) or dead bodies.", null, new ConfigurationManagerAttributes { Order = 190 }));

            MovementDeadzone = Config.Bind("Movement", "MovementDeadzone", 0.25f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 130 }));
            DeadzoneBuffer = Config.Bind("Movement", "DeadzoneBuffer", 0.5f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 120, IsAdvanced = true }));
            LeanSensitivity = Config.Bind("Movement", "LeanSensitivity", 50f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 100, IsAdvanced = true }));

            DualsenseIcons = Config.Bind("UI", "DualsenseIcons", false, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 150 }));
            ScrollSensitivity = Config.Bind("UI", "ScrollSensitivity", 1f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 140 }));
            InterfaceStick = Config.Bind("UI", "InterfaceStick", ERaidPadUseStick.None, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 130 }));
            InterfaceSkipStick = Config.Bind("UI", "InterfaceSkipStick", ERaidPadUseStick.RS, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 120 }));
            ScrollStick = Config.Bind("UI", "ScrollStick", ERaidPadUseStick.LS, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 110 }));
            WindowStick = Config.Bind("UI", "WindowStick", ERaidPadUseStick.LS, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 100 }));
            InterfaceStickMoveDelay = Config.Bind("UI", "InterfaceStickMoveDelay", 0.15f, new ConfigDescription("Seconds between selection moves when holding the interface navigation stick. Higher = slower / less sensitive.", new AcceptableValueRange<float>(0.05f, 1f), new ConfigurationManagerAttributes { Order = 105 }));

            SelectColor = Config.Bind("UI Selected Box", "SelectColor", new Color(1f, 0.7659f, 0.3518f, 1), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 100 }));

            BlockPosition = Config.Bind("UI Button Blocks", "BlockPosition", new Vector2(-30f, 57f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 150 }));
            BlockSize = Config.Bind("UI Button Blocks", "BlockSize", new Vector2(40f, 40f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 140, IsAdvanced = true }));
            BlockSpacing = Config.Bind("UI Button Blocks", "BlockSpacing", 16, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 130, IsAdvanced = true }));
            BlockIconSpacing = Config.Bind("UI Button Blocks", "BlockIconSpacing", 8, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 120, IsAdvanced = true }));
            PressFontSize = Config.Bind("UI Button Blocks", "PressFontSize", 20, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 110, IsAdvanced = true }));
            HoldDoubleClickFontSize = Config.Bind("UI Button Blocks", "HoldDoubleClickFontSize", 12, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 100, IsAdvanced = true }));

            new RaidPadPlayerPatch().Enable();
            new RaidPadTarkovApplicationPatch().Enable();
            new RaidPadSSAAPatch().Enable();
            new RaidPadInventoryScreenShowPatch().Enable();
            new RaidPadInventoryScreenClosePatch().Enable();
            new RaidPadActionPanelPatch().Enable();
            new BattleStancePanelPatch().Enable();
            // Controller UI
            new TemplatedGridsViewShowPatch().Enable();
            new GeneratedGridsViewShowPatch().Enable();
            //new TradingGridViewShowPatch().Enable();
            //new TradingGridViewTraderShowPatch().Enable();
            //new TradingTableGridViewShowPatch().Enable();
            new GridViewHidePatch().Enable();
            new ContainedGridsViewClosePatch().Enable();
            new ItemSpecificationPanelShowPatch().Enable();
            new ItemSpecificationPanelClosePatch().Enable();
            new EquipmentTabShowPatch().Enable();
            new EquipmentTabHidePatch().Enable();
            new ContainersPanelShowPatch().Enable();
            new ContainersPanelClosePatch().Enable();
            new SearchButtonShowPatch().Enable();
            new SearchButtonClosePatch().Enable();
            new ContextMenuButtonShowPatch().Enable();
            new ContextMenuButtonClosePatch().Enable();
            new ScrollRectNoDragOnEnable().Enable();
            new ScrollRectNoDragOnDisable().Enable();

            new ItemViewOnBeginDrag().Enable();
            new ItemViewOnEndDrag().Enable();
            new ItemViewUpdate().Enable();
            new DraggedItemViewSetInCenter().Enable();
            new TooltipMethodSetPosition().Enable();
            new SimpleStashPanelShowPatch().Enable();
            new SplitDialogShowPatch().Enable();
            new SplitDialogHidePatch().Enable();
            new SearchableSlotViewShowPatch().Enable();
            new SearchableSlotViewHidePatch().Enable();

            LeftControl = new InputGetKeyDownLeftControlPatch();
        }
    }
    public class RaidPadPlayerPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(Player).GetMethod("Init", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref Player __instance)
        {
            if (__instance != null && __instance.IsYourPlayer)
            {
                RaidPadPlugin.RaidPadClassComponent.UpdateController(__instance);
            }
        }
    }
    public class RaidPadTarkovApplicationPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(TarkovApplication).GetMethod("Init", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref TarkovApplication __instance, InputTree inputTree)
        {
            RaidPadPlugin.RaidPadClassComponent.inputTree = inputTree;
        }
    }
    public class RaidPadSSAAPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(SSAA).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref SSAA __instance)
        {
            RaidPadPlugin.RaidPadClassComponent.currentSSAA = __instance;
        }
    }
    public class RaidPadInventoryScreenShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(InventoryScreen).GetMethods(BindingFlags.Instance | BindingFlags.Public).First(x => x.Name == "Show");
        }
        [PatchPostfix]
        private static void PatchPostFix(ref InventoryScreen __instance)
        {
            RaidPadPlugin.RaidPadClassComponent.Tabs = Traverse.Create(__instance).Field("_tabDictionary").GetValue<Dictionary<EInventoryTab, Tab>>();
            RaidPadPlugin.RaidPadClassComponent.UpdateInterfaceBinds(true);
            RaidPadPlugin.RaidPadClassComponent.UpdateInterface(__instance);
            AsynControllerUIMoveToClosest();
        }
        private static async void AsynControllerUIMoveToClosest()
        {
            await Task.Delay(200);
            RaidPadPlugin.RaidPadClassComponent.ControllerUIMoveToClosest(false);
        }
    }
    public class RaidPadInventoryScreenClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(InventoryScreen).GetMethod("Close", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref InventoryScreen __instance)
        {
            RaidPadPlugin.RaidPadClassComponent.UpdateInterfaceBinds(false);
        }
    }
    public class RaidPadActionPanelPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(ActionPanel).GetMethod("AvailableInteractionStateChangedHandler", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref ActionPanel __instance, AvailableInteractionState interactionState)
        {
            // 4.1.x removed the obfuscated bool_0 field this used to read. Traverse returned
            // default(bool) for the missing field, so the binds were permanently disabled.
            // Mirror the game's own gate for _interactionButtonsContainer visibility instead.
            bool Enabled = interactionState != null && interactionState.Actions.Count > 0;
            RaidPadPlugin.RaidPadClassComponent.UpdateActionPanelBinds(Enabled);
        }
    }
    public class BattleStancePanelPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BattleStancePanel).GetMethod("Show", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref BattleStancePanel __instance)
        {
            RaidPadPlugin.RaidPadClassComponent.speedSlider = Traverse.Create(__instance).Field("_speedSlider").GetValue<Slider>();
        }
    }
    // Controller UI
    public class TemplatedGridsViewShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(TemplatedGridsView).GetMethods(BindingFlags.Instance | BindingFlags.Public).First(x => x.Name == "Show");
        }
        [PatchPostfix]
        private static void PatchPostFix(ref TemplatedGridsView __instance)
        {
            if (__instance.GetComponentInParent<GridWindow>() != null)
            {
                if (RaidPadPlugin.RaidPadClassComponent.containedGridsViews.Contains(__instance)) return;
                RaidPadPlugin.RaidPadClassComponent.containedGridsViews.Add(__instance);
            }
            else
            {
                foreach (GridView gridView in __instance.GridViews)
                {
                    if (RaidPadPlugin.RaidPadClassComponent.gridViews.Contains(gridView)) continue;
                    RaidPadPlugin.RaidPadClassComponent.gridViews.Add(gridView);
                }
            }
        }
    }
    public class GeneratedGridsViewShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GeneratedGridsView).GetMethods(BindingFlags.Instance | BindingFlags.Public).First(x => x.Name == "Show");
        }
        [PatchPostfix]
        private static void PatchPostFix(ref GeneratedGridsView __instance)
        {
            if (__instance.GetComponentInParent<GridWindow>() != null)
            {
                if (RaidPadPlugin.RaidPadClassComponent.containedGridsViews.Contains(__instance)) return;
                RaidPadPlugin.RaidPadClassComponent.containedGridsViews.Add(__instance);
            }
            else
            {
                foreach (GridView gridView in __instance.GridViews)
                {
                    if (RaidPadPlugin.RaidPadClassComponent.gridViews.Contains(gridView)) continue;
                    RaidPadPlugin.RaidPadClassComponent.gridViews.Add(gridView);
                }
            }
        }
    }
    public class TradingGridViewShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(TradingGridView).GetMethods().First((MethodInfo x) => x.Name == "Show" && x.GetParameters().Count() == 5);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref TradingGridView __instance)
        {
            if (RaidPadPlugin.RaidPadClassComponent.gridViews.Contains(__instance)) return;
            RaidPadPlugin.RaidPadClassComponent.gridViews.Add(__instance);
        }
    }
    public class TradingGridViewTraderShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(TradingGridView).GetMethods().First((MethodInfo x) => x.Name == "Show" && x.GetParameters().Count() == 6 && x.GetParameters()[5].Name == "raiseEvents");
        }
        [PatchPostfix]
        private static void PatchPostFix(ref TradingGridView __instance)
        {
            if (RaidPadPlugin.RaidPadClassComponent.gridViews.Contains(__instance)) return;
            RaidPadPlugin.RaidPadClassComponent.gridViews.Add(__instance);
        }
    }
    public class TradingTableGridViewShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(TradingTableGridView).GetMethods().First((MethodInfo x) => x.Name == "Show" && x.GetParameters().Count() == 4);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref TradingTableGridView __instance)
        {
            RaidPadPlugin.RaidPadClassComponent.tradingTableGridView = __instance;
        }
    }
    public class ContainedGridsViewClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // Neither ContainedGridsView nor GeneratedGridsView overrides Close, so this
            // always resolved to UIElement.Close. Target it directly and filter by type
            // below rather than reflecting an unimplemented override off a derived type.
            return typeof(UIElement).GetMethod("Close", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref UIElement __instance)
        {
            ContainedGridsView containedGridsView = __instance as ContainedGridsView;
            if (containedGridsView == null) return;

            if (containedGridsView.GetComponentInParent<GridWindow>() != null)
            {
                RaidPadPlugin.RaidPadClassComponent.containedGridsViews.Remove(containedGridsView);
            }
        }
    }
    public class GridViewHidePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GridView).GetMethod("Close", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref GridView __instance)
        {
            if (__instance == null) return;
            if (RaidPadPlugin.RaidPadClassComponent.tradingTableGridView == __instance)
            {
                RaidPadPlugin.RaidPadClassComponent.tradingTableGridView = null;
                return;
            }
            if (!RaidPadPlugin.RaidPadClassComponent.gridViews.Contains(__instance)) return;
            RaidPadPlugin.RaidPadClassComponent.gridViews.Remove(__instance);
        }
    }
    public class EquipmentTabShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(EquipmentTab).GetMethod("Show", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref EquipmentTab __instance, InventoryController inventoryController)
        {
            if (__instance.gameObject.name == "Gear Panel")
            {
                foreach (KeyValuePair<EquipmentSlot, SlotView> slotView in Traverse.Create(__instance).Field("_slotViews").GetValue<Dictionary<EquipmentSlot, SlotView>>())
                {
                    if (slotView.Key == EquipmentSlot.FirstPrimaryWeapon || slotView.Key == EquipmentSlot.SecondPrimaryWeapon)
                    {
                        RaidPadPlugin.RaidPadClassComponent.weaponsSlotViews.Add(slotView.Value);
                    }
                    else if (slotView.Key == EquipmentSlot.ArmBand)
                    {
                        RaidPadPlugin.RaidPadClassComponent.armbandSlotView = slotView.Value;
                    }
                    else
                    {
                        RaidPadPlugin.RaidPadClassComponent.equipmentSlotViews.Add(slotView.Value);
                    }
                }
            }
            else
            {
                foreach (KeyValuePair<EquipmentSlot, SlotView> slotView in Traverse.Create(__instance).Field("_slotViews").GetValue<Dictionary<EquipmentSlot, SlotView>>())
                {
                    if (slotView.Key == EquipmentSlot.FirstPrimaryWeapon || slotView.Key == EquipmentSlot.SecondPrimaryWeapon)
                    {
                        RaidPadPlugin.RaidPadClassComponent.lootWeaponsSlotViews.Add(slotView.Value);
                    }
                    else if (slotView.Key == EquipmentSlot.ArmBand)
                    {
                        if (slotView.Value.Slot != null) RaidPadPlugin.RaidPadClassComponent.lootArmbandSlotView = slotView.Value;
                    }
                    else
                    {
                        RaidPadPlugin.RaidPadClassComponent.lootEquipmentSlotViews.Add(slotView.Value);
                    }
                }
            }
        }
    }
    public class EquipmentTabHidePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(EquipmentTab).GetMethod("Hide", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref EquipmentTab __instance)
        {
            if (__instance.gameObject.name == "Gear Panel")
            {
                RaidPadPlugin.RaidPadClassComponent.equipmentSlotViews.Clear();
                RaidPadPlugin.RaidPadClassComponent.weaponsSlotViews.Clear();
                RaidPadPlugin.RaidPadClassComponent.armbandSlotView = null;
            }
            else
            {
                RaidPadPlugin.RaidPadClassComponent.lootEquipmentSlotViews.Clear();
                RaidPadPlugin.RaidPadClassComponent.lootWeaponsSlotViews.Clear();
                RaidPadPlugin.RaidPadClassComponent.lootArmbandSlotView = null;
            }
        }
    }
    public class ContainersPanelShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(ContainersPanel).GetMethod("Show", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref ContainersPanel __instance)
        {
            Dictionary<EquipmentSlot, SlotView> slotViews = Traverse.Create(__instance).Field("_slotViews").GetValue<Dictionary<EquipmentSlot, SlotView>>();
            if (slotViews == null) return;

            Transform parent = __instance.transform.parent;
            if (parent != null && parent.gameObject.name == "Scrollview Parent")
            {
                foreach (KeyValuePair<EquipmentSlot, SlotView> slotView in slotViews)
                {
                    if (slotView.Key != EquipmentSlot.Pockets)
                    {
                        RaidPadPlugin.RaidPadClassComponent.containersSlotViews.Add(slotView.Value);
                    }
                }
            }
            else
            {
                foreach (KeyValuePair<EquipmentSlot, SlotView> slotView in slotViews)
                {
                    if (slotView.Key != EquipmentSlot.Pockets) RaidPadPlugin.RaidPadClassComponent.lootContainersSlotViews.Add(slotView.Value);
                }
                SlotView dogtagSlotView = Traverse.Create(__instance).Field("_dogtagSlotView").GetValue<SlotView>();
                if (dogtagSlotView != null && dogtagSlotView.gameObject.activeSelf)
                {
                    RaidPadPlugin.RaidPadClassComponent.dogtagSlotView = dogtagSlotView;
                }
            }
        }
    }
    public class ContainersPanelClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(ContainersPanel).GetMethod("Close", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref ContainersPanel __instance)
        {
            if (__instance.transform.parent.gameObject.name == "Scrollview Parent")
            {
                RaidPadPlugin.RaidPadClassComponent.containersSlotViews.Clear();
                RaidPadPlugin.RaidPadClassComponent.specialSlotSlotViews.Clear();
            }
            else
            {
                RaidPadPlugin.RaidPadClassComponent.lootContainersSlotViews.Clear();
            }
        }
    }

    public class ItemSpecificationPanelShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(ItemSpecificationPanel).GetMethod("Show", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref ItemSpecificationPanel __instance)
        {
            if (RaidPadPlugin.RaidPadClassComponent.itemSpecificationPanels.Contains(__instance)) return;
            RaidPadPlugin.RaidPadClassComponent.itemSpecificationPanels.Add(__instance);
        }
    }
    public class ItemSpecificationPanelClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(ItemSpecificationPanel).GetMethod("Close", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref ItemSpecificationPanel __instance)
        {
            if (__instance == null) return;
            RaidPadPlugin.RaidPadClassComponent.itemSpecificationPanels.Remove(__instance);
        }
    }
    public class SearchButtonShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(SearchButton).GetMethod("SetEnabled", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref SearchButton __instance, bool value)
        {
            AddAsync(__instance);
        }
        private async static void AddAsync(SearchButton instance)
        {
            await Task.Delay(100);
            if (instance.gameObject.activeSelf)
            {
                if (RaidPadPlugin.RaidPadClassComponent.searchButtons.Contains(instance)) return;
                RaidPadPlugin.RaidPadClassComponent.searchButtons.Add(instance);
            }
            else
            {
                if (!RaidPadPlugin.RaidPadClassComponent.searchButtons.Contains(instance)) return;
                RaidPadPlugin.RaidPadClassComponent.searchButtons.Remove(instance);
            }
        }
    }
    public class SearchButtonClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(SearchButton).GetMethod("Close", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref SearchButton __instance)
        {
            if (!RaidPadPlugin.RaidPadClassComponent.searchButtons.Contains(__instance)) return;
            RaidPadPlugin.RaidPadClassComponent.searchButtons.Remove(__instance);
        }
    }
    public class ItemViewOnBeginDrag : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(ItemView).GetMethod("OnBeginDrag", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPrefix]
        private static void PatchPreFix(ref ItemView __instance, PointerEventData eventData)
        {
            if (RaidPadPlugin.RaidPadClassComponent.Dragging && RaidPadPlugin.RaidPadClassComponent.InRaid)
            {
                RaidPadPlugin.RaidPadClassComponent.ControllerCancelDrag();
            }
        }
    }
    public class ItemViewOnEndDrag : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(ItemView).GetMethod("OnEndDrag", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref ItemView __instance, PointerEventData eventData)
        {
            if (RaidPadPlugin.RaidPadClassComponent.Dragging && RaidPadPlugin.RaidPadClassComponent.InRaid)
            {
                RaidPadPlugin.RaidPadClassComponent.ControllerCancelDrag();
            }
        }
    }
    public class ItemViewUpdate : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(ItemView).GetMethod("Update", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPrefix]
        private static bool PatchPreFix(ref ItemView __instance)
        {
            if (!RaidPadPlugin.RaidPadClassComponent.InRaid) return true;
            return (!RaidPadPlugin.RaidPadClassComponent.Dragging);
        }
    }
    public class DraggedItemViewSetInCenter : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(DraggedItemView).GetMethod("SetInCenter", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPrefix]
        private static bool PatchPreFix(ref DraggedItemView __instance)
        {
            if (RaidPadPlugin.RaidPadClassComponent.Dragging && RaidPadPlugin.RaidPadClassComponent.InRaid)
            {
                RectTransform rectTransform = Traverse.Create(__instance).Property("RectTransform").GetValue<RectTransform>();
                rectTransform.position = RaidPadPlugin.RaidPadClassComponent.globalPosition;
                return false;
            }
            else
            {
                return true;
            }
        }
    }
    public class TooltipMethodSetPosition : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(Tooltip).GetMethod("SetPosition", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPrefix]
        private static void PatchPreFix(ref ItemView __instance, ref Vector2 position)
        {
            if (RaidPadPlugin.RaidPadClassComponent.connected && RaidPadPlugin.RaidPadClassComponent.InRaid) position = RaidPadPlugin.RaidPadClassComponent.globalPosition + new Vector2(32f,-19f);
        }
    }
    public class ScrollRectNoDragOnEnable : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(ScrollRectNoDrag).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref ScrollRectNoDrag __instance)
        {
            if (!RaidPadPlugin.RaidPadClassComponent.scrollRectNoDrags.Contains(__instance))
            {
                if (RaidPadPlugin.RaidPadClassComponent.scrollRectNoDrags.Count == 0)
                {
                    RectTransform rectTransform;
                    rectTransform = __instance.GetComponent<RectTransform>();
                    RaidPadPlugin.RaidPadClassComponent.currentScrollRectNoDrag = __instance;
                    RaidPadPlugin.RaidPadClassComponent.currentScrollRectNoDragRectTransform = rectTransform;
                }
                RaidPadPlugin.RaidPadClassComponent.scrollRectNoDrags.Add(__instance);
            }
        }
    }
    public class ScrollRectNoDragOnDisable : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // ScrollRectNoDrag overrides OnEnable but not OnDisable, so this always
            // resolved to ScrollRect.OnDisable. Target it directly and filter by type below.
            return typeof(ScrollRect).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref ScrollRect __instance)
        {
            ScrollRectNoDrag scrollRectNoDrag = __instance as ScrollRectNoDrag;
            if (scrollRectNoDrag == null) return;
            RaidPadPlugin.RaidPadClassComponent.scrollRectNoDrags.Remove(scrollRectNoDrag);
        }
    }
    public class SimpleStashPanelShowPatch : ModulePatch
    {
        public static bool Searching = false;
        protected override MethodBase GetTargetMethod()
        {
            return typeof(SimpleStashPanel).GetMethod("Show", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref SimpleStashPanel __instance)
        {
            //if (!Searching && RaidPadPlugin.RaidPadClassComponent.InRaid) ShowAsync(__instance);
        }
        private async static void ShowAsync(SimpleStashPanel instance)
        {
            Searching = true;
            await Task.Delay(100);
            SearchableItemView searchableItemView = Traverse.Create(instance).Field("_simplePanel").GetValue<SearchableItemView>();
            if (searchableItemView != null)
            {
                // 4.1.x renamed the obfuscated containedGridsView_0 to _containedGridsView, and it is
                // typed as the ContainedGridsView base (TemplatedGridsView for rig layouts).
                ContainedGridsView containedGridsView = Traverse.Create(searchableItemView).Field("_containedGridsView").GetValue<ContainedGridsView>();
                if (containedGridsView != null)
                {
                    if (containedGridsView.GridViews.Count() == 0)
                    {
                        ShowAsync(instance);
                        return;
                    }
                    GridView gridView = containedGridsView.GridViews[0];
                    if (gridView != null)
                    {
                        Searching = false;
                        RaidPadPlugin.RaidPadClassComponent.SimpleStashGridView = gridView;
                        RaidPadPlugin.RaidPadClassComponent.ControllerUISelect(gridView);
                    }
                }
            }
            Searching = false;
        }
    }
    public class ContextMenuButtonShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(ContextMenuButton).GetMethod("Show", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref ContextMenuButton __instance)
        {
            if (RaidPadPlugin.RaidPadClassComponent.contextMenuButtons.Contains(__instance)) return;
            // 4.1.x routes button creation through InteractionButtonsContainer and parents them
            // under its _buttonsContainer, so SimpleContextMenu is no longer exactly two levels
            // up. Walk the full parent chain instead of assuming a fixed depth.
            if (__instance.GetComponentInParent<SimpleContextMenu>() == null) return;
            RaidPadPlugin.RaidPadClassComponent.contextMenuButtons.Add(__instance);
            if (!RaidPadPlugin.RaidPadClassComponent.ContextMenu)
            {
                RaidPadPlugin.RaidPadClassComponent.UpdateContextMenuBinds(true);
                if (RaidPadPlugin.RaidPadClassComponent.InRaid) RaidPadPlugin.RaidPadClassComponent.ControllerUISelect(__instance);
            }
        }
    }
    public class ContextMenuButtonClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // ContextMenuButton does not override Close, so this always resolved to
            // SimpleContextMenuButton.Close. Target it directly and filter by type below.
            return typeof(SimpleContextMenuButton).GetMethod("Close", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref SimpleContextMenuButton __instance)
        {
            ContextMenuButton contextMenuButton = __instance as ContextMenuButton;
            if (contextMenuButton != null)
            {
                RaidPadPlugin.RaidPadClassComponent.contextMenuButtons.Remove(contextMenuButton);
            }
            if (RaidPadPlugin.RaidPadClassComponent.contextMenuButtons.Count == 0)
            {
                RaidPadPlugin.RaidPadClassComponent.UpdateContextMenuBinds(false);
            }
        }
    }
    public class SplitDialogShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(SplitDialog).GetMethods().First(x => x.Name == "Show" && x.GetParameters().Count() > 7);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref SplitDialog __instance)
        {
            RaidPadPlugin.RaidPadClassComponent.splitDialog = __instance;
            RaidPadPlugin.RaidPadClassComponent.UpdateSplitDialogBinds(true);
        }
    }
    public class SplitDialogHidePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(SplitDialog).GetMethod("Hide", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref SplitDialog __instance)
        {
            RaidPadPlugin.RaidPadClassComponent.splitDialog = null;
            RaidPadPlugin.RaidPadClassComponent.UpdateSplitDialogBinds(false);
        }
    }
    public class SearchableSlotViewShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(SearchableSlotView).GetMethod("Show", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref SearchableSlotView __instance)
        {
            if (__instance.Slot != null && __instance.Slot.IsSpecial)
            {
                RaidPadPlugin.RaidPadClassComponent.specialSlotSlotViews.Add(__instance);
            }
        }
    }
    public class SearchableSlotViewHidePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // SearchableSlotView overrides Show but not Close, so this always resolved to
            // SlotView.Close. Target it directly and filter by type below.
            return typeof(SlotView).GetMethod("Close", BindingFlags.Instance | BindingFlags.Public);
        }
        [PatchPostfix]
        private static void PatchPostFix(ref SlotView __instance)
        {
            SearchableSlotView searchableSlotView = __instance as SearchableSlotView;
            if (searchableSlotView == null) return;
            if (searchableSlotView.Slot != null && searchableSlotView.Slot.IsSpecial)
            {
                RaidPadPlugin.RaidPadClassComponent.specialSlotSlotViews.Remove(searchableSlotView);
            }

        }
    }
    public class InputGetKeyDownLeftControlPatch : ModulePatch
    {
        MethodInfo methodInfo;
        public InputGetKeyDownLeftControlPatch()
        {
            methodInfo = typeof(Input).GetMethods().First(x => x.Name == "GetKey" && x.GetParamsNames().Contains("key"));
        }
        protected override MethodBase GetTargetMethod()
        {
            return methodInfo;
        }
        [PatchPrefix]
        private static bool PatchPreFix(ref bool __result, KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftControl:
                    __result = true;
                    return false;
            }
            return true;
        }
    }
}
