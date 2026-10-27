using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless checks that themes and character models really swap without code changes.
// Opens Inn.unity, swaps in a throwaway theme and model, checks the results, and never saves.
//
// Editor:   InnKeeper → Run Milestone 2 Checks
// Headless: Unity.exe -batchmode -nographics -quit -projectPath Game -executeMethod Milestone2Checks.RunBatch
public static class Milestone2Checks
{
    static readonly List<string> failures = new List<string>();

    [MenuItem("InnKeeper/Run Milestone 2 Checks")]
    public static bool Run()
    {
        failures.Clear();
        CheckThemeFormatting();
        CheckController();
        CheckCharacterPrefab();

        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Inn.unity");
        CheckHudLayout();
        CheckThemeSwap();
        CheckModelSwap();

        if (failures.Count == 0) Debug.Log("[M2] ALL CHECKS PASSED");
        else Debug.LogError($"[M2] {failures.Count} CHECK(S) FAILED");
        return failures.Count == 0;
    }

    public static void RunBatch()
    {
        if (!Run()) EditorApplication.Exit(1);
    }

    static void Check(bool ok, string what)
    {
        if (ok) Debug.Log("[M2] OK   " + what);
        else
        {
            Debug.LogError("[M2] FAIL " + what);
            failures.Add(what);
        }
    }

    static void CheckThemeFormatting()
    {
        // Every key used in code must have a name in both shipped themes.
        var keys = typeof(ThemeKeys).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue())
            .Where(k => !k.StartsWith("prop.")).ToArray();
        foreach (var path in new[] { ProjectSetup.ThemePath, ProjectSetup.IsekaiThemePath })
        {
            var t = AssetDatabase.LoadAssetAtPath<ThemeProfile>(path);
            var missing = t == null ? keys : keys.Where(k => t.GetName(k) == k).ToArray();
            Check(missing.Length == 0, $"{System.IO.Path.GetFileNameWithoutExtension(path)} names every key" +
                (missing.Length > 0 ? " (missing: " + string.Join(", ", missing) + ")" : ""));
        }
        var isekai = AssetDatabase.LoadAssetAtPath<ThemeProfile>(ProjectSetup.IsekaiThemePath);
        Check(isekai != null && isekai.staffAppearances.Length == 5 && isekai.staffAppearances.All(a => a != null && a.modelPrefab != null),
            "Isekai Stream theme has 5 staff personas with models");

