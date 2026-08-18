using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using Object = UnityEngine.Object;

[System.Serializable]
public class InfinityPBRManager : EditorWindow
{
    [SerializeField] private Object _packages;
    [SerializeField] private Object _tempPackages;
    [SerializeField] private Object _demoPath;
    [SerializeField] private Object _tempDemoPath;
    [SerializeField] private Object _tempMusicPath;
    [SerializeField] private String _packageSuffix;
    
    [SerializeField] private List<Object> _packageList = new List<Object>();
    
    [SerializeField] Vector2 _scrollPos;

    private int exportingIndex = 9999;
    
    [MenuItem("Window/Infinity PBR/InfinityPBR Manager")]			// Can load this via the window menu
    static void Init()
    {
        GetWindow(typeof(InfinityPBRManager));						// Brings this window to the front
    }

    void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        
        if (GUILayout.Button ("Reload Package List")) {
            ReloadPackageList ();
        }
        
        _packages = EditorGUILayout.ObjectField("Active Package Location",_packages, typeof(Object), false) as Object;
        _tempPackages = EditorGUILayout.ObjectField("Inactive Package Location",_tempPackages, typeof(Object), false) as Object;
        _demoPath = EditorGUILayout.ObjectField("Active Demo Location",_demoPath, typeof(Object), false) as Object;
        _tempDemoPath = EditorGUILayout.ObjectField("Inactive Demo Location",_tempDemoPath, typeof(Object), false) as Object;
        _tempMusicPath = EditorGUILayout.ObjectField("Inactive Music Location",_tempMusicPath, typeof(Object), false) as Object;
        _packageSuffix = EditorGUILayout.TextField("Exported Package Suffix: ", _packageSuffix);

        for (int i = 0; i < _packageList.Count; i++)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            string packageName = _packageList[i].name.Replace("_InfinityPBR - ", "");
            
            GUILayout.Label(packageName,GUILayout.Width(200));

