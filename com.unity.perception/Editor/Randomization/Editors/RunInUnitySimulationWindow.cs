using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Instrumentation;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Simulation.Client;
using UnityEditor.Build.Reporting;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Perception.Randomization.Scenarios;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using ZipUtility;

namespace UnityEditor.Perception.Randomization
{
    class RunInUnitySimulationWindow : EditorWindow
    {
        readonly HttpClient m_HttpClient = new HttpClient();

        string m_BuildDirectory;
        string m_BuildZipPath;
        SysParamDefinition[] m_SysParamDefinitions;
        IntegerField m_InstanceCountField;
        TextField m_RunNameField;
        IntegerField m_TotalIterationsField;
        ToolbarMenu m_SysParamMenu;
        int m_SysParamIndex;
        ObjectField m_ScenarioConfigField;
        Button m_RunButton;
        Label m_PrevRunNameLabel;
        Label m_ProjectIdLabel;
        Label m_PrevExecutionIdLabel;
        RunParameters m_RunParameters;

        #region DSaaS

        TextField m_AuthTokenField;

        #endregion


        [MenuItem("Window/Run in Unity Simulation")]
        static void ShowWindow()
        {
            var window = GetWindow<RunInUnitySimulationWindow>();
            window.titleContent = new GUIContent("Run In Unity Simulation");
            window.minSize = new Vector2(250, 50);
            window.Show();
        }

        void OnEnable()
        {
            m_BuildDirectory = Application.dataPath + "/../Build";
            Project.Activate();
            Project.clientReadyStateChanged += CreateEstablishingConnectionUI;
            CreateEstablishingConnectionUI(Project.projectIdState);
        }

        void OnFocus()
        {
            Application.runInBackground = true;
        }

        void OnLostFocus()
        {
            Application.runInBackground = false;
        }

        void CreateEstablishingConnectionUI(Project.State state)
        {
            rootVisualElement.Clear();
            if (Project.projectIdState == Project.State.Pending)
            {
                var waitingText = new TextElement();
                waitingText.text = "Waiting for connection to Unity Cloud...";
                rootVisualElement.Add(waitingText);
            }
            else if (Project.projectIdState == Project.State.Invalid)
            {
                var waitingText = new TextElement();
                waitingText.text = "The current project must be associated with a valid Unity Cloud project " +
                    "to run in Unity Simulation";
                rootVisualElement.Add(waitingText);
            }
            else
            {
                CreateRunInUnitySimulationUI();
            }
        }

        void CreateRunInUnitySimulationUI()
        {
            var root = rootVisualElement;
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                $"{StaticData.uxmlDir}/RunInUnitySimulationWindow.uxml").CloneTree(root);

            m_RunNameField = root.Q<TextField>("run-name");
            m_TotalIterationsField = root.Q<IntegerField>("total-iterations");
            m_InstanceCountField = root.Q<IntegerField>("instance-count");

            m_SysParamDefinitions = API.GetSysParams();
            m_SysParamMenu = root.Q<ToolbarMenu>("sys-param");
            for (var i = 0; i < m_SysParamDefinitions.Length; i++)
            {
                var index = i;
                var param = m_SysParamDefinitions[i];
                m_SysParamMenu.menu.AppendAction(
                    param.description,
                    action =>
                    {
                        m_SysParamIndex = index;
                        m_SysParamMenu.text = param.description;
                    });
            }

            m_ScenarioConfigField = root.Q<ObjectField>("scenario-config");
            m_ScenarioConfigField.objectType = typeof(TextAsset);
            var configPath = PlayerPrefs.GetString("SimWindow/scenarioConfig");
            if (configPath != string.Empty)
                m_ScenarioConfigField.value = AssetDatabase.LoadAssetAtPath<TextAsset>(configPath);

            m_RunButton = root.Q<Button>("run-button");
            m_RunButton.clicked += RunInUnitySimulation;

            m_PrevRunNameLabel = root.Q<Label>("prev-run-name");
            m_ProjectIdLabel = root.Q<Label>("project-id");
            m_PrevExecutionIdLabel = root.Q<Label>("execution-id");

            var copyExecutionIdButton = root.Q<Button>("copy-execution-id");
            copyExecutionIdButton.clicked += () =>
                EditorGUIUtility.systemCopyBuffer = PlayerPrefs.GetString("SimWindow/prevExecutionId");

            var copyProjectIdButton = root.Q<Button>("copy-project-id");
            copyProjectIdButton.clicked += () =>
                EditorGUIUtility.systemCopyBuffer = CloudProjectSettings.projectId;

