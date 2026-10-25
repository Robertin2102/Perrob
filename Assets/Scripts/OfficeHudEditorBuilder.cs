#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One-shot editor menu command that builds the first-pass match HUD.</summary>
[InitializeOnLoad]
public static class OfficeHudEditorBuilder
{
    private static readonly Color Ink = new Color32(20, 28, 39, 244);
    private static readonly Color Card = new Color32(31, 43, 56, 248);
    private static readonly Color Muted = new Color32(169, 184, 195, 255);
    private static readonly Color White = new Color32(247, 243, 231, 255);
    private static readonly Color Teal = new Color32(70, 205, 181, 255);
    private static readonly Color Coral = new Color32(255, 122, 102, 255);

    static OfficeHudEditorBuilder()
    {
        EditorApplication.delayCall += BuildHudForRequestedScene;
    }

    private static void BuildHudForRequestedScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "Sc_Game")
            return;

        string preferenceKey = "Perrob.OfficeHudCreated." + Application.dataPath;
        if (EditorPrefs.GetBool(preferenceKey, false))
            return;

        Canvas existingCanvas = Object.FindAnyObjectByType<Canvas>();
        bool isPartialOfficeHud = existingCanvas != null && existingCanvas.name == "OfficeHudCanvas" &&
                                  existingCanvas.GetComponent<OfficeHudController>() == null;
        if (existingCanvas != null && !isPartialOfficeHud)
        {
            EditorPrefs.SetBool(preferenceKey, true);
            return;
        }

        try
        {
            CreateHud();
            EditorPrefs.SetBool(preferenceKey, true);
            EditorSceneManager.SaveScene(activeScene);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [MenuItem("Pelea por el Salario Mínimo/Crear HUD en escena")]
    public static void CreateHud()
    {
        Canvas existingCanvas = Object.FindAnyObjectByType<Canvas>();
        if (existingCanvas != null)
        {
            if (existingCanvas.name == "OfficeHudCanvas" && existingCanvas.GetComponent<OfficeHudController>() == null)
                Undo.DestroyObjectImmediate(existingCanvas.gameObject);
            else
            {
                EditorUtility.DisplayDialog("HUD existente", "La escena ya tiene un Canvas. No creé otro para evitar duplicarlo.", "Entendido");
                return;
            }
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "Sc_Game")
        {
            EditorUtility.DisplayDialog("Escena incorrecta", "Abre Sc_Game antes de crear el HUD.", "Entendido");
            return;
        }

        GameObject canvasObject = new GameObject("OfficeHudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create office HUD");
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform hudRoot = CreateRect("HudRoot", canvasObject.transform);
        SetStretch(hudRoot);

        Text title = CreateText("GameTitle", hudRoot, "PELEA POR EL SALARIO MÍNIMO", 19, White, TextAnchor.UpperLeft, FontStyle.Bold);
        SetAnchoredRect(title.rectTransform, new Vector2(0.03f, 0.985f), new Vector2(0.03f, 0.985f), new Vector2(0f, 1f), new Vector2(0f, -9f), new Vector2(480f, 30f));

        Image topBar = CreatePanel("TopBar", hudRoot, new Vector2(0.025f, 0.865f), new Vector2(0.975f, 0.955f), Ink);
        AddPlayerScoreCard(topBar.transform, "PlayerOneCard", "EMPLEADO 01", Teal, true, out Text scoreOne);
        AddPlayerScoreCard(topBar.transform, "PlayerTwoCard", "EMPLEADO 02", Coral, false, out Text scoreTwo);

        Image timerCard = CreatePanel("TimerCard", topBar.transform, new Vector2(0.405f, 0.06f), new Vector2(0.595f, 0.94f), Card);
        CreateText("TimerCaption", timerCard.transform, "JORNADA", 13, Muted, TextAnchor.LowerCenter, FontStyle.Bold,
            new Vector2(0.05f, 0.49f), new Vector2(0.95f, 0.92f));
        Text timer = CreateText("TimerText", timerCard.transform, "02:00", 31, White, TextAnchor.UpperCenter, FontStyle.Bold,
            new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.62f));

        Image cargoCard = CreatePanel("CargoCard", hudRoot, new Vector2(0.025f, 0.035f), new Vector2(0.255f, 0.15f), Ink);
        AddAccent(cargoCard.transform, Teal);
        CreateText("CargoCaption", cargoCard.transform, "CARPETAS EN LA ESPALDA", 14, Muted, TextAnchor.MiddleLeft, FontStyle.Bold,
            new Vector2(0.08f, 0.47f), new Vector2(0.95f, 0.88f));
        Text carried = CreateText("CarriedDocumentsText", cargoCard.transform, "CARGA   0 / 5", 24, White, TextAnchor.MiddleLeft, FontStyle.Bold,
            new Vector2(0.08f, 0.05f), new Vector2(0.95f, 0.55f));

        Image energyCard = CreatePanel("EnergyCard", hudRoot, new Vector2(0.025f, 0.165f), new Vector2(0.255f, 0.235f), Card);
        CreateText("EnergyPlaceholder", energyCard.transform, "ENERGÍA   —", 16, Muted, TextAnchor.MiddleLeft, FontStyle.Bold,
            new Vector2(0.06f, 0.1f), new Vector2(0.94f, 0.9f));
        Slider energyPlaceholder = CreateProgressBar("EnergyBarPlaceholder", energyCard.transform, new Vector2(0.53f, 0.25f), new Vector2(0.95f, 0.75f), Muted);
        energyPlaceholder.value = 0f;

        Image deliveryCard = CreatePanel("DeliveryCard", hudRoot, new Vector2(0.31f, 0.035f), new Vector2(0.69f, 0.15f), Ink);
        CreateText("DeliveryText", deliveryCard.transform, "ENTREGA   QUÉDATE 2 SEGUNDOS EN TU ARCHIVERO", 15, White, TextAnchor.MiddleCenter, FontStyle.Bold,
            new Vector2(0.03f, 0.48f), new Vector2(0.97f, 0.92f));
        Slider delivery = CreateProgressBar("DeliveryProgress", deliveryCard.transform, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.38f), Teal);

        Image diagnosticCard = CreatePanel("DiagnosticCard", hudRoot, new Vector2(0.82f, 0.035f), new Vector2(0.975f, 0.15f), Card);
        Text diagnostics = CreateText("DiagnosticsText", diagnosticCard.transform, "FPS  --\nPING  --", 14, Muted, TextAnchor.MiddleCenter, FontStyle.Bold,
            new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));

        GameObject resultOverlay = new GameObject("ResultsPanel", typeof(RectTransform), typeof(Image));
        resultOverlay.transform.SetParent(hudRoot, false);
        SetStretch(resultOverlay.GetComponent<RectTransform>());
        resultOverlay.GetComponent<Image>().color = new Color(0.035f, 0.05f, 0.07f, 0.88f);
        resultOverlay.GetComponent<Image>().raycastTarget = false;
        Image resultCard = CreatePanel("ResultsCard", resultOverlay.transform, new Vector2(0.29f, 0.28f), new Vector2(0.71f, 0.72f), Card);
        AddAccent(resultCard.transform, Teal);
        Text result = CreateText("ResultsText", resultCard.transform, "¡EMPLEADO DEL MES!\n\n00 — 00 DOCUMENTOS", 34, White, TextAnchor.MiddleCenter, FontStyle.Bold,
            new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f));
        resultOverlay.SetActive(false);

        OfficeHudController controller = canvasObject.AddComponent<OfficeHudController>();
        controller.playerOne = FindPlayerById(0);
        controller.playerTwo = FindPlayerById(1);
        controller.playerOneDeliveryZone = FindDeliveryZoneForPlayer(0);
        controller.timerText = timer;
        controller.playerOneScoreText = scoreOne;
        controller.playerTwoScoreText = scoreTwo;
        controller.carriedDocumentsText = carried;
        controller.deliveryText = deliveryCard.transform.Find("DeliveryText").GetComponent<Text>();
        controller.diagnosticsText = diagnostics;
        controller.resultText = result;
        controller.deliveryProgress = delivery;
        controller.energyPlaceholder = energyPlaceholder;
        controller.resultsPanel = resultOverlay;
        controller.InitializeHud();

        EnsureEventSystem();
        Selection.activeGameObject = canvasObject;
        EditorSceneManager.MarkSceneDirty(activeScene);
        Debug.Log("Office HUD creado y conectado en Sc_Game.");
    }

    private static void AddPlayerScoreCard(Transform parent, string name, string playerLabel, Color accent, bool left, out Text score)
    {
        Vector2 min = left ? new Vector2(0.012f, 0.07f) : new Vector2(0.728f, 0.07f);
        Vector2 max = left ? new Vector2(0.292f, 0.93f) : new Vector2(0.988f, 0.93f);
        Image card = CreatePanel(name, parent, min, max, Card);
        AddAccent(card.transform, accent, !left);
        float labelMin = left ? 0.13f : 0.05f;
        float labelMax = left ? 0.76f : 0.67f;
        CreateText(name + "Label", card.transform, playerLabel, 15, Muted, left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, FontStyle.Bold,
            new Vector2(labelMin, 0.12f), new Vector2(labelMax, 0.88f));
        score = CreateText(name + "Score", card.transform, "00", 32, accent, left ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, FontStyle.Bold,
            left ? new Vector2(0.76f, 0.08f) : new Vector2(0.33f, 0.08f),
            left ? new Vector2(0.96f, 0.92f) : new Vector2(0.57f, 0.92f));
    }

    private static OfficePlayerMotor FindPlayerById(int playerId)
    {
        OfficePlayerMotor[] players = Object.FindObjectsByType<OfficePlayerMotor>(FindObjectsSortMode.None);
        foreach (OfficePlayerMotor player in players)
        {
            if (player.PlayerId == playerId)
                return player;
        }
        return null;
    }

    private static OfficeDeliveryZone FindDeliveryZoneForPlayer(int playerId)
    {
        OfficeDeliveryZone[] zones = Object.FindObjectsByType<OfficeDeliveryZone>(FindObjectsSortMode.None);
        foreach (OfficeDeliveryZone zone in zones)
        {
            SerializedObject serializedZone = new SerializedObject(zone);
            if (serializedZone.FindProperty("ownerPlayerId").intValue == playerId)
                return zone;
        }
        return null;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
        Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create event system");
        InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static Image CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void AddAccent(Transform parent, Color color, bool right = false)
    {
        Image accent = CreatePanel("Accent", parent,
            right ? new Vector2(0.975f, 0.12f) : new Vector2(0.025f, 0.12f),
            right ? new Vector2(1f, 0.88f) : new Vector2(0.05f, 0.88f), color);
        accent.raycastTarget = false;
    }

    private static Text CreateText(string name, Transform parent, string value, int fontSize, Color color,
        TextAnchor alignment, FontStyle style, Vector2? anchorMin = null, Vector2? anchorMax = null)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin ?? Vector2.zero;
        rect.anchorMax = anchorMax ?? Vector2.one;
        rect.offsetMin = new Vector2(8f, 2f);
        rect.offsetMax = new Vector2(-8f, -2f);

        Text text = gameObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Slider CreateProgressBar(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color fillColor)
    {
        GameObject barObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Slider));
        barObject.transform.SetParent(parent, false);
        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.anchorMin = anchorMin;
        barRect.anchorMax = anchorMax;
        barRect.offsetMin = new Vector2(5f, 2f);
        barRect.offsetMax = new Vector2(-5f, -2f);

        Image background = barObject.GetComponent<Image>();
        background.color = new Color32(54, 68, 80, 255);
        background.raycastTarget = false;

        GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(barObject.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(3f, 3f);
        fillAreaRect.offsetMax = new Vector2(-3f, -3f);

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fill = fillObject.GetComponent<Image>();
        fill.color = fillColor;
        fill.raycastTarget = false;

        Slider slider = barObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.wholeNumbers = false;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.targetGraphic = background;
        slider.fillRect = fillRect;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetAnchoredRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
#endif
