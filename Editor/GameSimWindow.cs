using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.RemoteConfig.Editor;
using Unity.RemoteConfig.Editor.UIComponents;
using Unity.Simulation.Games.Editor.UIComponents;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using ZipUtility;
using Debug = UnityEngine.Debug;

namespace Unity.Simulation.Games.Editor
{
    internal class GameSimWindow : EditorWindow, ISerializationCallbackReceiver
    {
        // GUI Copy
        const string kMessageString =
        "Create and upload a Linux build for simulation. Note: we will include the scenes from your most recent build. " +
        "If you haven't uploaded a build yet, we will include the open scenes in your project. " +
        "Specify a build name, and select the scenes you want to include from the list.";
        const string kScenesInBuild = "Scenes In Build";
        const string kNoScenesText = "No Scenes Loaded. Either load one or more scenes, or select scenes to include in the Build Settings dialog.";
        const string kLocationText = "Build Location";
        const string kFieldText = "Build Name";
        const string kSignUpText = "Sign Up";
        const string kGoToDashboardText = "Go To Dashboard";
        const string kHelpText = "Go To Forum";
        const string kWebRequestErrorText = "Looks like something went wrong.  Need help?  Please reach out to us by posting on our forum by clicking the button below.";
        const string kNoParametersFoundText = "No parameters found for selected Build.";
        const string kCreateSimulationSuccessBtnText = "View Simulation Details";
        const string kCreateSimulationSuccessText = "Visit the Game Simulation dashboard to view your simulation results.";
        const string kParametersLabelText = "Parameters: ";
        const string kJobNameLabelText = "Name: ";
        const string kJobNameSublabelText = "The name of your simulation";
        const string kBuildIdLabelText = "Build ID: ";
        const string kBuildIdSublabelText = "The ID of the build you want to run";
        const string kMaxRuntimeLabelText = "Max Runtime per Run: ";
        const string kMaxRuntimeSublabelText = "Max runtime per run instance (in minutes)";
        const string kMaxRunsLabelText = "Runs per Parameter Combination: ";
        const string kMaxRunsSublabelText = "Number of runs per parameter values combination";
        const string kParametersSublabelText =
            "The parameters you want to simulate in your simulation.  List comma-separated values in the Values text field to test multiple parameters for a given key e.g. '1, 2, 3'";
        private const string kMessageTypeError = "error";
        private const string kMessageTypeWarning = "warning";
        private const string kMessageTypeInfo = "info";
        private const string kHelpIconHoverText = "View the editor window documentation for more info";
        
        //UI Style variables
        const float k_LineHeight = 22f;
        const float k_LineHeightBuffer = k_LineHeight - 2;
        const float k_LinePadding = 5f;
        private GUIStyle guiStyleLabel = new GUIStyle();
        private GUIStyle guiStyleSubLabel = new GUIStyle();

        // Window Tabs Variables
        private enum GameSimTabs : int
        {
            parameterSetUp,
            buildUpload,
            createSimulation,
        };

        GameSimTabs selectedTab = 0;

        // Are Services Enabled Variables
        GUIContent servicesNotEnabledContent = new GUIContent("To get started with Unity Game Simulation, you must first link your project to a Unity Cloud Project ID. A Unity Cloud Project ID is an online identifier which is used across all Unity Services. These can be created within the Services window itself, or online on the Unity Services website. The simplest way is to use the Services window within Unity, as follows: \nTo open the Services Window, go to Window > General > Services.\nNote: using Unity Game Simulation does not require that you turn on any additional, individual cloud services like Analytics, Ads, Cloud Build, etc.");
        GUIContent missingEntitlementContent = new GUIContent("The Beta period for Unity Game Simulation has ended.  To continue using Game Simulation, please re-enroll through our self serve portal by clicking the button below.");
        bool isMakingHttpCall = false;
        bool hasEntitlement = true;
        bool hasWebRequestError = false;

        // Parameter Setup Variables
        SettingsTreeview treeview;
        string environmentId;
        string configId;
        JArray settings = new JArray();
        string _settings;
        const int buttonWidth = 110;

