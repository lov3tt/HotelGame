using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Lets a Live2D Cubism model act as a staff portrait. Add it next to a Cubism model prefab
// (the one Cubism's importer makes from a .model3.json), add a PortraitDriver, and put the
// prefab in a CharacterAppearance's Portrait Prefab slot.
//
// It talks to the Cubism SDK through reflection, so the game still compiles on machines where
// the SDK isn't imported (the SDK isn't in git: see docs/Live2D.md). Parameter ids are the
// standard Cubism ones (PortraitParams), which models made from the standard template already use.
[DefaultExecutionOrder(-100)] // write values before CubismUpdateController's LateUpdate applies them
public class CubismPortraitModel : MonoBehaviour, IPortraitModel
{
    const string ModelTypeName = "Live2D.Cubism.Core.CubismModel";

    class Param
    {
        public Component component;
        public float min, max, def;
        public float target;
    }

    [Serializable]
    public struct Alias
    {
        [Tooltip("The standard id the game sends, e.g. ParamMouthOpenY.")] public string standardId;
        [Tooltip("The id this model uses instead, e.g. ParamA.")] public string modelId;
    }

    [Tooltip("For models whose parameters don't use the standard ids.")]
    public List<Alias> aliases = new List<Alias>();

    readonly Dictionary<string, Param> parameters = new Dictionary<string, Param>();
    static FieldInfo valueField;
    bool bound;

    public bool HasModel => Bind();
    public int ParameterCount => Bind() ? parameters.Count : 0;

    bool Bind()
    {
        if (bound) return true;
        Component model = null;
        foreach (var c in GetComponentsInChildren<Component>(true))
            if (c != null && c.GetType().FullName == ModelTypeName) { model = c; break; }
        if (model == null) return false;

        var list = model.GetType().GetProperty("Parameters")?.GetValue(model) as Array;
        if (list == null) return false;
        foreach (Component p in list)
        {
            var t = p.GetType();
            valueField ??= t.GetField("Value");
            string id = t.GetProperty("Id")?.GetValue(p) as string;
            if (string.IsNullOrEmpty(id) || valueField == null) continue;
            float Get(string prop, float fallback) => t.GetProperty(prop)?.GetValue(p) is float f ? f : fallback;
            var param = new Param
            {
                component = p,
                min = Get("MinimumValue", float.MinValue),
                max = Get("MaximumValue", float.MaxValue),
                def = Get("DefaultValue", 0f),
            };
            param.target = param.def;
            parameters[id] = param;
        }
        foreach (var a in aliases)
            if (!string.IsNullOrEmpty(a.standardId) && a.modelId != null && parameters.TryGetValue(a.modelId, out var p))
                parameters[a.standardId] = p;
        bound = parameters.Count > 0;
        return bound;
    }

    public void SetParameter(string id, float value)
    {
        if (Bind() && parameters.TryGetValue(id, out var p)) p.target = Mathf.Clamp(value, p.min, p.max);
    }

    public float GetParameter(string id) => Bind() && parameters.TryGetValue(id, out var p) ? p.target : 0f;

    public float GetDefault(string id) => Bind() && parameters.TryGetValue(id, out var p) ? p.def : 0f;

    void LateUpdate() => Apply();

    // Pushes the target values into the Cubism parameters.
    public void Apply()
    {
        if (!Bind()) return;
        foreach (var p in parameters.Values) valueField.SetValue(p.component, p.target);
    }
}
