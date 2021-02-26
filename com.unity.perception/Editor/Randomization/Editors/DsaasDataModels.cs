using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityEditor.Perception.Dsaas.DataModels
{
    [Serializable]
    struct DsaasTemplate
    {
        public string title;
        public string description;
        public string id;
        [JsonProperty("public")]
        public bool isPublic;
        public string imgSrc;
        public string moreInfo;

        public override string ToString()
        {
            return $"Title: {title}\nDescription: {description}\nid: {id}";
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }
    }

    [Serializable]
    struct DsaasTemplateVersion
    {
        public string id;
        public string templateId;
        public string version;
        public bool published;
        public string authorId;
        [CanBeNull]
        public List<KeyValuePair<string, string>> tags;

        public override string ToString()
        {
            return $"Version: {version}\nAuthor ID: {authorId}\nid: {id}";
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }
    }

    [Serializable]
    struct DsaasCreateTemplateRequest
    {
        public string title;
        public string description;
        [JsonProperty("public")]
        public bool isPublic;
    }

    [Serializable]
    struct DsaasCreateTemplateVersionRequest
    {
        public bool published;
        public string version;
        public string authorId;
        [CanBeNull]
        public List<KeyValuePair<string, string>> tags;
        public JToken randomizers;
    }

    [Serializable]
    struct DsaasGenerateUploadUrlRequest
    {
        public string filename;
    }

    [Serializable]
    struct DsaasRunRequestTemplateVersion
    {
        public string id;
        public string templateId;
    }

    [Serializable]
    struct DsaasRunConfig
    {
        public int iterations;
    }

    [Serializable]
    struct DsaasCreateRunRequest
    {
        public DsaasRunRequestTemplateVersion version;
        public JToken randomizations;
        public List<string> assets;
        public DsaasRunConfig runConfig;
    }
}
