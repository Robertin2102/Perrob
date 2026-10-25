using Fusion;
using UnityEngine;

/// <summary>Feeds owner input into Fusion and simulates the player on the host.</summary>
public sealed class OfficeFusionPlayer : NetworkBehaviour
{
    [Networked] public int PlayerSlot { get; set; }

    private OfficePlayerMotor motor;
    private OfficePaperThrower thrower;

    public OfficePlayerMotor Motor => motor;

    public override void Spawned()
    {
        motor = GetComponent<OfficePlayerMotor>();
        thrower = GetComponent<OfficePaperThrower>();

        if (motor != null)
        {
            motor.SetPlayerId(PlayerSlot);
            motor.SetNetworkSimulation(true);
        }

        // Paper attacks will be routed through host authority in the next networking pass.
        if (thrower != null)
            thrower.SetNetworkInputMode(true);

        OfficeFusionRoomController room = FindFirstObjectByType<OfficeFusionRoomController>();
        if (room != null && motor != null)
            room.RegisterSpawnedPlayer(PlayerSlot, this);
    }

    public override void FixedUpdateNetwork()
    {
        if (motor == null || !GetInput(out OfficeFusionInput input))
            return;

        motor.SimulateNetworkInput(input.Move, input.AimPoint, input.HasAimPoint, Runner.DeltaTime);
    }

    public OfficeFusionInput ReadLocalInput()
    {
        OfficeFusionInput input = default;
        if (Object.HasInputAuthority && motor != null)
        {
            input.Move = motor.CaptureMoveInput();
            input.HasAimPoint = motor.CaptureAimPoint(out Vector3 point);
            input.AimPoint = point;
        }

        return input;
    }

    public void SetInitialSlot(int slot)
    {
        PlayerSlot = Mathf.Clamp(slot, 0, 1);
    }
}

/// <summary>Owner input transmitted to the Host for one Fusion tick.</summary>
public struct OfficeFusionInput : INetworkInput
{
    public Vector2 Move;
    public Vector3 AimPoint;
    public NetworkBool HasAimPoint;
}
