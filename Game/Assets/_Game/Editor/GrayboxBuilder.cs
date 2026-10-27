using System.Collections.Generic;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Builds the graybox inn as a side-view cutaway (GDD §8) into Assets/_Game/Scenes/Inn.unity,
// bakes the NavMesh and checks that every room is reachable from the street.
//
// Layout: the building runs along X, floors stack along Y, and the open front
// faces -Z toward the camera. Each floor has a walkway along the front (z 0..1.8)
// with rooms behind it (z 1.8..5). Rooms have no front wall, so you can see in.
//
//   x: -12 ── stairwell ── -5 ── rooms / ground-floor areas ── 13 ── yard ── 17
//
// Editor:   InnKeeper → Rebuild Graybox Inn
// Headless: Unity.exe -batchmode -nographics -quit -projectPath Game -executeMethod GrayboxBuilder.BuildBatch
//
// Re-running overwrites the scene, so hand-edits to Inn.unity will be lost.
// Once you start customising the scene by hand, stop using this.
public static class GrayboxBuilder
{
    const string Root = "Assets/_Game";
    const string ScenePath = Root + "/Scenes/Inn.unity";
    const string MaterialDir = Root + "/Art/Materials";

    const float FloorHeight = 3f;
    const float SlabThickness = 0.3f;
    const float WallHeight = FloorHeight - SlabThickness;
    const float WallThickness = 0.2f;

    // Depth (Z) layout shared by every floor.
    const float FrontZ = 0f;
    const float WalkwayDepth = 1.8f;   // open walkway along the front of each floor
    const float BackZ = 5f;
    const float StairLaneSplit = 2.5f; // stairwell: front lane z 0..2.5, back lane z 2.5..5

    // Stairwell X layout: ramps run between RampLowX and RampHighX, with landings either side.
    const float BuildingMinX = -12f;
    const float RampWestX = -10.5f;
    const float RampEastX = -6.5f;
    const float RoomsMinX = -5f;
    const float BuildingMaxX = 13f;
    const float YardMaxX = 17f;

    static readonly string[] Folders =
    {
        "Scripts/Core", "Scripts/Theme", "Scripts/Characters", "Scripts/Tasks", "Scripts/Stations",
        "Scripts/Agents", "Scripts/Staff", "Scripts/UI", "Data", "Prefabs", "Scenes", "Art/Materials", "Animation",
    };

    static Dictionary<string, Material> materials;

    [MenuItem("InnKeeper/Rebuild Graybox Inn")]
    public static void Build()
    {
        CreateFolders();
        // Create the scene before loading any assets: NewScene unloads assets nothing references,
        // which would leave the materials and theme loaded above as dead references.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateMaterials();
        var setup = ProjectSetup.EnsureAssets();

        var inn = new GameObject("Inn");
        BuildStreetAndYard(inn.transform);
        BuildGroundFloor(inn.transform);
        BuildFirstFloor(inn.transform);
        BuildSecondFloor(inn.transform);
        BuildStairwell(inn.transform);

        var sun = BuildLightAndCamera();
        BuildThemeManager(setup.theme, sun);
        BuildHud(setup);
        BuildDemoCharacters(setup);

        var surface = inn.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        string navPath = Root + "/Scenes/Inn_NavMesh.asset";
        AssetDatabase.DeleteAsset(navPath);
        AssetDatabase.CreateAsset(surface.navMeshData, navPath);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();

        Debug.Log("[Graybox] Saved " + ScenePath);
        CheckPaths();
    }

    // Exits with a non-zero code if any path check fails, so batch runs report failure.
    public static void BuildBatch()
    {
        Build();
        if (!CheckPaths()) EditorApplication.Exit(1);
    }

    // ---------- Floors ----------

