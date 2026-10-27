using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Creates (or refreshes) the assets the theme and character systems need:
//   - TextMeshPro essential resources
//   - Animation/InnCharacter.controller: shared Humanoid controller, with clips auto-filled
//     from Animation/Clips by file name (Idle.fbx, Walk.fbx, Serve.fbx, ...)
//   - Placeholder mannequin models + their CharacterAppearance assets, including the five
//     streamer staff personas of the Isekai Stream theme
//   - Data/Themes/Theme_IsekaiStream.asset (active) and Theme_Medieval.asset
//   - Prefabs/Characters/Character.prefab
//
// Assets you can edit (themes, appearances, prefabs, the controller's layout) are only created
// when missing, so your edits are never overwritten. Clip assignment re-runs every time, and
// name keys added in newer versions are filled into existing themes (existing names are kept).
//
// Editor: InnKeeper → Setup Theme & Characters (also runs as part of Rebuild Graybox Inn)
public static class ProjectSetup
{
    public const string AnimationDir = "Assets/_Game/Animation";
    public const string ClipsDir = AnimationDir + "/Clips";
    public const string ModelsDir = "Assets/_Game/Art/Characters";
    public const string ControllerPath = AnimationDir + "/InnCharacter.controller";

    const string PlaceholderDir = ModelsDir + "/Placeholders";
    const string ThemesDir = "Assets/_Game/Data/Themes";
    const string AppearancesDir = "Assets/_Game/Data/Appearances";
    const string PrefabsDir = "Assets/_Game/Prefabs/Characters";
    const string TmpPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
    const string TmpSettings = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    public const string ThemePath = ThemesDir + "/Theme_Medieval.asset";
    public const string IsekaiThemePath = ThemesDir + "/Theme_IsekaiStream.asset";
    public const string StaffAppearancePath = AppearancesDir + "/App_Staff_Placeholder.asset";
    public const string GuestAppearancePath = AppearancesDir + "/App_Guest_Placeholder.asset";
    public const string CharacterPrefabPath = PrefabsDir + "/Character.prefab";

    // Base-layer states besides Locomotion, in WorkAnim order (state name = enum name).
    static readonly WorkAnim[] WorkStates = System.Enum.GetValues(typeof(WorkAnim)).Cast<WorkAnim>()
        .Where(w => w != WorkAnim.None).ToArray();

    // Clips that should loop when imported from Animation/Clips.
    public static readonly HashSet<string> LoopingClips = new HashSet<string>
        { "idle", "walk", "carry", "cook", "clean", "stoke", "sit", "sleep", "pour", "pourbath" };

    public struct Assets
    {
        public ThemeProfile theme;          // the active theme for the scene (Isekai Stream)
        public ThemeProfile medievalTheme;
        public CharacterAppearance staffAppearance;
        public CharacterAppearance guestAppearance;
        public Dictionary<string, CharacterAppearance> personaByRole; // role key -> persona look
        public GameObject characterPrefab;
    }

    // Accessories a placeholder mannequin can wear.
    [System.Flags]
    enum Look
    {
        None = 0, Apron = 1, TopHat = 2, CatEars = 4, CatTail = 8, ElfEars = 16,
        Horns = 32, DragonTail = 64, Visor = 128, MaidHeadband = 256, ChefHat = 512,
    }

