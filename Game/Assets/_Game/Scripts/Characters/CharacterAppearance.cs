using UnityEngine;

// One "look" for a guest or staff member: which model to show and how to dress it.
// Any Humanoid-rigged model works with the shared animations (see docs/CustomCharacters.md).
[CreateAssetMenu(menuName = "InnKeeper/Character Appearance", fileName = "App_New")]
public class CharacterAppearance : ScriptableObject
{
    public string displayName = "New Look";

    [Tooltip("The model to spawn: an FBX with Rig → Humanoid, or a prefab made from one.")]
    public GameObject modelPrefab;
    [Tooltip("Optional. Overrides the Avatar found on the model's Animator.")]
    public Avatar avatarOverride;

    [Header("Dressing")]
    [Tooltip("Optional material swaps by slot index. Empty entries keep the model's own material.")]
    public Material[] materialOverrides;
    [Tooltip("Tint the model's main colour without making a new material.")]
    public bool useTint;
    public Color tint = Color.white;

    [Header("Fit")]
    [Tooltip("1 = model's own size. Use it to make kids, tall nobles, or fix a model imported at the wrong scale.")]
    public float heightScale = 1f;
    [Tooltip("Nudge the model inside the rig if its pivot isn't at its feet.")]
    public Vector3 modelOffset;
    [Tooltip("Rotate the model if it doesn't face +Z.")]
    public float yawOffset;
}
