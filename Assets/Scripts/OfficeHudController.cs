using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays the local worker's vitals and both players' replicated score.</summary>
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

    private OfficeFusionPlayer networkPlayerOne;
    private OfficeFusionPlayer networkPlayerTwo;
    private OfficeFusionPlayer localNetworkPlayer;
    private float remainingSeconds;
    private float diagnosticsRefreshTimer;
    private float smoothedFps;
    private bool roundFinished;
    private bool eventsSubscribed;

    private OfficePlayerMotor LocalMotor => localNetworkPlayer != null ? localNetworkPlayer.Motor : playerOne;

    private void OnEnable()
    {
        remainingSeconds = roundDurationSeconds;
        SubscribeToEvents();
    }

    public void InitializeHud()
    {
        SubscribeToEvents();
        RefreshPlayerReadouts();
        RefreshEnergy();
        UpdateTimerText();
    }

    private void SubscribeToEvents()
    {
        if (eventsSubscribed)
            return;

        if (playerOne != null)
            playerOne.DeliveredDocumentsChanged += OnScoresChanged;
        if (playerTwo != null)
            playerTwo.DeliveredDocumentsChanged += OnScoresChanged;

        OfficePlayerMotor localMotor = LocalMotor;
        if (localMotor != null)
        {
            localMotor.CarriedDocumentsChanged += OnCarriedDocumentsChanged;
            localMotor.EnergyChanged += OnEnergyChanged;
        }

        if (playerOneDeliveryZone != null)
            playerOneDeliveryZone.ProgressChanged += OnDeliveryProgressChanged;

        eventsSubscribed = true;
    }

    private void OnDisable()
    {
        if (!eventsSubscribed)
            return;

        if (playerOne != null)
            playerOne.DeliveredDocumentsChanged -= OnScoresChanged;
        if (playerTwo != null)
            playerTwo.DeliveredDocumentsChanged -= OnScoresChanged;

        OfficePlayerMotor localMotor = LocalMotor;
        if (localMotor != null)
        {
            localMotor.CarriedDocumentsChanged -= OnCarriedDocumentsChanged;
            localMotor.EnergyChanged -= OnEnergyChanged;
        }

        if (playerOneDeliveryZone != null)
            playerOneDeliveryZone.ProgressChanged -= OnDeliveryProgressChanged;
        eventsSubscribed = false;
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

    private void Update()
    {
        UpdateDiagnostics();
        if (roundFinished)
            return;

        remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
        if (localNetworkPlayer != null)
            SetDeliveryProgress(localNetworkPlayer.DeliveryProgress);

        UpdateTimerText();
        if (remainingSeconds <= 0f)
            FinishRound();
    }

    private void OnCarriedDocumentsChanged(int count)
    {
        if (carriedDocumentsText != null)
            carriedDocumentsText.text = $"CARGA   {count} / {(LocalMotor != null ? LocalMotor.MaximumDocuments : 5)}";
    }

    private void OnScoresChanged(int _) => RefreshScores();
    private void OnEnergyChanged(float _, float __) => RefreshEnergy();

    private void RefreshEnergy()
    {
        if (energyText == null)
        {
            Transform label = transform.Find("HudRoot/EnergyCard/EnergyPlaceholder");
            if (label != null)
                energyText = label.GetComponent<Text>();
        }

        OfficePlayerMotor localMotor = LocalMotor;
        float current = localMotor != null ? localMotor.Energy : 100f;
        float maximum = localMotor != null ? localMotor.MaximumEnergy : 100f;
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
        if (player == LocalMotor)
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
        OfficePlayerMotor localMotor = LocalMotor;
        if (localMotor != null && carriedDocumentsText != null)
            carriedDocumentsText.text = $"CARGA   {localMotor.CarriedDocuments} / {localMotor.MaximumDocuments}";
        RefreshScores();
    }

    private void RefreshScores()
    {
        if (playerOneScoreText != null)
            playerOneScoreText.text = playerOne != null ? playerOne.DeliveredDocuments.ToString("00") : "00";
        if (playerTwoScoreText != null)
            playerTwoScoreText.text = playerTwo != null ? playerTwo.DeliveredDocuments.ToString("00") : "00";
    }

    public void RegisterNetworkPlayer(int slot, OfficeFusionPlayer networkPlayer)
    {
        UnsubscribeFromEvents();

        if (slot == 0)
        {
            networkPlayerOne = networkPlayer;
            playerOne = networkPlayer != null ? networkPlayer.Motor : null;
        }
        else if (slot == 1)
        {
            networkPlayerTwo = networkPlayer;
            playerTwo = networkPlayer != null ? networkPlayer.Motor : null;
        }
        else
        {
            return;
        }

        if (networkPlayer != null && networkPlayer.Object.HasInputAuthority)
            localNetworkPlayer = networkPlayer;

        SubscribeToEvents();
        RefreshPlayerReadouts();
        RefreshEnergy();
    }

    private void UnsubscribeFromEvents()
    {
        if (!eventsSubscribed)
            return;

        if (playerOne != null)
            playerOne.DeliveredDocumentsChanged -= OnScoresChanged;
        if (playerTwo != null)
            playerTwo.DeliveredDocumentsChanged -= OnScoresChanged;
        OfficePlayerMotor localMotor = LocalMotor;
        if (localMotor != null)
        {
            localMotor.CarriedDocumentsChanged -= OnCarriedDocumentsChanged;
            localMotor.EnergyChanged -= OnEnergyChanged;
        }
        if (playerOneDeliveryZone != null)
            playerOneDeliveryZone.ProgressChanged -= OnDeliveryProgressChanged;
        eventsSubscribed = false;
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
        OfficeFusionRoomController room = FindFirstObjectByType<OfficeFusionRoomController>();
        float ping = room != null ? room.PingMilliseconds : 0f;
        diagnosticsText.text = $"FPS  {Mathf.RoundToInt(smoothedFps)}\nPING  {(ping > 0f ? $"{ping:0} ms" : "--")}";
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
