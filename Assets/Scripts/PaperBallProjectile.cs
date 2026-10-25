using UnityEngine;

/// <summary>Runtime physics and hit response for the paper-ball prefab.</summary>
public sealed class PaperBallProjectile : MonoBehaviour
{
    private Rigidbody body;
    private Collider ballCollider;
    private Transform owner;
    private float damage = 25f;
    private float expiresAt;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();

        ballCollider = GetComponent<Collider>();
        if (ballCollider == null)
            ballCollider = gameObject.AddComponent<SphereCollider>();

        body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Update()
    {
        if (Time.time >= expiresAt)
            Destroy(gameObject);
    }

    public void Launch(Transform projectileOwner, Vector3 velocity, float lifetime, float damageAmount)
    {
        owner = projectileOwner;
        damage = Mathf.Max(0f, damageAmount);
        expiresAt = Time.time + Mathf.Max(0.1f, lifetime);

        if (owner != null && ballCollider != null)
        {
            Collider[] ownerColliders = owner.GetComponentsInChildren<Collider>();
            foreach (Collider ownerCollider in ownerColliders)
            {
                if (ownerCollider != null)
                    Physics.IgnoreCollision(ballCollider, ownerCollider, true);
            }
        }

        body.linearVelocity = velocity;
        body.AddTorque(Random.insideUnitSphere * 4f, ForceMode.Impulse);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (owner != null && (collision.transform == owner || collision.transform.IsChildOf(owner)))
            return;

        OfficePlayerMotor target = collision.collider.GetComponentInParent<OfficePlayerMotor>();
        if (target != null && target.transform != owner)
            target.TakeDamage(damage);

        Destroy(gameObject);
    }
}
