# Customising — Themes & Characters

Everything that defines the game's look and names is data, not code. You can reskin the inn or change who walks around in it from the Inspector alone.

---

## Themes

A **ThemeProfile** asset holds:
- **Identity:** inn name, currency name and symbol.
- **Display names:** every role, guest type, room, service, tier and place.
- **Candidate first names** for the hiring board.
- **UI:** palette colours, font and panel sprite.
- **World:** prop prefabs that replace graybox furniture, sky colour, sun colour and strength, ambient light.
- **Audio:** service bell and music.
- **Characters:** which looks new staff and each guest type get.

Two themes ship in `Assets/_Game/Data/Themes/`:
- **Theme_IsekaiStream** (active): The Second Life Inn, with streamer-persona staff, pastel UI and a twilight sky.
- **Theme_Medieval:** The Wayside, the plain medieval inn.

To switch between them, drag one onto the **Game** object's **ThemeManager → Theme**.

Each persona's look is an appearance asset (`Data/Appearances/App_Persona_*.asset`). Replace its model with real art, such as a Humanoid model, and every place that persona appears updates. Its **Portrait Prefab** is the animated bust in the staff bar. It's a built-in 2.5D puppet now, and can be replaced by a real Live2D model. See [Live2D.md](Live2D.md).

### Make a new theme
1. In the Project window, select `Theme_Medieval` and press **Ctrl+D** to duplicate it. Rename the copy, e.g. `Theme_Saloon`.
2. Edit the copy in the Inspector:
   - Change **Inn Name** and **Currency**.
   - Rename entries in **Names**, e.g. `role.porter` → "Bellhop".
   - Recolour the **Palette** and change the **Background Color**.
3. Select the **Game** object in the `Inn` scene and drag your theme onto **ThemeManager → Theme**.

The HUD, colours and props update straight away, even outside Play mode.

### Replace graybox furniture with real art
Every furniture block in the scene has a **ThemedProp** component with a key such as `prop.bed`, `prop.stove` or `prop.front_desk`. To replace the blocks:
1. Make a prefab for the furniture:
   - Pivot at the **bottom centre**.
   - Front of the furniture facing **−Z**, toward the camera.
2. In your theme's **Props** list, add an entry with the key (e.g. `prop.bed`) and your prefab.

Every bed in the inn now shows your model, and the graybox block hides itself. The block's collider stays, so walking paths don't change. Keys you leave out keep their graybox block. The full key list is in `Scripts/Theme/ThemeKeys.cs`.

### Using themed text in new UI
Add a **ThemedText** component next to any TextMeshPro label and write a template with `{key}` tokens:

| Template | Shows (Medieval) |
|---|---|
| `{inn.name}` | The Wayside |
| `Hire a {role.porter}` | Hire a Porter |
| `Purse: 100{currency.symbol}` | Purse: 100c |

**ThemedGraphic** does the same for panel colours. In code, use `ThemeManager.Current.GetName(ThemeKeys.RolePorter)` instead of typing "Porter".

---

## Characters

Every guest and staff member is a **Character** (`Prefabs/Characters/Character.prefab`). The prefab has:
- **CharacterRig:** spawns a model from a **CharacterAppearance** asset into its `ModelSlot`.
- **AgentAnimator:** tells the shared animator what the character is doing.

To swap a character's model, select it and drag a different appearance into **CharacterRig → Appearance**. Two placeholder looks come with the project: `App_Staff_Placeholder` (green tunic, apron) and `App_Guest_Placeholder` (blue coat, top hat). They're made from primitives and have no rig, so they don't animate. A real Humanoid model replaces them.

### Add your own character model
Any model with a **humanoid skeleton** works. Sources include Mixamo characters, Synty, VRoid exports, or your own Blender model.

1. **Import:** drop the `.fbx` into `Assets/_Game/Art/Characters/`.
   - An import rule sets **Rig → Animation Type: Humanoid** automatically.
   - If the import doesn't look right, select the FBX, open the **Rig** tab and click **Configure** to check the bone mapping.
2. **Create a look:** right-click in `Assets/_Game/Data/Appearances/`, choose **Create → InnKeeper → Character Appearance**, and drag your FBX into **Model Prefab**.
   - **Height Scale:** fixes a model imported at the wrong size. Mixamo models are usually fine at 1.
   - **Yaw Offset:** fixes a model that doesn't face forward.
   - **Material Overrides / Tint:** make colour variants of the same model.
3. **Use it:**
   - On a single character: drag it into **CharacterRig → Appearance**.
   - For hiring and guests: add it to your theme's **Staff Appearances** or **Guest Appearances** pools.

### Add animations
All characters share one controller, `Animation/InnCharacter.controller`. Because it's Humanoid, every clip works on every Humanoid model.

1. Download clips, for example from Mixamo:
   - Pick **FBX for Unity**.
   - Choose **Without Skin** for clips after the first one.
   - Tick **In Place** for walk.
2. Name each file after the state it's for and drop it into `Assets/_Game/Animation/Clips/`:

   | File name | Used for |
   |---|---|
   | `Idle`, `Walk` | Standing and walking (blended by speed) |
   | `Carry` | Arms holding a tray, buckets or luggage. Plays on top of walking |
   | `Pour`, `Cook`, `Serve`, `Clean`, `Knock`, `Stoke` | Work at a station |
   | `PourBath`, `MakeBed` | Room service work |
   | `Sit`, `Sleep` | Resting in the servants' room, guests in bed |

   Case and punctuation don't matter: `Pour Bath.fbx`, `pour_bath.fbx` and `PourBath.fbx` all match.
3. **What happens next is automatic:**
   - The import rules set clips to Humanoid and bake root motion in place, so the NavMesh moves characters, not the animation.
   - Looping clips (idle, walk, carry, cook...) get **Loop Time** turned on.
   - The controller fills in the matching state.
   - The Console lists which states still have no clip.
4. If a state isn't picked up, run **InnKeeper → Setup Theme & Characters**.

In code, gameplay only talks to `AgentAnimator`:
```csharp
anim.PlayWork(WorkAnim.Serve);  // play the serve animation
anim.SetCarrying(true);         // arms hold a tray while walking
anim.StopWork();                // back to idle/walk
```
Walking speed comes from the NavMeshAgent automatically.

### Import rules only apply once
The automatic Humanoid, loop and root-motion settings are applied the first time a file is imported. Changes you make in the FBX's import settings afterwards are kept.

---

## Checking it all still works
Run **InnKeeper → Run Milestone 2 Checks**. It checks:
- Theme names, colours and props swap correctly.
- Models swap correctly.
- The controller has every state.
- The HUD is laid out properly.

It changes nothing in your scene. Headless, from the repo root:
```bash
"/c/Program Files/Unity/Hub/Editor/6000.0.79f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath Game -executeMethod Milestone2Checks.RunBatch -logFile m2.log
```