    static void BuildStreetAndYard(Transform parent)
    {
        var outside = Group("Outside", parent);
        Slab("Street", outside, BuildingMinX - 2, YardMaxX + 2, FrontZ - 2.5f, FrontZ, 0, "Stone");
        Slab("Yard", outside, BuildingMaxX, YardMaxX, FrontZ, BackZ, 0, "Grass");
        Prop(ThemeKeys.PropWoodpile, "Woodpile", outside, new Vector3(15.5f, 0.4f, 3.8f), new Vector3(1.5f, 0.8f, 1f), "Wood");
        Marker("Entrance", outside, new Vector3(-1f, 0, FrontZ - 1f));
    }

    static void BuildGroundFloor(Transform parent)
    {
        var floor = Group("Floor_Ground", parent);
        float y = 0f;

        Slab("Slab", floor, BuildingMinX, BuildingMaxX, FrontZ, BackZ, y);
        BackWall(floor, BuildingMinX, BuildingMaxX, y);
        SideWall("Wall_West", floor, BuildingMinX, FrontZ, BackZ, y);
        // East side opens onto the yard through the walkway.
        SideWall("Wall_East", floor, BuildingMaxX, WalkwayDepth, BackZ, y);

        // Areas behind the walkway, separated by divider walls.
        var lobby = Area("Lobby", floor, RoomsMinX, 3f, y);
        Prop(ThemeKeys.PropFrontDesk, "FrontDesk", lobby, new Vector3(-1f, y + 0.5f, 3.2f), new Vector3(3f, 1f, 0.8f), "Wood");
        Prop(ThemeKeys.PropTaproomTable, "Table_Taproom", lobby, new Vector3(1.8f, y + 0.4f, 3.8f), new Vector3(1.2f, 0.8f, 1.2f), "Wood");

        var kitchen = Area("Kitchen", floor, 3f, 8f, y);
        Prop(ThemeKeys.PropStove, "Stove", kitchen, new Vector3(6.5f, y + 0.5f, 4.3f), new Vector3(1.5f, 1f, 1f), "Stone");
        Prop(ThemeKeys.PropCopper, "Copper", kitchen, new Vector3(4.5f, y + 0.5f, 4.3f), new Vector3(1f, 1f, 1f), "Copper");

        var laundry = Area("Laundry", floor, 8f, 10.5f, y);
        Prop(ThemeKeys.PropLaundryTub, "LaundryTub", laundry, new Vector3(9.25f, y + 0.4f, 4.1f), new Vector3(1.2f, 0.8f, 1.2f), "Linen");

        var rest = Area("RestArea_Servants", floor, 10.5f, BuildingMaxX, y);
        for (int i = 0; i < 3; i++)
            Prop(ThemeKeys.PropSeat, "Seat_" + (i + 1), rest, new Vector3(11.1f + i * 0.7f, y + 0.25f, 4.4f), new Vector3(0.5f, 0.5f, 0.5f), "Wood");
    }

    static void BuildFirstFloor(Transform parent)
    {
        var floor = Group("Floor_1", parent);
        float y = FloorHeight;
        UpperFloorShell(floor, y, holeInFrontLane: true);

        Room("Room_Common_1", floor, RoomsMinX, -0.5f, y);
        Room("Room_Common_2", floor, -0.5f, 4f, y);
        Room("Room_Common_3", floor, 4f, 8.5f, y);
        Room("Room_Common_4", floor, 8.5f, BuildingMaxX, y);
    }

    static void BuildSecondFloor(Transform parent)
    {
        var floor = Group("Floor_2", parent);
        float y = FloorHeight * 2;
        UpperFloorShell(floor, y, holeInFrontLane: false);

        Room("Room_Private_1", floor, RoomsMinX, -0.5f, y);
        Room("Room_Private_2", floor, -0.5f, 4f, y);
        var suite = Room("Room_Suite", floor, 4f, BuildingMaxX, y, doubleBed: true);
        Prop(ThemeKeys.PropTub, "Tub_Slot", suite, new Vector3(11.5f, y + 0.3f, 3.8f), new Vector3(1.6f, 0.6f, 0.9f), "Copper");
    }

