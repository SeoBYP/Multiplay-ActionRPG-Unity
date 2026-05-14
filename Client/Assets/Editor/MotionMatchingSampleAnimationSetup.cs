#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.System.MotionSystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Unity.Mathematics;
using UnityEngine;

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
    private const string DatabasesFolder = OutputFolder + "/Databases";
    private const string DenseDatabasesFolder = DatabasesFolder + "/Dense";
    private const string SparseDatabasesFolder = DatabasesFolder + "/Sparse";
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
        PrepareWritableOutputAssets();

        Avatar sampleAvatar = LoadMannequinAvatar();
        var avatar = CreateTransientHumanoidAvatar(sampleAvatar);
        var clips = CollectLocomotionClips(
            out var motionSearchDatabaseAssets,
            out var motionSearchDatabases);
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
                avatar,
                poseStep: 0.1f,
                futureEstimates: 3,
                futureEstimatesTime: 1.0f,
                pastEstimates: 3,
                pastEstimatesTime: 0.6f,
                databaseName: "PlayerMotionDataset",
                root: bakeRoot.transform,
                recordVelocity: 1,
                combinations: tags.combinations,
                tags: tags.motionTags,
                actionTags: tags.actionTags,
                idleTags: tags.idleTags,
                characteristics: characteristics,
                rac: animator.runtimeAnimatorController,
                motionSearchDatabaseAssets: motionSearchDatabaseAssets,
                motionSearchDatabases: motionSearchDatabases);

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

            string clipName = dataset.GetAnimationName(animID);
            string databaseName = dataset.GetDatabaseNameForAnimation(animID);

            Debug.Log(
                "[MotionMatching Dataset Diagnostics]\n" +
                $"Clip: {clipName} | Database: {databaseName} | Frames: {frames.Count} | Bones: {frameBonesCount}\n" +
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

    private static BuiltTags BuildTags(IReadOnlyList<AnimationClip> clips)
    {
        var walk = new TagBase("Walk");
        var run = new TagBase("Run");
        var walkArc = new TagBase("WalkArc");
        var runArc = new TagBase("RunArc");
        var walkCircle = new TagBase("WalkCircle");
        var runCircle = new TagBase("RunCircle");
        var directionalMotionTags = new Dictionary<string, TagBase>(StringComparer.Ordinal);
        var idle = new IdleTag("Idle", false)
        {
            initRanges = new List<TagRange>(),
            loopRanges = new List<TagRange>()
        };
        var actions = new Dictionary<string, ActionTag>(StringComparer.Ordinal)
        {
            ["WalkToRun"] = CreateInterruptibleAction("WalkToRun"),
            ["RunToWalk"] = CreateInterruptibleAction("RunToWalk"),
            ["IdleTurn"] = CreateInterruptibleAction("IdleTurn")
        };

        foreach (var clip in clips)
        {
            var range = new TagRange(clip.name, 0, int.MaxValue);
            if (clip.name.Contains("Idle_Loop", StringComparison.OrdinalIgnoreCase))
                idle.loopRanges.Add(range);
            else if (clip.name.Contains("Walk_Arc", StringComparison.OrdinalIgnoreCase))
            {
                walkArc.ranges.Add(range);
                AddSideMotionTag(directionalMotionTags, "WalkArc", clip.name, range);
            }
            else if (clip.name.Contains("Run_Arc", StringComparison.OrdinalIgnoreCase))
            {
                runArc.ranges.Add(range);
                AddSideMotionTag(directionalMotionTags, "RunArc", clip.name, range);
            }
            else if (clip.name.Contains("Walk_Circle", StringComparison.OrdinalIgnoreCase))
            {
                walkCircle.ranges.Add(range);
                AddSideMotionTag(directionalMotionTags, "WalkCircle", clip.name, range);
            }
            else if (clip.name.Contains("Run_Circle", StringComparison.OrdinalIgnoreCase))
            {
                runCircle.ranges.Add(range);
                AddSideMotionTag(directionalMotionTags, "RunCircle", clip.name, range);
            }
            else if (clip.name.Contains("Walk_Start", StringComparison.OrdinalIgnoreCase))
                AddDirectionalAction(actions, "IdleToWalk", clip.name, range);
            else if (clip.name.Contains("Run_Start", StringComparison.OrdinalIgnoreCase))
                AddDirectionalAction(actions, "IdleToRun", clip.name, range);
            else if (clip.name.Contains("Transition_Walk_to_Run", StringComparison.OrdinalIgnoreCase))
                actions["WalkToRun"].ranges.Add(range);
            else if (clip.name.Contains("Transition_Run_to_Walk", StringComparison.OrdinalIgnoreCase))
                actions["RunToWalk"].ranges.Add(range);
            else if (clip.name.Contains("Walk_Stop", StringComparison.OrdinalIgnoreCase))
                AddDirectionalAction(actions, "WalkToStop", clip.name, range);
            else if (clip.name.Contains("Run_Stop", StringComparison.OrdinalIgnoreCase))
                AddDirectionalAction(actions, "RunToStop", clip.name, range);
            else if (clip.name.Contains("Walk_Reface_Start", StringComparison.OrdinalIgnoreCase))
                AddTurnAction(actions, "WalkReface", clip.name, range);
            else if (clip.name.Contains("Run_Reface_Start", StringComparison.OrdinalIgnoreCase))
                AddTurnAction(actions, "RunReface", clip.name, range);
            else if (clip.name.Contains("Run_Turn", StringComparison.OrdinalIgnoreCase))
                AddTurnAction(actions, "RunTurn", clip.name, range);
            else if (clip.name.Contains("Walk_Pivot", StringComparison.OrdinalIgnoreCase))
                AddDirectionalAction(actions, "WalkPivot", clip.name, range);
            else if (clip.name.Contains("Run_Pivot", StringComparison.OrdinalIgnoreCase))
                AddDirectionalAction(actions, "RunPivot", clip.name, range);
            else if (clip.name.Contains("Stand_Turn", StringComparison.OrdinalIgnoreCase) ||
                     clip.name.StartsWith("Turn_", StringComparison.OrdinalIgnoreCase) ||
                     clip.name.Contains("Idle_turn", StringComparison.OrdinalIgnoreCase))
                actions["IdleTurn"].ranges.Add(range);
            else if (clip.name.Contains("Walk", StringComparison.OrdinalIgnoreCase))
            {
                walk.ranges.Add(range);
                AddDirectionalMotionTag(directionalMotionTags, "Walk", clip.name, range);
            }
            else if (clip.name.Contains("Run", StringComparison.OrdinalIgnoreCase))
            {
                run.ranges.Add(range);
                AddDirectionalMotionTag(directionalMotionTags, "Run", clip.name, range);
            }
        }

        var motionTags = new[] { walk, run, walkArc, runArc, walkCircle, runCircle }
            .Where(tag => tag.ranges.Count > 0)
            .Concat(directionalMotionTags.Values.Where(tag => tag.ranges.Count > 0))
            .ToList();
        var combinations = motionTags
            .Select(tag => new List<TagBase> { tag })
            .ToList();
        var actionTags = actions.Values
            .Where(tag => tag.ranges.Count > 0)
            .ToList();

        Debug.Log(
            "[MotionMatching] Built sample tags: " +
            $"motions={string.Join(", ", motionTags.Select(tag => $"{tag.name}:{tag.ranges.Count}"))}, " +
            $"actions={string.Join(", ", actionTags.Select(tag => $"{tag.name}:{tag.ranges.Count}"))}, " +
            $"idle={idle.loopRanges.Count}");

        return new BuiltTags(motionTags, combinations, actionTags, new List<IdleTag> { idle });
    }

    private static List<AnimationClip> CollectLocomotionClips(
        out List<MotionSearchDatabaseAsset> motionSearchDatabaseAssets,
        out List<MotionSearchDatabaseBakeRecord> motionSearchDatabases)
    {
        var includedDatabases = LoadIncludedMotionSearchDatabases();
        motionSearchDatabaseAssets = includedDatabases.ToList();
        var selectedPaths = includedDatabases
            .SelectMany(database => database.animations.Where(clip => clip != null))
            .Select(AssetDatabase.GetAssetPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .Distinct()
            .OrderBy(path => path)
            .ToList();
        if (selectedPaths.Count == 0)
            throw new InvalidOperationException("No MotionSearchDatabase assets with included animations were found.");

        var clips = new List<AnimationClip>();
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
        }

        motionSearchDatabases = BuildDatabaseBakeRecords(includedDatabases, selectedPaths);

        return clips;
    }

    private static List<MotionSearchDatabaseAsset> LoadIncludedMotionSearchDatabases()
    {
        EnsurePoseSearchDatabaseAssets();
        return AssetDatabase.FindAssets("t:MotionSearchDatabaseAsset", new[] { DatabasesFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<MotionSearchDatabaseAsset>)
            .Where(database => database != null && database.includeInBake)
            .OrderBy(database => database.group)
            .ThenBy(database => database.role)
            .ThenBy(database => database.name)
            .ToList();
    }

    private static List<MotionSearchDatabaseBakeRecord> BuildDatabaseBakeRecords(
        IReadOnlyList<MotionSearchDatabaseAsset> databases,
        IReadOnlyList<string> selectedPaths)
    {
        var animationIDByPath = selectedPaths
            .Select((path, index) => new { path, index })
            .ToDictionary(item => item.path, item => item.index);

        return databases.Select(database =>
        {
            var record = new MotionSearchDatabaseBakeRecord
            {
                name = database.name,
                group = database.group,
                role = database.role,
                animationIDs = new List<int>(),
                animationPaths = new List<string>()
            };

            foreach (var path in database.animations
                         .Where(clip => clip != null)
                         .Select(AssetDatabase.GetAssetPath)
                         .Where(path => !string.IsNullOrEmpty(path))
                         .Distinct()
                         .OrderBy(path => path))
            {
                if (!animationIDByPath.TryGetValue(path, out int animationID))
                    continue;

                record.animationIDs.Add(animationID);
                record.animationPaths.Add(path);
            }

            return record;
        }).ToList();
    }

    private static void EnsurePoseSearchDatabaseAssets()
    {
        EnsureDatabaseFolders();

        var definitions = new[]
        {
            new DatabaseDefinition("PSD_Dense_Stand_Idles", DenseDatabasesFolder, MotionSearchDatabaseGroup.Dense, MotionSearchDatabaseRole.Idle, true,
                path => FileName(path).Contains("Stand_Idle_Loop", StringComparison.OrdinalIgnoreCase) ||
                        FileName(path).Contains("Idle_turn", StringComparison.OrdinalIgnoreCase) ||
                        FileName(path).Contains("Stand_Turn", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Dense_Stand_Walk_Loops", DenseDatabasesFolder, MotionSearchDatabaseGroup.Dense, MotionSearchDatabaseRole.Loop, true,
                path => FileName(path).Contains("_Walk_Loop_", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Dense_Stand_Run_Loops", DenseDatabasesFolder, MotionSearchDatabaseGroup.Dense, MotionSearchDatabaseRole.Loop, true,
                path => FileName(path).Contains("_Run_Loop_", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Dense_Stand_Walk_Starts", DenseDatabasesFolder, MotionSearchDatabaseGroup.Dense, MotionSearchDatabaseRole.Start, true,
                path => FileName(path).Contains("_Walk_Start_", StringComparison.OrdinalIgnoreCase) ||
                        FileName(path).Contains("_Walk_Reface_Start_", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Dense_Stand_Run_Starts", DenseDatabasesFolder, MotionSearchDatabaseGroup.Dense, MotionSearchDatabaseRole.Start, true,
                path => FileName(path).Contains("_Run_Start_", StringComparison.OrdinalIgnoreCase) ||
                        FileName(path).Contains("_Run_Reface_Start_", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Dense_Stand_Walk_Stops", DenseDatabasesFolder, MotionSearchDatabaseGroup.Dense, MotionSearchDatabaseRole.Stop, true,
                path => FileName(path).Contains("_Walk_Stop_", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Dense_Stand_Run_Stops", DenseDatabasesFolder, MotionSearchDatabaseGroup.Dense, MotionSearchDatabaseRole.Stop, true,
                path => FileName(path).Contains("_Run_Stop_", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Dense_Stand_Walk_Pivots", DenseDatabasesFolder, MotionSearchDatabaseGroup.Dense, MotionSearchDatabaseRole.Pivot, true,
                path => FileName(path).Contains("_Walk_Pivot_", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Dense_Stand_Run_Pivots", DenseDatabasesFolder, MotionSearchDatabaseGroup.Dense, MotionSearchDatabaseRole.Pivot, true,
                path => FileName(path).Contains("_Run_Pivot_", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Dense_Stand_Run_SpinTransition", DenseDatabasesFolder, MotionSearchDatabaseGroup.Dense, MotionSearchDatabaseRole.Transition, true,
                path => FileName(path).Contains("_Run_Turn_", StringComparison.OrdinalIgnoreCase) ||
                        FileName(path).Contains("_Run_Spin_", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Dense_Stand_Arcs", DenseDatabasesFolder, MotionSearchDatabaseGroup.Dense, MotionSearchDatabaseRole.Arc, true,
                path => FileName(path).Contains("_Walk_Arc_", StringComparison.OrdinalIgnoreCase) ||
                        FileName(path).Contains("_Run_Arc_", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Dense_Stand_Circles", DenseDatabasesFolder, MotionSearchDatabaseGroup.Dense, MotionSearchDatabaseRole.Circle, true,
                path => FileName(path).Contains("_Walk_Circle_Strafe_", StringComparison.OrdinalIgnoreCase) ||
                        FileName(path).Contains("_Run_Circle_Strafe_", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Dense_Stand_WalkToRun", DenseDatabasesFolder, MotionSearchDatabaseGroup.Dense, MotionSearchDatabaseRole.Transition, true,
                path => FileName(path).Contains("Transition_Walk_to_Run", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Dense_Stand_RunToWalk", DenseDatabasesFolder, MotionSearchDatabaseGroup.Dense, MotionSearchDatabaseRole.Transition, true,
                path => FileName(path).Contains("Transition_Run_to_Walk", StringComparison.OrdinalIgnoreCase)),

            new DatabaseDefinition("PSD_Sparse_Stand_Walk_Loops", SparseDatabasesFolder, MotionSearchDatabaseGroup.Sparse, MotionSearchDatabaseRole.Loop, false,
                path => FileName(path).Contains("_Walk_Loop_F", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Sparse_Stand_Run_Loops", SparseDatabasesFolder, MotionSearchDatabaseGroup.Sparse, MotionSearchDatabaseRole.Loop, false,
                path => FileName(path).Contains("_Run_Loop_F", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Sparse_Stand_Walk_Starts", SparseDatabasesFolder, MotionSearchDatabaseGroup.Sparse, MotionSearchDatabaseRole.Start, false,
                path => FileName(path).Contains("_Walk_Start_F", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Sparse_Stand_Run_Starts", SparseDatabasesFolder, MotionSearchDatabaseGroup.Sparse, MotionSearchDatabaseRole.Start, false,
                path => FileName(path).Contains("_Run_Start_F", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Sparse_Stand_Walk_Stops", SparseDatabasesFolder, MotionSearchDatabaseGroup.Sparse, MotionSearchDatabaseRole.Stop, false,
                path => FileName(path).Contains("_Walk_Stop_F", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Sparse_Stand_Run_Stops", SparseDatabasesFolder, MotionSearchDatabaseGroup.Sparse, MotionSearchDatabaseRole.Stop, false,
                path => FileName(path).Contains("_Run_Stop_F", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Sparse_Stand_Walk_Pivots", SparseDatabasesFolder, MotionSearchDatabaseGroup.Sparse, MotionSearchDatabaseRole.Pivot, false,
                path => FileName(path).Contains("_Walk_Pivot_F", StringComparison.OrdinalIgnoreCase)),
            new DatabaseDefinition("PSD_Sparse_Stand_Run_Pivots", SparseDatabasesFolder, MotionSearchDatabaseGroup.Sparse, MotionSearchDatabaseRole.Pivot, false,
                path => FileName(path).Contains("_Run_Pivot_F", StringComparison.OrdinalIgnoreCase))
        };

        var allAnimationPaths = Directory.GetFiles(SampleRoot, "*.FBX", SearchOption.AllDirectories)
            .Select(ToAssetPath)
            .Where(path => path != MannequinPath)
            .ToList();

        foreach (var definition in definitions)
        {
            string assetPath = $"{definition.folder}/{definition.name}.asset";
            var database = AssetDatabase.LoadAssetAtPath<MotionSearchDatabaseAsset>(assetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<MotionSearchDatabaseAsset>();
                AssetDatabase.CreateAsset(database, assetPath);
            }

            database.group = definition.group;
            database.role = definition.role;
            database.includeInBake = definition.includeInBake;
            database.animations = allAnimationPaths
                .Where(definition.predicate)
                .Select(path => AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)))
                .Where(clip => clip != null)
                .Distinct()
                .OrderBy(clip => AssetDatabase.GetAssetPath(clip))
                .ToList();
            EditorUtility.SetDirty(database);
        }

        AssetDatabase.SaveAssets();
    }

    private static void EnsureDatabaseFolders()
    {
        EnsureOutputFolder();
        if (!AssetDatabase.IsValidFolder(DatabasesFolder))
            AssetDatabase.CreateFolder(OutputFolder, "Databases");
        if (!AssetDatabase.IsValidFolder(DenseDatabasesFolder))
            AssetDatabase.CreateFolder(DatabasesFolder, "Dense");
        if (!AssetDatabase.IsValidFolder(SparseDatabasesFolder))
            AssetDatabase.CreateFolder(DatabasesFolder, "Sparse");
    }

    private static string FileName(string path)
    {
        return Path.GetFileNameWithoutExtension(path);
    }

    private static ActionTag CreateInterruptibleAction(string name)
    {
        return new ActionTag(name)
        {
            isInterruptibleByState = new[] { true, true, true },
            interruptibleType = InterruptibleBy.All
        };
    }

    private static void AddDirectionalAction(
        Dictionary<string, ActionTag> actions,
        string baseName,
        string clipName,
        TagRange range)
    {
        string direction = ExtractDirectionalActionSuffix(baseName, clipName);
        string actionName = string.IsNullOrEmpty(direction) ? baseName : baseName + direction;

        if (!actions.TryGetValue(actionName, out var action))
        {
            action = CreateInterruptibleAction(actionName);
            actions[actionName] = action;
        }

        action.ranges.Add(range);
    }

    private static void AddTurnAction(
        Dictionary<string, ActionTag> actions,
        string baseName,
        string clipName,
        TagRange range)
    {
        string suffix = ExtractTurnSuffix(clipName);
        string actionName = string.IsNullOrEmpty(suffix) ? baseName : baseName + suffix;

        if (!actions.TryGetValue(actionName, out var action))
        {
            action = CreateInterruptibleAction(actionName);
            actions[actionName] = action;
        }

        action.ranges.Add(range);
    }

    private static void AddDirectionalMotionTag(
        Dictionary<string, TagBase> tags,
        string baseName,
        string clipName,
        TagRange range)
    {
        string direction = ExtractLoopDirection(clipName);
        if (string.IsNullOrEmpty(direction))
            return;

        string tagName = baseName + direction;
        if (!tags.TryGetValue(tagName, out var tag))
        {
            tag = new TagBase(tagName);
            tags[tagName] = tag;
        }

        tag.ranges.Add(range);
    }

    private static void AddSideMotionTag(
        Dictionary<string, TagBase> tags,
        string baseName,
        string clipName,
        TagRange range)
    {
        string side = ExtractSideSuffix(clipName);
        if (string.IsNullOrEmpty(side))
            return;

        string tagName = baseName + side;
        if (!tags.TryGetValue(tagName, out var tag))
        {
            tag = new TagBase(tagName);
            tags[tagName] = tag;
        }

        tag.ranges.Add(range);
    }

    private static string ExtractLoopDirection(string clipName)
    {
        int tokenIndex = clipName.IndexOf("Loop_", StringComparison.OrdinalIgnoreCase);
        if (tokenIndex < 0)
            return string.Empty;

        int start = tokenIndex + "Loop_".Length;
        int end = clipName.IndexOf('_', start);
        string direction = end < 0
            ? clipName.Substring(start)
            : clipName.Substring(start, end - start);

        return direction switch
        {
            "F" or "FR" or "RR" or "BR" or "B" or "BL" or "LL" or "FL" or "LR" or "RL" => direction,
            _ => string.Empty
        };
    }

    private static string ExtractDirectionalActionSuffix(string baseName, string clipName)
    {
        if (baseName.EndsWith("Pivot", StringComparison.OrdinalIgnoreCase))
            return ExtractBetweenMotionTokenAndFoot(clipName, "Pivot_");

        if (baseName.EndsWith("Stop", StringComparison.OrdinalIgnoreCase))
            return ExtractBetweenMotionTokenAndFoot(clipName, "Stop_");

        if (baseName.StartsWith("IdleTo", StringComparison.OrdinalIgnoreCase))
            return ExtractBetweenMotionTokenAndFoot(clipName, "Start_");

        return string.Empty;
    }

    private static string ExtractTurnSuffix(string clipName)
    {
        Match match = Regex.Match(
            clipName,
            @"_(?:Run_Turn|(?:Walk|Run)_Reface_Start)_(?:F_|B_)?(?<side>L|R)_(?<angle>045|090|135|180)",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            return string.Empty;

        return match.Groups["side"].Value.ToUpperInvariant() + match.Groups["angle"].Value;
    }

    private static string ExtractSideSuffix(string clipName)
    {
        Match match = Regex.Match(clipName, @"_(?<side>L|R)(?:foot)?$", RegexOptions.IgnoreCase);
        if (!match.Success)
            return string.Empty;

        return match.Groups["side"].Value.ToUpperInvariant();
    }

    private static string ExtractBetweenMotionTokenAndFoot(string clipName, string token)
    {
        int tokenIndex = clipName.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (tokenIndex < 0)
            return string.Empty;

        int start = tokenIndex + token.Length;
        int end = clipName.IndexOf("_Lfoot", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
            end = clipName.IndexOf("_Rfoot", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
            end = clipName.Length;

        return clipName.Substring(start, end - start).Replace("_", string.Empty);
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

    private readonly struct BuiltTags
    {
        public BuiltTags(
            List<TagBase> motionTags,
            List<List<TagBase>> combinations,
            List<ActionTag> actionTags,
            List<IdleTag> idleTags)
        {
            this.motionTags = motionTags;
            this.combinations = combinations;
            this.actionTags = actionTags;
            this.idleTags = idleTags;
        }

        public readonly List<TagBase> motionTags;
        public readonly List<List<TagBase>> combinations;
        public readonly List<ActionTag> actionTags;
        public readonly List<IdleTag> idleTags;
    }

    private readonly struct DatabaseDefinition
    {
        public DatabaseDefinition(
            string name,
            string folder,
            MotionSearchDatabaseGroup group,
            MotionSearchDatabaseRole role,
            bool includeInBake,
            Func<string, bool> predicate)
        {
            this.name = name;
            this.folder = folder;
            this.group = group;
            this.role = role;
            this.includeInBake = includeInBake;
            this.predicate = predicate;
        }

        public readonly string name;
        public readonly string folder;
        public readonly MotionSearchDatabaseGroup group;
        public readonly MotionSearchDatabaseRole role;
        public readonly bool includeInBake;
        public readonly Func<string, bool> predicate;
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

    private static void PrepareWritableOutputAssets()
    {
        foreach (string path in new[] { DatasetPath, AvatarPath, CharacteristicsPath, QueriesPath, TagsPath })
        {
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                continue;

            var attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.ReadOnly) == 0)
                continue;

            File.SetAttributes(fullPath, attributes & ~FileAttributes.ReadOnly);
        }
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
}
#endif
