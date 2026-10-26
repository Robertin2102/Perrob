using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Host-authoritative player state, movement, combat and document accounting.</summary>
public sealed class OfficeFusionPlayer : NetworkBehaviour
{
    [Header("Network prefabs")]
    [SerializeField] private NetworkObject paperBallPrefab;

    [Networked] public int PlayerSlot { get; set; }
    [Networked] public int CarriedDocuments { get; set; }
    [Networked] public int DeliveredDocuments { get; set; }
    [Networked] public float Energy { get; set; }
    [Networked] public NetworkBool KnockedOut { get; set; }
    [Networked] public TickTimer StunTimer { get; set; }
    [Networked] public TickTimer KnockoutTimer { get; set; }
    [Networked] public Vector3 RespawnPosition { get; set; }
    [Networked] public Quaternion RespawnRotation { get; set; }
    [Networked] public float DeliveryProgress { get; set; }
    [Networked] private NetworkBool IsMoving { get; set; }

    private OfficePlayerMotor motor;
    private OfficePaperThrower thrower;
    private OfficeDocumentStackVisuals stackVisuals;
    private float nextThrowAt;
    private bool hasInputAuthority;
    private bool hasStateAuthority;

    public OfficePlayerMotor Motor => motor;

    public void ConfigurePaperBallPrefab(NetworkObject prefab)
    {
        paperBallPrefab = prefab;
    }

    public override void Spawned()
    {
        motor = GetComponent<OfficePlayerMotor>();
        thrower = GetComponent<OfficePaperThrower>();
        stackVisuals = GetComponentInChildren<OfficeDocumentStackVisuals>(true);
        hasInputAuthority = Object != null && Object.HasInputAuthority;
        hasStateAuthority = Object != null && Object.HasStateAuthority;
        if (hasStateAuthority)
        {
            Energy = motor != null ? motor.MaximumEnergy : 100f;
            RespawnPosition = transform.position;
            RespawnRotation = transform.rotation;
        }

        if (motor != null)
        {
            motor.SetPlayerId(PlayerSlot);
            motor.SetNetworkSimulation(true);
        }

        if (thrower != null)
            thrower.SetNetworkInputMode(true);

        RegisterWithRoomController();

        if (hasInputAuthority)
        {
            BindLocalCamera();
            CreateTouchControlsIfAvailable();
        }
    }

    public override void Render()
    {
        if (motor == null)
            return;

        float stunSeconds = StunTimer.RemainingTime(Runner) ?? 0f;
        motor.ApplyNetworkState(CarriedDocuments, DeliveredDocuments, Energy,
            motor.MaximumEnergy, KnockedOut, stunSeconds);

        if (stackVisuals != null)
            stackVisuals.SetDocumentCount(CarriedDocuments);

        if (!hasStateAuthority)
            motor.SetNetworkRemoteLocomotion(IsMoving);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        OfficeFusionRoomController room = FindFirstObjectByType<OfficeFusionRoomController>();
        if (room != null)
            room.UnregisterSpawnedPlayer(this);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || motor == null)
            return;

        if (KnockedOut)
        {
            IsMoving = false;
            if (KnockoutTimer.ExpiredOrNotRunning(Runner))
                RespawnAuthority();
            return;
        }

        if (!GetInput(out OfficeFusionInput input))
        {
            IsMoving = false;
            return;
        }

        bool stunned = StunTimer.IsRunning && !StunTimer.Expired(Runner);
        Vector3 positionBeforeMove = transform.position;
        motor.SimulateNetworkInput(stunned ? Vector2.zero : input.Move,
            input.AimPoint, input.HasAimPoint, Runner.DeltaTime);
        Vector3 actualMovement = transform.position - positionBeforeMove;
        actualMovement.y = 0f;
        IsMoving = !stunned && actualMovement.sqrMagnitude > 0.000001f;

