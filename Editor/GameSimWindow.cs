using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using Unity.RemoteConfig.Editor;
using Unity.RemoteConfig.Editor.UIComponents;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine.SceneManagement;
using System.IO;
using UnityEditor.Build.Reporting;
using System.Diagnostics;
using ZipUtility;
using Debug = UnityEngine.Debug;

namespace Unity.Simulation.Games.Editor
{
    internal class GameSimWindow : EditorWindow, ISerializationCallbackReceiver
    {
        private enum GameSimTabs : int
        {
            parameterSetUp,
            buildUpload,
        };

        SettingsTreeview treeview;

        string environmentId;
        string configId;

        JArray settings = new JArray();
        string _settings;
        const float k_LineHeight = 22f;
        const int buttonWidth = 110;

        GUIContent servicesNotEnabledContent = new GUIContent("To get started with Unity Game Simulation, you must first link your project to a Unity Cloud Project ID. A Unity Cloud Project ID is an online identifier which is used across all Unity Services. These can be created within the Services window itself, or online on the Unity Services website. The simplest way is to use the Services window within Unity, as follows: \nTo open the Services Window, go to Window > General > Services.\nNote: using Unity Game Simulation does not require that you turn on any additional, individual cloud services like Analytics, Ads, Cloud Build, etc.");

        bool isMakingHttpCall = false;

        int selectedScenesCount;
        string[] buildSettingsScenes;
        string[] openScenes;
        string buildName;
        Vector2 scrollPosition;
        Dictionary<string, bool> selectedScenes = new Dictionary<string, bool>();
        List<string> _selectedScenesKeys = new List<string>();
        List<bool> _selectedScenesValues = new List<bool>();
        const int kScrollViewWidth = 490;

        GameSimTabs selectedTab = 0;

        const string kMessageString =
        "Create and upload a Linux build for simulation. Note: we will include the scenes from your most recent build. " +
        "If you haven't uploaded a build yet, we will include the open scenes in your project. " +
        "Specify a build name, and select the scenes you want to include from the list.";

        const string kScenesInBuild = "Scenes In Build";
        const string kNoScenesText = "No Scenes Loaded. Either load one or more scenes, or select scenes to include in the Build Settings dialog.";
        const string kLocationText = "Build Location";
        const string kFieldText = "Build Name";

        private BuildReport lastBuildReport = null;
        private List<BuildStepMessage> lastBuildErrorMessages = new List<BuildStepMessage>();

        Rect toolbarRect
        {
            get
            {
                return new Rect(0, 0, position.width / 2, 1.5f * k_LineHeight);
            }
        }

        Rect treeviewToolbarRect
        {
            get
            {
                return new Rect(toolbarRect.x, toolbarRect.y + toolbarRect.height, position.width, 1.1f * k_LineHeight);
            }
        }

        Rect treeviewRect
        {
            get
            {
                return new Rect(treeviewToolbarRect.x, treeviewToolbarRect.y + treeviewToolbarRect.height, position.width, position.height - toolbarRect.height - treeviewToolbarRect.height);
            }
        }

        Rect buildUploadHelpTextRect
        {
            get
            {
                return new Rect(toolbarRect.x, toolbarRect.y + toolbarRect.height, position.width, 2 * k_LineHeight);
            }
        }

        [MenuItem("Window/Game Simulation")]
        public static void GetWindow()
        {
            var GSWindow = GetWindow<GameSimWindow>();
            GSWindow.titleContent = new GUIContent("Game Simulation");
            GSWindow.minSize = new Vector2(600, 380);
            GSWindow.Focus();
            GSWindow.Repaint();
        }

        private void OnEnable()
        {
            treeview = new SettingsTreeview("Add Parameter", "Parameter", "Default Value");
            treeview.activeSettingsList = new JArray();
            treeview.OnSettingChanged += Treeview_OnSettingChanged;
            RemoteConfigWebApiClient.fetchEnvironmentsFinished += RemoteConfigWebApiClient_fetchEnvironmentsFinished;
            ResetState();
        }

        void InitIfNeeded()
        {
            if (string.IsNullOrEmpty(environmentId) && !isMakingHttpCall)
            {
                FetchEnvironments();
            }
        }

        public void ResetState()
        {
            isMakingHttpCall = false;
            environmentId = null;
            configId = null;
        }