    // The Isekai Stream staff: original streamer personas, one per role.
    // Primitive stand-ins until Live2D / real character art replaces them.
    static readonly (string role, string id, string displayName, Color cloth, Color hair, Look look, float height)[] Personas =
    {
        (ThemeKeys.RoleClerk, "Lumi", "Lumi, Elf Receptionist",
            new Color(0.50f, 0.30f, 0.75f), new Color(0.98f, 0.88f, 0.50f), Look.ElfEars, 1.0f),
        (ThemeKeys.RolePorter, "Ember", "Ember, Dragon Porter",
            new Color(0.60f, 0.15f, 0.15f), new Color(0.95f, 0.35f, 0.20f), Look.Horns | Look.DragonTail, 1.08f),
        (ThemeKeys.RoleServer, "Mochi", "Mochi, Catgirl Server",
            new Color(0.98f, 0.60f, 0.75f), new Color(0.80f, 0.65f, 1.00f), Look.CatEars | Look.CatTail | Look.Apron, 0.95f),
        (ThemeKeys.RoleChambermaid, "Unit07", "Unit-07, Android Maid",
            new Color(0.12f, 0.12f, 0.15f), new Color(0.75f, 0.95f, 1.00f), Look.Visor | Look.MaidHeadband | Look.Apron, 1.0f),
        (ThemeKeys.RoleCook, "Gigi", "Gigi, Gremlin Chef",
            new Color(1.00f, 0.55f, 0.15f), new Color(0.40f, 0.85f, 0.35f), Look.ChefHat | Look.Apron, 0.8f),
    };

    [MenuItem("InnKeeper/Setup Theme & Characters")]
    public static void SetupMenu() => EnsureAssets();

    public static Assets EnsureAssets()
    {
        foreach (var dir in new[] { ClipsDir, PlaceholderDir + "/Materials", ThemesDir, AppearancesDir, PrefabsDir })
            EnsureFolder(dir);

        ImportTmpEssentials();
        var controller = EnsureController();
        AssignClips(controller);

        var staffModel = EnsureMannequin("Mannequin_Staff", new Color(0.30f, 0.45f, 0.25f), null, Look.Apron);
        var guestModel = EnsureMannequin("Mannequin_Guest", new Color(0.25f, 0.35f, 0.60f), null, Look.TopHat);
        var staffApp = EnsureAppearance(StaffAppearancePath, "Placeholder Staff", staffModel);
        var guestApp = EnsureAppearance(GuestAppearancePath, "Placeholder Guest", guestModel);

        var personaByRole = new Dictionary<string, CharacterAppearance>();
        foreach (var p in Personas)
        {
            var model = EnsureMannequin("Persona_" + p.id, p.cloth, p.hair, p.look);
            personaByRole[p.role] = EnsureAppearance(AppearancesDir + "/App_Persona_" + p.id + ".asset", p.displayName, model, p.height);
        }

        var medieval = EnsureTheme(ThemePath, theme => ConfigureMedieval(theme, staffApp, guestApp));
        BackfillNames(medieval, MedievalNames);
        var isekai = EnsureTheme(IsekaiThemePath, theme => ConfigureIsekai(theme, personaByRole.Values.ToArray(), guestApp));
        BackfillNames(isekai, IsekaiNames);
        var prefab = EnsureCharacterPrefab(controller);

        AssetDatabase.SaveAssets();
        return new Assets
        {
            theme = isekai, medievalTheme = medieval,
            staffAppearance = staffApp, guestAppearance = guestApp,
            personaByRole = personaByRole, characterPrefab = prefab,
        };
    }

    // ---------- TextMeshPro ----------

    // AssetDatabase.ImportPackage is asynchronous and never finishes in batch mode, so unpack the
    // .unitypackage ourselves. It's a gzipped tar with one folder per asset GUID, each holding
    // "asset", "asset.meta" and "pathname". Keeping the .meta files keeps Unity's GUIDs, so
    // anything that references the default font still finds it.
    static void ImportTmpEssentials()
    {
        if (File.Exists(TmpSettings)) return;

        var ugui = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
        string packageFile = ugui != null
            ? Path.Combine(ugui.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage")
            : Path.GetFullPath(TmpPackage);

        var entries = new Dictionary<string, Dictionary<string, byte[]>>();
        using (var file = File.OpenRead(packageFile))
        using (var gzip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Decompress))
        {
            foreach (var (name, data) in ReadTar(gzip))
            {
                int slash = name.IndexOf('/');
                if (slash <= 0 || slash == name.Length - 1) continue;
                string guid = name.Substring(0, slash);
                if (!entries.TryGetValue(guid, out var parts)) entries[guid] = parts = new Dictionary<string, byte[]>();
                parts[name.Substring(slash + 1)] = data;
            }
        }

        int written = 0;
        foreach (var parts in entries.Values)
        {
            if (!parts.TryGetValue("pathname", out var pathBytes)) continue;
            string assetPath = System.Text.Encoding.UTF8.GetString(pathBytes).Split('\n')[0].Trim();
            if (!assetPath.StartsWith("Assets/")) continue;
            if (parts.TryGetValue("asset", out var asset))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                File.WriteAllBytes(assetPath, asset);
                written++;
            }
            else Directory.CreateDirectory(assetPath); // a folder entry
            if (parts.TryGetValue("asset.meta", out var meta)) File.WriteAllBytes(assetPath + ".meta", meta);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log(File.Exists(TmpSettings)
            ? $"[Setup] Imported TextMeshPro essentials ({written} files)."
            : "[Setup] TextMeshPro essentials missing. Use Window → TextMeshPro → Import TMP Essential Resources.");
    }