    // Slab, back wall and side walls for an upper floor, with a stairwell hole in one lane.
    // Floor 1 has the hole in the front lane (above the ground→1 ramp), floor 2 in the back lane.
    static void UpperFloorShell(GameObject floor, float y, bool holeInFrontLane)
    {
        Slab("Slab_StairLanding_W", floor, BuildingMinX, RampWestX, FrontZ, BackZ, y);
        if (holeInFrontLane) Slab("Slab_StairBackLane", floor, RampWestX, RampEastX, StairLaneSplit, BackZ, y);
        else Slab("Slab_StairFrontLane", floor, RampWestX, RampEastX, FrontZ, StairLaneSplit, y);
        Slab("Slab_Main", floor, RampEastX, BuildingMaxX, FrontZ, BackZ, y);

        BackWall(floor, BuildingMinX, BuildingMaxX, y);
        SideWall("Wall_West", floor, BuildingMinX, FrontZ, BackZ, y);
        SideWall("Wall_East", floor, BuildingMaxX, FrontZ, BackZ, y);
    }

    // Switchback stairwell at the west end: ground→1 rises east in the front lane,
    // 1→2 rises west in the back lane. Landings sit either side of the ramps.
    static void BuildStairwell(Transform parent)
    {
        var stairs = Group("Stairwell", parent);
        float frontLane = (FrontZ + StairLaneSplit) / 2f;
        float backLane = (StairLaneSplit + BackZ) / 2f;
        Ramp("Stairs_Ground_To_1", stairs, new Vector3(RampWestX, 0, frontLane), new Vector3(RampEastX, FloorHeight, frontLane));
        Ramp("Stairs_1_To_2", stairs, new Vector3(RampEastX, FloorHeight, backLane), new Vector3(RampWestX, FloorHeight * 2, backLane));
    }

    // ---------- Building blocks ----------

    // An open-fronted room: divider walls either side, a door marker on the walkway edge, a bed and a table.
    static GameObject Room(string name, GameObject floor, float x0, float x1, float y, bool doubleBed = false)
    {
        var room = Area(name, floor, x0, x1, y);
        float midX = (x0 + x1) / 2f;
        Marker("Door_" + name.Replace("Room_", ""), room, new Vector3(midX, y, WalkwayDepth));

        Prop(doubleBed ? ThemeKeys.PropBedDouble : ThemeKeys.PropBed, "Bed", room, new Vector3(x0 + 1.4f, y + 0.25f, 4f),
            doubleBed ? new Vector3(2f, 0.5f, 1.8f) : new Vector3(1.1f, 0.5f, 1.8f), "Linen");
        Prop(ThemeKeys.PropRoomTable, "Table", room, new Vector3(x1 - 1f, y + 0.35f, 4.1f), new Vector3(0.8f, 0.7f, 0.8f), "Wood");
        return room;
    }

    // A space behind the walkway with divider walls on both sides.
    static GameObject Area(string name, GameObject floor, float x0, float x1, float y)
    {
        var area = Group(name, floor.transform);
        SideWall("Wall_W", area, x0, WalkwayDepth, BackZ, y);
        SideWall("Wall_E", area, x1, WalkwayDepth, BackZ, y);
        return area;
    }

    static void Slab(string name, GameObject parent, float x0, float x1, float z0, float z1, float y, string mat = "Floor")
    {
        Box(name, parent, new Vector3((x0 + x1) / 2f, y - SlabThickness / 2f, (z0 + z1) / 2f),
            new Vector3(x1 - x0, SlabThickness, z1 - z0), mat);
    }

    static void BackWall(GameObject parent, float x0, float x1, float y)
    {
        Box("Wall_Back", parent, new Vector3((x0 + x1) / 2f, y + WallHeight / 2f, BackZ + WallThickness / 2f),
            new Vector3(x1 - x0, WallHeight, WallThickness), "Wall");
    }

