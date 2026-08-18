using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using InfinityPBR;
using MagicPigGames.Shared;
using UnityEngine.UI;
using static InfinityPBR.EquipmentSystemStatic;

namespace InfinityPBR
{
    [CustomEditor(typeof(PrefabAndObjectManager))]
    [CanEditMultipleObjects]
    [Serializable]
    public class PrefabAndObjectManagerEditor : MagicPigEditor
    {
        private Color inactiveColor2 = new Color(0.75f, .75f, 0.75f, 1f);
        private Color activeColor = new Color(0.6f, 1f, 0.6f, 1f);
        private Color activeColor2 = new Color(0.0f, 1f, 0.0f, 1f);
        private Color mixedColor = Color.yellow;
        private Color redColor = new Color(1f, 0.25f, 0.25f, 1f);

        // Cache group type names to avoid rebuilding every GUI pass
        private List<string> _cachedGroupTypeNames;
        // Cache all labels once on enable
        private static List<string> _cachedAllLabels;

        // Per-repaint cache so ColorSet + activate/deactivate button don't each re-run GroupIsActive's LINQ passes
        private readonly Dictionary<PrefabGroup, int> _groupActiveCache = new Dictionary<PrefabGroup, int>();
        // Per-repaint cache: GroupsOfType(string) → list; rebuilt at top of OnInspectorGUI
        private readonly Dictionary<string, List<PrefabGroup>> _groupsOfTypeCache = new Dictionary<string, List<PrefabGroup>>();
        // Per-repaint cache: CanRandomize(type) result; CanRandomize itself does two LINQ Count() passes
        private readonly Dictionary<string, bool> _canRandomizeCache = new Dictionary<string, bool>();
        // Per-repaint cache of variable name array, used by every group-object popup that lacks a variable
        private string[] _variableNamesCache;

        // Cache for expensive AssetDatabase searches (keyed by labelMask + search)
        private static readonly Dictionary<string, (double timestamp, UnityEngine.Object[] results)> _searchCache
            = new Dictionary<string, (double, UnityEngine.Object[])>();

        // How long to keep cache entries alive (in seconds)
        private const double SearchCacheTtlSeconds = 5.0;
        private string BuildSearchKey(int labelMask, string search)
        {
            return $"{labelMask}|{search}";
        }

        private UnityEngine.Object[] FindAssetsByLabelFast(int labelMask, string search)
        {
            // Build label part of the query from cached labels
            var labels = _cachedAllLabels;
            var labelFilters = new List<string>();
            for (int i = 0; i < labels.Count; i++)
            {
                if ((labelMask & (1 << i)) != 0)
                    labelFilters.Add($"l:{labels[i]}");
            }

            // Constrain to GameObjects/Prefabs to avoid scanning audio and other asset types
            // (Unity treats Prefabs as GameObjects for FindAssets type filtering)
            var typeFilter = "t:GameObject";

            // Name/search filtering — keep simple; AssetDatabase supports bare tokens as name filters
            var searchFilter = string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim();

            var query = string.Join(" ", labelFilters);
            if (!string.IsNullOrEmpty(typeFilter)) query = string.IsNullOrEmpty(query) ? typeFilter : $"{query} {typeFilter}";
            if (!string.IsNullOrEmpty(searchFilter)) query = string.IsNullOrEmpty(query) ? searchFilter : $"{query} {searchFilter}";

            var guids = AssetDatabase.FindAssets(query);
            var results = new List<UnityEngine.Object>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (obj is GameObject)
                    results.Add(obj);
            }

            return results.ToArray();
        }

        private UnityEngine.Object[] FindAssetsByLabelCached(int labelMask, string search)
        {
            string key = BuildSearchKey(labelMask, search);
            if (_searchCache.TryGetValue(key, out var entry))
            {
                if (EditorApplication.timeSinceStartup - entry.timestamp <= SearchCacheTtlSeconds)
                    return entry.results;
                _searchCache.Remove(key);
            }

            var results = FindAssetsByLabelFast(labelMask, search);
            _searchCache[key] = (EditorApplication.timeSinceStartup, results);
            return results;
        }

        private WardrobePrefabManager WardrobePrefabManager => Manager.WardrobePrefabManager;
        private BlendShapesManager BlendShapesManager => Manager.BlendShapesManager;

        private PrefabAndObjectManager Manager => GetManager();
        private PrefabAndObjectManager _prefabAndObjectManager;
        private List<string> GroupTypeNames => _cachedGroupTypeNames;
        public List<PrefabObjectVariable> variables = new List<PrefabObjectVariable>();

        private PrefabGroup _activateGroup;
        private PrefabGroup _deactivateGroup;

        private bool _cachedEquipmentObjects = false;
        private List<GameObject> _equipmentObjects = new List<GameObject>();

        private string newVariableName = "New Variable";
        private int variableIndex = 0;

        private void CacheEquipmentObjects()
        {
            _equipmentObjects = EquipmentObjectObjects();
            Manager.equipmentObjects = _equipmentObjects;
            _cachedEquipmentObjects = true;
            Debug.Log($"<color=#00ff00>Cache Successful!</color> {_equipmentObjects.Count} Equipment Objects found.");
        }

        private PrefabAndObjectManager GetManager()
        {
            if (_prefabAndObjectManager != null) return _prefabAndObjectManager;
            _prefabAndObjectManager = (PrefabAndObjectManager) target;
            return _prefabAndObjectManager;
        }


        public Color ColorSet(int g) => ColorSet(Manager.prefabGroups[g]);

        public Color ColorSet(PrefabGroup group)
        {
            int v = GroupIsActiveCached(group);
            if (v == 2)
                return activeColor2;
            if (v == 1)
                return mixedColor;
            return Color.white;
        }

        private int GroupIsActiveCached(PrefabGroup group)
        {
            if (_groupActiveCache.TryGetValue(group, out int cached))
                return cached;
            int v = Manager.GroupIsActive(group);
            _groupActiveCache[group] = v;
            return v;
        }

        private List<PrefabGroup> GroupsOfTypeCached(string groupType)
        {
            // Normalize key for whitespace types (treated as "no type")
            string key = string.IsNullOrWhiteSpace(groupType) ? string.Empty : groupType;
            if (_groupsOfTypeCache.TryGetValue(key, out var cached))
                return cached;
            var list = GroupsOfType(groupType);
            _groupsOfTypeCache[key] = list;
            return list;
        }

        private bool CanRandomizeCached(string typeName)
        {
            string key = typeName ?? string.Empty;
            if (_canRandomizeCache.TryGetValue(key, out bool cached))
                return cached;
            bool v = Manager.CanRandomize(typeName);
            _canRandomizeCache[key] = v;
            return v;
        }

        private string[] VariableNamesCached()
        {
            if (_variableNamesCache != null) return _variableNamesCache;
            var vars = Manager.variables;
            var arr = new string[vars.Count];
            for (int i = 0; i < vars.Count; i++) arr[i] = vars[i].name;
            _variableNamesCache = arr;
            return arr;
        }

        private void DefaultEditorBool(string optionString, bool value = true)
        {
            if (EditorPrefs.HasKey(optionString)) return;
            EditorPrefs.SetBool(optionString, value);
        }

        public void OnEnable()
        {
            _cachedEquipmentObjects = false;
            
            if (GetBool("Auto Find Equipment Objects On Enable"))
                CacheEquipmentObjects();

            RemoveMissingObjects();
            Manager.MarkPrefabs();
            Manager.SetUid();
            ReloadSources();
            
            // Read labels from the shared cache. The cache is rebuilt only when project assets actually
            // change (see MagicPigLabelCacheInvalidator) instead of forcing a full project rescan on every enable.
            _cachedGroupTypeNames = Manager.GetGroupTypeNames();
            _cachedAllLabels = MagicPigStatic.GetAllLabels();

            Undo.undoRedoPerformed += UndoCallback;
            SetBool("Reset Since Load Equipment Object", false);
        }

        private void RemoveMissingObjects()
        {
            foreach (var group in Manager.prefabGroups)
            {
                group.groupObjects.RemoveAll(x => x.objectToHandle == null);
            }
        }
        