    // Minimal tar reader: yields (path, contents) for each regular file.
    static IEnumerable<(string name, byte[] data)> ReadTar(Stream stream)
    {
        var header = new byte[512];
        while (ReadFully(stream, header, 512))
        {
            if (header.All(b => b == 0)) yield break; // end-of-archive marker
            string name = System.Text.Encoding.ASCII.GetString(header, 0, 100).TrimEnd('\0');
            string prefix = System.Text.Encoding.ASCII.GetString(header, 345, 155).TrimEnd('\0');
            if (prefix.Length > 0) name = prefix + "/" + name;
            string sizeText = System.Text.Encoding.ASCII.GetString(header, 124, 12).Trim('\0', ' ');
            long size = sizeText.Length > 0 ? System.Convert.ToInt64(sizeText, 8) : 0;
            char type = (char)header[156];

            var data = new byte[size];
            if (!ReadFully(stream, data, (int)size)) yield break;
            int padding = (int)((512 - size % 512) % 512);
            if (padding > 0 && !ReadFully(stream, new byte[padding], padding)) yield break;

            if (type == '0' || type == '\0')
            {
                if (name.StartsWith("./")) name = name.Substring(2);
                yield return (name, data);
            }
        }
    }

    static bool ReadFully(Stream stream, byte[] buffer, int count)
    {
        int read = 0;
        while (read < count)
        {
            int n = stream.Read(buffer, read, count - read);
            if (n <= 0) return false;
            read += n;
        }
        return true;
    }

    // ---------- Animator controller ----------

