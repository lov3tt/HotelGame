using System;
using UnityEngine;

// The body of every guest and staff member. It spawns the model from a CharacterAppearance
// into the model slot and hooks it up to the shared Humanoid animator controller, so any
// Humanoid model plays the same animations.
//
// Change the Appearance field in the Inspector (or call SetAppearance) to swap models.
// Outside Play mode it shows a temporary, unsaved preview so you can see the look in the Scene view.
[ExecuteAlways]
public class CharacterRig : MonoBehaviour
{
    [SerializeField] CharacterAppearance appearance;
    [Tooltip("Where the model is spawned. Defaults to this object.")]
    [SerializeField] Transform modelSlot;
    [Tooltip("Shared controller with Idle/Walk/Carry/work states (Animation/InnCharacter).")]
    [SerializeField] RuntimeAnimatorController animatorController;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    public CharacterAppearance Appearance => appearance;
    public GameObject Model { get; private set; }
    // Null when the model isn't rigged (e.g. a graybox placeholder); AgentAnimator copes with that.
    public Animator Animator { get; private set; }
    public event Action<CharacterRig> ModelChanged;

    void OnEnable()
    {
        if (!gameObject.scene.IsValid()) return; // a prefab asset on disk, not a placed character
        Build();
    }

    void OnDisable()
    {
        if (!Application.isPlaying) ClearModel();
    }

    public void SetAppearance(CharacterAppearance newAppearance)
    {
        appearance = newAppearance;
        if (isActiveAndEnabled) Build();
    }

    void Build()
    {
        ClearModel();
        var slot = modelSlot != null ? modelSlot : transform;

        var source = appearance != null ? appearance.modelPrefab : null;
        Model = source != null ? Instantiate(source, slot) : CreateFallbackModel(slot);
        Model.name = "Model";
        if (!Application.isPlaying)
            foreach (var t in Model.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave; // preview only, never saved into the scene

        float scale = appearance != null ? appearance.heightScale : 1f;
        Model.transform.localPosition = appearance != null ? appearance.modelOffset : Vector3.zero;
        Model.transform.localRotation = Quaternion.Euler(0f, appearance != null ? appearance.yawOffset : 0f, 0f);
        Model.transform.localScale = Vector3.one * scale;

        // Models shouldn't block the NavMesh or clicks meant for the rig.
        foreach (var col in Model.GetComponentsInChildren<Collider>(true))
            DestroyNow(col);

        Animator = Model.GetComponentInChildren<Animator>();
        if (Animator != null)
        {
            if (appearance != null && appearance.avatarOverride != null) Animator.avatar = appearance.avatarOverride;
            Animator.runtimeAnimatorController = animatorController;
            Animator.applyRootMotion = false; // the NavMeshAgent moves the character, not the animation
        }

        Dress();
        ModelChanged?.Invoke(this);
    }

    void Dress()
    {
        if (appearance == null) return;
        var block = new MaterialPropertyBlock();
        foreach (var r in Model.GetComponentsInChildren<Renderer>(true))
        {
            var overrides = appearance.materialOverrides;
            if (overrides != null && overrides.Length > 0)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length && i < overrides.Length; i++)
                    if (overrides[i] != null) mats[i] = overrides[i];
                r.sharedMaterials = mats;
            }
            if (appearance.useTint)
            {
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, appearance.tint); // URP Lit
                block.SetColor(ColorId, appearance.tint);     // other shaders
                r.SetPropertyBlock(block);
            }
        }
    }

    // A plain capsule, used when no appearance or model is set.
    GameObject CreateFallbackModel(Transform slot)
    {
        var root = new GameObject("Model");
        root.transform.SetParent(slot, false);
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        body.transform.localScale = new Vector3(0.5f, 0.85f, 0.5f);
        return root;
    }

    void ClearModel()
    {
        if (Model != null) DestroyNow(Model);
        Model = null;
        Animator = null;
    }

    static void DestroyNow(UnityEngine.Object obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

#if UNITY_EDITOR
    // Rebuild the preview when the appearance is changed in the Inspector.
    // Objects can't be created inside OnValidate itself, so wait one editor tick.
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled && gameObject.scene.IsValid()) Build();
        };
    }
#endif
}