            SetFieldsFromPlayerPreferences();

            #region DSaaS

            m_AuthTokenField = root.Q<TextField>("auth-token");
            var token = PlayerPrefs.GetString("latestDsaasAuthToken");
            if (!string.IsNullOrEmpty(token))
            {
                m_AuthTokenField.value = token;
            }

            #endregion

        }

        void SetFieldsFromPlayerPreferences()
        {
            m_RunNameField.value = IncrementRunName(PlayerPrefs.GetString("SimWindow/runName"));
            m_TotalIterationsField.value = PlayerPrefs.GetInt("SimWindow/totalIterations");
            m_InstanceCountField.value = PlayerPrefs.GetInt("SimWindow/instanceCount");
            m_SysParamIndex = PlayerPrefs.GetInt("SimWindow/sysParamIndex");
            m_SysParamMenu.text = m_SysParamDefinitions[m_SysParamIndex].description;
            m_PrevRunNameLabel.text = $"Run Name: {PlayerPrefs.GetString("SimWindow/runName")}";
            m_ProjectIdLabel.text = $"Project ID: {CloudProjectSettings.projectId}";
            m_PrevExecutionIdLabel.text = $"Execution ID: {PlayerPrefs.GetString("SimWindow/prevExecutionId")}";
        }

        static string IncrementRunName(string runName)
        {
            if (string.IsNullOrEmpty(runName))
                return "Run0";
            var stack = new Stack<char>();
            var i = runName.Length - 1;
            for (; i >= 0; i--)
            {
                if (!char.IsNumber(runName[i]))
                    break;
                stack.Push(runName[i]);
            }
            if (stack.Count == 0)
                return runName + "1";
            var numericString = string.Concat(stack.ToArray());
            var runVersion = int.Parse(numericString) + 1;
            return runName.Substring(0, i + 1) + runVersion;
        }

