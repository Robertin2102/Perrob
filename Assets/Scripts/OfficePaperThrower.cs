using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Local prototype input for throwing the office paper-ball prefab.</summary>
public sealed class OfficePaperThrower : MonoBehaviour
{
    [Header("Prefab and aim")]
    [SerializeField] private GameObject paperBallPrefab;
    [SerializeField] private Transform throwPoint;
    [SerializeField] private Camera aimCamera;
    [SerializeField, Min(0.1f)] private float throwSpeed = 15f;
    [SerializeField, Min(0f)] private float upwardArc = 3.5f;

    [Header("Combat")]
    [SerializeField, Min(0f)] private float cooldownSeconds = 0.8f;
    [SerializeField, Min(0.1f)] private float projectileLifetime = 3f;
    [SerializeField, Min(0f)] private float damagePerHit = 25f;
    [SerializeField] private bool readMouseInput = true;

    private OfficePlayerMotor playerMotor;
    private float nextThrowTime;
    private bool networkInputMode;
    private bool mouseThrowPending;

    public Transform ThrowPoint => throwPoint;
    public float ThrowSpeed => throwSpeed;
    public float UpwardArc => upwardArc;
    public float ProjectileLifetime => projectileLifetime;
    public float DamagePerHit => damagePerHit;

#if UNITY_EDITOR
    private void Reset()
    {
        paperBallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PaperBall.prefab");
        aimCamera = Camera.main;
    }
#endif

    private void Awake()
    {
        playerMotor = GetComponent<OfficePlayerMotor>();
        if (aimCamera == null)
            aimCamera = Camera.main;
    }

    private void Update()
    {
        if (!readMouseInput || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (!networkInputMode)
        {
            RequestThrow();
            return;
        }

        OfficeFusionPlayer networkPlayer = GetComponent<OfficeFusionPlayer>();
        if (networkPlayer != null && networkPlayer.HasLocalInputAuthority)
            mouseThrowPending = true;
    }

    public void SetNetworkInputMode(bool enabled)
    {
        networkInputMode = enabled;
    }

    public void SetTouchInputMode(bool enabled)
    {
        if (enabled)
            readMouseInput = false;
    }

    public bool ConsumeMouseThrowRequest()
    {
        bool requested = mouseThrowPending;
        mouseThrowPending = false;
        return requested;
    }

    public void SetAimCamera(Camera camera)
    {
        aimCamera = camera;
    }

    /// <summary>Can be called by a mobile button or a future Photon input adapter.</summary>
    public void RequestThrow()
    {
        if (networkInputMode)
        {
            OfficeFusionPlayer networkPlayer = GetComponent<OfficeFusionPlayer>();
            if (networkPlayer != null && playerMotor != null && !playerMotor.IsStunned)
                networkPlayer.RequestNetworkThrow(GetAimDirection());
            return;
        }

        if (paperBallPrefab == null || Time.time < nextThrowTime ||
            (playerMotor != null && playerMotor.IsStunned))
            return;

        Vector3 direction = GetAimDirection();
        Vector3 origin = throwPoint != null
            ? throwPoint.position
            : transform.position + Vector3.up * 1.1f + direction * 0.55f;

        GameObject ball = Instantiate(paperBallPrefab, origin, Quaternion.LookRotation(direction));
        PaperBallProjectile projectile = ball.GetComponent<PaperBallProjectile>();
        if (projectile == null)
            projectile = ball.AddComponent<PaperBallProjectile>();

        Vector3 launchVelocity = direction * throwSpeed + Vector3.up * upwardArc;
        projectile.Launch(transform, launchVelocity, projectileLifetime, damagePerHit);
        nextThrowTime = Time.time + cooldownSeconds;
    }

    private Vector3 GetAimDirection()
    {
        if (playerMotor != null && playerMotor.TryGetAimDirection(out Vector3 aimDirection))
            return aimDirection;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
    }
}
