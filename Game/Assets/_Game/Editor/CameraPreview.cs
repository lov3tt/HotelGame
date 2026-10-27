using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders the Inn scene's camera at several zoom levels to PNG files,
// so the 2D → 3D zoom blend can be checked without pressing Play.
// Shows the 3D scene only: screen-space UI (the HUD) isn't part of a camera render.
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