    // Wall running along Z (front to back) at a fixed x.
    static void SideWall(string name, GameObject parent, float x, float z0, float z1, float y)
    {
        Box(name, parent, new Vector3(x, y + WallHeight / 2f, (z0 + z1) / 2f),
            new Vector3(WallThickness, WallHeight, z1 - z0), "Wall");
    }

    // A walkable ramp whose top surface runs from 'bottom' to 'top'.
    static void Ramp(string name, GameObject parent, Vector3 bottom, Vector3 top)
    {
        Vector3 dir = top - bottom;
        var ramp = Box(name, parent, Vector3.zero, new Vector3(2f, 0.2f, dir.magnitude), "Wood");
        ramp.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        // Shift down by half the thickness so the top surface sits on the line.
        ramp.transform.position = (bottom + top) / 2f - ramp.transform.up * 0.1f;
    }

    static GameObject Box(string name, GameObject parent, Vector3 position, Vector3 scale, string mat)
        => Box(name, parent.transform, position, scale, mat);

    // A graybox block the theme can replace with a prefab (see ThemedProp).
    static GameObject Prop(string propKey, string name, GameObject parent, Vector3 position, Vector3 scale, string mat)
    {
        var block = Box(name, parent, position, scale, mat);
        var prop = block.AddComponent<ThemedProp>();
        SetField(prop, "propKey", propKey);
        return block;
    }