        // Build Upload Variables
        int selectedScenesCount;
        string[] buildSettingsScenes;
        string[] openScenes;
        string buildName;
        Vector2 scrollPosition;
        Dictionary<string, bool> selectedScenes = new Dictionary<string, bool>();
        List<string> _selectedScenesKeys = new List<string>();
        List<bool> _selectedScenesValues = new List<bool>();
        const int kScrollViewWidth = 490;
        private BuildReport lastBuildReport = null;
        private List<BuildStepMessage> lastBuildErrorMessages = new List<BuildStepMessage>();

        // Metrics Variables
        private GameSimMetrics metrics;
        private UnityEditor.Editor metricsEditor;

        // Create Simulation Variables
        Dictionary<string, string> GameSimLinks = new Dictionary<string, string>() {
            {"forum", "https://forum.unity.com/forums/unity-game-simulation.472"},
            {"package", "https://docs.unity3d.com/Packages/com.unity.simulation.games@0.4/manual/index.html"},
        };
        Dictionary<string, MessageType> CreateJobMessageDict = new Dictionary<string, MessageType>() {
            {kMessageTypeWarning, MessageType.Warning},
            {kMessageTypeError, MessageType.Error},
            {kMessageTypeInfo, MessageType.Info},
        };
        string jobId = null;
        bool hasEmptyParameters = false;

        const int RedirectBtnWidth = 210;
        private UnityWebRequestAsyncOperation GetSimulationBuilds = null;
        private string jobName = "MySimulationName";
        private string buildId;
        List<string> BuildIdsList = new List<string>();
        string[] BuildMenuOptions;
        string selectedBuild;
        string maxRuntime = "15";
        string maxRuns = "5";
        string values;
        //// Simulation Parameter Treeview
        [SerializeField] TreeViewState _simulationParametersTreeviewState;
        private GameSimParametersTreeview _simulationParametersTreeview;

        // Treeview Rects
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

        Rect paramTreeviewRect
        {
            get
            {
                return new Rect(treeviewToolbarRect.x, treeviewToolbarRect.y + treeviewToolbarRect.height, position.width, position.height - toolbarRect.height - treeviewToolbarRect.height);
            }
        }
        
