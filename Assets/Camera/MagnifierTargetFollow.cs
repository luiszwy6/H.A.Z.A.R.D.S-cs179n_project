using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MagnifierTargetFollowMouseWorld : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera rayCamera;

    [Header("Raycast")]
    [SerializeField] private LayerMask mouseWorldLayers = ~0;
    [SerializeField] private float rayDistance = 300f;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Fallback Ground Plane")]
    [SerializeField] private bool useGroundPlaneFallback = true;
    [SerializeField] private float groundPlaneY = 0f;

    [Header("Follow")]
    [SerializeField] private bool smoothFollow = true;
    [SerializeField] private float followSpeed = 30f;

    [Header("Vertical")]
    [SerializeField] private bool keepCurrentY = true;

    private void Reset()
    {
        if (rayCamera == null)
            rayCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (rayCamera == null || Mouse.current == null)
            return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = rayCamera.ScreenPointToRay(mousePos);

        Vector3 targetPos;

        if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                rayDistance,
                mouseWorldLayers,
                triggerInteraction))
        {
            targetPos = hit.point;
        }
        else if (useGroundPlaneFallback)
        {
            Plane plane = new Plane(Vector3.up, new Vector3(0f, groundPlaneY, 0f));

            if (!plane.Raycast(ray, out float enter))
                return;

            targetPos = ray.GetPoint(enter);
        }
        else
        {
            return;
        }

        if (keepCurrentY)
            targetPos.y = transform.position.y;

        if (smoothFollow)
        {
            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                1f - Mathf.Exp(-followSpeed * Time.deltaTime)
            );
        }
        else
        {
            transform.position = targetPos;
        }
    }
}