        var theme = AssetDatabase.LoadAssetAtPath<ThemeProfile>(ProjectSetup.ThemePath);
        Check(theme != null, "Theme_Medieval exists");
        if (theme == null) return;
        Check(theme.Format("Hire a {role.porter} at {inn.name}") == "Hire a Porter at The Wayside", "theme fills {tokens} in text");
        Check(theme.GetName("role.unknown") == "role.unknown", "missing names fall back to the key");
        Check(theme.FormatMoney(25) == "25c", "money uses the theme's currency symbol");
    }

    static void CheckController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ProjectSetup.ControllerPath);
        Check(controller != null, "InnCharacter.controller exists");
        if (controller == null) return;
        var parameters = controller.parameters.Select(p => p.name).ToArray();
        Check(new[] { "Speed", "Work", "Carry" }.All(parameters.Contains), "controller has Speed/Work/Carry parameters");
        var states = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
        var expected = System.Enum.GetNames(typeof(WorkAnim)).Where(n => n != "None").Append("Locomotion");
        Check(expected.All(states.Contains), "controller has Locomotion + a state per WorkAnim");
        Check(controller.layers.Length == 2 && controller.layers[1].avatarMask != null, "Carry layer uses an upper-body mask");
    }

    static void CheckCharacterPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectSetup.CharacterPrefabPath);
        Check(prefab != null, "Character prefab exists");
        if (prefab == null) return;
        Check(prefab.GetComponent<CharacterRig>() != null && prefab.GetComponent<AgentAnimator>() != null, "prefab has CharacterRig + AgentAnimator");
        Check(prefab.transform.Find("ModelSlot/Model") == null, "prefab has no model baked in (models come from appearances)");
    }

    // The HUD can't be rendered headless, so check its layout settings instead.
    static void CheckHudLayout()
    {
        var bar = GameObject.Find("HUD/TopBar")?.GetComponent<RectTransform>();
        var innName = GameObject.Find("HUD/TopBar/InnName")?.GetComponent<TMP_Text>();
        var purse = GameObject.Find("HUD/TopBar/Purse")?.GetComponent<TMP_Text>();
        Check(bar != null && innName != null && purse != null, "HUD has a top bar with inn-name and purse labels");
        if (bar == null || innName == null || purse == null) return;

        var canvas = bar.GetComponentInParent<Canvas>();
        Check(canvas.renderMode == RenderMode.ScreenSpaceOverlay, "HUD draws on top of the game (screen-space overlay)");
        Check(bar.anchorMin == new Vector2(0, 1) && bar.anchorMax == new Vector2(1, 1) && bar.sizeDelta.y > 0,
            "top bar spans the full width along the top edge");
        Check(innName.alignment == TextAlignmentOptions.MidlineLeft && purse.alignment == TextAlignmentOptions.MidlineRight,
            "inn name sits left, purse sits right");
        Check(innName.font != null, "HUD text has a font (TextMeshPro essentials imported)");
        string expected = ThemeManager.Current != null ? ThemeManager.Current.Format(GrayboxBuilder.HudPurseTemplate) : "?";
        Check(purse.text == expected && !purse.text.Contains("ui."), $"purse label is themed (shows \"{purse.text}\")");
    }

    static void CheckThemeSwap()
    {
        var manager = Object.FindFirstObjectByType<ThemeManager>();
        var innName = GameObject.Find("HUD/TopBar/InnName")?.GetComponent<TMP_Text>();
        Check(manager != null && innName != null, "scene has a ThemeManager and HUD inn-name label");
        if (manager == null || innName == null) return;

        var original = ThemeManager.Current;
        Check(original != null && innName.text == original.innName, $"HUD shows the active theme's inn name ({innName.text})");

        // The two shipped themes: swap between them for real.
        var medieval = AssetDatabase.LoadAssetAtPath<ThemeProfile>(ProjectSetup.ThemePath);
        manager.SetTheme(medieval);
        Check(innName.text == "The Wayside", "switching to Theme_Medieval shows The Wayside");
        manager.SetTheme(original);
        Check(innName.text == original.innName, "switching back shows " + original.innName);

        // A throwaway theme with different names, colours and a replacement bed.
        var testTheme = Object.Instantiate(original);
        testTheme.innName = "Frontier Saloon";
        var palette = testTheme.palette;
        palette.highlight = Color.red;
        testTheme.palette = palette;
        var fakeBed = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fakeBed.name = "TestBedArt";
        testTheme.props = new List<ThemeProfile.PropEntry> { new ThemeProfile.PropEntry { key = ThemeKeys.PropBed, prefab = fakeBed } };

        var beds = Object.FindObjectsByType<ThemedProp>(FindObjectsSortMode.None).Where(p => p.PropKey == ThemeKeys.PropBed).ToArray();
        Check(beds.Length == 6, $"scene has 6 single beds tagged for theming (found {beds.Length})");

        manager.SetTheme(testTheme);
        Check(innName.text == "Frontier Saloon", "swapping theme renames the inn on the HUD");
        Check(innName.color == Color.red, "swapping theme recolours the HUD");
        Check(beds.All(b => b.Spawned != null && !b.GetComponent<Renderer>().enabled), "theme prop replaces every graybox bed");
        var desk = Object.FindObjectsByType<ThemedProp>(FindObjectsSortMode.None).First(p => p.PropKey == ThemeKeys.PropFrontDesk);
        Check(desk.Spawned == null && desk.GetComponent<Renderer>().enabled, "props the theme doesn't replace keep their graybox block");

        manager.SetTheme(original);
        Check(innName.text == original.innName && beds.All(b => b.Spawned == null && b.GetComponent<Renderer>().enabled),
            "switching back restores names and graybox beds");

        Object.DestroyImmediate(fakeBed);
        Object.DestroyImmediate(testTheme);
    }

    static void CheckModelSwap()
    {
        var rigs = Object.FindObjectsByType<CharacterRig>(FindObjectsSortMode.None);
        Check(rigs.Length == 7, $"scene has 7 demo characters (found {rigs.Length})");
        var clerk = rigs.FirstOrDefault(r => r.name == "Clerk");
        if (clerk == null) { Check(false, "Clerk demo character exists"); return; }

        Check(clerk.Model != null && clerk.Model.transform.Find("Ear_Elf_L") != null, "Clerk shows the Lumi persona (elf ears)");

        var guestLook = AssetDatabase.LoadAssetAtPath<CharacterAppearance>(ProjectSetup.GuestAppearancePath);
        var staffLook = clerk.Appearance;
        var firstModel = clerk.Model;
        clerk.SetAppearance(guestLook);
        Check(firstModel == null && clerk.Model != null && clerk.Model.transform.Find("Hat_Top") != null,
            "swapping appearance replaces the model (old one removed, guest hat shown)");
        Check(clerk.Model.GetComponentsInChildren<Collider>().Length == 0, "spawned models have no colliders");

        // A brand-new appearance with tint and scale, made in code like a designer would in the Inspector.
        var tall = ScriptableObject.CreateInstance<CharacterAppearance>();
        tall.modelPrefab = staffLook.modelPrefab;
        tall.heightScale = 1.2f;
        clerk.SetAppearance(tall);
        Check(Mathf.Approximately(clerk.Model.transform.localScale.y, 1.2f), "appearance height scale is applied");

        clerk.SetAppearance(null);
        Check(clerk.Model != null && clerk.Model.transform.Find("Body") != null, "no appearance falls back to a capsule");

        clerk.SetAppearance(staffLook);
        Object.DestroyImmediate(tall);
    }
}