        Rect paramTreeviewFooterRect
        {
            get
            {
                return new Rect(0, paramTreeviewRect.y + paramTreeviewRect.height-3f, paramTreeviewRect.width, k_LineHeight);
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
            // Initialize button links
            GameSimLinks.Add("selfServePortal", $"https://dashboard.unity3d.com/organizations/{CloudProjectSettings.organizationId}/metered-billing/marketplace/products/2771b1e8-4d77-4b34-9b9d-7d6f15ca6ba1");
            GameSimLinks.Add("dashboard", $"https://gamesimulation.unity3d.com/simulations?projectId={Application.cloudProjectId}");

            // Parameter Setup Tab Treeview
            treeview = new SettingsTreeview("Add Parameter", "Parameter", "Default Value");
            treeview.activeSettingsList = new JArray();
            treeview.OnSettingChanged += Treeview_OnSettingChanged;
            RemoteConfigWebApiClient.fetchEnvironmentsFinished += RemoteConfigWebApiClient_fetchEnvironmentsFinished;

            // Create Job Tab Parameter Treeview 
            var keyColumn = new MultiColumnHeaderState.Column()
             {
                 headerContent = new GUIContent("Parameter"),
                 headerTextAlignment = TextAlignment.Center,
                 canSort = false,
                 width = 210,
                 minWidth = 50,
                 autoResize = true,
                 allowToggleVisibility = false
             };
            var typeColumn = new MultiColumnHeaderState.Column()
            {
                headerContent = new GUIContent("Type"),
                headerTextAlignment = TextAlignment.Center,
                canSort = false,
                width = 70,
                minWidth = 50,
                autoResize = true,
                allowToggleVisibility = false
            };
            var defaultValueColumn = new MultiColumnHeaderState.Column()
            {
                headerContent = new GUIContent("Default Value"),
                headerTextAlignment = TextAlignment.Center,
                canSort = false,
                width = 150,
                minWidth = 50,
                autoResize = true,
                allowToggleVisibility = false
            };
            var valuesColumn = new MultiColumnHeaderState.Column()
            {
                headerContent = new GUIContent("Values"),
                headerTextAlignment = TextAlignment.Center,
                canSort = false,
                width = 210,
                minWidth = 50,
                autoResize = true,
                allowToggleVisibility = false
            };

            var headerState = new MultiColumnHeaderState(new MultiColumnHeaderState.Column[]
                { keyColumn, typeColumn, defaultValueColumn, valuesColumn });

            var _simulationMultiColumnHeader = new MultiColumnHeader(headerState);

            // Check whether there is already a serialized view state (state 
            // that survived assembly reloading)
            if (_simulationParametersTreeviewState == null)
                _simulationParametersTreeviewState = new TreeViewState();

            // Create TreeView with column header 
            if (_simulationParametersTreeviewState != null)
            {
                _simulationParametersTreeview = new GameSimParametersTreeview(_simulationParametersTreeviewState, _simulationMultiColumnHeader);
            }

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
            if (EditorPrefs.HasKey("isEntitled"))
                hasEntitlement = EditorPrefs.GetBool("isEntitled", false);

            environmentId = null;
            configId = null;
        }

        private bool AreServicesEnabled()
        {
            GUIStyle style = GUI.skin.label;

            if (string.IsNullOrEmpty(CloudProjectSettings.projectId) || string.IsNullOrEmpty(CloudProjectSettings.organizationId))
            {
                style.wordWrap = true;
                EditorGUILayout.LabelField(servicesNotEnabledContent, style);
                return false;
            }
            
            if (!hasEntitlement)  
            {
                EditorGUILayout.LabelField(missingEntitlementContent, style);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(kSignUpText, new[] { GUILayout.Width(RedirectBtnWidth) }))
                {
                    Help.BrowseURL(GameSimLinks["selfServePortal"]);
                }
                GUILayout.EndHorizontal();
                return false;
            }

            if (hasWebRequestError)
            {
                Debug.LogWarning("Web Request Error: " + kWebRequestErrorText);
                string errorMessage = kWebRequestErrorText;
                EditorGUILayout.LabelField(errorMessage, style);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(kHelpText, new[] { GUILayout.Width(RedirectBtnWidth) }))
                {
                    Help.BrowseURL(GameSimLinks["forum"]);
                }
                GUILayout.EndHorizontal();
                return false;
            }

            return true;
        }

        private void showMessage(Rect messageRect, string messageText, string messageType = "warning")
        {
            EditorGUI.HelpBox(messageRect, messageText, CreateJobMessageDict[messageType]);
        }
        
        private void CreateBuildDropdown(float currentY, Rect parameterPaneRect)
        {
            var labelX = parameterPaneRect.x + 5;
            var textFieldX = labelX + 260f;
            var textFieldWidth = parameterPaneRect.width / 3;
            var labelHeight = (k_LineHeightBuffer * .8f);
            var subLabelHeight = (k_LineHeightBuffer * .8f);
            var subLabelColor = new Color(0.4f, 0.4f, 0.4f, 1.0f);
            var buttonSize = 25f;
            
            guiStyleLabel = GUI.skin.label;
            guiStyleSubLabel.fontSize = 8;
            guiStyleSubLabel.normal.textColor = subLabelColor;
            guiStyleSubLabel.alignment = TextAnchor.UpperLeft;
            guiStyleSubLabel.padding = new RectOffset(2, 0, 0, 2);
            Texture helpButtonTexture = EditorGUIUtility.FindTexture("_Help");
        
            GUI.Label(new Rect(labelX, currentY, 260f, k_LineHeightBuffer), kBuildIdLabelText);
            GUI.Label(new Rect(labelX, currentY + labelHeight, 260f, subLabelHeight), kBuildIdSublabelText, guiStyleSubLabel);
            var textFieldRect = new Rect(textFieldX, currentY, textFieldWidth, k_LineHeightBuffer);
            EditorGUIUtility.AddCursorRect(textFieldRect, MouseCursor.Text);
            if (GUI.Button(new Rect(textFieldX - (2f * buttonSize), currentY, buttonSize, buttonSize), new GUIContent(helpButtonTexture, "View the editor window documentation for more info"), new GUIStyle(GUIStyle.none)))
            {
                Help.BrowseURL(GameSimLinks["package"]);
            }
            void handleItemClicked(object build)
            {
                selectedBuild = build.ToString();
            }
            
            if (GUI.Button(textFieldRect, selectedBuild, EditorStyles.popup))
            {
                var menu = new GenericMenu();
                foreach (var build in BuildMenuOptions)
                    CreateDropdownItemForBuilds(build, menu, handleItemClicked);
                menu.DropDown(textFieldRect);
            }
        }
        