        private void ReloadSources(){
            EnsureTypeNames();
            DoCache(true);
            if (BlendShapesManager)
                BlendShapesManager.BuildMatchLists();
        }

        void UndoCallback()
        {
            DoCache();
        }

        // cacheGroups is retained for call-site compatibility; the old per-group caching subsystem was unused and removed.
        private void DoCache(bool cacheGroups = false)
        {
            CacheTypes();
        }

        private void EnsureTypeNames()
        {
            foreach (var group in Manager.prefabGroups)
            {
                if (!String.IsNullOrWhiteSpace(group.groupType)) continue;
                group.groupType = null;
            }
        }

        private void GroupActivateDeactivate()
        {
            if (_deactivateGroup != null)
                Manager.DeactivateGroup(_deactivateGroup);
            if (_activateGroup != null)
                Manager.ActivateGroup(_activateGroup);

            _deactivateGroup = null;
            _activateGroup = null;
        }

        public override void OnInspectorGUI()
        {
            // Skip Undo.RecordObject on Repaint/Layout — recording every frame bloated the undo stack into hundreds of MB.
            var evtType = Event.current.type;
            if (evtType != EventType.Repaint && evtType != EventType.Layout)
                Undo.RecordObject(Manager, "Undo Changes");

            EditorGUI.BeginChangeCheck();
            GroupActivateDeactivate();

            // Utility actions
            SetDefaultEditorPrefs();

            _groupActiveCache.Clear();
            _groupsOfTypeCache.Clear();
            _canRandomizeCache.Clear();
            _variableNamesCache = null;

            LinkToDocs("https://infinitypbr.gitbook.io/infinity-pbr/equipment-systems/prefab-and-object-manager");
            Space();

            HelpBoxMessage("PREFAB AND OBJECT MANAGER\n" +
                           "This inspector script is intended to make it easier to assign groups of prefabs and objects, and" +
                           " activate / deactivate them as a group. This could be helpful for managing modular " +
                           "wardrobe or other objects, such as props inside a room.\n\n" +
                           "The script handles instantiating and destroying prefabs as well as activating and deactivating " +
                           "objects already in your scene. Each group can handle any number of both types of objects.");

            //Space();
            //ShowUpdate();
            Space();
            
            // Inspector Drawing
            //Undo.RecordObject(Manager, "Undo Button Changes");
            SectionButtons();
            Space();

            //Undo.RecordObject(Manager, "Undo Setup & Option Changes");
            SetupAndOptions();
            
            ShowVariables();

            //Undo.RecordObject(Manager, "Undo Object Delete");
            GroupTypes();

            //Undo.RecordObject(Manager, "Show Prefab Group OPtions");
            ShowPrefabGroups();
            
            Space();
            

            ResetColor();
            DrawDefaultInspectorToggle("Prefab and Object Manager");

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(Manager);
            /*
            PrefabUtility.RecordPrefabInstancePropertyModifications(Manager.gameObject);
            */
        }

        private void ShowActionButtons()
        {
            if (Button("Randomize All", 100))
                Manager.ActivateRandomAllGroups();
        }

        /*
         * This shows the buttons to load the various panels
         */
        private void SectionButtons()
        {
            // Cache values
            var tempGroups = EditorPrefs.GetBool("Prefab Manager Show Prefab Groups");
            var tempVariables = EditorPrefs.GetBool("Prefab Manager Show Variables");
            var tempTypes = EditorPrefs.GetBool("Prefab Manager Show Group Types");
            var tempSetup = EditorPrefs.GetBool("Prefab Manager Show Setup And Options");
            
            // Show buttons
            EditorGUILayout.BeginHorizontal();
            SectionButton($"Prefab Groups ({Manager.prefabGroups.Count})", "Prefab Manager Show Prefab Groups");
            SectionButton($"Variables ({Manager.variables.Count})", "Prefab Manager Show Variables");
            SectionButton($"Group Types ({GroupTypeNames.Count})", "Prefab Manager Show Group Types");
            SectionButton("Setup & Options", "Prefab Manager Show Setup And Options");
            EditorGUILayout.EndHorizontal();
            
            // Check for changes -- Ensure others are turned off
            if (!tempGroups && EditorPrefs.GetBool("Prefab Manager Show Prefab Groups"))
            {
                EditorPrefs.SetBool("Prefab Manager Show Group Types", false);
                EditorPrefs.SetBool("Prefab Manager Show Setup And Options", false);
                EditorPrefs.SetBool("Prefab Manager Show Variables", false);
            }
            if (!tempTypes && EditorPrefs.GetBool("Prefab Manager Show Group Types"))
            {
                EditorPrefs.SetBool("Prefab Manager Show Prefab Groups", false);
                EditorPrefs.SetBool("Prefab Manager Show Setup And Options", false);
                EditorPrefs.SetBool("Prefab Manager Show Variables", false);
            }
            if (!tempSetup && EditorPrefs.GetBool("Prefab Manager Show Setup And Options"))
            {
                EditorPrefs.SetBool("Prefab Manager Show Group Types", false);
                EditorPrefs.SetBool("Prefab Manager Show Prefab Groups", false);
                EditorPrefs.SetBool("Prefab Manager Show Variables", false);
            }
            if (!tempVariables && EditorPrefs.GetBool("Prefab Manager Show Variables"))
            {
                EditorPrefs.SetBool("Prefab Manager Show Group Types", false);
                EditorPrefs.SetBool("Prefab Manager Show Prefab Groups", false);
                EditorPrefs.SetBool("Prefab Manager Show Setup And Options", false);
            }
        }
        
        private void ShowVariables()
        {
            if (!EditorPrefs.GetBool("Prefab Manager Show Variables")) return;

            HelpBoxMessage("Add variables which can be used to help determine which objects are turned on or " +
                           "instantiated when a group is loaded.", MessageType.Info);

            if (Manager.variables.Count > 0)
                DisplayVariableHeader();
            
            foreach (var variable in Manager.variables)
                DisplayVariable(variable);

            Space();
            BackgroundColor(Color.yellow);
            StartRow();
            newVariableName = TextField(GetNewVariableName(), 150);
            if (Button("Add New Variable", 150))
                Manager.variables.Add(new PrefabObjectVariable(newVariableName));
            EndRow();
            ResetColor();
        }

        private void DisplayVariable(PrefabObjectVariable variable)
        {
            var tempName = variable.name;
            var tempBool = variable.valueBool;
            var tempFloat = variable.valueFloat;
            var tempString = variable.valueString;
            
            StartRow();
            variable.name = TextField(variable.name, 150);
            variable.valueBool = Check(variable.valueBool, 50);
            variable.valueFloat = Float(variable.valueFloat, 50);
            variable.valueString = TextField(variable.valueString, 150);
            BackgroundColor(Color.red);
            if (Button(symbolX, 25))
            {
                RemoveGroupVariables(variable.name);
                Manager.variables.RemoveAll(x => x.name == variable.name);
                ExitGUI();
            }
            ResetColor();
            EndRow();

            if (variable.name != tempName)
            {
                if (Manager.variables.Count(x => x.name == variable.name) > 1)
                {
                    variable.name = tempName;
                    Debug.LogWarning("Each variable name must be unique");
                }
                else
                {
                    UpdateGroupVariableNames(tempName, variable.name);
                }
            }

            if (variable.valueBool != tempBool
                || variable.valueFloat != tempFloat
                || variable.valueString != tempString)
            {
                Manager.ReloadActiveGroups();
            }
        }

        private void RemoveGroupVariables(string variableName)
        {
            foreach (var variable in Manager.prefabGroups.SelectMany(x => x.groupObjects)
                .Where(x => x.variable.name == variableName).Select(x => x.variable))
            {
                variable.name = "";
            }
        }

        private void UpdateGroupVariableNames(string oldName, string newName)
        {
            foreach (var obj in Manager.prefabGroups.SelectMany(x => x.groupObjects))
            {
                if (obj.variable.name != oldName) continue;
                obj.variable.name = newName;
            }
        }

