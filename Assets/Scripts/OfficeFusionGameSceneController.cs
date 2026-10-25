using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Spawns one network-controlled office worker for each player in the running session.</summary>
public sealed class OfficeFusionGameSceneController : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private Transform playerOneSpawn;
    [SerializeField] private Transform playerTwoSpawn;
    [SerializeField] private GameObject offlinePlayerTemplate;

    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
    private readonly HashSet<PlayerRef> spawningPlayers = new HashSet<PlayerRef>();
    private NetworkRunner runner;
    private bool callbacksRegistered;
    private bool returnToMenuScheduled;

    public void Configure(NetworkObject prefab, Transform spawnOne, Transform spawnTwo, GameObject template)
    {
        playerPrefab = prefab;
        playerOneSpawn = spawnOne;
        playerTwoSpawn = spawnTwo;
        offlinePlayerTemplate = template;
    }

    public bool TryGetSpawnPose(int slot, out Vector3 position, out Quaternion rotation)
    {
        Transform spawn = slot == 0 ? playerOneSpawn : playerTwoSpawn;
        if (spawn != null)
        {
            position = spawn.position;
            rotation = spawn.rotation;
            return true;
        }

        position = default;
        rotation = Quaternion.identity;
        return false;
    }

    private void Awake()
    {
        runner = FindRunningRunner();
        if (runner == null)
            return;

        if (offlinePlayerTemplate != null)
            offlinePlayerTemplate.SetActive(false);

        runner.AddCallbacks(this);
        callbacksRegistered = true;
    }

    private void Start()
    {
        // The session began in Sc_Menu, so its OnPlayerJoined callbacks happened before this
        // scene existed. The host must spawn the players already in the room here.
        if (runner == null || !runner.IsServer)
            return;

        foreach (PlayerRef player in runner.ActivePlayers.OrderBy(item => item.PlayerId))
            SpawnPlayer(player);
    }

    private void OnDestroy()
    {
        if (callbacksRegistered && runner != null)
            runner.RemoveCallbacks(this);
    }

    public void ReturnToMenuAfterResult(float delaySeconds = 10f)
    {
        if (runner == null || !runner.IsServer || returnToMenuScheduled)
            return;

        returnToMenuScheduled = true;
        StartCoroutine(ReturnToMenuRoutine(delaySeconds));
    }

    private IEnumerator ReturnToMenuRoutine(float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delaySeconds));

        if (runner == null || !runner.IsRunning)
            yield break;

        int menuSceneIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/Sc_Menu.unity");
        if (menuSceneIndex < 0)
        {
            Debug.LogError("Sc_Menu no está incluida en Build Settings; no se puede volver al lobby.", this);
            returnToMenuScheduled = false;
            yield break;
        }

        foreach (NetworkObject playerObject in spawnedPlayers.Values)
        {
            if (playerObject != null)
                runner.Despawn(playerObject);
        }
        spawnedPlayers.Clear();

        runner.LoadScene(SceneRef.FromIndex(menuSceneIndex), LoadSceneMode.Single,
            LocalPhysicsMode.None, true);
    }

    public void OnPlayerJoined(NetworkRunner activeRunner, PlayerRef player)
    {
        if (activeRunner.IsServer)
            SpawnPlayer(player);
    }

    public void OnPlayerLeft(NetworkRunner activeRunner, PlayerRef player)
    {
        if (!activeRunner.IsServer || !spawnedPlayers.TryGetValue(player, out NetworkObject playerObject))
            return;

        if (playerObject != null)
            activeRunner.Despawn(playerObject);
        spawnedPlayers.Remove(player);
        spawningPlayers.Remove(player);
    }

    private async void SpawnPlayer(PlayerRef player)
    {
        if (runner == null || !runner.IsServer || playerPrefab == null ||
            spawnedPlayers.ContainsKey(player) || !spawningPlayers.Add(player))
            return;

        PlayerRef[] activePlayers = runner.ActivePlayers.OrderBy(item => item.PlayerId).ToArray();
        int slot = Array.IndexOf(activePlayers, player);
        if (slot < 0 || slot > 1)
        {
            spawningPlayers.Remove(player);
            return;
        }

        Transform spawn = slot == 0 ? playerOneSpawn : playerTwoSpawn;
        Vector3 position = spawn != null ? spawn.position : new Vector3(slot == 0 ? -2f : 2f, 0f, 0f);
        Quaternion rotation = spawn != null ? spawn.rotation : Quaternion.identity;

        NetworkRunner spawnRunner = runner;
        try
        {
            NetworkSpawnOp spawnOperation = spawnRunner.SpawnAsync(
                playerPrefab,
                position,
                rotation,
                player,
                (_, spawnedObject) => spawnedObject.GetComponent<OfficeFusionPlayer>().SetInitialSlot(slot));
            NetworkObject playerObject = await spawnOperation;
            spawningPlayers.Remove(player);

            if (playerObject == null)
                return;

            if (this == null || runner != spawnRunner || !spawnRunner.IsRunning ||
                !spawnRunner.ActivePlayers.Contains(player))
            {
                if (spawnRunner.IsRunning)
                    spawnRunner.Despawn(playerObject);
                return;
            }

            spawnedPlayers[player] = playerObject;
        }
        catch (Exception exception)
        {
            spawningPlayers.Remove(player);
            Debug.LogError($"No se pudo crear el jugador {player} en Sc_Game: {exception.Message}", this);
        }
    }

    private static NetworkRunner FindRunningRunner()
    {
        foreach (NetworkRunner candidate in NetworkRunner.Instances)
        {
            if (candidate != null && candidate.IsRunning)
                return candidate;
        }

        return null;
    }

    public void OnInput(NetworkRunner activeRunner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner activeRunner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner activeRunner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner activeRunner) { }
    public void OnDisconnectedFromServer(NetworkRunner activeRunner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner activeRunner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner activeRunner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnSessionListUpdated(NetworkRunner activeRunner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner activeRunner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner activeRunner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner activeRunner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner activeRunner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner activeRunner) { }
    public void OnSceneLoadStart(NetworkRunner activeRunner) { }
    public void OnObjectExitAOI(NetworkRunner activeRunner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner activeRunner, NetworkObject obj, PlayerRef player) { }
}