            if (exportingIndex == i)
            {
                GUILayout.Label("Exporting, please wait...", GUILayout.Width(200));
            }
            else
            {
                if (IsInProject(_packageList[i].name))
                {
                    GUI.color = Color.cyan;
                    if (GUILayout.Button ("Remove", GUILayout.Width(60)))
                    {
                        MovePackage(i, false, false);
                    }

                    if (MusicInProject(i) || MusicInTemp(i))
                    {
                        if (GUILayout.Button("-", GUILayout.Width(30)))
                        {
                            MovePackage(i, false, true);
                        }
                    }
                    else
                    {
                        GUILayout.Label("", GUILayout.Width(30));
                    }

                    GUI.color = Color.white;
                }
                else
                {
                    if (GUILayout.Button ("Add", GUILayout.Width(60)))
                    {
                        MovePackage(i, true, false);
                    }
                    if (MusicInProject(i) || MusicInTemp(i))
                    {
                        if (GUILayout.Button("+", GUILayout.Width(30)))
                        {
                            MovePackage(i, true, true);
                        }
                    }
                    else
                    {
                        GUILayout.Label("", GUILayout.Width(30));
                    }
                }
                if (MusicInProject(i))
                {
                    GUI.color = Color.cyan;
                    if (GUILayout.Button ("Remove Music", GUILayout.Width(90)))
                    {
                        MoveMusic(i, false);
                    }
                    GUI.color = Color.white;
                }
                else if (MusicInTemp(i))
                {
                    if (GUILayout.Button ("Add Music", GUILayout.Width(90)))
                    {
                        MoveMusic(i, true);
                    }
                }
                else
                {
                    GUILayout.Label("No music found.", GUILayout.Width(90));
                }
            
                GUILayout.Label("   ", GUILayout.Width(10));
            
                if (IsInProject(_packageList[i].name))
                {
                    GUI.color = Color.green;
                    if (GUILayout.Button ("Export Pack", GUILayout.Width(90)))
                    {
                        ExportPackage(i, false);
                    }
                    GUI.color = Color.white;
                
                    if (MusicInProject(i))
                    {
                        GUI.color = Color.green;
                        if (GUILayout.Button ("Export Music", GUILayout.Width(90)))
                        {
                            ExportMusic(i, false);
                        }
                        GUI.color = Color.white;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void ExportPackage(int i, bool b)
    {
        string packageName = _packageList[i].name.Replace("_InfinityPBR - ", "");
        packageName = packageName.Replace(" ", "");
        packageName = packageName.Replace("&", "And");
        string musicPath = AssetDatabase.GetAssetPath(_packageList[i]) + "/Music";
        exportingIndex = i;
        AssetDatabase.ExportPackage(AssetDatabase.GetAssetPath(_packages), packageName + "_" + _packageSuffix + ".unitypackage", ExportPackageOptions.Recurse);
        if (EditorUtility.DisplayDialog("Export Complete!",
            "Would you like to remove the package folder?", "Yes, remove", "Do not remove"))
        {
            MovePackage(i, false, false);
        }

        exportingIndex = 9999;
    }

    private void ExportMusic(int i, bool b)
    {
        string packageName = _packageList[i].name.Replace("_InfinityPBR - ", "");
        packageName = packageName.Replace(" ", "");
        packageName = packageName.Replace("&", "And");
        string musicPath = AssetDatabase.GetAssetPath(_packageList[i]) + "/Music";
        exportingIndex = i;
        AssetDatabase.ExportPackage(musicPath, packageName + "_Music.unitypackage", ExportPackageOptions.Recurse);
        if (EditorUtility.DisplayDialog("Export Complete!",
            "Would you like to remove the music folder?", "Yes, remove", "Do not remove"))
        {
            MoveMusic(i, false);
        }

        exportingIndex = 9999;
    }

    private bool MusicInProject(int i)
    {
        string musicPath = AssetDatabase.GetAssetPath(_packageList[i]) + "/Music";
        return AssetDatabase.IsValidFolder(musicPath);
    }
    
    private bool MusicInTemp(int i)
    {
        string packageName = _packageList[i].name.Replace("_InfinityPBR - ", "");
        string musicPath = AssetDatabase.GetAssetPath(_tempMusicPath) + "/Music - " + packageName;
        return AssetDatabase.IsValidFolder(musicPath);
    }

    private bool IsInProject(string s)
    {
        if (AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(_packages) + "/" + s))
        {
            return true;
        }

        return false;
    }

    private void MoveMusic(int i, bool moveToProject)
    {
        string packageName = _packageList[i].name.Replace("_InfinityPBR - ", "");
        if (moveToProject)
        {
            string musicPath = AssetDatabase.GetAssetPath(_packageList[i]) + "/Music";
            string tempMusicPath = AssetDatabase.GetAssetPath(_tempMusicPath) + "/Music - " + packageName;
            if (AssetDatabase.IsValidFolder(tempMusicPath))
            {
                Debug.Log("Moving music folder");
                AssetDatabase.MoveAsset(tempMusicPath, musicPath);
            }
            else
            {
                Debug.Log("No music found.");
            }
        }
        else
        {
            string musicPath = AssetDatabase.GetAssetPath(_packageList[i]) + "/Music";
            string tempMusicPath = AssetDatabase.GetAssetPath(_tempMusicPath) + "/Music - " + packageName;
            if (AssetDatabase.IsValidFolder(musicPath))
            {
                Debug.Log("Moving music folder");
                AssetDatabase.MoveAsset(musicPath, tempMusicPath);
            }
            else
            {
                Debug.Log("No music found.");
            }
        }
    }

    private void MovePackage(int i, bool moveToProject, bool includeMusic)
    {
        string packageName = _packageList[i].name.Replace("_InfinityPBR - ", "");
        
        if (includeMusic)
            MoveMusic(i, moveToProject);
        
        if (moveToProject)
        {
            //Debug.Log("Moving " + packageName + " into project");

            // Move main project
            string tempPath = AssetDatabase.GetAssetPath(_packageList[i]);
            string mainPath = AssetDatabase.GetAssetPath(_packages) + "/" + _packageList[i].name;
            AssetDatabase.MoveAsset(tempPath, mainPath);

            // Move Demo Scene
            string demoName = _packageList[i].name.Replace("_", "");
            string tempDemoPath = AssetDatabase.GetAssetPath(_tempDemoPath) + "/" + demoName + ".unity";
            string demoPath = AssetDatabase.GetAssetPath(_demoPath) + "/" + demoName + ".unity";
            if (System.IO.File.Exists(tempDemoPath))
            {
                AssetDatabase.MoveAsset(tempDemoPath, demoPath);
            }
            else
            {
                Debug.LogWarning("Did not find demo scene: " + tempDemoPath);
            }
        }

        if (!moveToProject)
        {
            //Debug.Log("Moving " + packageName + " out of project");
            // Move music if applicable
           /* string musicPath = AssetDatabase.GetAssetPath(_packageList[i]) + "/Music";
            string tempMusicPath = AssetDatabase.GetAssetPath(_tempMusicPath) + "/Music - " + packageName;
            if (AssetDatabase.IsValidFolder(musicPath))
            {
                Debug.Log("Moving music folder");
                AssetDatabase.MoveAsset(musicPath, tempMusicPath);
            }
            else
            {
                Debug.Log("No music found.");
            }*/

            // Move main project
            string currentPath = AssetDatabase.GetAssetPath(_packageList[i]);
            string newTempPath = AssetDatabase.GetAssetPath(_tempPackages) + "/" + _packageList[i].name;
            AssetDatabase.MoveAsset(currentPath, newTempPath);

            // Move Demo Scene
            string demoName = _packageList[i].name.Replace("_", "");
            string tempDemoPath = AssetDatabase.GetAssetPath(_tempDemoPath) + "/" + demoName + ".unity";
            string demoPath = AssetDatabase.GetAssetPath(_demoPath) + "/" + demoName + ".unity";
            if (System.IO.File.Exists(demoPath))
                AssetDatabase.MoveAsset(demoPath, tempDemoPath);
            else
                Debug.LogWarning("Did not find demo scene: " + demoPath);
        }
        ReloadPackageList();
        
        string debugColor = "<size=18><color=yellow>";
        string closeColor = "</color></size>";
       
        Debug.Log(debugColor + "Successfully moved to project!" + closeColor);
        
        string[] colors = new[] {"red", "pink", "purple", "blue", "yellow", "green", "orange"};
        string message = "";
        for (int c = 0; c < colors.Length; c++)
        {
            message = message + "<color=" + colors[c] + ">\u2665</color> ";
        }
    
        for (int n = 0; n < 5; n++)
        {
            message = message + message;
        }
        Debug.Log("<size=24>" + message + "</size>");
    }

    private void ReloadPackageList()
    {
        //Debug.Log("Reloading package list at path: " + AssetDatabase.GetAssetPath(_packages));
        _packageList.Clear();
        String[] objectsFound = AssetDatabase.GetSubFolders(AssetDatabase.GetAssetPath(_packages));
        for (int i = 0; i < objectsFound.Length; i++)
        {
            TryAddingObject(objectsFound[i]);
        }
        objectsFound = AssetDatabase.GetSubFolders(AssetDatabase.GetAssetPath(_tempPackages));
        for (int i = 0; i < objectsFound.Length; i++)
        {
            TryAddingObject(objectsFound[i]);
        }
        _packageList = _packageList.OrderBy(o=>o.name).ToList();
        //Debug.Log("Found " + _packageList.Count + " packages!");
    }

    private void TryAddingObject(String o)
    {
        //Debug.Log("Trying " + o);
        if (o.Contains("_InfinityPBR - ") || o.Contains("_InfinityPBR Human - ") || o.Contains("_InfinityPBR Environment - "))
        {
            //Debug.Log("Adding " + o);
            _packageList.Add((Object)AssetDatabase.LoadAssetAtPath(o, typeof(Object)));
        }
    }
    
    
    
    
    /*
    string[] colors = new[] {"red", "pink", "purple", "blue", "yellow", "green", "orange"};
            string message = "";
            for (int c = 0; c < colors.Length; c++)
            {
                message = message + "<color=" + colors[c] + ">\u2665</color> ";
            }
    
            for (int n = 0; n < 5; n++)
            {
                message = message + message;
            }
            Debug.Log("<size=24>" + message + "</size>");
            */
}
