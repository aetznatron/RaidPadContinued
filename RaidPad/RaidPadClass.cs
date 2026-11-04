using UnityEngine;
using System.Collections.Generic;
using EFT.UI;
using EFT;
using SharpDX.XInput;
using HarmonyLib;
using UnityEngine.UI;
using System;
using System.Reflection;
using EFT.InputSystem;
using System.Threading.Tasks;
using EFT.UI.DragAndDrop;
using UnityEngine.EventSystems;
using Comfort.Common;
using EFT.Communications;
using EFT.InventoryLogic;
using Diz.Binding;
using SPT.Common.Utils;
using System.IO;
using Newtonsoft.Json;
using UnityEngine.Networking;
using TMPro;
using EFT.Interactive;
using System.Net;
using UnityEngine.Rendering;

namespace RaidPad
{
    public class RaidPadClass : MonoBehaviour
    {
        // RaidPad
        public Player Player;
        public InputTree inputTree;
        public Player.FirearmController firearmController;
        public bool InRaid = false;

        // SharpDX
        public Controller controller;
        public Gamepad gamepad;
        public bool connected = false;
        private int controllerRescanCounter = 0;

        // Native PS pad fallback, used when no XInput slot is occupied.
        private RaidPadHidController hid;
        private bool usingHid = false;

        // Out-of-raid mouse driver.
        private RaidPadMenuCursor menuCursor = new RaidPadMenuCursor();
        private bool menuCursorWasActive = false;
        public float maxValue = short.MaxValue;

        // Inputs
        public Vector2 LS, RS = Vector2.zero;
        public float LSXYSqrt, RSXYSqrt;

        public float LeftTrigger, RightTrigger;

        public bool LSINPUT = false;
        public bool LSUP = false;
        public bool LSDOWN = false;
        public bool LSLEFT = false;
        public bool LSRIGHT = false;

        public bool RSINPUT = false;
        public bool RSUP = false;
        public bool RSDOWN = false;
        public bool RSLEFT = false;
        public bool RSRIGHT = false;

        public bool A = false;
        public bool B = false;
        public bool X = false;
        public bool Y = false;

        public bool LB = false;
        public bool RB = false;

        public bool RT = false;
        public bool LT = false;

        public bool R = false;
        public bool L = false;

        public bool UP = false;
        public bool DOWN = false;
        public bool LEFT = false;
        public bool RIGHT = false;

        public bool BACK = false;
        public bool MENU = false;

        // Custom Inputs
        public bool SlowLeanLeft;
        public bool SlowLeanRight;

        // Input Sets
        public bool isAiming = false;

        public bool LB_RB = false;
        public bool Interface_LB_RB = false;

        public bool Interface = false;
        public bool ContextMenu = false;

        // EFT Methods
        private MethodInfo TranslateInput;
        private object[] TranslateInputInvokeParameters = new object[3] { new List<ECommand>(), null, ECursorResult.Ignore };
        private MethodInfo ButtonPress;

        // Movement
        private object MovementContextObject;
        private Type MovementContextType;
        private MethodInfo SetCharacterMovementSpeed;
        private object[] MovementInvokeParameters = new object[2] { 0.0, false };

        private bool resetCharacterMovementSpeed = false;
        private float CharacterMovementSpeed = 0f;
        private float StateSpeedLimit = 0f;
        private float MaxSpeed = 0f;

        public Slider speedSlider;

        // Aim
        public Vector2 Aim = Vector2.zero;
        private Vector2 InvertY = new Vector2(100f, -100f);
        private AnimationCurve AimAnimationCurve = new AnimationCurve();
        private Keyframe[] AimKeys = new Keyframe[3] { new Keyframe(0f, 0f), new Keyframe(0.75f, 0.5f, 0.75f, 0.5f), new Keyframe(1f, 1f), };

        // Aim Assist
        private Collider[] colliders;
        public int colliderCount;
        public LayerMask AimAssistLayerMask;

        public Dictionary<LocalPlayer, float> AimAssistPlayers = new Dictionary<LocalPlayer, float>();
        private RaycastHit hit;
        private LayerMask HighLayerMask;

        private Vector2 ScreenSize = new Vector2(Screen.width, Screen.height);
        private Vector2 ScreenSizeRatioMultiplier = new Vector2(1f, Screen.height / Screen.width);

        private Vector3 AimPosition;
        private Vector3 AimDirection;

        private bool Magnetism;
        private float Stickiness;
        private float StickinessSmooth;
        private Vector2 AutoAim = Vector2.zero;
        private Vector2 AutoAimSmooth = Vector2.zero;

        private float AimAssistAngle;
        private float AimAssistBoneAngle;

        private LocalPlayer AimAssistLocalPlayer = null;
        private LocalPlayer HitAimAssistLocalPlayer = null;
        private Vector2 AimAssistTarget2DPoint = Vector2.zero;
        private Vector2 AimAssistScreenLocalPosition = Vector2.zero;

        public SSAA currentSSAA;
        public float SSAARatio;

        // Binds
        public ControllerPresetJsonClass controllerPresetJsonClass;

        Dictionary<string, Dictionary<ERaidPadButton, List<RaidPadButtonBind>>> RaidPadSets = new Dictionary<string, Dictionary<ERaidPadButton, List<RaidPadButtonBind>>>();
        List<string> ActiveRaidPadSets = new List<string>();

        Dictionary<ERaidPadButton, RaidPadButtonSnapshot> RaidPadButtonSnapshots = new Dictionary<ERaidPadButton, RaidPadButtonSnapshot>();
        Dictionary<ERaidPadButton, List<RaidPadButtonBind>> RaidPadButtonBinds = new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>();

        List<string> AsyncPress = new List<string>();
        List<string> AsyncHold = new List<string>();

