using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Shows lightweight, effect-free document meshes on the character's Stack anchor.</summary>
public sealed class OfficeDocumentStackVisuals : MonoBehaviour
{
    [Header("Source visual")]
    [SerializeField] private Renderer documentVisualSource;
    [SerializeField] private Transform stackRoot;
    [SerializeField, Min(0.01f)] private float longestSide = 0.32f;
    [SerializeField] private Vector3 visualLocalRotation;

    [Header("Stack layout")]
    [SerializeField] private Vector3 firstDocumentOffset;
    [SerializeField] private Vector3 spacing = new Vector3(0f, 0.025f, -0.035f);

    private readonly List<GameObject> documentVisuals = new List<GameObject>();
    private OfficePlayerMotor playerMotor;
    private Mesh sourceMesh;
    private Material[] sourceMaterials;

#if UNITY_EDITOR
    private void Reset()
    {
        stackRoot = transform;
        GameObject documentPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Document.prefab");
        if (documentPrefab != null)
            documentVisualSource = documentPrefab.GetComponent<Renderer>();
    }
#endif

    private void Awake()
    {
        if (stackRoot == null)
            stackRoot = transform;

        CacheVisualSource();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        playerMotor = GetComponentInParent<OfficePlayerMotor>();
        if (playerMotor == null)
        {
            Debug.LogWarning("OfficeDocumentStackVisuals debe estar en Stack, dentro del personaje.", this);
            return;
        }

        playerMotor.CarriedDocumentsChanged += Refresh;
        Refresh(playerMotor.CarriedDocuments);
    }

    private void OnDisable()
    {
        if (playerMotor != null)
            playerMotor.CarriedDocumentsChanged -= Refresh;
    }

    private void CacheVisualSource()
    {
        if (documentVisualSource == null)
            return;

        MeshFilter meshFilter = documentVisualSource.GetComponent<MeshFilter>();
        if (meshFilter != null)
            sourceMesh = meshFilter.sharedMesh;
        else if (documentVisualSource is SkinnedMeshRenderer skinnedRenderer)
            sourceMesh = skinnedRenderer.sharedMesh;
        else if (documentVisualSource is ParticleSystemRenderer particleRenderer)
            sourceMesh = particleRenderer.mesh;

        sourceMaterials = documentVisualSource.sharedMaterials;
    }

    private void Refresh(int count)
    {
        if (stackRoot == null)
            stackRoot = transform;

        count = Mathf.Max(0, count);
        while (documentVisuals.Count < count)
            documentVisuals.Add(CreateDocumentVisual(documentVisuals.Count));

        while (documentVisuals.Count > count)
        {
            int lastIndex = documentVisuals.Count - 1;
            Destroy(documentVisuals[lastIndex]);
            documentVisuals.RemoveAt(lastIndex);
        }

        for (int i = 0; i < documentVisuals.Count; i++)
        {
            Transform document = documentVisuals[i].transform;
            document.localPosition = firstDocumentOffset + spacing * i;
            document.localRotation = Quaternion.Euler(visualLocalRotation);
        }
    }

    public void SetDocumentCount(int count)
    {
        count = Mathf.Max(0, count);
        if (documentVisuals.Count != count)
            Refresh(count);
    }

    private GameObject CreateDocumentVisual(int index)
    {
        GameObject visual;
        Mesh mesh = sourceMesh;
        if (mesh == null)
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider fallbackCollider = visual.GetComponent<Collider>();
            if (fallbackCollider != null)
                Destroy(fallbackCollider);
            visual.transform.localScale = new Vector3(longestSide, 0.025f, longestSide * 0.72f);
        }
        else
        {
            visual = new GameObject("CarriedDocument_" + (index + 1), typeof(MeshFilter), typeof(MeshRenderer));
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer meshRenderer = visual.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = sourceMaterials;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            float largestDimension = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z);
            float scale = largestDimension > 0.0001f ? longestSide / largestDimension : longestSide;
            visual.transform.localScale = Vector3.one * scale;
        }

        visual.name = "CarriedDocument_" + (index + 1);
        visual.transform.SetParent(stackRoot, false);
        visual.transform.localPosition = firstDocumentOffset + spacing * index;
        visual.transform.localRotation = Quaternion.Euler(visualLocalRotation);
        return visual;
    }
}
