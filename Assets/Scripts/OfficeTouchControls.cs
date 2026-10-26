using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>Runtime touch joystick and aim/release-to-throw controls for the local player.</summary>
public sealed class OfficeTouchControls : MonoBehaviour
{
    private const float DeadZone = 0.12f;

    private OfficePlayerMotor motor;
    private OfficePaperThrower thrower;
    private GameObject canvasObject;
    private RectTransform canvasRect;
    private UnityEngine.UI.Image moveBase;
    private UnityEngine.UI.Image moveKnob;
    private UnityEngine.UI.Image aimBase;
    private UnityEngine.UI.Image aimKnob;
    private Sprite circleSprite;
    private Texture2D circleTexture;
    private int moveFingerId = -1;
    private int aimFingerId = -1;
    private Vector2 moveOrigin;
    private Vector2 aimOrigin;
    private float joystickRadius;
    private float aimRadius;

    public void Initialize(OfficePlayerMotor playerMotor, OfficePaperThrower paperThrower)
    {
        motor = playerMotor;
        thrower = paperThrower;
        if (thrower != null)
            thrower.SetTouchInputMode(true);
        BuildControls();
    }

    private void Update()
    {
        if (motor == null || Touchscreen.current == null)
            return;

        joystickRadius = Mathf.Min(Screen.width, Screen.height) * 0.12f;
        aimRadius = joystickRadius;
        bool sawMoveFinger = false;
        bool sawAimFinger = false;
        var touches = Touchscreen.current.touches;

        for (int i = 0; i < touches.Count; i++)
        {
            TouchControl touch = touches[i];
            int id = touch.touchId.ReadValue();
            Vector2 position = touch.position.ReadValue();
            bool pressed = touch.press.isPressed;

            if (touch.press.wasPressedThisFrame)
            {
                if (moveFingerId < 0 && position.x < Screen.width * 0.48f && position.y < Screen.height * 0.68f)
                {
                    moveFingerId = id;
                    moveOrigin = position;
                    PlaceAtScreenPosition(moveBase.rectTransform, moveOrigin);
                    PlaceAtScreenPosition(moveKnob.rectTransform, moveOrigin);
                }
                else if (aimFingerId < 0 && position.x > Screen.width * 0.52f && position.y < Screen.height * 0.68f)
                {
                    aimFingerId = id;
                    aimOrigin = position;
                    PlaceAtScreenPosition(aimBase.rectTransform, aimOrigin);
                    PlaceAtScreenPosition(aimKnob.rectTransform, aimOrigin);
                }
            }

            if (id == moveFingerId)
            {
                sawMoveFinger = true;
                if (pressed)
                    UpdateMoveStick(position);
                else if (touch.press.wasReleasedThisFrame)
                    ReleaseMoveStick();
            }

            if (id == aimFingerId)
            {
                sawAimFinger = true;
                if (pressed)
                    UpdateAimStick(position);
                else if (touch.press.wasReleasedThisFrame)
                    ReleaseAimStick(true);
            }
        }

        // Recover cleanly if the operating system cancels a touch (for example, an app interruption).
        if (moveFingerId >= 0 && !sawMoveFinger)
            ReleaseMoveStick();
        if (aimFingerId >= 0 && !sawAimFinger)
            ReleaseAimStick(false);
    }

