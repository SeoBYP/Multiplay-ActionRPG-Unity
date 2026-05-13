#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.System.MotionSystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MotionMatchingSampleAnimationSetup
{
    private const string SampleRoot = "Assets/Art/SampleAnimation";
    private const string MannequinPath = SampleRoot + "/SKM_UEFN_Mannequin/SKM_UEFN_Mannequin.FBX";
    private const string OutputFolder = "Assets/GameResources/MotionMatching/Player";
    private const string DatasetPath = OutputFolder + "/PlayerMotionDataset.asset";
    private const string AvatarPath = OutputFolder + "/PlayerHumanoidAvatar.asset";
    private const string RuntimeAvatarPath = OutputFolder + "/PlayerRuntimeHumanoidAvatar.asset";
    private const string InputPath = OutputFolder + "/PlayerCameraBasedMotionInput.asset";
    private const string ControllerPath = OutputFolder + "/PlayerCharacterMotionController.asset";
    private const string BakeControllerPath = OutputFolder + "/PlayerSampleBake.controller";
    private const string CharacteristicsPath = OutputFolder + "/PlayerCharacteristics.asset";
    private const string QueriesPath = OutputFolder + "/PlayerQueriesComputed.asset";
    private const string TagsPath = OutputFolder + "/PlayerTags.asset";
    private const string MainScenePath = "Assets/Scenes/Main.unity";
    private const string PlayerName = "PlayerCharacter";

    [MenuItem("Tools/Motion Matching/Sample Animation/Setup PlayerCharacter")]
    public static void SetupPlayerCharacter()
    {
        EnsureOutputFolder();
        var input = EnsureAsset<CameraBasedMotionInput>(InputPath);
        var controller = EnsureAsset<PlayerCharacterMotionController>(ControllerPath);
        SetObjectReference(controller, "customInput", input);
        SetBool(controller, "applyRootMovement", false);

        var dataset = AssetDatabase.LoadAssetAtPath<Dataset>(DatasetPath);

        SetupMainScene(dataset, controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MotionMatching] PlayerCharacter setup complete.");
    }

    [MenuItem("Tools/Motion Matching/Sample Animation/Apply Import Settings")]
    public static void ApplyImportSettings()
    {
        EnsureOutputFolder();
        if (AssetImporter.GetAtPath(MannequinPath) is ModelImporter mannequinImporter)
        {
            mannequinImporter.importAnimation = true;
            mannequinImporter.animationType = ModelImporterAnimationType.Human;
            mannequinImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mannequinImporter.SaveAndReimport();
        }

        var sourceAvatar = LoadMannequinAvatar();
        foreach (string path in Directory.GetFiles(SampleRoot, "*.FBX", SearchOption.AllDirectories)
                     .Select(ToAssetPath))
        {
            if (path == MannequinPath)
                continue;

            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                continue;

            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = sourceAvatar;

            var clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                bool isLoop = IsLoopClip(Path.GetFileNameWithoutExtension(path));
                clips[i].loopTime = isLoop;
                clips[i].loopPose = isLoop;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MotionMatching] SampleAnimation import settings applied.");
    }

    [MenuItem("Tools/Motion Matching/Sample Animation/Bake Player Sample Database")]
    public static void BakePlayerSampleDatabase()
    {
        ApplyImportSettings();
        EnsureOutputFolder();
        DeleteIfExists(DatasetPath);
        DeleteIfExists(AvatarPath);
        DeleteIfExists(CharacteristicsPath);
        DeleteIfExists(QueriesPath);
        DeleteIfExists(TagsPath);

        Avatar sampleAvatar = LoadMannequinAvatar();
        var avatar = CreateTransientHumanoidAvatar(sampleAvatar);
        var clips = CollectLocomotionClips(out var clipPaths);
        if (clips.Count == 0)
            throw new InvalidOperationException("No SampleAnimation locomotion clips were found.");

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(MannequinPath);
        var bakeRoot = (GameObject)PrefabUtility.InstantiatePrefab(model);
        bakeRoot.name = "MotionMatching_PlayerSampleBake_Temp";
        try
        {
            var animator = bakeRoot.GetComponent<Animator>() ?? bakeRoot.AddComponent<Animator>();
            animator.applyRootMotion = true;
            animator.avatar = sampleAvatar;
            animator.runtimeAnimatorController = EnsureBakeAnimatorController();

            var recorder = bakeRoot.GetComponent<RecordPositions>() ?? bakeRoot.AddComponent<RecordPositions>();
            var tags = BuildTags(clips);
            var characteristics = avatar.GetAvatarDefinition()
                .Where(bone => bone.id >= 0)
                .Select(bone => new BoneCharacteristic
                {
                    bone = bone,
                    weightPosition = 1f,
                    weightVelocity = IsLowerBodyBone(bone.alias) ? 1f : 0.35f
                })
                .ToList();

            recorder.ProcessData(
                ref clips,
                clipPaths,
                avatar,
                poseStep: 0.1f,
                futureEstimates: 3,
                futureEstimatesTime: 1.0f,
                pastEstimates: 3,
                pastEstimatesTime: 0.6f,
                databaseName: "PlayerMotionDataset",
                root: bakeRoot.transform,
                recordVelocity: 1,
                combinations: new List<List<TagBase>>
                {
                    new() { tags.walk },
                    new() { tags.run }
                },
                tags: new List<TagBase> { tags.walk, tags.run },
                actionTags: new List<ActionTag>(),
                idleTags: new List<IdleTag> { tags.idle },
                characteristics: characteristics,
                rac: animator.runtimeAnimatorController);

            RunRecorderToCompletion(recorder);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(bakeRoot);
        }

        var dataset = AssetDatabase.LoadAssetAtPath<Dataset>(DatasetPath);
        var controller = AssetDatabase.LoadAssetAtPath<PlayerCharacterMotionController>(ControllerPath);
        SetupMainScene(dataset, controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MotionMatching] Player sample database baked and assigned to PlayerCharacter.");
    }

    [MenuItem("Tools/Motion Matching/Sample Animation/Log Dataset Pose Variance")]
    public static void LogDatasetPoseVariance()
    {
        var dataset = AssetDatabase.LoadAssetAtPath<Dataset>(DatasetPath);
        if (dataset == null)
        {
            Debug.LogWarning($"[MotionMatching] Dataset was not found at {DatasetPath}.");
            return;
        }

        EnsureManagedAnimationData(dataset);
        if (dataset.animationsData == null || dataset.animationsData.Count == 0)
        {
            Debug.LogWarning("[MotionMatching] Dataset has no baked animation data.");
            return;
        }

        int bonesCount = dataset.animationsData
            .Where(animation => animation != null && animation.Count > 0)
            .Select(animation => animation[0].bonesData?.Length ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        string[] boneLabels = BuildBoneLabels(dataset.avatar, bonesCount);

        for (int animID = 0; animID < dataset.animationsData.Count; animID++)
        {
            List<AnimationData> frames = dataset.animationsData[animID];
            if (frames == null || frames.Count == 0 || frames[0].bonesData == null)
                continue;

            int frameBonesCount = frames[0].bonesData.Length;
            var maxLocalPositionDelta = new float[frameBonesCount];
            var maxRootRelativePositionDelta = new float[frameBonesCount];
            var maxRotationAngle = new float[frameBonesCount];
            var maxAngularVelocity = new float[frameBonesCount];
            float maxRootPositionDelta = 0f;
            float maxRootRotationAngle = 0f;
            AnimationData firstFrame = frames[0];

            for (int frame = 0; frame < frames.Count; frame++)
            {
                AnimationData currentFrame = frames[frame];
                if (currentFrame.bonesData == null)
                    continue;

                maxRootPositionDelta = math.max(maxRootPositionDelta,
                    math.length(currentFrame.rootPosition - firstFrame.rootPosition));
                maxRootRotationAngle = math.max(maxRootRotationAngle,
                    QuaternionAngleDegrees(firstFrame.rootRotation, currentFrame.rootRotation));

                for (int bone = 0; bone < math.min(frameBonesCount, currentFrame.bonesData.Length); bone++)
                {
                    BoneData firstBone = firstFrame.bonesData[bone];
                    BoneData currentBone = currentFrame.bonesData[bone];
                    if (!firstBone.isValid || !currentBone.isValid)
                        continue;

                    maxLocalPositionDelta[bone] = math.max(maxLocalPositionDelta[bone],
                        math.length(currentBone.localPosition - firstBone.localPosition));
                    maxRootRelativePositionDelta[bone] = math.max(maxRootRelativePositionDelta[bone],
                        math.length(currentBone.position - firstBone.position));
                    maxRotationAngle[bone] = math.max(maxRotationAngle[bone],
                        QuaternionAngleDegrees(firstBone.rotation, currentBone.rotation));
                    maxAngularVelocity[bone] = math.max(maxAngularVelocity[bone],
                        math.length(currentBone.angularVelocity));
                }
            }

            string clipName = dataset.animationPaths != null && animID < dataset.animationPaths.Count
                ? GetClipName(dataset.animationPaths[animID])
                : $"Animation {animID}";

            Debug.Log(
                "[MotionMatching Dataset Diagnostics]\n" +
                $"Clip: {clipName} | Frames: {frames.Count} | Bones: {frameBonesCount}\n" +
                $"Root Position Delta: {maxRootPositionDelta:F5} | Root Rotation Delta: {maxRootRotationAngle:F2} deg\n" +
                $"Top LocalPosition Delta: {FormatTopValues(maxLocalPositionDelta, boneLabels, 8, "m")}\n" +
                $"Top RootRelative Position Delta: {FormatTopValues(maxRootRelativePositionDelta, boneLabels, 8, "m")}\n" +
                $"Top Rotation Delta: {FormatTopValues(maxRotationAngle, boneLabels, 8, "deg")}\n" +
                $"Top Angular Velocity: {FormatTopValues(maxAngularVelocity, boneLabels, 8, "rad/s")}");
        }
    }

    private static void EnsureManagedAnimationData(Dataset dataset)
    {
        if (dataset.animationsData != null && dataset.animationsData.Count > 0)
            return;

        dataset.OnAfterDeserialize();
    }

    private static string[] BuildBoneLabels(CustomAvatar avatar, int bonesCount)
    {
        var labels = Enumerable.Range(0, bonesCount)
            .Select(index => index < (int)HumanBodyBones.LastBone
                ? ((HumanBodyBones)index).ToString()
                : $"Bone {index}")
            .ToArray();

        if (avatar == null)
            return labels;

        foreach (AvatarBone bone in avatar.GetAvatarDefinition())
        {
            if (bone.id < 0 || bone.id >= labels.Length)
                continue;

            labels[bone.id] = string.IsNullOrWhiteSpace(bone.alias)
                ? bone.boneName
                : bone.alias;
        }

        return labels;
    }

    private static string FormatTopValues(float[] values, string[] labels, int count, string unit)
    {
        return string.Join(", ", values
            .Select((value, index) => new { value, index })
            .OrderByDescending(item => item.value)
            .Take(count)
            .Select(item => $"{labels[item.index]}={item.value:F4}{unit}"));
    }

    private static float QuaternionAngleDegrees(quaternion a, quaternion b)
    {
        float dot = math.abs(math.dot(a.value, b.value));
        dot = math.clamp(dot, -1f, 1f);
        return math.degrees(2f * math.acos(dot));
    }

    private static (TagBase walk, TagBase run, IdleTag idle) BuildTags(IReadOnlyList<AnimationClip> clips)
    {
        var walk = new TagBase("Walk");
        var run = new TagBase("Run");
        var idle = new IdleTag("Idle", false)
        {
            initRanges = new List<TagRange>(),
            loopRanges = new List<TagRange>()
        };

        foreach (var clip in clips)
        {
            var range = new TagRange(clip.name, 0, int.MaxValue);
            if (clip.name.Contains("Idle", StringComparison.OrdinalIgnoreCase))
                idle.loopRanges.Add(range);
            else if (clip.name.Contains("Walk", StringComparison.OrdinalIgnoreCase))
                walk.ranges.Add(range);
            else if (clip.name.Contains("Run", StringComparison.OrdinalIgnoreCase))
                run.ranges.Add(range);
        }

        return (walk, run, idle);
    }

    private static List<AnimationClip> CollectLocomotionClips(out List<string> clipPaths)
    {
        var selectedPaths = new[]
            {
                SampleRoot + "/Idle/M_Neutral_Stand_Idle_Loop.FBX"
            }
            .Concat(Directory.GetFiles(Path.Combine(Application.dataPath, "Art/SampleAnimation/Walk"), "*.FBX")
                .Select(ToAssetPath)
                .Where(path => Path.GetFileNameWithoutExtension(path).Contains("_Walk_Loop_")))
            .Concat(Directory.GetFiles(Path.Combine(Application.dataPath, "Art/SampleAnimation/Run"), "*.FBX")
                .Select(ToAssetPath)
                .Where(path => Path.GetFileNameWithoutExtension(path).Contains("_Run_Loop_")))
            .Distinct()
            .OrderBy(path => path)
            .ToList();

        var clips = new List<AnimationClip>();
        clipPaths = new List<string>();
        foreach (string path in selectedPaths)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (clip == null)
                continue;

            var bakeClip = UnityEngine.Object.Instantiate(clip);
            bakeClip.name = ToClipName(path);
            clips.Add(bakeClip);
            clipPaths.Add(path + "//" + bakeClip.name);
        }

        return clips;
    }

    private static void RunRecorderToCompletion(RecordPositions recorder)
    {
        MethodInfo fixedUpdate = typeof(RecordPositions).GetMethod("FixedUpdate",
            BindingFlags.Instance | BindingFlags.NonPublic);

        for (int i = 0; i < 20000; i++)
        {
            fixedUpdate?.Invoke(recorder, null);
            if (AssetDatabase.LoadAssetAtPath<Dataset>(DatasetPath) != null)
                return;
        }

        throw new TimeoutException("MotionMatching dataset bake did not complete within the editor step budget.");
    }

    private static RuntimeAnimatorController EnsureBakeAnimatorController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BakeControllerPath);
        if (controller != null)
            return controller;

        controller = AnimatorController.CreateAnimatorControllerAtPath(BakeControllerPath);
        controller.layers[0].stateMachine.AddState("BakeSample");
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static HumanoidAvatar EnsureHumanoidAvatar()
    {
        var avatar = EnsureAsset<HumanoidAvatar>(AvatarPath);
        SetObjectReference(avatar, "avatar", LoadMannequinAvatar());
        SetInt(avatar, "humanRootBone", (int)HumanBodyBones.Hips);
        avatar.SetRootBone((int)HumanBodyBones.Hips);
        return avatar;
    }

    private static HumanoidAvatar CreateTransientHumanoidAvatar(Avatar unityAvatar)
    {
        var avatar = ScriptableObject.CreateInstance<HumanoidAvatar>();
        avatar.avatar = unityAvatar != null ? unityAvatar : LoadMannequinAvatar();
        avatar.SetRootBone((int)HumanBodyBones.Hips);
        SetInt(avatar, "humanRootBone", (int)HumanBodyBones.Hips);
        return avatar;
    }

    private static Avatar LoadMannequinAvatar()
    {
        var avatar = AssetDatabase.LoadAllAssetsAtPath(MannequinPath)
            .OfType<Avatar>()
            .FirstOrDefault(a => a.isHuman);

        if (avatar == null)
            throw new InvalidOperationException("Sample mannequin humanoid avatar was not found.");

        return avatar;
    }

    private static HumanoidAvatar EnsureRuntimeHumanoidAvatar(GameObject player)
    {
        var runtimeAnimator = FindRuntimeAnimator(player);
        if (runtimeAnimator == null || runtimeAnimator.avatar == null)
        {
            Debug.LogWarning("[MotionMatching] PlayerCharacter runtime Animator avatar was not found. Falling back to sample mannequin avatar.");
            return EnsureHumanoidAvatar();
        }

        var avatar = EnsureAsset<HumanoidAvatar>(RuntimeAvatarPath);
        SetObjectReference(avatar, "avatar", runtimeAnimator.avatar);
        SetInt(avatar, "humanRootBone", (int)HumanBodyBones.Hips);
        avatar.SetRootBone((int)HumanBodyBones.Hips);
        EditorUtility.SetDirty(avatar);
        return avatar;
    }

    private static Animator FindRuntimeAnimator(GameObject player)
    {
        return player.GetComponentsInChildren<Animator>(true)
            .FirstOrDefault(animator => animator.avatar != null && animator.gameObject != player);
    }

    private static RuntimeRig GetRuntimeRig()
    {
        var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        var player = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == PlayerName)?.gameObject;

        if (player == null)
            throw new InvalidOperationException($"GameObject '{PlayerName}' was not found in {MainScenePath}.");

        var animator = FindRuntimeAnimator(player);
        if (animator == null || animator.avatar == null)
        {
            Debug.LogWarning("[MotionMatching] PlayerCharacter runtime Animator avatar was not found. Baking with sample mannequin.");
            return new RuntimeRig(LoadMannequinAvatar(), AssetDatabase.LoadAssetAtPath<GameObject>(MannequinPath));
        }

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(animator.gameObject);
        var model = !string.IsNullOrEmpty(prefabPath)
            ? AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
            : null;

        if (model == null)
            Debug.LogWarning("[MotionMatching] PlayerCharacter runtime model prefab was not found. Baking with sample mannequin model.");

        return new RuntimeRig(animator.avatar, model);
    }

    private readonly struct RuntimeRig
    {
        public RuntimeRig(Avatar avatar, GameObject model)
        {
            this.avatar = avatar;
            this.model = model;
        }

        public readonly Avatar avatar;
        public readonly GameObject model;
    }

    private static void DisableRuntimeAnimator(GameObject player)
    {
        foreach (var animator in player.GetComponentsInChildren<Animator>(true))
        {
            if (animator.gameObject == player || animator.avatar == null)
                continue;

            animator.enabled = false;
            EditorUtility.SetDirty(animator);
        }
    }

    private static void SetupMainScene(Dataset dataset, CharacterControllerBase controller)
    {
        var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        var player = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == PlayerName)?.gameObject;

        if (player == null)
            throw new InvalidOperationException($"GameObject '{PlayerName}' was not found in {MainScenePath}.");

        var runtimeAvatar = EnsureRuntimeHumanoidAvatar(player);
        DisableRuntimeAnimator(player);

        var motionMatching = player.GetComponent<MotionMatching>() ?? player.AddComponent<MotionMatching>();
        var so = new SerializedObject(motionMatching);
        so.FindProperty("avatar").objectReferenceValue = runtimeAvatar;
        so.FindProperty("dataset").objectReferenceValue = dataset;
        so.FindProperty("characterControllerBase").objectReferenceValue = controller;
        so.FindProperty("isRunning").boolValue = dataset != null;
        so.FindProperty("wantApplyRootBonePosition").boolValue = true;
        so.FindProperty("searchRate").floatValue = 0.08f;
        so.FindProperty("animationSwitchPenalty").floatValue = 0.15f;
        so.FindProperty("responsivenessDirections").floatValue = 0.65f;
        so.FindProperty("responsivenessPositions").floatValue = 0.55f;
        var startingQuery = so.FindProperty("startingQuery");
        startingQuery.arraySize = 1;
        startingQuery.GetArrayElementAtIndex(0).stringValue = "Idle";
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static T EnsureAsset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;

        EnsureOutputFolder();
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/GameResources"))
            AssetDatabase.CreateFolder("Assets", "GameResources");
        if (!AssetDatabase.IsValidFolder("Assets/GameResources/MotionMatching"))
            AssetDatabase.CreateFolder("Assets/GameResources", "MotionMatching");
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/GameResources/MotionMatching", "Player");
    }

    private static void DeleteIfExists(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            AssetDatabase.DeleteAsset(path);
    }

    private static void SetObjectReference(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetBool(UnityEngine.Object target, string field, bool value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetInt(UnityEngine.Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static bool IsLoopClip(string name)
    {
        return name.Contains("_Loop_", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("_Loop", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Circle_Strafe", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Idle_Loop", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLowerBodyBone(string alias)
    {
        return alias.Contains("Hips", StringComparison.OrdinalIgnoreCase) ||
               alias.Contains("Leg", StringComparison.OrdinalIgnoreCase) ||
               alias.Contains("Foot", StringComparison.OrdinalIgnoreCase) ||
               alias.Contains("Toes", StringComparison.OrdinalIgnoreCase) ||
               alias.Contains("Spine", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToAssetPath(string fullPath)
    {
        string normalized = fullPath.Replace("\\", "/");
        int index = normalized.IndexOf("/Assets/", StringComparison.Ordinal);
        return index >= 0 ? normalized.Substring(index + 1) : normalized;
    }

    private static string ToClipName(string assetPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        return fileName
            .Replace("M_Neutral_", string.Empty)
            .Replace("Stand_", string.Empty);
    }

    private static string GetClipName(string path)
    {
        int separatorIndex = path.LastIndexOf("//", StringComparison.Ordinal);
        return separatorIndex >= 0 ? path.Substring(separatorIndex + 2) : ToClipName(path);
    }
}
#endif
