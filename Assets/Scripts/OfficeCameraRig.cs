using UnityEngine;

/// <summary>
/// Fixed elevated camera for the office arena, with optional smooth target following.
/// </summary>
[RequireComponent(typeof(Camera))]
public sealed class OfficeCameraRig : MonoBehaviour
{
    [Header("Office framing")]
    [SerializeField] private Vector3 officeCenter = Vector3.zero;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 20f, -14f);
    [SerializeField, Min(0.1f)] private float orthographicSize = 14f;
    [SerializeField] private bool useOrthographicProjection = true;

    [Header("Optional follow")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private bool followTargetPosition;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.8f, 0f);
    [SerializeField, Min(0f)] private float followSharpness = 8f;

    private Camera sceneCamera;

    public Camera SceneCamera => sceneCamera != null ? sceneCamera : GetComponent<Camera>();

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
        followTargetPosition = target != null;
        if (followTargetPosition)
            SnapToTarget();
    }

    private void Awake()
    {
        sceneCamera = GetComponent<Camera>();
        ApplyProjection();
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (sceneCamera == null)
            return;

        ApplyProjection();

        Vector3 targetPosition = GetTargetPosition();
        Vector3 desiredPosition = targetPosition + cameraOffset;
        float interpolation = followSharpness <= 0f
            ? 1f
            : 1f - Mathf.Exp(-followSharpness * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, desiredPosition, interpolation);
        Vector3 lookDirection = targetPosition - transform.position;
        if (lookDirection.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
    }

    private Vector3 GetTargetPosition()
    {
        if (followTargetPosition && followTarget != null)
            return followTarget.position + targetOffset;

        return officeCenter;
    }

    private void SnapToTarget()
    {
        Vector3 targetPosition = GetTargetPosition();
        transform.position = targetPosition + cameraOffset;
        Vector3 lookDirection = targetPosition - transform.position;
        if (lookDirection.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
    }

    private void ApplyProjection()
    {
        sceneCamera.orthographic = useOrthographicProjection;
        if (useOrthographicProjection)
            sceneCamera.orthographicSize = orthographicSize;
    }

    private void OnValidate()
    {
        if (sceneCamera == null)
            sceneCamera = GetComponent<Camera>();

        if (sceneCamera != null)
            ApplyProjection();
    }
}
