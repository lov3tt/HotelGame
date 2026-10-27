# Live2D Staff Portraits

The staff bar at the bottom of the HUD shows each persona as an animated bust in the VTuber style. The busts can:
- turn their head with a 2.5D parallax effect
- blink, look around and breathe
- talk
- react to mood: **Happy**, **Tired** or **Panic**

There are two kinds of portrait, and both plug into the same slot:

| | Built-in puppet (now) | Real Live2D model (later) |
|---|---|---|
| What | Layered sprites moved by `PortraitPuppet` | A Cubism `.moc3` model rigged in Cubism Editor |
| Made by | `tools/portraits/make_portraits.py` (code-drawn art) | You or an artist, in Cubism Editor |
| Quality | Good enough to play with | Real mesh deformation, physics and motions |
| Needs | Nothing | The Cubism SDK for Unity imported into the project |

The game drives both kinds of portrait the same way. `PortraitDriver` sets standard Live2D parameter IDs, such as `ParamAngleX`, `ParamEyeLOpen` and `ParamMouthOpenY`. Swapping the puppet for a real model needs no code changes.

---

## The pieces

| File | Job |
|---|---|
| `tools/portraits/make_portraits.py` | Draws the 5 personas as separate layers. Writes the PNG layers + `rig.json` into `Art/Portraits/<Id>/`, a layered PSD into `ArtSource/Live2D/<Id>.psd`, and a contact sheet at `previews/portraits_sheet.png`. |
| `Scripts/Portraits/PortraitParams.cs` | The standard parameter IDs, and the `IPortraitModel` interface. |
| `Scripts/Portraits/PortraitPuppet.cs` | The built-in 2.5D rig: parallax, blink, iris look, brows, mouth, breath, and spring physics for hair, ears and tails. |
| `Scripts/Portraits/PortraitDriver.cs` | Idle life and moods. Set `mood` and `talking` from gameplay. |
| `Scripts/Portraits/CubismPortraitModel.cs` | The adapter that lets a real Cubism model take the driver's parameters. |
| `Scripts/Portraits/PortraitView.cs` | A UI RawImage that films a portrait with its own camera, on the hidden **Portrait** layer. |
| `Editor/PortraitBuilder.cs` | Builds `Prefabs/Portraits/Portrait_<Id>.prefab` from the art. **InnKeeper → Rebuild Portraits** |
| `Editor/PortraitChecks.cs` | Headless checks. **InnKeeper → Run Portrait Checks** |

Each persona's appearance asset (`Data/Appearances/App_Persona_*.asset`) has a **Portrait Prefab** slot. The staff bar and the pop-ups to come use whatever is in that slot.

### Try it in the editor
1. Open `Scenes/Inn.unity` and press **Play**. The staff bar portraits blink, breathe and glance around.
2. In the Hierarchy, open **HUD → StaffBar → Card_Mochi → Portrait** and change **Mood** to *Panic*.
3. To pose a puppet by hand, outside Play mode:
   - Open `Prefabs/Portraits/Portrait_Mochi.prefab`.
   - Drag the **Parameters** sliders on its `PortraitPuppet`, e.g. Angle X, Eye L Open or Mouth Open Y.

---

## Changing the art

Edit the colours and shapes for each persona in `PERSONAS` in `tools/portraits/make_portraits.py`, then run:

```bash
python tools/portraits/make_portraits.py
```

Then, in Unity, run **InnKeeper → Rebuild Portraits**.

You can also paint over the generated layers in Krita. Keep the same layer names and sizes, and re-run **Rebuild Portraits**. If you re-run the script afterwards, it overwrites the PNGs.

---

## Making a real Live2D model

Tools you need:
- **Krita** (installed): free painting, for touching up or redrawing the layers.
- **Live2D Cubism Editor 5.3** (installed): for rigging.
- The free version is enough to learn on. Some automation features need the Pro edition, which has a trial.

### 1. Start from the layered PSD
`ArtSource/Live2D/<Id>.psd` already has every part on its own layer:

| Layers | Part |
|---|---|
| `eye_white_L/R`, `iris_L/R`, `eye_highlight_L/R`, `eye_lash_L/R` | Open eyes |
| `eye_closed_L/R` | Closed eyes (hidden by default) |
| `brow_L/R` | Eyebrows |
| `mouth_closed` | Closed mouth |
| `mouth_open` | Open mouth (hidden by default) |
| `face`, `blush` | Face |
| `ear_*`, `cat_ear_*`, `horn_*` | Ears and horns |
| `hair_front`, `hair_side_L/R`, `hair_back` | Hair |
| `body`, `outfit`, `tail` | Body |

