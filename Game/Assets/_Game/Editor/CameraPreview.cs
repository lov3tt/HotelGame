using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders the Inn scene's camera at several zoom levels to PNG files,
// so the 2D → 3D zoom blend can be checked without pressing Play.
// Also renders the HUD (switched to camera space for the shot) and a sheet of every
// portrait in every expression.
//
// Editor:   InnKeeper → Capture Camera Previews  (writes to <repo>/previews)
// Headless: Unity.exe -batchmode -quit -projectPath Game -executeMethod CameraPreview.Capture [-previewOut <dir>]
//           (no -nographics: rendering needs a graphics device)
public static class CameraPreview
{
    const string ScenePath = "Assets/_Game/Scenes/Inn.unity";

    static readonly (string name, float zoom, Vector3 focus)[] Shots =
    {
        ("1_zoomed_out_2d", 0f, new Vector3(2.5f, 4.5f, 2.5f)),
        ("2_mid_still_2d", 0.55f, new Vector3(4f, 4.5f, 2.5f)),
        ("3_tilting", 0.8f, new Vector3(4f, 4.5f, 3f)),
        ("4_zoomed_in_3d", 1f, new Vector3(8.5f, 7f, 3f)),
        ("5_lobby_characters_3d", 1f, new Vector3(-0.8f, 1.1f, 2.5f)),
        ("6_reception_and_kitchen", 0.9f, new Vector3(2.5f, 1.2f, 2.5f)),
        ("7_first_floor_staff", 0.92f, new Vector3(4.5f, 4.1f, 2.5f)),
    };

    [MenuItem("InnKeeper/Capture Camera Previews")]
    public static void Capture()
    {
        string outDir = GetArg("-previewOut")
                        ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../../previews"));
        Directory.CreateDirectory(outDir);

        EditorSceneManager.OpenScene(ScenePath);
        var controller = UnityEngine.Object.FindFirstObjectByType<CutawayCameraController>();
        var cam = controller.GetComponent<Camera>();

        // The first render after opening a scene in batch mode comes out unlit,
        // so render once and throw it away.
        RenderToPng(cam, Path.Combine(outDir, "_warmup.png"), 64, 36);
        File.Delete(Path.Combine(outDir, "_warmup.png"));

        foreach (var shot in Shots)
        {
            controller.SetFocus(shot.focus);
            controller.SetZoom(shot.zoom, instant: true);
            string path = Path.Combine(outDir, shot.name + ".png");
            RenderToPng(cam, path, 1600, 900);
            Debug.Log("[Preview] Wrote " + path);
        }

        CaptureHud(controller, cam, Path.Combine(outDir, "8_hud_staff_bar.png"));
        CapturePortraitSheet(Path.Combine(outDir, "9_portrait_expressions.png"));
    }

    // An overlay canvas isn't part of a camera render, so switch the HUD to camera space just for this shot.
    static void CaptureHud(CutawayCameraController controller, Camera cam, string path)
    {
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        controller.SetFocus(new Vector3(2.5f, 4.5f, 2.5f));
        controller.SetZoom(0.3f, instant: true);
        var mode = canvas.renderMode;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = cam.nearClipPlane + 0.05f;
        // Render the portraits a few times first: in batch mode their textures finish uploading
        // over the first frames, and a single render can catch them half loaded.
        var views = UnityEngine.Object.FindObjectsByType<PortraitView>(FindObjectsSortMode.None);
        for (int pass = 0; pass < 3; pass++)
        {
            foreach (var view in views)
            {
                if (view.StageCamera == null) continue;
                view.Instance.GetComponent<PortraitPuppet>()?.Apply(0f);
                view.StageCamera.Render();
            }
            RenderToPng(cam, path, 64, 36);
        }
        Canvas.ForceUpdateCanvases();
        foreach (var text in UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
            text.ForceMeshUpdate(true, true);
        RenderToPng(cam, path, 1920, 1080);
        canvas.renderMode = mode;
        Debug.Log("[Preview] Wrote " + path);
    }

    static readonly (string name, Action<IPortraitModel> pose)[] Expressions =
    {
        ("rest", m => { }),
        ("turn_left", m => { m.SetParameter(PortraitParams.AngleX, -28); m.SetParameter(PortraitParams.EyeBallX, -0.8f); }),
        ("turn_right", m => { m.SetParameter(PortraitParams.AngleX, 28); m.SetParameter(PortraitParams.EyeBallX, 0.8f); }),
        ("look_up", m => { m.SetParameter(PortraitParams.AngleY, 25); m.SetParameter(PortraitParams.EyeBallY, 0.8f); }),
        ("blink", m => { m.SetParameter(PortraitParams.EyeLOpen, 0); m.SetParameter(PortraitParams.EyeROpen, 0); }),
        ("talk", m => m.SetParameter(PortraitParams.MouthOpenY, 0.9f)),
        ("happy", m => PortraitDriver.Pose(m, PortraitMood.Happy, false, 0.5f, 7, 0)),
        ("tired", m => PortraitDriver.Pose(m, PortraitMood.Tired, false, 0.5f, 7, 0)),
        ("panic", m => PortraitDriver.Pose(m, PortraitMood.Panic, true, 0.52f, 7, 0)),
    };

    // One row per persona, one column per expression, for checking the puppet rigs.
    static void CapturePortraitSheet(string path)
    {
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { PortraitBuilder.PrefabDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p.GetComponent<IPortraitModel>() != null).OrderBy(p => p.name).ToArray();
        if (prefabs.Length == 0) return;
        const int cell = 256;
        var sheet = new Texture2D(cell * Expressions.Length, cell * prefabs.Length, TextureFormat.RGB24, false);
        var stage = new GameObject("PreviewStage");
        stage.transform.position = new Vector3(0, -2000, 0);
        var cam = new GameObject("PreviewCam").AddComponent<Camera>();
        cam.transform.SetParent(stage.transform, false);
        cam.transform.localPosition = new Vector3(0, 0.08f, -10);
        cam.orthographic = true;
        cam.orthographicSize = 0.8f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.92f, 0.89f, 0.97f);
        cam.cullingMask = 1 << PortraitView.PortraitLayer;
        try
        {
            for (int row = 0; row < prefabs.Length; row++)
            {
                for (int col = 0; col < Expressions.Length; col++)
                {
                    var go = UnityEngine.Object.Instantiate(prefabs[row], stage.transform);
                    go.transform.localPosition = Vector3.zero;
                    foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PortraitView.PortraitLayer;
                    var model = go.GetComponent<IPortraitModel>();
                    Expressions[col].pose(model);
                    go.GetComponent<PortraitPuppet>()?.Apply(0f);
                    var pixels = RenderPixels(cam, cell, cell);
                    sheet.SetPixels(col * cell, (prefabs.Length - 1 - row) * cell, cell, cell, pixels);
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Debug.Log("[Preview] Wrote " + path + " (" + string.Join(", ", Expressions.Select(e => e.name)) + ")");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(stage);
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }

    static Color[] RenderPixels(Camera cam, int width, int height)
    {
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            cam.targetTexture = rt;
            cam.aspect = (float)width / height;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            return tex.GetPixels();
        }
        finally
        {
            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    static void RenderToPng(Camera cam, string path, int width, int height)
    {
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            cam.targetTexture = rt;
            cam.aspect = (float)width / height;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }
        finally
        {
            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    static string GetArg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