        RaidPadButtonBind EmptyBind = new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.None), ERaidPadPressType.Press, -100);

        // UI
        public InventoryScreen inventoryScreen;

        public List<GridView> gridViews = new List<GridView>();
        public GridView SimpleStashGridView;
        public TradingTableGridView tradingTableGridView;
        public List<ContainedGridsView> containedGridsViews = new List<ContainedGridsView>();
        public List<ItemSpecificationPanel> itemSpecificationPanels = new List<ItemSpecificationPanel>();

        public List<SlotView> equipmentSlotViews = new List<SlotView>();
        public List<SlotView> weaponsSlotViews = new List<SlotView>();
        public SlotView armbandSlotView;
        public List<SlotView> containersSlotViews = new List<SlotView>();
        public List<SlotView> lootEquipmentSlotViews = new List<SlotView>();
        public List<SlotView> lootWeaponsSlotViews = new List<SlotView>();
        public SlotView lootArmbandSlotView;
        public List<SlotView> lootContainersSlotViews = new List<SlotView>();
        public SlotView dogtagSlotView;
        public List<SlotView> specialSlotSlotViews = new List<SlotView>();

        public List<SearchButton> searchButtons = new List<SearchButton>();
        public Image SearchButtonImage;

        public List<ContextMenuButton> contextMenuButtons = new List<ContextMenuButton>();

        public SplitDialog splitDialog;

        public List<ScrollRectNoDrag> scrollRectNoDrags = new List<ScrollRectNoDrag>();

        public GridView currentGridView;
        public ModSlotView currentModSlotView;
        public TradingTableGridView currentTradingTableGridView;
        public ContainedGridsView currentContainedGridsView;
        public ItemSpecificationPanel currentItemSpecificationPanel;
        public SlotView currentEquipmentSlotView;
        public SlotView currentWeaponsSlotView;
        public SlotView currentArmbandSlotView;
        public SlotView currentContainersSlotView;
        public SlotView currentDogtagSlotView;
        public SlotView currentSpecialSlotSlotView;
        public SearchButton currentSearchButton;
        public ContextMenuButton currentContextMenuButton;
        public ScrollRectNoDrag currentScrollRectNoDrag;
        public RectTransform currentScrollRectNoDragRectTransform;

        private GridView snapshotGridView;
        private ModSlotView snapshotModSlotView;
        private TradingTableGridView snapshotTradingTableGridView;
        private ContainedGridsView snapshotContainedGridsView;
        private ItemSpecificationPanel snapshotItemSpecificationPanel;
        private SlotView snapshotEquipmentSlotView;
        private SlotView snapshotWeaponsSlotView;
        private SlotView snapshotArmbandSlotView;
        private SlotView snapshotContainersSlotView;
        private SlotView snapshotDogtagSlotView;
        private SlotView snapshotSpecialSlotSlotView;
        private SearchButton snapshotSearchButton;

        public Vector2 globalPosition = Vector2.zero;
        private Vector2 globalSize = Vector2.zero;

        private Vector2Int gridViewLocation = Vector2Int.one;
        private Vector2Int SnapshotGridViewLocation = Vector2Int.one;

        public float ScreenRatio = 1f;
        public float GridSize = 63f;
        public float ModSize = 63f;
        public float SlotSize = 124f;

        public bool LSButtons = false;
        public bool RSButtons = false;

        public Dictionary<EInventoryTab, Tab> Tabs = new Dictionary<EInventoryTab, Tab>();

        // UI AutoMove
        private bool AutoMove = false;
        private bool SplitDialogAutoMove = false;

        private float AutoMoveTime = 0f;
        private float AutoMoveTimeDelay = 0.2f;

        private float InterfaceStickMoveTime = 0f;
        private float InterfaceStickMoveTimeDelay = 0.3f;

        private float InterfaceSkipStickMoveTime = 0f;
        private float InterfaceSkipStickMoveTimeDelay = 0.3f;

        public Vector2Int lastDirection = Vector2Int.zero;
        public int lastIntSliderValue;

        // UI Pointer
        private PointerEventData pointerEventData = null;
        private EventSystem eventSystem = null;
        private ItemView onPointerEnterItemView;

        // UI Drag
        public bool Dragging = false;
        private ItemView DraggingItemView = null;

        // UI Methods
        private MethodInfo ExecuteInteraction;
        private object[] ExecuteInteractionInvokeParameters = new object[1] { EItemInfoButton.Inspect };

        private MethodInfo IsInteractionAvailable;
        private object[] IsInteractionAvailableInvokeParameters = new object[1] { EItemInfoButton.Inspect };

        private MethodInfo ExecuteMiddleClick;
        private MethodInfo QuickFindAppropriatePlace;
        private MethodInfo CanExecute;
        private MethodInfo RunNetworkTransaction;
        private MethodInfo CalculateRotatedSize;
        private MethodInfo DraggedItemViewMethodRotateItem;

        private MethodInfo ItemUIContextBindItem;
        private object[] ItemUIContextInvokeParameters = new object[2] { typeof(Item), EBoundItem.Item4 };

        private MethodInfo ShowContextMenu;
        private object[] ShowContextMenuInvokeParameters = new object[1] { Vector2.zero };

        public bool QuickSkipStick = false;

        // UI Selected Box
        public GameObject SelectedGameObject;
        public RectTransform SelectedRectTransform;
        public Image SelectedImage;
        public LayoutElement SelectedLayoutElement;

        // UI Button Blocks
        public delegate void RaidPadButtonState(ERaidPadButton Button, bool Pressed);
        public static RaidPadButtonState onRaidPadButtonState;
        public GameObject AllGameObject;
        public Dictionary<ERaidPadButton, RaidPadButtonBlock> ButtonBlocks = new Dictionary<ERaidPadButton, RaidPadButtonBlock>();

        // Files
        public static Dictionary<string, Sprite> LoadedSprites = new Dictionary<string, Sprite>();
        public static Dictionary<string, AudioClip> LoadedAudioClips = new Dictionary<string, AudioClip>();

        private Texture2D _cursorWhiteTex;
        private Texture2D _cursorBlackTex;

        private void DrawCursorBar(float cx, float cy, float w, float h, Texture2D tex)
        {
            GUI.DrawTexture(new Rect(cx - (w / 2f), cy - (h / 2f), w, h), tex);
        }

        public void OnGUI()
        {
            if (connected && !InRaid && RaidPadPlugin.MenuCursorEnabled.Value && RaidPadPlugin.MenuCursorCrosshair.Value)
            {
                if (_cursorWhiteTex == null)
                {
                    _cursorWhiteTex = new Texture2D(1, 1);
                    _cursorWhiteTex.SetPixel(0, 0, Color.white);
                    _cursorWhiteTex.Apply();
                    _cursorBlackTex = new Texture2D(1, 1);
                    _cursorBlackTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.85f));
                    _cursorBlackTex.Apply();
                }
                Vector3 mouse = Input.mousePosition;
                float cx = mouse.x;
                float cy = Screen.height - mouse.y;
                DrawCursorBar(cx, cy, 24f, 6f, _cursorBlackTex);
                DrawCursorBar(cx, cy, 6f, 24f, _cursorBlackTex);
                DrawCursorBar(cx, cy, 20f, 2f, _cursorWhiteTex);
                DrawCursorBar(cx, cy, 2f, 20f, _cursorWhiteTex);
            }
            // The 0.4.0 debug HUD below this point was already dead (unconditional return).
            return;
            GUILayout.BeginArea(new Rect(20, 10, 1280, 720));

            RaidPadButtonBind[] raidPadControllerButtonBind = GetPriorityButtonBinds(ERaidPadButton.A);

            List<string> Actions = ControllerGetButtonAction(raidPadControllerButtonBind[0]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("A " + Action);
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[1]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("A " + Action);
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[2]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("A " + Action + " (HOLD)");
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[3]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("A " + Action);
            }

            raidPadControllerButtonBind = GetPriorityButtonBinds(ERaidPadButton.X);

            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[0]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("X " + Action);
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[1]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("X " + Action);
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[2]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("X " + Action + " (HOLD)");
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[3]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("X " + Action);
            }

            raidPadControllerButtonBind = GetPriorityButtonBinds(ERaidPadButton.Y);

            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[0]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("Y " + Action);
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[1]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("Y " + Action);
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[2]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("Y " + Action + " (HOLD)");
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[3]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("Y " + Action);
            }

            raidPadControllerButtonBind = GetPriorityButtonBinds(ERaidPadButton.B);

            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[0]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("B " + Action);
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[1]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("B " + Action);
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[2]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("B " + Action + " (HOLD)");
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[3]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("B " + Action);
            }

            raidPadControllerButtonBind = GetPriorityButtonBinds(ERaidPadButton.UP);

            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[0]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("UP " + Action);
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[1]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("UP " + Action);
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[2]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("UP " + Action + " (HOLD)");
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[3]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("UP " + Action);
            }

            raidPadControllerButtonBind = GetPriorityButtonBinds(ERaidPadButton.DOWN);

            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[0]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("DOWN " + Action);
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[1]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("DOWN " + Action);
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[2]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("DOWN " + Action + " (HOLD)");
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[3]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("DOWN " + Action);
            }

            raidPadControllerButtonBind = GetPriorityButtonBinds(ERaidPadButton.LEFT);

            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[0]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("LEFT " + Action);
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[1]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("LEFT " + Action);
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[2]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("LEFT " + Action + " (HOLD)");
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[3]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("LEFT " + Action);
            }

            raidPadControllerButtonBind = GetPriorityButtonBinds(ERaidPadButton.RIGHT);

            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[0]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("RIGHT " + Action);
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[1]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("RIGHT " + Action);
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[2]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("RIGHT " + Action + " (HOLD)");
            }
            Actions = ControllerGetButtonAction(raidPadControllerButtonBind[3]);
            foreach (string Action in Actions)
            {
                if (Action == "") continue;
                GUILayout.Label("RIGHT " + Action);
            }

            GUILayout.EndArea();



            GUIContent gUIContent = new GUIContent();
            if (InRaid && Interface && currentContextMenuButton == null)
            {
                GUI.Box(new Rect(new Vector2(globalPosition.x - (GridSize / 2), (Screen.height) - globalPosition.y - (GridSize / 2)), new Vector2(GridSize, GridSize)), gUIContent);
            }
            if (InRaid && Interface && currentScrollRectNoDrag != null && currentScrollRectNoDragRectTransform != null)
            {
                float height = currentScrollRectNoDrag.content.rect.height;
                Vector2 position = new Vector2(currentScrollRectNoDragRectTransform.position.x + (currentScrollRectNoDragRectTransform.rect.x * ScreenRatio), currentScrollRectNoDragRectTransform.position.y - ((currentScrollRectNoDragRectTransform.rect.height * (currentScrollRectNoDragRectTransform.pivot.y - 1f)) * ScreenRatio));
                /*if (globalPosition.x > position.x && globalPosition.x < (position.x + (currentScrollRectNoDragRectTransform.rect.width * ScreenRatio)))
                {
                    if ((globalPosition.y + (GridSize / 2f)) > position.y)
                    {
                        currentScrollRectNoDrag.verticalNormalizedPosition = currentScrollRectNoDrag.verticalNormalizedPosition + (((1000f / height) / height) * 10000f * Time.deltaTime * RaidPadPlugin.ScrollSensitivity.Value);
                        UpdateGlobalPosition();
                        if (!((globalPosition.y + (GridSize / 2f)) > position.y) && !((globalPosition.y - (GridSize / 2f)) < (position.y - (currentScrollRectNoDragRectTransform.rect.height * ScreenRatio))))
                        {
                            ControllerUIOnMove(Vector2Int.zero, globalPosition);
                        }
                    }
                    else if ((globalPosition.y - (GridSize / 2f)) < (position.y - (currentScrollRectNoDragRectTransform.rect.height * ScreenRatio)))
                    {
                        currentScrollRectNoDrag.verticalNormalizedPosition = currentScrollRectNoDrag.verticalNormalizedPosition + (((-1000f / height) / height) * 10000f * Time.deltaTime * RaidPadPlugin.ScrollSensitivity.Value);
                        UpdateGlobalPosition();
                        if (!((globalPosition.y + (GridSize / 2f)) > position.y) && !((globalPosition.y - (GridSize / 2f)) < (position.y - (currentScrollRectNoDragRectTransform.rect.height * ScreenRatio))))
                        {
                            ControllerUIOnMove(Vector2Int.zero, globalPosition);
                        }
                    }
                }*/
                GUI.Box(new Rect(new Vector2(position.x, (Screen.height) - position.y), new Vector2(currentScrollRectNoDragRectTransform.rect.width * ScreenRatio, currentScrollRectNoDragRectTransform.rect.height * ScreenRatio)), gUIContent);
            }
        }

        private void Awake()
        {
            AimAssistLayerMask = LayerMask.GetMask("Player");
            // TransparentCollider matches EFT's own BallisticsCalculatorConstants.STATIC_HITTABLE_LAYERS:
            // a bullet stops at glass, so aim assist must not see a target through a window either.
            HighLayerMask = LayerMask.GetMask("Terrain", "HighPolyCollider", "TransparentCollider");
        }
        public void Start()
        {
            // 4.1.x de-obfuscated this to BindItem(Item, EBoundItem). method_1 still exists but is
            // now an unrelated 7-parameter message-window helper, so invoking it with 2 args threw.
            ItemUIContextBindItem = typeof(ItemUiContext).GetMethod("BindItem", BindingFlags.Instance | BindingFlags.Public);
            TranslateInput = typeof(InputTree).GetMethod("TranslateInput", BindingFlags.Instance | BindingFlags.Public);
            ButtonPress = typeof(Button).GetMethod("Press", BindingFlags.Instance | BindingFlags.NonPublic);
            ExecuteMiddleClick = typeof(ItemView).GetMethod("ExecuteMiddleClick", BindingFlags.Instance | BindingFlags.Public);
            QuickFindAppropriatePlace = typeof(ItemUiContext).GetMethod("QuickFindAppropriatePlace", BindingFlags.Instance | BindingFlags.Public);
            CanExecute = typeof(ItemController).GetMethod("CanExecute", BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(IOperationResult) }, null);
            RunNetworkTransaction = typeof(ItemController).GetMethod("RunNetworkTransaction", BindingFlags.Instance | BindingFlags.Public);
            ShowContextMenu = typeof(ItemView).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.Public);
            CalculateRotatedSize = typeof(Item).GetMethod("CalculateRotatedSize", BindingFlags.Instance | BindingFlags.Public);
            DraggedItemViewMethodRotateItem = typeof(DraggedItemView).GetMethod("RotateItem", BindingFlags.Instance | BindingFlags.Public);

            onRaidPadButtonState += ControllerButtonStateMethod;

            if (!File.Exists((AppDomain.CurrentDomain.BaseDirectory + "/BepInEx/plugins/RaidPad/assets/Default.json")))
            {
                DefaultJSON();
            }
            if (File.Exists((AppDomain.CurrentDomain.BaseDirectory + "/BepInEx/plugins/RaidPad/assets/Default.json")))
            {
                controllerPresetJsonClass = ReadFromJsonFile<ControllerPresetJsonClass>((AppDomain.CurrentDomain.BaseDirectory + "/BepInEx/plugins/RaidPad/assets/Default.json"));
            }

            AimAnimationCurve.keys = AimKeys;

            ReloadFiles();

            RaidPadPlugin.BlockPosition.SettingChanged += BlockPositionUpdated;
            RaidPadPlugin.UserIndex.SettingChanged += UserIndexUpdated;
        }
        public void Update()
        {
            if (!connected)
            {
                // Re-scan roughly once a second so a pad plugged in mid-session is picked up.
                if ((++controllerRescanCounter & 0x3F) == 0) ResolveController();
                if (!connected) return;
            }

            InRaid = Player != null;

            try
            {
                if (usingHid)
                {
                    if (!hid.Alive)
                    {
                        connected = false;
                        return;
                    }
                    gamepad = hid.GetGamepad();
                }
                else
                {
                    gamepad = controller.GetState().Gamepad;
                }
            }
            catch
            {
                connected = false;
                return;
            }

            // Out of raid the menus are plain mouse UI, so drive the real cursor instead of
            // synthesising EFT input commands.
            if (!InRaid)
            {
                if (RaidPadPlugin.MenuCursorEnabled.Value)
                {
                    menuCursor.OnRotate = ControllerRotateMenuDragged;
                    menuCursor.Update(gamepad, Time.unscaledDeltaTime);
                    menuCursorWasActive = true;
                }
                return;
            }

            if (menuCursorWasActive)
            {
                menuCursor.Release();
                menuCursorWasActive = false;
            }

            // Dynamic Maps peek overlay: keep the MapOverlay set in sync with the mod's own state
            if (RaidPadDynamicMapsBridge.IsPeeking())
            {
                if (!ActiveRaidPadSets.Contains("MapOverlay"))
                {
                    EnableSet("MapOverlay");
                }
            }
            else if (ActiveRaidPadSets.Contains("MapOverlay"))
            {
                DisableSet("MapOverlay");
            }

            if (LeftTrigger > RaidPadPlugin.LTDeadzone.Value)
            {
                if (!LT)
                {
                    LT = true;
                    GeneratePressType(ERaidPadButton.LT, true);
                }
            }
            else
            {
                if (LT)
                {
                    LT = false;
                    GeneratePressType(ERaidPadButton.LT, false);
                    if (RaidPadPlugin.HoldAim.Value)
                    {
                        RaidPadButton(new RaidPadButtonBind(new RaidPadCommand(ECommand.EndAlternativeShooting), ERaidPadPressType.Release, 3));
                    }
                }
            }
            if (RightTrigger > RaidPadPlugin.RTDeadzone.Value)
            {
                if (!RT)
                {
                    RT = true;
                    GeneratePressType(ERaidPadButton.RT, true);
                }
            }
            else
            {
                if (RT)
                {
                    RT = false;
                    GeneratePressType(ERaidPadButton.RT, false);
                }
            }
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.A))
            {
                if (!A)
                {
                    A = true;
                    GeneratePressType(ERaidPadButton.A, true);

                }
            }
            else
            {
                if (A)
                {
                    A = false;
                    GeneratePressType(ERaidPadButton.A, false);
                }
            }
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.B))
            {
                if (!B)
                {
                    B = true;
                    GeneratePressType(ERaidPadButton.B, true);
                }
            }
            else
            {
                if (B)
                {
                    B = false;
                    GeneratePressType(ERaidPadButton.B, false);
                }
            }
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.X))
            {
                if (!X)
                {
                    X = true;
                    GeneratePressType(ERaidPadButton.X, true);
                }
            }
            else
            {
                if (X)
                {
                    X = false;
                    GeneratePressType(ERaidPadButton.X, false);
                }
            }
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Y))
            {
                if (!Y)
                {
                    Y = true;
                    GeneratePressType(ERaidPadButton.Y, true);
                }
            }
            else
            {
                if (Y)
                {
                    Y = false;
                    GeneratePressType(ERaidPadButton.Y, false);
                }
            }
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder))
            {
                if (!LB)
                {
                    LB = true;
                    GeneratePressType(ERaidPadButton.LB, true);
                    EnableSet("LB");
                }
            }
            else
            {
                if (LB)
                {
                    LB = false;
                    GeneratePressType(ERaidPadButton.LB, false);
                    DisableSet("LB");
                }
            }
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder))
            {
                if (!RB)
                {
                    RB = true;
                    GeneratePressType(ERaidPadButton.RB, true);
                    EnableSet("RB");
                }
            }
            else
            {
                if (RB)
                {
                    RB = false;
                    GeneratePressType(ERaidPadButton.RB, false);
                    DisableSet("RB");
                }
            }
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder | GamepadButtonFlags.RightShoulder))
            {
                if (!LB_RB)
                {
                    LB_RB = true;
                    EnableSet("LB_RB");
                }
            }
            else
            {
                if (LB_RB)
                {
                    LB_RB = false;
                    DisableSet("LB_RB");
                }
            }
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder | GamepadButtonFlags.RightShoulder) && Interface)
            {
                if (!Interface_LB_RB)
                {
                    Interface_LB_RB = true;
                    EnableSet("Interface_LB_RB");
                }
            }
            else
            {
                if (Interface_LB_RB)
                {
                    Interface_LB_RB = false;
                    DisableSet("Interface_LB_RB");
                }
            }
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb))
            {
                if (!L)
                {
                    L = true;
                    GeneratePressType(ERaidPadButton.LS, true);
                }
            }
            else
            {
                if (L)
                {
                    L = false;
                    GeneratePressType(ERaidPadButton.LS, false);
                }
            }
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb))
            {
                if (!R)
                {
                    R = true;
                    GeneratePressType(ERaidPadButton.RS, true);
                }
            }
            else
            {
                if (R)
                {
                    R = false;
                    GeneratePressType(ERaidPadButton.RS, false);
                }
            }
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp))
            {
                if (!UP)
                {
                    UP = true;
                    GeneratePressType(ERaidPadButton.UP, true);
                }
            }
            else
            {
                if (UP)
                {
                    UP = false;
                    GeneratePressType(ERaidPadButton.UP, false);
                }
            }
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown))
            {
                if (!DOWN)
                {
                    DOWN = true;
                    GeneratePressType(ERaidPadButton.DOWN, true);
                }
            }
            else
            {
                if (DOWN)
                {
                    DOWN = false;
                    GeneratePressType(ERaidPadButton.DOWN, false);
                }
            }
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft))
            {
                if (!LEFT)
                {
                    LEFT = true;
                    GeneratePressType(ERaidPadButton.LEFT, true);
                }
            }
            else
            {
                if (LEFT)
                {
                    LEFT = false;
                    GeneratePressType(ERaidPadButton.LEFT, false);
                }
            }
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight))
            {
                if (!RIGHT)
                {
                    RIGHT = true;
                    GeneratePressType(ERaidPadButton.RIGHT, true);
                }
            }
            else
            {
                if (RIGHT)
                {
                    RIGHT = false;
                    GeneratePressType(ERaidPadButton.RIGHT, false);
                }
            }
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Back))
            {
                if (!BACK)
                {
                    BACK = true;
                    GeneratePressType(ERaidPadButton.BACK, true);
                }
            }
            else
            {
                if (BACK)
                {
                    BACK = false;
                    GeneratePressType(ERaidPadButton.BACK, false);
                }
            }
            if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Start))
            {
                if (!MENU)
                {
                    MENU = true;
                    GeneratePressType(ERaidPadButton.MENU, true);
                }
            }
            else
            {
                if (MENU)
                {
                    MENU = false;
                    GeneratePressType(ERaidPadButton.MENU, false);
                }
            }

            LeftTrigger = (float)gamepad.LeftTrigger / 255f;
            RightTrigger = (float)gamepad.RightTrigger / 255f;

            LS.x = (float)gamepad.LeftThumbX / maxValue;
            LS.y = (float)gamepad.LeftThumbY / maxValue;
            LSXYSqrt = Mathf.Sqrt(Mathf.Pow(LS.x, 2) + Mathf.Pow(LS.y, 2));

            RS.x = (float)gamepad.RightThumbX / maxValue;
            RS.y = (float)gamepad.RightThumbY / maxValue;
            RSXYSqrt = Mathf.Sqrt(Mathf.Pow(RS.x, 2) + Mathf.Pow(RS.y, 2));

            if (LSXYSqrt > RaidPadPlugin.LSDeadzone.Value)
            {
                if (!LSINPUT)
                {
                    LSINPUT = true;
                }
            }
            else
            {
                if (LSINPUT)
                {
                    LSINPUT = false;
                }
            }
            if (LS.y > RaidPadPlugin.LSDeadzone.Value)
            {
                if (!LSUP)
                {
                    LSUP = true;
                    GeneratePressType(ERaidPadButton.LSUP, true);
                    if (!LSButtons && RaidPadPlugin.InterfaceStick.Value == ERaidPadUseStick.LS)
                    {
                        ControllerUIMove(new Vector2Int(0, 1), false);
                        InterfaceStickMoveTime = 0f;
                        InterfaceStickMoveTimeDelay = 0.15f;
                    }
                    if (QuickSkipStick && !LSButtons && RaidPadPlugin.InterfaceSkipStick.Value == ERaidPadUseStick.LS)
                    {
                        ControllerUIMove(new Vector2Int(0, 1), true);
                        InterfaceSkipStickMoveTime = 0f;
                        InterfaceSkipStickMoveTimeDelay = 0.15f;
                    }
                }
            }
            else
            {
                if (LSUP)
                {
                    LSUP = false;
                    GeneratePressType(ERaidPadButton.LSUP, false);
                }
            }
            if (LS.y < -RaidPadPlugin.LSDeadzone.Value)
            {
                if (!LSDOWN)
                {
                    LSDOWN = true;
                    GeneratePressType(ERaidPadButton.LSDOWN, true);
                    if (!LSButtons && RaidPadPlugin.InterfaceStick.Value == ERaidPadUseStick.LS)
                    {
                        ControllerUIMove(new Vector2Int(0, -1), false);
                        InterfaceStickMoveTime = 0f;
                        InterfaceStickMoveTimeDelay = 0.15f;
                    }
                    if (QuickSkipStick && !LSButtons && RaidPadPlugin.InterfaceSkipStick.Value == ERaidPadUseStick.LS)
                    {
                        ControllerUIMove(new Vector2Int(0, -1), true);
                        InterfaceSkipStickMoveTime = 0f;
                        InterfaceSkipStickMoveTimeDelay = 0.15f;
                    }
                }
            }
            else
            {
                if (LSDOWN)
                {
                    LSDOWN = false;
                    GeneratePressType(ERaidPadButton.LSDOWN, false);
                }
            }
            if (LS.x > RaidPadPlugin.LSDeadzone.Value)
            {
                if (!LSRIGHT)
                {
                    LSRIGHT = true;
                    GeneratePressType(ERaidPadButton.LSRIGHT, true);
                    if (!LSButtons && RaidPadPlugin.InterfaceStick.Value == ERaidPadUseStick.LS)
                    {
                        ControllerUIMove(new Vector2Int(1, 0), false);
                        InterfaceStickMoveTime = 0f;
                        InterfaceStickMoveTimeDelay = 0.15f;
                    }
                    if (QuickSkipStick && !LSButtons && RaidPadPlugin.InterfaceSkipStick.Value == ERaidPadUseStick.LS)
                    {
                        ControllerUIMove(new Vector2Int(1, 0), true);
                        InterfaceSkipStickMoveTime = 0f;
                        InterfaceSkipStickMoveTimeDelay = 0.15f;
                    }
                }
            }
            else
            {
                if (LSRIGHT)
                {
                    LSRIGHT = false;
                    GeneratePressType(ERaidPadButton.LSRIGHT, false);
                }
            }
            if (LS.x < -RaidPadPlugin.LSDeadzone.Value)
            {
                if (!LSLEFT)
                {
                    LSLEFT = true;
                    GeneratePressType(ERaidPadButton.LSLEFT, true);
                    if (!LSButtons && RaidPadPlugin.InterfaceStick.Value == ERaidPadUseStick.LS)
                    {
                        ControllerUIMove(new Vector2Int(-1, 0), false);
                        InterfaceStickMoveTime = 0f;
                        InterfaceStickMoveTimeDelay = 0.15f;
                    }
                    if (QuickSkipStick && !LSButtons && RaidPadPlugin.InterfaceSkipStick.Value == ERaidPadUseStick.LS)
                    {
                        ControllerUIMove(new Vector2Int(-1, 0), true);
                        InterfaceSkipStickMoveTime = 0f;
                        InterfaceSkipStickMoveTimeDelay = 0.15f;
                    }
                }
            }
            else
            {
                if (LSLEFT)
                {
                    LSLEFT = false;
                    GeneratePressType(ERaidPadButton.LSLEFT, false);
                }
            }

            if (RSXYSqrt > RaidPadPlugin.RSDeadzone.Value)
            {
                if (!RSINPUT)
                {
                    RSINPUT = true;
                }
            }
            else
            {
                if (RSINPUT)
                {
                    RSINPUT = false;
                }
            }
            if (RS.y > RaidPadPlugin.RSDeadzone.Value)
            {
                if (!RSUP)
                {
                    RSUP = true;
                    GeneratePressType(ERaidPadButton.RSUP, true);
                    if (!RSButtons && RaidPadPlugin.InterfaceStick.Value == ERaidPadUseStick.RS)
                    {
                        ControllerUIMove(new Vector2Int(0, 1), false);
                        InterfaceStickMoveTime = 0f;
                        InterfaceStickMoveTimeDelay = 0.15f;
                    }
                    if (QuickSkipStick && !RSButtons && RaidPadPlugin.InterfaceSkipStick.Value == ERaidPadUseStick.RS)
                    {
                        ControllerUIMove(new Vector2Int(0, 1), true);
                        InterfaceSkipStickMoveTime = 0f;
                        InterfaceSkipStickMoveTimeDelay = 0.15f;
                    }
                }
            }
            else
            {
                if (RSUP)
                {
                    RSUP = false;
                    GeneratePressType(ERaidPadButton.RSUP, false);
                }
            }
            if (RS.y < -RaidPadPlugin.RSDeadzone.Value)
            {
                if (!RSDOWN)
                {
                    RSDOWN = true;
                    GeneratePressType(ERaidPadButton.RSDOWN, true);
                    if (!RSButtons && RaidPadPlugin.InterfaceStick.Value == ERaidPadUseStick.RS)
                    {
                        ControllerUIMove(new Vector2Int(0, -1), false);
                        InterfaceStickMoveTime = 0f;
                        InterfaceStickMoveTimeDelay = 0.15f;
                    }
                    if (QuickSkipStick && !RSButtons && RaidPadPlugin.InterfaceSkipStick.Value == ERaidPadUseStick.RS)
                    {
                        ControllerUIMove(new Vector2Int(0, -1), true);
                        InterfaceSkipStickMoveTime = 0f;
                        InterfaceSkipStickMoveTimeDelay = 0.15f;
                    }
                }
            }
            else
            {
                if (RSDOWN)
                {
                    RSDOWN = false;
                    GeneratePressType(ERaidPadButton.RSDOWN, false);
                }
            }
            if (RS.x > RaidPadPlugin.RSDeadzone.Value)
            {
                if (!RSRIGHT)
                {
                    RSRIGHT = true;
                    GeneratePressType(ERaidPadButton.RSRIGHT, true);
                    if (!RSButtons && RaidPadPlugin.InterfaceStick.Value == ERaidPadUseStick.RS)
                    {
                        ControllerUIMove(new Vector2Int(1, 0), false);
                        InterfaceStickMoveTime = 0f;
                        InterfaceStickMoveTimeDelay = 0.15f;
                    }
                    if (QuickSkipStick && !RSButtons && RaidPadPlugin.InterfaceSkipStick.Value == ERaidPadUseStick.RS)
                    {
                        ControllerUIMove(new Vector2Int(1, 0), true);
                        InterfaceSkipStickMoveTime = 0f;
                        InterfaceSkipStickMoveTimeDelay = 0.15f;
                    }
                }
            }
            else
            {
                if (RSRIGHT)
                {
                    RSRIGHT = false;
                    GeneratePressType(ERaidPadButton.RSRIGHT, false);
                }
            }
            if (RS.x < -RaidPadPlugin.RSDeadzone.Value)
            {
                if (!RSLEFT)
                {
                    RSLEFT = true;
                    GeneratePressType(ERaidPadButton.RSLEFT, true);
                    if (!RSButtons && RaidPadPlugin.InterfaceStick.Value == ERaidPadUseStick.RS)
                    {
                        ControllerUIMove(new Vector2Int(-1, 0), false);
                        InterfaceStickMoveTime = 0f;
                        InterfaceStickMoveTimeDelay = 0.15f;
                    }
                    if (QuickSkipStick && !RSButtons && RaidPadPlugin.InterfaceSkipStick.Value == ERaidPadUseStick.RS)
                    {
                        ControllerUIMove(new Vector2Int(-1, 0), true);
                        InterfaceSkipStickMoveTime = 0f;
                        InterfaceSkipStickMoveTimeDelay = 0.15f;
                    }
                }
            }
            else
            {
                if (RSLEFT)
                {
                    RSLEFT = false;
                    GeneratePressType(ERaidPadButton.RSLEFT, false);
                }
            }

            // Interface
            if (Interface)
            {
                // Auto Move
                if (AutoMove || SplitDialogAutoMove)
                {
                    AutoMoveTime += Time.deltaTime;
                    if (AutoMoveTime > AutoMoveTimeDelay)
                    {
                        AutoMoveTime = 0f;
                        AutoMoveTimeDelay = 0.1f;
                        if (SplitDialogAutoMove)
                        {
                            ControllerSplitDialogAdd(lastIntSliderValue);
                        }
                        else if (AutoMove)
                        {
                            ControllerUIMove(lastDirection, false);
                        }
                    }
                }
                else
                {
                    AutoMoveTimeDelay = 0.2f;
                }

                // Window Move
                bool WindowLS = false;
                bool WindowRS = false;
                switch (RaidPadPlugin.WindowStick.Value)
                {
                    case ERaidPadUseStick.LS:
                        if (!LSButtons && LSXYSqrt > RaidPadPlugin.LSDeadzone.Value)
                        {
                            if (currentContainedGridsView != null)
                            {
                                currentContainedGridsView.transform.parent.position += new Vector3(LS.x, LS.y, 0f) * 1000f * Time.deltaTime;
                                WindowLS = true;
                            }
                            else if (currentItemSpecificationPanel != null)
                            {
                                currentItemSpecificationPanel.transform.position += new Vector3(LS.x, LS.y, 0f) * 1000f * Time.deltaTime;
                                WindowLS = true;
                            }
                        }
                        break;
                    case ERaidPadUseStick.RS:
                        if (!RSButtons && currentContainedGridsView != null && RSXYSqrt > RaidPadPlugin.RSDeadzone.Value)
                        {
                            if (currentContainedGridsView != null)
                            {
                                currentContainedGridsView.transform.parent.position += new Vector3(RS.x, RS.y, 0f) * 1000f * Time.deltaTime;
                                WindowRS = true;
                            }
                            else if (currentItemSpecificationPanel != null)
                            {
                                currentItemSpecificationPanel.transform.position += new Vector3(RS.x, RS.y, 0f) * 1000f * Time.deltaTime;
                                WindowRS = true;
                            }
                        }
                        break;
                }

                // Stick Move
                switch (RaidPadPlugin.InterfaceStick.Value)
                {
                    case ERaidPadUseStick.LS:
                        if (LSButtons || WindowLS) break;
                        InterfaceStickMoveTime += Time.deltaTime;
                        if (InterfaceStickMoveTime > InterfaceStickMoveTimeDelay && (Mathf.Abs(LS.x) > RaidPadPlugin.LSDeadzone.Value || Mathf.Abs(LS.y) > RaidPadPlugin.LSDeadzone.Value))
                        {
                            InterfaceStickMoveTime = 0f;
                            InterfaceStickMoveTimeDelay = RaidPadPlugin.InterfaceStickMoveDelay.Value;
                            ControllerUIMove(new Vector2Int(LS.x > RaidPadPlugin.LSDeadzone.Value ? 1 : LS.x < -RaidPadPlugin.LSDeadzone.Value ? -1 : 0, LS.y > RaidPadPlugin.LSDeadzone.Value ? 1 : LS.y < -RaidPadPlugin.LSDeadzone.Value ? -1 : 0), false);
                        }
                        else if (!(Mathf.Abs(LS.x) > RaidPadPlugin.LSDeadzone.Value || Mathf.Abs(LS.y) > RaidPadPlugin.LSDeadzone.Value))
                        {
                            InterfaceStickMoveTime = 1f;
                            InterfaceStickMoveTimeDelay = 0.3f;
                        }
                        break;
                    case ERaidPadUseStick.RS:
                        if (RSButtons || WindowRS) break;
                        InterfaceStickMoveTime += Time.deltaTime;
                        if (InterfaceStickMoveTime > InterfaceStickMoveTimeDelay && (Mathf.Abs(RS.x) > RaidPadPlugin.RSDeadzone.Value || Mathf.Abs(RS.y) > RaidPadPlugin.RSDeadzone.Value))
                        {
                            InterfaceStickMoveTime = 0f;
                            InterfaceStickMoveTimeDelay = RaidPadPlugin.InterfaceStickMoveDelay.Value;
                            ControllerUIMove(new Vector2Int(RS.x > RaidPadPlugin.RSDeadzone.Value ? 1 : RS.x < -RaidPadPlugin.RSDeadzone.Value ? -1 : 0, RS.y > RaidPadPlugin.RSDeadzone.Value ? 1 : RS.y < -RaidPadPlugin.RSDeadzone.Value ? -1 : 0), false);
                        }
                        else if (!(Mathf.Abs(RS.x) > RaidPadPlugin.RSDeadzone.Value || Mathf.Abs(RS.y) > RaidPadPlugin.RSDeadzone.Value))
                        {
                            InterfaceStickMoveTime = 1f;
                            InterfaceStickMoveTimeDelay = 0.3f;
                        }
                        break;
                }

                // Stick Skip Move
                switch (RaidPadPlugin.InterfaceSkipStick.Value)
                {
                    case ERaidPadUseStick.LS:
                        if (LSButtons || WindowLS) break;
                        InterfaceSkipStickMoveTime += Time.deltaTime;
                        if (InterfaceSkipStickMoveTime > InterfaceSkipStickMoveTimeDelay && (Mathf.Abs(LS.x) > RaidPadPlugin.LSDeadzone.Value || Mathf.Abs(LS.y) > RaidPadPlugin.LSDeadzone.Value))
                        {
                            InterfaceSkipStickMoveTime = 0f;
                            InterfaceSkipStickMoveTimeDelay = 0.15f;
                            ControllerUIMove(new Vector2Int(LS.x > RaidPadPlugin.LSDeadzone.Value ? 1 : LS.x < -RaidPadPlugin.LSDeadzone.Value ? -1 : 0, LS.y > RaidPadPlugin.LSDeadzone.Value ? 1 : LS.y < -RaidPadPlugin.LSDeadzone.Value ? -1 : 0), true);
                        }
                        else if (!(Mathf.Abs(LS.x) > RaidPadPlugin.LSDeadzone.Value || Mathf.Abs(LS.y) > RaidPadPlugin.LSDeadzone.Value))
                        {
                            InterfaceSkipStickMoveTime = 1f;
                            InterfaceSkipStickMoveTimeDelay = 0.3f;
                        }
                        break;
                    case ERaidPadUseStick.RS:
                        if (RSButtons || WindowRS) break;
                        InterfaceSkipStickMoveTime += Time.deltaTime;
                        if (InterfaceSkipStickMoveTime > InterfaceSkipStickMoveTimeDelay && (Mathf.Abs(RS.x) > RaidPadPlugin.RSDeadzone.Value || Mathf.Abs(RS.y) > RaidPadPlugin.RSDeadzone.Value))
                        {
                            InterfaceSkipStickMoveTime = 0f;
                            InterfaceSkipStickMoveTimeDelay = 0.15f;
                            ControllerUIMove(new Vector2Int(RS.x > RaidPadPlugin.RSDeadzone.Value ? 1 : RS.x < -RaidPadPlugin.RSDeadzone.Value ? -1 : 0, RS.y > RaidPadPlugin.RSDeadzone.Value ? 1 : RS.y < -RaidPadPlugin.RSDeadzone.Value ? -1 : 0), true);
                        }
                        else if (!(Mathf.Abs(RS.x) > RaidPadPlugin.RSDeadzone.Value || Mathf.Abs(RS.y) > RaidPadPlugin.RSDeadzone.Value))
                        {
                            InterfaceSkipStickMoveTime = 1f;
                            InterfaceSkipStickMoveTimeDelay = 0.3f;
                        }
                        break;
                }

                // Scroll
                if (currentScrollRectNoDrag != null && currentScrollRectNoDragRectTransform != null && !ContextMenu)
                {
                    switch (RaidPadPlugin.ScrollStick.Value)
                    {
                        case ERaidPadUseStick.None:
                            ControllerAutoScroll();
                            break;
                        case ERaidPadUseStick.LS:
                            if (Mathf.Abs(LS.y) > RaidPadPlugin.LSDeadzone.Value && !LSButtons && !WindowLS)
                            {
                                ControllerScroll(LS.y);
                            }
                            else
                            {
                                ControllerAutoScroll();
                            }
                            break;
                        case ERaidPadUseStick.RS:
                            if (Mathf.Abs(RS.y) > RaidPadPlugin.RSDeadzone.Value && !RSButtons && !WindowRS)
                            {
                                ControllerScroll(RS.y);
                            }
                            else
                            {
                                ControllerAutoScroll();
                            }
                            break;
                    }
                }

                return;
            }

            // Movement
            if (!LSButtons && LSXYSqrt > RaidPadPlugin.MovementDeadzone.Value)
            {
                Player.Move(LS.normalized);
                CharacterMovementSpeed = 0f;
                if (MovementContextObject != null)
                {
                    StateSpeedLimit = Traverse.Create(MovementContextObject).Property("StateSpeedLimit").GetValue<float>();
                    MaxSpeed = Traverse.Create(MovementContextObject).Property("MaxSpeed").GetValue<float>();
                    CharacterMovementSpeed = Mathf.Lerp(-RaidPadPlugin.MovementDeadzone.Value - RaidPadPlugin.DeadzoneBuffer.Value, 1f, LSXYSqrt) * Mathf.Min(StateSpeedLimit, MaxSpeed);
                    MovementInvokeParameters[0] = CharacterMovementSpeed;
                    SetCharacterMovementSpeed.Invoke(MovementContextObject, MovementInvokeParameters);
                }
                if (speedSlider != null)
                {
                    speedSlider.value = Mathf.Floor(((CharacterMovementSpeed + 0.005f) / speedSlider.maxValue) * 20f) * (speedSlider.maxValue / 20f);
                }
                resetCharacterMovementSpeed = true;
            }
            else if (resetCharacterMovementSpeed)
            {
                resetCharacterMovementSpeed = false;
                if (MovementContextObject != null)
                {
                    MovementInvokeParameters[0] = 0f;
                    SetCharacterMovementSpeed.Invoke(MovementContextObject, MovementInvokeParameters);
                }
                if (speedSlider != null)
                {
                    speedSlider.value = 0;
                }
            }

            // Aiming
            if (Player != null && Camera.main != null)
            {
                Magnetism = false;
                Stickiness = 0;
                AutoAim = Vector2.zero;

                AimPosition = Vector3.one;
                AimDirection = Vector3.forward;

                // Never cache this. EFT pools hands controllers, so a cached reference stays
                // non-null across a weapon swap while still pointing at the previous weapon's
                // fireport. That put the occlusion raycast's origin on the wrong muzzle, which is
                // what let aim assist lock through walls.
                firearmController = Player.HandsController as Player.FirearmController;
                if (firearmController != null)
                {
                    AimPosition = firearmController.CurrentFireport.position;
                    AimDirection = firearmController.WeaponDirection;
                    firearmController.AdjustShotVectors(ref AimPosition, ref AimDirection);
                }
                colliders = new Collider[100];
                colliderCount = Physics.OverlapCapsuleNonAlloc(AimPosition, AimPosition + (AimDirection * 200f), RaidPadPlugin.Radius.Value, colliders, AimAssistLayerMask, QueryTriggerInteraction.Ignore);

                ScreenSize = new Vector2(Screen.width, Screen.height);
                ScreenSizeRatioMultiplier = new Vector2(1f, (float)(Screen.height) / (float)(Screen.width));

                AimAssistAngle = 100000f;
                AimAssistLocalPlayer = null;

                for (int i = 0; i < colliderCount; i++)
                {
                    SSAARatio = (float)currentSSAA.GetOutputHeight() / (float)currentSSAA.GetInputHeight();

                    HitAimAssistLocalPlayer = colliders[i].transform.gameObject.GetComponent<LocalPlayer>();
                    if (HitAimAssistLocalPlayer != null && HitAimAssistLocalPlayer != Player && IsAimAssistTarget(HitAimAssistLocalPlayer))
                    {
                        AimAssistScreenLocalPosition = ((((((Vector2)Camera.main.WorldToScreenPoint(HitAimAssistLocalPlayer.PlayerBones.Head.position + (HitAimAssistLocalPlayer.Velocity * 0f))  * SSAARatio) - (((((Vector2)Camera.main.WorldToScreenPoint(AimPosition + (AimDirection * Vector3.Distance(AimPosition, HitAimAssistLocalPlayer.PlayerBones.Head.position + (HitAimAssistLocalPlayer.Velocity * 0f))))  * SSAARatio) - (((Vector2)Camera.main.WorldToScreenPoint(HitAimAssistLocalPlayer.PlayerBones.Head.position + (HitAimAssistLocalPlayer.Velocity * 0f))  * SSAARatio) - (ScreenSize / 2f))) - (ScreenSize / 2f)) * 2f)) - (ScreenSize / 2f)) / ScreenSize) * ScreenSizeRatioMultiplier);
                        AimAssistBoneAngle = Mathf.Sqrt(Vector2.SqrMagnitude(AimAssistScreenLocalPosition)) / (ScreenSize.y / ScreenSize.x);
                        if (AimAssistBoneAngle < Mathf.Max(RaidPadPlugin.MagnetismRadius.Value, RaidPadPlugin.StickinessRadius.Value, RaidPadPlugin.AutoAimRadius.Value) && AimAssistBoneAngle < AimAssistAngle && !Physics.Raycast(AimPosition, (HitAimAssistLocalPlayer.PlayerBones.Head.position - AimPosition).normalized, out hit, Vector3.Distance(HitAimAssistLocalPlayer.PlayerBones.Head.position, AimPosition), HighLayerMask, QueryTriggerInteraction.Ignore))
                        {
                            AimAssistAngle = AimAssistBoneAngle;
                            AimAssistLocalPlayer = HitAimAssistLocalPlayer;
                            AimAssistTarget2DPoint = AimAssistScreenLocalPosition;
                        }
                        AimAssistScreenLocalPosition = ((((((Vector2)Camera.main.WorldToScreenPoint(HitAimAssistLocalPlayer.PlayerBones.Ribcage.position + (HitAimAssistLocalPlayer.Velocity * 0f))  * SSAARatio) - (((((Vector2)Camera.main.WorldToScreenPoint(AimPosition + (AimDirection * Vector3.Distance(AimPosition, HitAimAssistLocalPlayer.PlayerBones.Ribcage.position + (HitAimAssistLocalPlayer.Velocity * 0f))))  * SSAARatio) - (((Vector2)Camera.main.WorldToScreenPoint(HitAimAssistLocalPlayer.PlayerBones.Ribcage.position + (HitAimAssistLocalPlayer.Velocity * 0f))  * SSAARatio) - (ScreenSize / 2f))) - (ScreenSize / 2f)) * 2f)) - (ScreenSize / 2f)) / ScreenSize) * ScreenSizeRatioMultiplier);
                        AimAssistBoneAngle = Mathf.Sqrt(Vector2.SqrMagnitude(AimAssistScreenLocalPosition)) / (ScreenSize.y / ScreenSize.x);
                        if (AimAssistBoneAngle < Mathf.Max(RaidPadPlugin.MagnetismRadius.Value, RaidPadPlugin.StickinessRadius.Value, RaidPadPlugin.AutoAimRadius.Value) && AimAssistBoneAngle < AimAssistAngle && !Physics.Raycast(AimPosition, (HitAimAssistLocalPlayer.PlayerBones.Ribcage.position - AimPosition).normalized, out hit, Vector3.Distance(HitAimAssistLocalPlayer.PlayerBones.Ribcage.position, AimPosition), HighLayerMask, QueryTriggerInteraction.Ignore))
                        {
                            AimAssistAngle = AimAssistBoneAngle;
                            AimAssistLocalPlayer = HitAimAssistLocalPlayer;
                            AimAssistTarget2DPoint = AimAssistScreenLocalPosition;
                        }
                        AimAssistScreenLocalPosition = ((((((Vector2)Camera.main.WorldToScreenPoint(HitAimAssistLocalPlayer.PlayerBones.Pelvis.position + (HitAimAssistLocalPlayer.Velocity * 0f))  * SSAARatio) - (((((Vector2)Camera.main.WorldToScreenPoint(AimPosition + (AimDirection * Vector3.Distance(AimPosition, HitAimAssistLocalPlayer.PlayerBones.Pelvis.position + (HitAimAssistLocalPlayer.Velocity * 0f))))  * SSAARatio) - (((Vector2)Camera.main.WorldToScreenPoint(HitAimAssistLocalPlayer.PlayerBones.Pelvis.position + (HitAimAssistLocalPlayer.Velocity * 0f))  * SSAARatio) - (ScreenSize / 2f))) - (ScreenSize / 2f)) * 2f)) - (ScreenSize / 2f)) / ScreenSize) * ScreenSizeRatioMultiplier);
                        AimAssistBoneAngle = Mathf.Sqrt(Vector2.SqrMagnitude(AimAssistScreenLocalPosition)) / (ScreenSize.y / ScreenSize.x);
                        if (AimAssistBoneAngle < Mathf.Max(RaidPadPlugin.MagnetismRadius.Value, RaidPadPlugin.StickinessRadius.Value, RaidPadPlugin.AutoAimRadius.Value) && AimAssistBoneAngle < AimAssistAngle && !Physics.Raycast(AimPosition, (HitAimAssistLocalPlayer.PlayerBones.Pelvis.position - AimPosition).normalized, out hit, Vector3.Distance(HitAimAssistLocalPlayer.PlayerBones.Pelvis.position, AimPosition), HighLayerMask, QueryTriggerInteraction.Ignore))
                        {
                            AimAssistAngle = AimAssistBoneAngle;
                            AimAssistLocalPlayer = HitAimAssistLocalPlayer;
                            AimAssistTarget2DPoint = AimAssistScreenLocalPosition;
                        }
                    }
                }
                if (AimAssistLocalPlayer != null && firearmController != null)
                {
                    if (AimAssistAngle < RaidPadPlugin.MagnetismRadius.Value)
                    {
                        Magnetism = true;
                    }
                    if (AimAssistAngle < RaidPadPlugin.StickinessRadius.Value)
                    {
                        Stickiness = Mathf.Lerp(1f, 0f, (Mathf.Clamp(AimAssistAngle / RaidPadPlugin.StickinessRadius.Value, 0.5f, 1f) - 0.5f) / (1f - 0.5f));
                    }
                    if (AimAssistAngle < RaidPadPlugin.AutoAimRadius.Value)
                    {
                        AutoAim = Vector2.Lerp(Vector2.Lerp(Vector2.zero, Vector2.Lerp(new Vector2(Mathf.Clamp((AimAssistTarget2DPoint.x) * 10f, -0.5f, 0.5f), Mathf.Clamp((AimAssistTarget2DPoint.y) * -5f, -0.5f, 0.5f)) * 100f * Time.deltaTime, Vector2.zero, (Mathf.Clamp(AimAssistAngle / RaidPadPlugin.AutoAimRadius.Value, 0.5f, 1f) - 0.5f) / (1f - 0.5f)) * RaidPadPlugin.AutoAim.Value, 1f) / firearmController.AimingSensitivity * (firearmController.IsAiming ? 2f : 1f), Vector2.zero, RSXYSqrt);
                    }
                }
            }
            StickinessSmooth += ((Stickiness - StickinessSmooth) * RaidPadPlugin.StickinessSmooth.Value) * Time.deltaTime;
            AutoAimSmooth += ((AutoAim - AutoAimSmooth) * RaidPadPlugin.AutoAimSmooth.Value) * Time.deltaTime;
            if (!RSButtons && RSXYSqrt > RaidPadPlugin.AimDeadzone.Value || Mathf.Sqrt(Mathf.Pow(AutoAimSmooth.x, 2) + Mathf.Pow(AutoAimSmooth.y, 2)) > RaidPadPlugin.AimDeadzone.Value)
            {
                Aim.x = RS.x * AimAnimationCurve.Evaluate(RSXYSqrt);
                Aim.y = RS.y * AimAnimationCurve.Evaluate(RSXYSqrt);
                Player.Rotate(((Aim * (isAiming ? RaidPadPlugin.AimingSensitivity.Value : RaidPadPlugin.Sensitivity.Value) * (RaidPadPlugin.InvertY.Value ? Vector2.one * 100f : InvertY) * Time.deltaTime) * Mathf.Lerp(1f, RaidPadPlugin.Stickiness.Value, StickinessSmooth)) + AutoAimSmooth, false);
            }

            // Aiming Set
            if (Player.HandsController != null)
            {
                if (Player.HandsController.IsAiming && !isAiming)
                {
                    isAiming = true;
                    EnableSet("Aiming");
                }
                else if (isAiming && !Player.HandsController.IsAiming)
                {
                    isAiming = false;
                    DisableSet("Aiming");
                }
            }

            // Lean
            if (SlowLeanLeft || SlowLeanRight)
            {
                Player.SlowLean(((SlowLeanLeft ? -RaidPadPlugin.LeanSensitivity.Value: 0) + (SlowLeanRight ? RaidPadPlugin.LeanSensitivity.Value : 0)) * Time.deltaTime);
            }
        }
        private void BlockPositionUpdated(object sender, EventArgs e)
        {
            if (AllGameObject != null && AllGameObject.activeSelf)
            {
                AllGameObject.transform.position = new Vector2(Screen.width, 0f) + RaidPadPlugin.BlockPosition.Value;
            }
        }
        private void UserIndexUpdated(object sender, EventArgs e)
        {
            if (Player != null)
            {
                ResolveController();
            }
        }

        /// <summary>
        /// Pick an input device: the pinned XInput slot if the user set one and it is live,
        /// otherwise the first occupied XInput slot, otherwise a native DualShock/DualSense pad.
        /// </summary>
        private void ResolveController()
        {
            int index = RaidPadPlugin.UserIndex.Value;
            if (index >= 1 && index <= 4)
            {
                Controller pinned = new Controller((UserIndex)(index - 1));
                if (pinned.IsConnected)
                {
                    controller = pinned;
                    usingHid = false;
                    connected = true;
                    return;
                }
            }

            for (int i = 0; i < 4; i++)
            {
                Controller candidate = new Controller((UserIndex)i);
                if (candidate.IsConnected)
                {
                    controller = candidate;
                    usingHid = false;
                    connected = true;
                    return;
                }
            }

            if (hid == null) hid = new RaidPadHidController();
            if (hid.EnsureOpen())
            {
                usingHid = true;
                connected = true;
            }
            else
            {
                usingHid = false;
                connected = false;
            }
        }

        /// <summary>Aim assist skips the dead and, optionally, your own group.</summary>
        private bool IsAimAssistTarget(LocalPlayer target)
        {
            try
            {
                if (target.HealthController != null && !target.HealthController.IsAlive) return false;
                if (RaidPadPlugin.ExcludeTeammates.Value && Player != null)
                {
                    string groupId = target.GroupId;
                    // Solo raids leave GroupId empty on everyone, so an empty id must never match.
                    if (!string.IsNullOrEmpty(groupId) && groupId == Player.GroupId) return false;
                }
                return true;
            }
            catch
            {
                return true;
            }
        }

        public void UpdateController(Player player)
        {
            ScreenRatio = (Screen.height / 1080f);

            eventSystem = FindObjectOfType<EventSystem>();
            pointerEventData = new PointerEventData(eventSystem);
            pointerEventData.button = PointerEventData.InputButton.Left;
            ResolveController();

            if (player != null)
            {
                Player = player;
                //movementContext = localPlayer.MovementContext;
                MovementContextObject = Traverse.Create(Player).Property("MovementContext").GetValue<object>();
                MovementContextType = MovementContextObject.GetType();
                SetCharacterMovementSpeed = MovementContextType.GetMethod("SetCharacterMovementSpeed", BindingFlags.Instance | BindingFlags.Public);
            }
            globalPosition = new Vector2(Screen.width / 2.2f, Screen.height);
            if (controllerPresetJsonClass != null && controllerPresetJsonClass.RaidPadButtonBinds != null && controllerPresetJsonClass.RaidPadSets != null)
            {
                RaidPadSets.Clear();
                RaidPadButtonBinds.Clear();
                RaidPadSets = new Dictionary<string, Dictionary<ERaidPadButton, List<RaidPadButtonBind>>>(controllerPresetJsonClass.RaidPadSets);
                RaidPadButtonBinds = new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>(controllerPresetJsonClass.RaidPadButtonBinds);
                return;
            }
        }
        public void DefaultJSON()
        {
            Dictionary<string, Dictionary<ERaidPadButton, List<RaidPadButtonBind>>> RaidPadSets = new Dictionary<string, Dictionary<ERaidPadButton, List<RaidPadButtonBind>>>();
            Dictionary<ERaidPadButton, List<RaidPadButtonBind>> RaidPadButtonBinds = new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>();

            RaidPadSets.Clear();
            RaidPadSets.Add("LB", new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>());
            RaidPadSets["LB"].Add(ERaidPadButton.UP, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ThrowGrenade), ERaidPadPressType.Press, 2) });
            RaidPadSets["LB"].Add(ERaidPadButton.DOWN, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.SelectSecondaryWeapon), ERaidPadPressType.Press, 2) });
            RaidPadSets["LB"][ERaidPadButton.DOWN].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.SelectSecondaryWeapon), ERaidPadPressType.DoubleClick, 2));
            RaidPadSets["LB"].Add(ERaidPadButton.LEFT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.SelectSecondPrimaryWeapon), ERaidPadPressType.Press, 2) });
            RaidPadSets["LB"].Add(ERaidPadButton.RIGHT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.SelectFirstPrimaryWeapon), ERaidPadPressType.Press, 2) });

            RaidPadSets["LB"].Add(ERaidPadButton.A, new List<RaidPadButtonBind> { new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.PressSlot4), new RaidPadCommand(ERaidPadCommand.EnableSet, "HealingLimbSelector") }, ERaidPadPressType.Press, 2) });
            RaidPadSets["LB"][ERaidPadButton.A].Add(new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.SelectFastSlot4), new RaidPadCommand(ERaidPadCommand.DisableSet, "HealingLimbSelector") }, ERaidPadPressType.Release, 2));
            RaidPadSets["LB"].Add(ERaidPadButton.B, new List<RaidPadButtonBind> { new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.PressSlot5), new RaidPadCommand(ERaidPadCommand.EnableSet, "HealingLimbSelector") }, ERaidPadPressType.Press, 2) });
            RaidPadSets["LB"][ERaidPadButton.B].Add(new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.SelectFastSlot5), new RaidPadCommand(ERaidPadCommand.DisableSet, "HealingLimbSelector") }, ERaidPadPressType.Release, 2));
            RaidPadSets["LB"].Add(ERaidPadButton.X, new List<RaidPadButtonBind> { new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.PressSlot6), new RaidPadCommand(ERaidPadCommand.EnableSet, "HealingLimbSelector") }, ERaidPadPressType.Press, 2) });
            RaidPadSets["LB"][ERaidPadButton.X].Add(new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.SelectFastSlot6), new RaidPadCommand(ERaidPadCommand.DisableSet, "HealingLimbSelector") }, ERaidPadPressType.Release, 2));
            RaidPadSets["LB"].Add(ERaidPadButton.Y, new List<RaidPadButtonBind> { new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.PressSlot7), new RaidPadCommand(ERaidPadCommand.EnableSet, "HealingLimbSelector") }, ERaidPadPressType.Press, 2) });
            RaidPadSets["LB"][ERaidPadButton.Y].Add(new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.SelectFastSlot7), new RaidPadCommand(ERaidPadCommand.DisableSet, "HealingLimbSelector") }, ERaidPadPressType.Release, 2));

            RaidPadSets["LB"].Add(ERaidPadButton.LS, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.DropBackpack), ERaidPadPressType.Press, 2) });
            RaidPadSets["LB"].Add(ERaidPadButton.BACK, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ToggleGoggles), ERaidPadPressType.Press, 2) });

            RaidPadSets.Add("RB", new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>());
            RaidPadSets["RB"].Add(ERaidPadButton.X, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ChamberUnload), ERaidPadPressType.Press, 1) });
            RaidPadSets["RB"][ERaidPadButton.X].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.CheckChamber), ERaidPadPressType.Hold, 1));
            RaidPadSets["RB"][ERaidPadButton.X].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.UnloadMagazine), ERaidPadPressType.DoubleClick, 1));

            RaidPadSets["RB"].Add(ERaidPadButton.B, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.EnableSet, "Movement"), ERaidPadPressType.Press, 1) });
            RaidPadSets["RB"][ERaidPadButton.B].Add(new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.DisableSet, "Movement"), ERaidPadPressType.Release, 1));

            RaidPadSets["RB"].Add(ERaidPadButton.Y, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.FoldStock), ERaidPadPressType.Press, 1) });
            RaidPadSets["RB"][ERaidPadButton.Y].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.CheckChamber), ERaidPadPressType.Hold, 1));

            RaidPadSets["RB"].Add(ERaidPadButton.LEFT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ToggleLeanLeft), ERaidPadPressType.Press, 1) });
            RaidPadSets["RB"].Add(ERaidPadButton.RIGHT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ToggleLeanRight), ERaidPadPressType.Press, 1) });

            RaidPadSets["RB"].Add(ERaidPadButton.BACK, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.SwitchHeadLight), ERaidPadPressType.Press, 1) });
            RaidPadSets["RB"][ERaidPadButton.BACK].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.ToggleHeadLight), ERaidPadPressType.Hold, 1));


            RaidPadSets["RB"].Add(ERaidPadButton.LS, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.LeftStanceToggle), ERaidPadPressType.Press, 1) });

            RaidPadSets.Add("LB_RB", new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>());
            RaidPadSets["LB_RB"].Add(ERaidPadButton.UP, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ToggleBlindAbove), ERaidPadPressType.Press, 3) });
            RaidPadSets["LB_RB"][ERaidPadButton.UP].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.BlindShootEnd), ERaidPadPressType.Release, 3));
            RaidPadSets["LB_RB"].Add(ERaidPadButton.DOWN, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ToggleBlindRight), ERaidPadPressType.Press, 3) });
            RaidPadSets["LB_RB"][ERaidPadButton.DOWN].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.BlindShootEnd), ERaidPadPressType.Release, 3));
            RaidPadSets["LB_RB"].Add(ERaidPadButton.LEFT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ToggleStepLeft), ERaidPadPressType.Press, 3) });
            RaidPadSets["LB_RB"][ERaidPadButton.LEFT].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.ReturnFromLeftStep), ERaidPadPressType.Release, 3));
            RaidPadSets["LB_RB"].Add(ERaidPadButton.RIGHT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ToggleStepRight), ERaidPadPressType.Press, 3) });
            RaidPadSets["LB_RB"][ERaidPadButton.RIGHT].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.ReturnFromRightStep), ERaidPadPressType.Release, 3));

            RaidPadSets["LB_RB"].Add(ERaidPadButton.A, new List<RaidPadButtonBind> { new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.PressSlot8), new RaidPadCommand(ERaidPadCommand.EnableSet, "HealingLimbSelector") }, ERaidPadPressType.Press, 3) });
            RaidPadSets["LB_RB"][ERaidPadButton.A].Add(new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.SelectFastSlot8), new RaidPadCommand(ERaidPadCommand.DisableSet, "HealingLimbSelector") }, ERaidPadPressType.Release, 3));
            RaidPadSets["LB_RB"].Add(ERaidPadButton.B, new List<RaidPadButtonBind> { new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.PressSlot9), new RaidPadCommand(ERaidPadCommand.EnableSet, "HealingLimbSelector") }, ERaidPadPressType.Press, 3) });
            RaidPadSets["LB_RB"][ERaidPadButton.B].Add(new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.SelectFastSlot9), new RaidPadCommand(ERaidPadCommand.DisableSet, "HealingLimbSelector") }, ERaidPadPressType.Release, 3));
            RaidPadSets["LB_RB"].Add(ERaidPadButton.X, new List<RaidPadButtonBind> { new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.PressSlot0), new RaidPadCommand(ERaidPadCommand.EnableSet, "HealingLimbSelector") }, ERaidPadPressType.Press, 3) });
            RaidPadSets["LB_RB"][ERaidPadButton.X].Add(new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.SelectFastSlot0), new RaidPadCommand(ERaidPadCommand.DisableSet, "HealingLimbSelector") }, ERaidPadPressType.Release, 3));
            RaidPadSets["LB_RB"].Add(ERaidPadButton.Y, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.DisplayTimer), ERaidPadPressType.Press, 3) });
            RaidPadSets["LB_RB"][ERaidPadButton.Y].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.DisplayTimerAndExits), ERaidPadPressType.DoubleClick, 3));
            RaidPadSets["LB_RB"].Add(ERaidPadButton.BACK, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.PauseRaid), ERaidPadPressType.Press, 3) });

            RaidPadSets.Add("MapOverlay", new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>());
            RaidPadSets["MapOverlay"].Add(ERaidPadButton.MENU, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.MapPeek), ERaidPadPressType.Press, 25) });
            RaidPadSets["MapOverlay"].Add(ERaidPadButton.UP, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.MapPanDown), ERaidPadPressType.Press, 25) });
            RaidPadSets["MapOverlay"].Add(ERaidPadButton.DOWN, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.MapPanUp), ERaidPadPressType.Press, 25) });
            RaidPadSets["MapOverlay"].Add(ERaidPadButton.LEFT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.MapPanRight), ERaidPadPressType.Press, 25) });
            RaidPadSets["MapOverlay"].Add(ERaidPadButton.RIGHT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.MapPanLeft), ERaidPadPressType.Press, 25) });
            RaidPadSets["MapOverlay"].Add(ERaidPadButton.LB, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.MapZoomOut), ERaidPadPressType.Press, 25) });
            RaidPadSets["MapOverlay"].Add(ERaidPadButton.RB, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.MapZoomIn), ERaidPadPressType.Press, 25) });
            RaidPadSets["MapOverlay"].Add(ERaidPadButton.Y, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.MapCenter), ERaidPadPressType.Press, 25) });
            RaidPadSets["MapOverlay"].Add(ERaidPadButton.LS, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.MapLevelUp), ERaidPadPressType.Press, 25) });
            RaidPadSets["MapOverlay"].Add(ERaidPadButton.RS, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.MapLevelDown), ERaidPadPressType.Press, 25) });
            RaidPadSets["MapOverlay"].Add(ERaidPadButton.A, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.None), ERaidPadPressType.Press, 25) });
            RaidPadSets["MapOverlay"].Add(ERaidPadButton.B, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.None), ERaidPadPressType.Press, 25) });
            RaidPadSets["MapOverlay"].Add(ERaidPadButton.X, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.None), ERaidPadPressType.Press, 25) });
            RaidPadSets["MapOverlay"].Add(ERaidPadButton.BACK, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.None), ERaidPadPressType.Press, 25) });


            RaidPadSets.Add("ActionPanel", new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>());
            RaidPadSets["ActionPanel"].Add(ERaidPadButton.X, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.BeginInteracting), ERaidPadPressType.Press, 10) });
            RaidPadSets["ActionPanel"][ERaidPadButton.X].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.EndInteracting), ERaidPadPressType.Release, 10));
            RaidPadSets["ActionPanel"].Add(ERaidPadButton.UP, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ScrollPrevious), ERaidPadPressType.Press, 10) });
            RaidPadSets["ActionPanel"].Add(ERaidPadButton.DOWN, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ScrollNext), ERaidPadPressType.Press, 10) });

            RaidPadSets.Add("HealingLimbSelector", new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>());
            RaidPadSets["HealingLimbSelector"].Add(ERaidPadButton.UP, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ScrollNext), ERaidPadPressType.Press, 11) });
            RaidPadSets["HealingLimbSelector"].Add(ERaidPadButton.DOWN, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ScrollPrevious), ERaidPadPressType.Press, 11) });

            RaidPadSets.Add("Movement", new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>());
            RaidPadSets["Movement"].Add(ERaidPadButton.UP, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.NextWalkPose), ERaidPadPressType.Press, 3) });
            RaidPadSets["Movement"].Add(ERaidPadButton.DOWN, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.PreviousWalkPose), ERaidPadPressType.Press, 3) });
            RaidPadSets["Movement"].Add(ERaidPadButton.LSLEFT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.SlowLeanLeft), ERaidPadPressType.Press, 3) });
            RaidPadSets["Movement"][ERaidPadButton.LSLEFT].Add(new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.EndSlowLean), ERaidPadPressType.Release, 3));
            RaidPadSets["Movement"].Add(ERaidPadButton.LSRIGHT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.SlowLeanRight), ERaidPadPressType.Press, 3) });
            RaidPadSets["Movement"][ERaidPadButton.LSRIGHT].Add(new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.EndSlowLean), ERaidPadPressType.Release, 3));

            RaidPadSets.Add("Aiming", new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>());
            RaidPadSets["Aiming"].Add(ERaidPadButton.RS, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ToggleBreathing), ERaidPadPressType.Press, 4) });
            RaidPadSets["Aiming"].Add(ERaidPadButton.RB, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.EnableSet, "Aiming_RB"), ERaidPadPressType.Press, 4) });
            RaidPadSets["Aiming"][ERaidPadButton.RB].Add(new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.DisableSet, "Aiming_RB"), ERaidPadPressType.Release, 4));

            RaidPadSets.Add("Aiming_RB", new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>());
            RaidPadSets["Aiming_RB"].Add(ERaidPadButton.UP, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.OpticCalibrationSwitchUp), ERaidPadPressType.Press, 4) });
            RaidPadSets["Aiming_RB"].Add(ERaidPadButton.DOWN, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.OpticCalibrationSwitchDown), ERaidPadPressType.Press, 4) });

            RaidPadSets.Add("Interface", new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>());
            RaidPadSets["Interface"].Add(ERaidPadButton.UP, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.InterfaceUp), ERaidPadPressType.Press, 20) });
            RaidPadSets["Interface"][ERaidPadButton.UP].Add(new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.InterfaceDisableAutoMove), ERaidPadPressType.Release, 20));
            RaidPadSets["Interface"].Add(ERaidPadButton.DOWN, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.InterfaceDown), ERaidPadPressType.Press, 20) });
            RaidPadSets["Interface"][ERaidPadButton.DOWN].Add(new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.InterfaceDisableAutoMove), ERaidPadPressType.Release, 20));
            RaidPadSets["Interface"].Add(ERaidPadButton.LEFT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.InterfaceLeft), ERaidPadPressType.Press, 20) });
            RaidPadSets["Interface"][ERaidPadButton.LEFT].Add(new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.InterfaceDisableAutoMove), ERaidPadPressType.Release, 20));
            RaidPadSets["Interface"].Add(ERaidPadButton.RIGHT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.InterfaceRight), ERaidPadPressType.Press, 20) });
            RaidPadSets["Interface"][ERaidPadButton.RIGHT].Add(new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.InterfaceDisableAutoMove), ERaidPadPressType.Release, 20));

            RaidPadSets["Interface"].Add(ERaidPadButton.A, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.BeginDrag), ERaidPadPressType.Press, 20) });
            RaidPadSets["Interface"][ERaidPadButton.A].Add(new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.ShowContextMenu), ERaidPadPressType.Hold, 20));
            RaidPadSets["Interface"].Add(ERaidPadButton.B, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.Escape), ERaidPadPressType.Press, 20) });
            RaidPadSets["Interface"].Add(ERaidPadButton.X, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.Use), ERaidPadPressType.Press, 20) });
            RaidPadSets["Interface"][ERaidPadButton.X].Add(new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.UseHold), ERaidPadPressType.Hold, 20));
            RaidPadSets["Interface"].Add(ERaidPadButton.Y, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.QuickMove), ERaidPadPressType.Press, 20) });

            RaidPadSets["Interface"].Add(ERaidPadButton.RS, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.Discard), ERaidPadPressType.Press, 20) });

            RaidPadSets["Interface"].Add(ERaidPadButton.LB, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.EnableSet, "Interface_LB"), ERaidPadPressType.Press, 20) });
            RaidPadSets["Interface"][ERaidPadButton.LB].Add(new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.DisableSet, "Interface_LB"), ERaidPadPressType.Release, 20));

            RaidPadSets["Interface"].Add(ERaidPadButton.LT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.PreviousTab), ERaidPadPressType.Press, 20) });
            RaidPadSets["Interface"].Add(ERaidPadButton.RT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.NextTab), ERaidPadPressType.Press, 20) });

            RaidPadSets.Add("OnDrag", new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>());
            RaidPadSets["OnDrag"].Add(ERaidPadButton.A, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.EndDrag), ERaidPadPressType.Press, 23) });
            RaidPadSets["OnDrag"].Add(ERaidPadButton.B, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.CancelDrag), ERaidPadPressType.Press, 23) });
            RaidPadSets["OnDrag"].Add(ERaidPadButton.X, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.RotateDragged), ERaidPadPressType.Press, 23) });
            RaidPadSets["OnDrag"].Add(ERaidPadButton.Y, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.SplitDragged), ERaidPadPressType.Press, 23) });

            RaidPadSets.Add("Interface_LB", new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>());
            RaidPadSets["Interface_LB"].Add(ERaidPadButton.A, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.InterfaceBind4), ERaidPadPressType.Press, 21) });
            RaidPadSets["Interface_LB"].Add(ERaidPadButton.B, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.InterfaceBind5), ERaidPadPressType.Press, 21) });
            RaidPadSets["Interface_LB"].Add(ERaidPadButton.X, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.InterfaceBind6), ERaidPadPressType.Press, 21) });
            RaidPadSets["Interface_LB"].Add(ERaidPadButton.Y, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.InterfaceBind7), ERaidPadPressType.Press, 21) });

            RaidPadSets.Add("Interface_LB_RB", new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>());
            RaidPadSets["Interface_LB_RB"].Add(ERaidPadButton.A, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.InterfaceBind8), ERaidPadPressType.Press, 22) });
            RaidPadSets["Interface_LB_RB"].Add(ERaidPadButton.B, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.InterfaceBind9), ERaidPadPressType.Press, 22) });
            RaidPadSets["Interface_LB_RB"].Add(ERaidPadButton.X, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.InterfaceBind10), ERaidPadPressType.Press, 22) });

            RaidPadSets.Add("SearchButton", new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>());
            RaidPadSets["SearchButton"].Add(ERaidPadButton.A, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.Search), ERaidPadPressType.Press, 24) });

            RaidPadSets.Add("ContextMenu", new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>());
            RaidPadSets["ContextMenu"].Add(ERaidPadButton.A, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.ContextMenuUse), ERaidPadPressType.Press, 30) });
            RaidPadSets["ContextMenu"].Add(ERaidPadButton.X, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.None), ERaidPadPressType.Press, 30) });
            RaidPadSets["ContextMenu"].Add(ERaidPadButton.Y, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.None), ERaidPadPressType.Press, 30) });
            RaidPadSets["ContextMenu"].Add(ERaidPadButton.RS, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.None), ERaidPadPressType.Press, 30) });

            RaidPadSets.Add("SplitDialog", new Dictionary<ERaidPadButton, List<RaidPadButtonBind>>());
            RaidPadSets["SplitDialog"].Add(ERaidPadButton.A, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.SplitDialogAccept), ERaidPadPressType.Press, 50) });
            RaidPadSets["SplitDialog"].Add(ERaidPadButton.UP, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.SplitDialogAdd), ERaidPadPressType.Press, 50) });
            RaidPadSets["SplitDialog"][ERaidPadButton.UP].Add(new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.SplitDialogDisableAutoMove), ERaidPadPressType.Release, 50));
            RaidPadSets["SplitDialog"].Add(ERaidPadButton.DOWN, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.SplitDialogSubtract), ERaidPadPressType.Press, 50) });
            RaidPadSets["SplitDialog"][ERaidPadButton.DOWN].Add(new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.SplitDialogDisableAutoMove), ERaidPadPressType.Release, 50));
            RaidPadSets["SplitDialog"].Add(ERaidPadButton.LEFT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.SplitDialogSubtract), ERaidPadPressType.Press, 50) });
            RaidPadSets["SplitDialog"][ERaidPadButton.LEFT].Add(new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.SplitDialogDisableAutoMove), ERaidPadPressType.Release, 50));
            RaidPadSets["SplitDialog"].Add(ERaidPadButton.RIGHT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.SplitDialogAdd), ERaidPadPressType.Press, 50) });
            RaidPadSets["SplitDialog"][ERaidPadButton.RIGHT].Add(new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.SplitDialogDisableAutoMove), ERaidPadPressType.Release, 50));
            RaidPadSets["SplitDialog"].Add(ERaidPadButton.RS, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.None), ERaidPadPressType.Press, 50) });

            RaidPadButtonBinds.Clear();
            RaidPadButtonBinds.Add(ERaidPadButton.LT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.ToggleAlternativeShooting), new RaidPadCommand(ECommand.EndSprinting), new RaidPadCommand(ECommand.TryLowThrow) }, ERaidPadPressType.Press, -1) });
            RaidPadButtonBinds[ERaidPadButton.LT].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.FinishLowThrow), ERaidPadPressType.Release, -1));
            RaidPadButtonBinds.Add(ERaidPadButton.RT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.ToggleShooting), new RaidPadCommand(ECommand.EndSprinting), new RaidPadCommand(ECommand.TryHighThrow) }, ERaidPadPressType.Press, -1) });
            RaidPadButtonBinds[ERaidPadButton.RT].Add(new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.EndShooting), new RaidPadCommand(ECommand.FinishHighThrow) }, ERaidPadPressType.Release, -1));

            RaidPadButtonBinds.Add(ERaidPadButton.A, new List<RaidPadButtonBind> { new RaidPadButtonBind(new List<RaidPadCommand> { new RaidPadCommand(ECommand.Jump), new RaidPadCommand(ECommand.VaultingEnd) }, ERaidPadPressType.Release, -1) });
            RaidPadButtonBinds[ERaidPadButton.A].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.Vaulting), ERaidPadPressType.Press, -1));
            RaidPadButtonBinds.Add(ERaidPadButton.B, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ToggleDuck), ERaidPadPressType.Press, -1) });
            RaidPadButtonBinds[ERaidPadButton.B].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.ToggleProne), ERaidPadPressType.Hold, -1));
            RaidPadButtonBinds.Add(ERaidPadButton.X, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ReloadWeapon), ERaidPadPressType.Press, -1) });
            RaidPadButtonBinds[ERaidPadButton.X].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.CheckAmmo), ERaidPadPressType.Hold, -1));
            RaidPadButtonBinds[ERaidPadButton.X].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.QuickReloadWeapon), ERaidPadPressType.DoubleClick, -1));
            RaidPadButtonBinds.Add(ERaidPadButton.Y, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.QuickSelectWeapon), ERaidPadPressType.Press, -1) });
            RaidPadButtonBinds[ERaidPadButton.Y].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.ExamineWeapon), ERaidPadPressType.Hold, -1));
            RaidPadButtonBinds.Add(ERaidPadButton.LS, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ToggleSprinting), ERaidPadPressType.Press, -1) });
            RaidPadButtonBinds.Add(ERaidPadButton.RS, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.QuickKnifeKick), ERaidPadPressType.Press, -1) });
            RaidPadButtonBinds[ERaidPadButton.RS].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.SelectKnife), ERaidPadPressType.Hold, -1));

            RaidPadButtonBinds.Add(ERaidPadButton.UP, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.NextTacticalDevice), ERaidPadPressType.Press, -1) });
            RaidPadButtonBinds[ERaidPadButton.UP].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.ToggleTacticalDevice), ERaidPadPressType.Hold, -1));
            RaidPadButtonBinds.Add(ERaidPadButton.DOWN, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ChangeWeaponMode), ERaidPadPressType.Press, -1) });
            RaidPadButtonBinds[ERaidPadButton.DOWN].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.CheckFireMode), ERaidPadPressType.Hold, -1));
            RaidPadButtonBinds[ERaidPadButton.DOWN].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.ForceAutoWeaponMode), ERaidPadPressType.DoubleClick, -1));
            RaidPadButtonBinds.Add(ERaidPadButton.LEFT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ChangeScopeMagnification), ERaidPadPressType.Press, -1) });
            RaidPadButtonBinds[ERaidPadButton.LEFT].Add(new RaidPadButtonBind(new RaidPadCommand(ECommand.ChangeScope), ERaidPadPressType.Hold, -1));
            RaidPadButtonBinds.Add(ERaidPadButton.RIGHT, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ChangeScope), ERaidPadPressType.Press, -1) });
            RaidPadButtonBinds.Add(ERaidPadButton.BACK, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ECommand.ToggleInventory), ERaidPadPressType.Press, -1) });
            RaidPadButtonBinds.Add(ERaidPadButton.MENU, new List<RaidPadButtonBind> { new RaidPadButtonBind(new RaidPadCommand(ERaidPadCommand.MapPeek), ERaidPadPressType.Press, 0) });

            ControllerPresetJsonClass controllerPresetJsonClass = new ControllerPresetJsonClass();
            controllerPresetJsonClass.RaidPadButtonBinds = RaidPadButtonBinds;
            controllerPresetJsonClass.RaidPadSets = RaidPadSets;

            WriteToJsonFile<ControllerPresetJsonClass>((AppDomain.CurrentDomain.BaseDirectory + "/BepInEx/plugins/RaidPad/assets/Default.json"), controllerPresetJsonClass, false);
        }
        public void UpdateInterfaceBinds(bool Enabled)
        {
            Interface = Enabled;
            if (Enabled)
            {
                EnableSet("Interface");
            }
            else
            {
                DisableSet("Interface");
                DisableSet("Interface_LB");
                DisableSet("Interface_LB_RB");
                DisableSet("OnDrag");
                DisableSet("SearchButton");
            }
        }
        public void UpdateActionPanelBinds(bool Enabled)
        {
            if (Enabled)
            {
                EnableSet("ActionPanel");
            }
            else
            {
                DisableSet("ActionPanel");
            }
        }
        public void UpdateSplitDialogBinds(bool Enabled)
        {
            if (Enabled)
            {
                EnableSet("SplitDialog");
            }
            else
            {
                DisableSet("SplitDialog");
            }
        }
        public void UpdateContextMenuBinds(bool Enabled)
        {
            ContextMenu = Enabled;
            if (Enabled)
            {
                EnableSet("ContextMenu");
            }
            else
            {
                DisableSet("ContextMenu");
            }
        }
        public void UpdateInterface(InventoryScreen inventoryScreen)
        {
            this.inventoryScreen = inventoryScreen;
            if (AllGameObject != null && !InRaid)
            {
                Destroy(AllGameObject);
            }
            if (AllGameObject != null || !InRaid) return;


            AllGameObject = new GameObject("All");
            RectTransform AllRectTransform = AllGameObject.AddComponent<RectTransform>();
            AllRectTransform.SetParent(inventoryScreen.transform);
            AllRectTransform.pivot = new Vector2(1f,0f);
            AllRectTransform.position = new Vector2(Screen.width, 0f) + RaidPadPlugin.BlockPosition.Value;
            AllRectTransform.sizeDelta = new Vector2(2560f, RaidPadPlugin.BlockSize.Value.y);
            HorizontalLayoutGroup AllHorizontalLayoutGroup = AllGameObject.AddComponent<HorizontalLayoutGroup>();
            AllHorizontalLayoutGroup.childForceExpandHeight = false;
            AllHorizontalLayoutGroup.childForceExpandWidth = false;
            AllHorizontalLayoutGroup.childAlignment = TextAnchor.MiddleRight;
            AllHorizontalLayoutGroup.spacing = RaidPadPlugin.BlockSpacing.Value;

            ButtonBlocks.Clear();

            ButtonBlocks.Add(ERaidPadButton.Y, new GameObject("Y").AddComponent<RaidPadButtonBlock>());
            ButtonBlocks[ERaidPadButton.Y].transform.SetParent(AllRectTransform);
            ButtonBlocks[ERaidPadButton.Y].Button = ERaidPadButton.Y;

            ButtonBlocks.Add(ERaidPadButton.X, new GameObject("X").AddComponent<RaidPadButtonBlock>());
            ButtonBlocks[ERaidPadButton.X].transform.SetParent(AllRectTransform);
            ButtonBlocks[ERaidPadButton.X].Button = ERaidPadButton.X;

            //ButtonBlocks.Add(ERaidPadButton.B, new GameObject("B").AddComponent<RaidPadButtonBlock>());
            //ButtonBlocks[ERaidPadButton.B].transform.SetParent(AllRectTransform);
            //ButtonBlocks[ERaidPadButton.B].Button = ERaidPadButton.B;

            ButtonBlocks.Add(ERaidPadButton.A, new GameObject("A").AddComponent<RaidPadButtonBlock>());
            ButtonBlocks[ERaidPadButton.A].transform.SetParent(AllRectTransform);
            ButtonBlocks[ERaidPadButton.A].Button = ERaidPadButton.A;

        }

        public void GeneratePressType(ERaidPadButton Button, bool Pressed)
        {
            onRaidPadButtonState(Button, Pressed);
            if (RaidPadButtonSnapshots.ContainsKey(Button))
            {
                RaidPadButtonSnapshot RaidPadButtonSnapshot = RaidPadButtonSnapshots[Button];
                if (Pressed)
                {
                    if (RaidPadButtonSnapshot.DoubleClickBind.Priority != -100 && Time.time - RaidPadButtonSnapshot.Time <= RaidPadPlugin.DoubleClickDelay.Value)
                    {
                        RaidPadButton(RaidPadButtonSnapshot.DoubleClickBind);
                    }
                    AsyncHold.Remove(Button.ToString() + RaidPadButtonSnapshot.Time.ToString());
                    AsyncPress.Remove(Button.ToString() + RaidPadButtonSnapshot.Time.ToString());
                    RaidPadButtonSnapshots.Remove(Button);
                }
                else
                {
                    // Temp
                    if (RaidPadButtonSnapshot.ReleaseBind.Priority != -100)
                    {
                        RaidPadButton(RaidPadButtonSnapshot.ReleaseBind);
                        RaidPadButtonSnapshots.Remove(Button);
                    }
                    else
                    {
                        // Temp
                        if (RaidPadButtonSnapshot.HoldBind.Priority == -100 && RaidPadButtonSnapshot.DoubleClickBind.Priority == -100)
                        {
                            if (RaidPadButtonSnapshot.ReleaseBind.Priority != -100)
                            {
                                RaidPadButton(RaidPadButtonSnapshot.ReleaseBind);
                            }
                            RaidPadButtonSnapshots.Remove(Button);
                        }
                        else if (RaidPadButtonSnapshot.HoldBind.Priority != -100 || RaidPadButtonSnapshot.DoubleClickBind.Priority != -100)
                        {
                            AsyncHold.Remove(Button.ToString() + RaidPadButtonSnapshot.Time.ToString());
                        }
                        if (RaidPadButtonSnapshot.DoubleClickBind.Priority == -100 && RaidPadButtonSnapshot.ReleaseBind.Priority == -100)
                        {
                            AsyncPress.Remove(Button.ToString() + RaidPadButtonSnapshot.Time.ToString());
                            RaidPadButton(RaidPadButtonSnapshot.PressBind);
                            RaidPadButtonSnapshots.Remove(Button);
                        }
                    }
                }
            }
            else if (Pressed)
            {
                float time = Time.time;
                RaidPadButtonBind[] Binds = GetPriorityButtonBinds(Button);
                // Temp
                if (Binds[1].Priority != -100)
                {
                    RaidPadButtonSnapshots.Add(Button, new RaidPadButtonSnapshot(true, time, Binds[0], Binds[1], Binds[2], Binds[3]));
                    RaidPadButton(Binds[0]);
                }
                else
                {
                    // Temp
                    if (Binds[2].Priority == -100 && Binds[3].Priority == -100)
                    {
                        RaidPadButton(Binds[0]);
                    }
                    else if (Binds[2].Priority != -100 || Binds[3].Priority != -100)
                    {
                        ButtonTimer(Button.ToString() + time.ToString(), Button);
                    }
                    if (Binds[2].Priority != -100 || Binds[3].Priority != -100)
                    {
                        RaidPadButtonSnapshots.Add(Button, new RaidPadButtonSnapshot(true, time, Binds[0], Binds[1], Binds[2], Binds[3]));
                    }
                }
            }
        }
        public RaidPadButtonBind[] GetPriorityButtonBinds(ERaidPadButton Button)
        {
            RaidPadButtonBind PressBind = EmptyBind;
            RaidPadButtonBind ReleaseBind = EmptyBind;
            RaidPadButtonBind HoldBind = EmptyBind;
            RaidPadButtonBind DoubleClickBind = EmptyBind;

            int SetPriority = -69;
            string PrioritySet = "";

            foreach (string Set in ActiveRaidPadSets)
            {
                if (RaidPadSets[Set].ContainsKey(Button))
                {
                    foreach (RaidPadButtonBind Bind in RaidPadSets[Set][Button])
                    {
                        if (Bind.Priority > SetPriority)
                        {
                            SetPriority = Bind.Priority;
                            PrioritySet = Set;
                        }
                    }
                }
            }
            if (PrioritySet != "")
            {
                foreach (RaidPadButtonBind Bind in RaidPadSets[PrioritySet][Button])
                {
                    switch (Bind.PressType)
                    {
                        case ERaidPadPressType.Press:
                            PressBind = Bind;
                            break;
                        case ERaidPadPressType.Release:
                            ReleaseBind = Bind;
                            break;
                        case ERaidPadPressType.Hold:
                            HoldBind = Bind;
                            break;
                        case ERaidPadPressType.DoubleClick:
                            DoubleClickBind = Bind;
                            break;
                    }
                }
            }
            else
            {
                if (RaidPadButtonBinds.ContainsKey(Button))
                {
                    foreach (RaidPadButtonBind Bind in RaidPadButtonBinds[Button])
                    {
                        switch (Bind.PressType)
                        {
                            case ERaidPadPressType.Press:
                                PressBind = Bind;
                                break;
                            case ERaidPadPressType.Release:
                                ReleaseBind = Bind;
                                break;
                            case ERaidPadPressType.Hold:
                                HoldBind = Bind;
                                break;
                            case ERaidPadPressType.DoubleClick:
                                DoubleClickBind = Bind;
                                break;
                        }
                    }
                }
            }
            return new RaidPadButtonBind[4] { PressBind, ReleaseBind, HoldBind, DoubleClickBind };
        }
        public void RaidPadButton(RaidPadButtonBind Bind)
        {
            List<ECommand> Commands = new List<ECommand>();
            foreach (RaidPadCommand RaidPadCommand in Bind.RaidPadCommands)
            {
                if (RaidPadCommand.Command == ERaidPadCommand.None) continue;
                switch (RaidPadCommand.Command)
                {
                    case ERaidPadCommand.ToggleSet:
                        ToggleSet(RaidPadCommand.RaidPadSet);
                        break;
                    case ERaidPadCommand.EnableSet:
                        EnableSet(RaidPadCommand.RaidPadSet);
                        break;
                    case ERaidPadCommand.DisableSet:
                        DisableSet(RaidPadCommand.RaidPadSet);
                        break;
                    case ERaidPadCommand.InputTree:
                        if (inputTree != null)
                        {
                            Commands.Add(RaidPadCommand.InputTree);
                        }
                        break;
                    case ERaidPadCommand.QuickSelectWeapon:
                        break;
                    case ERaidPadCommand.SlowLeanLeft:
                        SlowLeanLeft = true;
                        break;
                    case ERaidPadCommand.SlowLeanRight:
                        SlowLeanRight = true;
                        break;
                    case ERaidPadCommand.EndSlowLean:
                        SlowLeanLeft = false;
                        SlowLeanRight = false;
                        break;
                    case ERaidPadCommand.RestoreLean:
                        if (inputTree != null)
                        {
                            Commands.Add(ECommand.EndLeanLeft);
                            Commands.Add(ECommand.EndLeanRight);
                        }
                        break;
                    case ERaidPadCommand.InterfaceUp:
                        ControllerUIMove(new Vector2Int(0, 1), false);
                        AutoMove = true;
                        AutoMoveTime = 0f;
                        break;
                    case ERaidPadCommand.InterfaceDown:
                        ControllerUIMove(new Vector2Int(0, -1), false);
                        AutoMove = true;
                        AutoMoveTime = 0f;
                        break;
                    case ERaidPadCommand.InterfaceLeft:
                        ControllerUIMove(new Vector2Int(-1, 0), false);
                        AutoMove = true;
                        AutoMoveTime = 0f;
                        break;
                    case ERaidPadCommand.InterfaceRight:
                        ControllerUIMove(new Vector2Int(1, 0), false);
                        AutoMove = true;
                        AutoMoveTime = 0f;
                        break;
                    case ERaidPadCommand.InterfaceDisableAutoMove:
                        AutoMove = false;
                        break;
                    case ERaidPadCommand.BeginDrag:
                        ControllerBeginDrag();
                        break;
                    case ERaidPadCommand.EndDrag:
                        ControllerEndDrag();
                        break;
                    case ERaidPadCommand.RotateDragged:
                        ControllerRotateDragged();
                        break;
                    case ERaidPadCommand.SplitDragged:
                        ControllerSplitDragged();
                        break;
                    case ERaidPadCommand.CancelDrag:
                        ControllerCancelDrag();
                        break;
                    case ERaidPadCommand.Search:
                        ControllerSearch();
                        break;
                    case ERaidPadCommand.Use:
                        ControllerUse(false);
                        break;
                    case ERaidPadCommand.UseHold:
                        ControllerUse(true);
                        break;
                    case ERaidPadCommand.QuickMove:
                        ControllerQuickMove();
                        break;
                    case ERaidPadCommand.Discard:
                        ControllerDiscard();
                        break;
                    case ERaidPadCommand.InterfaceBind4:
                        ControllerInterfaceBind(EBoundItem.Item4);
                        break;
                    case ERaidPadCommand.InterfaceBind5:
                        ControllerInterfaceBind(EBoundItem.Item5);
                        break;
                    case ERaidPadCommand.InterfaceBind6:
                        ControllerInterfaceBind(EBoundItem.Item6);
                        break;
                    case ERaidPadCommand.InterfaceBind7:
                        ControllerInterfaceBind(EBoundItem.Item7);
                        break;
                    case ERaidPadCommand.InterfaceBind8:
                        ControllerInterfaceBind(EBoundItem.Item8);
                        break;
                    case ERaidPadCommand.InterfaceBind9:
                        ControllerInterfaceBind(EBoundItem.Item9);
                        break;
                    case ERaidPadCommand.InterfaceBind10:
                        ControllerInterfaceBind(EBoundItem.Item10);
                        break;
                    case ERaidPadCommand.ShowContextMenu:
                        ControllerShowContextMenu();
                        break;
                    case ERaidPadCommand.ContextMenuUse:
                        ControllerContextMenuUse();
                        break;
                    case ERaidPadCommand.SplitDialogAccept:
                        ControllerSplitDialogAccept();
                        break;
                    case ERaidPadCommand.SplitDialogAdd:
                        ControllerSplitDialogAdd(1);
                        break;
                    case ERaidPadCommand.SplitDialogSubtract:
                        ControllerSplitDialogAdd(-1);
                        break;
                    case ERaidPadCommand.SplitDialogDisableAutoMove:
                        SplitDialogAutoMove = false;
                        break;
                    case ERaidPadCommand.PreviousTab:
                        ControllerPreviousTab();
                        break;
                    case ERaidPadCommand.NextTab:
                        ControllerNextTab();
                        break;
                    case ERaidPadCommand.PauseRaid:
                        RaidPadPauseBridge.Toggle();
                        break;
                    case ERaidPadCommand.MapPeek:
                        RaidPadDynamicMapsBridge.TogglePeek();
                        break;
                    case ERaidPadCommand.MapPanUp:
                        RaidPadDynamicMapsBridge.Pan(0f, 1f);
                        break;
                    case ERaidPadCommand.MapPanDown:
                        RaidPadDynamicMapsBridge.Pan(0f, -1f);
                        break;
                    case ERaidPadCommand.MapPanLeft:
                        RaidPadDynamicMapsBridge.Pan(-1f, 0f);
                        break;
                    case ERaidPadCommand.MapPanRight:
                        RaidPadDynamicMapsBridge.Pan(1f, 0f);
                        break;
                    case ERaidPadCommand.MapZoomIn:
                        RaidPadDynamicMapsBridge.Zoom(1);
                        break;
                    case ERaidPadCommand.MapZoomOut:
                        RaidPadDynamicMapsBridge.Zoom(-1);
                        break;
                    case ERaidPadCommand.MapCenter:
                        RaidPadDynamicMapsBridge.CenterOnPlayer();
                        break;
                    case ERaidPadCommand.MapLevelUp:
                        RaidPadDynamicMapsBridge.ChangeLevel(1);
                        break;
                    case ERaidPadCommand.MapLevelDown:
                        RaidPadDynamicMapsBridge.ChangeLevel(-1);
                        break;
                }
            }
            if (Commands.Count != 0 && inputTree != null)
            {
                TranslateInputInvokeParameters[0] = Commands;
                try
                {
                    TranslateInput.Invoke(inputTree, TranslateInputInvokeParameters);
                }
                catch (Exception ex)
                {
                    RaidPadPlugin.Log.LogError($"RaidPad: command dispatch failed for [{string.Join(",", Commands)}]: {ex}");
                }
            }
        }
        public void ToggleSet(string RaidPadSet)
        {
            if (ActiveRaidPadSets.Contains(RaidPadSet))
            {
                ActiveRaidPadSets.Remove(RaidPadSet);
                LSRSButtonsCheck();
                ControllerSetState(RaidPadSet, false);
            }
            else if (RaidPadSets.ContainsKey(RaidPadSet))
            {
                ActiveRaidPadSets.Add(RaidPadSet);
                LSRSButtonsCheck();
                ControllerSetState(RaidPadSet, true);
            }
        }
        public void EnableSet(string RaidPadSet)
        {
            if (RaidPadSets.ContainsKey(RaidPadSet) && !ActiveRaidPadSets.Contains(RaidPadSet))
            {
                ActiveRaidPadSets.Add(RaidPadSet);
                LSRSButtonsCheck();
                ControllerSetState(RaidPadSet, true);
            }
        }
        public void DisableSet(string RaidPadSet)
        {
            ActiveRaidPadSets.Remove(RaidPadSet);
            LSRSButtonsCheck();
            ControllerSetState(RaidPadSet, false);
        }
        private async void ButtonTimer(string Token, ERaidPadButton Button)
        {
            AsyncPress.Add(Token);
            AsyncHold.Add(Token);
            await Task.Delay((int)(Interface ? RaidPadPlugin.HoldDelay.Value * 800 : RaidPadPlugin.HoldDelay.Value * 1000));
            if (AsyncHold.Contains(Token))
            {
                RaidPadButton(RaidPadButtonSnapshots[Button].HoldBind);
                AsyncHold.Remove(Token);
                RaidPadButtonSnapshots.Remove(Button);
            }
            else if (AsyncPress.Contains(Token))
            {
                RaidPadButton(RaidPadButtonSnapshots[Button].PressBind);
                AsyncPress.Remove(Token);
                RaidPadButtonSnapshots.Remove(Button);
            }
        }
        private void LSRSButtonsCheck()
        {
            LSButtons = false;
            RSButtons = false;
            foreach (string ActiveSet in ActiveRaidPadSets)
            {
                if (RaidPadSets[ActiveSet].ContainsKey(ERaidPadButton.LSUP) || RaidPadSets[ActiveSet].ContainsKey(ERaidPadButton.LSDOWN) || RaidPadSets[ActiveSet].ContainsKey(ERaidPadButton.LSLEFT) || RaidPadSets[ActiveSet].ContainsKey(ERaidPadButton.LSRIGHT)) LSButtons = true;
                if (RaidPadSets[ActiveSet].ContainsKey(ERaidPadButton.RSUP) || RaidPadSets[ActiveSet].ContainsKey(ERaidPadButton.RSDOWN) || RaidPadSets[ActiveSet].ContainsKey(ERaidPadButton.RSLEFT) || RaidPadSets[ActiveSet].ContainsKey(ERaidPadButton.RSRIGHT)) RSButtons = true;
            }
            if (!LSButtons)
            {
                if (LSUP)
                {
                    LSUP = false;
                    GeneratePressType(ERaidPadButton.LSUP, false);
                }
                if (LSDOWN)
                {
                    LSDOWN = false;
                    GeneratePressType(ERaidPadButton.LSRIGHT, false);
                }
                if (LSLEFT)
                {
                    LSLEFT = false;
                    GeneratePressType(ERaidPadButton.LSLEFT, false);
                }
                if (LSRIGHT)
                {
                    LSRIGHT = false;
                    GeneratePressType(ERaidPadButton.LSRIGHT, false);
                }
            }
            if (!RSButtons)
            {
                if (RSUP)
                {
                    RSUP = false;
                    GeneratePressType(ERaidPadButton.RSUP, false);
                }
                if (RSDOWN)
                {
                    RSDOWN = false;
                    GeneratePressType(ERaidPadButton.RSRIGHT, false);
                }
                if (RSLEFT)
                {
                    RSLEFT = false;
                    GeneratePressType(ERaidPadButton.RSLEFT, false);
                }
                if (RSRIGHT)
                {
                    RSRIGHT = false;
                    GeneratePressType(ERaidPadButton.RSRIGHT, false);
                }
            }
        }

        // UI Navigation
        private void ResetAllCurrent()
        {
            currentGridView = null;
            currentModSlotView = null;
            currentTradingTableGridView = null;
            currentContainedGridsView = null;
            currentItemSpecificationPanel = null;
            currentEquipmentSlotView = null;
            currentWeaponsSlotView = null;
            currentArmbandSlotView = null;
            currentContainersSlotView = null;
            currentDogtagSlotView = null;
            currentSpecialSlotSlotView = null;
            currentSearchButton = null;
            currentContextMenuButton = null;
        }
        private bool FindGridView(Vector2 Position)
        {
            RectTransform rectTransform;
            // GridViews Window stuff inside needs to be out
            if (containedGridsViews.Count != 0)
            {
                Vector2 position;
                int bestDepth = -1;
                CanvasRenderer canvasRenderer;
                ContainedGridsView bestContainedGridsView = null;
                foreach (ContainedGridsView containedGridsView in containedGridsViews)
                {
                    if (containedGridsView == null || containedGridsView == currentContainedGridsView) continue;
                    rectTransform = containedGridsView.GetComponent<RectTransform>();
                    position = new Vector2(containedGridsView.transform.position.x, containedGridsView.transform.position.y - (rectTransform.sizeDelta.y * (rectTransform.pivot.y - 1f) * ScreenRatio));
                    if (Position.x > position.x && Position.x < (position.x + (rectTransform.sizeDelta.x * ScreenRatio)) && Position.y < position.y && Position.y > (position.y - (rectTransform.sizeDelta.y * ScreenRatio)))
                    {
                        canvasRenderer = containedGridsView.GetComponent<CanvasRenderer>();
                        if (canvasRenderer != null && canvasRenderer.absoluteDepth > bestDepth)
                        {
                            bestDepth = canvasRenderer.absoluteDepth;
                            bestContainedGridsView = containedGridsView;
                        }
                    }
                }
                if (bestContainedGridsView != null)
                {
                    rectTransform = bestContainedGridsView.GetComponent<RectTransform>();
                    int GridWidth;
                    int GridHeight;

                    float distance;
                    float bestDistance = 999999f;

                    GridView bestGridView = null;
                    Vector2Int bestGridViewLocation = Vector2Int.zero;

                    foreach (GridView gridView in bestContainedGridsView.GridViews)
                    {
                        GridWidth = gridView.Grid.GridWidth;
                        GridHeight = gridView.Grid.GridHeight;

                        if (GridWidth == 1 && GridHeight == 1)
                        {
                            position.x = GridSize / 2f;
                            position.y = -GridSize / 2f;
                        }
                        else if (GridWidth == 1)
                        {
                            position.x = GridSize / 2f;
                            position.y = -Mathf.Clamp(gridView.transform.position.y - Position.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                        }
                        else if (GridHeight == 1)
                        {
                            position.x = Mathf.Clamp(Position.x - gridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                            position.y = -GridSize / 2f;
                        }
                        else
                        {
                            position.x = Mathf.Clamp(Position.x - gridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                            position.y = -Mathf.Clamp(gridView.transform.position.y - Position.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                        }

                        distance = Vector2.Distance(Position, (Vector2)gridView.transform.position + position);

                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestGridView = gridView;
                            bestGridViewLocation = new Vector2Int(Mathf.RoundToInt((position.x + (GridSize / 2f)) / GridSize), -Mathf.RoundToInt((position.y - (GridSize / 2f)) / GridSize));
                        }
                    }
                    if (bestGridView != null)
                    {
                        ResetAllCurrent();
                        currentGridView = bestGridView;
                        currentContainedGridsView = bestContainedGridsView;
                        gridViewLocation = bestGridViewLocation;
                        globalPosition.x = bestGridView.transform.position.x + (GridSize * gridViewLocation.x) - (GridSize / 2f);
                        globalPosition.y = bestGridView.transform.position.y - (GridSize * gridViewLocation.y) + (GridSize / 2f);
                        return true;
                    }
                }
            }
            // Support SlotViews
            if (itemSpecificationPanels.Count != 0)
            {
                Vector2 position;
                int bestDepth = -1;
                CanvasRenderer canvasRenderer;
                ItemSpecificationPanel bestItemSpecificationPanel = null;
                if (currentItemSpecificationPanel != null)
                {
                    canvasRenderer = currentItemSpecificationPanel.GetComponent<CanvasRenderer>();
                    bestDepth = canvasRenderer.absoluteDepth;
                }
                foreach (ItemSpecificationPanel itemSpecificationPanel in itemSpecificationPanels)
                {
                    if (itemSpecificationPanel == null || itemSpecificationPanel == currentItemSpecificationPanel) continue;
                    rectTransform = itemSpecificationPanel.GetComponent<RectTransform>();
                    position = new Vector2(itemSpecificationPanel.transform.position.x - ((rectTransform.sizeDelta.x / 2) * ScreenRatio), itemSpecificationPanel.transform.position.y + ((rectTransform.sizeDelta.y / 2) * ScreenRatio));
                    if (Position.x > position.x && Position.x < (position.x + (rectTransform.sizeDelta.x * ScreenRatio)) && Position.y < position.y && Position.y > (position.y - (rectTransform.sizeDelta.y * ScreenRatio)))
                    {
                        canvasRenderer = itemSpecificationPanel.GetComponent<CanvasRenderer>();
                        if (canvasRenderer != null && canvasRenderer.absoluteDepth > bestDepth)
                        {
                            bestDepth = canvasRenderer.absoluteDepth;
                            bestItemSpecificationPanel = itemSpecificationPanel;
                        }
                    }
                }
                if (bestItemSpecificationPanel != null)
                {
                    rectTransform = bestItemSpecificationPanel.GetComponent<RectTransform>();

                    float distance;
                    float bestDistance = 999999f;

                    ModSlotView bestModSlotView = null;

                    foreach (ModSlotView modSlotView in Traverse.Create(bestItemSpecificationPanel).Field("_modsContainer").GetValue<RectTransform>().GetComponentsInChildren<ModSlotView>())
                    {
                        distance = Vector2.Distance(Position, (Vector2)modSlotView.transform.position);

                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestModSlotView = modSlotView;
                        }
                    }
                    if (bestModSlotView != null)
                    {
                        ResetAllCurrent();
                        currentModSlotView = bestModSlotView;
                        currentItemSpecificationPanel = bestItemSpecificationPanel;
                        gridViewLocation = new Vector2Int(1, 1);
                        globalPosition.x = currentModSlotView.transform.position.x;
                        globalPosition.y = currentModSlotView.transform.position.y;
                        return true;
                    }
                }
            }

            // find gridviews 0 depth
            if (currentContainedGridsView != null || currentItemSpecificationPanel != null)
            {
                foreach (GridView gridView in gridViews)
                {
                    rectTransform = gridView.GetComponent<RectTransform>();
                    if (Position.x > gridView.transform.position.x && Position.x < (gridView.transform.position.x + (rectTransform.sizeDelta.x * ScreenRatio)) && Position.y < gridView.transform.position.y && Position.y > (gridView.transform.position.y - (rectTransform.sizeDelta.y * ScreenRatio)))
                    {
                        Vector2 position;
                        int GridWidth = gridView.Grid.GridWidth;
                        int GridHeight = gridView.Grid.GridHeight;

                        if (GridWidth == 1 && GridHeight == 1)
                        {
                            position.x = GridSize / 2f;
                            position.y = -GridSize / 2f;
                        }
                        else if (GridWidth == 1)
                        {
                            position.x = GridSize / 2f;
                            position.y = -Mathf.Clamp(gridView.transform.position.y - Position.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                        }
                        else if (GridHeight == 1)
                        {
                            position.x = Mathf.Clamp(Position.x - gridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                            position.y = -GridSize / 2f;
                        }
                        else
                        {
                            position.x = Mathf.Clamp(Position.x - gridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                            position.y = -Mathf.Clamp(gridView.transform.position.y - Position.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                        }
                        ResetAllCurrent();
                        currentGridView = gridView;
                        gridViewLocation = new Vector2Int(Mathf.RoundToInt((position.x + (GridSize / 2f)) / GridSize), -Mathf.RoundToInt((position.y - (GridSize / 2f)) / GridSize));
                        globalPosition.x = gridView.transform.position.x + (GridSize * gridViewLocation.x) - (GridSize / 2f);
                        globalPosition.y = gridView.transform.position.y - (GridSize * gridViewLocation.y) + (GridSize / 2f);
                        return true;
                    }
                }
            }
            // find tradingtablegridview 0 depth
            if (currentContainedGridsView != null && tradingTableGridView != null)
            {
                rectTransform = tradingTableGridView.GetComponent<RectTransform>();
                Vector2 size = rectTransform.sizeDelta * ScreenRatio;
                Vector2 position = new Vector2(tradingTableGridView.transform.position.x - (size.x / 2f), tradingTableGridView.transform.position.y + (size.y / 2f));
                if (Position.x > position.x && Position.x < (position.x + size.x) && Position.y < position.y && Position.y > (position.y - size.y))
                {
                    int GridWidth = tradingTableGridView.Grid.GridWidth;
                    int GridHeight = tradingTableGridView.Grid.GridHeight;

                    if (GridWidth == 1 && GridHeight == 1)
                    {
                        position.x = GridSize / 2f;
                        position.y = -GridSize / 2f;
                    }
                    else if (GridWidth == 1)
                    {
                        position.x = GridSize / 2f;
                        position.y = -Mathf.Clamp(position.y - Position.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                    }
                    else if (GridHeight == 1)
                    {
                        position.x = Mathf.Clamp(Position.x - position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                        position.y = -GridSize / 2f;
                    }
                    else
                    {
                        position.x = Mathf.Clamp(Position.x - position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                        position.y = -Mathf.Clamp(position.y - Position.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                    }
                    ResetAllCurrent();
                    currentTradingTableGridView = tradingTableGridView;
                    gridViewLocation = new Vector2Int(Mathf.RoundToInt((position.x + (GridSize / 2f)) / GridSize), -Mathf.RoundToInt((position.y - (GridSize / 2f)) / GridSize));
                    size = currentTradingTableGridView.GetComponent<RectTransform>().sizeDelta * ScreenRatio;
                    globalPosition.x = (currentTradingTableGridView.transform.position.x - (size.x / 2f)) + (GridSize * gridViewLocation.x) - (GridSize / 2f);
                    globalPosition.y = (currentTradingTableGridView.transform.position.y + (size.y / 2f)) - (GridSize * gridViewLocation.y) + (GridSize / 2f);
                    return true;
                }
            }

            // find equipmentSlotViews 0 depth
            if (currentContainedGridsView != null)
            {
                foreach (SlotView slotView in equipmentSlotViews)
                {
                    if (Position.x > slotView.transform.position.x && Position.x < slotView.transform.position.x + SlotSize && Position.y < slotView.transform.position.y && Position.y > (slotView.transform.position.y - SlotSize))
                    {
                        ResetAllCurrent();
                        currentEquipmentSlotView = slotView;
                        gridViewLocation = new Vector2Int(1, 1);
                        globalPosition.x = currentEquipmentSlotView.transform.position.x + (SlotSize / 2f);
                        globalPosition.y = currentEquipmentSlotView.transform.position.y - (SlotSize / 2f);
                        return true;
                    }
                }
            }
            // find weaponsSlotViews 0 depth
            if (currentContainedGridsView != null)
            {
                foreach (SlotView slotView in weaponsSlotViews)
                {
                    if (Position.x > slotView.transform.position.x && Position.x < slotView.transform.position.x + (314.1622f * ScreenRatio) && Position.y < slotView.transform.position.y && Position.y > (slotView.transform.position.y - SlotSize))
                    {
                        ResetAllCurrent();
                        currentWeaponsSlotView = slotView;
                        gridViewLocation = new Vector2Int(1, 1);
                        globalPosition.x = currentWeaponsSlotView.transform.position.x + (157.0811f * ScreenRatio);
                        globalPosition.y = currentWeaponsSlotView.transform.position.y - (SlotSize / 2f);
                        return true;
                    }
                }
            }
            // find armbandSlotView 0 depth
            if (currentContainedGridsView != null)
            {
                if (armbandSlotView != null)
                {
                    if (Position.x > armbandSlotView.transform.position.x && Position.x < armbandSlotView.transform.position.x + SlotSize && Position.y < armbandSlotView.transform.position.y && Position.y > (armbandSlotView.transform.position.y - (64f * ScreenRatio)))
                    {
                        ResetAllCurrent();
                        currentArmbandSlotView = armbandSlotView;
                        gridViewLocation = new Vector2Int(1, 1);
                        globalPosition.x = currentArmbandSlotView.transform.position.x + (SlotSize / 2f);
                        globalPosition.y = currentArmbandSlotView.transform.position.y - (32f * ScreenRatio);
                        return true;
                    }
                }
            }
            // find containersSlotViews 0 depth
            if (currentContainedGridsView != null)
            {
                foreach (SlotView slotView in containersSlotViews)
                {
                    if (Position.x > slotView.transform.position.x && Position.x < slotView.transform.position.x + SlotSize && Position.y < slotView.transform.position.y && Position.y > (slotView.transform.position.y - SlotSize))
                    {
                        ResetAllCurrent();
                        currentContainersSlotView = slotView;
                        gridViewLocation = new Vector2Int(1, 1);
                        globalPosition.x = currentContainersSlotView.transform.position.x + (SlotSize / 2f);
                        globalPosition.y = currentContainersSlotView.transform.position.y - (SlotSize / 2f);
                        return true;
                    }
                }
            }

            // find lootEquipmentSlotViews 0 depth
            if (currentContainedGridsView != null)
            {
                foreach (SlotView slotView in lootEquipmentSlotViews)
                {
                    if (Position.x > slotView.transform.position.x && Position.x < slotView.transform.position.x + SlotSize && Position.y < slotView.transform.position.y && Position.y > (slotView.transform.position.y - SlotSize))
                    {
                        ResetAllCurrent();
                        currentEquipmentSlotView = slotView;
                        gridViewLocation = new Vector2Int(1, 1);
                        globalPosition.x = currentEquipmentSlotView.transform.position.x + (SlotSize / 2f);
                        globalPosition.y = currentEquipmentSlotView.transform.position.y - (SlotSize / 2f);
                        return true;
                    }
                }
            }
            // find lootWeaponsSlotViews 0 depth
            if (currentContainedGridsView != null)
            {
                foreach (SlotView slotView in lootWeaponsSlotViews)
                {
                    if (Position.x > slotView.transform.position.x && Position.x < slotView.transform.position.x + (314.1622f * ScreenRatio) && Position.y < slotView.transform.position.y && Position.y > (slotView.transform.position.y - SlotSize))
                    {
                        ResetAllCurrent();
                        currentWeaponsSlotView = slotView;
                        gridViewLocation = new Vector2Int(1, 1);
                        globalPosition.x = currentWeaponsSlotView.transform.position.x + (157.0811f * ScreenRatio);
                        globalPosition.y = currentWeaponsSlotView.transform.position.y - (SlotSize / 2f);
                        return true;
                    }
                }
            }
            // find lootArmbandSlotView 0 depth
            if (currentContainedGridsView != null)
            {
                if (lootArmbandSlotView != null)
                {
                    if (Position.x > lootArmbandSlotView.transform.position.x && Position.x < lootArmbandSlotView.transform.position.x + SlotSize && Position.y < lootArmbandSlotView.transform.position.y && Position.y > (lootArmbandSlotView.transform.position.y - (64f * ScreenRatio)))
                    {
                        ResetAllCurrent();
                        currentArmbandSlotView = lootArmbandSlotView;
                        gridViewLocation = new Vector2Int(1, 1);
                        globalPosition.x = currentArmbandSlotView.transform.position.x + (SlotSize / 2f);
                        globalPosition.y = currentArmbandSlotView.transform.position.y - (32f * ScreenRatio);
                        return true;
                    }
                }
            }
            // find lootContainersSlotViews 0 depth
            if (currentContainedGridsView != null)
            {
                foreach (SlotView slotView in lootContainersSlotViews)
                {
                    if (Position.x > slotView.transform.position.x && Position.x < slotView.transform.position.x + SlotSize && Position.y < slotView.transform.position.y && Position.y > (slotView.transform.position.y - SlotSize))
                    {
                        ResetAllCurrent();
                        currentContainersSlotView = slotView;
                        gridViewLocation = new Vector2Int(1, 1);
                        globalPosition.x = currentContainersSlotView.transform.position.x + (SlotSize / 2f);
                        globalPosition.y = currentContainersSlotView.transform.position.y - (SlotSize / 2f);
                        return true;
                    }
                }
            }
            return false;
        }
        private bool FindGridWindow(Vector2 Position)
        {
            RectTransform rectTransform;

            if (containedGridsViews.Count != 0)
            {
                Vector2 position;
                int bestDepth = -1;
                CanvasRenderer canvasRenderer;
                ContainedGridsView bestContainedGridsView = null;
                if (currentContainedGridsView != null)
                {
                    canvasRenderer = currentContainedGridsView.GetComponent<CanvasRenderer>();
                    bestDepth = canvasRenderer.absoluteDepth;
                }
                foreach (ContainedGridsView containedGridsView in containedGridsViews)
                {
                    if (containedGridsView == null || containedGridsView == currentContainedGridsView) continue;
                    rectTransform = containedGridsView.GetComponent<RectTransform>();
                    position = new Vector2(containedGridsView.transform.position.x, containedGridsView.transform.position.y - (rectTransform.sizeDelta.y * (rectTransform.pivot.y - 1f) * ScreenRatio));
                    if (Position.x > position.x && Position.x < (position.x + (rectTransform.sizeDelta.x * ScreenRatio)) && Position.y < position.y && Position.y > (position.y - (rectTransform.sizeDelta.y * ScreenRatio)))
                    {
                        canvasRenderer = containedGridsView.GetComponent<CanvasRenderer>();
                        if (canvasRenderer != null && canvasRenderer.absoluteDepth > bestDepth)
                        {
                            bestDepth = canvasRenderer.absoluteDepth;
                            bestContainedGridsView = containedGridsView;
                        }
                    }
                }
                if (bestContainedGridsView != null)
                {
                    rectTransform = bestContainedGridsView.GetComponent<RectTransform>();
                    int GridWidth;
                    int GridHeight;

                    float distance;
                    float bestDistance = 999999f;

                    GridView bestGridView = null;
                    Vector2Int bestGridViewLocation = Vector2Int.zero;

                    foreach (GridView gridView in bestContainedGridsView.GridViews)
                    {
                        GridWidth = gridView.Grid.GridWidth;
                        GridHeight = gridView.Grid.GridHeight;

                        if (GridWidth == 1 && GridHeight == 1)
                        {
                            position.x = GridSize / 2f;
                            position.y = -GridSize / 2f;
                        }
                        else if (GridWidth == 1)
                        {
                            position.x = GridSize / 2f;
                            position.y = -Mathf.Clamp(gridView.transform.position.y - Position.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                        }
                        else if (GridHeight == 1)
                        {
                            position.x = Mathf.Clamp(Position.x - gridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                            position.y = -GridSize / 2f;
                        }
                        else
                        {
                            position.x = Mathf.Clamp(Position.x - gridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                            position.y = -Mathf.Clamp(gridView.transform.position.y - Position.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                        }

                        distance = Vector2.Distance(Position, (Vector2)gridView.transform.position + position);

                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestGridView = gridView;
                            bestGridViewLocation = new Vector2Int(Mathf.RoundToInt((position.x + (GridSize / 2f)) / GridSize), -Mathf.RoundToInt((position.y - (GridSize / 2f)) / GridSize));
                        }
                    }
                    if (bestGridView != null)
                    {
                        ResetAllCurrent();
                        currentGridView = bestGridView;
                        currentContainedGridsView = bestContainedGridsView;
                        gridViewLocation = bestGridViewLocation;
                        globalPosition.x = currentGridView.transform.position.x + (GridSize * gridViewLocation.x) - (GridSize / 2f);
                        globalPosition.y = currentGridView.transform.position.y - (GridSize * gridViewLocation.y) + (GridSize / 2f);
                        return true;
                    }
                }
            }
            // Support SlotViews Window

            if (itemSpecificationPanels.Count != 0)
            {
                Vector2 position;
                int bestDepth = -1;
                CanvasRenderer canvasRenderer;
                ItemSpecificationPanel bestItemSpecificationPanel = null;
                if (currentItemSpecificationPanel != null)
                {
                    canvasRenderer = currentItemSpecificationPanel.GetComponent<CanvasRenderer>();
                    bestDepth = canvasRenderer.absoluteDepth;
                }
                foreach (ItemSpecificationPanel itemSpecificationPanel in itemSpecificationPanels)
                {
                    if (itemSpecificationPanel == null || itemSpecificationPanel == currentItemSpecificationPanel) continue;
                    rectTransform = itemSpecificationPanel.GetComponent<RectTransform>();
                    position = new Vector2(itemSpecificationPanel.transform.position.x - ((rectTransform.sizeDelta.x / 2) * ScreenRatio), itemSpecificationPanel.transform.position.y + ((rectTransform.sizeDelta.y / 2) * ScreenRatio));
                    if (Position.x > position.x && Position.x < (position.x + (rectTransform.sizeDelta.x * ScreenRatio)) && Position.y < position.y && Position.y > (position.y - (rectTransform.sizeDelta.y * ScreenRatio)))
                    {
                        canvasRenderer = itemSpecificationPanel.GetComponent<CanvasRenderer>();
                        if (canvasRenderer != null && canvasRenderer.absoluteDepth > bestDepth)
                        {
                            bestDepth = canvasRenderer.absoluteDepth;
                            bestItemSpecificationPanel = itemSpecificationPanel;
                        }
                    }
                }
                if (bestItemSpecificationPanel != null)
                {
                    rectTransform = bestItemSpecificationPanel.GetComponent<RectTransform>();

                    float distance;
                    float bestDistance = 999999f;

                    ModSlotView bestModSlotView = null;

                    foreach (ModSlotView modSlotView in Traverse.Create(bestItemSpecificationPanel).Field("_modsContainer").GetValue<RectTransform>().GetComponentsInChildren<ModSlotView>())
                    {
                        distance = Vector2.Distance(Position, (Vector2)modSlotView.transform.position);

                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestModSlotView = modSlotView;
                        }
                    }
                    if (bestModSlotView != null)
                    {
                        ResetAllCurrent();
                        currentModSlotView = bestModSlotView;
                        currentItemSpecificationPanel = bestItemSpecificationPanel;
                        gridViewLocation = new Vector2Int(1, 1);
                        globalPosition.x = currentModSlotView.transform.position.x;
                        globalPosition.y = currentModSlotView.transform.position.y;
                        return true;
                    }
                }
            }
            return false;
        }
        private bool FindScrollRectNoDrag(Vector2 Position)
        {
            currentScrollRectNoDrag = null;
            currentScrollRectNoDragRectTransform = null;

            RectTransform rectTransform;
            Vector2 position;
            foreach (ScrollRectNoDrag scrollRectNoDrag in scrollRectNoDrags)
            {
                rectTransform = scrollRectNoDrag.GetComponent<RectTransform>();
                position = new Vector2(rectTransform.position.x + (rectTransform.rect.x * ScreenRatio), rectTransform.position.y - ((rectTransform.rect.height * (rectTransform.pivot.y - 1f)) * ScreenRatio));
                if (Position.x > position.x && Position.x < (position.x + (rectTransform.rect.width * ScreenRatio)) && Position.y < position.y && Position.y > (position.y - (rectTransform.rect.height * ScreenRatio)))
                {
                    currentScrollRectNoDrag = scrollRectNoDrag;
                    currentScrollRectNoDragRectTransform = rectTransform;
                    return true;
                }
                else
                {
                    RectTransform rectTransform2 = scrollRectNoDrag.content;
                    position = new Vector2(rectTransform2.position.x + (rectTransform2.rect.x * ScreenRatio), rectTransform2.position.y - ((rectTransform2.rect.height * (rectTransform2.pivot.y - 1f)) * ScreenRatio));
                    if (Position.x > position.x && Position.x < (position.x + (rectTransform2.rect.width * ScreenRatio)) && Position.y < position.y && Position.y > (position.y - (rectTransform2.rect.height * ScreenRatio)))
                    {
                        currentScrollRectNoDrag = scrollRectNoDrag;
                        currentScrollRectNoDragRectTransform = rectTransform;
                        return true;
                    }
                }
            }
            return false;
        }
        public void ControllerUIMove(Vector2Int direction, bool Skip)
        {
            lastDirection = direction;

            DisableSet("SearchButton");

            if (SearchButtonImage != null)
            {
                SearchButtonImage.color = Color.white;
                SearchButtonImage = null;
            }

            ScreenRatio = (Screen.height / 1080f);
            GridSize = 63f * ScreenRatio;
            ModSize = 63f * ScreenRatio;
            SlotSize = 125f * ScreenRatio;

            Vector2 position;

            int GridWidth = 1;
            int GridHeight = 1;

            float dot;
            float distance;
            float score;
            float bestScore = 99999f;

            GridView bestGridView = null;
            ModSlotView bestModSlotView = null;
            SlotView bestEquipmentSlotView = null;
            SlotView bestWeaponsSlotView = null;
            SlotView bestArmbandSlotView = null;
            SlotView bestContainersSlotView = null;
            SlotView bestDogtagSlotView = null;
            SlotView bestSpecialSlotSlotView = null;
            ItemSpecificationPanel bestItemSpecificationPanel = null;
            TradingTableGridView bestTradingTableGridView = null;
            ContainedGridsView bestContainedGridsView = null;
            SearchButton bestSearchButton = null;
            ContextMenuButton bestContextMenuButton = null;
            Vector2Int bestGridViewLocation = Vector2Int.one;

            // Exclusive SimpleContextMenuButton Blind Search
            if (contextMenuButtons.Count > 0)
            {
                UpdateGlobalPosition();
                foreach (ContextMenuButton contextMenuButton in contextMenuButtons)
                {
                    if (contextMenuButton == null || contextMenuButton == currentContextMenuButton) continue;

                    position.x = contextMenuButton.transform.position.x;
                    position.y = contextMenuButton.transform.position.y;

                    dot = direction == Vector2Int.zero ? 1f : Vector2.Dot((globalPosition - position).normalized, -direction);
                    distance = Vector2.Distance(globalPosition, position);
                    score = Mathf.Lerp(distance, distance * 0.25f, dot);

                    if (score < bestScore && dot > 0.4f)
                    {
                        bestScore = score;
                        bestContextMenuButton = contextMenuButton;
                    }
                }
                if (bestContextMenuButton == null) return;
                if ((currentContextMenuButton != null && currentContextMenuButton.gameObject.activeSelf))
                {
                    currentContextMenuButton.OnPointerExit(null);
                }
                currentContextMenuButton = bestContextMenuButton;
                UpdateGlobalPosition();
                currentContextMenuButton.OnPointerEnter(null);
                return;
            }

            if ((currentGridView == null || !currentGridView.gameObject.activeSelf) && (currentTradingTableGridView == null || !currentTradingTableGridView.gameObject.activeSelf) && (currentEquipmentSlotView == null || !currentEquipmentSlotView.gameObject.activeSelf) && (currentWeaponsSlotView == null || !currentWeaponsSlotView.gameObject.activeSelf) && (currentArmbandSlotView == null || !currentArmbandSlotView.gameObject.activeSelf) && (currentContainersSlotView == null || !currentContainersSlotView.gameObject.activeSelf) && (currentDogtagSlotView == null || !currentDogtagSlotView.gameObject.activeSelf) && (currentSpecialSlotSlotView == null || !currentSpecialSlotSlotView.gameObject.activeSelf) && (currentModSlotView == null || !currentModSlotView.gameObject.activeSelf) && (currentSearchButton == null || !currentSearchButton.gameObject.activeSelf))
            {
                ControllerUIMoveToClosest(false);
                return;
            }

            if (Skip) goto Skip1;

            // Local GridView Search
            if ((currentGridView != null && currentGridView.gameObject.activeSelf))
            {
                GridWidth = currentGridView.Grid.GridWidth;
                GridHeight = currentGridView.Grid.GridHeight;
            }
            if ((currentGridView != null && currentGridView.gameObject.activeSelf) && gridViewLocation.x + direction.x >= 1 && gridViewLocation.x + direction.x <= GridWidth && gridViewLocation.y - direction.y >= 1 && gridViewLocation.y - direction.y <= GridHeight)
            {
                gridViewLocation.x += direction.x;
                gridViewLocation.y -= direction.y;

                globalPosition.x = currentGridView.transform.position.x + (GridSize * gridViewLocation.x) - (GridSize / 2f);
                globalPosition.y = currentGridView.transform.position.y - (GridSize * gridViewLocation.y) + (GridSize / 2f);
                FindGridWindow(globalPosition);
                ControllerUIOnMove(direction, globalPosition);
                return;
            }

            // Local TradingTableGridView Search
            if ((currentTradingTableGridView != null && currentTradingTableGridView.gameObject.activeSelf))
            {
                GridWidth = currentTradingTableGridView.Grid.GridWidth;
                GridHeight = currentTradingTableGridView.Grid.GridHeight;
            }
            if ((currentTradingTableGridView != null && currentTradingTableGridView.gameObject.activeSelf) && gridViewLocation.x + direction.x >= 1 && gridViewLocation.x + direction.x <= GridWidth && gridViewLocation.y - direction.y >= 1 && gridViewLocation.y - direction.y <= GridHeight)
            {
                gridViewLocation.x += direction.x;
                gridViewLocation.y -= direction.y;

                Vector2 size = currentTradingTableGridView.GetComponent<RectTransform>().sizeDelta * ScreenRatio;
                globalPosition.x = (currentTradingTableGridView.transform.position.x - (size.x / 2f)) + (GridSize * gridViewLocation.x) - (GridSize / 2f);
                globalPosition.y = (currentTradingTableGridView.transform.position.y + (size.y / 2f)) - (GridSize * gridViewLocation.y) + (GridSize / 2f);
                FindGridWindow(globalPosition);
                ControllerUIOnMove(direction, globalPosition);
                return;
            }

            // Local ContainedGridsView GridView Blind Search
            if ((currentGridView != null && currentGridView.gameObject.activeSelf) && currentContainedGridsView != null)
            {
                globalPosition.x = currentGridView.transform.position.x + (GridSize * gridViewLocation.x) - (GridSize / 2f);
                globalPosition.y = currentGridView.transform.position.y - (GridSize * gridViewLocation.y) + (GridSize / 2f);

                foreach (GridView gridView in currentContainedGridsView.GridViews)
                {
                    if (gridView == null || gridView == currentGridView) continue;

                    GridWidth = gridView.Grid.GridWidth;
                    GridHeight = gridView.Grid.GridHeight;

                    if (GridWidth == 1 && GridHeight == 1)
                    {
                        position.x = GridSize / 2f;
                        position.y = -GridSize / 2f;
                    }
                    else if (GridWidth == 1)
                    {
                        position.x = GridSize / 2f;
                        position.y = -Mathf.Clamp(gridView.transform.position.y - globalPosition.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                    }
                    else if (GridHeight == 1)
                    {
                        position.x = Mathf.Clamp(globalPosition.x - gridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                        position.y = -GridSize / 2f;
                    }
                    else
                    {
                        position.x = Mathf.Clamp(globalPosition.x - gridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                        position.y = -Mathf.Clamp(gridView.transform.position.y - globalPosition.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                    }

                    dot = Vector2.Dot((globalPosition - ((Vector2)gridView.transform.position + position)).normalized, -direction);
                    distance = Vector2.Distance(globalPosition, (Vector2)gridView.transform.position + position);
                    score = Mathf.Lerp(distance, distance * 0.25f, dot);

                    if (score < bestScore && dot > 0.4f)
                    {
                        bestScore = score;
                        bestGridView = gridView;
                        bestGridViewLocation = new Vector2Int(Mathf.RoundToInt((position.x + (GridSize / 2f)) / GridSize), -Mathf.RoundToInt((position.y - (GridSize / 2f)) / GridSize));
                    }
                }

                if (bestGridView != null)
                {
                    bestContainedGridsView = currentContainedGridsView;
                    ResetAllCurrent();
                    currentGridView = bestGridView;
                    currentContainedGridsView = bestContainedGridsView;
                    gridViewLocation = bestGridViewLocation;

                    globalPosition.x = currentGridView.transform.position.x + (GridSize * gridViewLocation.x) - (GridSize / 2f);
                    globalPosition.y = currentGridView.transform.position.y - (GridSize * gridViewLocation.y) + (GridSize / 2f);
                    FindGridWindow(globalPosition);
                    ControllerUIOnMove(direction, globalPosition);
                    return;
                }
                Vector2 point = globalPosition + ((Vector2)direction * 1000f);
                RectTransform rectTransform = currentContainedGridsView.GetComponent<RectTransform>();
                position.x = currentContainedGridsView.transform.position.x;
                position.y = currentContainedGridsView.transform.position.y - ((rectTransform.sizeDelta.y * ScreenRatio) * (rectTransform.pivot.y - 1f));
                if (FindGridView(new Vector2(position.x + Mathf.Clamp(point.x - position.x, 0, rectTransform.sizeDelta.x * ScreenRatio) + (direction.x * (ModSize / 2)), position.y - Mathf.Clamp(position.y - point.y, 0, rectTransform.sizeDelta.y * ScreenRatio) + (direction.y * (ModSize / 2)))))
                {
                    ControllerUIOnMove(direction, globalPosition);
                    return;
                }
            }

            // Local ItemSpecificationPanel ModSlotView Blind Search
            if ((currentModSlotView != null && currentModSlotView.gameObject.activeSelf) && currentItemSpecificationPanel != null)
            {
                globalPosition.x = currentModSlotView.transform.position.x;
                globalPosition.y = currentModSlotView.transform.position.y;

                foreach (ModSlotView modSlotView in Traverse.Create(currentItemSpecificationPanel).Field("_modsContainer").GetValue<RectTransform>().GetComponentsInChildren<ModSlotView>())
                {
                    if (modSlotView == null || modSlotView == currentModSlotView) continue;

                    dot = Vector2.Dot((globalPosition - ((Vector2)modSlotView.transform.position)).normalized, -direction);
                    distance = Vector2.Distance(globalPosition, (Vector2)modSlotView.transform.position);
                    score = Mathf.Lerp(distance, distance * 0.25f, dot);

                    if (score < bestScore && dot > 0.4f)
                    {
                        bestScore = score;
                        bestModSlotView = modSlotView;
                    }
                }

                if (bestModSlotView != null)
                {
                    bestItemSpecificationPanel = currentItemSpecificationPanel;
                    ResetAllCurrent();
                    currentModSlotView = bestModSlotView;
                    currentItemSpecificationPanel = bestItemSpecificationPanel;
                    gridViewLocation = new Vector2Int(1, 1);

                    globalPosition.x = currentModSlotView.transform.position.x;
                    globalPosition.y = currentModSlotView.transform.position.y;
                    FindGridWindow(globalPosition);
                    ControllerUIOnMove(direction, globalPosition);
                    return;
                }
                Vector2 point = globalPosition + ((Vector2)direction * 1000f);
                RectTransform rectTransform = currentItemSpecificationPanel.GetComponent<RectTransform>();
                position.x = currentItemSpecificationPanel.transform.position.x - ((rectTransform.sizeDelta.x * ScreenRatio) / 2);
                position.y = currentItemSpecificationPanel.transform.position.y + ((rectTransform.sizeDelta.y * ScreenRatio) / 2);
                if (FindGridView(new Vector2(position.x + Mathf.Clamp(point.x - position.x, 0, rectTransform.sizeDelta.x * ScreenRatio) + (direction.x * (ModSize / 2)), position.y - Mathf.Clamp(position.y - point.y, 0, rectTransform.sizeDelta.y * ScreenRatio) + (direction.y * (ModSize / 2)))))
                {
                    ControllerUIOnMove(direction, globalPosition);
                    return;
                }


            }

            Skip1:

            // GlobalPosition
            UpdateGlobalPosition();
            // Global Blind Search

            if (Skip) goto Skip2;

            // GridView Blind Search
            foreach (GridView gridView in gridViews)
            {
                if (gridView == null || gridView == currentGridView) continue;

                GridWidth = gridView.Grid.GridWidth;
                GridHeight = gridView.Grid.GridHeight;

                if (GridWidth == 1 && GridHeight == 1)
                {
                    position.x = GridSize / 2f;
                    position.y = -GridSize / 2f;
                }
                else if (GridWidth == 1)
                {
                    position.x = GridSize / 2f;
                    position.y = -Mathf.Clamp(gridView.transform.position.y - globalPosition.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                }
                else if (GridHeight == 1)
                {
                    position.x = Mathf.Clamp(globalPosition.x - gridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                    position.y = -GridSize / 2f;
                }
                else
                {
                    position.x = Mathf.Clamp(globalPosition.x - gridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                    position.y = -Mathf.Clamp(gridView.transform.position.y - globalPosition.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                }

                dot = Vector2.Dot((globalPosition - ((Vector2)gridView.transform.position + position)).normalized, -direction);
                distance = Vector2.Distance(globalPosition, (Vector2)gridView.transform.position + position);
                score = Mathf.Lerp(distance, distance * 0.25f, dot);

                if (score < bestScore && dot > 0.4f)
                {
                    bestScore = score;
                    bestGridView = gridView;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(Mathf.RoundToInt((position.x + (GridSize / 2f)) / GridSize), -Mathf.RoundToInt((position.y - (GridSize / 2f)) / GridSize));
                }
            }
            // ContainedGridsView GridView Blind Search
            foreach (ContainedGridsView containedGridsView in containedGridsViews)
            {
                if (containedGridsView == null || containedGridsView == currentContainedGridsView) continue;
                foreach (GridView gridView in containedGridsView.GridViews)
                {
                    if (gridView == null || gridView == currentGridView) continue;

                    GridWidth = gridView.Grid.GridWidth;
                    GridHeight = gridView.Grid.GridHeight;

                    if (GridWidth == 1 && GridHeight == 1)
                    {
                        position.x = GridSize / 2f;
                        position.y = -GridSize / 2f;
                    }
                    else if (GridWidth == 1)
                    {
                        position.x = GridSize / 2f;
                        position.y = -Mathf.Clamp(gridView.transform.position.y - globalPosition.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                    }
                    else if (GridHeight == 1)
                    {
                        position.x = Mathf.Clamp(globalPosition.x - gridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                        position.y = -GridSize / 2f;
                    }
                    else
                    {
                        position.x = Mathf.Clamp(globalPosition.x - gridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                        position.y = -Mathf.Clamp(gridView.transform.position.y - globalPosition.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                    }

                    dot = Vector2.Dot((globalPosition - ((Vector2)gridView.transform.position + position)).normalized, -direction);
                    distance = Vector2.Distance(globalPosition, (Vector2)gridView.transform.position + position);
                    score = Mathf.Lerp(distance, distance * 0.25f, dot);

                    if (score < bestScore && dot > 0.4f)
                    {
                        bestScore = score;
                        bestGridView = gridView;
                        bestModSlotView = null;
                        bestEquipmentSlotView = null;
                        bestWeaponsSlotView = null;
                        bestArmbandSlotView = null;
                        bestContainersSlotView = null;
                        bestDogtagSlotView = null;
                        bestSpecialSlotSlotView = null;
                        bestItemSpecificationPanel = null;
                        bestTradingTableGridView = null;
                        bestContainedGridsView = containedGridsView;
                        bestSearchButton = null;
                        bestContextMenuButton = null;
                        bestGridViewLocation = new Vector2Int(Mathf.RoundToInt((position.x + (GridSize / 2f)) / GridSize), -Mathf.RoundToInt((position.y - (GridSize / 2f)) / GridSize));
                    }
                }
            }
            // TradingTableGridView Blind Search
            if (tradingTableGridView != null)
            {
                Vector2 size = tradingTableGridView.GetComponent<RectTransform>().sizeDelta * ScreenRatio;
                Vector2 positionTradingTableGridView = new Vector2(tradingTableGridView.transform.position.x - (size.x / 2f), tradingTableGridView.transform.position.y + (size.y / 2f));

                GridWidth = tradingTableGridView.Grid.GridWidth;
                GridHeight = tradingTableGridView.Grid.GridHeight;

                position.x = Mathf.Clamp(globalPosition.x - positionTradingTableGridView.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                position.y = -Mathf.Clamp(positionTradingTableGridView.y - globalPosition.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));

                dot = Vector2.Dot((globalPosition - (positionTradingTableGridView + position)).normalized, -direction);
                distance = Vector2.Distance(globalPosition, positionTradingTableGridView + position);
                score = Mathf.Lerp(distance, distance * 0.25f, dot);

                if (score < bestScore && dot > 0.4f)
                {
                    bestScore = score;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = tradingTableGridView;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestContextMenuButton = null;
                    bestGridViewLocation = new Vector2Int(Mathf.RoundToInt((position.x + (GridSize / 2f)) / GridSize), -Mathf.RoundToInt((position.y - (GridSize / 2f)) / GridSize));
                }
            }
            // ItemSpecificationPanel ModSlotView Blind Search
            foreach (ItemSpecificationPanel itemSpecificationPanel in itemSpecificationPanels)
            {
                if (itemSpecificationPanel == null || itemSpecificationPanel == currentItemSpecificationPanel) continue;
                foreach (ModSlotView modSlotView in Traverse.Create(itemSpecificationPanel).Field("_modsContainer").GetValue<RectTransform>().GetComponentsInChildren<ModSlotView>())
                {
                    if (modSlotView == null || modSlotView == currentModSlotView) continue;

                    dot = Vector2.Dot((globalPosition - ((Vector2)modSlotView.transform.position)).normalized, -direction);
                    distance = Vector2.Distance(globalPosition, (Vector2)modSlotView.transform.position);
                    score = Mathf.Lerp(distance, distance * 0.25f, dot);

                    if (score < bestScore && dot > 0.4f)
                    {
                        bestScore = score;
                        bestGridView = null;
                        bestModSlotView = modSlotView;
                        bestEquipmentSlotView = null;
                        bestWeaponsSlotView = null;
                        bestArmbandSlotView = null;
                        bestContainersSlotView = null;
                        bestDogtagSlotView = null;
                        bestSpecialSlotSlotView = null;
                        bestItemSpecificationPanel = itemSpecificationPanel;
                        bestTradingTableGridView = null;
                        bestContainedGridsView = null;
                        bestSearchButton = null;
                        bestGridViewLocation = new Vector2Int(1, 1);
                    }
                }
            }
            // SearchButton Blind Search
            foreach (SearchButton searchButton in searchButtons)
            {
                if (searchButton == null || searchButton == currentSearchButton) continue;
                Vector2 size = searchButton.GetComponent<RectTransform>().sizeDelta * ScreenRatio;
                position.x = searchButton.transform.position.x;// - (size.x / 2f);
                position.y = searchButton.transform.position.y;// - (size.y / 2f);

                dot = Vector2.Dot((globalPosition - position).normalized, -direction);
                distance = Vector2.Distance(globalPosition, position);
                score = Mathf.Lerp(distance, distance * 0.25f, dot);

                if (score < bestScore && dot > 0.4f)
                {
                    bestScore = score;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = searchButton;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // SpecialSlot Blind Search
            foreach (SlotView slotView in specialSlotSlotViews)
            {
                if (slotView == null || slotView == currentSpecialSlotSlotView) continue;
                position = new Vector2(slotView.transform.position.x + (GridSize / 2f), slotView.transform.position.y - (GridSize / 2f));
                dot = Vector2.Dot((globalPosition - position).normalized, -direction);
                distance = Vector2.Distance(globalPosition, position);
                score = Mathf.Lerp(distance, distance * 0.25f, dot);

                if (score < bestScore && dot > 0.4f)
                {
                    bestScore = score;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = slotView;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }

        Skip2:

            // EquipmentSlotView Blind Search
            foreach (SlotView slotView in equipmentSlotViews)
            {
                if (slotView == null || slotView == currentEquipmentSlotView) continue;
                position = new Vector2(slotView.transform.position.x + (SlotSize / 2f), slotView.transform.position.y - (SlotSize / 2f));
                dot = Vector2.Dot((globalPosition - position).normalized, -direction);
                distance = Vector2.Distance(globalPosition, position);
                score = Mathf.Lerp(distance, distance * 0.25f, dot);

                if (score < bestScore && dot > 0.4f)
                {
                    bestScore = score;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = slotView;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // WeaponsSlotView Blind Search
            foreach (SlotView slotView in weaponsSlotViews)
            {
                if (slotView == null || slotView == currentWeaponsSlotView) continue;
                position = new Vector2(slotView.transform.position.x + (157.0811f * ScreenRatio), slotView.transform.position.y - (SlotSize / 2f));
                dot = Vector2.Dot((globalPosition - position).normalized, -direction);
                distance = Vector2.Distance(globalPosition, position);
                score = Mathf.Lerp(distance, distance * 0.25f, dot);

                if (score < bestScore && dot > 0.4f)
                {
                    bestScore = score;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = slotView;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // ArmbandSlotView Blind Search
            if (armbandSlotView != null && armbandSlotView != currentArmbandSlotView)
            {
                position = new Vector2(armbandSlotView.transform.position.x + (SlotSize / 2f), armbandSlotView.transform.position.y - (32f * ScreenRatio));
                dot = Vector2.Dot((globalPosition - position).normalized, -direction);
                distance = Vector2.Distance(globalPosition, position);
                score = Mathf.Lerp(distance, distance * 0.25f, dot);

                if (score < bestScore && dot > 0.4f)
                {
                    bestScore = score;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = armbandSlotView;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // ContainersSlotView Blind Search
            foreach (SlotView slotView in containersSlotViews)
            {
                if (slotView == null || slotView == currentContainersSlotView) continue;
                position = new Vector2(slotView.transform.position.x + (SlotSize / 2f), slotView.transform.position.y - (SlotSize / 2f));
                dot = Vector2.Dot((globalPosition - position).normalized, -direction);
                distance = Vector2.Distance(globalPosition, position);
                score = Mathf.Lerp(distance, distance * 0.25f, dot);

                if (score < bestScore && dot > 0.4f)
                {
                    bestScore = score;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = slotView;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // LootEquipmentSlotView Blind Search
            foreach (SlotView slotView in lootEquipmentSlotViews)
            {
                if (slotView == null || slotView == currentEquipmentSlotView) continue;
                position = new Vector2(slotView.transform.position.x + (SlotSize / 2f), slotView.transform.position.y - (SlotSize / 2f));
                dot = Vector2.Dot((globalPosition - position).normalized, -direction);
                distance = Vector2.Distance(globalPosition, position);
                score = Mathf.Lerp(distance, distance * 0.25f, dot);

                if (score < bestScore && dot > 0.4f)
                {
                    bestScore = score;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = slotView;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // LootWeaponsSlotView Blind Search
            foreach (SlotView slotView in lootWeaponsSlotViews)
            {
                if (slotView == null || slotView == currentWeaponsSlotView) continue;
                position = new Vector2(slotView.transform.position.x + (157.0811f * ScreenRatio), slotView.transform.position.y - (SlotSize / 2f));
                dot = Vector2.Dot((globalPosition - position).normalized, -direction);
                distance = Vector2.Distance(globalPosition, position);
                score = Mathf.Lerp(distance, distance * 0.25f, dot);

                if (score < bestScore && dot > 0.4f)
                {
                    bestScore = score;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = slotView;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // LootArmbandSlotView Blind Search
            if (lootArmbandSlotView != null && lootArmbandSlotView != currentArmbandSlotView)
            {
                position = new Vector2(lootArmbandSlotView.transform.position.x + (SlotSize / 2f), lootArmbandSlotView.transform.position.y - (32f * ScreenRatio));
                dot = Vector2.Dot((globalPosition - position).normalized, -direction);
                distance = Vector2.Distance(globalPosition, position);
                score = Mathf.Lerp(distance, distance * 0.25f, dot);

                if (score < bestScore && dot > 0.4f)
                {
                    bestScore = score;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = lootArmbandSlotView;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // LootContainersSlotView Blind Search
            foreach (SlotView slotView in lootContainersSlotViews)
            {
                if (slotView == null || slotView == currentContainersSlotView) continue;
                position = new Vector2(slotView.transform.position.x + (SlotSize / 2f), slotView.transform.position.y - (SlotSize / 2f));
                dot = Vector2.Dot((globalPosition - position).normalized, -direction);
                distance = Vector2.Distance(globalPosition, position);
                score = Mathf.Lerp(distance, distance * 0.25f, dot);

                if (score < bestScore && dot > 0.4f)
                {
                    bestScore = score;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = slotView;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // Dogtag Blind Search
            if (dogtagSlotView != null)
            {
                position.x = dogtagSlotView.transform.position.x - GridSize - (GridSize / 2f);
                position.y = dogtagSlotView.transform.position.y - (GridSize / 2f);

                dot = Vector2.Dot((globalPosition - position).normalized, -direction);
                distance = Vector2.Distance(globalPosition, position);
                score = Mathf.Lerp(distance, distance * 0.25f, dot);

                if (score < bestScore && dot > 0.4f)
                {
                    bestScore = score;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = dogtagSlotView;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // Skip Include SimpleStash
            if (Skip && SimpleStashGridView != null && SimpleStashGridView != currentGridView)
            {
                GridWidth = SimpleStashGridView.Grid.GridWidth;
                GridHeight = SimpleStashGridView.Grid.GridHeight;

                if (GridWidth == 1 && GridHeight == 1)
                {
                    position.x = GridSize / 2f;
                    position.y = -GridSize / 2f;
                }
                else if (GridWidth == 1)
                {
                    position.x = GridSize / 2f;
                    position.y = -Mathf.Clamp(SimpleStashGridView.transform.position.y - globalPosition.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                }
                else if (GridHeight == 1)
                {
                    position.x = Mathf.Clamp(globalPosition.x - SimpleStashGridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                    position.y = -GridSize / 2f;
                }
                else
                {
                    position.x = Mathf.Clamp(globalPosition.x - SimpleStashGridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                    position.y = -Mathf.Clamp(SimpleStashGridView.transform.position.y - globalPosition.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                }

                dot = Vector2.Dot((globalPosition - ((Vector2)SimpleStashGridView.transform.position + position)).normalized, -direction);
                distance = Vector2.Distance(globalPosition, (Vector2)SimpleStashGridView.transform.position + position);
                score = Mathf.Lerp(distance, distance * 0.25f, dot);

                if (score < bestScore && dot > 0.4f)
                {
                    bestScore = score;
                    bestGridView = SimpleStashGridView;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(Mathf.RoundToInt((position.x + (GridSize / 2f)) / GridSize), -Mathf.RoundToInt((position.y - (GridSize / 2f)) / GridSize));
                }
            }
            // Support

            if (bestGridView == null && bestTradingTableGridView == null && bestEquipmentSlotView == null && bestWeaponsSlotView == null && bestArmbandSlotView == null && bestContainersSlotView == null && bestDogtagSlotView == null && bestSpecialSlotSlotView == null && bestModSlotView == null && bestSearchButton == null && Skip)
            {
                ControllerUIMove(direction, false);
                return;
            }
            // Set GridView/SlotView
            if (bestGridView == null && bestTradingTableGridView == null && bestEquipmentSlotView == null && bestWeaponsSlotView == null && bestArmbandSlotView == null && bestContainersSlotView == null && bestDogtagSlotView == null && bestSpecialSlotSlotView == null && bestModSlotView == null && bestSearchButton == null) return;

            currentGridView = bestGridView;
            currentModSlotView = bestModSlotView;
            currentTradingTableGridView = bestTradingTableGridView;
            currentContainedGridsView = bestContainedGridsView;
            currentItemSpecificationPanel = bestItemSpecificationPanel;
            currentEquipmentSlotView = bestEquipmentSlotView;
            currentWeaponsSlotView = bestWeaponsSlotView;
            currentArmbandSlotView = bestArmbandSlotView;
            currentContainersSlotView = bestContainersSlotView;
            currentDogtagSlotView = bestDogtagSlotView;
            currentSpecialSlotSlotView = bestSpecialSlotSlotView;
            currentSearchButton = bestSearchButton;
            gridViewLocation = bestGridViewLocation;

            if ((currentSearchButton != null && currentSearchButton.gameObject.activeSelf))
            {
                EnableSet("SearchButton");
                SearchButtonImage = currentSearchButton.GetComponent<Image>();
                if (SearchButtonImage != null)
                {
                    SearchButtonImage.color = Color.red;
                }
            }

            UpdateGlobalPosition();
            FindGridWindow(globalPosition);
            // OnMove
            ControllerUIOnMove(direction, globalPosition);
        }
        public void ControllerUIMoveToClosest(bool Skip)
        {
            DisableSet("SearchButton");

            if (SearchButtonImage != null)
            {
                SearchButtonImage.color = Color.white;
                SearchButtonImage = null;
            }

            ScreenRatio = (Screen.height / 1080f);
            GridSize = 63f * ScreenRatio;
            ModSize = 63f * ScreenRatio;
            SlotSize = 125f * ScreenRatio;

            Vector2 position;

            int GridWidth = 1;
            int GridHeight = 1;

            float distance;
            float bestScore = 99999f;

            GridView bestGridView = null;
            ModSlotView bestModSlotView = null;
            SlotView bestEquipmentSlotView = null;
            SlotView bestWeaponsSlotView = null;
            SlotView bestArmbandSlotView = null;
            SlotView bestContainersSlotView = null;
            SlotView bestDogtagSlotView = null;
            SlotView bestSpecialSlotSlotView = null;
            ItemSpecificationPanel bestItemSpecificationPanel = null;
            TradingTableGridView bestTradingTableGridView = null;
            ContainedGridsView bestContainedGridsView = null;
            SearchButton bestSearchButton = null;
            Vector2Int bestGridViewLocation = Vector2Int.one;

            // GlobalPosition
            UpdateGlobalPosition();
            // Global Blind Search

            if (Skip) goto Skip2;

            // GridView Blind Search
            foreach (GridView gridView in gridViews)
            {
                if (gridView == null) continue;

                GridWidth = gridView.Grid.GridWidth;
                GridHeight = gridView.Grid.GridHeight;

                if (GridWidth == 1 && GridHeight == 1)
                {
                    position.x = GridSize / 2f;
                    position.y = -GridSize / 2f;
                }
                else if (GridWidth == 1)
                {
                    position.x = GridSize / 2f;
                    position.y = -Mathf.Clamp(gridView.transform.position.y - globalPosition.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                }
                else if (GridHeight == 1)
                {
                    position.x = Mathf.Clamp(globalPosition.x - gridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                    position.y = -GridSize / 2f;
                }
                else
                {
                    position.x = Mathf.Clamp(globalPosition.x - gridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                    position.y = -Mathf.Clamp(gridView.transform.position.y - globalPosition.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                }

                distance = Vector2.Distance(globalPosition, (Vector2)gridView.transform.position + position);

                if (distance < bestScore)
                {
                    bestScore = distance;
                    bestGridView = gridView;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(Mathf.RoundToInt((position.x + (GridSize / 2f)) / GridSize), -Mathf.RoundToInt((position.y - (GridSize / 2f)) / GridSize));
                }
            }
            // ContainedGridsView GridView Blind Search
            foreach (ContainedGridsView containedGridsView in containedGridsViews)
            {
                if (containedGridsView == null) continue;
                foreach (GridView gridView in containedGridsView.GridViews)
                {
                    if (gridView == null) continue;

                    GridWidth = gridView.Grid.GridWidth;
                    GridHeight = gridView.Grid.GridHeight;

                    if (GridWidth == 1 && GridHeight == 1)
                    {
                        position.x = GridSize / 2f;
                        position.y = -GridSize / 2f;
                    }
                    else if (GridWidth == 1)
                    {
                        position.x = GridSize / 2f;
                        position.y = -Mathf.Clamp(gridView.transform.position.y - globalPosition.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                    }
                    else if (GridHeight == 1)
                    {
                        position.x = Mathf.Clamp(globalPosition.x - gridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                        position.y = -GridSize / 2f;
                    }
                    else
                    {
                        position.x = Mathf.Clamp(globalPosition.x - gridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                        position.y = -Mathf.Clamp(gridView.transform.position.y - globalPosition.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                    }

                    distance = Vector2.Distance(globalPosition, (Vector2)gridView.transform.position + position);

                    if (distance < bestScore)
                    {
                        bestScore = distance;
                        bestGridView = gridView;
                        bestModSlotView = null;
                        bestEquipmentSlotView = null;
                        bestWeaponsSlotView = null;
                        bestArmbandSlotView = null;
                        bestContainersSlotView = null;
                        bestDogtagSlotView = null;
                        bestSpecialSlotSlotView = null;
                        bestItemSpecificationPanel = null;
                        bestTradingTableGridView = null;
                        bestContainedGridsView = containedGridsView;
                        bestSearchButton = null;
                        bestGridViewLocation = new Vector2Int(Mathf.RoundToInt((position.x + (GridSize / 2f)) / GridSize), -Mathf.RoundToInt((position.y - (GridSize / 2f)) / GridSize));
                    }
                }
            }
            // TradingTableGridView Blind Search
            if (tradingTableGridView != null)
            {
                Vector2 size = tradingTableGridView.GetComponent<RectTransform>().sizeDelta * ScreenRatio;
                Vector2 positionTradingTableGridView = new Vector2(tradingTableGridView.transform.position.x - (size.x / 2f), tradingTableGridView.transform.position.y + (size.y / 2f));

                GridWidth = tradingTableGridView.Grid.GridWidth;
                GridHeight = tradingTableGridView.Grid.GridHeight;

                position.x = Mathf.Clamp(globalPosition.x - positionTradingTableGridView.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                position.y = -Mathf.Clamp(positionTradingTableGridView.y - globalPosition.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));

                distance = Vector2.Distance(globalPosition, positionTradingTableGridView + position);

                if (distance < bestScore)
                {
                    bestScore = distance;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = tradingTableGridView;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(Mathf.RoundToInt((position.x + (GridSize / 2f)) / GridSize), -Mathf.RoundToInt((position.y - (GridSize / 2f)) / GridSize));
                }
            }
            // ItemSpecificationPanel ModSlotView Blind Search
            foreach (ItemSpecificationPanel itemSpecificationPanel in itemSpecificationPanels)
            {
                if (itemSpecificationPanel == null) continue;
                foreach (ModSlotView modSlotView in Traverse.Create(itemSpecificationPanel).Field("_modsContainer").GetValue<RectTransform>().GetComponentsInChildren<ModSlotView>())
                {
                    if (modSlotView == null) continue;

                    distance = Vector2.Distance(globalPosition, (Vector2)modSlotView.transform.position);

                    if (distance < bestScore)
                    {
                        bestScore = distance;
                        bestGridView = null;
                        bestModSlotView = modSlotView;
                        bestEquipmentSlotView = null;
                        bestWeaponsSlotView = null;
                        bestArmbandSlotView = null;
                        bestContainersSlotView = null;
                        bestDogtagSlotView = null;
                        bestSpecialSlotSlotView = null;
                        bestItemSpecificationPanel = itemSpecificationPanel;
                        bestTradingTableGridView = null;
                        bestContainedGridsView = null;
                        bestSearchButton = null;
                        bestGridViewLocation = new Vector2Int(1, 1);
                    }
                }
            }
            // SearchButton Blind Search
            foreach (SearchButton searchButton in searchButtons)
            {
                if (searchButton == null) continue;
                Vector2 size = searchButton.GetComponent<RectTransform>().sizeDelta * ScreenRatio;
                position.x = searchButton.transform.position.x;// - (size.x / 2f);
                position.y = searchButton.transform.position.y;// - (size.y / 2f);

                distance = Vector2.Distance(globalPosition, position);

                if (distance < bestScore)
                {
                    bestScore = distance;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = searchButton;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // SpecialSlot Blind Search
            foreach (SlotView slotView in specialSlotSlotViews)
            {
                if (slotView == null) continue;
                position = new Vector2(slotView.transform.position.x + (GridSize / 2f), slotView.transform.position.y - (GridSize / 2f));
                distance = Vector2.Distance(globalPosition, position);

                if (distance < bestScore)
                {
                    bestScore = distance;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = slotView;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }

        Skip2:

            // EquipmentSlotView Blind Search
            foreach (SlotView slotView in equipmentSlotViews)
            {
                if (slotView == null) continue;
                position = new Vector2(slotView.transform.position.x + (SlotSize / 2f), slotView.transform.position.y - (SlotSize / 2f));
                distance = Vector2.Distance(globalPosition, position);

                if (distance < bestScore)
                {
                    bestScore = distance;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = slotView;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // WeaponsSlotView Blind Search
            foreach (SlotView slotView in weaponsSlotViews)
            {
                if (slotView == null) continue;
                position = new Vector2(slotView.transform.position.x + (157.0811f * ScreenRatio), slotView.transform.position.y - (SlotSize / 2f));
                distance = Vector2.Distance(globalPosition, position);

                if (distance < bestScore)
                {
                    bestScore = distance;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = slotView;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // ArmbandSlotView Blind Search
            if (armbandSlotView != null)
            {
                position = new Vector2(armbandSlotView.transform.position.x + (SlotSize / 2f), armbandSlotView.transform.position.y - (32f * ScreenRatio));
                distance = Vector2.Distance(globalPosition, position);

                if (distance < bestScore)
                {
                    bestScore = distance;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = armbandSlotView;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // ContainersSlotView Blind Search
            foreach (SlotView slotView in containersSlotViews)
            {
                if (slotView == null) continue;
                position = new Vector2(slotView.transform.position.x + (SlotSize / 2f), slotView.transform.position.y - (SlotSize / 2f));
                distance = Vector2.Distance(globalPosition, position);

                if (distance < bestScore)
                {
                    bestScore = distance;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = slotView;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // LootEquipmentSlotView Blind Search
            foreach (SlotView slotView in lootEquipmentSlotViews)
            {
                if (slotView == null) continue;
                position = new Vector2(slotView.transform.position.x + (SlotSize / 2f), slotView.transform.position.y - (SlotSize / 2f));
                distance = Vector2.Distance(globalPosition, position);

                if (distance < bestScore)
                {
                    bestScore = distance;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = slotView;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // LootWeaponsSlotView Blind Search
            foreach (SlotView slotView in lootWeaponsSlotViews)
            {
                if (slotView == null) continue;
                position = new Vector2(slotView.transform.position.x + (157.0811f * ScreenRatio), slotView.transform.position.y - (SlotSize / 2f));
                distance = Vector2.Distance(globalPosition, position);

                if (distance < bestScore)
                {
                    bestScore = distance;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = slotView;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // LootArmbandSlotView Blind Search
            if (lootArmbandSlotView != null)
            {
                position = new Vector2(lootArmbandSlotView.transform.position.x + (SlotSize / 2f), lootArmbandSlotView.transform.position.y - (32f * ScreenRatio));
                distance = Vector2.Distance(globalPosition, position);

                if (distance < bestScore)
                {
                    bestScore = distance;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = lootArmbandSlotView;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // LootContainersSlotView Blind Search
            foreach (SlotView slotView in lootContainersSlotViews)
            {
                if (slotView == null) continue;
                position = new Vector2(slotView.transform.position.x + (SlotSize / 2f), slotView.transform.position.y - (SlotSize / 2f));
                distance = Vector2.Distance(globalPosition, position);

                if (distance < bestScore)
                {
                    bestScore = distance;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = slotView;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // Dogtag Blind Search
            if (dogtagSlotView != null)
            {
                position.x = dogtagSlotView.transform.position.x - GridSize - (GridSize / 2f);
                position.y = dogtagSlotView.transform.position.y - (GridSize / 2f);

                distance = Vector2.Distance(globalPosition, position);

                if (distance < bestScore)
                {
                    bestScore = distance;
                    bestGridView = null;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = dogtagSlotView;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(1, 1);
                }
            }
            // Skip Include SimpleStash
            if (Skip && SimpleStashGridView != null)
            {
                GridWidth = SimpleStashGridView.Grid.GridWidth;
                GridHeight = SimpleStashGridView.Grid.GridHeight;

                if (GridWidth == 1 && GridHeight == 1)
                {
                    position.x = GridSize / 2f;
                    position.y = -GridSize / 2f;
                }
                else if (GridWidth == 1)
                {
                    position.x = GridSize / 2f;
                    position.y = -Mathf.Clamp(SimpleStashGridView.transform.position.y - globalPosition.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                }
                else if (GridHeight == 1)
                {
                    position.x = Mathf.Clamp(globalPosition.x - SimpleStashGridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                    position.y = -GridSize / 2f;
                }
                else
                {
                    position.x = Mathf.Clamp(globalPosition.x - SimpleStashGridView.transform.position.x, GridSize / 2f, (GridSize * GridWidth) - (GridSize / 2f));
                    position.y = -Mathf.Clamp(SimpleStashGridView.transform.position.y - globalPosition.y, GridSize / 2f, (GridSize * GridHeight) - (GridSize / 2f));
                }

                distance = Vector2.Distance(globalPosition, (Vector2)SimpleStashGridView.transform.position + position);

                if (distance < bestScore)
                {
                    bestScore = distance;
                    bestGridView = SimpleStashGridView;
                    bestModSlotView = null;
                    bestEquipmentSlotView = null;
                    bestWeaponsSlotView = null;
                    bestArmbandSlotView = null;
                    bestContainersSlotView = null;
                    bestDogtagSlotView = null;
                    bestSpecialSlotSlotView = null;
                    bestItemSpecificationPanel = null;
                    bestTradingTableGridView = null;
                    bestContainedGridsView = null;
                    bestSearchButton = null;
                    bestGridViewLocation = new Vector2Int(Mathf.RoundToInt((position.x + (GridSize / 2f)) / GridSize), -Mathf.RoundToInt((position.y - (GridSize / 2f)) / GridSize));
                }
            }
            // Support

            if (bestGridView == null && bestTradingTableGridView == null && bestEquipmentSlotView == null && bestWeaponsSlotView == null && bestArmbandSlotView == null && bestContainersSlotView == null && bestDogtagSlotView == null && bestSpecialSlotSlotView == null && bestModSlotView == null && bestSearchButton == null && Skip)
            {
                ControllerUIMoveToClosest(false);
                return;
            }
            // Set GridView/SlotView
            if (bestGridView == null && bestTradingTableGridView == null && bestEquipmentSlotView == null && bestWeaponsSlotView == null && bestArmbandSlotView == null && bestContainersSlotView == null && bestDogtagSlotView == null && bestSpecialSlotSlotView == null && bestModSlotView == null && bestSearchButton == null) return;

            currentGridView = bestGridView;
            currentModSlotView = bestModSlotView;
            currentTradingTableGridView = bestTradingTableGridView;
            currentContainedGridsView = bestContainedGridsView;
            currentItemSpecificationPanel = bestItemSpecificationPanel;
            currentEquipmentSlotView = bestEquipmentSlotView;
            currentWeaponsSlotView = bestWeaponsSlotView;
            currentArmbandSlotView = bestArmbandSlotView;
            currentContainersSlotView = bestContainersSlotView;
            currentDogtagSlotView = bestDogtagSlotView;
            currentSpecialSlotSlotView = bestSpecialSlotSlotView;
            currentSearchButton = bestSearchButton;
            gridViewLocation = bestGridViewLocation;

            if ((currentSearchButton != null && currentSearchButton.gameObject.activeSelf))
            {
                EnableSet("SearchButton");
                SearchButtonImage = currentSearchButton.GetComponent<Image>();
                if (SearchButtonImage != null)
                {
                    SearchButtonImage.color = Color.red;
                }
            }

            UpdateGlobalPosition();
            FindGridWindow(globalPosition);
            // OnMove
            ControllerUIOnMove(Vector2Int.zero, globalPosition);
        }
        public void ControllerUIMoveSnapshot()
        {
            snapshotGridView = currentGridView;
            snapshotModSlotView = currentModSlotView;
            snapshotTradingTableGridView = currentTradingTableGridView;
            snapshotContainedGridsView = currentContainedGridsView;
            snapshotItemSpecificationPanel = currentItemSpecificationPanel;
            snapshotEquipmentSlotView = currentEquipmentSlotView;
            snapshotWeaponsSlotView = currentWeaponsSlotView;
            snapshotArmbandSlotView = currentArmbandSlotView;
            snapshotContainersSlotView = currentContainersSlotView;
            snapshotDogtagSlotView = currentDogtagSlotView;
            snapshotSpecialSlotSlotView = currentSpecialSlotSlotView;
            snapshotSearchButton = currentSearchButton;
            SnapshotGridViewLocation = gridViewLocation;
        }
        public void ControllerUIMoveToSnapshot()
        {
            currentGridView = snapshotGridView;
            currentModSlotView = snapshotModSlotView;
            currentTradingTableGridView = snapshotTradingTableGridView;
            currentContainedGridsView = snapshotContainedGridsView;
            currentItemSpecificationPanel = snapshotItemSpecificationPanel;
            currentEquipmentSlotView = snapshotEquipmentSlotView;
            currentWeaponsSlotView = snapshotWeaponsSlotView;
            currentArmbandSlotView = snapshotArmbandSlotView;
            currentContainersSlotView = snapshotContainersSlotView;
            currentDogtagSlotView = snapshotDogtagSlotView;
            currentSpecialSlotSlotView = snapshotSpecialSlotSlotView;
            currentSearchButton = snapshotSearchButton;
            gridViewLocation = SnapshotGridViewLocation;
            UpdateGlobalPosition();
            FindGridWindow(globalPosition);
            // OnMove
            ControllerUIOnMove(Vector2Int.zero, globalPosition);
        }
        private void ControllerUIOnMove(Vector2Int direction, Vector2 position)
        {
            FindScrollRectNoDrag(position);
            ControllerOnPointerMove();
            ControllerOnDrag();
            UpdateSelector();
        }
        public void UpdateGlobalPosition()
        {
            switch (GetControllerCurrentUI())
            {
                case ERaidPadCurrentUI.GridView:
                    globalPosition.x = currentGridView.transform.position.x + (GridSize * gridViewLocation.x) - (GridSize / 2f);
                    globalPosition.y = currentGridView.transform.position.y - (GridSize * gridViewLocation.y) + (GridSize / 2f);
                    //globalSize = new Vector2(GridSize, GridSize) * ScreenRatio;
                    break;
                case ERaidPadCurrentUI.TradingTableGridView:
                    Vector2 size = currentTradingTableGridView.GetComponent<RectTransform>().sizeDelta * ScreenRatio;
                    globalPosition.x = (currentTradingTableGridView.transform.position.x - (size.x / 2f)) + (GridSize * gridViewLocation.x) - (GridSize / 2f);
                    globalPosition.y = (currentTradingTableGridView.transform.position.y + (size.y / 2f)) - (GridSize * gridViewLocation.y) + (GridSize / 2f);
                    //globalSize = new Vector2(GridSize, GridSize) * ScreenRatio;
                    break;
                case ERaidPadCurrentUI.ModSlotView:
                    globalPosition = currentModSlotView.transform.position;
                    //globalSize = new Vector2(GridSize, GridSize) * ScreenRatio;
                    break;
                case ERaidPadCurrentUI.EquipmentSlotView:
                    globalPosition = new Vector2(currentEquipmentSlotView.transform.position.x + (SlotSize / 2f), currentEquipmentSlotView.transform.position.y - (SlotSize / 2f));
                    //globalSize = new Vector2(SlotSize, SlotSize) * ScreenRatio;
                    break;
                case ERaidPadCurrentUI.WeaponsSlotView:
                    globalPosition = new Vector2(currentWeaponsSlotView.transform.position.x + (157.0811f * ScreenRatio) - 2f, currentWeaponsSlotView.transform.position.y - (SlotSize / 2f));
                    //globalSize = new Vector2(157.0811f * ScreenRatio * 2f, SlotSize) * ScreenRatio;
                    break;
                case ERaidPadCurrentUI.ArmbandSlotView:
                    globalPosition = new Vector2(currentArmbandSlotView.transform.position.x + (SlotSize / 2f), currentArmbandSlotView.transform.position.y - (32f * ScreenRatio));
                    //globalSize = new Vector2(SlotSize, 32f * ScreenRatio * 2f) * ScreenRatio;
                    break;
                case ERaidPadCurrentUI.ContainersSlotView:
                    globalPosition = new Vector2(currentContainersSlotView.transform.position.x + (SlotSize / 2f), currentContainersSlotView.transform.position.y - (SlotSize / 2f));
                    //globalSize = new Vector2(SlotSize, SlotSize) * ScreenRatio;
                    break;
                case ERaidPadCurrentUI.DogtagSlotView:
                    globalPosition = new Vector2(currentDogtagSlotView.transform.position.x - GridSize - (GridSize / 2f), currentDogtagSlotView.transform.position.y - (GridSize / 2f));
                    //globalSize = new Vector2(GridSize, GridSize) * ScreenRatio;
                    break;
                case ERaidPadCurrentUI.SpecialSlotSlotView:
                    globalPosition.x = currentSpecialSlotSlotView.transform.position.x + (GridSize / 2f);
                    globalPosition.y = currentSpecialSlotSlotView.transform.position.y - (GridSize / 2f);
                    //globalSize = new Vector2(GridSize, GridSize) * ScreenRatio;
                    break;
                case ERaidPadCurrentUI.SearchButton:
                    globalPosition.x = currentSearchButton.transform.position.x;
                    globalPosition.y = currentSearchButton.transform.position.y;
                    //globalSize = Vector2.zero;
                    break;
                case ERaidPadCurrentUI.ContextMenuButton:
                    globalPosition.x = currentContextMenuButton.transform.position.x;
                    globalPosition.y = currentContextMenuButton.transform.position.y;
                    //globalSize = Vector2.zero;
                    break;
            }
        }
        public ERaidPadCurrentUI GetControllerCurrentUI()
        {
            if ((currentContextMenuButton != null && currentContextMenuButton.gameObject.activeSelf))
            {
                return ERaidPadCurrentUI.ContextMenuButton;
            }
            else if ((currentGridView != null && currentGridView.gameObject.activeSelf))
            {
                return ERaidPadCurrentUI.GridView;
            }
            else if ((currentTradingTableGridView != null && currentTradingTableGridView.gameObject.activeSelf))
            {
                return ERaidPadCurrentUI.TradingTableGridView;
            }
            else if ((currentModSlotView != null && currentModSlotView.gameObject.activeSelf))
            {
                return ERaidPadCurrentUI.ModSlotView;
            }
            else if ((currentEquipmentSlotView != null && currentEquipmentSlotView.gameObject.activeSelf))
            {
                return ERaidPadCurrentUI.EquipmentSlotView;
            }
            else if ((currentWeaponsSlotView != null && currentWeaponsSlotView.gameObject.activeSelf))
            {
                return ERaidPadCurrentUI.WeaponsSlotView;
            }
            else if ((currentArmbandSlotView != null && currentArmbandSlotView.gameObject.activeSelf))
            {
                return ERaidPadCurrentUI.ArmbandSlotView;
            }
            else if ((currentContainersSlotView != null && currentContainersSlotView.gameObject.activeSelf))
            {
                return ERaidPadCurrentUI.ContainersSlotView;
            }
            else if ((currentDogtagSlotView != null && currentDogtagSlotView.gameObject.activeSelf))
            {
                return ERaidPadCurrentUI.DogtagSlotView;
            }
            else if ((currentSpecialSlotSlotView != null && currentSpecialSlotSlotView.gameObject.activeSelf))
            {
                return ERaidPadCurrentUI.SpecialSlotSlotView;
            }
            else if ((currentSearchButton != null && currentSearchButton.gameObject.activeSelf))
            {
                return ERaidPadCurrentUI.SearchButton;
            }
            return ERaidPadCurrentUI.None;
        }
        public void ControllerUISelect()
        {
            ResetAllCurrent();
            gridViewLocation = Vector2Int.one;
            UpdateGlobalPosition();
            FindGridWindow(globalPosition);
            ControllerUIOnMove(Vector2Int.zero, globalPosition);
        }
        public void ControllerUISelect(GridView gridView, ItemView itemView)
        {
            ResetAllCurrent();
            currentGridView = gridView;
            gridViewLocation = CalculateItemLocation(gridView, itemView) + Vector2Int.one;
            UpdateGlobalPosition();
            FindGridWindow(globalPosition);
            ControllerUIOnMove(Vector2Int.zero, globalPosition);
        }
        public void ControllerUISelect(GridView gridView)
        {
            ResetAllCurrent();
            currentGridView = gridView;
            gridViewLocation = Vector2Int.one;
            UpdateGlobalPosition();
            FindGridWindow(globalPosition);
            ControllerUIOnMove(Vector2Int.zero, globalPosition);
        }
        public void ControllerUISelect(ContextMenuButton contextMenuButton)
        {
            if ((currentContextMenuButton != null && currentContextMenuButton.gameObject.activeSelf))
            {
                currentContextMenuButton.OnPointerExit(null);
            }
            currentContextMenuButton = contextMenuButton;
            currentContextMenuButton.OnPointerEnter(null);
        }
        public Vector2Int CalculateItemLocation(GridView gridView, ItemView itemView)
        {
            int GridWidth = gridView.Grid.GridWidth;
            int GridHeight = gridView.Grid.GridHeight;

            RectTransform rectTransform = gridView.transform.GetComponent<RectTransform>();
            Vector2 size = rectTransform.rect.size;
            Vector2 pivot = rectTransform.pivot;
            Vector2 b = size * pivot;
            Vector2 vector = rectTransform.InverseTransformPoint((Vector2)itemView.transform.position + new Vector2(0f,-64f));
            vector += b;

            object XYCellSizeStruct = CalculateRotatedSize.Invoke(itemView.Item, new object[1] { itemView.ItemRotation });

            vector /= 63f;
            vector.y = (float)GridHeight - vector.y;

            vector.y -= (float)Traverse.Create(XYCellSizeStruct).Field("Y").GetValue<int>();

            return new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(vector.x), 0, GridWidth), Mathf.Clamp(Mathf.RoundToInt(vector.y), 0, GridHeight));
        }
        public void UpdateSelector()
        {
            if (SelectedGameObject == null && InRaid)
            {
                SelectedGameObject = new GameObject("SelectorGameObject");
                if (SelectedGameObject != null)
                {
                    SelectedRectTransform = SelectedGameObject.AddComponent<RectTransform>();
                    if (SelectedRectTransform != null)
                    {
                        SelectedRectTransform.sizeDelta = new Vector2(GridSize, GridSize);
                        SelectedImage = SelectedGameObject.AddComponent<Image>();
                        if (SelectedImage != null)
                        {
                            SelectedImage.sprite = LoadedSprites["Grid.png"];
                            SelectedImage.raycastTarget = false;
                            SelectedImage.color = RaidPadPlugin.SelectColor.Value;
                        }
                        SelectedLayoutElement = SelectedGameObject.AddComponent<LayoutElement>();
                        if (SelectedLayoutElement != null)
                        {
                            SelectedLayoutElement.ignoreLayout = true;
                        }
                    }
                }
            }
            else if (SelectedGameObject != null && !InRaid)
            {
                Destroy(SelectedGameObject);
            }
            if (SelectedGameObject != null && SelectedRectTransform != null && SelectedImage != null)
            {
                SelectedGameObject.SetActive(false);
                SelectedGameObject.transform.SetParent(null);

                if ((currentGridView != null && currentGridView.gameObject.activeSelf) && currentGridView.gameObject.activeSelf)
                {
                    SelectedImage.sprite = LoadedSprites["Grid.png"];
                    globalSize = new Vector2(GridSize, GridSize);// * ScreenRatio;
                    SelectedGameObject.SetActive(true);
                    SelectedRectTransform.SetParent(currentGridView.transform);
                    SelectedRectTransform.sizeDelta = globalSize;
                    SelectedRectTransform.localPosition = Vector2.zero;
                    SelectedRectTransform.position = globalPosition;
                }
                else if ((currentTradingTableGridView != null && currentTradingTableGridView.gameObject.activeSelf))
                {
                    SelectedImage.sprite = LoadedSprites["Grid.png"];
                    globalSize = new Vector2(GridSize, GridSize);// * ScreenRatio;
                    SelectedGameObject.SetActive(true);
                    SelectedRectTransform.SetParent(tradingTableGridView.transform);
                    SelectedRectTransform.sizeDelta = globalSize;
                    SelectedRectTransform.localPosition = Vector2.zero;
                    SelectedRectTransform.position = globalPosition;
                }
                else if ((currentModSlotView != null && currentModSlotView.gameObject.activeSelf))
                {
                    SelectedImage.sprite = LoadedSprites["Grid.png"];
                    globalSize = new Vector2(GridSize, GridSize);// * ScreenRatio;
                    SelectedGameObject.SetActive(true);
                    SelectedRectTransform.SetParent(currentModSlotView.transform);
                    SelectedRectTransform.sizeDelta = globalSize;
                    SelectedRectTransform.localPosition = Vector2.zero;
                    SelectedRectTransform.position = globalPosition;
                }
                else if ((currentEquipmentSlotView != null && currentEquipmentSlotView.gameObject.activeSelf))
                {
                    SelectedImage.sprite = LoadedSprites["Slot.png"];
                    globalSize = new Vector2(SlotSize, SlotSize);// * ScreenRatio;
                    SelectedGameObject.SetActive(true);
                    RectTransform slotPlace = Traverse.Create(currentEquipmentSlotView).Field("_slotPlace").GetValue<RectTransform>();
                    SelectedRectTransform.SetParent(slotPlace);
                    SelectedRectTransform.sizeDelta = globalSize;
                    SelectedRectTransform.localPosition = Vector2.zero;
                    SelectedRectTransform.position = globalPosition;
                }
                else if ((currentWeaponsSlotView != null && currentWeaponsSlotView.gameObject.activeSelf))
                {
                    SelectedImage.sprite = LoadedSprites["WeaponSlot.png"];
                    globalSize = new Vector2(157.0811f * ScreenRatio * 2f, SlotSize);// * ScreenRatio;
                    SelectedGameObject.SetActive(true);
                    SelectedRectTransform.SetParent(currentWeaponsSlotView.transform);
                    SelectedRectTransform.sizeDelta = globalSize;
                    SelectedRectTransform.localPosition = Vector2.zero;
                    SelectedRectTransform.position = globalPosition;
                }
                else if ((currentArmbandSlotView != null && currentArmbandSlotView.gameObject.activeSelf))
                {
                    SelectedImage.sprite = LoadedSprites["ArmbandSlot.png"];
                    globalSize = new Vector2(SlotSize, 32f * ScreenRatio * 2f);// * ScreenRatio;
                    SelectedGameObject.SetActive(true);
                    SelectedRectTransform.SetParent(currentArmbandSlotView.transform);
                    SelectedRectTransform.sizeDelta = globalSize;
                    SelectedRectTransform.localPosition = Vector2.zero;
                    SelectedRectTransform.position = globalPosition;
                }
                else if ((currentContainersSlotView != null && currentContainersSlotView.gameObject.activeSelf))
                {
                    SelectedImage.sprite = LoadedSprites["Slot.png"];
                    globalSize = new Vector2(SlotSize, SlotSize);// * ScreenRatio;
                    SelectedGameObject.SetActive(true);
                    RectTransform slotPlace = Traverse.Create(currentContainersSlotView).Field("_slotPlace").GetValue<RectTransform>();
                    SelectedRectTransform.SetParent(slotPlace);
                    SelectedRectTransform.sizeDelta = globalSize;
                    SelectedRectTransform.localPosition = Vector2.zero;
                    SelectedRectTransform.position = globalPosition;
                }
                else if ((currentDogtagSlotView != null && currentDogtagSlotView.gameObject.activeSelf))
                {
                    SelectedImage.sprite = LoadedSprites["Grid.png"];
                    globalSize = new Vector2(GridSize, GridSize);// * ScreenRatio;
                    SelectedGameObject.SetActive(true);
                    SelectedRectTransform.SetParent(currentDogtagSlotView.transform);
                    SelectedRectTransform.sizeDelta = globalSize;
                    SelectedRectTransform.localPosition = Vector2.zero;
                    SelectedRectTransform.position = globalPosition;
                }
                else if ((currentSpecialSlotSlotView != null && currentSpecialSlotSlotView.gameObject.activeSelf))
                {
                    SelectedImage.sprite = LoadedSprites["Grid.png"];
                    globalSize = new Vector2(GridSize, GridSize);// * ScreenRatio;
                    SelectedGameObject.SetActive(true);
                    SelectedRectTransform.SetParent(currentSpecialSlotSlotView.transform);
                    SelectedRectTransform.sizeDelta = globalSize;
                    SelectedRectTransform.localPosition = Vector2.zero;
                    SelectedRectTransform.position = globalPosition;
                }
            }
        }
        public EInventoryTab CurrentTab()
        {
            if (Tabs != null)
            {
                foreach (KeyValuePair<EInventoryTab, Tab> Tab in Tabs)
                {
                    if (Traverse.Create(Tab.Value).Field("_uiSelected").GetValue<bool>()) return Tab.Key;
                }
            }
            return EInventoryTab.Unchanged;
        }

        // UI Pointer
        public void ControllerOnPointerMove()
        {
            if (pointerEventData != null)
            {
                pointerEventData.position = globalPosition;
                List<RaycastResult> results = new List<RaycastResult>();
                eventSystem.RaycastAll(pointerEventData, results);
                if (onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf)
                {
                    onPointerEnterItemView.OnPointerExit(pointerEventData);
                }
                if (results.Count > 0)
                {
                    pointerEventData.pointerEnter = results[0].gameObject;
                    if (results[0].gameObject.GetComponentInParent<QuickSlotItemView>() == null)
                    {
                        onPointerEnterItemView = results[0].gameObject.GetComponentInParent<ItemView>();
                        if (onPointerEnterItemView == null)
                        {
                            onPointerEnterItemView = results[0].gameObject.GetComponentInParent<GridItemView>();
                        }
                        if (onPointerEnterItemView == null)
                        {
                            onPointerEnterItemView = results[0].gameObject.GetComponentInParent<SlotItemView>();
                        }
                        if (onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf)
                        {
                            onPointerEnterItemView.OnPointerEnter(pointerEventData);
                        }
                    }
                }
                else
                {
                    pointerEventData.pointerEnter = null;
                    onPointerEnterItemView = null;
                }
                UpdateControllerBlocks();
            }
        }

        // UI Inputs
        private void ControllerUse(bool Hold)
        {
            if (onPointerEnterItemView == null || (onPointerEnterItemView != null && !onPointerEnterItemView.gameObject.activeSelf))
            {
                ControllerOnPointerMove();
            }
            if (pointerEventData != null && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf)
            {
                pointerEventData.position = globalPosition;
                ItemUiContext ItemUiContext = ItemUiContext.Instance;
                object NewContextInteractionsObject = Traverse.Create(onPointerEnterItemView).Property("NewContextInteractions").GetValue();
                if (NewContextInteractionsObject != null)
                {
                    if (ExecuteInteraction == null)
                    {
                        ExecuteInteraction = NewContextInteractionsObject.GetType().GetMethod("ExecuteInteraction", BindingFlags.Instance | BindingFlags.Public);
                    }
                    if (IsInteractionAvailable == null)
                    {
                        IsInteractionAvailable = NewContextInteractionsObject.GetType().GetMethod("IsInteractionAvailable", BindingFlags.Instance | BindingFlags.Public);
                    }
                }
                if (!onPointerEnterItemView.IsSearched && ExecuteMiddleClick != null && (bool)ExecuteMiddleClick.Invoke(onPointerEnterItemView, null)) return;
                if (!ItemUiContext || !onPointerEnterItemView.IsSearched) return;
                ItemController itemController = Traverse.Create(onPointerEnterItemView).Field("ItemController").GetValue<ItemController>();
                if (ExecuteInteraction != null && IsInteractionAvailable != null)
                {
                    if (onPointerEnterItemView.Item is FoodDrink || onPointerEnterItemView.Item is Meds)
                    {
                        ExecuteInteractionInvokeParameters[0] = EItemInfoButton.Use;
                        if (!(bool)ExecuteInteraction.Invoke(NewContextInteractionsObject, ExecuteInteractionInvokeParameters))
                        {
                            ExecuteInteractionInvokeParameters[0] = EItemInfoButton.UseAll;
                            ExecuteInteraction.Invoke(NewContextInteractionsObject, ExecuteInteractionInvokeParameters);
                        }
                        return;
                    }
                    if (onPointerEnterItemView.Item.IsContainer && !Hold)
                    {
                        ExecuteInteractionInvokeParameters[0] = EItemInfoButton.Open;
                        if ((bool)ExecuteInteraction.Invoke(NewContextInteractionsObject, ExecuteInteractionInvokeParameters)) return;
                    }
                    if (Hold && ExecuteMiddleClick != null && (bool)ExecuteMiddleClick.Invoke(onPointerEnterItemView, null)) return;
                    SimpleTooltip tooltip = ItemUiContext.Tooltip;
                    IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.Equip;
                    if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed)
                    {
                        ItemUiContext.EquipItem(onPointerEnterItemView.Item).HandleExceptions();
                        if (tooltip != null)
                        {
                            tooltip.Close();
                        }
                        ControllerOnPointerMove();
                        return;
                    }
                    else
                    {
                        bool IsBeingLoadedMagazine = Traverse.Create(Traverse.Create(onPointerEnterItemView).Property("IsBeingLoadedMagazine").GetValue<object>()).Field("CurrentState").GetValue<bool>();
                        bool IsBeingUnloadedMagazine = Traverse.Create(Traverse.Create(onPointerEnterItemView).Property("IsBeingUnloadedMagazine").GetValue<object>()).Field("CurrentState").GetValue<bool>();
                        if (IsBeingLoadedMagazine || IsBeingUnloadedMagazine)
                        {
                            itemController.StopProcesses();
                            return;
                        }
                    }
                    ExecuteInteractionInvokeParameters[0] = EItemInfoButton.CheckMagazine;
                    if ((bool)ExecuteInteraction.Invoke(NewContextInteractionsObject, ExecuteInteractionInvokeParameters)) return;
                    if (ExecuteMiddleClick != null && (bool)ExecuteMiddleClick.Invoke(onPointerEnterItemView, null)) return;
                }
            }
        }
        private void ControllerQuickMove()
        {
            if (onPointerEnterItemView == null || (onPointerEnterItemView != null && !onPointerEnterItemView.gameObject.activeSelf))
            {
                ControllerOnPointerMove();
            }
            if (pointerEventData != null && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf)
            {
                pointerEventData.position = globalPosition;
                ItemUiContext ItemUiContext = ItemUiContext.Instance;
                if (ItemUiContext == null || !onPointerEnterItemView.IsSearched) return;
                ItemController ItemController = Traverse.Create(onPointerEnterItemView).Field("ItemController").GetValue<ItemController>();
                SimpleTooltip tooltip = ItemUiContext.Tooltip;
                object ItemContext = Traverse.Create(onPointerEnterItemView).Property("ItemContext").GetValue<object>();
                if (ItemContext != null)
                {
                    object gstructObject = QuickFindAppropriatePlace.Invoke(ItemUiContext, new object[5] { ItemContext, ItemController, false, true, true });
                    if (gstructObject != null)
                    {
                        bool Failed = Traverse.Create(gstructObject).Property("Failed").GetValue<bool>();
                        if (Failed) return;
                        object Value = Traverse.Create(gstructObject).Field("Value").GetValue<object>();
                        if (Value != null)
                        {
                            if (!(bool)CanExecute.Invoke(ItemController, new object[1] { Value }))
                            {
                                return;
                            }
                            bool ItemsDestroyRequired = Traverse.Create(Value).Property("ItemsDestroyRequired").GetValue<bool>();
                            if (ItemsDestroyRequired)
                            {
                                NotificationManager.DisplayWarningNotification("DiscardLimit", ENotificationDurationType.Default);
                                return;
                            }
                            string itemSound = onPointerEnterItemView.Item.ItemSound;
                            RunNetworkTransaction.Invoke(ItemController, new object[2] { Value, null });
                            if (tooltip != null)
                            {
                                tooltip.Close();
                            }
                            Singleton<GUISounds>.Instance.PlayItemSound(itemSound, EInventorySoundType.pickup, false);
                            ControllerOnPointerMove();
                        }
                    }
                }
            }
        }
        private void ControllerDiscard()
        {
            if (onPointerEnterItemView == null || (onPointerEnterItemView != null && !onPointerEnterItemView.gameObject.activeSelf))
            {
                ControllerOnPointerMove();
            }
            if (pointerEventData != null && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf)
            {
                pointerEventData.position = globalPosition;
                ItemUiContext ItemUiContext = ItemUiContext.Instance;
                if (ItemUiContext == null || !onPointerEnterItemView.IsSearched) return;
                object NewContextInteractionsObject = Traverse.Create(onPointerEnterItemView).Property("NewContextInteractions").GetValue();
                if (NewContextInteractionsObject != null)
                {
                    if (IsInteractionAvailable == null)
                    {
                        IsInteractionAvailable = NewContextInteractionsObject.GetType().GetMethod("IsInteractionAvailable", BindingFlags.Instance | BindingFlags.Public);
                    }
                }
                if (IsInteractionAvailable != null)
                {
                    SimpleTooltip tooltip = ItemUiContext.Tooltip;
                    IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.Discard;
                    if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed)
                    {
                        ItemUiContext.ThrowItem(onPointerEnterItemView.Item).HandleExceptions();
                        if (tooltip != null)
                        {
                            tooltip.Close();
                        }
                        ControllerOnPointerMove();
                        return;
                    }
                }
            }
        }

        private void ControllerBeginDrag()
        {
            if (onPointerEnterItemView == null || (onPointerEnterItemView != null && !onPointerEnterItemView.gameObject.activeSelf))
            {
                ControllerOnPointerMove();
            }
            if (!Dragging && pointerEventData != null && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf)
            {
                ItemController ItemController = Traverse.Create(onPointerEnterItemView).Field("ItemController").GetValue<ItemController>();
                if (ItemController != null)
                {
                    bool IsBeingLoadedMagazine = Traverse.Create(Traverse.Create(onPointerEnterItemView).Property("IsBeingLoadedMagazine").GetValue<object>()).Field("CurrentState").GetValue<bool>();
                    bool IsBeingUnloadedMagazine = Traverse.Create(Traverse.Create(onPointerEnterItemView).Property("IsBeingUnloadedMagazine").GetValue<object>()).Field("CurrentState").GetValue<bool>();
                    if (IsBeingLoadedMagazine || IsBeingUnloadedMagazine)
                    {
                        ItemController.StopProcesses();
                        return;
                    }
                }
                ControllerUIMoveSnapshot();
                pointerEventData.position = globalPosition;
                pointerEventData.dragging = false;
                DraggingItemView = onPointerEnterItemView;
                DraggingItemView.OnBeginDrag(pointerEventData);
                pointerEventData.dragging = true;
                Dragging = true;
                EnableSet("OnDrag");
                ControllerOnPointerMove();
                ControllerOnDrag();
            }
        }
        private void ControllerOnDrag()
        {
            if (Dragging && pointerEventData != null)
            {
                pointerEventData.position = globalPosition;
                if (DraggingItemView != null && DraggingItemView.gameObject.activeSelf && DraggingItemView.BeingDragged)
                {
                    DraggingItemView.OnDrag(pointerEventData);
                }
                else if ((DraggingItemView != null && !DraggingItemView.BeingDragged) || !DraggingItemView.gameObject.activeSelf)
                {
                    ControllerCancelDrag();
                }
            }
        }
        private void ControllerEndDrag()
        {
            if (Dragging && pointerEventData != null)
            {
                pointerEventData.position = globalPosition;
                if (pointerEventData.pointerEnter != null)
                {
                    Dragging = false;
                    pointerEventData.dragging = false;
                    if (DraggingItemView != null && DraggingItemView.gameObject.activeSelf)
                    {
                        DraggingItemView.OnEndDrag(pointerEventData);
                        DraggingItemView = null;
                        ControllerOnPointerMove();
                    }
                    DisableSet("OnDrag");
                }
                else
                {
                    ControllerCancelDrag();
                }
            }
        }
        public void ControllerCancelDrag()
        {
            if (Dragging)
            {
                Dragging = false;
                PointerEventData pointerEventData = new PointerEventData(eventSystem);
                pointerEventData.button = PointerEventData.InputButton.Left;
                pointerEventData.position = globalPosition;
                pointerEventData.pointerEnter = null;
                pointerEventData.dragging = false;
                if (DraggingItemView != null && DraggingItemView.gameObject.activeSelf)
                {
                    DraggingItemView.OnEndDrag(pointerEventData);
                    DraggingItemView = null;
                    ControllerUIMoveToSnapshot();
                }
                DisableSet("OnDrag");
            }
        }
        private void ControllerRotateDragged()
        {
            if (Dragging && DraggingItemView != null && DraggingItemView.gameObject.activeSelf)
            {
                DraggedItemView DraggedItemView = Traverse.Create(DraggingItemView).Property("DraggedItemView").GetValue<DraggedItemView>();
                if (DraggedItemView != null)
                {
                    object ItemContext = Traverse.Create(DraggedItemView).Property("ItemContext").GetValue<object>();
                    if (ItemContext != null)
                    {
                        ItemRotation ItemRotation = Traverse.Create(ItemContext).Field("ItemRotation").GetValue<ItemRotation>();
                        DraggedItemViewMethodRotateItem.Invoke(DraggedItemView, new object[1] { (ItemRotation == ItemRotation.Horizontal ? ItemRotation.Vertical : ItemRotation.Horizontal) });
                        ControllerOnDrag();
                    }
                }
            }
        }
        /// <summary>
        /// Rotate whatever the menu cursor is dragging. Out of raid there is no tracked
        /// DraggedItemView, so find the live one instead.
        /// </summary>
        private void ControllerRotateMenuDragged()
        {
            try
            {
                DraggedItemView draggedItemView = FindObjectOfType<DraggedItemView>();
                if (draggedItemView == null || !draggedItemView.gameObject.activeInHierarchy) return;

                object ItemContext = Traverse.Create(draggedItemView).Property("ItemContext").GetValue<object>();
                if (ItemContext == null) return;

                ItemRotation ItemRotation = Traverse.Create(ItemContext).Field("ItemRotation").GetValue<ItemRotation>();
                DraggedItemViewMethodRotateItem.Invoke(draggedItemView, new object[1] { (ItemRotation == ItemRotation.Horizontal ? ItemRotation.Vertical : ItemRotation.Horizontal) });
            }
            catch
            {
            }
        }
        private void ControllerSplitDragged()
        {
            if (Dragging && pointerEventData != null)
            {
                RaidPadPlugin.LeftControl.Enable();
                pointerEventData.position = globalPosition;
                if (pointerEventData.pointerEnter != null)
                {
                    Dragging = false;
                    pointerEventData.dragging = false;
                    if (DraggingItemView != null && DraggingItemView.gameObject.activeSelf)
                    {
                        DraggingItemView.OnEndDrag(pointerEventData);
                        DraggingItemView = null;
                        ControllerOnPointerMove();
                    }
                    DisableSet("OnDrag");
                }
                else
                {
                    ControllerCancelDrag();
                }
                RaidPadPlugin.LeftControl.Disable();
            }
        }

        private void ControllerSearch()
        {
            if (Interface && (currentSearchButton != null && currentSearchButton.gameObject.activeSelf))
            {
                ButtonPress.Invoke(currentSearchButton, null);
            }
            else
            {
                DisableSet("SearchButton");
                if (SearchButtonImage != null)
                {
                    SearchButtonImage.color = Color.white;
                    SearchButtonImage = null;
                }
            }
        }
        private void ControllerShowContextMenu()
        {
            if (!ContextMenu && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf)
            {
                UpdateGlobalPosition();
                ShowContextMenuInvokeParameters[0] = globalPosition;
                ShowContextMenu.Invoke(onPointerEnterItemView, ShowContextMenuInvokeParameters);
            }
        }
        private void ControllerContextMenuUse()
        {
            if ((currentContextMenuButton != null && currentContextMenuButton.gameObject.activeSelf))
            {
                Button _button = Traverse.Create(currentContextMenuButton).Field("_button").GetValue<Button>();
                if (_button != null)
                {
                    _button.onClick.Invoke();
                }
            }
        }
        private void ControllerInterfaceBind(EBoundItem bindIndex)
        {
            if (onPointerEnterItemView == null || (onPointerEnterItemView != null && !onPointerEnterItemView.gameObject.activeSelf))
            {
                ControllerOnPointerMove();
            }
            if (pointerEventData != null && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf)
            {
                pointerEventData.position = globalPosition;
                ItemUiContext itemUiContext = ItemUiContext.Instance;
                if (itemUiContext && onPointerEnterItemView.Item != null && ItemUIContextBindItem != null)
                {
                    ItemUIContextInvokeParameters[0] = onPointerEnterItemView.Item;
                    ItemUIContextInvokeParameters[1] = bindIndex;
                    ItemUIContextBindItem.Invoke(itemUiContext, ItemUIContextInvokeParameters);
                }
            }
        }
        private void ControllerSplitDialogAccept()
        {
            if (splitDialog != null)
            {
                splitDialog.Accept();
            }
        }
        private void ControllerSplitDialogAdd(int Value)
        {
            if (splitDialog != null)
            {
                IntSlider _intSlider = Traverse.Create(splitDialog).Field("_intSlider").GetValue<IntSlider>();
                if (_intSlider != null && _intSlider.gameObject.activeSelf)
                {
                    lastIntSliderValue = Value;
                    SplitDialogAutoMove = true;
                    int minValue = Traverse.Create(_intSlider).Field("_minValue").GetValue<int>() - 1;
                    _intSlider.UpdateValue((_intSlider.CurrentValue() + minValue) + ((LB || RB) ? Value * 10 : Value));
                }
                StepSlider _stepSlider = Traverse.Create(splitDialog).Field("_stepSlider").GetValue<StepSlider>();
                if (_stepSlider != null && _stepSlider.gameObject.activeSelf)
                {
                    lastIntSliderValue = Value;
                    SplitDialogAutoMove = true;
                    //Temp
                    int minValue = Traverse.Create(_stepSlider).Field("_minValue").GetValue<int>();
                    int maxValue = Traverse.Create(_stepSlider).Field("_maxValue").GetValue<int>();
                    _stepSlider.Show(minValue, maxValue, (int)_stepSlider.CurrentValue() + ((LB || RB) ? Value * 10 : Value));
                }
            }
        }
        private void ControllerScroll(float Value)
        {
            float height = currentScrollRectNoDrag.content.rect.height * ScreenRatio;
            Vector2 position = new Vector2(currentScrollRectNoDragRectTransform.position.x + (currentScrollRectNoDragRectTransform.rect.x * ScreenRatio), currentScrollRectNoDragRectTransform.position.y - ((currentScrollRectNoDragRectTransform.rect.height * (currentScrollRectNoDragRectTransform.pivot.y - 1f)) * ScreenRatio));
            currentScrollRectNoDrag.verticalNormalizedPosition = currentScrollRectNoDrag.verticalNormalizedPosition + ((((Value * 1000f) / height) / height) * 10000f * Time.deltaTime * RaidPadPlugin.ScrollSensitivity.Value);
            UpdateGlobalPosition();
            if ((globalPosition.y + (GridSize / 2f)) > position.y)
            {
                ControllerUIMove(new Vector2Int(0, -1), false);
            }
            else if ((globalPosition.y - (GridSize / 2f)) < (position.y - (currentScrollRectNoDragRectTransform.rect.height * ScreenRatio)))
            {
                ControllerUIMove(new Vector2Int(0, 1), false);
            }
        }
        private void ControllerAutoScroll()
        {
            float height = currentScrollRectNoDrag.content.rect.height;
            Vector2 position = new Vector2(currentScrollRectNoDragRectTransform.position.x + (currentScrollRectNoDragRectTransform.rect.x * ScreenRatio), currentScrollRectNoDragRectTransform.position.y - ((currentScrollRectNoDragRectTransform.rect.height * (currentScrollRectNoDragRectTransform.pivot.y - 1f)) * ScreenRatio));
            if (globalPosition.x > position.x && globalPosition.x < (position.x + (currentScrollRectNoDragRectTransform.rect.width * ScreenRatio)))
            {
                if ((globalPosition.y + (GridSize / 2f)) > position.y)
                {
                    currentScrollRectNoDrag.verticalNormalizedPosition = currentScrollRectNoDrag.verticalNormalizedPosition + (((1000f / height) / height) * 10000f * Time.deltaTime * RaidPadPlugin.ScrollSensitivity.Value);
                    UpdateGlobalPosition();
                    if (!((globalPosition.y + (GridSize / 2f)) > position.y) && !((globalPosition.y - (GridSize / 2f)) < (position.y - (currentScrollRectNoDragRectTransform.rect.height * ScreenRatio))))
                    {
                        ControllerUIOnMove(Vector2Int.zero, globalPosition);
                    }
                }
                else if ((globalPosition.y - (GridSize / 2f)) < (position.y - (currentScrollRectNoDragRectTransform.rect.height * ScreenRatio)))
                {
                    currentScrollRectNoDrag.verticalNormalizedPosition = currentScrollRectNoDrag.verticalNormalizedPosition + (((-1000f / height) / height) * 10000f * Time.deltaTime * RaidPadPlugin.ScrollSensitivity.Value);
                    UpdateGlobalPosition();
                    if (!((globalPosition.y + (GridSize / 2f)) > position.y) && !((globalPosition.y - (GridSize / 2f)) < (position.y - (currentScrollRectNoDragRectTransform.rect.height * ScreenRatio))))
                    {
                        ControllerUIOnMove(Vector2Int.zero, globalPosition);
                    }
                }
            }
        }
        private void ControllerNextTab()
        {
            Tab tab = null;
            switch (CurrentTab())
            {
                case EInventoryTab.Overall:
                    if (Tabs.ContainsKey(EInventoryTab.Gear)) tab = Tabs[EInventoryTab.Gear];
                    break;
                case EInventoryTab.Gear:
                    if (Tabs.ContainsKey(EInventoryTab.Health)) tab = Tabs[EInventoryTab.Health];
                    break;
                case EInventoryTab.Health:
                    if (Tabs.ContainsKey(EInventoryTab.Skills)) tab = Tabs[EInventoryTab.Skills];
                    break;
                case EInventoryTab.Skills:
                    if (Tabs.ContainsKey(EInventoryTab.Map)) tab = Tabs[EInventoryTab.Map];
                    break;
                case EInventoryTab.Map:
                    if (Tabs.ContainsKey(EInventoryTab.Notes)) tab = Tabs[EInventoryTab.Notes];
                    break;
                case EInventoryTab.Notes:
                    if (Tabs.ContainsKey(EInventoryTab.Overall)) tab = Tabs[EInventoryTab.Overall];
                    break;
            }
            if (tab != null)
            {
                tab.OnPointerClick(null);
            }
        }
        private void ControllerPreviousTab()
        {
            Tab tab = null;
            switch (CurrentTab())
            {
                case EInventoryTab.Overall:
                    if (Tabs.ContainsKey(EInventoryTab.Notes)) tab = Tabs[EInventoryTab.Notes];
                    break;
                case EInventoryTab.Gear:
                    if (Tabs.ContainsKey(EInventoryTab.Overall)) tab = Tabs[EInventoryTab.Overall];
                    break;
                case EInventoryTab.Health:
                    if (Tabs.ContainsKey(EInventoryTab.Gear)) tab = Tabs[EInventoryTab.Gear];
                    break;
                case EInventoryTab.Skills:
                    if (Tabs.ContainsKey(EInventoryTab.Health)) tab = Tabs[EInventoryTab.Health];
                    break;
                case EInventoryTab.Map:
                    if (Tabs.ContainsKey(EInventoryTab.Skills)) tab = Tabs[EInventoryTab.Skills];
                    break;
                case EInventoryTab.Notes:
                    if (Tabs.ContainsKey(EInventoryTab.Map)) tab = Tabs[EInventoryTab.Map];
                    break;
            }
            if (tab != null)
            {
                tab.OnPointerClick(null);
            }
        }

        // UI Inputs Actions
        public List<string> ControllerGetButtonAction(RaidPadButtonBind Bind)
        {
            List<string> Actions = new List<string>();
            foreach (RaidPadCommand RaidPadCommand in Bind.RaidPadCommands)
            {
                if (RaidPadCommand.Command == ERaidPadCommand.None) continue;
                switch (RaidPadCommand.Command)
                {
                    case ERaidPadCommand.ToggleSet:
                        if (ActiveRaidPadSets.Contains(RaidPadCommand.RaidPadSet))
                        {
                            Actions.Add("Enable " + RaidPadCommand.RaidPadSet);
                        }
                        else if (RaidPadSets.ContainsKey(RaidPadCommand.RaidPadSet))
                        {
                            Actions.Add("Disable " + RaidPadCommand.RaidPadSet);
                        }
                        break;
                    case ERaidPadCommand.EnableSet:
                        if (RaidPadSets.ContainsKey(RaidPadCommand.RaidPadSet) && !ActiveRaidPadSets.Contains(RaidPadCommand.RaidPadSet))
                        {
                            Actions.Add("Enable " + RaidPadCommand.RaidPadSet);
                        }
                        break;
                    case ERaidPadCommand.DisableSet:
                        Actions.Add("Disable " + RaidPadCommand.RaidPadSet);
                        break;
                    case ERaidPadCommand.InputTree:
                        Actions.Add("" + RaidPadCommand.InputTree);
                        break;
                    case ERaidPadCommand.QuickSelectWeapon:
                        Actions.Add("QuickSelectWeapon");
                        break;
                    case ERaidPadCommand.SlowLeanLeft:
                        Actions.Add("SlowLeanLeft");
                        break;
                    case ERaidPadCommand.SlowLeanRight:
                        Actions.Add("SlowLeanRight");
                        break;
                    case ERaidPadCommand.EndSlowLean:
                        Actions.Add("EndSlowLean");
                        break;
                    case ERaidPadCommand.RestoreLean:
                        Actions.Add("RestoreLean");
                        break;
                    case ERaidPadCommand.InterfaceUp:
                        Actions.Add("InterfaceUp");
                        break;
                    case ERaidPadCommand.InterfaceDown:
                        Actions.Add("InterfaceDown");
                        break;
                    case ERaidPadCommand.InterfaceLeft:
                        Actions.Add("InterfaceLeft");
                        break;
                    case ERaidPadCommand.InterfaceRight:
                        Actions.Add("InterfaceRight");
                        break;
                    case ERaidPadCommand.InterfaceDisableAutoMove:
                        Actions.Add("InterfaceDisableAutoMove");
                        break;
                    case ERaidPadCommand.BeginDrag:
                        if (!Dragging && pointerEventData != null && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf) Actions.Add("Drag");
                        break;
                    case ERaidPadCommand.EndDrag:
                        Actions.Add("EndDrag");
                        break;
                    case ERaidPadCommand.RotateDragged:
                        Actions.Add("Rotate");
                        break;
                    case ERaidPadCommand.SplitDragged:
                        Actions.Add("Split");
                        break;
                    case ERaidPadCommand.CancelDrag:
                        Actions.Add("CancelDrag");
                        break;
                    case ERaidPadCommand.Search:
                        Actions.Add("Search");
                        break;
                    case ERaidPadCommand.Use:
                        Actions.Add(ControllerUseAction(false));
                        break;
                    case ERaidPadCommand.UseHold:
                        Actions.Add(ControllerUseAction(true));
                        break;
                    case ERaidPadCommand.QuickMove:
                        if (pointerEventData != null && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf) Actions.Add("QuickMove");
                        break;
                    case ERaidPadCommand.Discard:
                        if (pointerEventData != null && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf) Actions.Add("Discard");
                        break;
                    case ERaidPadCommand.InterfaceBind4:
                        if (pointerEventData != null && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf) Actions.Add("Bind4");
                        break;
                    case ERaidPadCommand.InterfaceBind5:
                        if (pointerEventData != null && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf) Actions.Add("Bind5");
                        break;
                    case ERaidPadCommand.InterfaceBind6:
                        if (pointerEventData != null && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf) Actions.Add("Bind6");
                        break;
                    case ERaidPadCommand.InterfaceBind7:
                        if (pointerEventData != null && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf) Actions.Add("Bind7");
                        break;
                    case ERaidPadCommand.InterfaceBind8:
                        if (pointerEventData != null && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf) Actions.Add("Bind8");
                        break;
                    case ERaidPadCommand.InterfaceBind9:
                        if (pointerEventData != null && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf) Actions.Add("Bind9");
                        break;
                    case ERaidPadCommand.InterfaceBind10:
                        if (pointerEventData != null && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf) Actions.Add("Bind10");
                        break;
                    case ERaidPadCommand.ShowContextMenu:
                        if (!ContextMenu && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf) Actions.Add("ContextMenu");
                        break;
                    case ERaidPadCommand.ContextMenuUse:
                        Actions.Add("Select");
                        break;
                    case ERaidPadCommand.SplitDialogAccept:
                        Actions.Add("Accept");
                        break;
                    case ERaidPadCommand.SplitDialogAdd:
                        Actions.Add("SplitDialogAdd");
                        break;
                    case ERaidPadCommand.SplitDialogSubtract:
                        Actions.Add("SplitDialogSubtract");
                        break;
                    case ERaidPadCommand.SplitDialogDisableAutoMove:
                        Actions.Add("SplitDialogDisableAutoMove");
                        break;
                    case ERaidPadCommand.PreviousTab:
                        Actions.Add("PreviousTab");
                        break;
                    case ERaidPadCommand.NextTab:
                        Actions.Add("NextTab");
                        break;
                    case ERaidPadCommand.PauseRaid:
                        Actions.Add("Pause");
                        break;
                    case ERaidPadCommand.MapPeek:
                        Actions.Add("Map");
                        break;
                    case ERaidPadCommand.MapPanUp:
                        Actions.Add("Map up");
                        break;
                    case ERaidPadCommand.MapPanDown:
                        Actions.Add("Map down");
                        break;
                    case ERaidPadCommand.MapPanLeft:
                        Actions.Add("Map left");
                        break;
                    case ERaidPadCommand.MapPanRight:
                        Actions.Add("Map right");
                        break;
                    case ERaidPadCommand.MapZoomIn:
                        Actions.Add("Map zoom in");
                        break;
                    case ERaidPadCommand.MapZoomOut:
                        Actions.Add("Map zoom out");
                        break;
                    case ERaidPadCommand.MapCenter:
                        Actions.Add("Map center");
                        break;
                    case ERaidPadCommand.MapLevelUp:
                        Actions.Add("Map level up");
                        break;
                    case ERaidPadCommand.MapLevelDown:
                        Actions.Add("Map level down");
                        break;
                }
            }
            return Actions;
        }
        public string ControllerUseAction(bool Hold)
        {
            if (onPointerEnterItemView == null || (onPointerEnterItemView != null && !onPointerEnterItemView.gameObject.activeSelf))
            {
                return "";
            }
            if (pointerEventData != null && onPointerEnterItemView != null && onPointerEnterItemView.gameObject.activeSelf)
            {
                ItemUiContext ItemUiContext = ItemUiContext.Instance;
                object NewContextInteractionsObject = Traverse.Create(onPointerEnterItemView).Property("NewContextInteractions").GetValue();
                if (NewContextInteractionsObject == null) return "";
                if (ExecuteInteraction == null)
                {
                    ExecuteInteraction = NewContextInteractionsObject.GetType().GetMethod("ExecuteInteraction", BindingFlags.Instance | BindingFlags.Public);
                }
                if (IsInteractionAvailable == null)
                {
                    IsInteractionAvailable = NewContextInteractionsObject.GetType().GetMethod("IsInteractionAvailable", BindingFlags.Instance | BindingFlags.Public);
                }

                if (!onPointerEnterItemView.IsSearched && IsInteractionAvailable != null)
                {
                    IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.Examine;
                    if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "Examine";
                }
                if (ItemUiContext == null || !onPointerEnterItemView.IsSearched) return "";
                ItemController ItemController = Traverse.Create(onPointerEnterItemView).Field("ItemController").GetValue<ItemController>();
                if (onPointerEnterItemView.Item != null && ExecuteInteraction != null && IsInteractionAvailable != null && !Hold)
                {
                    if (onPointerEnterItemView.Item is FoodDrink)
                    {
                        IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.Use;
                        if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "Consume";
                        IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.UseAll;
                        if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "Consume";
                    }
                    if (onPointerEnterItemView.Item is Meds)
                    {
                        IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.Use;
                        if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "Use";
                        IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.UseAll;
                        if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "Use";
                    }
                    if (onPointerEnterItemView.Item.IsContainer)
                    {
                        IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.Open;
                        if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "Open";
                    }
                    IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.Equip;
                    if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed)
                    {
                        return "Equip";
                    }
                    else
                    {
                        bool IsBeingLoadedMagazine = Traverse.Create(Traverse.Create(onPointerEnterItemView).Property("IsBeingLoadedMagazine").GetValue<object>()).Field("CurrentState").GetValue<bool>();
                        bool IsBeingUnloadedMagazine = Traverse.Create(Traverse.Create(onPointerEnterItemView).Property("IsBeingUnloadedMagazine").GetValue<object>()).Field("CurrentState").GetValue<bool>();
                        if (IsBeingLoadedMagazine)
                        {
                            return "Stop Loading Mag";
                        }
                        if (IsBeingUnloadedMagazine)
                        {
                            return "Stop Unloading Mag";
                        }
                    }
                    IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.CheckMagazine;
                    if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "CheckMagazine";
                    if (IsInteractionAvailable != null)
                    {
                        IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.Examine;
                        if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "Examine";
                        IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.Fold;
                        if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "Fold";
                        IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.Unfold;
                        if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "Unfold";
                        IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.TurnOn;
                        if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "TurnOn";
                        IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.TurnOff;
                        if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "TurnOff";
                    }
                }
                if (onPointerEnterItemView.Item != null && ExecuteInteraction != null && IsInteractionAvailable != null && Hold)
                {
                    IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.Fold;
                    if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "Fold";
                    IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.Unfold;
                    if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "Unfold";
                    IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.TurnOn;
                    if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "TurnOn";
                    IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.TurnOff;
                    if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "TurnOff";
                    IsInteractionAvailableInvokeParameters[0] = EItemInfoButton.Equip;
                    if (((IResult)IsInteractionAvailable.Invoke(NewContextInteractionsObject, IsInteractionAvailableInvokeParameters)).Succeed) return "Equip";
                }
            }
            return "";
        }

        // UI Blocks
        public void ControllerButtonStateMethod(ERaidPadButton Button, bool Pressed)
        {
            if (ButtonBlocks.ContainsKey(Button) && ButtonBlocks[Button] != null) ButtonBlocks[Button].UpdateButtonPressed(Pressed);
        }
        public void ControllerSetState(string RaidPadSet, bool Enabled)
        {
            UpdateControllerBlocks();
        }
        public void UpdateControllerBlocks()
        {
            foreach (KeyValuePair<ERaidPadButton, RaidPadButtonBlock> Block in ButtonBlocks)
            {
                if (Block.Value == null) continue;
                bool PressValid = false;
                bool HoldValid = false;
                bool DoubleValid = false;
                RaidPadButtonBind[] Binds = GetPriorityButtonBinds(Block.Key);
                foreach (string Action in ControllerGetButtonAction(Binds[0]))
                {
                    if (Action == "") continue;
                    Block.Value.Press = Action;
                    PressValid = true;
                    break;
                }
                foreach (string Action in ControllerGetButtonAction(Binds[2]))
                {
                    if (Action == "") continue;
                    Block.Value.Hold = Action;
                    HoldValid = true;
                    break;
                }
                foreach (string Action in ControllerGetButtonAction(Binds[3]))
                {
                    if (Action == "") continue;
                    Block.Value.DoubleClick = Action;
                    DoubleValid = true;
                    break;
                }
                Block.Value.HoldGameObject.SetActive(HoldValid);
                Block.Value.DoubleClickGameObject.SetActive(DoubleValid);
                if (PressValid)
                {
                    Block.Value.gameObject.SetActive(true);
                    Block.Value.UpdateCommands();
                }
                else
                {
                    Block.Value.gameObject.SetActive(false);
                }
            }
        }

        // Files
        public static void WriteToJsonFile<T>(string filePath, T objectToWrite, bool append = false) where T : new()
        {
            TextWriter writer = null;
            try
            {
                var contentsToWriteToFile = JsonConvert.SerializeObject(objectToWrite, Formatting.Indented);//Json.Serialize(objectToWrite);
                writer = new StreamWriter(filePath, append);
                writer.Write(contentsToWriteToFile);
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }
        public static T ReadFromJsonFile<T>(string filePath) where T : new()
        {
            TextReader reader = null;
            try
            {
                reader = new StreamReader(filePath);
                var fileContents = reader.ReadToEnd();
                return Json.Deserialize<T>(fileContents);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }
        public static void ReloadFiles()
        {
            string[] Files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "/BepInEx/plugins/RaidPad/assets/images/", "*.png");
            foreach (string File in Files)
            {
                LoadSprite(File);
            }
            // There are no sound assets at this time, I assume this is meant for future support?
            string SoundsPath = AppDomain.CurrentDomain.BaseDirectory + "/BepInEx/plugins/RaidPad/assets/sounds/";
            try
            {
                string[] AudioFiles = Directory.GetFiles(SoundsPath);
                foreach (string File in AudioFiles)
                {
                    LoadAudioClip(File);
                }
            }
            catch (DirectoryNotFoundException)
            {
                RaidPadPlugin.Log.LogWarning($"Sound asset directory not found, skipping audio load: {SoundsPath}");
            }
            
        }
        private async static void LoadSprite(string path)
        {
            LoadedSprites[Path.GetFileName(path)] = await RequestSprite(path);
        }
        private async static Task<Sprite> RequestSprite(string path)
        {
            UnityWebRequest www = UnityWebRequestTexture.GetTexture(path);
            var SendWeb = www.SendWebRequest();

            while (!SendWeb.isDone)
                await Task.Yield();

            if (www.isNetworkError || www.isHttpError)
            {
                return null;
            }
            else
            {
                Texture2D texture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));

                return sprite;
            }
        }
        private async static void LoadAudioClip(string path)
        {
            LoadedAudioClips[Path.GetFileName(path)] = await RequestAudioClip(path);
        }
        private async static Task<AudioClip> RequestAudioClip(string path)
        {
            string extension = Path.GetExtension(path);
            AudioType audioType = AudioType.WAV;
            switch (extension)
            {
                case ".wav":
                    audioType = AudioType.WAV;
                    break;
                case ".ogg":
                    audioType = AudioType.OGGVORBIS;
                    break;
            }
            UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(path, audioType);
            var SendWeb = www.SendWebRequest();

            while (!SendWeb.isDone)
                await Task.Yield();

            if (www.isNetworkError || www.isHttpError)
            {
                return null;
            }
            else
            {
                AudioClip audioclip = DownloadHandlerAudioClip.GetContent(www);
                return audioclip;
            }
        }
    }
    public class RaidPadButtonBlock : MonoBehaviour
    {
        public ERaidPadButton Button;
        public bool Pressed;
        public string Press = "";
        public string Hold = "";
        public string DoubleClick = "";

        public RectTransform RectTransform;
        public HorizontalLayoutGroup HorizontalLayoutGroup;

        public GameObject CommandsGameObject;
        public RectTransform CommandsRectTransform;
        public VerticalLayoutGroup CommandsVerticalLayoutGroup;

        public GameObject IconGameObject;
        public RectTransform IconRectTransform;
        public Image IconImage;
        public LayoutElement IconLayoutElement;

        public GameObject PressGameObject;
        public RectTransform PressRectTransform;
        public TextMeshProUGUI PressTextMeshProUGUI;

        public GameObject HoldGameObject;
        public RectTransform HoldRectTransform;
        public TextMeshProUGUI HoldTextMeshProUGUI;

        public GameObject DoubleClickGameObject;
        public RectTransform DoubleClickRectTransform;
        public TextMeshProUGUI DoubleClickTextMeshProUGUI;

        public void Start()
        {
            RectTransform = gameObject.AddComponent<RectTransform>();
            RectTransform.sizeDelta = RaidPadPlugin.BlockSize.Value;

            HorizontalLayoutGroup = gameObject.AddComponent<HorizontalLayoutGroup>();
            HorizontalLayoutGroup.childForceExpandHeight = false;
            HorizontalLayoutGroup.childForceExpandWidth = false;
            HorizontalLayoutGroup.childAlignment = TextAnchor.MiddleRight;
            HorizontalLayoutGroup.spacing = RaidPadPlugin.BlockIconSpacing.Value;

            CommandsGameObject = new GameObject("Commands");
            CommandsGameObject.transform.SetParent(RectTransform);
            CommandsRectTransform = CommandsGameObject.AddComponent<RectTransform>();
            CommandsVerticalLayoutGroup = CommandsGameObject.AddComponent<VerticalLayoutGroup>();
            CommandsVerticalLayoutGroup.childForceExpandHeight = false;
            CommandsVerticalLayoutGroup.childForceExpandWidth = false;
            CommandsVerticalLayoutGroup.childAlignment = TextAnchor.MiddleRight;

            PressGameObject = new GameObject("Press");
            PressGameObject.transform.SetParent(CommandsRectTransform);
            PressRectTransform = PressGameObject.AddComponent<RectTransform>();
            PressTextMeshProUGUI = PressGameObject.AddComponent<TextMeshProUGUI>();
            PressTextMeshProUGUI.autoSizeTextContainer = true;
            PressTextMeshProUGUI.horizontalAlignment = HorizontalAlignmentOptions.Right;
            PressTextMeshProUGUI.text = Press;
            PressTextMeshProUGUI.fontSize = RaidPadPlugin.PressFontSize.Value;

            HoldGameObject = new GameObject("Hold");
            HoldGameObject.transform.SetParent(CommandsRectTransform);
            HoldRectTransform = HoldGameObject.AddComponent<RectTransform>();
            HoldTextMeshProUGUI = HoldGameObject.AddComponent<TextMeshProUGUI>();
            HoldTextMeshProUGUI.autoSizeTextContainer = true;
            HoldTextMeshProUGUI.horizontalAlignment = HorizontalAlignmentOptions.Right;
            HoldTextMeshProUGUI.text = Hold + " (HOLD)";
            HoldTextMeshProUGUI.fontSize = RaidPadPlugin.HoldDoubleClickFontSize.Value;
            HoldTextMeshProUGUI.color = new Color(1f,1f,1f,0.7f);

            DoubleClickGameObject = new GameObject("DoubleClick");
            DoubleClickGameObject.transform.SetParent(CommandsRectTransform);
            DoubleClickRectTransform = DoubleClickGameObject.AddComponent<RectTransform>();
            DoubleClickTextMeshProUGUI = DoubleClickGameObject.AddComponent<TextMeshProUGUI>();
            DoubleClickTextMeshProUGUI.autoSizeTextContainer = true;
            DoubleClickTextMeshProUGUI.horizontalAlignment = HorizontalAlignmentOptions.Right;
            DoubleClickTextMeshProUGUI.text = DoubleClick + " 2x";
            DoubleClickTextMeshProUGUI.fontSize = RaidPadPlugin.HoldDoubleClickFontSize.Value;
            DoubleClickTextMeshProUGUI.color = new Color(1f, 1f, 1f, 0.7f);

            IconGameObject = new GameObject("Icon");
            IconGameObject.transform.SetParent(RectTransform);
            IconRectTransform = IconGameObject.AddComponent<RectTransform>();
            IconRectTransform.sizeDelta = RaidPadPlugin.BlockSize.Value;
            IconImage = IconGameObject.AddComponent<Image>();
            IconImage.raycastTarget = false;
            if (RaidPadClass.LoadedSprites.ContainsKey((RaidPadPlugin.DualsenseIcons.Value ? "Dualsense" : "") + Button.ToString() + ".png"))
            {
                IconImage.sprite = RaidPadClass.LoadedSprites[(RaidPadPlugin.DualsenseIcons.Value ? "Dualsense" : "") + Button.ToString() + ".png"];
            }
            IconLayoutElement = IconGameObject.AddComponent<LayoutElement>();
            IconLayoutElement.preferredWidth = RaidPadPlugin.BlockSize.Value.x;
            IconLayoutElement.preferredHeight = RaidPadPlugin.BlockSize.Value.y;

        }
        public void UpdateCommands()
        {
            if (PressTextMeshProUGUI != null) PressTextMeshProUGUI.text = Press;

            if (HoldTextMeshProUGUI != null) HoldTextMeshProUGUI.text = Hold + " (HOLD)";

            if (DoubleClickTextMeshProUGUI != null) DoubleClickTextMeshProUGUI.text = DoubleClick + " 2x";
        }
        public void UpdateButtonPressed(bool Pressed)
        {
            this.Pressed = Pressed;

            if (IconImage != null && RaidPadClass.LoadedSprites.ContainsKey(Pressed ? (RaidPadPlugin.DualsenseIcons.Value ? "Dualsense" : "") + (Button.ToString() + "_PRESSED.png") : (RaidPadPlugin.DualsenseIcons.Value ? "Dualsense" : "") + Button.ToString() + ".png"))
            {
                IconImage.sprite = RaidPadClass.LoadedSprites[Pressed ? (RaidPadPlugin.DualsenseIcons.Value ? "Dualsense" : "") + (Button.ToString() + "_PRESSED.png") : (RaidPadPlugin.DualsenseIcons.Value ? "Dualsense" : "") + Button.ToString() + ".png"];
            }

        }
    }
    public enum ERaidPadCurrentUI
    {
        None = 0,
        GridView = 1,
        TradingTableGridView = 2,
        ModSlotView = 3,
        EquipmentSlotView = 4,
        WeaponsSlotView = 5,
        ArmbandSlotView = 6,
        ContainersSlotView = 7,
        DogtagSlotView = 8,
        SpecialSlotSlotView = 9,
        SearchButton = 10,
        ContextMenuButton = 11
    }
    public class ControllerPresetJsonClass
    {
        public Dictionary<ERaidPadButton, List<RaidPadButtonBind>> RaidPadButtonBinds { get; set; }
        public Dictionary<string, Dictionary<ERaidPadButton, List<RaidPadButtonBind>>> RaidPadSets { get; set; }
    }
    public struct RaidPadCommand
    {
        public ERaidPadCommand Command;
        public ECommand InputTree;
        public string RaidPadSet;
        public RaidPadCommand(ERaidPadCommand Command, ECommand InputTree, string RaidPadSet)
        {
            this.Command = Command;
            this.InputTree = InputTree;
            this.RaidPadSet = RaidPadSet;
        }
        public RaidPadCommand(ERaidPadCommand Command, ECommand InputTree)
        {
            this.Command = Command;
            this.InputTree = InputTree;
            this.RaidPadSet = "";
        }
        public RaidPadCommand(ERaidPadCommand Command, string RaidPadSet)
        {
            this.Command = Command;
            this.InputTree = ECommand.None;
            this.RaidPadSet = RaidPadSet;
        }
        public RaidPadCommand(ERaidPadCommand Command)
        {
            this.Command = Command;
            this.InputTree = ECommand.None;
            this.RaidPadSet = "";
        }
        public RaidPadCommand(ECommand InputTree)
        {
            this.Command = ERaidPadCommand.InputTree;
            this.InputTree = InputTree;
            this.RaidPadSet = "";
        }
    }
    public struct RaidPadButtonBind
    {
        public List<RaidPadCommand> RaidPadCommands;
        public ERaidPadPressType PressType;
        public int Priority;

        public RaidPadButtonBind(List<RaidPadCommand> RaidPadCommands, ERaidPadPressType PressType, int Priority)
        {
            this.RaidPadCommands = RaidPadCommands;
            this.PressType = PressType;
            this.Priority = Priority;
        }
        public RaidPadButtonBind(RaidPadCommand RaidPadCommands, ERaidPadPressType PressType, int Priority)
        {
            this.RaidPadCommands = new List<RaidPadCommand> { RaidPadCommands };
            this.PressType = PressType;
            this.Priority = Priority;
        }
    }
    public struct RaidPadButtonSnapshot
    {
        public bool Pressed;
        public float Time;
        public RaidPadButtonBind PressBind;
        public RaidPadButtonBind ReleaseBind;
        public RaidPadButtonBind HoldBind;
        public RaidPadButtonBind DoubleClickBind;

        public RaidPadButtonSnapshot(bool Pressed, float Time, RaidPadButtonBind PressBind, RaidPadButtonBind ReleaseBind, RaidPadButtonBind HoldBind, RaidPadButtonBind DoubleClickBind)
        {
            this.Pressed = Pressed;
            this.Time = Time;
            this.PressBind = PressBind;
            this.ReleaseBind = ReleaseBind;
            this.HoldBind = HoldBind;
            this.DoubleClickBind = DoubleClickBind;
        }
    }
    public enum ERaidPadButton
    {
        A = 0,
        B = 1,
        X = 2,
        Y = 3,
        LB = 4,
        RB = 5,
        LT = 6,
        RT = 7,
        LS = 8,
        RS = 9,
        UP = 10,
        DOWN = 11,
        LEFT = 12,
        RIGHT = 13,
        LSUP = 14,
        LSDOWN = 15,
        LSLEFT = 16,
        LSRIGHT = 17,
        RSUP = 18,
        RSDOWN = 19,
        RSLEFT = 20,
        RSRIGHT = 21,
        BACK = 24,
        MENU = 25,
    }
    public enum ERaidPadPressType
    {
        Press = 0,
        Release = 1,
        Hold = 2,
        DoubleClick = 3
    }
    public enum ERaidPadCommand
    {
        None = 0,
        ToggleSet = 1,
        EnableSet = 2,
        DisableSet = 3,
        InputTree = 4,
        QuickSelectWeapon = 5,
        SlowLeanLeft = 6,
        SlowLeanRight = 7,
        EndSlowLean = 8,
        RestoreLean = 9,
        InterfaceUp = 10,
        InterfaceDown = 11,
        InterfaceLeft = 12,
        InterfaceRight = 13,
        InterfaceDisableAutoMove = 14,
        BeginDrag = 15,
        EndDrag = 16,
        Search = 17,
        Use = 18,
        UseHold = 19,
        QuickMove = 20,
        Discard = 21,
        InterfaceBind4 = 22,
        InterfaceBind5 = 23,
        InterfaceBind6 = 24,
        InterfaceBind7 = 25,
        InterfaceBind8 = 26,
        InterfaceBind9 = 27,
        InterfaceBind10 = 28,
        ShowContextMenu = 29,
        ContextMenuUse = 30,
        SplitDialogAccept = 31,
        SplitDialogAdd = 32,
        SplitDialogSubtract = 33,
        SplitDialogDisableAutoMove = 34,
        RotateDragged = 35,
        CancelDrag = 36,
        SplitDragged = 37,
        PreviousTab = 38,
        NextTab = 39,
        PauseRaid = 40,
        MapPeek = 41,
        MapPanUp = 42,
        MapPanDown = 43,
        MapPanLeft = 44,
        MapPanRight = 45,
        MapZoomIn = 46,
        MapZoomOut = 47,
        MapCenter = 48,
        MapLevelUp = 49,
        MapLevelDown = 50
    }
    public enum ERaidPadUseStick
    {
        None = 0,
        LS = 1,
        RS = 2
    }
}