        private void CreateDropdownItemForBuilds(string buildId, GenericMenu menu, GenericMenu.MenuFunction2 Callback)
        {
            menu.AddItem(new GUIContent(buildId), string.Equals(buildId, selectedBuild), Callback, buildId);
        }

        private string CreateLabelWithSubLabelTextFieldAndHelpButton(string labelText, string subLabelText, string textFieldText, float currentY, Rect currentRect)
        {
            var labelX = currentRect.x + 5;
            var labelWidth = 255f;
            var textFieldX = labelX + labelWidth + 5;
            var textFieldWidth = currentRect.width - labelWidth - 15;
            var labelHeight = (k_LineHeightBuffer * .8f);
            var subLabelHeight = (k_LineHeightBuffer * .8f);
            var subLabelColor = new Color(0.4f, 0.4f, 0.4f, 1.0f);
            var buttonSize = 25f;

            guiStyleLabel = GUI.skin.label;
            guiStyleSubLabel.fontSize = 8;
            guiStyleSubLabel.normal.textColor = subLabelColor;
            guiStyleSubLabel.alignment = TextAnchor.UpperLeft;
            guiStyleSubLabel.padding = new RectOffset(2, 0, 0, 2);
            Texture helpButtonTexture = EditorGUIUtility.FindTexture("_Help");

            GUI.Label(new Rect(labelX, currentY, labelWidth, labelHeight), labelText, guiStyleLabel);
            GUI.Label(new Rect(labelX, currentY + labelHeight, labelWidth, subLabelHeight), subLabelText, guiStyleSubLabel);
            var textFieldRect = new Rect(textFieldX, currentY, textFieldWidth, k_LineHeightBuffer);
            EditorGUIUtility.AddCursorRect(textFieldRect, MouseCursor.Text);
            if (GUI.Button(new Rect(textFieldX - (2f * buttonSize), currentY, buttonSize, buttonSize), new GUIContent(helpButtonTexture, kHelpIconHoverText), new GUIStyle(GUIStyle.none)))
            {
                Help.BrowseURL(GameSimLinks["package"]);
            }

            return GUI.TextField(textFieldRect, textFieldText);
        }
        