        async void RunInUnitySimulation()
        {
            m_RunParameters = new RunParameters
            {
                runName = m_RunNameField.value,
                totalIterations = m_TotalIterationsField.value,
                instanceCount = m_InstanceCountField.value,
                sysParamIndex = m_SysParamIndex,
                scenarioConfig = (TextAsset)m_ScenarioConfigField.value,
                currentOpenScenePath = SceneManager.GetSceneAt(0).path,
                currentScenario = FindObjectOfType<ScenarioBase>()
            };
            var runGuid = Guid.NewGuid();
            PerceptionEditorAnalytics.ReportRunInUnitySimulationStarted(
                runGuid,
                m_RunParameters.totalIterations,
                m_RunParameters.instanceCount,
                null);
            try
            {
                ValidateSettings();
                //CreateLinuxBuildAndZip();

                SetDsaasEnvVars();
                //await UploadAsDsaasTemplate();
                //await DsaasRefreshTemplates();
                //await DsaasCreateTemplate("test template 1", "helo helo heloooo", true);
                //await DsaasRefreshTemplateVersions(m_TemplateId);
                await DsaasCreateNewTemplateVersion(m_TemplateId, true, new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("testK3","testV3"),
                    new KeyValuePair<string, string>("testK23","testV23")
                });
                //await UploadBuildToDsaasTemplateVersion(m_TemplateId, m_TemplateVersionId);

                //await StartUnitySimulationRun(runGuid);
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                //PerceptionEditorAnalytics.ReportRunInUnitySimulationFailed(runGuid, e.Message);
                throw;
            }
        }

        #region DSaaS


        string m_AuthToken;
        string m_OrgID = "20066313632537";
        string m_StgUrl = "https://perception-api.stg.simulation.unity3d.com";
        string m_TemplateId;
        string m_TemplateVersionId;
        string m_LatestCreatedTemplateId;
        string m_LatestCreatedTemplateVersionId;
        bool m_UseProjectAccessToken = false;

        List<DsaasTempLate> m_DsaasTemplates = new List<DsaasTempLate>();
        Dictionary<string, List<DsaasTemplateVersion>> m_DsaasTemplateVersions = new Dictionary<string, List<DsaasTemplateVersion>>();

        void SetDsaasEnvVars()
        {
            m_AuthToken = m_AuthTokenField.value;
            if (!string.IsNullOrEmpty(m_AuthToken))
            {
                PlayerPrefs.SetString("latestDsaasAuthToken", m_AuthToken);
            }

            m_TemplateId = "24c233a5-b9f9-4fd1-ba6d-eb8bde5abd46";
            m_TemplateVersionId = "f34767a1-cc79-41c6-ad91-88d73bedb6ec";
        }

        struct DsaasTempLate
        {
            public string title;
            public string description;
            public string id;
            [JsonProperty("public")]
            public bool isPublic;
            public string imgSrc;
            public string moreInfo;
        }

        struct DsaasTemplateVersion
        {
            public string id;
            public string templateId;
            public string version;
            public bool published;
            public string authorId;
            [CanBeNull]
            public List<KeyValuePair<string, string>> tags;
        }

        struct DsaasCreateTemplateRequest
        {
            public string title;
            public string description;
            [JsonProperty("public")]
            public bool isPublic;
        }

        struct DsaasCreateTemplateVersionRequest
        {
            public bool published;
            public string version;
            public string authorId;
            [CanBeNull]
            public List<KeyValuePair<string, string>> tags;
            public JToken randomizers;
        }

        struct DsaasGenerateUploadUrlRequest
        {
            public string filename;
        }

        async Task DsaasRefreshTemplates()
        {
            m_HttpClient.DefaultRequestHeaders.Clear();
            m_HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", m_UseProjectAccessToken? Project.accessToken : m_AuthToken);

            var requestUri = new Uri($"{m_StgUrl}/v1/organizations/{m_OrgID}/templates/");

            try
            {
                HttpResponseMessage httpResponse = await m_HttpClient.GetAsync(requestUri);
                if (httpResponse.IsSuccessStatusCode)
                {
                    m_DsaasTemplates.Clear();
                    var responseString = httpResponse.Content.ReadAsStringAsync().Result;
                    var responseJson = JObject.Parse(responseString);
                    m_DsaasTemplates.AddRange(JsonConvert.DeserializeObject<List<DsaasTempLate>>(((JObject)responseJson.GetValue("object"))?.GetValue("templates").ToString()));
                }
            }
            catch (HttpRequestException e)
            {
            }
        }

        async Task DsaasRefreshTemplateVersions(string templateId)
        {
            m_HttpClient.DefaultRequestHeaders.Clear();
            m_HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", m_UseProjectAccessToken? Project.accessToken : m_AuthToken);

            var requestUri = new Uri($"{m_StgUrl}/v1/organizations/{m_OrgID}/templates/{templateId}/versions");

            try
            {
                HttpResponseMessage httpResponse = await m_HttpClient.GetAsync(requestUri);
                if (httpResponse.IsSuccessStatusCode)
                {
                    if (m_DsaasTemplateVersions.ContainsKey(templateId))
                    {
                        m_DsaasTemplateVersions[templateId].Clear();
                    }
                    else
                    {
                        m_DsaasTemplateVersions[templateId] = new List<DsaasTemplateVersion>();
                    }

                    var responseString = httpResponse.Content.ReadAsStringAsync().Result;
                    var responseJson = JObject.Parse(responseString);
                    var versions = ((JObject)responseJson.GetValue("object"))?.GetValue("versions");

                    if (versions != null && versions.HasValues)
                    {
                        m_DsaasTemplateVersions[templateId].AddRange(JsonConvert.DeserializeObject<List<DsaasTemplateVersion>>(versions.ToString()));
                    }
                }
            }
            catch (HttpRequestException e)
            {
            }
        }

        async Task DsaasCreateTemplate(string templateTitle, string templateDescription, bool isTemplatePublic)
        {
            m_HttpClient.DefaultRequestHeaders.Clear();
            m_HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", m_UseProjectAccessToken? Project.accessToken : m_AuthToken);

            var requestUri = new Uri($"{m_StgUrl}/v1/organizations/{m_OrgID}/templates/");

            var request = new DsaasCreateTemplateRequest
            {
                title = templateTitle,
                description = templateDescription,
                isPublic = isTemplatePublic
            };

            HttpContent requestContents = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage httpResponse = await m_HttpClient.PostAsync(requestUri, requestContents);
                if (httpResponse.IsSuccessStatusCode)
                {
                    m_LatestCreatedTemplateId = httpResponse.Headers.Location.Segments.Last();
                }
            }
            catch (HttpRequestException e)
            {
            }
        }

        async Task DsaasCreateNewTemplateVersion(string templateId, bool published, List<KeyValuePair<string,string>> tags = null)
        {
            await DsaasRefreshTemplateVersions(templateId);

            var currentScenario = FindObjectOfType<ScenarioBase>();

            if (!currentScenario)
            {
                //Todo: throw exception
                Debug.Log("No scenario was found.");
                return;
            }

            m_HttpClient.DefaultRequestHeaders.Clear();
            m_HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", m_UseProjectAccessToken? Project.accessToken : m_AuthToken);

            var requestUri = new Uri($"{m_StgUrl}/v1/organizations/{m_OrgID}/templates/{templateId}/versions");

            if (!m_DsaasTemplateVersions.TryGetValue(templateId, out var versions))
            {
                //TODO: throw exception
                Debug.Log("Could not retrieve versions list for requested template.");
                return;
            }

            versions = versions.OrderByDescending(ver => ver.version).ToList();
            string newVersionString;

            if (versions.Count != 0)
            {
                var currentVersionString = versions.First().version;
                var currentVersionNumber = float.Parse(currentVersionString.Substring(1));
                var newVersionNumber = currentVersionNumber + 0.1f;
                newVersionString = $"v{newVersionNumber}";
            }
            else
            {
                newVersionString = "V1.0";
            }

            //TODO: figure out versioning scheme

            var request = new DsaasCreateTemplateVersionRequest
            {
                published = published,
                version = newVersionString,
                tags = tags,
                authorId = m_OrgID,
                randomizers = JObject.Parse(currentScenario.SerializeToJson()).GetValue("randomizers")
            };

            HttpContent requestContents = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage httpResponse = await m_HttpClient.PostAsync(requestUri, requestContents);
                if (httpResponse.IsSuccessStatusCode)
                {
                    m_LatestCreatedTemplateVersionId = httpResponse.Headers.Location.Segments.Last();
                }
            }
            catch (HttpRequestException e)
            {
            }
        }

        async Task UploadBuildToDsaasTemplateVersion(string templateId, string templateVersionId)
        {
            //CreateLinuxBuildAndZip();

            var projectBuildDirectory = $"{m_BuildDirectory}/{m_RunNameField.value}";
            m_BuildZipPath = projectBuildDirectory + ".zip";

            m_HttpClient.DefaultRequestHeaders.Clear();
            m_HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", m_UseProjectAccessToken? Project.accessToken : m_AuthToken);

            var requestUri = new Uri($"{m_StgUrl}/v1/organizations/{m_OrgID}/templates/{templateId}/versions/{templateVersionId}:uploadUrl");

            var uploadUrlRequest = new DsaasGenerateUploadUrlRequest()
            {
                filename = "test.zip"
            };

            string uploadUrl = string.Empty;

            HttpContent requestContents = new StringContent(JsonConvert.SerializeObject(uploadUrlRequest), Encoding.UTF8, "application/json");
            try
            {
                HttpResponseMessage httpResponse = await m_HttpClient.PostAsync(requestUri, requestContents);

                if (httpResponse.IsSuccessStatusCode)
                {
                    var responseString = httpResponse.Content.ReadAsStringAsync().Result;
                    var responseJson = JObject.Parse(responseString);
                    uploadUrl = responseJson.GetValue("url").ToString();
                    Debug.Log("expires: " + responseJson.GetValue("expires"));
                }
            }
            catch (HttpRequestException e)
            {
            }

            if (!string.IsNullOrEmpty(uploadUrl))
            {
                var stream = File.OpenRead(m_BuildZipPath);
                requestContents = new StreamContent(stream);
                requestContents.Headers.Add("Content-Type", "application/zip");
                HttpResponseMessage httpResponse = await m_HttpClient.PutAsync(uploadUrl, requestContents);

                Debug.Log(httpResponse.StatusCode);
                Debug.Log(httpResponse.Headers);
                Debug.Log(httpResponse.Content);
            }
        }


        #endregion

        void ValidateSettings()
        {
            if (string.IsNullOrEmpty(m_RunParameters.runName))
                throw new MissingFieldException("Empty run name");
            if (m_RunParameters.instanceCount <= 0)
                throw new NotSupportedException("Invalid instance count specified");
            if (m_RunParameters.totalIterations <= 0)
                throw new NotSupportedException("Invalid total iteration count specified");
            if (string.IsNullOrEmpty(m_RunParameters.currentOpenScenePath))
                throw new MissingFieldException("Invalid scene path");
            if (m_RunParameters.currentScenario == null)
                throw new MissingFieldException(
                    "There is not a Unity Simulation compatible scenario present in the scene");
            if (!StaticData.IsSubclassOfRawGeneric(
                typeof(UnitySimulationScenario<>), m_RunParameters.currentScenario.GetType()))
                throw new NotSupportedException(
                    "Scenario class must be derived from UnitySimulationScenario to run in Unity Simulation");
            if (m_RunParameters.scenarioConfig != null &&
                Path.GetExtension(m_RunParameters.scenarioConfigAssetPath) != ".json")
                throw new NotSupportedException(
                    "Scenario configuration must be a JSON text asset");
        }

        void CreateLinuxBuildAndZip()
        {
            var projectBuildDirectory = $"{m_BuildDirectory}/{m_RunParameters.runName}";
            if (!Directory.Exists(projectBuildDirectory))
                Directory.CreateDirectory(projectBuildDirectory);
            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { m_RunParameters.currentOpenScenePath },
                locationPathName = Path.Combine(projectBuildDirectory, $"{m_RunParameters.runName}.x86_64"),
                target = BuildTarget.StandaloneLinux64
            };
            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                throw new Exception($"The Linux build did not succeed: status = {summary.result}");

            EditorUtility.DisplayProgressBar("Unity Simulation Run", "Zipping Linux build...", 0f);
            Zip.DirectoryContents(projectBuildDirectory, m_RunParameters.runName);
            m_BuildZipPath = projectBuildDirectory + ".zip";
        }

        List<AppParam> UploadAppParam()
        {
            var appParamIds = new List<AppParam>();
            var configuration = JObject.Parse(m_RunParameters.scenarioConfig != null
                ? File.ReadAllText(m_RunParameters.scenarioConfigAssetPath)
                : m_RunParameters.currentScenario.SerializeToJson());

            var constants = configuration["constants"];
            constants["totalIterations"] = m_RunParameters.totalIterations;
            constants["instanceCount"] = m_RunParameters.instanceCount;

            var appParamName = $"{m_RunParameters.runName}";
            var appParamsString = JsonConvert.SerializeObject(configuration, Formatting.Indented);
            var appParamId = API.UploadAppParam(appParamName, appParamsString);
            appParamIds.Add(new AppParam
            {
                id = appParamId,
                name = appParamName,
                num_instances = m_RunParameters.instanceCount
            });
            return appParamIds;
        }

        async Task StartUnitySimulationRun(Guid runGuid)
        {
            m_RunButton.SetEnabled(false);

            // Upload build
            var cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            var buildId = await API.UploadBuildAsync(
                m_RunParameters.runName,
                m_BuildZipPath,
                null, null,
                cancellationTokenSource,
                progress =>
                {
                    EditorUtility.DisplayProgressBar(
                        "Unity Simulation Run", "Uploading build...", progress * 0.90f);
                });
            if (token.IsCancellationRequested)
            {
                Debug.Log("The build upload process has been cancelled. Aborting Unity Simulation launch.");
                EditorUtility.ClearProgressBar();
                return;
            }

            // Generate and upload app-params
            EditorUtility.DisplayProgressBar("Unity Simulation Run", "Uploading app-params...", 0.90f);
            var appParams = UploadAppParam();

            // Upload run definition
            EditorUtility.DisplayProgressBar("Unity Simulation Run", "Uploading run definition...", 0.95f);
            var runDefinitionId = API.UploadRunDefinition(new RunDefinition
            {
                app_params = appParams.ToArray(),
                name = m_RunParameters.runName,
                sys_param_id = m_SysParamDefinitions[m_RunParameters.sysParamIndex].id,
                build_id = buildId
            });

            // Execute run
            EditorUtility.DisplayProgressBar("Unity Simulation Run", "Executing run...", 1f);
            var run = Run.CreateFromDefinitionId(runDefinitionId);
            run.Execute();

            // Cleanup
            m_RunButton.SetEnabled(true);
            EditorUtility.ClearProgressBar();
            PerceptionEditorAnalytics.ReportRunInUnitySimulationSucceeded(runGuid, run.executionId);

            // Set new Player Preferences
            PlayerPrefs.SetString("SimWindow/runName", m_RunParameters.runName);
            PlayerPrefs.SetString("SimWindow/prevExecutionId", run.executionId);
            PlayerPrefs.SetInt("SimWindow/totalIterations", m_RunParameters.totalIterations);
            PlayerPrefs.SetInt("SimWindow/instanceCount", m_RunParameters.instanceCount);
            PlayerPrefs.SetInt("SimWindow/sysParamIndex", m_RunParameters.sysParamIndex);
            PlayerPrefs.SetString("SimWindow/scenarioConfig",
                m_RunParameters.scenarioConfig != null ? m_RunParameters.scenarioConfigAssetPath : string.Empty);

            SetFieldsFromPlayerPreferences();
        }

        struct RunParameters
        {
            public string runName;
            public int totalIterations;
            public int instanceCount;
            public int sysParamIndex;
            public TextAsset scenarioConfig;
            public string currentOpenScenePath;
            public ScenarioBase currentScenario;

            public string scenarioConfigAssetPath => AssetDatabase.GetAssetPath(scenarioConfig);
        }
    }
}
