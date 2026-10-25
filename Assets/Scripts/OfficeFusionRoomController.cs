using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
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

    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkRunner runner;
    private OfficeFusionPlayer localPlayer;
    private OfficeFusionLobbyState lobbyState;
    private NetworkObject lobbyStateObject;
    private OfficeFusionPlayer[] playersBySlot = new OfficeFusionPlayer[2];
    private string roomName;
    private string status = "Crea una sala o únete a una existente.";
    private bool connecting;
    private OfficeFusionLobbyState CurrentLobbyState =>
        lobbyState != null ? lobbyState : FindFirstObjectByType<OfficeFusionLobbyState>();

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
        runner.LoadScene(SceneRef.FromIndex(gameSceneIndex), LoadSceneMode.Single, LocalPhysicsMode.None, true);
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

    public void RegisterLobbyState(OfficeFusionLobbyState state)
    {
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
        if (localPlayer != null)
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