    static AnimatorController EnsureController()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing != null) return existing;

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Work", AnimatorControllerParameterType.Int);
        controller.AddParameter("Carry", AnimatorControllerParameterType.Bool);

        // Base layer: Idle/Walk blend driven by Speed, plus one state per work animation.
        var sm = controller.layers[0].stateMachine;
        var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(null, 0f);   // Idle
        tree.AddChild(null, 1.5f); // Walk (typical NavMeshAgent walking speed)
        sm.defaultState = locomotion;

        for (int i = 0; i < WorkStates.Length; i++)
        {
            var work = WorkStates[i];
            var state = sm.AddState(work.ToString(), new Vector3(500f, i * 60f, 0f));

            var enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.Equals, (int)work, "Work");
            enter.hasExitTime = false;
            enter.duration = 0.15f;
            enter.canTransitionToSelf = false;

            var exit = state.AddTransition(locomotion);
            exit.AddCondition(AnimatorConditionMode.Equals, 0, "Work");
            exit.hasExitTime = false;
            exit.duration = 0.15f;
        }

        // Carry layer: arms-only overlay so characters can walk while holding a tray or buckets.
        var mask = new AvatarMask { name = "UpperBody" };
        var armParts = new[]
        {
            AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
            AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers,
            AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK,
        };
        for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
            mask.SetHumanoidBodyPartActive(part, armParts.Contains(part));
        AssetDatabase.CreateAsset(mask, AnimationDir + "/UpperBody.mask");

        controller.AddLayer("Carry");
        var layers = controller.layers;
        layers[1].defaultWeight = 1f;
        layers[1].avatarMask = mask;
        controller.layers = layers;

        var carrySm = controller.layers[1].stateMachine;
        var empty = carrySm.AddState("Empty");
        empty.writeDefaultValues = false; // an empty override state must not reset the arms
        var carry = carrySm.AddState("Carry");
        carrySm.defaultState = empty;
        var startCarry = empty.AddTransition(carry);
        startCarry.AddCondition(AnimatorConditionMode.If, 0, "Carry");
        startCarry.hasExitTime = false;
        startCarry.duration = 0.2f;
        var stopCarry = carry.AddTransition(empty);
        stopCarry.AddCondition(AnimatorConditionMode.IfNot, 0, "Carry");
        stopCarry.hasExitTime = false;
        stopCarry.duration = 0.2f;

        Debug.Log("[Setup] Created " + ControllerPath);
        return controller;
    }

    // Fills controller states with clips from Animation/Clips, matched by file or clip name
    // ignoring case and punctuation: "Pour Bath.fbx", "pour_bath.anim" and "PourBath" all match PourBath.
    static void AssignClips(AnimatorController controller)
    {
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipsDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var inFile = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            if (inFile.Length == 0) continue;
            // Mixamo names every clip "mixamo.com", so the file name is the reliable match.
            clips.TryAdd(Normalize(Path.GetFileNameWithoutExtension(path)), inFile[0]);
            foreach (var c in inFile) clips.TryAdd(Normalize(c.name), c);
        }

        var assigned = new List<string>();
        var missing = new List<string>();
        void Fill(string stateName, System.Action<AnimationClip> set)
        {
            if (clips.TryGetValue(Normalize(stateName), out var clip)) { set(clip); assigned.Add(stateName); }
            else { set(null); missing.Add(stateName); }
        }

        foreach (var layer in controller.layers)
        {
            foreach (var child in layer.stateMachine.states)
            {
                var state = child.state;
                if (state.motion is BlendTree tree && state.name == "Locomotion")
                {
                    var children = tree.children;
                    if (children.Length >= 2)
                    {
                        Fill("Idle", c => children[0].motion = c);
                        Fill("Walk", c => children[1].motion = c);
                        tree.children = children;
                    }
                }
                else if (state.name != "Empty")
                {
                    Fill(state.name, c => state.motion = c);
                }
            }
        }

        EditorUtility.SetDirty(controller);
        Debug.Log($"[Setup] Animation clips: {assigned.Count} assigned ({string.Join(", ", assigned)}); " +
                  $"{missing.Count} missing ({string.Join(", ", missing)}). Drop clips into {ClipsDir} and re-run.");
    }

    public static string Normalize(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    // ---------- Placeholder models ----------

    // A simple person built from primitives, so swapping models can be tested before real art exists.
    // Faces +Z. No rig, so it doesn't animate: real Humanoid models (or Live2D) replace these.
    static GameObject EnsureMannequin(string name, Color clothColor, Color? hairColor, Look look)
    {
        string path = PlaceholderDir + "/" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var skin = EnsureMaterial("M_Char_Skin", new Color(0.93f, 0.76f, 0.62f));
        var cloth = EnsureMaterial("M_" + name + "_Cloth", clothColor);
        var trousers = EnsureMaterial("M_Char_Trousers", new Color(0.30f, 0.24f, 0.20f));
        var white = EnsureMaterial("M_Char_Apron", new Color(0.92f, 0.90f, 0.85f));
        var hair = hairColor.HasValue ? EnsureMaterial("M_" + name + "_Hair", hairColor.Value) : null;

        var root = new GameObject(name);
        Part(root, PrimitiveType.Cylinder, "Leg_L", new Vector3(-0.1f, 0.4f, 0f), new Vector3(0.14f, 0.4f, 0.14f), trousers);
        Part(root, PrimitiveType.Cylinder, "Leg_R", new Vector3(0.1f, 0.4f, 0f), new Vector3(0.14f, 0.4f, 0.14f), trousers);
        Part(root, PrimitiveType.Capsule, "Torso", new Vector3(0f, 1.16f, 0f), new Vector3(0.42f, 0.36f, 0.28f), cloth);
        Part(root, PrimitiveType.Capsule, "Arm_L", new Vector3(-0.27f, 1.15f, 0f), new Vector3(0.11f, 0.3f, 0.11f), cloth);
        Part(root, PrimitiveType.Capsule, "Arm_R", new Vector3(0.27f, 1.15f, 0f), new Vector3(0.11f, 0.3f, 0.11f), cloth);
        Part(root, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.68f, 0f), Vector3.one * 0.26f, skin);
        Part(root, PrimitiveType.Cube, "Nose", new Vector3(0f, 1.68f, 0.13f), Vector3.one * 0.05f, skin); // shows facing

        if (hair != null)
        {
            // A cap over the top of the head plus long hair down the back.
            Part(root, PrimitiveType.Sphere, "Hair_Top", new Vector3(0f, 1.75f, -0.01f), new Vector3(0.29f, 0.2f, 0.29f), hair);
            Part(root, PrimitiveType.Cube, "Hair_Back", new Vector3(0f, 1.55f, -0.1f), new Vector3(0.28f, 0.42f, 0.08f), hair);
        }

        if (look.HasFlag(Look.Apron))
            Part(root, PrimitiveType.Cube, "Apron", new Vector3(0f, 0.95f, 0.15f), new Vector3(0.34f, 0.5f, 0.03f), white);
        if (look.HasFlag(Look.TopHat))
        {
            var hat = EnsureMaterial("M_Char_Hat", new Color(0.35f, 0.22f, 0.12f));
            Part(root, PrimitiveType.Cylinder, "Hat_Brim", new Vector3(0f, 1.83f, 0f), new Vector3(0.38f, 0.01f, 0.38f), hat);
            Part(root, PrimitiveType.Cylinder, "Hat_Top", new Vector3(0f, 1.92f, 0f), new Vector3(0.22f, 0.09f, 0.22f), hat);
        }
        if (look.HasFlag(Look.CatEars))
        {
            Part(root, PrimitiveType.Cube, "Ear_Cat_L", new Vector3(-0.09f, 1.85f, 0f), new Vector3(0.08f, 0.1f, 0.04f), hair, new Vector3(0, 0, 25f));
            Part(root, PrimitiveType.Cube, "Ear_Cat_R", new Vector3(0.09f, 1.85f, 0f), new Vector3(0.08f, 0.1f, 0.04f), hair, new Vector3(0, 0, -25f));
        }
        if (look.HasFlag(Look.CatTail))
            Part(root, PrimitiveType.Capsule, "Tail_Cat", new Vector3(0f, 0.8f, -0.25f), new Vector3(0.05f, 0.25f, 0.05f), hair, new Vector3(-40f, 0, 0));
        if (look.HasFlag(Look.ElfEars))
        {
            Part(root, PrimitiveType.Cube, "Ear_Elf_L", new Vector3(-0.16f, 1.71f, 0f), new Vector3(0.13f, 0.03f, 0.03f), skin, new Vector3(0, 0, -25f));
            Part(root, PrimitiveType.Cube, "Ear_Elf_R", new Vector3(0.16f, 1.71f, 0f), new Vector3(0.13f, 0.03f, 0.03f), skin, new Vector3(0, 0, 25f));
        }
        if (look.HasFlag(Look.Horns))
        {
            var bone = EnsureMaterial("M_Char_Horn", new Color(0.95f, 0.90f, 0.75f));
            Part(root, PrimitiveType.Cylinder, "Horn_L", new Vector3(-0.08f, 1.87f, 0f), new Vector3(0.04f, 0.08f, 0.04f), bone, new Vector3(0, 0, 25f));
            Part(root, PrimitiveType.Cylinder, "Horn_R", new Vector3(0.08f, 1.87f, 0f), new Vector3(0.04f, 0.08f, 0.04f), bone, new Vector3(0, 0, -25f));
        }
        if (look.HasFlag(Look.DragonTail))
            Part(root, PrimitiveType.Capsule, "Tail_Dragon", new Vector3(0f, 0.5f, -0.3f), new Vector3(0.13f, 0.35f, 0.13f), cloth, new Vector3(-60f, 0, 0));
        if (look.HasFlag(Look.Visor))
        {
            var glow = EnsureMaterial("M_Char_Visor", new Color(0.30f, 0.95f, 1.00f));
            Part(root, PrimitiveType.Cube, "Visor", new Vector3(0f, 1.71f, 0.12f), new Vector3(0.25f, 0.05f, 0.04f), glow);
        }
        if (look.HasFlag(Look.MaidHeadband))
            Part(root, PrimitiveType.Cube, "Headband", new Vector3(0f, 1.84f, 0.03f), new Vector3(0.27f, 0.04f, 0.07f), white);
        if (look.HasFlag(Look.ChefHat))
        {
            Part(root, PrimitiveType.Cylinder, "ChefHat_Band", new Vector3(0f, 1.86f, 0f), new Vector3(0.22f, 0.06f, 0.22f), white);
            Part(root, PrimitiveType.Sphere, "ChefHat_Puff", new Vector3(0f, 1.98f, 0f), new Vector3(0.3f, 0.17f, 0.3f), white);
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void Part(GameObject root, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Material EnsureMaterial(string name, Color color)
    {
        string path = PlaceholderDir + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // ---------- Data assets ----------

    static CharacterAppearance EnsureAppearance(string path, string displayName, GameObject model, float height = 1f)
    {
        var app = AssetDatabase.LoadAssetAtPath<CharacterAppearance>(path);
        if (app != null) return app;
        app = ScriptableObject.CreateInstance<CharacterAppearance>();
        app.displayName = displayName;
        app.modelPrefab = model;
        app.heightScale = height;
        AssetDatabase.CreateAsset(app, path);
        return app;
    }

    static ThemeProfile EnsureTheme(string path, System.Action<ThemeProfile> configure)
    {
        var theme = AssetDatabase.LoadAssetAtPath<ThemeProfile>(path);
        if (theme != null) return theme;
        theme = ScriptableObject.CreateInstance<ThemeProfile>();
        configure(theme);
        AssetDatabase.CreateAsset(theme, path);
        Debug.Log("[Setup] Created " + path);
        return theme;
    }

    // Adds any name keys the theme doesn't have yet. Never changes names that are already there.
    static void BackfillNames(ThemeProfile theme, (string key, string name)[] defaults)
    {
        var have = new HashSet<string>(theme.names.Select(n => n.key));
        var added = defaults.Where(d => !have.Contains(d.key)).ToArray();
        if (added.Length == 0) return;
        foreach (var d in added) theme.names.Add(N(d.key, d.name));
        EditorUtility.SetDirty(theme);
        Debug.Log($"[Setup] Added {added.Length} new name(s) to {theme.name}: {string.Join(", ", added.Select(a => a.key))}");
    }

    static void ConfigureMedieval(ThemeProfile theme, CharacterAppearance staff, CharacterAppearance guest)
    {
        theme.innName = "The Wayside";
        theme.currencyName = "Coin";
        theme.currencySymbol = "c";
        theme.names = MedievalNames.Select(n => N(n.key, n.name)).ToList();
        theme.firstNames = new[]
        {
            "Marta", "Edric", "Agnes", "Osric", "Matilda", "Wulfric", "Beatrix", "Godwin", "Isolde", "Aldous",
            "Rowena", "Hugh", "Elspeth", "Tobias", "Maud", "Cedric", "Joan", "Alaric", "Gwen", "Piers",
        };
        theme.palette = ThemePalette.Default;
        theme.props = new List<ThemeProfile.PropEntry>(); // empty = keep the graybox blocks
        theme.staffAppearances = new[] { staff };
        theme.guestAppearances = GuestPools(guest);
    }

    // Medieval fantasy locals check in; the inn is run by streamer-persona staff who woke up in this world.
    static void ConfigureIsekai(ThemeProfile theme, CharacterAppearance[] personas, CharacterAppearance guest)
    {
        theme.innName = "The Second Life Inn";
        theme.currencyName = "Gold";
        theme.currencySymbol = "g";
        theme.names = IsekaiNames.Select(n => N(n.key, n.name)).ToList();
        // Original names for hiring-board candidates (not real streamers).
        theme.firstNames = new[]
        {
            "Mochi", "Lumi", "Ember", "Gigi", "Yuzu", "Pipi", "Nova", "Kiko", "Momo", "Zuzu",
            "Coco", "Fable", "Tansy", "Bun", "Rhea", "Puddle", "Sora", "Wisp", "Lark", "Taffy",
        };
        theme.palette = new ThemePalette
        {
            panel = new Color(0.18f, 0.10f, 0.28f, 0.92f),
            panelAccent = new Color(0.45f, 0.20f, 0.60f, 1f),
            text = new Color(0.98f, 0.95f, 1.00f),
            textMuted = new Color(0.80f, 0.72f, 0.90f),
            highlight = new Color(1.00f, 0.45f, 0.75f),
            good = new Color(0.40f, 0.95f, 0.70f),
            warning = new Color(1.00f, 0.85f, 0.35f),
            bad = new Color(1.00f, 0.35f, 0.45f),
        };
        theme.backgroundColor = new Color(0.62f, 0.56f, 0.82f); // twilight lilac
        theme.sunColor = new Color(1.00f, 0.88f, 0.90f);
        theme.sunIntensity = 1.15f;
        theme.ambientColor = new Color(0.50f, 0.45f, 0.60f);
        theme.props = new List<ThemeProfile.PropEntry>();
        theme.staffAppearances = personas;
        theme.guestAppearances = GuestPools(guest);
    }

    static List<ThemeProfile.AppearancePool> GuestPools(CharacterAppearance guest) => new List<ThemeProfile.AppearancePool>
    {
        new ThemeProfile.AppearancePool { guestType = ThemeKeys.GuestPilgrim, appearances = new[] { guest } },
        new ThemeProfile.AppearancePool { guestType = ThemeKeys.GuestTraveller, appearances = new[] { guest } },
        new ThemeProfile.AppearancePool { guestType = ThemeKeys.GuestMerchant, appearances = new[] { guest } },
        new ThemeProfile.AppearancePool { guestType = ThemeKeys.GuestNoble, appearances = new[] { guest } },
    };

    public static readonly (string key, string name)[] MedievalNames =
    {
        (ThemeKeys.RoleClerk, "Clerk"), (ThemeKeys.RolePorter, "Porter"), (ThemeKeys.RoleServer, "Server"),
        (ThemeKeys.RoleChambermaid, "Chambermaid"), (ThemeKeys.RoleCook, "Cook"),
        (ThemeKeys.GuestPilgrim, "Pilgrim"), (ThemeKeys.GuestTraveller, "Traveller"),
        (ThemeKeys.GuestMerchant, "Merchant"), (ThemeKeys.GuestNoble, "Noble"),
        (ThemeKeys.RoomBunk, "Bunk Room"), (ThemeKeys.RoomCommon, "Common Room"),
        (ThemeKeys.RoomPrivate, "Private Chamber"), (ThemeKeys.RoomSuite, "Noble Suite"),
        (ThemeKeys.ServiceMeal, "Supper"), (ThemeKeys.ServiceDrink, "Ale"), (ThemeKeys.ServiceFirewood, "Firewood"),
        (ThemeKeys.ServiceBath, "Hot Bath"), (ThemeKeys.ServiceTurndown, "Turndown"),
        (ThemeKeys.ServiceBootPolish, "Boot Polish"), (ThemeKeys.ServiceWakeUp, "Wake-up Call"),
        (ThemeKeys.ServiceLuggage, "Luggage"),
        (ThemeKeys.TierD, "Apprentice"), (ThemeKeys.TierC, "Hand"), (ThemeKeys.TierB, "Journeyman"),
        (ThemeKeys.TierA, "Skilled"), (ThemeKeys.TierS, "Master"),
        (ThemeKeys.PlaceFrontDesk, "Front Desk"), (ThemeKeys.PlaceKitchen, "Kitchen"),
        (ThemeKeys.PlaceLaundry, "Laundry"), (ThemeKeys.PlaceRestArea, "Servants' Room"),
        (ThemeKeys.PlaceYard, "Yard"),
        (ThemeKeys.UiPurse, "Purse"), (ThemeKeys.UiReputation, "Reputation"), (ThemeKeys.UiTip, "Tip"),
    };

    public static readonly (string key, string name)[] IsekaiNames =
    {
        (ThemeKeys.RoleClerk, "Receptionist"), (ThemeKeys.RolePorter, "Porter"), (ThemeKeys.RoleServer, "Server"),
        (ThemeKeys.RoleChambermaid, "Maid"), (ThemeKeys.RoleCook, "Chef"),
        (ThemeKeys.GuestPilgrim, "Pilgrim"), (ThemeKeys.GuestTraveller, "Adventurer"),
        (ThemeKeys.GuestMerchant, "Merchant"), (ThemeKeys.GuestNoble, "Noble"),
        (ThemeKeys.RoomBunk, "Party Bunks"), (ThemeKeys.RoomCommon, "Cozy Room"),
        (ThemeKeys.RoomPrivate, "Private Room"), (ThemeKeys.RoomSuite, "Royal Suite"),
        (ThemeKeys.ServiceMeal, "Hearty Stew"), (ThemeKeys.ServiceDrink, "Fizzy Mead"), (ThemeKeys.ServiceFirewood, "Firewood"),
        (ThemeKeys.ServiceBath, "Hot Spring Bath"), (ThemeKeys.ServiceTurndown, "Pillow Fluffing"),
        (ThemeKeys.ServiceBootPolish, "Boot Shine"), (ThemeKeys.ServiceWakeUp, "Morning Call"),
        (ThemeKeys.ServiceLuggage, "Inventory Carry"),
        (ThemeKeys.TierD, "Trainee"), (ThemeKeys.TierC, "Debuted"), (ThemeKeys.TierB, "Partner"),
        (ThemeKeys.TierA, "Top Streamer"), (ThemeKeys.TierS, "Legend"),
        (ThemeKeys.PlaceFrontDesk, "Reception"), (ThemeKeys.PlaceKitchen, "Kitchen"),
        (ThemeKeys.PlaceLaundry, "Laundry"), (ThemeKeys.PlaceRestArea, "Green Room"),
        (ThemeKeys.PlaceYard, "Yard"),
        (ThemeKeys.UiPurse, "Gold"), (ThemeKeys.UiReputation, "Followers"), (ThemeKeys.UiTip, "Donation"),
    };

    static ThemeProfile.NameEntry N(string key, string value) => new ThemeProfile.NameEntry { key = key, displayName = value };

    static GameObject EnsureCharacterPrefab(RuntimeAnimatorController controller)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
        if (existing != null) return existing;

        // CharacterRig spawns a preview model as soon as it's added, but previews are DontSave,
        // so they're left out of the saved prefab (the checks verify this).
        var root = new GameObject("Character");
        var slot = new GameObject("ModelSlot").transform;
        slot.SetParent(root.transform, false);
        var rig = root.AddComponent<CharacterRig>();
        root.AddComponent<AgentAnimator>();

        var so = new SerializedObject(rig);
        so.FindProperty("modelSlot").objectReferenceValue = slot;
        so.FindProperty("animatorController").objectReferenceValue = controller;
        so.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, CharacterPrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("[Setup] Created " + CharacterPrefabPath);
        return prefab;
    }

    static void EnsureFolder(string path)
    {
        string current = "Assets";
        foreach (var part in path.Substring("Assets/".Length).Split('/'))
        {
            string next = current + "/" + part;
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
            current = next;
        }
    }
}
