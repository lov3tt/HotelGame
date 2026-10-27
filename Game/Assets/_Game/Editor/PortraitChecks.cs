using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless checks for the 2.5D staff portraits: the puppet rigs respond to every parameter,
// each persona has a portrait, and the HUD staff bar shows each one through its own camera.
// Opens Inn.unity and never saves.
//
// Editor:   InnKeeper → Run Portrait Checks
// Headless: Unity.exe -batchmode -nographics -quit -projectPath Game -executeMethod PortraitChecks.RunBatch
public static class PortraitChecks
{
    static readonly List<string> failures = new List<string>();

    [MenuItem("InnKeeper/Run Portrait Checks")]
    public static bool Run()
    {
        failures.Clear();
        var setup = ProjectSetup.EnsureAssets();
        CheckPersonas(setup);
        CheckRigs();
        CheckCubismBridge();

        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Inn.unity");
        CheckStaffBar();

        if (failures.Count == 0) Debug.Log("[Portraits] ALL CHECKS PASSED");
        else Debug.LogError($"[Portraits] {failures.Count} CHECK(S) FAILED");
        return failures.Count == 0;
    }

    public static void RunBatch()
    {
        if (!Run()) EditorApplication.Exit(1);
    }

    static void Check(bool ok, string what)
    {
        if (ok) Debug.Log("[Portraits] OK   " + what);
        else
        {
            Debug.LogError("[Portraits] FAIL " + what);
            failures.Add(what);
        }
    }

    static void CheckPersonas(ProjectSetup.Assets setup)
    {
        Check(LayerMask.NameToLayer(PortraitView.LayerName) >= 0, "the Portrait layer exists");
        foreach (var role in ProjectSetup.PersonaRoles)
        {
            var app = setup.personaByRole[role];
            Check(app.portraitPrefab != null && app.portraitPrefab.GetComponent<IPortraitModel>() != null,
                $"{app.displayName} has a portrait model");
        }
    }

