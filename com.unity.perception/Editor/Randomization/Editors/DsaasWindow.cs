using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Simulation.Client;
using UnityEditor.Build.Reporting;
using UnityEditor.Perception.Dsaas.API;
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
        //use these fields temporarily to set cloud settings until DSaaS is available on production
        const string k_OrgID = "20066313632537";
        const string k_ProjectID = "f29ae914-a9d6-4e7a-970d-c6a6cc3139df";

        string m_BuildDirectory;
        string m_BuildZipPath;
        string m_SelectedTemplateId;
        string m_SelectedTemplateVersionId;
        string m_AuthToken;

        bool m_UploadInProgress;

        ListView m_DsaasTemplatesListView;
        ListView m_DsaasSelectedTemplateVersionsListView;

        TextField m_BuildNameField;
        TextField m_Iterations;
        TextField m_NewTemplateTitle;
        TextField m_NewTemplateDescription;
        TextField m_SelectedBuildPathTextField;
        TextField m_AuthTokenField;

        Label m_ProjectIdLabel;
        Label m_DsaasSelectedTemplateIdLabel;
        Label m_DsaasSelectedVersionIdLabel;
        Label m_NoVersionsLabel;
        Label m_RefreshingLabel;
        Label m_TemplateOperationStatus;
        Label m_TemplateVersionOperationStatus;
        Label m_RunCreationStatus;
        Label m_BuildUploadStatus;
        Label m_BuildNameWarningLabel;
        Label m_IterationsWarning;

        Button m_UploadButton;
        Button m_RunButton;
        Button m_RefreshTemplatesButton;
        Button m_CreateNewTemplateButton;
        Button m_CreateNewTemplateVersionButton;
        Button m_SelectBuildButton;
        Button m_CopyTemplateIdButton;
        Button m_CopyTemplateVersionIdButton;

        VisualElement m_VersionsContainer;
        VisualElement m_UploadSection;
        VisualElement m_BuildSelectControls;
        VisualElement m_TemplatesInnerSection;

        Toggle m_NewTemplateIsPublic;
        Toggle m_NewVersionPublish;
        Toggle m_CreateNewBuildToggle;

        ObjectField m_DsaasConfigField;

        List<DsaasTemplate> m_DsaasTemplates = new List<DsaasTemplate>();
        List<DsaasTemplateVersion> m_DsaasSelectedTemplateVersions = new List<DsaasTemplateVersion>();
        Dictionary<string, List<DsaasTemplateVersion>> m_DsaasTemplateVersions = new Dictionary<string, List<DsaasTemplateVersion>>();

        public List<string> newVersionKeyValueList;

        CancellationTokenSource m_CancellationTokenSource = new CancellationTokenSource();

        BuildParameters m_BuildParameters;

        [MenuItem("Window/Upload to DSaaS...")]
        static void ShowWindow()
        {
            var window = GetWindow<DsaasWindow>();
            window.titleContent = new GUIContent("DSaaS");
            window.minSize = new Vector2(740, 1100);
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

            m_UploadButton = root.Q<Button>("upload-button");
            m_UploadButton.clicked += UploadToDsaas;
            m_RunButton = root.Q<Button>("run-button");
            m_RunButton.clicked += CreateDsaasRun;

            m_TemplatesInnerSection = root.Q<VisualElement>("templates-inner-section");
            m_TemplatesInnerSection.SetEnabled(false);

            m_DsaasConfigField = root.Q<ObjectField>("dsaas-config");
            m_DsaasConfigField.objectType = typeof(TextAsset);

            m_BuildNameField = root.Q<TextField>("run-name");

            m_AuthTokenField = root.Q<TextField>("auth-token");

            m_DsaasTemplatesListView = root.Q<ListView>("dsaas-templates");
            m_DsaasTemplatesListView.onSelectionChanged += SelectedTemplateChanged;

            m_DsaasSelectedTemplateVersionsListView = root.Q<ListView>("dsaas-versions");
            m_DsaasSelectedTemplateVersionsListView.onSelectionChanged += SelectedTemplateVersionChanged;

            m_DsaasSelectedTemplateIdLabel = root.Q<Label>("selected-template-id");

            m_DsaasSelectedVersionIdLabel = root.Q<Label>("selected-version-id");

            m_NoVersionsLabel = root.Q<Label>("no-versions");
            m_NoVersionsLabel.style.display = DisplayStyle.None;

            m_RefreshingLabel = root.Q<Label>("refreshing");
            m_TemplateOperationStatus = root.Q<Label>("template-operations-status");
            m_TemplateVersionOperationStatus = root.Q<Label>("template-version-operations-status");
            m_RunCreationStatus = root.Q<Label>("run-creation-status");
            m_BuildUploadStatus = root.Q<Label>("build-upload-status");
            m_BuildNameWarningLabel = root.Q<Label>("build-name-warning");
            m_IterationsWarning = root.Q<Label>("iterations-warning");

            m_RefreshTemplatesButton = root.Q<Button>("refresh-templates-button");
            m_RefreshTemplatesButton.clicked += RefreshTemplatesBtnClicked;

            m_CopyTemplateIdButton = root.Q<Button>("copy-template-id");
            m_CopyTemplateIdButton.clicked += () =>
            {
                GUIUtility.systemCopyBuffer = m_SelectedTemplateId;
            };
            m_CopyTemplateVersionIdButton = root.Q<Button>("copy-template-version-id");
            m_CopyTemplateVersionIdButton.clicked += () =>
            {
                GUIUtility.systemCopyBuffer = m_SelectedTemplateVersionId;
            };

            m_VersionsContainer = root.Q<VisualElement>("versions-container");
            m_VersionsContainer.SetEnabled(false);

            m_UploadSection = root.Q<VisualElement>("upload-section");
            m_UploadSection.SetEnabled(false);

            m_CreateNewTemplateButton = root.Q<Button>("create-template-button");
            m_CreateNewTemplateButton.clicked += CreateTemplateBtnPressed;

            m_CreateNewTemplateVersionButton = root.Q<Button>("create-template-version-button");
            m_CreateNewTemplateVersionButton.clicked += CreateTemplateVersionBtnPressed;

            m_NewTemplateTitle = root.Q<TextField>("new-template-title");
            m_NewTemplateDescription = root.Q<TextField>("new-template-description");
            m_NewTemplateIsPublic = root.Q<Toggle>("template-public-toggle");

            m_NewVersionPublish = root.Q<Toggle>("version-publish-toggle");
            m_NewVersionPublish.visible = false;
            m_NewVersionPublish.value = true;

            m_CreateNewBuildToggle = root.Q<Toggle>("create-new-build");
            m_CreateNewBuildToggle.value = true;

            m_CreateNewBuildToggle.RegisterCallback<MouseUpEvent>(evt =>
            {
                ApplyNewBuildCreationState();
            });

            m_BuildSelectControls = root.Q<VisualElement>("build-select-controls");
            m_BuildSelectControls.SetEnabled(!m_CreateNewBuildToggle.value);

            m_SelectedBuildPathTextField = root.Q<TextField>("selected-build-path");
            m_SelectedBuildPathTextField.isReadOnly = true;

            m_SelectBuildButton = root.Q<Button>("select-build-file-button");
            m_SelectBuildButton.clicked += () =>
            {
                var path = EditorUtility.OpenFilePanel("Select build ZIP file", "", "zip");
                if (path.Length != 0)
                {
                    m_SelectedBuildPathTextField.value = path;
                }
            };

            m_Iterations = root.Q<TextField>("iterations");

            root.Q<PropertyField>("keyvalue-list").Bind(new SerializedObject(this));

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

        void ApplyNewBuildCreationState()
        {
            m_BuildNameWarningLabel.visible = false;
            m_BuildSelectControls.SetEnabled(!m_CreateNewBuildToggle.value);
            m_BuildNameField.SetEnabled(m_CreateNewBuildToggle.value);
        }

        async void SelectedTemplateChanged(List<object> objs)
        {
            DisableUploadSectionUI();
            m_TemplateOperationStatus.visible = false;
            m_RunCreationStatus.visible = false;
            m_TemplateVersionOperationStatus.visible = false;
            m_BuildUploadStatus.visible = false;

            var selectedTemplate = m_DsaasTemplates[m_DsaasTemplatesListView.selectedIndex];

            m_SelectedTemplateId = selectedTemplate.id;

            m_DsaasSelectedTemplateIdLabel.text = "Selected template id: " + selectedTemplate.id;

            if (!m_DsaasTemplateVersions.TryGetValue(selectedTemplate.id, out var versions))
            {
                //the template versions should have already been downloaded, but try once more just in case there was an issue before
                var success = await RefreshTemplateVersions(selectedTemplate.id);

                if (success)
                {
                    versions = m_DsaasTemplateVersions[selectedTemplate.id];
                }
                else
                {
                    m_VersionsContainer.SetEnabled(false);

                    //TODO: throw exception
                    return;
                }
            }

            m_VersionsContainer.SetEnabled(true);
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
            if (m_UploadInProgress)
            {
                m_UploadButton.text = "Upload";
                m_BuildUploadStatus.style.color = Color.red;
                m_BuildUploadStatus.text = "Upload cancelled.";
                m_CancellationTokenSource.Cancel();
                m_UploadInProgress = false;
            }
            else
            {
                m_UploadInProgress = true;

                m_BuildUploadStatus.style.color = Color.green;

                m_BuildUploadStatus.visible = true;
                m_BuildNameWarningLabel.visible = false;

                //TODO: analytics for upload attempt start

                if (m_CreateNewBuildToggle.value)
                {
                    if (string.IsNullOrEmpty(m_BuildNameField.value))
                    {
                        m_BuildUploadStatus.visible = false;
                        m_BuildNameWarningLabel.visible = true;
                        m_UploadInProgress = false;
                        return;
                    }

                    m_BuildUploadStatus.text = "Creating and uploading new build...";

                    m_BuildParameters = new BuildParameters
                    {
                        dsaasConfig = (TextAsset)m_DsaasConfigField.value,
                        currentOpenScenePath = SceneManager.GetSceneAt(0).path,
                        currentScenario = FindObjectOfType<ScenarioBase>(),
                        buildName = m_BuildNameField.value
                    };
                    ValidateSettings();

                    CreateLinuxBuildAndZip();
                    var projectBuildDirectory = $"{m_BuildDirectory}/{m_BuildNameField.value}";
                    m_BuildZipPath = projectBuildDirectory + ".zip";
                }
                else
                {
                    m_BuildZipPath = m_SelectedBuildPathTextField.value;
                    m_BuildUploadStatus.text = "Uploading selected build...";
                }

                m_UploadButton.text = "Cancel Upload";

                var cancellationToken = m_CancellationTokenSource.Token;

                var success = await DsaasAPI.DsaasUploadBuildToTemplateVersion(m_SelectedTemplateId, m_SelectedTemplateVersionId, m_BuildZipPath, cancellationToken);

                if (success)
                {
                    m_BuildUploadStatus.text = "Build uploaded successfully";
                }
                else
                {
                    m_BuildUploadStatus.text = "Build upload failed. Check the Console for more information";
                    m_BuildUploadStatus.style.color = Color.red;
                }

                m_UploadInProgress = false;
            }
        }

        async void CreateDsaasRun()
        {
            m_IterationsWarning.visible = false;
            if (!int.TryParse(m_Iterations.value, out int iterations))
            {
                m_IterationsWarning.visible = true;
                return;
            }

            m_RunCreationStatus.visible = true;
            m_RunCreationStatus.style.color = Color.green;
            var success = await DsaasAPI.DsaasCreateRunForTemplateVersion(m_SelectedTemplateId, m_SelectedTemplateVersionId, iterations);

            if (success)
            {
                m_RunCreationStatus.text = "Run created successfully";
            }
            else
            {
                m_RunCreationStatus.text = "Run creation failed. Check the Console for more information.";
                m_RunCreationStatus.style.color = Color.red;
            }
        }

        void SetDsaasEnvVars()
        {
            m_AuthToken = m_AuthTokenField.value;
            if (!string.IsNullOrEmpty(m_AuthToken))
            {
                PlayerPrefs.SetString("latestDsaasAuthToken", m_AuthToken);
            }


            DsaasAPI.SetServerUrl("https://perception-api.stg.simulation.unity3d.com");
            DsaasAPI.SetOrgID(k_OrgID);
            DsaasAPI.SetProjectID(k_ProjectID);
            DsaasAPI.SetAuthToken(m_AuthToken);

            m_SelectedTemplateId = "24c233a5-b9f9-4fd1-ba6d-eb8bde5abd46";
            m_SelectedTemplateVersionId = "f34767a1-cc79-41c6-ad91-88d73bedb6ec";
        }

        async void RefreshTemplatesBtnClicked()
        {
            m_RefreshingLabel.style.color = Color.green;
            m_RefreshingLabel.text = "Refreshing DSaaS templates and their versions...";
            m_RefreshingLabel.visible = true;

            m_DsaasTemplatesListView.SetEnabled(false);
            m_DsaasSelectedTemplateVersionsListView.SetEnabled(false);

            var success = await RefreshDsaasTemplates();

            if (success)
            {
                RefreshLists();
                m_TemplatesInnerSection.SetEnabled(true);
                m_DsaasTemplatesListView.SetEnabled(true);
                m_DsaasSelectedTemplateVersionsListView.SetEnabled(true);
                m_RefreshingLabel.visible = false;
            }
        }

        async Task<bool> RefreshDsaasTemplates()
        {
            SetDsaasEnvVars();
            var templates = await DsaasAPI.DsaasGetTemplates();

            if (templates != null)
            {
                m_DsaasTemplates.Clear();
                m_DsaasTemplates.AddRange(templates);
            }
            else
            {
                m_RefreshingLabel.style.color = Color.red;
                m_RefreshingLabel.text = "Failed to refresh templates. Check the Console for more information.";
                return false;
            }

            foreach (var template in m_DsaasTemplates)
            {
                var success = await RefreshTemplateVersions(template.id);
                if (!success)
                {
                    m_RefreshingLabel.style.color = Color.red;
                    m_RefreshingLabel.text = $"Failed to refresh versions for one or more templates. Check the Console for more information.";
                    return false;
                }
            }

            return true;
        }

        async Task<bool> RefreshTemplateVersions(string templateId)
        {
            var versions = await DsaasAPI.DsaasGetTemplateVersions(templateId);

            if (versions != null)
            {
                m_DsaasTemplateVersions[templateId] = new List<DsaasTemplateVersion>();
                m_DsaasTemplateVersions[templateId].AddRange(versions);
                return true;
            }

            return false;
        }

        async void CreateTemplateBtnPressed()
        {
            m_TemplateOperationStatus.style.color = Color.green;
            m_TemplateOperationStatus.text = "Creating new template...";
            m_TemplateOperationStatus.visible = true;

            var newTemplateId = await DsaasAPI.DsaasCreateTemplate(m_NewTemplateTitle.text, m_NewTemplateDescription.text, m_NewTemplateIsPublic.value);

            if (!string.IsNullOrEmpty(newTemplateId))
            {
                m_TemplateOperationStatus.text = "New template created successfully";
                await RefreshDsaasTemplates();
                await RefreshTemplateVersions(newTemplateId);
                RefreshLists();
            }
            else
            {
                m_TemplateOperationStatus.style.color = Color.red;
                m_TemplateOperationStatus.text = "Template creation failed. Check the Console for more information.";
            }
        }

        async void CreateTemplateVersionBtnPressed()
        {
            m_TemplateVersionOperationStatus.style.color = Color.green;
            m_TemplateVersionOperationStatus.text = "Creating new template version...";
            m_TemplateVersionOperationStatus.visible = true;

            var tags = ParseKeyValueList();

            if (tags == null)
            {
                m_TemplateVersionOperationStatus.style.color = Color.red;
                m_TemplateVersionOperationStatus.text = $"Failed to create new template version. Provided tag list has invalid format.";
                return;
            }

            var currentScenario = FindObjectOfType<ScenarioBase>();

            if (!currentScenario)
            {
                m_TemplateVersionOperationStatus.style.color = Color.red;
                m_TemplateVersionOperationStatus.text = $"Failed to create new template version. No Scenario was found in the active Scene.";
                return;
            }

            var config = JObject.Parse(currentScenario.SerializeToJson()).GetValue("randomizers");

            //TODO: Should a user-specified config be accepted here? Or is the one supplied at the build upload stage is sufficient?

            var newTemplateVersionId = await DsaasAPI.DsaasCreateNewTemplateVersion(m_SelectedTemplateId, m_NewVersionPublish.value, config, tags);

            if (!string.IsNullOrEmpty(newTemplateVersionId))
            {
                m_TemplateVersionOperationStatus.text = "New template version created successfully";
                await RefreshTemplateVersions(m_SelectedTemplateId);
                SelectedTemplateChanged(null);
                m_TemplateVersionOperationStatus.visible = true;
                RefreshLists();
            }
            else
            {
                m_TemplateVersionOperationStatus.style.color = Color.red;
                m_TemplateVersionOperationStatus.text = $"Failed to create new template version. Check the Console for more information.";
            }
        }

        List<KeyValuePair<string, string>> ParseKeyValueList()
        {
            var result = new List<KeyValuePair<string, string>>();

            foreach (var kv in newVersionKeyValueList)
            {
                var split = kv.Split(',');
                if (split.Length != 2 || split[0].Length < 3 || split[1].Length < 3 ||
                    !split[0].StartsWith("\"") || !split[0].EndsWith("\"") ||
                    !split[1].StartsWith("\"") || !split[1].EndsWith("\""))
                {
                    m_TemplateVersionOperationStatus.text = "Provided key-value pair has invalid format: " + kv;
                    m_TemplateVersionOperationStatus.style.color = Color.red;
                    return null;
                }

                var key = split[0].Substring(1, split[0].Length - 2);
                var value = split[1].Substring(1, split[1].Length - 2);

                result.Add(new KeyValuePair<string, string>(key, value));
            }

            return result;
        }

        void RefreshLists()
        {
            m_DsaasTemplatesListView.Refresh();
            m_DsaasSelectedTemplateVersionsListView.Refresh();
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
            if (m_BuildParameters.dsaasConfig != null &&
                Path.GetExtension(m_BuildParameters.dsaasConfigAssetPath) != ".json")
                throw new NotSupportedException(
                    "DSaaS configuration must be a JSON text asset");
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

            Zip.DirectoryContents(projectBuildDirectory, m_BuildParameters.buildName);
            m_BuildZipPath = projectBuildDirectory + ".zip";
        }

        struct BuildParameters
        {
            public TextAsset dsaasConfig;
            public string currentOpenScenePath;
            public ScenarioBase currentScenario;
            public string buildName;
            public string dsaasConfigAssetPath => AssetDatabase.GetAssetPath(dsaasConfig);
        }
    }
}