        private bool AreServicesEnabled()
        {
            if (string.IsNullOrEmpty(CloudProjectSettings.projectId) || string.IsNullOrEmpty(CloudProjectSettings.organizationId))
            {

                GUIStyle style = GUI.skin.label;
                style.wordWrap = true;
                EditorGUILayout.LabelField(servicesNotEnabledContent, style);
                return false;
            }
            return true;
        }

        private void Treeview_OnSettingChanged(JObject arg1, JObject arg2)
        {
            if (arg1 == null && arg2 != null)
            {
                //new setting
                var newSetting = new JObject()
                {
                    {
                        "metadata", new JObject()
                        {
                            {
                                "entityId", Guid.NewGuid().ToString()
                            }
                        }
                    },
                    {
                        "rs", new JObject()
                        {
                            {
                                "key", "new-key-" + settings.Count
                            },
                            {
                                "type", ""
                            },
                            {
                                "value", ""
                            }
                        }
                    }
                };
                settings.Add(newSetting);
                UpdateSettingsTreeview(settings);
            }

            else if (arg1 != null && arg2 == null)
            {
                //delete setting
                for (int i = 0; i < settings.Count; i++)
                {
                    if (settings[i]["metadata"]["entityId"].Value<string>() == arg1["metadata"]["entityId"].Value<string>())
                    {
                        settings.RemoveAt(i);
                        break;
                    }
                }
                UpdateSettingsTreeview(settings);
            }
            else if (arg1 != null)
            {
                // update setting
                for (int i = 0; i < settings.Count; i++)
                {
                    if (settings[i]["metadata"]["entityId"].Value<string>() == arg2["metadata"]["entityId"].Value<string>())
                    {
                        settings[i] = arg2;
                        break;
                    }
                }
                UpdateSettingsTreeview(settings);
            }
        }

        void UpdateSettingsTreeview(JArray newSettings)
        {
            treeview.settingsList = newSettings;
            treeview.activeSettingsList = newSettings;
        }

        private void RemoteConfigWebApiClient_fetchEnvironmentsFinished(JArray environments)
        {
            JObject gsEnv = null;
            foreach (var environment in environments)
            {
                if (environment["name"].Value<string>() == "GameSim")
                {
                    gsEnv = (JObject)environment;
                    environmentId = gsEnv["id"].Value<string>();
                    FetchConfig(environmentId);
                    break;
                }
            }
            if (gsEnv == null)
            {
                RemoteConfigWebApiClient.environmentCreated += RemoteConfigWebApiClient_environmentCreated;
                RemoteConfigWebApiClient.CreateEnvironment(Application.cloudProjectId, "GameSim");
            }
        }

        void FetchConfig(string envId)
        {
            RemoteConfigWebApiClient.fetchConfigsFinished += RemoteConfigWebApiClient_fetchConfigsFinished;
            RemoteConfigWebApiClient.FetchConfigs(Application.cloudProjectId, envId);
        }

        private void RemoteConfigWebApiClient_fetchConfigsFinished(JObject config)
        {
            RemoteConfigWebApiClient.fetchConfigsFinished -= RemoteConfigWebApiClient_fetchConfigsFinished;
            if (config.HasValues)
            {
                configId = config["id"].Value<string>();
                settings = AddMetadataToSettings((JArray)config["value"]);
                UpdateSettingsTreeview(settings);
            }
            else
            {
                RemoteConfigWebApiClient.postConfigRequestFinished += RemoteConfigWebApiClient_postConfigRequestFinished;
                RemoteConfigWebApiClient.PostConfig(Application.cloudProjectId, environmentId, new JArray());
            }
            isMakingHttpCall = false;
        }

        JArray AddMetadataToSettings(JArray settingsArr)
        {
            var returnArr = new JArray();

            foreach (var t in settingsArr)
            {
                returnArr.Add(new JObject()
                {
                    {
                        "metadata", new JObject()
                        {
                            {
                                "entityId", Guid.NewGuid().ToString()
                            }
                        }
                    },
                    {
                        "rs", t
                    }
                });
            }

            return returnArr;
        }

        private void RemoteConfigWebApiClient_postConfigRequestFinished(string obj)
        {
            RemoteConfigWebApiClient.postConfigRequestFinished -= RemoteConfigWebApiClient_postConfigRequestFinished;
            configId = obj;
        }

        void FetchEnvironments()
        {
            isMakingHttpCall = true;
            RemoteConfigWebApiClient.FetchEnvironments(Application.cloudProjectId);
        }

