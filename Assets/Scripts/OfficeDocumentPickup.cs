using Fusion;
using UnityEngine;

/// <summary>Networked document pickup; only the host can award and despawn it.</summary>
public sealed class OfficeDocumentPickup : NetworkBehaviour
{
    private OfficeDocumentSpawner spawner;
    private int spawnIndex = -1;

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

    public void Initialize(OfficeDocumentSpawner owner, int sourceSpawnIndex)
    {
        spawner = owner;
        spawnIndex = sourceSpawnIndex;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Object.HasStateAuthority)
            return;

        OfficeFusionPlayer player = other.GetComponentInParent<OfficeFusionPlayer>();
        if (player == null || !player.TryCollectDocumentAuthority())
            return;

        if (spawner != null)
            spawner.OnDocumentCollected(this);
        else
            Runner.Despawn(Object);
    }

    public int SpawnIndex => spawnIndex;
}