        private void CreateLabelWithSubLabel(string labelText, string subLabelText, float currentY, Rect currentRect)
        {
            var labelX = currentRect.x + 5;
            var labelWidth = 125f;
            var textFieldX = labelX + labelWidth + 5;
            var textFieldWidth = currentRect.width - labelWidth - 15;
            var labelHeight = (k_LineHeightBuffer * 0.8f);
            var subLabelHeight = (k_LineHeightBuffer * 0.8f);
            var subLabelColor = new Color(0.4f, 0.4f, 0.4f, 1.0f);

            guiStyleLabel = GUI.skin.label;
            guiStyleSubLabel.fontSize = 8;
            guiStyleSubLabel.normal.textColor = subLabelColor;
            guiStyleSubLabel.alignment = TextAnchor.UpperLeft;
            guiStyleSubLabel.padding = new RectOffset(2,0,0,2);

            GUI.Label(new Rect(labelX, currentY, labelWidth, labelHeight), labelText, guiStyleLabel);
            GUI.Label(new Rect(labelX, currentY+labelHeight, labelWidth, subLabelHeight), subLabelText, guiStyleSubLabel);
            var textFieldRect = new Rect(textFieldX, currentY, textFieldWidth, k_LineHeightBuffer);
            EditorGUIUtility.AddCursorRect(textFieldRect, MouseCursor.Text);
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

                if (_simulationParametersTreeview != null)
                {
                    var items = new List<GameSimParametersTreeElement>();
            
                    foreach (var setting in settings)
                    {
                        var element = new GameSimParametersTreeElement
                        {
                            Key = (string)setting["rs"]["key"],
                            Type = (string)setting["rs"]["type"],
                            DefaultValue = (string)setting["rs"]["value"],
                            Values = ""
                        };
            
                        items.Add(element);
                    }

                    _simulationParametersTreeview?.Setup(items.ToArray());
                }

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
                selectedTab = (GameSimTabs)GUILayout.Toolbar((int)selectedTab, new[] {"Parameter Set Up", "Build Upload", "Create Simulation"});

                switch (selectedTab)
                {
                    case GameSimTabs.buildUpload:
                        DrawBuildUpload();
                        GetSimulationBuilds = null;
                        break;
                    case GameSimTabs.parameterSetUp:
                        DrawParameterSetup();
                        GetSimulationBuilds = null;
                        break;
                    case GameSimTabs.createSimulation:
                        CreateSimulationBoot();
                        DrawCreateSimulation();
                        break;
                }

             EditorGUILayout.EndVertical();
            }