        private void RemoteConfigWebApiClient_environmentCreated(string envId)
        {
            environmentId = envId;
            RemoteConfigWebApiClient.environmentCreated -= RemoteConfigWebApiClient_environmentCreated;
            FetchConfig(environmentId);
        }

        private void OnDisable()
        {
            RemoteConfigWebApiClient.fetchEnvironmentsFinished -= RemoteConfigWebApiClient_fetchEnvironmentsFinished;
            treeview.OnSettingChanged -= Treeview_OnSettingChanged;
        }

        private void OnGUI()
        {
            if (!AreServicesEnabled())
            {
                return;
            }

            InitIfNeeded();
            EditorGUI.BeginDisabledGroup(isMakingHttpCall);

            if (!GameSimApiClient.instance.gamesimUrl.Equals("https://api.prd.gamesimulation.unity3d.com"))
            {
                GUI.Label(new Rect(0, toolbarRect.y, toolbarRect.width, k_LineHeight), GameSimApiClient.instance.gamesimUrl);
            }

            {
                EditorGUILayout.BeginVertical();
                selectedTab = (GameSimTabs)GUILayout.Toolbar((int)selectedTab, new[] {"Parameter Set Up", "Build Upload"});

                switch (selectedTab)
                {
                    case GameSimTabs.buildUpload:
                        DrawBuildUpload();
                        break;
                    case GameSimTabs.parameterSetUp:
                        DrawParameterSetup();
                        break;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUI.EndDisabledGroup();
        }

        void DrawBuildUpload()
        {
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Create Simulation", new[] {GUILayout.Width(buttonWidth)}))
                {
                    Help.BrowseURL($"https://gamesimulation.unity3d.com/simulations/new?projectId={Application.cloudProjectId}");
                }

                GUILayout.EndHorizontal();
            }

            buildSettingsScenes = GetBuildSettingScenes();
            openScenes = GetOpenScenes();

            EditorGUILayout.LabelField(kMessageString, EditorStyles.wordWrappedLabel);

            DrawScenes();
        }

        void DrawParameterSetup()
        {
            DrawToolbar(treeviewToolbarRect);
            treeview.OnGUI(treeviewRect);
        }

        void DrawScenes()
        {
            selectedScenesCount = 0;
                var rect = new Rect(0, (float)(1.5 * k_LineHeight), position.width, position.height - (float)(3.5 * k_LineHeight));
            var labelRect = new Rect(rect.x, rect.y, rect.width, k_LineHeight);
            var scrollRect = new Rect(labelRect.x, labelRect.y + labelRect.height, rect.width, k_LineHeight * 6);

            var scenes = buildSettingsScenes == null || buildSettingsScenes.Length == 0 ? openScenes : buildSettingsScenes;

            EditorGUILayout.LabelField(kScenesInBuild, EditorStyles.boldLabel);
            {
                float minHeight = k_LineHeight;
                var boxRect = GUILayoutUtility.GetRect(0, kScrollViewWidth, k_LineHeight, k_LineHeight * 10);

                var displayBoxRect = new Rect(boxRect.x + k_LineHeight / 2, boxRect.y, boxRect.width - k_LineHeight, boxRect.height);
                GUI.Box(displayBoxRect, "", EditorStyles.helpBox);

                var scenesListRect = new Rect(displayBoxRect.x + 5, displayBoxRect.y, displayBoxRect.width - 10, displayBoxRect.height);
                scrollPosition = GUI.BeginScrollView(scenesListRect, scrollPosition, new Rect(0, 0, kScrollViewWidth, k_LineHeight * scenes.Length));
                if (scenes != null && scenes.Length > 0)
                {
                    for (int i = 0; i < scenes.Length; i++)
                    {
                        var selected = false;
                        if (scenes[i] != null)
                        {
                            selectedScenes.TryGetValue(scenes[i], out selected);
                        }
                        selected = GUI.Toggle(new Rect(0, 0 + (k_LineHeight * i), kScrollViewWidth, k_LineHeight), selected, scenes[i]);
                        if (scenes[i] != null)
                        {
                            selectedScenes[scenes[i]] = selected;
                        }
                        selectedScenesCount += selected ? 1 : 0;
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(kNoScenesText, EditorStyles.wordWrappedLabel);
                }

                GUI.EndScrollView();
            }

            var location = Path.Combine("Assets", "..", "Build", string.IsNullOrEmpty(buildName) ? "BuildName" : buildName);
            var locationRect = new Rect(scrollRect.x, scrollRect.y + scrollRect.height + k_LineHeight, scrollRect.width, k_LineHeight);
            EditorGUILayout.LabelField(kLocationText, location);
            var buildNameRect = new Rect(locationRect.x, locationRect.y + locationRect.height, locationRect.width, 20);
            buildName = EditorGUILayout.TextField(kFieldText, buildName);
            DrawButtons(new Rect(buildNameRect.x, buildNameRect.y + buildNameRect.height + k_LineHeight, rect.width, k_LineHeight));
        }


        private readonly Regex buildPatternRegex = new Regex(@"^[a-zA-Z0-9]{2,63}$");
        void DrawButtons(Rect rect)
        {
            int numberHelpBoxes = 0;

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(buildName) || !buildPatternRegex.IsMatch(buildName) || selectedScenesCount == 0);

            bool doBuildUpload = false;
            bool doUpload = false;
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                doBuildUpload = GUILayout.Button("Build and Upload", new[] {GUILayout.Width(buttonWidth)});
                doUpload = GUILayout.Button("Upload Build", new[] { GUILayout.Width(buttonWidth) });
                GUILayout.EndHorizontal();
            }

            if (string.IsNullOrEmpty(buildName) || !buildPatternRegex.IsMatch(buildName))
            {
                ++numberHelpBoxes;
                EditorGUILayout.HelpBox(
                    "Names must be between 2 and 63 characters long and can only contain alpha numeric characters.",
                    MessageType.Error);
            }


            string zippedBuildFile = null;
            if (doBuildUpload)
            {
                var includedScenes = new List<string>(selectedScenesCount);
                foreach (var kv in selectedScenes)
                {
                    if (kv.Value)
                    {
                        includedScenes.Add(kv.Key);
                    }
                }

                var buildLocation = Path.Combine(Application.dataPath, "..", "Build", buildName);
                Directory.CreateDirectory(buildLocation);
                lastBuildReport = BuildProject(buildLocation, buildName, includedScenes.ToArray(), BuildTarget.StandaloneLinux64, compress: true, launch: false);

                if (lastBuildReport.summary.result == BuildResult.Succeeded)
                {
                    zippedBuildFile = $"{buildLocation}.zip";
                }
                else
                {
                    lastBuildErrorMessages.Clear();
                    foreach (var step in lastBuildReport.steps)
                    {
                        foreach (var message in step.messages)
                        {
                            if (message.type == LogType.Error)
                            {
                                lastBuildErrorMessages.Add(message);
                            }
                        }
                    }
                }
            }

            if (doUpload)
            {
                zippedBuildFile = EditorUtility.OpenFilePanel("game simulation build archive", "", "zip");
            }

            if (doBuildUpload || doUpload)
            {
                var id = GameSimApiClient.instance.UploadBuild(buildName, zippedBuildFile);
                Debug.Log($"Build {buildName} uploaded with build id {id}");
            }

            if (lastBuildReport != null && lastBuildReport.summary.result != BuildResult.Succeeded)
            {
                DisplayBuildErrors(rect, ref numberHelpBoxes);
            }

            EditorGUI.EndDisabledGroup();
        }

