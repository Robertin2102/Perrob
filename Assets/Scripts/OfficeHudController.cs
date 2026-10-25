using UnityEngine;
using UnityEngine.UI;

/// <summary>Connects the first-pass match HUD to the local player and delivery zone.</summary>
public sealed class OfficeHudController : MonoBehaviour
{
    [Header("Scene references")]
    public OfficePlayerMotor playerOne;
    public OfficePlayerMotor playerTwo;
    public OfficeDeliveryZone playerOneDeliveryZone;

    [Header("HUD references")]
    public Text timerText;
    public Text playerOneScoreText;
    public Text playerTwoScoreText;
    public Text carriedDocumentsText;
    public Text energyText;
    public Text deliveryText;
    public Text diagnosticsText;
    public Text resultText;
    public Slider deliveryProgress;
    public Slider energyPlaceholder;
    public GameObject resultsPanel;

    [Header("Prototype round")]
    [SerializeField, Min(1f)] private float roundDurationSeconds = 120f;

    private float remainingSeconds;
    private float diagnosticsRefreshTimer;
    private float smoothedFps;
    private bool roundFinished;
    private bool eventsSubscribed;

    private void OnEnable()
    {
        remainingSeconds = roundDurationSeconds;
        SubscribeToEvents();
    }

    /// <summary>Called by the scene builder after assigning scene references.</summary>
    public void InitializeHud()
    {
        SubscribeToEvents();
        RefreshPlayerReadouts();
        UpdateTimerText();
    }

    private void SubscribeToEvents()
    {
        if (eventsSubscribed)
            return;

        if (playerOne != null)
        {
            playerOne.CarriedDocumentsChanged += OnCarriedDocumentsChanged;
            playerOne.DeliveredDocumentsChanged += OnScoresChanged;
            playerOne.EnergyChanged += OnEnergyChanged;
            eventsSubscribed = true;
        }

        if (playerTwo != null)
        {
            playerTwo.DeliveredDocumentsChanged += OnScoresChanged;
            eventsSubscribed = true;
        }

        if (playerOneDeliveryZone != null)
        {
            playerOneDeliveryZone.ProgressChanged += OnDeliveryProgressChanged;
            eventsSubscribed = true;
        }
    }

    private void Start()
    {
        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        RefreshPlayerReadouts();
        RefreshEnergy();
        UpdateTimerText();
        SetDeliveryProgress(0f);
    }

    private void OnDisable()
    {
        if (!eventsSubscribed)
            return;

        if (playerOne != null)
        {
            playerOne.CarriedDocumentsChanged -= OnCarriedDocumentsChanged;
            playerOne.DeliveredDocumentsChanged -= OnScoresChanged;
            playerOne.EnergyChanged -= OnEnergyChanged;
        }

        if (playerTwo != null)
            playerTwo.DeliveredDocumentsChanged -= OnScoresChanged;

        if (playerOneDeliveryZone != null)
            playerOneDeliveryZone.ProgressChanged -= OnDeliveryProgressChanged;

        eventsSubscribed = false;
    }

    private void Update()
    {
        UpdateDiagnostics();
        if (roundFinished)
            return;

        remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
        UpdateTimerText();
        if (remainingSeconds <= 0f)
            FinishRound();
    }

    private void OnCarriedDocumentsChanged(int count)
    {
        if (carriedDocumentsText != null)
            carriedDocumentsText.text = $"CARGA   {count} / 5";
    }

    private void OnScoresChanged(int _) => RefreshScores();

    private void OnEnergyChanged(float _, float __) => RefreshEnergy();

    private void RefreshEnergy()
    {
        // The editor-built HUD predates this binding, so find its existing label by name.
        if (energyText == null)
        {
            Transform label = transform.Find("HudRoot/EnergyCard/EnergyPlaceholder");
            if (label != null)
                energyText = label.GetComponent<Text>();
        }

        float current = playerOne != null ? playerOne.Energy : 100f;
        float maximum = playerOne != null ? playerOne.MaximumEnergy : 100f;
        if (energyPlaceholder != null)
        {
            energyPlaceholder.minValue = 0f;
            energyPlaceholder.maxValue = maximum;
            energyPlaceholder.value = current;

            if (energyPlaceholder.fillRect != null)
            {
                Image fill = energyPlaceholder.fillRect.GetComponent<Image>();
                if (fill != null)
                    fill.color = new Color32(255, 122, 102, 255);
            }
        }

        if (energyText != null)
            energyText.text = $"ENERGÍA   {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}";
    }

    private void OnDeliveryProgressChanged(OfficePlayerMotor player, float progress)
    {
        if (player == playerOne)
            SetDeliveryProgress(progress);
    }

    private void SetDeliveryProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (deliveryProgress != null)
            deliveryProgress.value = progress;

        if (deliveryText != null)
            deliveryText.text = progress > 0f
                ? $"ENTREGANDO   {Mathf.RoundToInt(progress * 100f)}%"
                : "ENTREGA   QUÉDATE 2 SEGUNDOS EN TU ARCHIVERO";
    }

    private void RefreshPlayerReadouts()
    {
        if (playerOne != null && carriedDocumentsText != null)
            carriedDocumentsText.text = $"CARGA   {playerOne.CarriedDocuments} / 5";

        RefreshScores();
    }

    private void RefreshScores()
    {
        if (playerOneScoreText != null)
            playerOneScoreText.text = playerOne != null ? playerOne.DeliveredDocuments.ToString("00") : "00";

        if (playerTwoScoreText != null)
            playerTwoScoreText.text = playerTwo != null ? playerTwo.DeliveredDocuments.ToString("00") : "00";
    }

    private void UpdateTimerText()
    {
        if (timerText == null)
            return;

        int wholeSeconds = Mathf.CeilToInt(remainingSeconds);
        timerText.text = $"{wholeSeconds / 60:00}:{wholeSeconds % 60:00}";
    }

    private void UpdateDiagnostics()
    {
        if (diagnosticsText == null)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        if (deltaTime > 0f)
            smoothedFps = Mathf.Lerp(smoothedFps, 1f / deltaTime, 1f - Mathf.Exp(-deltaTime * 4f));

        diagnosticsRefreshTimer -= deltaTime;
        if (diagnosticsRefreshTimer > 0f)
            return;

        diagnosticsRefreshTimer = 0.25f;
        diagnosticsText.text = $"FPS  {Mathf.RoundToInt(smoothedFps)}\nPING  --";
    }

    private void FinishRound()
    {
        roundFinished = true;
        int scoreOne = playerOne != null ? playerOne.DeliveredDocuments : 0;
        int scoreTwo = playerTwo != null ? playerTwo.DeliveredDocuments : 0;

        if (resultText != null)
        {
            string result = scoreOne == scoreTwo
                ? "¡EMPATE!"
                : scoreOne > scoreTwo ? "¡EMPLEADO 01 DEL MES!" : "¡EMPLEADO 02 DEL MES!";
            resultText.text = $"¡EMPLEADO DEL MES!\n{result}\n\n{scoreOne:00}  —  {scoreTwo:00} DOCUMENTOS";
        }

        if (resultsPanel != null)
            resultsPanel.SetActive(true);
    }
}
