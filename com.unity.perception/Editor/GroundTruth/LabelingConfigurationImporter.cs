using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;

namespace UnityEditor.Perception.GroundTruth
{
    [ScriptedImporter(1, "labelconfig")]
    public class LabelingConfigurationImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var text = File.ReadAllText(ctx.assetPath);
            var jObject = JObject.Parse(text);
            var config = ScriptableObject.CreateInstance<LabelingConfiguration>();
            if (jObject["auto-assign-ids"].HasValues)
                config.AutoAssignIds =  jObject["auto-assign-ids"].Value<bool>();

            if (jObject["starting-index"].HasValues)
            {
                var startingLabelId = (StartingLabelId)jObject["starting-index"].Value<int>();
                config.StartingLabelId = startingLabelId;
            }

            if (jObject["configs"].HasValues)
            {

            }
            ctx.SetMainObject(config);
        }
    }
}