        private void DisplayBuildErrors(Rect rect, ref int numberHelpBoxes)
        {
            if (lastBuildErrorMessages.Count > 0)
            {
                foreach (var message in lastBuildErrorMessages)
                {
                    ++numberHelpBoxes;
                    EditorGUI.HelpBox(
                        new Rect(rect.x + 50, rect.y + numberHelpBoxes * (k_LineHeight + 5), rect.width - 100,
                            k_LineHeight),
                        message.content,
                        MessageType.Error);
                }
            }
            else
            {
                ++numberHelpBoxes;
                EditorGUI.HelpBox(
                    new Rect(rect.x + 50, rect.y + numberHelpBoxes * (k_LineHeight + 5), rect.width - 100,
                        k_LineHeight),
                    "Unknown Error",
                    MessageType.Error);
            }
        }

        public static BuildReport BuildProject(string savePath, string name, string[] scenes = null, BuildTarget target = BuildTarget.StandaloneLinux64, bool compress = true, bool launch = false)
        {
            var currentScriptingDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
            bool alreadyContains = currentScriptingDefines.EndsWith(";UNITY_GAME_SIMULATION") ||
                                   currentScriptingDefines.Contains("UNITY_GAME_SIMULATION;") ||
                                   currentScriptingDefines.Equals("UNITY_GAME_SIMULATION");

            if (!alreadyContains)
            {
                if (currentScriptingDefines.Length == 0)
                {
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, "UNITY_GAME_SIMULATION");
                }
                else
                {
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, "UNITY_GAME_SIMULATION;" + currentScriptingDefines);
                }
            }
            