        private void DisplayVariableHeader()
        {
            StartRow();
            Label("Variable Name", 150, true);
            Label("Bool", 50, true);
            Label("Float", 50, true);
            Label("String", 150, true);
            EndRow();
        }

        private string GetNewVariableName()
        {
            if (String.IsNullOrEmpty(newVariableName)) return "New Variable";
            if (Manager.variables.FirstOrDefault(x => x.name == newVariableName) == null) return newVariableName;

            var i = 1;
            while (Manager.variables.FirstOrDefault(x => x.name == $"New Variable {i}") != null)
                i++;

            return $"New Variable {i}";
        }

        private void GroupTypes()
        {
            if (!EditorPrefs.GetBool("Prefab Manager Show Group Types")) return;

            HelpBoxMessage("Organize your groups into types, often used to ensure that only one group " +
                           "of each type is active at a time. An example would be \"Hair\" for characters, or " +
                           "perhaps \"Table Items\" for props on the top of a table. You can update the name of a type" +
                           "here.\n\n" +
                           "To add a new type, simply write it into the \"Type\" field when viewing your prefab groups. " +
                           "Each group starts without a type.\n\n" +
                           "To delete a type, remove all groups of that type, or change all groups to a different type.", MessageType.Info);

            foreach (var typeName in GroupTypeNames)
                DisplayGroupType(typeName);
        }

        private void DisplayGroupType(string typeName)
        {
            if (String.IsNullOrWhiteSpace(typeName)) return;
            
            var oldName = typeName;
            var newName = EditorGUILayout.DelayedTextField(typeName, GUILayout.Width(250));
            if (oldName == newName) return;

            if (!UpdateGroupTypeName(oldName, newName))
                return;

            EditorPrefs.SetBool($"Prefab Manager Show Type {newName}", true);
        }

        /*
         * This will check to see if we can update the type name. If so, will change all the groups of the type to
         * the new name.
         */
        private bool UpdateGroupTypeName(string oldName, string newName)
        {
            newName = newName.Trim(); // Remove any whitespace before / after the content
            if (String.IsNullOrWhiteSpace(newName)) return false; // If it's empty, return
            if (oldName == newName) return false; // If we didn't change anything, return
            if (GroupTypeNames.Count(x => x == newName) > 0) // If we already have a type of that name, return
            {
                Debug.LogWarning($"Error: {newName} already exists!");
                return false;
            }

            // Make the update on all existing prefab groups
            foreach (var group in Manager.prefabGroups)
            {
                if (group.groupType != oldName) continue;
                group.groupType = newName;
            }

            CacheTypes(); // force the types cache to reload
            
            return true;
        }

        private void CacheTypes(bool value = true) => Manager.cacheTypes = value;

        
        