    static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, string mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = materials[mat];
        return go;
    }

    static GameObject Group(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static GameObject Marker(string name, GameObject parent, Vector3 position)
    {
        var go = Group(name, parent.transform);
        go.transform.position = position;
        return go;
    }

    // ---------- Scene setup ----------

    static Light BuildLightAndCamera()
    {
        var sun = new GameObject("Sun");
        var light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        // Light from the front-left so room interiors are lit in the 2D view.
        sun.transform.rotation = Quaternion.Euler(35f, 30f, 0f);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.70f, 0.85f);
        cam.cullingMask = ~(1 << PortraitView.PortraitLayer);
        camGo.AddComponent<CutawayCameraController>();
        return light;
    }

    static void BuildThemeManager(ThemeProfile theme, Light sun)
    {
        // Added while inactive so it doesn't warn about a missing theme before the fields are set.
        var game = new GameObject("Game");
        game.SetActive(false);
        var manager = game.AddComponent<ThemeManager>();
        SetField(manager, "theme", theme);
        SetField(manager, "sun", sun);
        game.SetActive(true);
    }

    public const string HudPurseTemplate = "{ui.purse}: 100{currency.symbol}";

    // Top bar with the inn's name and a purse placeholder (the real purse arrives with the economy).
    static void BuildHud(ProjectSetup.Assets setup)
    {
        var hud = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = hud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var bar = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
        var barRect = (RectTransform)bar.transform;
        barRect.SetParent(hud.transform, false);
        barRect.anchorMin = new Vector2(0, 1);
        barRect.anchorMax = new Vector2(1, 1);
        barRect.pivot = new Vector2(0.5f, 1);
        barRect.sizeDelta = new Vector2(0, 64);
        bar.AddComponent<ThemedGraphic>();

        Label("InnName", barRect, "{inn.name}", PaletteRole.Highlight, 36, TextAlignmentOptions.MidlineLeft);
        Label("Purse", barRect, HudPurseTemplate, PaletteRole.Text, 28, TextAlignmentOptions.MidlineRight);

        BuildStaffBar(hud.transform, setup);
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    public const float StaffCardWidth = 150f, StaffCardHeight = 200f, StaffCardGap = 10f;

    // Bottom-left row of staff cards, each with the persona's live portrait (PortraitView),
    // name and themed role. Drag-and-drop assignment from these cards arrives in Milestone 7.
    static void BuildStaffBar(Transform hud, ProjectSetup.Assets setup)
    {
        var bar = new GameObject("StaffBar", typeof(RectTransform));
        var barRect = (RectTransform)bar.transform;
        barRect.SetParent(hud, false);
        barRect.anchorMin = barRect.anchorMax = barRect.pivot = Vector2.zero;
        barRect.anchoredPosition = new Vector2(16, 16);
        var roles = ProjectSetup.PersonaRoles;
        barRect.sizeDelta = new Vector2(roles.Length * (StaffCardWidth + StaffCardGap) - StaffCardGap, StaffCardHeight);

        for (int i = 0; i < roles.Length; i++)
        {
            var look = setup.personaByRole[roles[i]];
            string personaName = look.displayName.Split(',')[0].Trim();
            var card = new GameObject("Card_" + personaName, typeof(RectTransform), typeof(Image));
            var cardRect = (RectTransform)card.transform;
            cardRect.SetParent(barRect, false);
            cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = Vector2.zero;
            cardRect.anchoredPosition = new Vector2(i * (StaffCardWidth + StaffCardGap), 0);
            cardRect.sizeDelta = new Vector2(StaffCardWidth, StaffCardHeight);
            card.AddComponent<ThemedGraphic>();

            var portrait = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage));
            var portraitRect = (RectTransform)portrait.transform;
            portraitRect.SetParent(cardRect, false);
            portraitRect.anchorMin = new Vector2(0, 1);
            portraitRect.anchorMax = new Vector2(1, 1);
            portraitRect.pivot = new Vector2(0.5f, 1);
            portraitRect.anchoredPosition = new Vector2(0, -4);
            portraitRect.sizeDelta = new Vector2(-8, StaffCardWidth - 8);
            var view = portrait.AddComponent<PortraitView>();
            view.SetAppearance(look);
            SetField(view, "appearance", look);

            var label = Label("Name", cardRect, personaName + "\n<size=70%>{" + roles[i] + "}</size>",
                PaletteRole.Text, 22, TextAlignmentOptions.Bottom);
            label.offsetMin = new Vector2(6, 6);
            label.offsetMax = new Vector2(-6, 0);
        }
    }

    static RectTransform Label(string name, RectTransform parent, string template, PaletteRole color, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(24, 0);
        rect.offsetMax = new Vector2(-24, 0);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = align;
        text.fontStyle = FontStyles.Bold;
        var themed = go.AddComponent<ThemedText>();
        SetField(themed, "template", template);
        SetField(themed, "color", (int)color);
        themed.Template = template; // refresh now so the label shows in the editor straight away
        return rect;
    }

    // The staff personas at their posts plus a couple of guests, to show the model and theme systems working.
    static void BuildDemoCharacters(ProjectSetup.Assets setup)
    {
        var root = new GameObject("Characters");
        var staff = setup.personaByRole;
        DemoCharacter(setup.characterPrefab, root, "Clerk", staff[ThemeKeys.RoleClerk], new Vector3(-1f, 0, 4.2f), 180f);
        DemoCharacter(setup.characterPrefab, root, "Guest_AtDesk", setup.guestAppearance, new Vector3(-0.3f, 0, 1.4f), -30f);
        DemoCharacter(setup.characterPrefab, root, "Cook", staff[ThemeKeys.RoleCook], new Vector3(5.6f, 0, 3.4f), 160f);
        DemoCharacter(setup.characterPrefab, root, "Porter", staff[ThemeKeys.RolePorter], new Vector3(14.5f, 0, 2.5f), 200f);
        DemoCharacter(setup.characterPrefab, root, "Server_Walkway", staff[ThemeKeys.RoleServer], new Vector3(2f, FloorHeight, 0.9f), 90f);
        DemoCharacter(setup.characterPrefab, root, "Chambermaid", staff[ThemeKeys.RoleChambermaid], new Vector3(7.2f, FloorHeight, 3f), 180f);
        DemoCharacter(setup.characterPrefab, root, "Guest_InRoom", setup.guestAppearance, new Vector3(2.6f, FloorHeight, 2.6f), 200f);
    }

    static void DemoCharacter(GameObject prefab, GameObject parent, string name, CharacterAppearance look, Vector3 pos, float yaw)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
        go.name = name;
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        var rig = go.GetComponent<CharacterRig>();
        SetField(rig, "appearance", look);
        rig.SetAppearance(look); // rebuild the preview now rather than on the next editor tick
    }

    static void SetField(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        switch (value)
        {
            case Object obj: prop.objectReferenceValue = obj; break;
            case string str: prop.stringValue = str; break;
            case int i: prop.intValue = i; break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateFolders()
    {
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "_Game");
        foreach (var path in Folders)
        {
            string current = Root;
            foreach (var part in path.Split('/'))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }

    static void CreateMaterials()
    {
        var colors = new Dictionary<string, Color>
        {
            { "Floor", new Color(0.55f, 0.42f, 0.30f) },
            { "Wall", new Color(0.78f, 0.74f, 0.66f) },
            { "Wood", new Color(0.42f, 0.27f, 0.15f) },
            { "Stone", new Color(0.45f, 0.45f, 0.47f) },
            { "Copper", new Color(0.72f, 0.45f, 0.20f) },
            { "Linen", new Color(0.90f, 0.88f, 0.80f) },
            { "Grass", new Color(0.35f, 0.50f, 0.25f) },
        };

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        materials = new Dictionary<string, Material>();
        foreach (var kv in colors)
        {
            string path = MaterialDir + "/M_" + kv.Key + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", kv.Value);
            mat.color = kv.Value;
            EditorUtility.SetDirty(mat);
            materials[kv.Key] = mat;
        }
    }

    // ---------- Verification ----------

    // Checks that an agent can walk from the street to key spots on every floor.
    static bool CheckPaths()
    {
        const float roomPointZ = 2.6f; // just inside a room, past the walkway
        var targets = new Dictionary<string, Vector3>
        {
            { "Front desk", new Vector3(-1f, 0, 2.3f) },
            { "Kitchen stove", new Vector3(6.5f, 0, 3.2f) },
            { "Laundry", new Vector3(9.25f, 0, 3f) },
            { "Servants' room", new Vector3(11.8f, 0, 3.2f) },
            { "Woodpile (yard)", new Vector3(14f, 0, 3.5f) },
            { "Common Room 1", new Vector3(-2.75f, FloorHeight, roomPointZ) },
            { "Common Room 4", new Vector3(10.75f, FloorHeight, roomPointZ) },
            { "Private Room 1", new Vector3(-2.75f, FloorHeight * 2, roomPointZ) },
            { "Private Room 2", new Vector3(1.75f, FloorHeight * 2, roomPointZ) },
            { "Noble Suite", new Vector3(8.5f, FloorHeight * 2, roomPointZ) },
        };

        if (!NavMesh.SamplePosition(new Vector3(-1f, 0, FrontZ - 1f), out var start, 1f, NavMesh.AllAreas))
        {
            Debug.LogError("[Graybox] PATH CHECK: no NavMesh at the entrance.");
            return false;
        }

        bool allOk = true;
        var path = new NavMeshPath();
        foreach (var kv in targets)
        {
            if (!NavMesh.SamplePosition(kv.Value, out var end, 1f, NavMesh.AllAreas))
            {
                string nearest = NavMesh.SamplePosition(kv.Value, out var near, 5f, NavMesh.AllAreas) ? near.position.ToString() : "none within 5m";
                Debug.LogError($"[Graybox] PATH FAIL entrance -> {kv.Key} (no NavMesh near {kv.Value}, nearest: {nearest})");
                allOk = false;
                continue;
            }
            path.ClearCorners();
            bool ok = NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
                      && path.status == NavMeshPathStatus.PathComplete;
            if (ok) Debug.Log($"[Graybox] PATH OK   entrance -> {kv.Key}");
            else
            {
                var corners = path.corners;
                string reached = corners.Length > 0 ? corners[corners.Length - 1].ToString() : "nowhere";
                Debug.LogError($"[Graybox] PATH FAIL entrance -> {kv.Key} ({path.status}, got to {reached})");
            }
            allOk &= ok;
        }
        return allOk;
    }
}
