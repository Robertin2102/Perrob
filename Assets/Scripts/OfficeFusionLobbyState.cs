using System.Linq;
using Fusion;
using UnityEngine;

/// <summary>Host-authoritative ready flags for the two players in a Photon session.</summary>
public sealed class OfficeFusionLobbyState : NetworkBehaviour
{
    [Networked] public NetworkBool PlayerOneReady { get; set; }
    [Networked] public NetworkBool PlayerTwoReady { get; set; }

    public bool BothPlayersReady => Runner != null && Runner.ActivePlayers.Count() == 2 &&
                                    PlayerOneReady && PlayerTwoReady;

    public override void Spawned()
    {
        OfficeFusionRoomController room = FindFirstObjectByType<OfficeFusionRoomController>();
        if (room != null)
            room.RegisterLobbyState(this);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetReady(bool isReady, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || Runner == null)
            return;

        PlayerRef[] activePlayers = Runner.ActivePlayers.OrderBy(player => player.PlayerId).ToArray();
        int slot = System.Array.IndexOf(activePlayers, info.Source);
        if (slot == 0)
            PlayerOneReady = isReady;
        else if (slot == 1)
            PlayerTwoReady = isReady;
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
