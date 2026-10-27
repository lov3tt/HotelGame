using UnityEngine;
using UnityEngine.InputSystem;

// Side-view cutaway camera: the game mainly looks 2D, with 3D up close.
//
// Zoomed out (most of the zoom range): looks straight at the open front of the
// inn through a near-orthographic lens, so every floor reads like a flat 2D
// dollhouse. Zooming in close swings the camera to an angle and widens the lens
// into real perspective, revealing the 3D rooms.
//
// The trick: a very narrow field of view from far away looks almost identical to
// an orthographic camera, so one perspective camera blends FOV, distance and angle.
// The on-screen size of the inn depends only on 'viewSize', so zoom feels consistent.
//
// WASD / arrow keys or middle-mouse drag to pan, mouse wheel to zoom.
// Uses unscaled time so it still works while the game is paused.
[RequireComponent(typeof(Camera))]
public class CutawayCameraController : MonoBehaviour
{
    [Header("Focus & pan (the inn's front faces -Z)")]
    [SerializeField] Vector3 focus = new Vector3(2.5f, 4.5f, 2.5f);
    [SerializeField] float panSpeed = 12f;
    [SerializeField] Vector2 boundsMinXY = new Vector2(-12f, 0f);
    [SerializeField] Vector2 boundsMaxXY = new Vector2(17f, 9f);

    [Header("Zoom (0 = fully out / 2D, 1 = fully in / 3D)")]
    [SerializeField, Range(0f, 1f)] float zoom = 0f;
    [SerializeField] float zoomStep = 0.1f;
    [SerializeField] float zoomSmoothTime = 0.12f;
    [Tooltip("Half the visible height in world units when fully zoomed out.")]
    [SerializeField] float maxViewSize = 9f;
    [Tooltip("Half the visible height in world units when fully zoomed in.")]
    [SerializeField] float minViewSize = 2.5f;
    [Tooltip("Zoom level where the 3D swing begins. Below this the view stays flat 2D.")]
    [SerializeField, Range(0f, 1f)] float tiltStart = 0.6f;

    [Header("2D look (zoomed out)")]
    [SerializeField] float flatPitch = 0f;
    [SerializeField] float flatYaw = 0f;
    [SerializeField] float flatFov = 4f;

    [Header("3D look (zoomed in)")]
    [SerializeField] float closePitch = 20f;
    [SerializeField] float closeYaw = 25f;
    [SerializeField] float closeFov = 35f;

    Camera cam;
    float targetZoom;
    float zoomVelocity;
    float currentPitch, currentViewSize;

    public float Zoom => zoom;
    // 0 = flat 2D, 1 = full 3D. UI or effects can react to it (e.g. hide labels up close).
    public float TiltAmount { get; private set; }

    void Awake()
    {
        cam = GetComponent<Camera>();
        targetZoom = zoom;
        Apply();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                targetZoom = Mathf.Clamp01(targetZoom + Mathf.Sign(scroll) * zoomStep);
        }
        zoom = Mathf.SmoothDamp(zoom, targetZoom, ref zoomVelocity, zoomSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        // Pan across the face of the building: left/right along the floors, up/down between them.
        Vector2 move = Vector2.zero;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
        }
        float speedScale = currentViewSize / 8f;
        Vector3 pan = (Vector3.right * move.x + Vector3.up * move.y).normalized;
        focus += pan * panSpeed * speedScale * Time.unscaledDeltaTime;

        // Middle-mouse drag: move the building under the cursor.
        if (mouse != null && mouse.middleButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            float worldPerPixel = 2f * currentViewSize / Screen.height;
            focus -= (Vector3.right * delta.x + Vector3.up * delta.y / Mathf.Cos(currentPitch * Mathf.Deg2Rad)) * worldPerPixel;
        }

        focus.x = Mathf.Clamp(focus.x, boundsMinXY.x, boundsMaxXY.x);
        focus.y = Mathf.Clamp(focus.y, boundsMinXY.y, boundsMaxXY.y);
        Apply();
    }

    // Jump (or ease, if instant is false) to a zoom level. Used by tools and later by UI buttons.
    public void SetZoom(float value, bool instant)
    {
        targetZoom = Mathf.Clamp01(value);
        if (instant)
        {
            zoom = targetZoom;
            zoomVelocity = 0f;
            Apply();
        }
    }

    public void SetFocus(Vector3 point)
    {
        focus = point;
        Apply();
    }

    void Apply()
    {
        if (cam == null) cam = GetComponent<Camera>();

        TiltAmount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tiltStart, 1f, zoom));
        currentPitch = Mathf.Lerp(flatPitch, closePitch, TiltAmount);
        float yaw = Mathf.LerpAngle(flatYaw, closeYaw, TiltAmount);
        float fov = Mathf.Lerp(flatFov, closeFov, TiltAmount);
        currentViewSize = Mathf.Lerp(maxViewSize, minViewSize, zoom);

        // Back off far enough that 'viewSize' fills half the screen height at this FOV.
        float distance = currentViewSize / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        Quaternion rot = Quaternion.Euler(currentPitch, yaw, 0f);
        transform.SetPositionAndRotation(focus - rot * Vector3.forward * distance, rot);

        cam.orthographic = false;
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = distance + 50f;
    }

    void OnValidate() => Apply();
}