    static void CheckRigs()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PortraitBuilder.PrefabDir }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var go = Object.Instantiate(prefab);
            try { CheckRig(prefab.name, go.GetComponent<PortraitPuppet>()); }
            finally { Object.DestroyImmediate(go); }
        }
    }

    static void CheckRig(string name, PortraitPuppet p)
    {
        if (p == null) { Check(false, name + " has a PortraitPuppet"); return; }
        Check(p.head && p.body && p.eyeL && p.eyeR && p.irisL && p.irisR && p.mouthOpen && p.mouthClosed && p.eyeClosedL,
            name + " rig has head, body, eyes, irises and mouths");
        Check(p.parts.Count >= 20 && p.parts.All(part => part.transform != null), $"{name} has {p.parts.Count} layers, all bound");
        Check(p.swingGroups.Count >= 3, $"{name} has {p.swingGroups.Count} physics groups (hair, ears, tail)");
        Check(p.GetComponentsInChildren<SpriteMask>().Length == 2, name + " clips both irises to the eye whites");

        var hairFront = p.parts.First(x => x.transform.name == "hair_front");
        var hairBack = p.parts.First(x => x.transform.name == "hair_back");
        Pose(p, PortraitParams.AngleX, 30);
        float frontShift = hairFront.transform.localPosition.x - hairFront.basePosition.x;
        float backShift = hairBack.transform.localPosition.x - hairBack.basePosition.x;
        Check(frontShift > 0.05f && backShift < 0f, $"{name} head turn has parallax (fringe {frontShift:F3}, back hair {backShift:F3})");
        Pose(p, PortraitParams.AngleX, 0);

        Pose(p, PortraitParams.EyeLOpen, 0);
        Check(p.eyeL.localScale.y == 0f && p.eyeClosedL.enabled, name + " blink shuts the left eye");
        Pose(p, PortraitParams.EyeLOpen, 1);
        Check(p.eyeL.localScale.y > 0.99f && !p.eyeClosedL.enabled, name + " eye reopens");

        Pose(p, PortraitParams.MouthOpenY, 1);
        Check(p.mouthOpen.enabled && !p.mouthClosed.enabled, name + " mouth opens");
        Pose(p, PortraitParams.MouthOpenY, 0);
        Check(!p.mouthOpen.enabled && p.mouthClosed.enabled, name + " mouth closes");

        Vector3 iris = p.irisL.localPosition;
        Pose(p, PortraitParams.EyeBallX, 1);
        Check(p.irisL.localPosition.x > iris.x, name + " irises follow the look direction");

        Pose(p, PortraitParams.AngleX, 99);
        Check(Mathf.Approximately(p.GetParameter(PortraitParams.AngleX), 30), name + " clamps parameters to their range");
    }

    // Ren uses every standard parameter id; some other samples (e.g. Mao's vowel mouth) need aliases.
    public const string CubismSamplePath = "Assets/Live2D/Cubism/Samples/Models/Ren/Ren.prefab";

    // Drives one of the Cubism SDK's sample models through CubismPortraitModel, to prove a real
    // Live2D model can stand in for the puppet. Skipped where the SDK isn't imported.
    static void CheckCubismBridge()
    {
        var sample = AssetDatabase.LoadAssetAtPath<GameObject>(CubismSamplePath);
        if (sample == null) { Debug.Log("[Portraits] SKIP Cubism bridge: SDK samples not imported"); return; }
        var go = Object.Instantiate(sample);
        try
        {
            var bridge = go.AddComponent<CubismPortraitModel>();
            Check(bridge.HasModel && bridge.ParameterCount > 10, $"Cubism sample binds through the adapter ({bridge.ParameterCount} parameters)");
            bridge.SetParameter(PortraitParams.AngleX, 20f);
            bridge.SetParameter(PortraitParams.MouthOpenY, 0.8f);
            bridge.SetParameter(PortraitParams.AngleY, 999f);
            bridge.Apply();
            float angleX = CubismValue(go, PortraitParams.AngleX), mouth = CubismValue(go, PortraitParams.MouthOpenY);
            Check(Mathf.Approximately(angleX, 20f) && Mathf.Approximately(mouth, 0.8f),
                $"adapter writes head turn and mouth into the Cubism model (AngleX {angleX}, MouthOpenY {mouth})");
            Check(CubismValue(go, PortraitParams.AngleY) <= 30f, "adapter clamps to the Cubism parameter's range");
            PortraitDriver.Pose(bridge, PortraitMood.Tired, false, 1.3f, 3, 0f);
            bridge.Apply();
            float eye = CubismValue(go, PortraitParams.EyeLOpen);
            Check(eye > 0f && eye < bridge.GetDefault(PortraitParams.EyeLOpen), $"PortraitDriver moods drive a Cubism model (tired eyes {eye:F2})");
        }
        finally { Object.DestroyImmediate(go); }
    }

    static float CubismValue(GameObject root, string id)
    {
        foreach (var c in root.GetComponentsInChildren<Component>(true))
        {
            if (c == null || c.GetType().FullName != "Live2D.Cubism.Core.CubismParameter") continue;
            if ((string)c.GetType().GetProperty("Id").GetValue(c) == id) return (float)c.GetType().GetField("Value").GetValue(c);
        }
        return float.NaN;
    }

    static void Pose(PortraitPuppet p, string id, float value)
    {
        p.SetParameter(id, value);
        p.Apply(0f);
    }

    static void CheckStaffBar()
    {
        var mainCam = Object.FindFirstObjectByType<CutawayCameraController>().GetComponent<Camera>();
        Check((mainCam.cullingMask & (1 << PortraitView.PortraitLayer)) == 0, "the inn camera doesn't see the portrait layer");

        var views = Object.FindObjectsByType<PortraitView>(FindObjectsSortMode.None);
        Check(views.Length == ProjectSetup.PersonaRoles.Length, $"staff bar has {views.Length} portraits");
        // FindObjectsByType skips the hidden edit-mode stages, so collect the portraits from the views.
        var puppets = views.Where(v => v.Instance != null).Select(v => v.Instance.transform).ToArray();
        foreach (var view in views)
        {
            string who = view.Appearance != null ? view.Appearance.displayName : view.name;
            Check(view.Instance != null && view.StageCamera != null && view.StageCamera.targetTexture != null,
                who + " portrait has a model, camera and texture");
            if (view.StageCamera == null) continue;
            // Each camera must see its own portrait and nobody else's.
            var cam = view.StageCamera;
            var seen = puppets.Where(pp => pp.gameObject.layer == PortraitView.PortraitLayer && InView(cam, pp.position)).ToArray();
            Check(seen.Length == 1 && seen[0].gameObject == view.Instance,
                $"{who} camera sees only its own portrait (sees {seen.Length}: {string.Join(", ", seen.Select(s => s.name))})");
        }

        var cards = GameObject.Find("StaffBar").transform.Cast<RectTransform>().ToArray();
        bool overlap = cards.Any(a => cards.Any(b => a != b && Rect(a).Overlaps(Rect(b))));
        Check(!overlap, "staff cards don't overlap");
        var names = cards.Select(c => c.GetComponentInChildren<TMP_Text>()).ToArray();
        Check(names.All(t => t != null && !t.text.Contains("{") && t.text.Contains("\n")),
            "staff cards show name and themed role (" + string.Join(" | ", names.Select(t => t?.text.Replace("\n", " / "))) + ")");
    }

    static bool InView(Camera cam, Vector3 world)
    {
        var v = cam.WorldToViewportPoint(world);
        return v.z > 0 && v.x > -0.5f && v.x < 1.5f && v.y > -0.5f && v.y < 1.5f;
    }

    static Rect Rect(RectTransform r) => new Rect(r.anchoredPosition, r.sizeDelta);
}
