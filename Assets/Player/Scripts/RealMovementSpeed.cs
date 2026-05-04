using UnityEngine;

public class RealMovementSpeedDebug : MonoBehaviour
{
    [Header("Display")]
    public bool showOnGUI = true;
    public bool measureHorizontalOnly = true;
    public bool smoothSpeed = true;
    public float smoothRate = 10f;

    [Header("Optional Animator Sync")]
    public Animator animator;
    public bool writeSpeedToAnimator = false;
    public string animatorFloatName = "RealSpeed";

    [Header("Read Only")]
    public float rawSpeed;
    public float displayedSpeed;
    public float rawTotalSpeed;

    private Vector3 _lastPosition;
    private bool _initialized;
    private int _speedHash;

    void Awake()
    {
        _speedHash = Animator.StringToHash(animatorFloatName);
    }

    void OnEnable()
    {
        _lastPosition = transform.position;
        _initialized = true;
        rawSpeed = 0f;
        rawTotalSpeed = 0f;
        displayedSpeed = 0f;
    }

    void LateUpdate()
    {
        if (!_initialized)
        {
            _lastPosition = transform.position;
            _initialized = true;
            return;
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 delta = transform.position - _lastPosition;

        rawTotalSpeed = delta.magnitude / dt;

        Vector3 horizontalDelta = delta;
        horizontalDelta.y = 0f;
        rawSpeed = horizontalDelta.magnitude / dt;

        float target = measureHorizontalOnly ? rawSpeed : rawTotalSpeed;

        if (smoothSpeed)
            displayedSpeed = Mathf.Lerp(displayedSpeed, target, smoothRate * Time.deltaTime);
        else
            displayedSpeed = target;

        if (writeSpeedToAnimator && animator != null)
        {
            animator.SetFloat(_speedHash, displayedSpeed);
        }

        _lastPosition = transform.position;
    }

    void OnGUI()
    {
        if (!showOnGUI) return;

        GUI.Box(new Rect(12, 12, 220, 88), "Movement Debug");
        GUI.Label(new Rect(24, 40, 200, 20), $"Horizontal: {rawSpeed:F2}");
        GUI.Label(new Rect(24, 58, 200, 20), $"Total:      {rawTotalSpeed:F2}");
        GUI.Label(new Rect(24, 76, 200, 20), $"Display:    {displayedSpeed:F2}");
    }
}