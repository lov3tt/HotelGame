using System.IO;
using UnityEditor;

// Import settings for character models and animation clips, so a beginner can drop
// FBX files in and have them just work:
//   - Art/Characters/**     → Rig: Humanoid (so the shared animations retarget onto it)
//   - Animation/Clips/**    → Rig: Humanoid, root motion baked into pose (characters stay in place;
//                             the NavMeshAgent moves them), Loop Time on for looping clips
//
// Only applied on a file's first import. Change settings by hand afterwards and they're kept.
public class CharacterImportRules : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        bool isModel = assetPath.StartsWith(ProjectSetup.ModelsDir + "/");
        bool isClip = assetPath.StartsWith(ProjectSetup.ClipsDir + "/");
        if (!isModel && !isClip) return;

        var importer = (ModelImporter)assetImporter;
        if (!importer.importSettingsMissing) return; // already set up, respect manual changes

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = isClip;
    }

    void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(ProjectSetup.ClipsDir + "/")) return;
        var importer = (ModelImporter)assetImporter;
        if (!importer.importSettingsMissing) return;

        bool loop = ProjectSetup.LoopingClips.Contains(ProjectSetup.Normalize(Path.GetFileNameWithoutExtension(assetPath)));
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.loopTime = loop;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
        }
        importer.clipAnimations = clips;
    }

    // New clips mean the controller needs refreshing.
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (var path in imported)
        {
            if (path.StartsWith(ProjectSetup.ClipsDir + "/") && !path.EndsWith(".meta"))
            {
                EditorApplication.delayCall += () => ProjectSetup.EnsureAssets();
                return;
            }
        }
    }
}
