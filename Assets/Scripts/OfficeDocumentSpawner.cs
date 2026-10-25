using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Maintains a rotating set of pickups on configured office spawn points.</summary>
public sealed class OfficeDocumentSpawner : MonoBehaviour
{
    [Header("Prefab and points")]
    [SerializeField] private GameObject documentPrefab;
    [SerializeField] private Transform[] spawnPoints = new Transform[15];
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.08f, 0f);

    [Header("Rotation")]
    [SerializeField, Min(1)] private int activeDocuments = 8;
    [SerializeField, Min(0f)] private float respawnDelay = 0.8f;

    private readonly List<Transform> validSpawnPoints = new List<Transform>();
    private readonly HashSet<int> occupiedSpawnIndices = new HashSet<int>();
    private readonly Dictionary<OfficeDocumentPickup, int> pickupSpawnIndices =
        new Dictionary<OfficeDocumentPickup, int>();

    public int ActiveDocumentCount => pickupSpawnIndices.Count;

#if UNITY_EDITOR
    private void Reset()
    {
        documentPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Document.prefab");
    }
#endif

    private void Start()
    {
        CacheValidSpawnPoints();

        if (documentPrefab == null)
        {
            Debug.LogError("OfficeDocumentSpawner necesita asignar Assets/Prefabs/Document.prefab.", this);
            return;
        }

        if (validSpawnPoints.Count == 0)
        {
            Debug.LogError("OfficeDocumentSpawner no tiene puntos de aparición válidos asignados.", this);
            return;
        }

        int initialCount = Mathf.Min(activeDocuments, validSpawnPoints.Count);
        for (int i = 0; i < initialCount; i++)
            SpawnAtRandomFreePoint(-1);
    }

    public void OnDocumentCollected(OfficeDocumentPickup pickup)
    {
        if (pickup == null)
            return;

        if (!pickupSpawnIndices.TryGetValue(pickup, out int oldSpawnIndex))
        {
            // Dropped cargo is a loose pickup and does not occupy/replace a map spawn slot.
            Destroy(pickup.gameObject);
            return;
        }

        pickupSpawnIndices.Remove(pickup);
        occupiedSpawnIndices.Remove(oldSpawnIndex);
        Destroy(pickup.gameObject);
        StartCoroutine(RespawnAtDifferentPoint(oldSpawnIndex));
    }

    public void SpawnDroppedDocuments(int count, Vector3 aroundPosition)
    {
        if (documentPrefab == null || count <= 0)
            return;

        for (int i = 0; i < count; i++)
        {
            float angle = (Mathf.PI * 2f * i / count) + Random.Range(-0.2f, 0.2f);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.7f;
            GameObject instance = Instantiate(documentPrefab, aroundPosition + offset + spawnOffset, Quaternion.identity);
            OfficeDocumentPickup pickup = instance.GetComponent<OfficeDocumentPickup>();
            if (pickup == null)
                pickup = instance.AddComponent<OfficeDocumentPickup>();
            pickup.Initialize(this);
        }
    }

    private void CacheValidSpawnPoints()
    {
        validSpawnPoints.Clear();
        if (spawnPoints == null)
            return;

        HashSet<Transform> uniquePoints = new HashSet<Transform>();
        foreach (Transform point in spawnPoints)
        {
            if (point != null && uniquePoints.Add(point))
                validSpawnPoints.Add(point);
        }
    }

    private IEnumerator RespawnAtDifferentPoint(int oldSpawnIndex)
    {
        if (respawnDelay > 0f)
            yield return new WaitForSeconds(respawnDelay);

        if (pickupSpawnIndices.Count < activeDocuments)
            SpawnAtRandomFreePoint(oldSpawnIndex);
    }

    private void SpawnAtRandomFreePoint(int avoidIndex)
    {
        List<int> candidates = new List<int>();
        for (int i = 0; i < validSpawnPoints.Count; i++)
        {
            if (i != avoidIndex && !occupiedSpawnIndices.Contains(i))
                candidates.Add(i);
        }

        // Only possible when every spawn is occupied (for example, activeDocuments == 15).
        // In that case reuse the just-vacated point to keep the configured document count.
        if (candidates.Count == 0 && avoidIndex >= 0 && !occupiedSpawnIndices.Contains(avoidIndex))
            candidates.Add(avoidIndex);

        if (candidates.Count == 0)
            return;

        int spawnIndex = candidates[Random.Range(0, candidates.Count)];
        Transform point = validSpawnPoints[spawnIndex];
        GameObject instance = Instantiate(documentPrefab, point.position + spawnOffset, point.rotation);

        OfficeDocumentPickup pickup = instance.GetComponent<OfficeDocumentPickup>();
        if (pickup == null)
            pickup = instance.AddComponent<OfficeDocumentPickup>();
        pickup.Initialize(this);

        occupiedSpawnIndices.Add(spawnIndex);
        pickupSpawnIndices.Add(pickup, spawnIndex);
    }
}