        private void DefaultInspector()
        {
            if (!EditorPrefs.GetBool("Prefab Manager Show Full Inspector")) return;
            
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private void ShowPrefabGroups()
        {
            if (!EditorPrefs.GetBool("Prefab Manager Show Prefab Groups"))
            {
                return;
            }
            
            Line();
            
            StartRow();
            BackgroundColor(Color.yellow);
            if (Button("Create New Group (with no type)", 200))
            {
                Manager.CreateNewPrefabGroup();
                DoCache();
            }
            ResetColor();

            Label("", 20);
            ShowActionButtons();
            
            EndRow();
            ShowRequiredLabels();
            Line();

            foreach(var typeName in GroupTypeNames)
            {
                StartVerticalBox();
                ShowGroupsOfType(typeName);
                EndVerticalBox();
                Space();
            }
        }

        private void CollapseAllTypes()
        {
            foreach (var typeName in GroupTypeNames)
            {
                var prefsString = $"Prefab Manager Show Type {typeName}";
                SetBool(prefsString, false);
            }
        }

        private List<PrefabGroup> GroupsOfType(string groupType) => String.IsNullOrWhiteSpace(groupType) ? Manager.prefabGroups.Where(x => String.IsNullOrWhiteSpace(x.groupType)).ToList() : Manager.prefabGroups.Where(x => x.groupType == groupType).ToList();

        private void ShowGroupsOfType(string typeName = "")
        {
            var groupsOfType = GroupsOfTypeCached(typeName);
            var typeDetails = $"{groupsOfType.Count} groups";
            var prefsString = $"Prefab Manager Show Type {typeName}";
            bool canRandomize = CanRandomizeCached(typeName);


            StartRow();
            ColorsIf(GetBool(prefsString), Color.green, Color.grey, Color.white, Color.white);
            if (Button($"{(GetBool(prefsString) ? symbolCircleOpen : symbolDash)}", 25))
            {
                CollapseAllTypes();
                ToggleBool(prefsString);
            }
            ContentColor(Color.white);
            LabelBig($"{(!String.IsNullOrWhiteSpace(typeName) ? $"{typeName}" : "[No type]")} ({typeDetails})", 200, 14,true);
            ContentColorIf(canRandomize, Color.white, Color.grey);
            if (Button("Random", 60) && canRandomize)
            {
                Manager.ActivateRandomGroup(typeName);
            }
            EndRow();
            ColorsIf(GetBool(prefsString), Color.green, Color.grey, Color.white, Color.white);
            ContentColor(Color.white);
            
            if (!GetBool(prefsString)) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(10)); // Indent
            ShowDefaultToggle(null, true);
            ShowRandomToggle(null, true);
            ShowObjectsButton(null, true);
            ShowShapesButton(null, true);
            ShowGroupName(null, true);
            ShowGroupType(null, true);
            ShowGroupActivateDeactivate(null, true);
            ShowCopy(null, true);
            ShowRemovePrefabGroup(null, true);
            EditorGUILayout.EndHorizontal();
            
            foreach (var group in groupsOfType)
                ShowPrefabGroupRow(group);
            
            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button($"Create New{(string.IsNullOrWhiteSpace(typeName) ? "" : $" {typeName}")} Group", GUILayout.Width(250)))
            {
                Manager.CreateNewPrefabGroup(typeName);
                DoCache();
            }
            GUI.backgroundColor = Color.white;
            ResetColor();
        }

        private void ShowPrefabGroupRow(PrefabGroup group)
        {
            GUI.backgroundColor = ColorSet(group);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(10)); // Indent
            
            ShowDefaultToggle(group);
            ShowRandomToggle(group);
            ShowObjectsButton(group);
            ShowShapesButton(group);
            ShowGroupName(group);
            ShowGroupType(group);
            ShowGroupActivateDeactivate(group);
            ShowCopy(group);
            ShowRemovePrefabGroup(group);
            
            EditorGUILayout.EndHorizontal();

            ShowObjects(group);
            ShowShapes(group);
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
        }

        private void ShowCopy(PrefabGroup group, bool header = false)
        {
            if (header)
            { 
                Label("", 60);
                return;
            }

            if (Button($"Copy", 60))
            {
                Manager.CopyGroup(group);
            }
            ResetColor();
        }

        private void ShowObjects(PrefabGroup group)
        {
            if (!group.showPrefabs) return; // return if we haven't toggled this on
            
            // Show each of the objects attached to this Prefab Group
            foreach (var groupObject in group.groupObjects)
            {
                ShowGroupObject(group, groupObject);
                BackgroundColor(Color.white);
            }

            
            StartRow();
            ShowAddNewObjectField(group);
            EndRow();
            
            StartRow();
            ShowSelectEquipmentObject(group);
            EndRow();
        }

        private void ShowUpdate()
        {
            if (WardrobePrefabManager == null) return;
            
            BackgroundColor(Color.magenta);
            StartVerticalBox();
            if (!_cachedEquipmentObjects)
            {
                if (Button("Find EquipmentObject Prefabs"))
                {
                    CacheEquipmentObjects();
                    DoCache(true);
                }

                ResetColor();

                MessageBox(
                    "Click this button to find all prefabs with EquipmentObject components which match this Group. This process can take some time, and can be run manually, or automatically " +
                    $"if you don't mind the wait. This is required prior to adding any objects below, when the Wardrobe Prefab Manager is in use.\n\n Note: Progress bar will not run, as the method is much faster without showing the bar!");

                Space();
            }
            
            LeftCheckSetBool("Auto Find Equipment Objects On Enable", 
                $"Cache EquipmentObjects on Enable {symbolInfo}", 
                "When on, the system will cache all objects with EquipmentObject components on them whenever " +
                "an object with a Wardrobe Prefab Manager is viewed in the Inspector. This should be run before adding " +
                "Equipment Objects, but can be done manually via a button push, if this is toggled off.");
            EndVerticalBox();
        }
        
        private void ShowShapes(PrefabGroup group)
        {
            if (!group.showShapes) return; // return if we haven't toggled this on
            if (!WardrobePrefabManager) return;

            var blendShapeGroup = WardrobePrefabManager.GetGroup(group);
            if (blendShapeGroup == null) return;
            
            string[] blendShapeNames = blendShapeGroup.blendShapeNames.ToArray();

            // ON ACTIVATE
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent($"On Activate {symbolInfo}", $"These shapes will be modified when this Prefab Group is " +
                                                                                   $"activated. This is how you can set shapes to make sure they fit with the " +
                                                                                   $"wardrobe in the Prefab Group. Select a shape, and then click \"Add To List\"."), GUILayout.Width(120));

            if (blendShapeGroup.blendShapeNames.Count == 0)
            {
                EditorGUILayout.LabelField("No Blend Shapes Available");
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                blendShapeGroup.shapeChoiceIndex = EditorGUILayout.Popup(blendShapeGroup.shapeChoiceIndex, blendShapeNames);
                if (GUILayout.Button("Add To List"))
                {
                    WardrobePrefabManager.AddToList("Activate", blendShapeGroup);
                    SetAllDirty();
                }
                EditorGUILayout.EndHorizontal();
            }

            // ON ACTIVATE GLOBAL
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent($"Global Shapes {symbolInfo}", $"The top list contains only the shapes assigned to " +
                                                                                     $"the objects in the Prefab Group. \"Global Shapes\" will list all " +
                                                                                     $"shapes available."), GUILayout.Width(120));

            if (BlendShapesManager.matchList.Count == 0)
            {
                EditorGUILayout.LabelField("No Global Blend Shapes Available");
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                BlendShapesManager.matchListIndex = EditorGUILayout.Popup(BlendShapesManager.matchListIndex,
                    BlendShapesManager.matchListNames);
                if (GUILayout.Button("Add To List"))
                {
                    WardrobePrefabManager.AddToList("Activate",
                        BlendShapesManager.matchList[BlendShapesManager.matchListIndex], blendShapeGroup);
                    SetAllDirty();
                }
                EditorGUILayout.EndHorizontal();
            }

            // ON ACTIVATE LIST
            for (int i = 0; i < blendShapeGroup.onActivate.Count; i++)
            {
                BlendShapeItem item = blendShapeGroup.onActivate[i];
                WardrobePrefabManagerDisplayItem(blendShapeGroup, item, i, "Activate", WardrobePrefabManager);
            }

            EditorGUILayout.EndVertical();
            

            // ON DEACTIVATE
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent($"On Deactivate {symbolInfo}", $"These shapes will be triggered when the Prefab Group is " +
                                                                                     $"deactivated. Often the shapes will match those in \"On Activate\"."), GUILayout.Width(120));

            if (blendShapeGroup.blendShapeNames.Count == 0)
            {
                EditorGUILayout.LabelField("No Blend Shapes Available");
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                blendShapeGroup.shapeChoiceIndex = EditorGUILayout.Popup(blendShapeGroup.shapeChoiceIndex, blendShapeNames);
                if (GUILayout.Button("Add To List"))
                {
                    WardrobePrefabManager.AddToList("Deactivate", blendShapeGroup);
                    SetAllDirty();
                }
                if (GUILayout.Button("Revert Back"))
                {
                    WardrobePrefabManager.AddToList("Revert Back", blendShapeGroup);
                    SetAllDirty();
                }
                EditorGUILayout.EndHorizontal();
            }

            // ON DEACTIVATE GLOBAL
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent($"Global Shapes {symbolInfo}", $"The top list contains only the shapes assigned to " +
                                                                                     $"the objects in the Prefab Group. \"Global Shapes\" will list all " +
                                                                                     $"shapes available."), GUILayout.Width(120));

            if (BlendShapesManager.matchList.Count == 0)
            {
                EditorGUILayout.LabelField("No Global Blend Shapes Available");
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                BlendShapesManager.matchListIndex = EditorGUILayout.Popup(BlendShapesManager.matchListIndex,
                    BlendShapesManager.matchListNames);
                if (GUILayout.Button("Add To List"))
                {
                    WardrobePrefabManager.AddToList("Deactivate",
                        BlendShapesManager.matchList[BlendShapesManager.matchListIndex],
                        blendShapeGroup);
                    SetAllDirty();
                }
                if (GUILayout.Button("Revert Back"))
                {
                    WardrobePrefabManager.AddToList("Revert Back",
                        BlendShapesManager.matchList[BlendShapesManager.matchListIndex], blendShapeGroup);
                    SetAllDirty();
                }
                EditorGUILayout.EndHorizontal();
            }

            // ON ACTIVATE LIST
            for (int i = 0; i < blendShapeGroup.onDeactivate.Count; i++)
            {
                BlendShapeItem item = blendShapeGroup.onDeactivate[i];
                WardrobePrefabManagerDisplayItem(blendShapeGroup, item, i, "Deactivate", WardrobePrefabManager);
            }

            EditorGUILayout.EndVertical();
        }

        private void SetAllDirty(){
            EditorUtility.SetDirty(this);
        }
        
        private void WardrobePrefabManagerDisplayItem(BlendShapeGroup group, BlendShapeItem item, int itemIndex, string type, WardrobePrefabManager wardrobePrefabManager)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = redColor;
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                EditorGUILayout.EndHorizontal();

                if (type == "Activate")
                    group.onActivate.RemoveAt(itemIndex);
                if (type == "Deactivate")
                    group.onDeactivate.RemoveAt(itemIndex);

            }
            else
            {
                GUI.backgroundColor = Color.white;
                EditorGUILayout.LabelField(item.objectName + " " + item.triggerName);

                if (item.revertBack)
                {
                    EditorGUILayout.LabelField("This value will revert to pre-activation value");
                }
                else
                {
                    EditorGUILayout.LabelField(new GUIContent($"{symbolInfo}", $"\"Explicit\" will set the shape to the value specified, while \"Less than\" will set the shape to ensure it is " +
                                                                               $"less than or equal to the value specified. \"Greater than\" will set it to make sure it is greater than or equal to the " +
                                                                               $"specified value.\n\nUse the \"Set\" button to set the value selected and visualize the outcome.\n\n" +
                                                                               $"[On Deactivate] Use \"Revert Back\" to make this shape revert back to it's previous value that was active when the " +
                                                                               $"Prefab Group was activated."), GUILayout.Width(25));
                    item.actionTypeIndex = EditorGUILayout.Popup(item.actionTypeIndex, WardrobePrefabManager.actionTypes);
                    item.actionType = WardrobePrefabManager.actionTypes[item.actionTypeIndex];
                    item.value = EditorGUILayout.Slider(item.value, item.min, item.max);
                    if (GUILayout.Button("Set"))
                        wardrobePrefabManager.TriggerBlendShape(item.triggerName, item.value);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void UpdateAvailableObjects(PrefabGroup prefabGroup)
        {
            // Use cached, type-constrained search to avoid scanning huge non-Prefab asset sets (e.g., 6GB of audio)
            var foundObjects = FindAssetsByLabelCached(prefabGroup.labelMask, prefabGroup.searchString);
            prefabGroup.objectList = foundObjects;
            prefabGroup.objectListNames = foundObjects.Select(x => x.name).ToArray();
            prefabGroup.objectIndex = 0;
        }

        private void ShowRequiredLabels()
        {
            StartRow();
            Label($"Required Labels {symbolInfo}", 
                $"All Prefab Groups will require the labels selected here. \"Infinity\" is a required label in all " +
                $"cases, to reduce the amount of overhead when searching for appropriate objects.", 200);

            var tempLabelMask = Manager.labelMask;
            Manager.labelMask = DrawLabelSelection(Manager.labelMask);

            // Only invalidate the AssetDatabase search cache when the mask actually changed.
            if (tempLabelMask != Manager.labelMask)
            {
                _searchCache.Clear();
                foreach (var prefabGroup in Manager.prefabGroups)
                    prefabGroup.labelMask |= Manager.labelMask; // add new labels to prefabGroup.labelMask
            }
            
            // Remove unselected labels from prefabGroup.labelMask
            for (int i = 0; i < 32; i++) // assuming int is 32-bit
            {
                int bit = 1 << i;
                if ((tempLabelMask & bit) != 0 && (Manager.labelMask & bit) == 0)
                {
                    if (Dialog("Remove from all groups?", "Would you like to remove this label from all Prefab Groups?",
                            "Remove from all", "Keep all"))
                    {
                        // if a bit is set in tempLabelMask and not set in Manager.labelMask, remove it from prefabGroup.labelMask
                        foreach (var prefabGroup in Manager.prefabGroups)
                            prefabGroup.labelMask &= ~bit;
                    }
                }
            }
            
            EndRow();
            DisplaySelectedLabels(Manager.labelMask);
        }
        
        /*
         * This is where we add new objects to the Prefab Group, users can drag/drop or select an object to add it to the
         * list.
         */
        private void ShowSelectEquipmentObject(PrefabGroup prefabGroup)
        {
            if (Manager.WardrobePrefabManager == null) return; // Don't show this if it doesn't have a Wardrobe Prefab Manager

            var tempSearchString = prefabGroup.searchString;
            var tempLabelMask = prefabGroup.labelMask;
            
            StartVerticalBox();
            
            StartRow();
            Label($"Select Object by Label {symbolInfo}", 
                $"Select Asset Labels to include. Any object which has ALL of the labels will be shown in the list. " +
                $"Further narrow parameters via the text box. Object names must match the value of the search string.", 200);

            prefabGroup.labelMask = DrawLabelSelection(prefabGroup.labelMask);
            if (tempLabelMask != prefabGroup.labelMask)
                _searchCache.Clear();
            DrawSearchString(prefabGroup);
            if (Button($"{symbolRecycle}", 25))
            {
                UpdateAvailableObjects(prefabGroup);
            }
            EndRow();
            DisplaySelectedLabels(prefabGroup.labelMask);

            // If either the Label selection or the search string changed, we should cache the list.
            if (tempSearchString != prefabGroup.searchString || tempLabelMask != prefabGroup.labelMask)
                UpdateAvailableObjects(prefabGroup);
            
            if (prefabGroup.objectListNames.Length == 0)
            {
                ContentColor(Color.red);
                Label("There are no objects with the selected labels and search string.");
                ResetColor();
            }
            else
            {
                StartRow();
                ShowSelectionPopup(prefabGroup); // The list of found objects available to be added
                Label($"Add {symbolInfo}: ", $"\"Add\" will add only the selected object. \"Add All\" will prompt for confirmation before " +
                                             $"adding all objects in the list.\n\n\"Add each to new group\" will create a new group of this type " +
                                             $"for each object.", 50);
            
                BackgroundColor(Color.yellow);
                if (Button("Add", 50))
                {
                    AddObjectFromList(prefabGroup, (GameObject)prefabGroup.objectList[prefabGroup.objectIndex]);
                }
                if (Button("Add All", 75))
                {
                    if (Dialog($"Add All?", $"There are {prefabGroup.objectListNames.Length} objects. Are you sure you " +
                                            $"want to add them all?"))
                        AddAllObjectsFromList(prefabGroup);
                }
            
                if (Button("Add each to new group", 145))
                {
                    AddEachFromListToNewGroup(prefabGroup);
                }
                EndRow();
            }
            
            EndVerticalBox();
            ResetColor();
        }

        private void ShowSelectionPopup(PrefabGroup prefabGroup)
        {
            BackgroundColor(Color.yellow);

            prefabGroup.objectIndex = Popup(prefabGroup.objectIndex, prefabGroup.objectListNames, 200);
            ResetColor();
        }

        private void DrawSearchString(PrefabGroup prefabGroup)
        {
            BackgroundColor(Color.yellow);
            prefabGroup.searchString = DelayedText(prefabGroup.searchString, 150);
            ResetColor();
        }

        /*
        private void DrawLabelSelection(PrefabGroup prefabGroup)
        {
            BackgroundColor(Color.yellow);
            var allLabels = MagicPigStatic.GetAllLabels();
            
            // Get current labels of the asset
            //var currentLabels = GetCurrentLabels();

            // Create a mask from the selected labels
            var labelMask = prefabGroup.labelMask;

            // Find the index of "Infinity" in the label list
            var infinityIndex = allLabels.IndexOf("Infinity");

            // Check if "Infinity" is included in the labelMask
            var hasInfinityMask = (labelMask & (1 << infinityIndex)) != 0;

            if (!hasInfinityMask && infinityIndex >= 0)
            {
                // Include "Infinity" in the labelMask
                labelMask |= (1 << infinityIndex);
                prefabGroup.labelMask = labelMask;
            }
            
            
            labelMask = MaskField(labelMask, allLabels.ToArray(), 150);
            

            // Update the Manager object's labelMask
            if (prefabGroup.labelMask != labelMask)
            {
                prefabGroup.labelMask = labelMask;
                EditorUtility.SetDirty(Manager);
            }
            ResetColor();
        }
        */
        
        private int DrawLabelSelection(int labelMask)
        {
            BackgroundColor(Color.yellow);
            var allLabels = _cachedAllLabels;
            
            // Find the index of "Infinity" in the label list
            var infinityIndex = allLabels.IndexOf("Infinity");

            // Check if "Infinity" is included in the labelMask
            var hasInfinityMask = (labelMask & (1 << infinityIndex)) != 0;

            if (!hasInfinityMask && infinityIndex >= 0)
            {
                // Include "Infinity" in the labelMask
                labelMask |= (1 << infinityIndex);
                Manager.labelMask = labelMask;
            }
            
            labelMask = MaskField(labelMask, allLabels.ToArray(), 150);
            
            // Update the Manager object's labelMask
            if (labelMask != Manager.labelMask)
            {
                Manager.labelMask = labelMask;
                EditorUtility.SetDirty(Manager);
            }
            ResetColor();
            return labelMask;
        }

        private void DisplaySelectedLabels(int labelMask)
        {
            var allLabels = _cachedAllLabels;

            StartRow();
            var displayString = "";
            for (int i = 0; i < allLabels.Count; i++)
            {
                if ((labelMask & (1 << i)) == 0)
                    continue;
                
                
                if (displayString != "")
                    displayString = $"{displayString}, ";
                displayString = $"{displayString}{allLabels[i]}";
            }

            ContentColor(Color.grey);
            Label($"Selected Labels: {displayString}", false, true);
            ContentColor(Color.white);
            
            /*
            // Draw selected labels
            var drawn = 0;
            for (int i = 0; i < allLabels.Count; i++)
            {
                if ((labelMask & (1 << i)) == 0)
                    continue;
               
                LabelButton(allLabels[i]);
                
                drawn++;
                if (drawn % 4 != 0) continue;
                
                EndRow();
                StartRow();
            }
            */
            EndRow();
        }
        
        private void LabelButton(string label)
        {
            BackgroundColor(Color.blue);
            Button(label, 100);
            ResetColor();
        }
        
        /*private List<string> GetAllLabels()
        {
            var guids = AssetDatabase.FindAssets("l:Infinity");

            // Extract labels from guids
            var allLabels = new List<string>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var labels = AssetDatabase.GetLabels(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path));
                allLabels.AddRange(labels);
            }

            // Remove duplicate labels
            allLabels = allLabels.Distinct().ToList();

            // Ensure "Infinity" is in the label list
            if (!allLabels.Contains("Infinity"))
                allLabels.Insert(0, "Infinity");

            return allLabels;
        }*/

        private void AddEachFromListToNewGroup(PrefabGroup group)
        {
            foreach (var obj in group.objectList)
            {
                // Create the new group
                PrefabGroup newGroup = Manager.CreateNewPrefabGroup(group.groupType);
                newGroup.name = obj.name;
                
                // Add the object to the new group
                AddObjectFromList(newGroup, (GameObject)obj, false);
            }
            
            /*
             * foreach (var obj in group.equipmentObjectObjects)
            {
                // Create the new group
                PrefabGroup newGroup = Manager.CreateNewPrefabGroup(group.groupType);
                newGroup.name = obj.name;
                
                // Add the object to the new group
                AddObjectFromList(newGroup, obj, false);
            }
             */
        }

        private void AddAllObjectsFromList(PrefabGroup prefabGroup)
        {
            foreach (var obj in prefabGroup.objectList)
                AddObjectFromList(prefabGroup, (GameObject)obj, false);
            
            // Activate the preset in the scene
            if (Manager.instantiatePrefabsAsAdded)
                //_activateGroup = group;
                Manager.ActivateGroup(prefabGroup);
              
            
            //DoCache(true); // Update the lists!
            
            /*
             * foreach (var obj in group.equipmentObjectObjects)
                AddObjectFromList(group, obj, false);
            
            // Activate the preset in the scene
            if (Manager.instantiatePrefabsAsAdded)
                //_activateGroup = group;
                Manager.ActivateGroup(group);
                
            DoCache(true); // Update the lists!
             */
        }

        private void AddObjectFromList(PrefabGroup group, GameObject objectToAdd, bool doUpdateAndCache = true, bool noDuplicates = true)
        {
            if (noDuplicates && group.groupObjects.FirstOrDefault(x => x.objectToHandle == objectToAdd) != null)
                return;
            
            // Add the prefab to the list
            Manager.AddPrefabToGroup(group, objectToAdd);
                
            // Reduce the index value if needed
            group.equipmentObjectIndex = Mathf.Clamp(group.equipmentObjectIndex, 0, group.equipmentObjectObjects.Count - 1);

            if (!doUpdateAndCache) return;
            
            // Activate the preset in the scene
            if (Manager.instantiatePrefabsAsAdded)
                //_activateGroup = group;
                Manager.ActivateGroup(group);

            DoCache(true); // Update the lists!
        }

        /*
         * This is where we add new objects to the Prefab Group, users can drag/drop or select an object to add it to the
         * list.
         */
        private void ShowAddNewObjectField(PrefabGroup group)
        {
            BackgroundColor(Color.white);
            StartVerticalBox();
            StartRow();
            Label($"Add Prefab or Child Object to Group {symbolInfo}",
                $"Drag or select a Prefab from your project or a Game Object from the scene to add it to {group.name}. " +
                $"You can mix both types in each group. Prefabs will be instantiated and destroyed, and Game Objects will " +
                $"be turned on and off when this group is activated or deactivated.", 250);
            BackgroundColor(Color.yellow);
            group.newPrefab = Object(group.newPrefab, typeof(GameObject), 250, true) as GameObject;
            
            if (group.newPrefab)
            {
                if (group.newPrefab != null && group.newPrefab.transform.IsChildOf(Manager.transform))
                    Manager.AddGameObjectToGroup(group.newGameObject, group);
                else if (PrefabUtility.IsPartOfAnyPrefab(group.newPrefab))
                    Manager.AddPrefabToGroup(group);
                else if (group.newPrefab != null)
                    Debug.LogError("Error: " + group.newPrefab.name +
                                   " isn't a prefab that can be added, or isn't a child of the parent object.");

                group.newPrefab = null;

                if (Manager.instantiatePrefabsAsAdded)
                    //_activateGroup = group;
                    Manager.ActivateGroup(group);
                
                DoCache(true);
            }
            EndRow();
            EndVerticalBox();
            ResetColor();
        }

        private void ShowGroupObject(PrefabGroup group, GroupObject groupObject)
        {
            StartRow();
            Label("", 30);

            ShowObjectDelete(group, groupObject);
            ShowObjectRender(group, groupObject);
            ShowObjectFields(group, groupObject);
            ShowVariable(group, groupObject);

            CheckOptionsAreSet(group, groupObject);

            EndRow();
        }

        
        
        private void ShowVariable(PrefabGroup group, GroupObject groupObject)
        {
            if (Manager.variables.Count == 0) return;
            if (variableIndex >= Manager.variables.Count) variableIndex = Manager.variables.Count - 1;
            
            if (String.IsNullOrEmpty(groupObject.variable.name))
            {
                variableIndex = Popup(variableIndex, VariableNamesCached(), 150);
                if (Button("Add Variable", 100))
                {
                    groupObject.variable = new PrefabObjectVariable(Manager.variables[variableIndex].name);
                    ExitGUI();
                }

                return;
            }

            var variable = groupObject.variable;

            if (Button($"{symbolRecycle}", 25))
            {
                variable.name = "";
                ExitGUI();
            }
            
            Label($"{variable.name}", 100);
            variable.valueIndex = Popup(variable.valueIndex, variable.variableValueTypes, 50);

            if (variable.valueIndex == 0)
            {
                variable.valueBool = Check(variable.valueBool);
                return;
            }
            
            if (variable.valueIndex == 1)
            {
                if (variable.valueFloatOptionIndex >= variable.valueFloatOptions.Length)
                    variable.valueFloatOptionIndex = variable.valueFloatOptions.Length - 1;

                variable.valueFloatOptionIndex = Popup(variable.valueFloatOptionIndex, variable.valueFloatOptions, 50);

                variable.valueFloat = Float(variable.valueFloat, 50);
                return;
            }
            
            if (variable.valueIndex == 2)
            {
                if (variable.valueStringOptionIndex >= variable.valueStringOptions.Length)
                    variable.valueStringOptionIndex = variable.valueStringOptions.Length - 1;

                variable.valueStringOptionIndex = Popup(variable.valueStringOptionIndex, variable.valueStringOptions, 50);

                variable.valueString = TextField(variable.valueString, 100);
                return;
            }
        }

        private void ShowObjectRender(PrefabGroup group, GroupObject groupObject)
        {
            ColorsIf(groupObject.render, Color.grey, Color.grey, Color.white, Color.grey);
            if (Button(symbolCheck, 25))
            {
                groupObject.render = !groupObject.render;
                Manager.ActivateGroup(group);
            }
            ResetColor();
        }

        private void ShowObjectFields(PrefabGroup group, GroupObject groupObject)
        {
            GameObject oldObject = groupObject.objectToHandle;
            groupObject.objectToHandle =
                Object(groupObject.objectToHandle, typeof(GameObject), 250, !groupObject.isPrefab) as GameObject;
            if (oldObject != groupObject.objectToHandle)
            {
                if (groupObject.isPrefab)
                {
                    if (!PrefabUtility.IsPartOfAnyPrefab(groupObject.objectToHandle))
                    {
                        groupObject.objectToHandle = oldObject;
                        Debug.LogError("Error: This isn't a prefab that can be added.");
                    }
                    else
                    {
                        Event e = Event.current;
                        if (e.shift)
                            UpdateAllObjects(oldObject, groupObject.objectToHandle);
                    }
                }
                else
                {
                    if (!groupObject.objectToHandle.transform.IsChildOf(Manager.transform))
                    {
                        groupObject.objectToHandle = oldObject;
                        Debug.LogError("Error: This isn't a GameObject that can be added.");
                    }
                    else
                    {
                        Event e = Event.current;
                        if (e.shift)
                            UpdateAllObjects(oldObject, groupObject.objectToHandle);
                    }
                }

                if (Manager.instantiatePrefabsAsAdded)
                {
                    //_deactivateGroup = group;
                    //_activateGroup = group;
                    Manager.DeactivateGroup(group);
                    Manager.ActivateGroup(group);
                }

            }

            if (groupObject.isPrefab)
            {
                Transform oldTransformObject = groupObject.parentTransform;
                groupObject.parentTransform = Object(groupObject.parentTransform, typeof(Transform), 200, true) as
                    Transform;
                if (oldTransformObject != groupObject.parentTransform)
                {

                    if (!groupObject.parentTransform.IsChildOf(Manager.thisTransform))
                    {
                        groupObject.parentTransform = oldTransformObject;
                        Debug.LogError("Error: Transform must be the parent transform or a child of " +
                                       Manager.thisTransform.name);
                    }
                    else
                    {
                        Event e = Event.current;
                        if (e.shift)
                            UpdateAllTransforms(groupObject.parentTransform);
                    }
                }
            }
        }

        private void ShowObjectDelete(PrefabGroup group, GroupObject groupObject)
        {
            BackgroundColor(Color.red);
            if (Button(symbolX, 25))
            {
                RemoveObject(group, groupObject);
                DoCache(true);
                ExitGUI();
            }
            ResetColor();
        }

        private void ShowObjectsButton(PrefabGroup prefabGroup, bool header = false)
        {
            var fieldWidth = 75;
            if (header)
            { 
                Label("", fieldWidth + 3);
                return;
            }
            
            ColorsIf(prefabGroup.showPrefabs, Color.green, Color.grey, Color.white, Color.white);
            bool tempShowPrefabs = prefabGroup.showPrefabs;
            if (Button($"Objects", fieldWidth))
            {
                // If we are turning this one on, turn others off and cache the objects
                if (!prefabGroup.showPrefabs)
                {
                    TurnOffAllObjectsAndShapes();
                    //UpdateAvailableObjects(prefabGroup);
                }

                prefabGroup.showPrefabs = !prefabGroup.showPrefabs;
                prefabGroup.showShapes = !prefabGroup.showPrefabs && prefabGroup.showShapes;
            }

            // We are turning this group on, so do the cache here.
            //if (!tempShowPrefabs && prefabGroup.showPrefabs)
            //{
               // UpdateAvailableObjects(prefabGroup);
                //DoCache(true);
            //}
            ResetColor();
        }

        private void TurnOffAllObjectsAndShapes()
        {
            foreach (var group in Manager.prefabGroups)
            {
                group.showPrefabs = false;
                group.showShapes = false;
            }
        }
        
        private void ShowShapesButton(PrefabGroup prefabGroup, bool header = false)
        {
            if (!BlendShapesManager || !WardrobePrefabManager) return;
            var fieldWidth = 80;
            
            if (header)
            { 
                Label("", fieldWidth + 3);
                return;
            }

            ColorsIf(prefabGroup.showShapes, Color.green, Color.grey, Color.white, Color.white);
            if (Button($"Shapes", fieldWidth))
            {
                // If we are turning this one on, turn others off
                if (!prefabGroup.showPrefabs)
                    TurnOffAllObjectsAndShapes();
                
                prefabGroup.showShapes = !prefabGroup.showShapes;
                prefabGroup.showPrefabs = !prefabGroup.showShapes && prefabGroup.showPrefabs;
            }
            ResetColor();
        }

        private void ShowGroupActivateDeactivate(PrefabGroup group, bool header = false)
        {
            var fieldWidth = 160;
            if (header)
            { 
                Label("", fieldWidth);
                return;
            }

            var groupIsActive = GroupIsActiveCached(group) == 2;
            BackgroundColor(groupIsActive ? Color.green : Color.grey);
            if (group.isDefault && groupIsActive)
                ContentColor(Color.grey);
            if (Button($"Turn {(groupIsActive ? "off" : "on")}", 60))
            {
                if (groupIsActive)
                    Manager.DeactivateGroup(group);
                else
                    Manager.ActivateGroup(group);
            }
            ResetColor();
        }

        private void ShowRandomToggle(PrefabGroup group, bool header = false)
        {
            var fieldWidth = 30;
            if (header)
            { 
                EditorGUILayout.LabelField(new GUIContent($"{symbolArrowCircleRight} {symbolInfo}", $"If off, this will not be included in the \"Random\" method."), GUILayout.Width(fieldWidth));
                return;
            }

            ColorsIf(group.canRandomize, Color.grey, Color.grey, Color.white, Color.grey);
            if (String.IsNullOrWhiteSpace(group.groupType))
            {
                group.isDefault = false;
                ContentColor(Color.grey);
                EditorGUILayout.LabelField($"N/A", GUILayout.Width(fieldWidth));
                ResetColor();
                return;
            }
            
            group.canRandomize = ButtonToggle(group.canRandomize, $"{symbolArrowCircleRight}", fieldWidth);
            ResetColor();
        }

        private void ShowDefaultToggle(PrefabGroup group, bool header = false)
        {
            var fieldWidth = 30;
            if (header)
            { 
                EditorGUILayout.LabelField(new GUIContent($"{symbolStarClosed} {symbolInfo}", $"Optional. Toggle one group to " +
                                                                                   $"be the default group. When a group of this " +
                                                                                   $"type is deactivated, the default group will " +
                                                                                   $"automatically be activated.\n\nThis option is " +
                                                                                   $"only available for groups with a \"Type\"."), GUILayout.Width(fieldWidth));
                return;
            }

            if (String.IsNullOrWhiteSpace(group.groupType))
            {
                group.isDefault = false;
                ContentColor(Color.grey);
                EditorGUILayout.LabelField($"N/A", GUILayout.Width(fieldWidth));
                ResetColor();
                return;
            }

            var cacheToggle = group.isDefault;

            ColorsIf(group.isDefault, Color.green, Color.grey, Color.white, Color.grey);
            group.isDefault = ButtonToggle(group.isDefault, $"{symbolStarClosed}", fieldWidth);
            //group.isDefault = EditorGUILayout.Toggle(group.isDefault, GUILayout.Width(fieldWidth));

            // Set all the other ones to false if this is now true
            if (cacheToggle != group.isDefault && group.isDefault)
            {
                foreach (var groupOfType in GroupsOfTypeCached(group.groupType))
                {
                    if (groupOfType == group) continue;
                    groupOfType.isDefault = false;
                }
            }
            ResetColor();
        }

        private void ShowGroupType(PrefabGroup group, bool header = false)
        {
            var fieldWidth = 100;
            if (header)
            { 
                EditorGUILayout.LabelField(new GUIContent($"Type {symbolInfo}", $"You can group Prefab Groups by type, making " +
                                                                   $"it easier to have only one active at a time, or simply for " +
                                                                   $"organization purposes."), GUILayout.Width(fieldWidth));
                return;
            }
            
            var cachedType = group.groupType;
            group.groupType = EditorGUILayout.DelayedTextField(group.groupType, GUILayout.Width(100));
            if (cachedType != group.groupType)
            {
                EnsureTypeNames();
                DoCache();
                EditorPrefs.SetBool($"Prefab Manager Show Type {group.groupType}", true);
            }
        }

        private void ShowGroupName(PrefabGroup group, bool header = false)
        {
            var fieldWidth = 180;
            if (header)
            { 
                EditorGUILayout.LabelField(new GUIContent($"Group Name {symbolInfo}", $"The name of the group must " +
                                                                                      $"be unique, and can be used to activate and " +
                                                                                      $"deactivate the group at runtime."), GUILayout.Width(fieldWidth));
                return;
            }
            
            var cachedName = group.name;
            group.name = EditorGUILayout.DelayedTextField(group.name, GUILayout.Width(fieldWidth));
            if (cachedName != group.name)
            {
                if (String.IsNullOrEmpty(group.name))
                {
                    Debug.LogWarning("Error: Group names can not be empty.");
                    group.name = cachedName;
                }
                if (Manager.prefabGroups.Count(x => x.name == group.name) > 1)
                {
                    Debug.LogWarning("Error: Group names must be unique.");
                    group.name = cachedName;
                }
            }
        }

        private void ShowRemovePrefabGroup(PrefabGroup group, bool header = false)
        {
            var fieldWidth = 25;
            if (header)
            {
                Label("", fieldWidth);
                return;
            }
            
            BackgroundColor(Color.red);
            if (Button(symbolX, fieldWidth))
            {
                if (Dialog("Remove Group?", "Are you sure you want to do this?"))
                {
                    //foreach (var t in group.groupObjects)
                    //   RemoveObject(group, t);
                    //RemoveGroup(group);

                    Manager.RemovePrefabGroup(group);
                    DoCache();
                    ExitGUI();
                }
            }
            ResetColor();
        }

        private void RemoveGroup(PrefabGroup group) => Manager.prefabGroups.RemoveAll(x => x == group);

        private void SectionButton(string button, string prefs, int width = -1)
        {
            Color offColor = new Color(0.2f, 0.2f, 0.2f,1f);
            BackgroundColor(GetBool(prefs) ? Color.green : Color.grey);
            if (Button(button))
                SetBool(prefs, !GetBool(prefs));
            ResetColor();
        }
        
        private void SetupAndOptions()
        {
            if (!EditorPrefs.GetBool("Prefab Manager Show Setup And Options")) return;
            
            EditorGUI.indentLevel++;
            EditorPrefs.SetBool("Prefab Manager Show Help Boxes", 
                EditorGUILayout.Toggle(new GUIContent($"Show Help Boxes {symbolInfo}", "Toggles help boxes in the Inspector"), 
                    EditorPrefs.GetBool("Prefab Manager Show Help Boxes")));
            EditorPrefs.SetBool("Prefab Manager Show Full Inspector", 
                EditorGUILayout.Toggle(new GUIContent($"Show Full Inspector {symbolInfo}", "If true, will show the full default inspector at the bottom" +
                                                                               "of the window. Use for debugging, not for editing data!"), 
                    EditorPrefs.GetBool("Prefab Manager Show Full Inspector")));
            Manager.instantiatePrefabsAsAdded =  
                EditorGUILayout.Toggle(new GUIContent($"Instantiate Prefabs when Added to Group {symbolInfo}", "If true, prefabs that are added to a group " +
                    "will be instantiated into the scene."), Manager.instantiatePrefabsAsAdded);
            Manager.onlyOneGroupActivePerType =  
                EditorGUILayout.Toggle(new GUIContent($"Only one group active per type {symbolInfo}", "If true, only one group per named \"type\" can be active " +
                          "at a time, and any active group will be deactivated when a new one is " +
                          "activated. This means you only have to call the \"Activate\" method, and " +
                          "the rest is handled for you."), Manager.onlyOneGroupActivePerType);
            Manager.unpackPrefabs = 
                EditorGUILayout.Toggle(new GUIContent($"Unpack Prefabs when Instantiated {symbolInfo}", "If true, prefabs that are instantiated will be unpacked."), 
                    Manager.unpackPrefabs);

            EquipObject.rootBoneName = TextField($"Root Bone Name {symbolInfo}","This should be the root bone in your bone hierarchy.rootBoneName", EquipObject.rootBoneName);
            
            EditorGUILayout.Space();
            HelpBoxMessage("Use the option below to set all InGameObject values to null. This is useful " +
                                    "if you've copied the component values from another character, to clean it up.");
            if (GUILayout.Button("Make all \"In Game Objects\" null"))
            {
                RemoveInGameObjectLinks();
            }
            
            HelpBoxMessage("If you've copied another objects data or added the component from another object " +
                           "as new to this object, use this option to relink all the available objects to the new object.\n\n " +
                           "HINT: If you hold shift while you replace the transform in the list, all transforms will be updated to " +
                           "the new selection.");
            if (GUILayout.Button("Relink objects to this parent object"))
            {
                RelinkObjects();
            }
            
            EditorGUI.indentLevel--;
        }

        private void HelpBoxMessage(string message, MessageType messageType = MessageType.None)
        {
            if (!EditorPrefs.GetBool("Prefab Manager Show Help Boxes")) return;
            EditorGUILayout.HelpBox(message,messageType);
        }

        private void SetDefaultEditorPrefs()
        {
            DefaultEditorBool("Prefab Manager Show Help Boxes", true);
            DefaultEditorBool("Prefab Manager Show Full Inspector", false);
            DefaultEditorBool("Prefab Manager Instantiate When Added", true);
            DefaultEditorBool("Prefab Manager One Active Group Per Type", true);
            DefaultEditorBool("Prefab Manager Unpack Prefabs", true);
        }

        private void RemoveInGameObjectLinks()
        {
            foreach (PrefabGroup group in Manager.prefabGroups)
            {
                foreach (GroupObject obj in group.groupObjects)
                {
                    obj.inGameObject = null;
                }
            }
        }

        private void UpdateAllObjects(GameObject oldObject, GameObject newObject)
        {
            foreach (PrefabGroup group in Manager.prefabGroups)
            {
                foreach (GroupObject obj in group.groupObjects)
                {
                    if (obj.objectToHandle == oldObject)
                        obj.objectToHandle = newObject;
                }
            }
        }

        private void UpdateAllTransforms(Transform transform)
        {
            foreach (PrefabGroup group in Manager.prefabGroups)
            {
                foreach (GroupObject obj in group.groupObjects)
                {
                    obj.parentTransform = transform;
                }
            }
        }
        
        private void RemoveObject(PrefabGroup prefabGroup, GroupObject groupObject)
        {
            GameObject inGameObject = groupObject.inGameObject;
            if (groupObject.isPrefab && inGameObject)
                Manager.DestroyObject(inGameObject);
            else if (!groupObject.isPrefab && inGameObject)
                inGameObject.SetActive(false);

            prefabGroup.RemoveGroupObject(groupObject);
        }

        private void CheckOptionsAreSet(int g, int i) => CheckOptionsAreSet(Manager.prefabGroups[g], Manager.prefabGroups[g].groupObjects[i]);

        private void CheckOptionsAreSet(PrefabGroup group, GroupObject groupObject)
        {
            if (groupObject.parentTransform == null)
                groupObject.parentTransform = Manager.thisTransform;
        }
        
        private void RelinkObjects()
        {
            // Build the child lookup once instead of re-walking the entire hierarchy for every object (was O(n^2)).
            var childLookup = BuildChildGameObjectLookup();

            for (int g = 0; g < Manager.prefabGroups.Count; g++)
            {
                PrefabGroup prefabGroup = Manager.prefabGroups[g];

                for (int i = 0; i < prefabGroup.groupObjects.Count; i++)
                {
                    GroupObject groupObject = prefabGroup.groupObjects[i];

                    // If this is a prefab, do a different operation
                    if (groupObject.isPrefab)
                    {
                        GameObject foundObject = FindGameObject(groupObject.parentTransform.name, childLookup);
                        if (foundObject == null) continue;
                        groupObject.parentTransform = foundObject.transform;
                        continue;
                    }

                    if (groupObject.objectToHandle.transform.IsChildOf(groupObject.parentTransform))
                    {
                        GameObject foundObject = FindGameObject(groupObject.objectToHandle.name, childLookup);
                        if (foundObject == null) continue;
                        groupObject.parentTransform = Manager.gameObject.transform;
                    }
                }
            }
        }

        // Maps child name -> GameObject. First match in GetComponentsInChildren order wins, matching the
        // previous linear search's behavior when multiple children share a name.
        private Dictionary<string, GameObject> BuildChildGameObjectLookup()
        {
            var lookup = new Dictionary<string, GameObject>();
            var transforms = Manager.gameObject.GetComponentsInChildren<Transform>(true);
            foreach (var child in transforms)
                if (!lookup.ContainsKey(child.name))
                    lookup[child.name] = child.gameObject;
            return lookup;
        }

        private GameObject FindGameObject(string lookupName, Dictionary<string, GameObject> childLookup)
        {
            if (Manager.gameObject.name == lookupName)
                return Manager.gameObject;

            if (childLookup.TryGetValue(lookupName, out var found))
                return found;

            Debug.Log($"Warning: Did not find a child named {lookupName}! This re-link will be skipped.");
            return null;
        }
    }
}
