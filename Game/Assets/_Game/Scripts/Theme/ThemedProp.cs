using UnityEngine;

// Lets a theme replace a graybox block with real art.
// If the active theme has a prefab for this prop key, it's spawned in place and the block's
// renderer is hidden. The block's collider stays, so the NavMesh and clicks are unaffected.
// With no prefab, the graybox block simply stays visible.
//
// The prefab is placed on the floor under the block, at the block's rotation. Its pivot
// should be at the bottom centre, with the front of the furniture facing -Z (toward the camera).
// Outside Play mode the prop is a temporary, unsaved preview.
[ExecuteAlways]
public class ThemedProp : MonoBehaviour
{
    [SerializeField] string propKey = ThemeKeys.PropBed;

    GameObject spawned;
    Renderer blockRenderer;

    public string PropKey => propKey;
    public GameObject Spawned => spawned;

    void OnEnable()
    {
        blockRenderer = GetComponent<Renderer>();
        ThemeManager.ThemeChanged += Refresh;
        Refresh(ThemeManager.Current);
    }

    void OnDisable()
    {
        ThemeManager.ThemeChanged -= Refresh;
        Clear();
    }

    void Refresh(ThemeProfile theme)
    {
        Clear();
        var prefab = theme != null ? theme.GetProp(propKey) : null;

        if (blockRenderer != null) blockRenderer.enabled = prefab == null;
        if (prefab == null || !gameObject.scene.IsValid()) return;

        // Spawn as a sibling, not a child, so the block's non-uniform scale doesn't stretch it.
        var bounds = blockRenderer != null ? blockRenderer.bounds : new Bounds(transform.position, Vector3.zero);
        var floorPoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        spawned = Instantiate(prefab, floorPoint, transform.rotation, transform.parent);
        spawned.name = prefab.name + " (" + name + ")";
        // The block's collider already does the blocking; the art is just for looks.
        foreach (var col in spawned.GetComponentsInChildren<Collider>(true))
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
        if (!Application.isPlaying)
            foreach (var t in spawned.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave; // preview only, never saved into the scene
    }

    void Clear()
    {
        if (spawned == null) return;
        if (Application.isPlaying) Destroy(spawned);
        else DestroyImmediate(spawned);
        spawned = null;
    }
}