    private void BuildControls()
    {
        if (canvasObject != null)
            return;

        canvasObject = new GameObject("OfficeTouchControlsCanvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = canvasObject.GetComponent<RectTransform>();

        circleTexture = CreateCircleTexture(128);
        circleSprite = Sprite.Create(circleTexture, new Rect(0f, 0f, circleTexture.width, circleTexture.height),
            new Vector2(0.5f, 0.5f), 128f);

        RectTransform root = CreateRect("Controls", canvasRect);
        Stretch(root);
        moveBase = CreatePad("MoveBase", root, new Color32(30, 46, 58, 115));
        moveKnob = CreatePad("MoveThumb", root, new Color32(82, 212, 184, 190));
        aimBase = CreatePad("AimBase", root, new Color32(58, 41, 49, 120));
        aimKnob = CreatePad("AimThumb", root, new Color32(255, 134, 109, 205));

        float initialRadius = Mathf.Min(Screen.width, Screen.height) * 0.12f;
        Vector2 moveHome = new Vector2(Screen.width * 0.15f, Screen.height * 0.43f);
        Vector2 aimHome = new Vector2(Screen.width * 0.85f, Screen.height * 0.43f);
        PlaceAtScreenPosition(moveBase.rectTransform, moveHome);
        PlaceAtScreenPosition(moveKnob.rectTransform, moveHome);
        PlaceAtScreenPosition(aimBase.rectTransform, aimHome);
        PlaceAtScreenPosition(aimKnob.rectTransform, aimHome);
        float canvasScale = Mathf.Max(canvas.scaleFactor, 0.01f);
        SetPadSize(moveBase.rectTransform, initialRadius * 2f / canvasScale);
        SetPadSize(moveKnob.rectTransform, initialRadius * 0.88f / canvasScale);
        SetPadSize(aimBase.rectTransform, initialRadius * 2f / canvasScale);
        SetPadSize(aimKnob.rectTransform, initialRadius * 0.88f / canvasScale);

        CreateLabel(root, "MOVER", new Vector2(0.15f, 0.27f), new Color32(231, 240, 236, 205));
        CreateLabel(root, "APUNTA Y SUELTA", new Vector2(0.85f, 0.27f), new Color32(255, 224, 216, 220));
    }

    private void UpdateMoveStick(Vector2 position)
    {
        Vector2 delta = Vector2.ClampMagnitude(position - moveOrigin, joystickRadius);
        PlaceAtScreenPosition(moveKnob.rectTransform, moveOrigin + delta);
        Vector2 normalized = delta / Mathf.Max(joystickRadius, 1f);
        motor.SetMoveInput(normalized.magnitude < DeadZone ? Vector2.zero : normalized);
    }

    private void UpdateAimStick(Vector2 position)
    {
        Vector2 delta = Vector2.ClampMagnitude(position - aimOrigin, aimRadius);
        PlaceAtScreenPosition(aimKnob.rectTransform, aimOrigin + delta);
        Vector2 normalized = delta / Mathf.Max(aimRadius, 1f);
        if (normalized.magnitude >= DeadZone)
            motor.SetTouchAimDirection(normalized);
    }

    private void ReleaseMoveStick()
    {
        moveFingerId = -1;
        motor.SetMoveInput(Vector2.zero);
        Vector2 home = new Vector2(Screen.width * 0.15f, Screen.height * 0.43f);
        PlaceAtScreenPosition(moveBase.rectTransform, home);
        PlaceAtScreenPosition(moveKnob.rectTransform, home);
    }

    private void ReleaseAimStick(bool fire)
    {
        aimFingerId = -1;
        Vector2 home = new Vector2(Screen.width * 0.85f, Screen.height * 0.43f);
        PlaceAtScreenPosition(aimBase.rectTransform, home);
        PlaceAtScreenPosition(aimKnob.rectTransform, home);
        if (fire && thrower != null)
            thrower.RequestThrow();
    }

    private UnityEngine.UI.Image CreatePad(string name, Transform parent, Color color)
    {
        GameObject padObject = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        padObject.transform.SetParent(parent, false);
        UnityEngine.UI.Image image = padObject.GetComponent<UnityEngine.UI.Image>();
        image.sprite = circleSprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void CreateLabel(Transform parent, string value, Vector2 anchor, Color color)
    {
        GameObject labelObject = new GameObject(value + "Hint", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(300f, 36f);
        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 22;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = color;
        label.raycastTarget = false;
        Outline outline = labelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.05f, 0.07f, 0.09f, 0.8f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private void PlaceAtScreenPosition(RectTransform rect, Vector2 screenPosition)
    {
        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, null, out Vector2 localPosition))
            rect.anchoredPosition = localPosition;
    }

    private static void SetPadSize(RectTransform rect, float size)
    {
        rect.sizeDelta = new Vector2(size, size);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject rectObject = new GameObject(name, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return rectObject.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Texture2D CreateCircleTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "OfficeTouchCircle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = center - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                byte alpha = distance <= radius ? (byte)255 : (byte)0;
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void OnDestroy()
    {
        if (motor != null)
            motor.SetMoveInput(Vector2.zero);
        if (canvasObject != null)
            Destroy(canvasObject);
        if (circleSprite != null)
            Destroy(circleSprite);
        if (circleTexture != null)
            Destroy(circleTexture);
    }
}
