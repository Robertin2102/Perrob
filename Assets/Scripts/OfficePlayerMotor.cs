using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Planar character movement for Pelea por el Salario Mínimo.
/// Local input can later be replaced by validated input from the Photon server.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class OfficePlayerMotor : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float baseMoveSpeed = 5f;
    [SerializeField, Min(0f)] private float gravity = 20f;
    [SerializeField, Range(0f, 0.5f)] private float penaltyPerDocument = 0.08f;
    [SerializeField, Range(0f, 0.8f)] private float maximumLoadPenalty = 0.4f;
    [SerializeField, Min(0)] private int maximumDocuments = 5;
    [SerializeField] private bool readLocalKeyboard = true;

    [Header("Aim")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private bool faceMousePointer = true;
    [SerializeField, Min(0f)] private float turnSpeed = 720f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string idleState = "Base Layer.Anim_Idle";
    [SerializeField] private string runState = "Base Layer.Anim_Running";
    [SerializeField] private string hitState = "Base Layer.Anim_Dancing";
    [SerializeField, Min(0f)] private float hitStunSeconds = 0.65f;

    [Header("Energy")]
    [SerializeField, Min(1f)] private float maximumEnergy = 100f;
    [SerializeField, Min(0.1f)] private float respawnDelay = 3f;

    [Header("Player identity")]
    [SerializeField, Min(0)] private int playerId;

    private CharacterController characterController;
    private Vector2 externalMoveInput;
    private Vector3 externalAimPoint;
    private bool hasExternalAimPoint;
    private int carriedDocuments;
    private float verticalVelocity;
    private float stunTimer;
    private int activeAnimationHash;
    private int deliveredDocuments;
    private float currentEnergy;
    private float knockoutTimer;
    private Vector3 respawnPosition;
    private Quaternion respawnRotation;
    private bool isKnockedOut;
    private bool networkSimulation;

    public event Action<int> CarriedDocumentsChanged;
    public event Action<int> DeliveredDocumentsChanged;
    public event Action<float, float> EnergyChanged;

    public int PlayerId => playerId;
    public int CarriedDocuments => carriedDocuments;
    public int DeliveredDocuments => deliveredDocuments;
    public int MaximumDocuments => maximumDocuments;
    public float Energy => currentEnergy;
    public float MaximumEnergy => maximumEnergy;
    public float HitStunSeconds => hitStunSeconds;
    public float RespawnDelay => respawnDelay;
    public float CurrentMoveSpeed => baseMoveSpeed * (1f - CurrentLoadPenalty);
    public float CurrentLoadPenalty => Mathf.Min(carriedDocuments * penaltyPerDocument, maximumLoadPenalty);
    public bool IsStunned => isKnockedOut || stunTimer > 0f;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        respawnPosition = transform.position;
        respawnRotation = transform.rotation;
        currentEnergy = maximumEnergy;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
            animator.applyRootMotion = false;

        if (aimCamera == null)
            aimCamera = Camera.main;

        PlayAnimation(idleState);
    }

    private void Update()
    {
        if (networkSimulation)
            return;

        float deltaTime = Time.deltaTime;
        if (isKnockedOut)
        {
            knockoutTimer -= deltaTime;
            if (knockoutTimer <= 0f)
                Respawn();
            return;
        }

        if (stunTimer > 0f)
            stunTimer = Mathf.Max(0f, stunTimer - deltaTime);

        Vector2 input = IsStunned ? Vector2.zero : GetMoveInput();
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 planarDirection = GetCameraRelativeDirection(input);
        verticalVelocity = characterController.isGrounded
            ? -2f
            : verticalVelocity - gravity * deltaTime;
        characterController.Move(planarDirection * (CurrentMoveSpeed * deltaTime) +
                                 Vector3.up * (verticalVelocity * deltaTime));

        UpdateFacing(planarDirection);
        UpdateLocomotionAnimation(planarDirection.sqrMagnitude > 0.001f && !IsStunned);
    }

    /// <summary>Set input from a mobile joystick or a future network input adapter.</summary>
    public void SetMoveInput(Vector2 input)
    {
        externalMoveInput = Vector2.ClampMagnitude(input, 1f);
    }

    /// <summary>Disable local keyboard polling when another input source owns this player.</summary>
    public void SetLocalInputEnabled(bool enabled)
    {
        readLocalKeyboard = enabled;
    }

    public void SetNetworkSimulation(bool enabled)
    {
        networkSimulation = enabled;
    }

    public void SetAimCamera(Camera camera)
    {
        aimCamera = camera;
    }

    /// <summary>Applies the host-owned state received by the network player to this visual motor.</summary>
    public void ApplyNetworkState(int carried, int delivered, float energy, float maxEnergy,
        bool knockedOut, float stunSeconds)
    {
        bool wasStunned = IsStunned;
        bool carryChanged = carriedDocuments != carried;
        bool scoreChanged = deliveredDocuments != delivered;
        bool energyChanged = !Mathf.Approximately(currentEnergy, energy) ||
                             !Mathf.Approximately(maximumEnergy, maxEnergy);

        carriedDocuments = Mathf.Clamp(carried, 0, maximumDocuments);
        deliveredDocuments = Mathf.Max(0, delivered);
        currentEnergy = Mathf.Clamp(energy, 0f, maxEnergy);
        maximumEnergy = Mathf.Max(1f, maxEnergy);
        isKnockedOut = knockedOut;
        stunTimer = Mathf.Max(0f, stunSeconds);

        if (carryChanged) CarriedDocumentsChanged?.Invoke(carriedDocuments);
        if (scoreChanged) DeliveredDocumentsChanged?.Invoke(deliveredDocuments);
        if (energyChanged) EnergyChanged?.Invoke(currentEnergy, maximumEnergy);

        if (!wasStunned && IsStunned)
            PlayAnimation(hitState);
    }

    public void SetNetworkRemoteLocomotion(bool moving)
    {
        if (!IsStunned)
            UpdateLocomotionAnimation(moving);
    }

    /// <summary>Clear movement state after the network authority teleports this player back into the map.</summary>
    public void ResetNetworkMotionAfterRespawn()
    {
        verticalVelocity = 0f;
        externalAimPoint = default;
        hasExternalAimPoint = false;
        stunTimer = 0f;
        UpdateLocomotionAnimation(false);
    }

    public void SetPlayerId(int value)
    {
        playerId = Mathf.Max(0, value);
    }

    public Vector2 CaptureMoveInput()
    {
        return GetMoveInput();
    }

    public bool CaptureAimPoint(out Vector3 worldPoint)
    {
        worldPoint = default;
        if (aimCamera == null || Mouse.current == null)
            return false;

        Ray ray = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane floor = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (!floor.Raycast(ray, out float distance))
            return false;

        worldPoint = ray.GetPoint(distance);
        return true;
    }

    /// <summary>Moves the character from Fusion's fixed simulation tick.</summary>
    public void SimulateNetworkInput(Vector2 input, Vector3 aimPoint, bool hasAimPoint, float deltaTime)
    {
        if (!networkSimulation || characterController == null || !characterController.enabled)
            return;

        if (isKnockedOut)
        {
            knockoutTimer -= deltaTime;
            if (knockoutTimer <= 0f)
                Respawn();
            return;
        }

        if (hasAimPoint)
        {
            externalAimPoint = aimPoint;
            hasExternalAimPoint = true;
        }
        else
        {
            hasExternalAimPoint = false;
        }

        input = IsStunned ? Vector2.zero : Vector2.ClampMagnitude(input, 1f);
        Vector3 planarDirection = GetCameraRelativeDirection(input);
        verticalVelocity = characterController.isGrounded
            ? -2f
            : verticalVelocity - gravity * deltaTime;
        characterController.Move(planarDirection * (CurrentMoveSpeed * deltaTime) +
                                 Vector3.up * (verticalVelocity * deltaTime));
        UpdateFacing(planarDirection);
        UpdateLocomotionAnimation(planarDirection.sqrMagnitude > 0.001f && !IsStunned);
    }

    /// <summary>Supply a world-space point for touch aiming or an authoritative network adapter.</summary>
    public void SetAimPoint(Vector3 worldPoint)
    {
        externalAimPoint = worldPoint;
        hasExternalAimPoint = true;
    }

    public void ClearExternalAimPoint()
    {
        hasExternalAimPoint = false;
    }

    public void SetCarriedDocuments(int count)
    {
        int newCount = Mathf.Clamp(count, 0, maximumDocuments);
        if (carriedDocuments == newCount)
            return;

        carriedDocuments = newCount;
        CarriedDocumentsChanged?.Invoke(carriedDocuments);
    }

    public bool TryCollectDocument()
    {
        if (isKnockedOut || carriedDocuments >= maximumDocuments)
            return false;

        carriedDocuments++;
        CarriedDocumentsChanged?.Invoke(carriedDocuments);
        return true;
    }

    public int DropCarriedDocuments()
    {
        int droppedCount = carriedDocuments;
        carriedDocuments = 0;
        if (droppedCount > 0)
            CarriedDocumentsChanged?.Invoke(carriedDocuments);
        return droppedCount;
    }

    public void RecordDelivery(int documentCount)
    {
        if (documentCount <= 0)
            return;

        deliveredDocuments += documentCount;
        DeliveredDocumentsChanged?.Invoke(deliveredDocuments);
    }

    /// <summary>Play the Dancing hit reaction and briefly prevent movement.</summary>
    public void Stun(float duration = -1f)
    {
        if (isKnockedOut)
            return;

        stunTimer = duration < 0f ? hitStunSeconds : Mathf.Max(0f, duration);
        if (stunTimer > 0f)
            PlayAnimation(hitState);
    }

    /// <summary>Reduces energy after a paper hit and knocks the player out at zero.</summary>
    public void TakeDamage(float amount)
    {
        if (isKnockedOut || amount <= 0f)
            return;

        currentEnergy = Mathf.Max(0f, currentEnergy - amount);
        EnergyChanged?.Invoke(currentEnergy, maximumEnergy);

        if (currentEnergy <= 0f)
            KnockOut();
        else
            Stun(hitStunSeconds);
    }

    private void KnockOut()
    {
        isKnockedOut = true;
        stunTimer = 0f;
        knockoutTimer = Mathf.Max(0.1f, respawnDelay);
        PlayAnimation(hitState);

        int droppedCount = DropCarriedDocuments();
        if (droppedCount > 0)
        {
            OfficeDocumentSpawner spawner = FindFirstObjectByType<OfficeDocumentSpawner>();
            if (spawner != null)
                spawner.SpawnDroppedDocuments(droppedCount, transform.position);
        }
    }

    private void Respawn()
    {
        characterController.enabled = false;
        transform.SetPositionAndRotation(respawnPosition, respawnRotation);
        characterController.enabled = true;

        verticalVelocity = 0f;
        currentEnergy = maximumEnergy;
        stunTimer = 0f;
        isKnockedOut = false;
        EnergyChanged?.Invoke(currentEnergy, maximumEnergy);
        PlayAnimation(idleState);
    }

    private Vector2 GetMoveInput()
    {
        Vector2 input = externalMoveInput;

        if (readLocalKeyboard && Keyboard.current != null)
        {
            Vector2 keyboardInput = Vector2.zero;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) keyboardInput.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) keyboardInput.y -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) keyboardInput.x += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) keyboardInput.x -= 1f;
            if (keyboardInput.sqrMagnitude > 0f)
                input = keyboardInput;
        }

        return input;
    }

    private void UpdateFacing(Vector3 movementDirection)
    {
        if (hasExternalAimPoint)
        {
            FaceDirection(externalAimPoint - transform.position);
            return;
        }

        bool shouldAimAtMouse = faceMousePointer && aimCamera != null && Mouse.current != null &&
                                (Mouse.current.leftButton.isPressed || movementDirection.sqrMagnitude < 0.001f);
        if (!shouldAimAtMouse)
        {
            FaceDirection(movementDirection);
            return;
        }

        Ray ray = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane floor = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (!floor.Raycast(ray, out float distance))
        {
            FaceDirection(movementDirection);
            return;
        }

        FaceDirection(ray.GetPoint(distance) - transform.position);
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        Vector3 forward = aimCamera != null ? aimCamera.transform.forward : Vector3.forward;
        forward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        return Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private void UpdateLocomotionAnimation(bool isMoving)
    {
        if (IsStunned)
            return;

        PlayAnimation(isMoving ? runState : idleState);
    }

    private void PlayAnimation(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        int stateHash = Animator.StringToHash(stateName);
        if (activeAnimationHash == stateHash)
            return;

        if (!animator.HasState(0, stateHash))
            return;

        animator.CrossFadeInFixedTime(stateHash, 0.12f);
        activeAnimationHash = stateHash;
    }
}