        if (input.Throw && !stunned)
        {
            Vector3 aimDirection = input.HasAimPoint
                ? input.AimPoint - transform.position
                : transform.forward;
            aimDirection.y = 0f;
            ThrowPaperAuthority(aimDirection);
        }
    }

    public OfficeFusionInput ReadLocalInput()
    {
        OfficeFusionInput input = default;
        if (hasInputAuthority && motor != null)
        {
            input.Move = motor.CaptureMoveInput();
            input.HasAimPoint = motor.CaptureAimPoint(out Vector3 point);
            input.AimPoint = point;
            input.Throw = thrower != null && thrower.ConsumeMouseThrowRequest();
        }

        return input;
    }

    public void RequestNetworkThrow(Vector3 direction)
    {
        if (!Object.HasInputAuthority || direction.sqrMagnitude < 0.001f)
            return;

        direction.y = 0f;
        direction.Normalize();
        if (Object.HasStateAuthority)
            ThrowPaperAuthority(direction);
        else
            RPC_RequestThrow(direction);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestThrow(Vector3 direction)
    {
        ThrowPaperAuthority(direction);
    }

    private void ThrowPaperAuthority(Vector3 direction)
    {
        if (!Object.HasStateAuthority || KnockedOut ||
            (StunTimer.IsRunning && !StunTimer.Expired(Runner)) ||
            Runner.SimulationTime < nextThrowAt || paperBallPrefab == null)
            return;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.y = 0f;
        direction.Normalize();
        nextThrowAt = Runner.SimulationTime + 0.8f;

        Vector3 origin = thrower != null && thrower.ThrowPoint != null
            ? thrower.ThrowPoint.position
            : transform.position + Vector3.up * 1.1f + direction * 0.55f;
        float speed = thrower != null ? thrower.ThrowSpeed : 15f;
        float arc = thrower != null ? thrower.UpwardArc : 3.5f;
        float damage = thrower != null ? thrower.DamagePerHit : 25f;
        float lifetime = thrower != null ? thrower.ProjectileLifetime : 3f;
        PlayerRef owner = Object.InputAuthority;

        SpawnPaperBallAsync(Runner, owner, direction, origin, speed, arc, lifetime, damage);
    }

    private async void SpawnPaperBallAsync(NetworkRunner activeRunner, PlayerRef owner,
        Vector3 direction, Vector3 origin, float speed, float arc, float lifetime, float damage)
    {
        try
        {
            NetworkSpawnOp spawnOperation = activeRunner.SpawnAsync(
                paperBallPrefab,
                origin,
                Quaternion.LookRotation(direction),
                owner,
                (_, spawned) =>
                {
                    PaperBallProjectile projectile = spawned.GetComponent<PaperBallProjectile>();
                    if (projectile != null)
                        projectile.Launch(owner, direction * speed + Vector3.up * arc, lifetime, damage);
                });
            await spawnOperation;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"No se pudo crear la bola de papel: {exception.Message}", this);
        }
    }

    public bool TryCollectDocumentAuthority()
    {
        if (!Object.HasStateAuthority || KnockedOut || motor == null ||
            CarriedDocuments >= motor.MaximumDocuments)
            return false;

        CarriedDocuments++;
        return true;
    }

    public int DropCarriedDocumentsAuthority()
    {
        if (!Object.HasStateAuthority)
            return 0;

        int dropped = CarriedDocuments;
        CarriedDocuments = 0;
        DeliveryProgress = 0f;
        return dropped;
    }

    public void DeliverDocumentsAuthority(int count)
    {
        if (!Object.HasStateAuthority || count <= 0)
            return;

        DeliveredDocuments += count;
        DeliveryProgress = 0f;
    }

    public void SetDeliveryProgressAuthority(float progress)
    {
        if (Object.HasStateAuthority)
            DeliveryProgress = Mathf.Clamp01(progress);
    }

    public void ApplyDamageAuthority(float amount)
    {
        if (!Object.HasStateAuthority || KnockedOut || amount <= 0f)
            return;

        Energy = Mathf.Max(0f, Energy - amount);
        if (Energy > 0f)
        {
            StunTimer = TickTimer.CreateFromSeconds(Runner, motor != null ? motor.HitStunSeconds : 0.65f);
            return;
        }

        KnockedOut = true;
        StunTimer = TickTimer.None;
        KnockoutTimer = TickTimer.CreateFromSeconds(Runner, motor != null ? motor.RespawnDelay : 3f);
        int dropped = DropCarriedDocumentsAuthority();
        if (dropped > 0)
        {
            OfficeDocumentSpawner spawner = FindFirstObjectByType<OfficeDocumentSpawner>();
            if (spawner != null)
                spawner.SpawnDroppedDocuments(dropped, transform.position);
        }
    }

    public void SetInitialSlot(int slot)
    {
        PlayerSlot = Mathf.Clamp(slot, 0, 1);
        if (motor != null)
            motor.SetPlayerId(PlayerSlot);
    }

    private void RespawnAuthority()
    {
        if (!Object.HasStateAuthority)
            return;

        OfficeFusionGameSceneController sceneController = FindFirstObjectByType<OfficeFusionGameSceneController>();
        if (sceneController != null && sceneController.TryGetSpawnPose(PlayerSlot, out Vector3 spawnPosition, out Quaternion spawnRotation))
        {
            RespawnPosition = spawnPosition;
            RespawnRotation = spawnRotation;
        }

        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        NetworkTransform networkTransform = GetComponent<NetworkTransform>();
        if (networkTransform != null)
            networkTransform.Teleport(RespawnPosition, RespawnRotation);
        else
            transform.SetPositionAndRotation(RespawnPosition, RespawnRotation);

        if (controller != null)
            controller.enabled = true;

        motor?.ResetNetworkMotionAfterRespawn();
        IsMoving = false;
        Energy = motor != null ? motor.MaximumEnergy : 100f;
        KnockedOut = false;
        KnockoutTimer = TickTimer.None;
        StunTimer = TickTimer.None;
    }

    private void RegisterWithRoomController()
    {
        OfficeFusionRoomController room = FindFirstObjectByType<OfficeFusionRoomController>();
        if (room != null)
            room.RegisterSpawnedPlayer(PlayerSlot, this);
    }

    private void BindLocalCamera()
    {
        OfficeCameraRig cameraRig = FindFirstObjectByType<OfficeCameraRig>();
        Camera localCamera = cameraRig != null ? cameraRig.SceneCamera : Camera.main;
        if (localCamera == null)
            return;

        if (cameraRig != null)
            cameraRig.SetFollowTarget(transform);
        if (motor != null)
            motor.SetAimCamera(localCamera);
        if (thrower != null)
            thrower.SetAimCamera(localCamera);
    }

    private void CreateTouchControlsIfAvailable()
    {
        if (!Application.isMobilePlatform && Touchscreen.current == null)
            return;

        OfficeTouchControls controls = GetComponent<OfficeTouchControls>();
        if (controls == null)
            controls = gameObject.AddComponent<OfficeTouchControls>();
        try
        {
            controls.Initialize(motor, thrower);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"No se pudieron crear los controles táctiles: {exception.Message}", this);
            Destroy(controls);
        }
    }

    public bool HasLocalInputAuthority => hasInputAuthority;
}

/// <summary>Owner input transmitted to the Host for one Fusion tick.</summary>
public struct OfficeFusionInput : INetworkInput
{
    public Vector2 Move;
    public Vector3 AimPoint;
    public NetworkBool HasAimPoint;
    public NetworkBool Throw;
}