            Directory.CreateDirectory(savePath);

#if !UNITY_2019_1_OR_NEWER
            var displayResolutionDialog = PlayerSettings.displayResolutionDialog;
            PlayerSettings.displayResolutionDialog = ResolutionDialogSetting.Disabled;
#endif
            var runInBackground = PlayerSettings.runInBackground;
            PlayerSettings.runInBackground = true;

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.locationPathName = Path.Combine(savePath, name + ".x86_64");
            buildPlayerOptions.target = target;
            buildPlayerOptions.options = BuildOptions.None;
            buildPlayerOptions.scenes = scenes;

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (!alreadyContains)
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, currentScriptingDefines);
            }

#if !UNITY_2019_1_OR_NEWER
            PlayerSettings.displayResolutionDialog = displayResolutionDialog;
#endif
            PlayerSettings.runInBackground = runInBackground;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Build succeeded: {summary.totalSize} bytes");
            }

            if (summary.result == BuildResult.Failed)
            {
                Debug.Log("Build failed");
                return report;
            }

            if (launch)
            {
                var exe = Path.Combine(Application.dataPath, "..", savePath + ".app");
                Debug.Log("Executing " + exe);
                Process.Start(exe);
            }

            if (compress)
            {
                Zip.DirectoryContents(savePath, name);
            }

            return report;
        }

        public static string[] GetOpenScenes()
        {
            var countLoaded = SceneManager.sceneCount;
            var loadedScenes = new string[countLoaded];

            for (int i = 0; i < countLoaded; i++)
            {
                loadedScenes[i] = SceneManager.GetSceneAt(i).path;
            }

            return loadedScenes;
        }

        public static string[] GetBuildSettingScenes()
        {
            var countLoaded = SceneManager.sceneCountInBuildSettings;
            var loadedScenes = new string[countLoaded];
            for (int i = 0; i < countLoaded; i++)
            {
                loadedScenes[i] = SceneUtility.GetScenePathByBuildIndex(i);
            }
            return loadedScenes;
        }

        void DrawToolbar(Rect rect)
        {
            var buttonsRect = new Rect(rect.x, rect.y, rect.width / 2, k_LineHeight);
            var pushBtnRect = new Rect(buttonsRect.x + (rect.width / 2), buttonsRect.y, buttonsRect.width / 2, buttonsRect.height);
            var pullBtnRect = new Rect(pushBtnRect.x + pushBtnRect.width, buttonsRect.y, buttonsRect.width / 2, buttonsRect.height);
            if (GUI.Button(pullBtnRect, new GUIContent("Save")))
            {
                PushSettings(settings);
            }
        }

        void PushSettings(JArray settingsArr)
        {
            isMakingHttpCall = true;
            var newSettings = new JArray();

            foreach (var t in settingsArr)
            {
                newSettings.Add(t["rs"].DeepClone());
            }

            RemoteConfigWebApiClient.settingsRequestFinished += RemoteConfigWebApiClient_settingsRequestFinished;
            RemoteConfigWebApiClient.PutConfig(Application.cloudProjectId, environmentId, configId, newSettings);
        }

        private void RemoteConfigWebApiClient_settingsRequestFinished()
        {
            isMakingHttpCall = false;
            RemoteConfigWebApiClient.settingsRequestFinished -= RemoteConfigWebApiClient_settingsRequestFinished;
        }

        public void OnBeforeSerialize()
        {
            _settings = settings.ToString(Newtonsoft.Json.Formatting.None);
            _selectedScenesKeys = new List<string>(selectedScenes.Count);
            _selectedScenesValues = new List<bool>(selectedScenes.Count);
            foreach (var kvp in selectedScenes)
            {
                _selectedScenesKeys.Add(kvp.Key);
                _selectedScenesValues.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            settings = _settings != null ? JArray.Parse(_settings) : new JArray();

            selectedScenes = new Dictionary<string, bool>();

            for (int i = 0; i != Math.Min(_selectedScenesKeys.Count, _selectedScenesValues.Count); i++)
            {
                selectedScenes.Add(_selectedScenesKeys[i], _selectedScenesValues[i]);
            }
        }
    }

}