You can redraw any of them in Krita or another art app. Keep one layer per moving part, and save as PSD.

### 2. Rig it in Cubism Editor
1. **File → Open** the PSD and create a new model.
2. **Meshes:** select every ArtMesh, then choose **Modeling → Mesh → Automatic Mesh Generation**.
3. **Parameters:** use the **standard IDs** the game sends:

   | ID | Range | Rig it to... |
   |---|---|---|
   | `ParamAngleX` | −30..30 | Turn the head (warp deformer on the head) |
   | `ParamAngleY` | −30..30 | Nod |
   | `ParamAngleZ` | −30..30 | Tilt (rotation deformer) |
   | `ParamEyeLOpen`, `ParamEyeROpen` | 0..1 | Blink (squash the eye, show `eye_closed`) |
   | `ParamEyeBallX`, `ParamEyeBallY` | −1..1 | Move the irises |
   | `ParamBrowLY`, `ParamBrowRY` | −1..1 | Lower / raise the brows |
   | `ParamMouthOpenY` | 0..1 | Open the mouth (swap to `mouth_open`) |
   | `ParamBodyAngleX` | −10..10 | Lean the body |
   | `ParamBreath` | 0..1 | Breathing |

   - Cubism's standard model template already uses these IDs.
   - If your edition has automatic facial motion generation, it creates most of them for you.
4. **Physics:** add pendulums for `hair_side_*`, `hair_back`, the tail and the ears.
5. **Export:** choose **File → Export For Runtime → Export as moc3 file**. Also export any motions as `.motion3.json`.

This session can connect to Cubism Editor. While you rig, ask Claude to set parameters live, e.g. "turn Mochi's head to 20 and blink". You can check each one without dragging sliders.

### 3. Put it in the game
1. Import the Cubism SDK if this machine doesn't have it (see below).
2. Drop the exported folder into `Assets/_Game/Art/Live2D/<Name>/`. Cubism's importer makes a prefab next to the `.model3.json`.
3. Open that prefab and **Add Component**:
   - **Cubism Portrait Model**.
   - **Portrait Driver**.
   - If the model names a parameter differently, add an entry under **Aliases**. For example, the SDK's Mao sample uses `ParamA` for the mouth, so the alias is `ParamMouthOpenY` → `ParamA`.
4. Drag the prefab into the persona's appearance asset → **Portrait Prefab**.
5. If the bust sits too high, low, big or small in its card, adjust **Framing Centre / Framing Size** on the card's `PortraitView`. Or scale the prefab.

The adapter only fills **empty** portrait slots. Rebuilding never replaces a model you assigned yourself.

---

## The Cubism SDK (not in git)

The SDK lives in `Game/Assets/Live2D/`, along with the `csc.rsp`/`mcs.rsp` compiler flags it adds. These are git-ignored, because the licence doesn't allow republishing Cubism Core in a source repository. Each machine imports it once:

1. Download **Cubism SDK for Unity (URP)** from live2d.com/en/sdk/download/unity/ and accept the licence.
2. Import it:
   - **In the editor:** Assets → Import Package → Custom Package, and choose `CubismSdkForUnity-5-r.6.unitypackage`.
   - **Headless:** unpack the package into `Game/` (this session used a small Python unpacker).

The game compiles without the SDK. `CubismPortraitModel` reaches the SDK through reflection, and the Cubism check skips itself when the SDK is missing.

**Licence for shipping:** releasing a game with Cubism models needs Live2D's SDK release licence if your revenue is above their small-business threshold. Check the current terms on live2d.com before you release.

---

## Checking it works

Run **InnKeeper → Run Portrait Checks**, or headless from the repo root:

```bash
"/c/Program Files/Unity/Hub/Editor/6000.0.79f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath Game -executeMethod PortraitChecks.RunBatch -logFile portraits.log
```

The checks cover:
- **Rigs:**
  - every rig has its pieces
  - head turns show parallax
  - blink, mouth and look work
  - parameters stay within their ranges
- **Personas:** every persona has a portrait.
- **Staff bar:** has five portraits, each filmed by its own camera, and the inn camera can't see the portrait layer.
- **Cubism (when the SDK is present):** a Cubism sample model is driven through the adapter.

**InnKeeper → Capture Camera Previews** also writes:
- `previews/9_portrait_expressions.png`: every persona in every expression.
- `previews/8_hud_staff_bar.png`: the HUD with the staff bar. TMP text doesn't show in batch captures; the checks verify it.
