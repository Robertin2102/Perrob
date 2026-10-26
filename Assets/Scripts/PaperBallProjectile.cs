using Fusion;
using UnityEngine;

/// <summary>Host-simulated paper ball with replicated position and authoritative hit detection.</summary>
public sealed class PaperBallProjectile : NetworkBehaviour
{
    [SerializeField, Min(0.02f)] private float collisionRadius = 0.22f;
    [SerializeField] private LayerMask collisionMask = ~0;

    [Networked] private PlayerRef Owner { get; set; }
    [Networked] private Vector3 Velocity { get; set; }
    [Networked] private float Damage { get; set; }
    [Networked] private TickTimer Lifetime { get; set; }

    private Rigidbody body;
    private Collider projectileCollider;
    private Transform localOwner;
    private float localDamage;
    private float localExpiresAt;
    private bool localSimulation;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        projectileCollider = GetComponent<Collider>();
    }

    public override void Spawned()
    {
        body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        if (projectileCollider == null)
            projectileCollider = GetComponent<Collider>();
        if (projectileCollider != null)
            projectileCollider.enabled = false;
    }

    public void Launch(PlayerRef owner, Vector3 velocity, float lifetime, float damageAmount)
    {
        Owner = owner;
        Velocity = velocity;
        Damage = Mathf.Max(0f, damageAmount);
        Lifetime = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.1f, lifetime));
    }

    /// <summary>Offline fallback for direct Sc_Game iteration without a Photon session.</summary>
    public void Launch(Transform owner, Vector3 velocity, float lifetime, float damageAmount)
    {
        localSimulation = true;
        localOwner = owner;
        localDamage = Mathf.Max(0f, damageAmount);
        localExpiresAt = Time.time + Mathf.Max(0.1f, lifetime);
        if (body == null)
            body = GetComponent<Rigidbody>();
        if (projectileCollider == null)
            projectileCollider = GetComponent<Collider>();
        if (body != null)
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = velocity;
        }
        if (projectileCollider != null)
            projectileCollider.enabled = true;
    }

    private void Update()
    {
        if (localSimulation && Time.time >= localExpiresAt)
            Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!localSimulation || (localOwner != null &&
            (collision.transform == localOwner || collision.transform.IsChildOf(localOwner))))
            return;

        OfficePlayerMotor target = collision.collider.GetComponentInParent<OfficePlayerMotor>();
        if (target != null && target.transform != localOwner)
            target.TakeDamage(localDamage);
        Destroy(gameObject);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (Lifetime.Expired(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        float deltaTime = Runner.DeltaTime;
        Vector3 gravity = Physics.gravity;
        Vector3 start = transform.position;
        Vector3 displacement = Velocity * deltaTime + 0.5f * gravity * deltaTime * deltaTime;
        float distance = displacement.magnitude;

        if (distance > 0.0001f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(start, collisionRadius, displacement / distance,
                distance, collisionMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (RaycastHit hit in hits)
            {
                OfficeFusionPlayer target = hit.collider.GetComponentInParent<OfficeFusionPlayer>();
                if (target != null && target.Object.InputAuthority == Owner)
                    continue;

                if (target != null)
                {
                    OfficeFusionRoomController room = FindFirstObjectByType<OfficeFusionRoomController>();
                    OfficeFusionPlayer attacker = room != null ? room.FindPlayerByInputAuthority(Owner) : null;
                    if (target.ApplyDamageAuthority(Damage) && attacker != null)
                        attacker.RecordPaperHitAuthority(target.KnockedOut);
                }

                Runner.Despawn(Object);
                return;
            }
        }

        transform.position = start + displacement;
        Velocity += gravity * deltaTime;
    }
}
