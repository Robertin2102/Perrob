#if UNITY_EDITOR
using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Builds the room-code uGUI in Sc_Menu and makes it the first build scene.</summary>
[InitializeOnLoad]
public static class OfficePhotonMenuEditorBuilder
{
    private const string MenuScenePath = "Assets/Scenes/Sc_Menu.unity";
    private const string GameScenePath = "Assets/Scenes/Sc_Game.unity";
    private const string NetworkPrefabFolder = "Assets/Scripts/NetworkPrefabs";
    private const string LobbyPrefabPath = NetworkPrefabFolder + "/OfficeFusionLobbyState.prefab";

    private static readonly Color Ink = new Color32(20, 28, 39, 255);
    private static readonly Color Card = new Color32(31, 43, 56, 255);
    private static readonly Color Muted = new Color32(169, 184, 195, 255);
    private static readonly Color White = new Color32(247, 243, 231, 255);
    private static readonly Color Teal = new Color32(70, 205, 181, 255);
    private static readonly Color Coral = new Color32(255, 122, 102, 255);

    static OfficePhotonMenuEditorBuilder()
    {
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        EditorApplication.delayCall += BuildMenuSceneAndRestoreEditorScene;
    }

    private static void BuildMenuSceneAndRestoreEditorScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene previousScene = SceneManager.GetActiveScene();
        Scene menuScene = SceneManager.GetSceneByPath(MenuScenePath);
        bool openedForBuild = !menuScene.IsValid() || !menuScene.isLoaded;
        if (openedForBuild)
            menuScene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);

        if (menuScene.IsValid() && menuScene.isLoaded)
        {
            SceneManager.SetActiveScene(menuScene);
            CreateMenu();
            if (openedForBuild)
                EditorSceneManager.CloseScene(menuScene, true);
        }

        if (previousScene.IsValid() && previousScene.isLoaded)
            SceneManager.SetActiveScene(previousScene);
    }

    private static void OnActiveSceneChanged(Scene previous, Scene current)
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && current.name == "Sc_Menu")
            EditorApplication.delayCall += CreateMenu;
    }

    [MenuItem("Pelea por el Salario Mínimo/Crear menú Sc_Menu")]
    public static void CreateMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Sc_Menu")
        {
            Debug.LogWarning("Abre Sc_Menu para crear el menú de salas.");
            return;
        }

        OfficePhotonMenuView existingView = Object.FindFirstObjectByType<OfficePhotonMenuView>();
        if (existingView != null)
        {
            ConfigureExistingMenu(existingView);
            EnsureBuildSettings();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject canvasObject = new GameObject("OfficePhotonMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject background = CreatePanel("Background", canvasObject.transform, Ink);
        Stretch(background.GetComponent<RectTransform>());
        background.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;

        GameObject accent = CreatePanel("AccentStripe", canvasObject.transform, Teal);
        SetRect(accent.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.018f, 1f), Vector2.zero, Vector2.zero);
        accent.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;

        GameObject card = CreatePanel("MenuCard", canvasObject.transform, Card);
        SetCenterRect(card.GetComponent<RectTransform>(), new Vector2(720f, 690f));

        Text eyebrow = CreateText("Eyebrow", card.transform, "OFICINA CENTRAL  ·  JORNADA MULTIJUGADOR", 16, Teal, TextAnchor.MiddleCenter, FontStyle.Bold, font);
        SetAnchors(eyebrow.rectTransform, new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.94f));

        Text title = CreateText("Title", card.transform, "PELEA POR EL\nSALARIO MÍNIMO", 46, White, TextAnchor.MiddleCenter, FontStyle.Bold, font);
        SetAnchors(title.rectTransform, new Vector2(0.06f, 0.66f), new Vector2(0.94f, 0.86f));

        Text subtitle = CreateText("Subtitle", card.transform,
            "Dos empleados. Una oficina. Un ascenso que no alcanza para los dos.", 20, Muted, TextAnchor.MiddleCenter, FontStyle.Normal, font);
        SetAnchors(subtitle.rectTransform, new Vector2(0.1f, 0.57f), new Vector2(0.9f, 0.65f));

        Text roomLabel = CreateText("RoomCodeLabel", card.transform, "CÓDIGO DE SALA", 16, Muted, TextAnchor.MiddleLeft, FontStyle.Bold, font);
        SetAnchors(roomLabel.rectTransform, new Vector2(0.1f, 0.48f), new Vector2(0.9f, 0.54f));

        InputField roomCode = CreateRoomCodeInput(card.transform, font);
        SetAnchors(roomCode.GetComponent<RectTransform>(), new Vector2(0.1f, 0.39f), new Vector2(0.9f, 0.47f));

        Button createButton = CreateButton("CreateRoomButton", card.transform, "CREAR SALA", Teal, Ink, font);
        SetAnchors(createButton.GetComponent<RectTransform>(), new Vector2(0.1f, 0.27f), new Vector2(0.48f, 0.36f));

        Button joinButton = CreateButton("JoinRoomButton", card.transform, "UNIRSE A SALA", Coral, Ink, font);
        SetAnchors(joinButton.GetComponent<RectTransform>(), new Vector2(0.52f, 0.27f), new Vector2(0.9f, 0.36f));

        GameObject statusCard = CreatePanel("StatusCard", card.transform, Ink);
        SetAnchors(statusCard.GetComponent<RectTransform>(), new Vector2(0.1f, 0.12f), new Vector2(0.9f, 0.23f));

        Text status = CreateText("StatusText", statusCard.transform, "Crea una sala o escribe el código de otra.", 17, White, TextAnchor.MiddleCenter, FontStyle.Normal, font);
        Stretch(status.rectTransform, new Vector2(16f, 4f), new Vector2(-16f, -4f));

        Text playerCount = CreateText("PlayerCountText", card.transform, "EMPLEADOS CONECTADOS   0 / 2", 15, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, font);
        SetAnchors(playerCount.rectTransform, new Vector2(0.1f, 0.045f), new Vector2(0.9f, 0.105f));

        OfficeFusionRoomController roomController = Object.FindFirstObjectByType<OfficeFusionRoomController>();
        if (roomController == null)
        {
            GameObject roomObject = new GameObject("OfficeFusionSession");
            roomController = roomObject.AddComponent<OfficeFusionRoomController>();
        }

        NetworkObject lobbyPrefab = EnsureLobbyStatePrefab();
        roomController.ConfigureLobbyState(lobbyPrefab);

        GameObject lobbyPanel = CreateLobbyPanel(card.transform, font,
            out Text lobbyRoom, out Text lobbyStatus, out Text readyPlayers,
            out Button readyButton, out Button startMatchButton);
        GameObject[] connectionElements =
        {
            roomLabel.gameObject,
            roomCode.gameObject,
            createButton.gameObject,
            joinButton.gameObject,
            statusCard,
            playerCount.gameObject
        };

        OfficePhotonMenuView view = canvasObject.AddComponent<OfficePhotonMenuView>();
        view.Configure(roomController, roomCode, createButton, joinButton, status, playerCount,
            lobbyPanel, lobbyRoom, lobbyStatus, readyPlayers, readyButton, startMatchButton, connectionElements);
        EnsureEventSystem();
        EnsureBuildSettings();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = canvasObject;
        Debug.Log("Menú de salas creado y guardado en Sc_Menu.");
    }

    private static void ConfigureExistingMenu(OfficePhotonMenuView view)
    {
        Transform card = view.transform.Find("MenuCard");
        if (card == null)
        {
            Debug.LogError("No encontré MenuCard dentro del menú existente de Sc_Menu.", view);
            return;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Transform roomLabel = card.Find("RoomCodeLabel");
        Transform roomInput = card.Find("RoomCodeInput");
        Transform createButton = card.Find("CreateRoomButton");
        Transform joinButton = card.Find("JoinRoomButton");
        Transform statusCard = card.Find("StatusCard");
        Transform playerCount = card.Find("PlayerCountText");
        if (roomLabel == null || roomInput == null || createButton == null || joinButton == null || statusCard == null || playerCount == null)
        {
            Debug.LogError("Faltan controles de conexión en el menú existente. No los reemplacé.", view);
            return;
        }

        NetworkObject lobbyPrefab = EnsureLobbyStatePrefab();
        OfficeFusionRoomController roomController = Object.FindFirstObjectByType<OfficeFusionRoomController>();
        if (roomController != null)
        {
            roomController.ConfigureLobbyState(lobbyPrefab);
            EditorUtility.SetDirty(roomController);
        }

        Transform panelTransform = card.Find("LobbyPanel");
        GameObject lobbyPanel;
        Text lobbyRoom;
        Text lobbyStatus;
        Text readyPlayers;
        Button readyButton;
        Button startMatchButton;
        if (panelTransform == null)
        {
            lobbyPanel = CreateLobbyPanel(card, font, out lobbyRoom, out lobbyStatus, out readyPlayers,
                out readyButton, out startMatchButton);
        }
        else
        {
            lobbyPanel = panelTransform.gameObject;
            lobbyRoom = lobbyPanel.transform.Find("LobbyRoomText").GetComponent<Text>();
            lobbyStatus = lobbyPanel.transform.Find("LobbyStatusText").GetComponent<Text>();
            readyPlayers = lobbyPanel.transform.Find("ReadyPlayersText").GetComponent<Text>();
            readyButton = lobbyPanel.transform.Find("ReadyButton").GetComponent<Button>();
            startMatchButton = lobbyPanel.transform.Find("StartMatchButton").GetComponent<Button>();
        }

        GameObject[] connectionElements =
        {
            roomLabel.gameObject,
            roomInput.gameObject,
            createButton.gameObject,
            joinButton.gameObject,
            statusCard.gameObject,
            playerCount.gameObject
        };
        view.Configure(roomController, roomInput.GetComponent<InputField>(),
            createButton.GetComponent<Button>(), joinButton.GetComponent<Button>(),
            statusCard.Find("StatusText").GetComponent<Text>(), playerCount.GetComponent<Text>(),
            lobbyPanel, lobbyRoom, lobbyStatus, readyPlayers, readyButton, startMatchButton, connectionElements);
        EditorUtility.SetDirty(view);
    }

    private static GameObject CreateLobbyPanel(
        Transform parent,
        Font font,
        out Text lobbyRoom,
        out Text lobbyStatus,
        out Text readyPlayers,
        out Button readyButton,
        out Button startMatchButton)
    {
        GameObject panel = CreatePanel("LobbyPanel", parent, new Color(0f, 0f, 0f, 0f));
        SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.08f, 0.055f), new Vector2(0.92f, 0.57f));
        panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;

        lobbyRoom = CreateText("LobbyRoomText", panel.transform, "CÓDIGO DE SALA", 18, Teal, TextAnchor.MiddleCenter, FontStyle.Bold, font);
        SetAnchors(lobbyRoom.rectTransform, new Vector2(0.04f, 0.79f), new Vector2(0.96f, 0.98f));

        Text playerSlots = CreateText("PlayerSlotsText", panel.transform, "EMPLEADO 01     ●     EMPLEADO 02", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, font);
        SetAnchors(playerSlots.rectTransform, new Vector2(0.04f, 0.62f), new Vector2(0.96f, 0.78f));

        readyPlayers = CreateText("ReadyPlayersText", panel.transform, "EMPLEADO 01  ESPERANDO  ·  EMPLEADO 02  ESPERANDO", 15, White, TextAnchor.MiddleCenter, FontStyle.Normal, font);
        SetAnchors(readyPlayers.rectTransform, new Vector2(0.02f, 0.44f), new Vector2(0.98f, 0.61f));

        readyButton = CreateButton("ReadyButton", panel.transform, "ESTOY LISTO", Teal, Ink, font);
        SetAnchors(readyButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.41f));

        lobbyStatus = CreateText("LobbyStatusText", panel.transform, "Esperando a los dos empleados.", 15, Muted, TextAnchor.MiddleCenter, FontStyle.Normal, font);
        SetAnchors(lobbyStatus.rectTransform, new Vector2(0.03f, 0.12f), new Vector2(0.97f, 0.23f));

        startMatchButton = CreateButton("StartMatchButton", panel.transform, "INICIAR LA JORNADA", Coral, Ink, font);
        SetAnchors(startMatchButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.005f), new Vector2(0.92f, 0.12f));
        startMatchButton.gameObject.SetActive(false);
        panel.SetActive(false);
        return panel;
    }

    private static NetworkObject EnsureLobbyStatePrefab()
    {
        if (!AssetDatabase.IsValidFolder(NetworkPrefabFolder))
            AssetDatabase.CreateFolder("Assets/Scripts", "NetworkPrefabs");

        NetworkObject existing = AssetDatabase.LoadAssetAtPath<NetworkObject>(LobbyPrefabPath);
        if (existing != null)
            return existing;

        GameObject temporary = new GameObject("OfficeFusionLobbyState");
        temporary.AddComponent<NetworkObject>();
        temporary.AddComponent<OfficeFusionLobbyState>();
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(temporary, LobbyPrefabPath, out bool saved);
        Object.DestroyImmediate(temporary);
        AssetDatabase.SaveAssets();

        if (!saved || prefabAsset == null)
        {
            Debug.LogError("No pude crear el prefab de estado del lobby para Fusion.");
            return null;
        }

        return prefabAsset.GetComponent<NetworkObject>();
    }

    private static InputField CreateRoomCodeInput(Transform parent, Font font)
    {
        GameObject inputObject = CreatePanel("RoomCodeInput", parent, new Color32(243, 240, 228, 255));
        InputField input = inputObject.AddComponent<InputField>();
        UnityEngine.UI.Image image = inputObject.GetComponent<UnityEngine.UI.Image>();
        input.targetGraphic = image;

        Text value = CreateText("Value", inputObject.transform, "SALARIO01", 22, Ink, TextAnchor.MiddleLeft, FontStyle.Bold, font);
        Stretch(value.rectTransform, new Vector2(18f, 4f), new Vector2(-18f, -4f));
        value.supportRichText = false;
        value.raycastTarget = false;

        Text placeholder = CreateText("Placeholder", inputObject.transform, "Ej. EQUIPO-01", 20, new Color32(110, 121, 128, 255), TextAnchor.MiddleLeft, FontStyle.Normal, font);
        Stretch(placeholder.rectTransform, new Vector2(18f, 4f), new Vector2(-18f, -4f));
        placeholder.raycastTarget = false;

        input.textComponent = value;
        input.placeholder = placeholder;
        input.characterLimit = 24;
        input.contentType = InputField.ContentType.Alphanumeric;
        input.lineType = InputField.LineType.SingleLine;
        return input;
    }

    private static Button CreateButton(string name, Transform parent, string label, Color background, Color textColor, Font font)
    {
        GameObject buttonObject = CreatePanel(name, parent, background);
        Button button = buttonObject.AddComponent<Button>();
        UnityEngine.UI.Image image = buttonObject.GetComponent<UnityEngine.UI.Image>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = background;
        colors.highlightedColor = Color.Lerp(background, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(background, Color.black, 0.16f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text text = CreateText("Label", buttonObject.transform, label, 19, textColor, TextAnchor.MiddleCenter, FontStyle.Bold, font);
        Stretch(text.rectTransform, new Vector2(8f, 4f), new Vector2(-8f, -4f));
        text.raycastTarget = false;
        return button;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<UnityEngine.UI.Image>().color = color;
        return panel;
    }

    private static Text CreateText(string name, Transform parent, string value, int size, Color color, TextAnchor alignment, FontStyle style, Font font)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Undo.RegisterCreatedObjectUndo(eventSystem, "Create menu EventSystem");
    }

    private static void EnsureBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
            .Where(scene => scene.path != MenuScenePath && scene.path != GameScenePath)
            .ToList();

        scenes.Insert(0, new EditorBuildSettingsScene(GameScenePath, true));
        scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void SetCenterRect(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
#endif
