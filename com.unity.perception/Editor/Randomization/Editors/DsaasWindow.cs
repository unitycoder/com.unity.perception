﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Simulation.Client;
using UnityEditor.Build.Reporting;
using UnityEditor.Perception.Dsaas.DataModels;
using UnityEditor.Perception.Randomization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Perception.Randomization.Scenarios;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using ZipUtility;

namespace UnityEditor.Perception.Dsaas
{
    class DsaasWindow : EditorWindow
    {
        readonly HttpClient m_HttpClient = new HttpClient();

        string m_BuildDirectory;
        string m_BuildZipPath;
        SysParamDefinition[] m_SysParamDefinitions;
        IntegerField m_InstanceCountField;
        TextField m_BuildNameField;
        ObjectField m_ScenarioConfigField;
        Button m_UploadButton;

        Label m_ProjectIdLabel;

        ListView m_DsaasTemplatesListView;
        ListView m_DsaasSelectedTemplateVersionsListView;
        Label m_DsaasSelectedTemplateIdLabel;
        Label m_DsaasSelectedVersionIdLabel;
        Label m_NoVersionsLabel;
        Label m_RefreshingLabel;
        Button m_DsaasRefreshTemplatesButton;
        Button m_DsaasCreateNewTemplate;
        Button m_DsaasCreateNewTemplateVersion;
        VisualElement m_VersionsContainer;
        VisualElement m_UploadSection;

        BuildParameters m_BuildParameters;

        TextField m_AuthTokenField;


        string m_AuthToken;
        string m_OrgID = "20066313632537";
        string m_StgUrl = "https://perception-api.stg.simulation.unity3d.com";
        string m_SelectedTemplateId;
        string m_SelectedTemplateVersionId;
        bool m_UseProjectAccessToken = false;

        List<DsaasTemplate> m_DsaasTemplates = new List<DsaasTemplate>();
        Dictionary<string, List<DsaasTemplateVersion>> m_DsaasTemplateVersions = new Dictionary<string, List<DsaasTemplateVersion>>();
        List<DsaasTemplateVersion> m_DsaasSelectedTemplateVersions = new List<DsaasTemplateVersion>();


        [MenuItem("Window/Upload to DSaaS...")]
        static void ShowWindow()
        {
            var window = GetWindow<DsaasWindow>();
            window.titleContent = new GUIContent("DSaaS");
            window.minSize = new Vector2(700, 900);
            window.maxSize = window.minSize;
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
                CreateDsaasWindowUI();
            }
        }

        void CreateDsaasWindowUI()
        {
            var root = rootVisualElement;
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                $"{StaticData.uxmlDir}/DsaasWindow.uxml").CloneTree(root);

            m_UploadButton = root.Q<Button>("run-button");
            //m_UploadButton.clicked += UploadToDsaas;
            m_UploadButton.clicked += RefreshLists;

            m_ScenarioConfigField = root.Q<ObjectField>("scenario-config");
            m_ScenarioConfigField.objectType = typeof(TextAsset);

            m_BuildNameField = root.Q<TextField>("run-name");

            m_AuthTokenField = root.Q<TextField>("auth-token");

            m_DsaasTemplatesListView = root.Q<ListView>("dsaas-templates");
            m_DsaasTemplatesListView.onSelectionChanged += SelectedTemplateChanged;

            m_DsaasSelectedTemplateVersionsListView = root.Q<ListView>("dsaas-versions");
            m_DsaasSelectedTemplateVersionsListView.onSelectionChanged += SelectedTemplateVersionChanged;

            m_DsaasSelectedTemplateIdLabel = root.Q<Label>("selected-template-id");

            m_DsaasSelectedVersionIdLabel = root.Q<Label>("selected-version-id");

            m_NoVersionsLabel = root.Q<Label>("no-versions");

            m_RefreshingLabel = root.Q<Label>("refreshing");

            m_DsaasRefreshTemplatesButton = root.Q<Button>("refresh-templates-button");
            m_DsaasRefreshTemplatesButton.clicked += RefreshTemplatesBtnClicked;

            m_VersionsContainer = root.Q<VisualElement>("versions-container");

            m_UploadSection = root.Q<VisualElement>("upload-section");
            m_UploadSection.SetEnabled(false);

            m_DsaasCreateNewTemplate = root.Q<Button>("create-template-button");
            m_DsaasCreateNewTemplate.clicked += CreateTemplateBtnPressed;

            m_DsaasCreateNewTemplateVersion = root.Q<Button>("create-template-version-button");
            m_DsaasCreateNewTemplateVersion.clicked += CreateTemplateVersionBtnPressed;

            SetFieldsFromPlayerPreferences();

            SetupTemplatesList();
            SetupTemplateVersionsList();

