using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>Small Host/Client lobby for a two-player office match.</summary>
public sealed class OfficeFusionRoomController : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Network player")]
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private NetworkObject lobbyStatePrefab;
    [SerializeField] private Transform playerOneSpawn;
    [SerializeField] private Transform playerTwoSpawn;
    [SerializeField] private GameObject offlinePlayerTemplate;

    [Header("Lobby")]
    [SerializeField] private string defaultRoomName = "salario-minimo";
    [SerializeField] private string fixedPhotonRegion = "us";

    [Header("Backend de resultados")]
    [SerializeField] private string backendBaseUrl = "https://perrobbackend.onrender.com";
    [SerializeField] private string matchIngestToken;

    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkRunner runner;
    private OfficeFusionPlayer localPlayer;
    private OfficeFusionLobbyState lobbyState;
    private NetworkObject lobbyStateObject;
    private OfficeFusionPlayer[] playersBySlot = new OfficeFusionPlayer[2];
    private string roomName;
    private string status = "Crea una sala o únete a una existente.";
    private bool connecting;
    private string currentMatchId;
    private DateTime currentMatchStartedAt;
    private bool matchReportStarted;
    private OfficeFusionLobbyState CurrentLobbyState
    {
        get
        {
            if (lobbyState != null && lobbyState.IsReadyStateAvailable)
                return lobbyState;

            OfficeFusionLobbyState sceneState = FindFirstObjectByType<OfficeFusionLobbyState>();
            return sceneState != null && sceneState.IsReadyStateAvailable ? sceneState : null;
        }
    }

    public bool IsConfigured => playerPrefab != null;
    public string Status => status;
    public string RoomName => roomName;
    public bool IsConnecting => connecting;
    public bool HasActiveSession => runner != null && runner.IsRunning;
    public bool IsHost => runner != null && runner.IsRunning && runner.IsServer;
    public int ConnectedPlayers => HasActiveSession ? runner.ActivePlayers.Count() : 0;
    public bool HasLobbyState => CurrentLobbyState != null;
    public bool LocalPlayerReady => CurrentLobbyState != null && runner != null &&
                                    CurrentLobbyState.IsReady(runner.LocalPlayer);
    public bool BothPlayersReady => CurrentLobbyState != null && CurrentLobbyState.BothPlayersReady;
    public bool CanStartMatch => IsHost && ConnectedPlayers == 2 && BothPlayersReady;
    public OfficeFusionPlayer LocalNetworkPlayer => localPlayer;
    public int LocalPlayerSlot => localPlayer != null ? localPlayer.PlayerSlot : -1;
    public float PingMilliseconds => HasActiveSession
        ? (float)(runner.GetPlayerRtt(runner.LocalPlayer) * 1000.0)
        : 0f;
    public string ReadySummary => CurrentLobbyState != null
        ? CurrentLobbyState.GetReadySummary()
        : "Esperando a que se conecte el segundo empleado...";

    private void Awake()
    {
        roomName = defaultRoomName;
        Application.targetFrameRate = 60;
        if (offlinePlayerTemplate != null)
            offlinePlayerTemplate.SetActive(false);
    }

    public void Configure(NetworkObject prefab, Transform spawnOne, Transform spawnTwo, GameObject template)
    {
        playerPrefab = prefab;
        playerOneSpawn = spawnOne;
        playerTwoSpawn = spawnTwo;
        offlinePlayerTemplate = template;
    }

    public void ConfigureLobbyState(NetworkObject prefab)
    {
        lobbyStatePrefab = prefab;
    }

    public void CreateRoom(string code) => StartSession(GameMode.Host, code);

    public void JoinRoom(string code) => StartSession(GameMode.Client, code);

    public void ToggleReady()
    {
        OfficeFusionLobbyState state = CurrentLobbyState;
        if (!HasActiveSession || ConnectedPlayers != 2 || state == null || runner == null)
            return;

        bool ready = !state.IsReady(runner.LocalPlayer);
        if (state.Object != null && state.Object.HasStateAuthority)
            state.QueueHostReadyChange(runner.LocalPlayer, ready);
        else
            state.RPC_SetReady(ready);
    }

    public void StartMatch()
    {
        if (!CanStartMatch)
            return;

        int gameSceneIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/Sc_Game.unity");
        if (gameSceneIndex < 0)
        {
            status = "Sc_Game no está incluida en Build Settings.";
            return;
        }

        status = "Los dos empleados están listos. Abriendo la oficina...";
        currentMatchId = Guid.NewGuid().ToString();
        currentMatchStartedAt = DateTime.UtcNow;
        matchReportStarted = false;
        runner.LoadScene(SceneRef.FromIndex(gameSceneIndex), LoadSceneMode.Single, LocalPhysicsMode.None, true);
    }

    public string GetLocalTelemetryPlayerId()
    {
        const string key = "Perrob.TelemetryPlayerId";
        string id = PlayerPrefs.GetString(key, string.Empty);
        if (!string.IsNullOrEmpty(id))
            return id;

        id = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(key, id);
        PlayerPrefs.Save();
        return id;
    }

    public OfficeFusionPlayer FindPlayerByInputAuthority(PlayerRef owner)
    {
        foreach (OfficeFusionPlayer player in playersBySlot)
        {
            if (player != null && player.Object != null && player.Object.InputAuthority == owner)
                return player;
        }
        return null;
    }

    public void ReportMatchOnce(OfficeFusionPlayer playerOne, OfficeFusionPlayer playerTwo)
    {
        if (!IsHost || matchReportStarted || playerOne == null || playerTwo == null ||
            string.IsNullOrEmpty(currentMatchId))
            return;

        string token = string.IsNullOrWhiteSpace(matchIngestToken)
            ? Environment.GetEnvironmentVariable("PERROB_MATCH_INGEST_TOKEN")
            : matchIngestToken.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            Debug.LogWarning("No se enviará la partida: configura Match Ingest Token en OfficeFusionRoomController.", this);
            return;
        }

        DateTime endedAt = DateTime.UtcNow;
        float duration = (float)(endedAt - currentMatchStartedAt).TotalSeconds;
        if (duration < 1f)
            return;

        MatchReport report = new MatchReport
        {
            matchId = currentMatchId,
            roomCode = roomName != null && roomName.Length > 64 ? roomName.Substring(0, 64) : roomName,
            startedAt = currentMatchStartedAt.ToString("O"),
            endedAt = endedAt.ToString("O"),
            players = new[]
            {
                MakePlayerReport(playerOne, duration),
                MakePlayerReport(playerTwo, duration)
            }
        };
        matchReportStarted = true;
        StartCoroutine(SendMatchReport(JsonUtility.ToJson(report), token, currentMatchId));
    }

    private static MatchPlayerReport MakePlayerReport(OfficeFusionPlayer player, float duration)
    {
        string playerId = player.TelemetryPlayerId.ToString();
        if (string.IsNullOrWhiteSpace(playerId))
            playerId = "fusion-slot-" + player.PlayerSlot;
        return new MatchPlayerReport
        {
            slot = player.PlayerSlot,
            playerId = playerId,
            deliveredDocuments = player.DeliveredDocuments,
            paperBallsThrown = player.PaperBallsThrown,
            paperHitsLanded = player.PaperHitsLanded,
            paperHitsReceived = player.PaperHitsReceived,
            knockouts = player.Knockouts,
            deaths = player.Deaths,
            survivalSeconds = Mathf.Clamp(player.SurvivalSeconds, 0f, duration)
        };
    }

    private IEnumerator SendMatchReport(string json, string token, string matchId)
    {
        string url = backendBaseUrl.TrimEnd('/') + "/api/matches";
        byte[] body = Encoding.UTF8.GetBytes(json);
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + token);
                request.timeout = 90;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success &&
                    (request.responseCode == 200 || request.responseCode == 201))
                {
                    Debug.Log("Partida " + matchId + " guardada en el backend.", this);
                    yield break;
                }

                if (request.responseCode >= 400 && request.responseCode < 500)
                {
                    Debug.LogError("El backend rechazó la partida (HTTP " + request.responseCode +
                        "): " + request.downloadHandler.text, this);
                    yield break;
                }

                Debug.LogWarning("No se pudo enviar la partida (intento " + attempt + "/3): " +
                    request.error, this);
            }

            yield return new WaitForSecondsRealtime(2f * attempt);
        }
    }

    [Serializable]
    private sealed class MatchReport
    {
        public string matchId;
        public string roomCode;
        public string startedAt;
        public string endedAt;
        public MatchPlayerReport[] players;
    }

    [Serializable]
    private sealed class MatchPlayerReport
    {
        public int slot;
        public string playerId;
        public int deliveredDocuments;
        public int paperBallsThrown;
        public int paperHitsLanded;
        public int paperHitsReceived;
        public int knockouts;
        public int deaths;
        public float survivalSeconds;
    }

    private async void StartSession(GameMode mode, string code)
    {
        if (connecting || HasActiveSession)
            return;

        roomName = string.IsNullOrWhiteSpace(code)
            ? defaultRoomName.Trim().ToLowerInvariant()
            : code.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(roomName))
            roomName = "salario-minimo";

        string region = string.IsNullOrWhiteSpace(fixedPhotonRegion)
            ? string.Empty
            : fixedPhotonRegion.Trim().ToLowerInvariant();

        connecting = true;
        status = mode == GameMode.Host ? "Creando sala..." : "Buscando la sala...";
        DontDestroyOnLoad(gameObject);
        runner = new GameObject("OfficeFusionRunner").AddComponent<NetworkRunner>();
        runner.ProvideInput = true;
        runner.AddCallbacks(this);
        runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        runner.gameObject.AddComponent<NetworkObjectProviderDefault>();

        FusionAppSettings photonSettings = PhotonAppSettings.Global.AppSettings.GetCopy();
        if (!string.IsNullOrEmpty(region))
            photonSettings.FixedRegion = region;

        StartGameResult result;
        try
        {
            result = await runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = roomName,
                PlayerCount = 2,
                CustomPhotonAppSettings = photonSettings,
                SceneManager = runner.GetComponent<NetworkSceneManagerDefault>()
            });
        }
        catch (Exception exception)
        {
            connecting = false;
            string action = mode == GameMode.Host ? "crear" : "encontrar";
            string regionLabel = string.IsNullOrEmpty(region) ? "automática" : region;
            status = $"No se pudo {action} la sala '{roomName}' en región {regionLabel}: {exception.Message}";
            if (runner != null)
            {
                runner.RemoveCallbacks(this);
                Destroy(runner.gameObject);
                runner = null;
            }
            return;
        }

        connecting = false;
        if (!result.Ok)
        {
            string action = mode == GameMode.Host ? "crear" : "encontrar";
            string regionLabel = string.IsNullOrEmpty(region) ? "automática" : region;
            status = $"No se pudo {action} la sala '{roomName}' en región {regionLabel}: " +
                     $"{result.ShutdownReason}. Revisa que ambos clientes usen el mismo código y región.";
            runner.RemoveCallbacks(this);
            Destroy(runner.gameObject);
            runner = null;
            return;
        }

        string connectedRegion = runner.SessionInfo.Region;
        status = mode == GameMode.Host
            ? $"Sala '{roomName}' creada en {connectedRegion}. Esperando al segundo empleado..."
            : $"Conectado a '{roomName}' en {connectedRegion}. Esperando al inicio...";
    }

    public void RegisterSpawnedPlayer(int slot, OfficeFusionPlayer player)
    {
        if (slot < 0 || slot >= playersBySlot.Length || player == null)
            return;

        playersBySlot[slot] = player;
        if (player.Object != null && player.Object.HasInputAuthority)
            localPlayer = player;

        OfficeHudController hud = FindFirstObjectByType<OfficeHudController>();
        if (hud != null)
            hud.RegisterNetworkPlayer(slot, player);

        if (playersBySlot[0] != null && playersBySlot[1] != null)
            status = "¡Los dos empleados están en la oficina!";
    }

    public void UnregisterSpawnedPlayer(OfficeFusionPlayer player)
    {
        if (player == null)
            return;

        if (localPlayer == player)
            localPlayer = null;

        for (int i = 0; i < playersBySlot.Length; i++)
        {
            if (playersBySlot[i] == player)
                playersBySlot[i] = null;
        }
    }

    public void RegisterLobbyState(OfficeFusionLobbyState state)
    {
        if (state != null && state.IsReadyStateAvailable)
            lobbyState = state;
    }

    public void OnPlayerJoined(NetworkRunner activeRunner, PlayerRef player)
    {
        status = $"Conectado: {activeRunner.ActivePlayers.Count()}/2 empleados.";

        if (!activeRunner.IsServer)
            return;

        if (activeRunner.ActivePlayers.Count() == 2 && lobbyStateObject == null)
        {
            if (lobbyStatePrefab == null)
            {
                status = "Falta el prefab de estado del lobby. Revisa la configuración de Sc_Menu.";
                return;
            }

            lobbyStateObject = activeRunner.Spawn(lobbyStatePrefab, Vector3.zero, Quaternion.identity);
        }

    }

    public void OnPlayerLeft(NetworkRunner activeRunner, PlayerRef player)
    {
        if (activeRunner.IsServer && spawnedPlayers.TryGetValue(player, out NetworkObject networkPlayer))
        {
            if (networkPlayer != null)
                activeRunner.Despawn(networkPlayer);
            spawnedPlayers.Remove(player);
        }

        if (activeRunner.IsServer && CurrentLobbyState != null)
            CurrentLobbyState.ClearReadyState();

        status = "Un empleado salió de la sala.";
    }

    public void OnInput(NetworkRunner activeRunner, NetworkInput input)
    {
        if (localPlayer != null && localPlayer.HasLocalInputAuthority)
            input.Set(localPlayer.ReadLocalInput());
    }

    public void OnShutdown(NetworkRunner activeRunner, ShutdownReason shutdownReason)
    {
        status = $"Sesión cerrada: {shutdownReason}";
        connecting = false;
        runner = null;
        localPlayer = null;
        lobbyState = null;
        lobbyStateObject = null;
        playersBySlot = new OfficeFusionPlayer[2];
        spawnedPlayers.Clear();
    }

    public void OnConnectedToServer(NetworkRunner activeRunner) => status = "Conectado a Photon.";
    public void OnDisconnectedFromServer(NetworkRunner activeRunner, NetDisconnectReason reason) => status = $"Desconectado: {reason}";
    public void OnConnectFailed(NetworkRunner activeRunner, NetAddress remoteAddress, NetConnectFailedReason reason) => status = $"Falló la conexión: {reason}";

    public void OnReliableDataProgress(NetworkRunner activeRunner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnObjectExitAOI(NetworkRunner activeRunner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner activeRunner, NetworkObject obj, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner activeRunner, PlayerRef player, NetworkInput input) { }
    public void OnConnectRequest(NetworkRunner activeRunner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnSessionListUpdated(NetworkRunner activeRunner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner activeRunner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner activeRunner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner activeRunner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner activeRunner) { }
    public void OnSceneLoadStart(NetworkRunner activeRunner) { }
}
