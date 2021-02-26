using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Simulation.Client;
using UnityEditor.Perception.Dsaas.DataModels;
using UnityEngine;

namespace UnityEditor.Perception.Dsaas.API
{
    static class DsaasAPI
    {
        static readonly HttpClient k_HttpClient = new HttpClient();
        static string s_ServerUrl = "https://perception-api.stg.simulation.unity3d.com";
        static string s_OrgID = CloudProjectSettings.organizationId;
        static string s_ProjectID = CloudProjectSettings.projectId;
        static string s_AuthToken = Project.accessToken;

        internal static void SetServerUrl(string url)
        {
            s_ServerUrl = url;
        }

        internal static void SetOrgID(string orgID)
        {
            s_OrgID = orgID;
        }

        internal static void SetProjectID(string projectID)
        {
            s_ProjectID = projectID;
        }

        internal static void SetAuthToken(string token)
        {
            s_AuthToken = token;
        }

        internal static async Task<List<DsaasTemplate>> DsaasGetTemplates()
        {
            k_HttpClient.DefaultRequestHeaders.Clear();
            k_HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s_AuthToken);

            var requestUri = new Uri($"{s_ServerUrl}/v1/organizations/{s_OrgID}/templates/");

            try
            {
                HttpResponseMessage httpResponse = await k_HttpClient.GetAsync(requestUri);
                if (httpResponse.IsSuccessStatusCode)
                {
                    var responseString = httpResponse.Content.ReadAsStringAsync().Result;
                    var responseJson = JObject.Parse(responseString);
                    return JsonConvert.DeserializeObject<List<DsaasTemplate>>(((JObject)responseJson.GetValue("object"))?.GetValue("templates").ToString());
                }

                DsaasHandleErrors(httpResponse);
                return null;
            }
            catch (HttpRequestException e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        internal static async Task<List<DsaasTemplateVersion>> DsaasGetTemplateVersions(string templateId)
        {
            k_HttpClient.DefaultRequestHeaders.Clear();
            k_HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s_AuthToken);

            var requestUri = new Uri($"{s_ServerUrl}/v1/organizations/{s_OrgID}/templates/{templateId}/versions");

            try
            {
                HttpResponseMessage httpResponse = await k_HttpClient.GetAsync(requestUri);
                if (httpResponse.IsSuccessStatusCode)
                {
                    var responseString = httpResponse.Content.ReadAsStringAsync().Result;
                    var responseJson = JObject.Parse(responseString);
                    var versions = ((JObject)responseJson.GetValue("object"))?.GetValue("versions");

                    if (versions != null && versions.HasValues)
                    {
                        return JsonConvert.DeserializeObject<List<DsaasTemplateVersion>>(versions.ToString());
                    }

                    return new List<DsaasTemplateVersion>();
                }
                else
                {
                    Debug.LogError($"Could not retrieve versions for template {templateId}");
                    DsaasHandleErrors(httpResponse);
                    return null;
                }
            }
            catch (HttpRequestException e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        internal static async Task<string> DsaasCreateTemplate(string templateTitle, string templateDescription, bool isTemplatePublic)
        {
            k_HttpClient.DefaultRequestHeaders.Clear();
            k_HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s_AuthToken);

            var requestUri = new Uri($"{s_ServerUrl}/v1/organizations/{s_OrgID}/templates/");

            var request = new DsaasCreateTemplateRequest
            {
                title = templateTitle,
                description = templateDescription,
                isPublic = isTemplatePublic
            };

            HttpContent requestContents = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage httpResponse = await k_HttpClient.PostAsync(requestUri, requestContents);
                if (httpResponse.IsSuccessStatusCode)
                {
                    var templateId = httpResponse.Headers.Location.Segments.Last();
                    return templateId;
                }

                Debug.LogError("Template creation failed. Check the Console for more information.");
                DsaasHandleErrors(httpResponse);
                return null;
            }
            catch (HttpRequestException e)
            {
                Debug.LogException(e);
            }

            return null;
        }

        internal static async Task<string> DsaasCreateNewTemplateVersion(string templateId, bool published, JToken dsaasConfig, List<KeyValuePair<string, string>> tags = null)
        {
            var versions = await DsaasGetTemplateVersions(templateId);

            if (versions == null)
            {
                return null;
            }

            k_HttpClient.DefaultRequestHeaders.Clear();
            k_HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s_AuthToken);