            SetDsaasEnvVars();
        }

        void SetFieldsFromPlayerPreferences()
        {
            m_AuthTokenField.value = PlayerPrefs.GetString("latestDsaasAuthToken");
        }

        void SetupTemplatesList()
        {
            m_DsaasTemplatesListView.itemsSource = m_DsaasTemplates;

            VisualElement MakeItem() => new Label();
            void BindItem(VisualElement e, int i) => ((Label)e).text = m_DsaasTemplates[i].ToString();

            m_DsaasTemplatesListView.itemHeight = 50;
            m_DsaasTemplatesListView.selectionType = SelectionType.Single;
            m_DsaasTemplatesListView.makeItem = MakeItem;
            m_DsaasTemplatesListView.bindItem = BindItem;
        }

        void SetupTemplateVersionsList()
        {
            m_DsaasSelectedTemplateVersionsListView.itemsSource = m_DsaasSelectedTemplateVersions;

            VisualElement MakeItem() => new Label();
            void BindItem(VisualElement e, int i) => ((Label)e).text = m_DsaasSelectedTemplateVersions[i].ToString();

            m_DsaasSelectedTemplateVersionsListView.itemHeight = 50;
            m_DsaasSelectedTemplateVersionsListView.selectionType = SelectionType.Single;
            m_DsaasSelectedTemplateVersionsListView.makeItem = MakeItem;
            m_DsaasSelectedTemplateVersionsListView.bindItem = BindItem;
        }

        void SelectedTemplateChanged(List<object> objs)
        {
            DisableUploadSectionUI();
            //m_DsaasSelectedTemplateVersionsListView.selectedIndex = 0;

            var selectedTemplate = m_DsaasTemplates[m_DsaasTemplatesListView.selectedIndex];

            m_SelectedTemplateId = selectedTemplate.id;

            m_DsaasSelectedTemplateIdLabel.text = "Selected template id: " + selectedTemplate.id;

            var versions = m_DsaasTemplateVersions[selectedTemplate.id];
            if (versions == null)
            {
                m_VersionsContainer.style.display = DisplayStyle.None;

                //TODO: throw exception
                return;
            }

            m_VersionsContainer.style.display = DisplayStyle.Flex;
            m_NoVersionsLabel.style.display = versions.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            m_DsaasSelectedTemplateVersions.Clear();
            m_DsaasSelectedTemplateVersions.AddRange(versions);

            RefreshLists();
        }

        void SelectedTemplateVersionChanged(List<object> objs)
        {
            if (m_DsaasSelectedTemplateVersions.Count == 0 || m_DsaasSelectedTemplateVersions.Count <= m_DsaasSelectedTemplateVersionsListView.selectedIndex)
            {
                DisableUploadSectionUI();
                return;
            }

            var selectedTemplateVersion = m_DsaasSelectedTemplateVersions[m_DsaasSelectedTemplateVersionsListView.selectedIndex];
            m_SelectedTemplateVersionId = selectedTemplateVersion.id;

            m_DsaasSelectedVersionIdLabel.text = "Selected template version id: " + selectedTemplateVersion.id;

            m_UploadSection.SetEnabled(true);
        }

        void DisableUploadSectionUI()
        {
            m_SelectedTemplateVersionId = string.Empty;
            m_UploadSection.SetEnabled(false);
            m_DsaasSelectedVersionIdLabel.text = "Selected template version id: ";
        }
        async void UploadToDsaas()
        {
            m_BuildParameters = new BuildParameters
            {
                scenarioConfig = (TextAsset)m_ScenarioConfigField.value,
                currentOpenScenePath = SceneManager.GetSceneAt(0).path,
                currentScenario = FindObjectOfType<ScenarioBase>(),
                buildName = m_BuildNameField.value
            };

            //TODO: analytics for upload attempt start

            try
            {
                ValidateSettings();
                //CreateLinuxBuildAndZip();

                SetDsaasEnvVars();
                //await UploadAsDsaasTemplate();
                //await DsaasRefreshTemplates();
                //await DsaasCreateTemplate("test template 1", "helo helo heloooo", true);
                //await DsaasRefreshTemplateVersions(m_TemplateId);
                await DsaasCreateNewTemplateVersion(m_SelectedTemplateId, true, new List<KeyValuePair<string, string>>
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

        void SetDsaasEnvVars()
        {
            m_AuthToken = m_AuthTokenField.value;
            if (!string.IsNullOrEmpty(m_AuthToken))
            {
                PlayerPrefs.SetString("latestDsaasAuthToken", m_AuthToken);
            }

            m_SelectedTemplateId = "24c233a5-b9f9-4fd1-ba6d-eb8bde5abd46";
            m_SelectedTemplateVersionId = "f34767a1-cc79-41c6-ad91-88d73bedb6ec";
        }

        struct DsaasGenerateUploadUrlRequest
        {
            public string filename;
        }

        async void RefreshTemplatesBtnClicked()
        {
            m_RefreshingLabel.style.visibility = Visibility.Visible;

            m_DsaasTemplatesListView.SetEnabled(false);
            m_DsaasSelectedTemplateVersionsListView.SetEnabled(false);

            SetDsaasEnvVars();
            await DsaasRefreshTemplates();
            foreach (var template in m_DsaasTemplates)
            {
                await DsaasRefreshTemplateVersions(template.id);
            }
            RefreshLists();

            m_DsaasTemplatesListView.SetEnabled(true);
            m_DsaasSelectedTemplateVersionsListView.SetEnabled(true);

            m_RefreshingLabel.style.visibility = Visibility.Hidden;
        }

        async void CreateTemplateBtnPressed()
        {
            await DsaasCreateTemplate("test", "test description", true);
        }

        async void CreateTemplateVersionBtnPressed()
        {
            await DsaasCreateNewTemplateVersion(m_SelectedTemplateId, true, new List<KeyValuePair<string, string>>());
        }
        void RefreshLists()
        {
            m_DsaasTemplatesListView.Refresh();
            m_DsaasSelectedTemplateVersionsListView.Refresh();
        }

        void RefreshUI()
        {
            RefreshLists();
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
                    m_DsaasTemplates.AddRange(JsonConvert.DeserializeObject<List<DsaasTemplate>>(((JObject)responseJson.GetValue("object"))?.GetValue("templates").ToString()));
                }
                else
                {
                    DsaasHandleErrors(httpResponse);
                }
            }
            catch (HttpRequestException e)
            {
                Debug.LogError(e.StackTrace);
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
                else
                {
                    DsaasHandleErrors(httpResponse);
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
                    //m_LatestCreatedTemplateId = httpResponse.Headers.Location.Segments.Last();
                    await DsaasRefreshTemplates();
                    RefreshLists();
                }
                else
                {
                    DsaasHandleErrors(httpResponse);
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
                    //m_LatestCreatedTemplateVersionId = httpResponse.Headers.Location.Segments.Last();
                    await DsaasRefreshTemplateVersions(templateId);
                    SelectedTemplateChanged(null);
                    RefreshLists();
                }
                else
                {
                    DsaasHandleErrors(httpResponse);
                }
            }
            catch (HttpRequestException e)
            {
            }
        }

        async Task UploadBuildToDsaasTemplateVersion(string templateId, string templateVersionId)
        {
            //CreateLinuxBuildAndZip();

            var projectBuildDirectory = $"{m_BuildDirectory}/{m_BuildNameField.value}";
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
                else
                {
                    DsaasHandleErrors(httpResponse);
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
            }
        }

        static void DsaasHandleErrors(HttpResponseMessage responseMessage)
        {
            Debug.LogError("DSaaS API call failed with reason: " + responseMessage.ReasonPhrase);
        }
        void ValidateSettings()
        {
            if (string.IsNullOrEmpty(m_BuildParameters.currentOpenScenePath))
                throw new MissingFieldException("Invalid scene path");
            if (m_BuildParameters.currentScenario == null)
                throw new MissingFieldException(
                    "There is not a Unity Simulation compatible scenario present in the scene");
            if (!StaticData.IsSubclassOfRawGeneric(
                typeof(UnitySimulationScenario<>), m_BuildParameters.currentScenario.GetType()))
                throw new NotSupportedException(
                    "Scenario class must be derived from UnitySimulationScenario to run in Unity Simulation");
        }

        void CreateLinuxBuildAndZip()
        {
            var projectBuildDirectory = $"{m_BuildDirectory}/{m_BuildParameters.buildName}";
            if (!Directory.Exists(projectBuildDirectory))
                Directory.CreateDirectory(projectBuildDirectory);
            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { m_BuildParameters.currentOpenScenePath },
                locationPathName = Path.Combine(projectBuildDirectory, $"{m_BuildParameters.buildName}.x86_64"),
                target = BuildTarget.StandaloneLinux64
            };
            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                throw new Exception($"The Linux build did not succeed: status = {summary.result}");

            EditorUtility.DisplayProgressBar("Unity Simulation Run", "Zipping Linux build...", 0f);
            Zip.DirectoryContents(projectBuildDirectory, m_BuildParameters.buildName);
            m_BuildZipPath = projectBuildDirectory + ".zip";
        }



        struct BuildParameters
        {
            public TextAsset scenarioConfig;
            public string currentOpenScenePath;
            public ScenarioBase currentScenario;
            public string buildName;

            public string scenarioConfigAssetPath => AssetDatabase.GetAssetPath(scenarioConfig);
        }
    }
}
