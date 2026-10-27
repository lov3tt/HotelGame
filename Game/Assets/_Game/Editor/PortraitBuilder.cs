using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Turns the layered persona art in Art/Portraits/<Id>/ (PNG layers + rig.json, written by
// tools/portraits/make_portraits.py) into PortraitPuppet prefabs in Prefabs/Portraits/,
// and gives each persona's appearance its portrait.
//
// Editor: InnKeeper → Rebuild Portraits (also runs as part of Setup Theme & Characters)
// Prefabs are rebuilt from the art each time. An appearance only gets a portrait if it has
// none, so a Live2D model you assign yourself is never replaced.
public static class PortraitBuilder
{
    public const string ArtDir = "Assets/_Game/Art/Portraits";
    public const string PrefabDir = "Assets/_Game/Prefabs/Portraits";
    public const float PixelsPerUnit = 512f;

#pragma warning disable 0649 // filled by JsonUtility
    [Serializable] class RigFile { public string id; public string title; public int[] canvas; public RigGroup[] groups; public RigLayer[] layers; public RigDefaults defaults; }
    [Serializable] class RigGroup { public string name; public string parent; public float[] pivot; public float swing; }
    [Serializable] class RigLayer { public string name; public string file; public string group; public int order; public float depth; public int[] rect; }
    [Serializable] class RigDefaults { public float ParamEyeLOpen = 1f; public float ParamEyeROpen = 1f; }
#pragma warning restore 0649

    [MenuItem("InnKeeper/Rebuild Portraits")]
    public static void BuildMenu() => BuildAll();

    // Returns the portrait prefab for each persona id found in Art/Portraits.
    public static Dictionary<string, GameObject> BuildAll()
    {
        var result = new Dictionary<string, GameObject>();
        if (!AssetDatabase.IsValidFolder(ArtDir)) return result;
        EnsurePortraitLayer();
        EnsureFolder(PrefabDir);

        foreach (var dir in AssetDatabase.GetSubFolders(ArtDir))
        {
            string rigPath = dir + "/rig.json";
            if (!File.Exists(rigPath)) continue;
            var rig = JsonUtility.FromJson<RigFile>(File.ReadAllText(rigPath));
            ConfigureSprites(dir, rig);
            result[rig.id] = BuildPrefab(dir, rig);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Portraits] Built {result.Count} portrait prefabs: {string.Join(", ", result.Keys)}");
        return result;
    }

    static void ConfigureSprites(string dir, RigFile rig)
    {
        foreach (var layer in rig.layers)
        {
            string path = dir + "/" + layer.file;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            bool dirty = importer.textureType != TextureImporterType.Sprite
                         || importer.spriteImportMode != SpriteImportMode.Single
                         || !Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit)
                         || importer.mipmapEnabled;
            if (!dirty) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }

    // Canvas pixels (origin top-left, y down) to local units relative to a pivot (y up).
    static Vector3 ToLocal(float px, float py, Vector2 pivot) =>
        new Vector3((px - pivot.x) / PixelsPerUnit, -(py - pivot.y) / PixelsPerUnit, 0f);

    static GameObject BuildPrefab(string dir, RigFile rig)
    {
        var canvasCentre = new Vector2(rig.canvas[0] / 2f, rig.canvas[1] / 2f);
        var root = new GameObject("Portrait_" + rig.id);
        var puppet = root.AddComponent<PortraitPuppet>();
        root.AddComponent<PortraitDriver>();
        puppet.pixelsPerUnit = PixelsPerUnit;
        puppet.defaultEyeOpen = rig.defaults != null ? rig.defaults.ParamEyeLOpen : 1f;
        puppet.eyeLOpen = puppet.eyeROpen = puppet.defaultEyeOpen;

        // Groups, parents first.
        var groups = new Dictionary<string, (Transform t, Vector2 pivot)>();
        var pending = rig.groups.ToList();
        while (pending.Count > 0)
        {
            var ready = pending.FirstOrDefault(g => string.IsNullOrEmpty(g.parent) || groups.ContainsKey(g.parent));
            if (ready == null) throw new Exception($"[Portraits] {rig.id}: group parents form a loop or are missing");
            pending.Remove(ready);
            var (parent, parentPivot) = string.IsNullOrEmpty(ready.parent) ? (root.transform, canvasCentre) : groups[ready.parent];
            var go = new GameObject(ready.name);
            go.transform.SetParent(parent, false);
            var pivot = new Vector2(ready.pivot[0], ready.pivot[1]);
            go.transform.localPosition = ToLocal(pivot.x, pivot.y, parentPivot);
            groups[ready.name] = (go.transform, pivot);
            if (ready.swing > 0f) puppet.swingGroups.Add(new PortraitPuppet.SwingGroup { transform = go.transform, swing = ready.swing });
        }

        var renderers = new Dictionary<string, SpriteRenderer>();
        foreach (var layer in rig.layers.OrderBy(l => l.order))
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(dir + "/" + layer.file);
            if (sprite == null) { Debug.LogWarning($"[Portraits] {rig.id}: no sprite for {layer.file}"); continue; }
            // The closed-eye line must stay visible while the open eye group is squashed shut.
            string groupName = layer.name.StartsWith("eye_closed") ? "Head" : layer.group;
            var (parent, pivot) = groups.TryGetValue(groupName, out var g) ? g : (root.transform, canvasCentre);
            var go = new GameObject(layer.name);
            go.transform.SetParent(parent, false);
            var centre = ToLocal(layer.rect[0] + layer.rect[2] / 2f, layer.rect[1] + layer.rect[3] / 2f, pivot);
            go.transform.localPosition = centre;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = layer.order * 2;
            renderers[layer.name] = sr;
            puppet.parts.Add(new PortraitPuppet.Part { transform = go.transform, basePosition = centre, depth = layer.depth });
        }

        // Irises and highlights only show inside the eye whites.
        foreach (var side in new[] { "L", "R" })
        {
            if (!renderers.TryGetValue("eye_white_" + side, out var white)) continue;
            var masked = new[] { "iris_" + side, "eye_highlight_" + side }.Where(renderers.ContainsKey).Select(n => renderers[n]).ToArray();
            if (masked.Length == 0) continue;
            var mask = new GameObject("Mask").AddComponent<SpriteMask>();
            mask.transform.SetParent(white.transform, false);
            mask.sprite = white.sprite;
            mask.isCustomRangeActive = true;
            mask.backSortingOrder = white.sortingOrder;
            mask.frontSortingOrder = masked.Max(r => r.sortingOrder);
            foreach (var r in masked) r.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        Transform G(string n) => groups.TryGetValue(n, out var v) ? v.t : null;
        SpriteRenderer R(string n) => renderers.TryGetValue(n, out var v) ? v : null;
        puppet.body = G("Body");
        puppet.head = G("Head");
        puppet.eyeL = G("EyeL"); puppet.eyeR = G("EyeR");
        puppet.irisL = G("IrisL"); puppet.irisR = G("IrisR");
        puppet.browL = G("BrowL"); puppet.browR = G("BrowR");
        puppet.eyeClosedL = R("eye_closed_L"); puppet.eyeClosedR = R("eye_closed_R");
        puppet.mouthClosed = R("mouth_closed"); puppet.mouthOpen = R("mouth_open");
        if (puppet.head != null) puppet.headBase = puppet.head.localPosition;
        if (puppet.irisL != null) puppet.irisLBase = puppet.irisL.localPosition;
        if (puppet.irisR != null) puppet.irisRBase = puppet.irisR.localPosition;
        if (puppet.browL != null) puppet.browLBase = puppet.browL.localPosition;
        if (puppet.browR != null) puppet.browRBase = puppet.browR.localPosition;
        puppet.Apply(0f);

        string path = PrefabDir + "/Portrait_" + rig.id + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    // Portraits render on their own layer so the inn camera never sees them and vice versa.
    static void EnsurePortraitLayer()
    {
        if (LayerMask.NameToLayer(PortraitView.LayerName) >= 0) return;
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        for (int i = 31; i >= 8; i--)
        {
            var slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue)) continue;
            slot.stringValue = PortraitView.LayerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[Portraits] Added layer '{PortraitView.LayerName}' at index {i}");
            return;
        }
        Debug.LogWarning("[Portraits] No free layer for portraits; using layer 31");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
