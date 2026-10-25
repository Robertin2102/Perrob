using System.Linq;
using Fusion;
using UnityEngine;

/// <summary>Host-authoritative ready flags for the two players in a Photon session.</summary>
public sealed class OfficeFusionLobbyState : NetworkBehaviour
{
    [Networked] public NetworkBool PlayerOneReady { get; set; }
    [Networked] public NetworkBool PlayerTwoReady { get; set; }

    private bool hasQueuedHostReadyChange;
    private PlayerRef queuedHostPlayer;
    private bool queuedHostReady;

    public bool BothPlayersReady => Runner != null && Runner.ActivePlayers.Count() == 2 &&
                                    PlayerOneReady && PlayerTwoReady;

    public override void Spawned()
    {
        OfficeFusionRoomController room = FindFirstObjectByType<OfficeFusionRoomController>();
        if (room != null)
            room.RegisterLobbyState(this);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || !hasQueuedHostReadyChange)
            return;

        SetPlayerReadyAuthority(queuedHostPlayer, queuedHostReady);
        hasQueuedHostReadyChange = false;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetReady(bool isReady, RpcInfo info = default)
    {
        SetPlayerReadyAuthority(info.Source, isReady);
    }

    /// <summary>Updates a ready flag on the Host, including its local player without an RPC.</summary>
    public void SetPlayerReadyAuthority(PlayerRef player, bool isReady)
    {
        if (!Object.HasStateAuthority || Runner == null)
            return;

        PlayerRef[] activePlayers = Runner.ActivePlayers.OrderBy(player => player.PlayerId).ToArray();
        int slot = System.Array.IndexOf(activePlayers, player);
        if (slot == 0)
            PlayerOneReady = isReady;
        else if (slot == 1)
            PlayerTwoReady = isReady;
    }

    public void QueueHostReadyChange(PlayerRef player, bool isReady)
    {
        if (!Object.HasStateAuthority)
            return;

        queuedHostPlayer = player;
        queuedHostReady = isReady;
        hasQueuedHostReadyChange = true;
    }

    public bool IsReady(PlayerRef player)
    {
        if (Runner == null)
            return false;

        PlayerRef[] activePlayers = Runner.ActivePlayers.OrderBy(item => item.PlayerId).ToArray();
        int slot = System.Array.IndexOf(activePlayers, player);
        return slot == 0 ? PlayerOneReady : slot == 1 && PlayerTwoReady;
    }

    public string GetReadySummary()
    {
        string first = PlayerOneReady ? "LISTO" : "ESPERANDO";
        string second = PlayerTwoReady ? "LISTO" : "ESPERANDO";
        return $"EMPLEADO 01  {first}     ·     EMPLEADO 02  {second}";
    }

    public void ClearReadyState()
    {
        if (!Object.HasStateAuthority)
            return;

        PlayerOneReady = false;
        PlayerTwoReady = false;
    }
}
