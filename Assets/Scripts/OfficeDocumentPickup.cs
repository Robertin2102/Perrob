using UnityEngine;

/// <summary>Automatic pickup trigger attached at runtime to the Document prefab instance.</summary>
public sealed class OfficeDocumentPickup : MonoBehaviour
{
    private OfficeDocumentSpawner spawner;
    private bool collected;

    private void Awake()
    {
        Collider pickupCollider = GetComponent<Collider>();
        if (pickupCollider == null)
            pickupCollider = gameObject.AddComponent<SphereCollider>();
        pickupCollider.isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    public void Initialize(OfficeDocumentSpawner owner)
    {
        spawner = owner;
        collected = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        OfficePlayerMotor player = other.GetComponentInParent<OfficePlayerMotor>();
        if (player == null || !player.TryCollectDocument())
            return;

        collected = true;
        if (spawner != null)
            spawner.OnDocumentCollected(this);
        else
            Destroy(gameObject);
    }
}
