using UnityEngine;

#if !UNITY_SERVER
using Unity.Cinemachine;
using UnityEngine.InputSystem;
#endif

public class LocalPlayerCamera : MonoBehaviour
{
    public static LocalPlayerCamera Instance { get; private set; }

#if !UNITY_SERVER
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private float minimumOrthographicSize = 8f;
    [SerializeField] private float maximumOrthographicSize = 22f;
    [SerializeField] private float zoomStep = 1.5f;
    [SerializeField] private float zoomSmoothTime = 0.12f;

    private Transform currentTarget;
    private float desiredOrthographicSize;
    private float zoomVelocity;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (!cinemachineCamera)
            cinemachineCamera = GetComponent<CinemachineCamera>();

        if (!cinemachineCamera)
        {
            Debug.LogError("LocalPlayerCamera requires a CinemachineCamera", this);
            enabled = false;
            return;
        }

        desiredOrthographicSize = Mathf.Clamp(
            cinemachineCamera.Lens.OrthographicSize,
            minimumOrthographicSize,
            maximumOrthographicSize);
    }

    private void Update()
    {
        if (Mouse.current is null)
            return;

        var scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        desiredOrthographicSize -= Mathf.Sign(scroll) * zoomStep;

        desiredOrthographicSize = Mathf.Clamp(
            desiredOrthographicSize,
            minimumOrthographicSize,
            maximumOrthographicSize);
    }

    private void LateUpdate()
    {
        if (!cinemachineCamera)
            return;

        var lens = cinemachineCamera.Lens;

        lens.OrthographicSize = Mathf.SmoothDamp(
            lens.OrthographicSize,
            desiredOrthographicSize,
            ref zoomVelocity,
            zoomSmoothTime);

        cinemachineCamera.Lens = lens;
    }

    public void SetTarget(Transform target)
    {
        if (!target || !cinemachineCamera)
            return;

        currentTarget = target;
        cinemachineCamera.Follow = target;
        cinemachineCamera.LookAt = null;
        cinemachineCamera.PreviousStateIsValid = false;
    }

    public void ClearTarget(Transform target)
    {
        if (!target || target != currentTarget)
            return;

        currentTarget = null;

        if (cinemachineCamera)
            cinemachineCamera.Follow = null;
    }

    public void SnapToTarget()
    {
        if (!currentTarget || !cinemachineCamera)
            return;

        cinemachineCamera.CancelDamping(true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
#else
    private void Awake()
    {
        gameObject.SetActive(false);
    }

    public void SetTarget(Transform target)
    {
    }

    public void ClearTarget(Transform target)
    {
    }

    public void SnapToTarget()
    {
    }
#endif
}
