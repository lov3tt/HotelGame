using UnityEngine;
using UnityEngine.UI;

// Shows a character's animated portrait in the UI. It spawns the appearance's portrait prefab
// (a PortraitPuppet or a Live2D model) on a hidden stage far below the inn, films it with its
// own camera into a RenderTexture, and puts that texture on this RawImage.
//
// Works in the editor too, so the HUD shows the portraits without pressing Play.
[ExecuteAlways]
[RequireComponent(typeof(RawImage))]
public class PortraitView : MonoBehaviour
{
    public const string LayerName = "Portrait";
    const int FallbackLayer = 31;

    [SerializeField] CharacterAppearance appearance;
    [SerializeField] PortraitMood mood;
    [SerializeField] int resolution = 384;
    [Tooltip("Centre of the shot relative to the portrait's origin, in world units.")]
    [SerializeField] Vector2 framingCentre = new Vector2(0f, 0.08f);
    [Tooltip("Half the height of the shot, in world units. Smaller = closer.")]
    [SerializeField] float framingSize = 0.78f;

    static int nextSlot;
    GameObject stage;
    RenderTexture texture;

    public GameObject Instance { get; private set; }
    public Camera StageCamera { get; private set; }
    public PortraitDriver Driver { get; private set; }
    public IPortraitModel Model { get; private set; }
    public CharacterAppearance Appearance => appearance;

    public PortraitMood Mood
    {
        get => mood;
        set { mood = value; if (Driver != null) Driver.mood = value; }
    }

    public void SetAppearance(CharacterAppearance look)
    {
        appearance = look;
        Build();
    }

    public static int PortraitLayer
    {
        get { int layer = LayerMask.NameToLayer(LayerName); return layer >= 0 ? layer : FallbackLayer; }
    }

    void OnEnable() => Build();
    void OnDisable() => Teardown();

    void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Build(); };
#endif
    }

    void Build()
    {
        Teardown();
        var image = GetComponent<RawImage>();
        var prefab = appearance != null ? appearance.portraitPrefab : null;
        if (prefab == null)
        {
            image.texture = null;
            image.color = Color.clear;
            return;
        }

        stage = new GameObject("PortraitStage_" + name);
        stage.transform.position = new Vector3(nextSlot++ % 64 * 10f, -1000f, 0f);

        Instance = Instantiate(prefab, stage.transform);
        Instance.transform.localPosition = Vector3.zero;
        Model = Instance.GetComponent<IPortraitModel>();
        Driver = Instance.GetComponent<PortraitDriver>();
        if (Driver == null) Driver = Instance.AddComponent<PortraitDriver>();
        Driver.mood = mood;

        texture = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32)
            { name = "Portrait_" + name, antiAliasing = 2 };
        texture.Create();

        var camGo = new GameObject("PortraitCamera");
        camGo.transform.SetParent(stage.transform, false);
        camGo.transform.localPosition = new Vector3(framingCentre.x, framingCentre.y, -10f);
        StageCamera = camGo.AddComponent<Camera>();
        StageCamera.orthographic = true;
        StageCamera.orthographicSize = framingSize;
        StageCamera.nearClipPlane = 0.1f;
        StageCamera.farClipPlane = 20f;
        StageCamera.clearFlags = CameraClearFlags.SolidColor;
        StageCamera.backgroundColor = Color.clear;
        StageCamera.cullingMask = 1 << PortraitLayer;
        StageCamera.targetTexture = texture;

        Prepare(stage.transform, PortraitLayer, !Application.isPlaying);
        image.texture = texture;
        image.color = Color.white;
    }

    static void Prepare(Transform t, int layer, bool editPreview)
    {
        t.gameObject.layer = layer;
        // Edit-mode previews must never be saved into the scene.
        if (editPreview) t.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
        foreach (Transform child in t) Prepare(child, layer, editPreview);
    }

    void Teardown()
    {
        if (stage != null) Kill(stage);
        if (texture != null)
        {
            if (StageCamera != null) StageCamera.targetTexture = null;
            texture.Release();
            Kill(texture);
        }
        stage = null;
        texture = null;
        Instance = null;
        StageCamera = null;
        Driver = null;
        Model = null;
    }

    static void Kill(Object o)
    {
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