            var requestUri = new Uri($"{s_ServerUrl}/v1/organizations/{s_OrgID}/templates/{templateId}/versions");

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

            var request = new DsaasCreateTemplateVersionRequest
            {
                published = published,
                version = newVersionString,
                tags = tags,
                authorId = s_OrgID,
                randomizers = dsaasConfig
            };

            HttpContent requestContents = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage httpResponse = await k_HttpClient.PostAsync(requestUri, requestContents);
                if (httpResponse.IsSuccessStatusCode)
                {
                    var newTemplateVersionId = httpResponse.Headers.Location.Segments.Last();
                    return newTemplateVersionId;
                }

                DsaasHandleErrors(httpResponse);
                return null;
            }
            catch (HttpRequestException e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        internal static async Task<bool> DsaasUploadBuildToTemplateVersion(string templateId, string templateVersionId, string buildPath, CancellationToken cancellationToken)
        {
            k_HttpClient.DefaultRequestHeaders.Clear();
            k_HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s_AuthToken);

            var requestUri = new Uri($"{s_ServerUrl}/v1/organizations/{s_OrgID}/templates/{templateId}/versions/{templateVersionId}:uploadUrl");

            var uploadUrlRequest = new DsaasGenerateUploadUrlRequest()
            {
                filename = Path.GetFileName(buildPath)
            };

            string uploadUrl;

            HttpContent requestContents = new StringContent(JsonConvert.SerializeObject(uploadUrlRequest), Encoding.UTF8, "application/json");
            try
            {
                var httpResponse = await k_HttpClient.PostAsync(requestUri, requestContents, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    Debug.Log("Upload cancelled.");
                    return false;
                }

                if (httpResponse.IsSuccessStatusCode)
                {
                    var responseString = httpResponse.Content.ReadAsStringAsync().Result;
                    var responseJson = JObject.Parse(responseString);
                    uploadUrl = responseJson.GetValue("url").ToString();
                }
                else
                {
                    DsaasHandleErrors(httpResponse);
                    return false;
                }
            }
            catch (HttpRequestException e)
            {
                Debug.LogException(e);
                return false;
            }

            if (!string.IsNullOrEmpty(uploadUrl))
            {
                var stream = File.OpenRead(buildPath);
                requestContents = new StreamContent(stream);
                requestContents.Headers.Add("Content-Type", "application/zip");

                try
                {
                    var httpResponse = await k_HttpClient.PutAsync(uploadUrl, requestContents, cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        Debug.Log("Upload cancelled.");
                        return false;
                    }

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        Debug.Log("Build uploaded successfully.");
                        return true;
                    }

                    DsaasHandleErrors(httpResponse);
                    return false;
                }
                catch (HttpRequestException e)
                {
                    Debug.LogException(e);
                    return false;
                }
            }

            return false;
        }

        internal static async Task<bool> DsaasCreateRunForTemplateVersion(string templateId, string templateVersionId, int iterations)
        {
            k_HttpClient.DefaultRequestHeaders.Clear();
            k_HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s_AuthToken);

            var requestUri = new Uri($"{s_ServerUrl}/v1/organizations/{s_OrgID}/projects/{s_ProjectID}/runs");

            var request = new DsaasCreateRunRequest()
            {
                assets = new List<string>(),
                version = new DsaasRunRequestTemplateVersion()
                {
                    id = templateVersionId,
                    templateId = templateId
                },
                runConfig = new DsaasRunConfig()
                {
                    iterations = iterations
                },
                randomizations = new JObject()
            };

            HttpContent requestContents = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage httpResponse = await k_HttpClient.PostAsync(requestUri, requestContents);
                if (httpResponse.IsSuccessStatusCode)
                {
                    Debug.Log("Run created successfully. DSaaS Run ID: " + httpResponse.Headers.Location.Segments.Last());
                    return true;
                }

                DsaasHandleErrors(httpResponse);
                return false;

            }
            catch (HttpRequestException e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        static void DsaasHandleErrors(HttpResponseMessage responseMessage)
        {
            Debug.LogError("DSaaS API call failed with reason: " + responseMessage.ReasonPhrase);
        }
    }
}
