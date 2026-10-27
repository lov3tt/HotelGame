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
//   - Placeholder mannequin models + their CharacterAppearance assets
//   - Data/Themes/Theme_Medieval.asset
//   - Prefabs/Characters/Character.prefab
//
// Assets you can edit (themes, appearances, prefabs, the controller's layout) are only created
// when missing, so your edits are never overwritten. Clip assignment re-runs every time.
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
        public ThemeProfile theme;
        public CharacterAppearance staffAppearance;
        public CharacterAppearance guestAppearance;
        public GameObject characterPrefab;
    }

    [MenuItem("InnKeeper/Setup Theme & Characters")]
    public static void SetupMenu() => EnsureAssets();

    public static Assets EnsureAssets()
    {
        foreach (var dir in new[] { ClipsDir, PlaceholderDir + "/Materials", ThemesDir, AppearancesDir, PrefabsDir })
            EnsureFolder(dir);

        ImportTmpEssentials();
        var controller = EnsureController();
        AssignClips(controller);

        var staffModel = EnsureMannequin("Mannequin_Staff", new Color(0.30f, 0.45f, 0.25f), apron: true, hat: false);
        var guestModel = EnsureMannequin("Mannequin_Guest", new Color(0.25f, 0.35f, 0.60f), apron: false, hat: true);
        var staffApp = EnsureAppearance(StaffAppearancePath, "Placeholder Staff", staffModel);
        var guestApp = EnsureAppearance(GuestAppearancePath, "Placeholder Guest", guestModel);
        var theme = EnsureTheme(staffApp, guestApp);
        var prefab = EnsureCharacterPrefab(controller);

        AssetDatabase.SaveAssets();
        return new Assets { theme = theme, staffAppearance = staffApp, guestAppearance = guestApp, characterPrefab = prefab };
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
    // Faces +Z. No rig, so it doesn't animate: real Humanoid models replace these.
    static GameObject EnsureMannequin(string name, Color clothColor, bool apron, bool hat)
    {
        string path = PlaceholderDir + "/" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var skin = EnsureMaterial("M_Char_Skin", new Color(0.93f, 0.76f, 0.62f));
        var cloth = EnsureMaterial("M_" + name + "_Cloth", clothColor);
        var trousers = EnsureMaterial("M_Char_Trousers", new Color(0.30f, 0.24f, 0.20f));
        var accent = EnsureMaterial(apron ? "M_Char_Apron" : "M_Char_Hat",
            apron ? new Color(0.92f, 0.90f, 0.85f) : new Color(0.35f, 0.22f, 0.12f));

        var root = new GameObject(name);
        Part(root, PrimitiveType.Cylinder, "Leg_L", new Vector3(-0.1f, 0.4f, 0f), new Vector3(0.14f, 0.4f, 0.14f), trousers);
        Part(root, PrimitiveType.Cylinder, "Leg_R", new Vector3(0.1f, 0.4f, 0f), new Vector3(0.14f, 0.4f, 0.14f), trousers);
        Part(root, PrimitiveType.Capsule, "Torso", new Vector3(0f, 1.16f, 0f), new Vector3(0.42f, 0.36f, 0.28f), cloth);
        Part(root, PrimitiveType.Capsule, "Arm_L", new Vector3(-0.27f, 1.15f, 0f), new Vector3(0.11f, 0.3f, 0.11f), cloth);
        Part(root, PrimitiveType.Capsule, "Arm_R", new Vector3(0.27f, 1.15f, 0f), new Vector3(0.11f, 0.3f, 0.11f), cloth);
        Part(root, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.68f, 0f), Vector3.one * 0.26f, skin);
        Part(root, PrimitiveType.Cube, "Nose", new Vector3(0f, 1.68f, 0.13f), Vector3.one * 0.05f, skin); // shows facing
        if (apron)
            Part(root, PrimitiveType.Cube, "Apron", new Vector3(0f, 0.95f, 0.15f), new Vector3(0.34f, 0.5f, 0.03f), accent);
        if (hat)
        {
            Part(root, PrimitiveType.Cylinder, "Hat_Brim", new Vector3(0f, 1.83f, 0f), new Vector3(0.38f, 0.01f, 0.38f), accent);
            Part(root, PrimitiveType.Cylinder, "Hat_Top", new Vector3(0f, 1.92f, 0f), new Vector3(0.22f, 0.09f, 0.22f), accent);
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void Part(GameObject root, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = pos;
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

    static CharacterAppearance EnsureAppearance(string path, string displayName, GameObject model)
    {
        var app = AssetDatabase.LoadAssetAtPath<CharacterAppearance>(path);
        if (app != null) return app;
        app = ScriptableObject.CreateInstance<CharacterAppearance>();
        app.displayName = displayName;
        app.modelPrefab = model;
        AssetDatabase.CreateAsset(app, path);
        return app;
    }

    static ThemeProfile EnsureTheme(CharacterAppearance staff, CharacterAppearance guest)
    {
        var theme = AssetDatabase.LoadAssetAtPath<ThemeProfile>(ThemePath);
        if (theme != null) return theme;

        theme = ScriptableObject.CreateInstance<ThemeProfile>();
        theme.innName = "The Wayside";
        theme.currencyName = "Coin";
        theme.currencySymbol = "c";
        theme.names = new List<ThemeProfile.NameEntry>
        {
            N(ThemeKeys.RoleClerk, "Clerk"), N(ThemeKeys.RolePorter, "Porter"), N(ThemeKeys.RoleServer, "Server"),
            N(ThemeKeys.RoleChambermaid, "Chambermaid"), N(ThemeKeys.RoleCook, "Cook"),
            N(ThemeKeys.GuestPilgrim, "Pilgrim"), N(ThemeKeys.GuestTraveller, "Traveller"),
            N(ThemeKeys.GuestMerchant, "Merchant"), N(ThemeKeys.GuestNoble, "Noble"),
            N(ThemeKeys.RoomBunk, "Bunk Room"), N(ThemeKeys.RoomCommon, "Common Room"),
            N(ThemeKeys.RoomPrivate, "Private Chamber"), N(ThemeKeys.RoomSuite, "Noble Suite"),
            N(ThemeKeys.ServiceMeal, "Supper"), N(ThemeKeys.ServiceDrink, "Ale"), N(ThemeKeys.ServiceFirewood, "Firewood"),
            N(ThemeKeys.ServiceBath, "Hot Bath"), N(ThemeKeys.ServiceTurndown, "Turndown"),
            N(ThemeKeys.ServiceBootPolish, "Boot Polish"), N(ThemeKeys.ServiceWakeUp, "Wake-up Call"),
            N(ThemeKeys.ServiceLuggage, "Luggage"),
            N(ThemeKeys.TierD, "Apprentice"), N(ThemeKeys.TierC, "Hand"), N(ThemeKeys.TierB, "Journeyman"),
            N(ThemeKeys.TierA, "Skilled"), N(ThemeKeys.TierS, "Master"),
            N(ThemeKeys.PlaceFrontDesk, "Front Desk"), N(ThemeKeys.PlaceKitchen, "Kitchen"),
            N(ThemeKeys.PlaceLaundry, "Laundry"), N(ThemeKeys.PlaceRestArea, "Servants' Room"),
            N(ThemeKeys.PlaceYard, "Yard"),
        };
        theme.firstNames = new[]
        {
            "Marta", "Edric", "Agnes", "Osric", "Matilda", "Wulfric", "Beatrix", "Godwin", "Isolde", "Aldous",
            "Rowena", "Hugh", "Elspeth", "Tobias", "Maud", "Cedric", "Joan", "Alaric", "Gwen", "Piers",
        };
        theme.palette = ThemePalette.Default;
        theme.props = new List<ThemeProfile.PropEntry>(); // empty = keep the graybox blocks
        theme.staffAppearances = new[] { staff };
        theme.guestAppearances = new List<ThemeProfile.AppearancePool>
        {
            new ThemeProfile.AppearancePool { guestType = ThemeKeys.GuestPilgrim, appearances = new[] { guest } },
            new ThemeProfile.AppearancePool { guestType = ThemeKeys.GuestTraveller, appearances = new[] { guest } },
            new ThemeProfile.AppearancePool { guestType = ThemeKeys.GuestMerchant, appearances = new[] { guest } },
            new ThemeProfile.AppearancePool { guestType = ThemeKeys.GuestNoble, appearances = new[] { guest } },
        };

        AssetDatabase.CreateAsset(theme, ThemePath);
        Debug.Log("[Setup] Created " + ThemePath);
        return theme;
    }

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