            EditorGUI.EndDisabledGroup();
        }

        void DrawBuildUpload()
        {
            EnsureMetricsFileExists();

            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(kGoToDashboardText, new[] { GUILayout.Width(RedirectBtnWidth) }))
                {
                    Help.BrowseURL(GameSimLinks["dashboard"]);
                }

                GUILayout.EndHorizontal();
            }

            buildSettingsScenes = GetBuildSettingScenes();
            openScenes = GetOpenScenes();

            EditorGUILayout.LabelField(kMessageString, EditorStyles.wordWrappedLabel);

            DrawScenes();

            var location = Path.Combine("Assets", "..", "Build", string.IsNullOrEmpty(buildName) ? "BuildName" : buildName);
            EditorGUILayout.LabelField(kLocationText, location);
            buildName = EditorGUILayout.TextField(kFieldText, buildName);
            DrawButtons();

            metricsEditor.DrawDefaultInspector();
        }

        void EnsureMetricsFileExists()
        {
            if (metrics == null)
            {
                metrics = CreateInstance<GameSimMetrics>();
                metricsEditor = UnityEditor.Editor.CreateEditor(metrics);
            }
        }

        void DrawParameterSetup()
        {
            DrawToolbar(treeviewToolbarRect);
            treeview.OnGUI(treeviewRect);
           
        }

        void CreateSimulationBoot()
        {
            
            if (GetSimulationBuilds == null)
            {
                GetSimulationBuilds = GameSimApiClient.instance.GetBuilds();
            }

            if (GetSimulationBuilds.isDone)
            {
                // Error handler
                if (GetSimulationBuilds.webRequest.isHttpError)
                {
                    if (GetSimulationBuilds.webRequest.responseCode == 403)
                    {
                        EditorPrefs.SetBool("isEntitled", false);  // sets entitlement status to EditorPrefs
                    }
                    else
                    {
                        hasWebRequestError = true;
                        Debug.LogWarning("Error: " + GetSimulationBuilds.webRequest.error);
                    }
                }
                else {
                    EditorPrefs.SetBool("isEntitled", true);
                    // Parse and populate builds drop down menu on create simulation form
                    string buildsJsonResponse = GetSimulationBuilds.webRequest.downloadHandler.text;
                    JObject parsedBuildsResponse = JObject.Parse(buildsJsonResponse);
                    var buildIds = from p in parsedBuildsResponse["builds"] select (string)p["buildId"];
                    foreach (var item in buildIds)
                    {
                        BuildIdsList.Add($"{item}");
                    }
                    BuildMenuOptions = BuildIdsList.ToArray();
                } 
            }

        }
        void DrawCreateSimulation()
        {
            // Create Simulation Form
            bool doCreateSimulation = false;
            var createSimulationRect = new Rect(paramTreeviewRect.x, paramTreeviewRect.y, paramTreeviewRect.width, k_LineHeight);
            var currentY = createSimulationRect.y;
            var messageRect = new Rect(paramTreeviewRect.x,paramTreeviewFooterRect.y * .95f - (k_LineHeight * 2), createSimulationRect.width - k_LinePadding, k_LineHeight * 1.5f);
            
            var settingsCount = settings.Count;
            int numErrors = 0;

            EditorGUILayout.LabelField("Create and run a simulation using the form below", EditorStyles.wordWrappedLabel);
            
            var nameRect = new Rect(createSimulationRect.x, createSimulationRect.y, createSimulationRect.width * .65f, createSimulationRect.height);
            
            jobName = CreateLabelWithSubLabelTextFieldAndHelpButton(kJobNameLabelText, kJobNameSublabelText, jobName, currentY, nameRect);
            currentY += 1.4f*k_LineHeight;
            
            if (BuildMenuOptions == null)
                return;
            
            if (BuildMenuOptions.Length > 0)
            {
                CreateBuildDropdown(currentY, createSimulationRect);
                currentY += 1.4f*k_LineHeight;
            }

            maxRuns = CreateLabelWithSubLabelTextFieldAndHelpButton(kMaxRunsLabelText, kMaxRunsSublabelText, maxRuns,
                currentY, nameRect);
            currentY += 1.4f*k_LineHeight;
            
            maxRuntime = CreateLabelWithSubLabelTextFieldAndHelpButton(kMaxRuntimeLabelText, kMaxRuntimeSublabelText, maxRuntime,
                currentY, nameRect);
            currentY += 1.4f*k_LineHeight;
            
            if (settingsCount <= 0)
            {
                CreateLabelWithSubLabel(kParametersLabelText, kNoParametersFoundText, currentY, createSimulationRect);
                currentY += 1.4f*k_LineHeight;
            }
            else
            {
                CreateLabelWithSubLabel(kParametersLabelText, kParametersSublabelText, currentY, createSimulationRect);
                currentY += 1.4f*k_LineHeight;
            }

            var parameterTableRect = new Rect(createSimulationRect.x, currentY, createSimulationRect.width, createSimulationRect.height * 5);
            _simulationParametersTreeview.OnGUI(parameterTableRect);
           
            EditorGUI.BeginDisabledGroup(!string.IsNullOrEmpty(jobId) || string.IsNullOrEmpty(jobName) || !jobNamePatternRegex.IsMatch(jobName) || string.IsNullOrEmpty(maxRuns) ||  selectedBuild == null || !string.IsNullOrEmpty(jobId));
            if (GUI.Button(
                new Rect(k_LinePadding / 2, paramTreeviewFooterRect.y * .95f, createSimulationRect.width - k_LinePadding, k_LineHeight),
                "Run"))
            {
                doCreateSimulation = true;
            }
            EditorGUI.EndDisabledGroup();

            if (doCreateSimulation && jobId == null)
            {
                var parameters = _simulationParametersTreeview.parameterDict;
                hasEmptyParameters = parameters.Values.Any(p => string.IsNullOrEmpty(p.Item2));

                if (hasEmptyParameters)
                {
                    Debug.LogWarning("1 or more Parameter Values required for each parameter in Create Simulation form");
                }
                
                GameSimAnalytics.SendEvent(true);
                var CreateJobResponse = GameSimApiClient.instance.CreateJob(
                    jobName,
                    selectedBuild,
                    parameters,
                    maxRuns,
                    maxRuntime
                );
                jobId = CreateJobResponse;
            }

            // Create job success message
            if (!doCreateSimulation && !string.IsNullOrEmpty(jobId))
            {
                Debug.Log($"Simulation created with id: {jobId}");
                showMessage(messageRect, $"Simulation created with id: {jobId} " + kCreateSimulationSuccessText, kMessageTypeInfo);
                if (GUI.Button(
                    new Rect(k_LinePadding / 2, paramTreeviewFooterRect.y * .85f - k_LineHeight , createSimulationRect.width - k_LinePadding, k_LineHeight),
                    kCreateSimulationSuccessBtnText))
                {
                    Help.BrowseURL(GameSimLinks["dashboard"]);
                }
                if (GUI.Button(
                    new Rect(k_LinePadding / 2, paramTreeviewFooterRect.y * .90f - k_LineHeight , createSimulationRect.width - k_LinePadding, k_LineHeight),
                    "Create Another Simulation"))
                {
                    jobId = "";
                    doCreateSimulation = false;
                }
            }
            
            // Create Simulation Form Validations
            if (string.IsNullOrEmpty(jobName) || !jobNamePatternRegex.IsMatch(jobName))
            {
                numErrors++;
                if (numErrors <= 1)
                {
                    showMessage(messageRect, "Name required, must be alphanumeric and less than 100 characters.", kMessageTypeError);
                }

            } 
            
            if (selectedBuild == null)
            {
                numErrors++;
                if (numErrors <= 1)
                {
                    showMessage(messageRect, "Build ID required.", kMessageTypeError);
                }

            } 
            if (string.IsNullOrEmpty(maxRuns))
            {
                numErrors++;
                if (numErrors <= 1)
                {
                    showMessage(messageRect, "Max Number of Runs per Parameter Combination value required.",
                        kMessageTypeError);
                }
            }
            if (string.IsNullOrEmpty(maxRuntime))
            {
                numErrors++;
                if (numErrors <= 1)
                {
                    showMessage(messageRect, "Max Runtime per Run value required.", kMessageTypeError);
                }
            }
            
            if (hasEmptyParameters)
            {
                numErrors++;
                if (numErrors <= 1)
                {
                    showMessage(messageRect, "1 or more Parameter Values required for each parameter.", kMessageTypeError);
                }
            }
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
        }

        private readonly Regex buildPatternRegex = new Regex(@"^[a-zA-Z0-9]{2,63}$");
        private readonly Regex jobNamePatternRegex = new Regex(@"^[a-zA-Z0-9\-]{2,100}$");

        void DrawButtons()
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
                buildLocation = Path.GetFullPath(buildLocation);  // fixes issue in linux, need to not have the Assets folder in the path
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
                var id = GameSimApiClient.instance.UploadBuild(buildName, zippedBuildFile, metrics.metrics);
                Debug.Log($"Build {buildName} uploaded with build id {id}");
                
                EditorGUILayout.HelpBox(
                    $"Build {buildName} uploaded with build id {id}",
                    MessageType.Info);
                
            }

            if (lastBuildReport != null && lastBuildReport.summary.result != BuildResult.Succeeded)
            {
                DisplayBuildErrors(ref numberHelpBoxes);
            }

            EditorGUI.EndDisabledGroup();
        }

        private void DisplayBuildErrors(ref int numberHelpBoxes)
        {
            if (lastBuildErrorMessages.Count > 0)
            {
                foreach (var message in lastBuildErrorMessages)
                {
                    ++numberHelpBoxes;
                    EditorGUILayout.HelpBox(message.content, MessageType.Error);
                }
            }
            else
            {
                ++numberHelpBoxes;
                EditorGUILayout.HelpBox("Unknown Error", MessageType.Error);
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

        void OnDestroy()
        {
            if (metrics != null)
            {
                DestroyImmediate(metrics);
            }
            
            metrics = null;
            metricsEditor = null;
            EditorPrefs.DeleteKey("isEntitled");
        }
    }
    
   

}

