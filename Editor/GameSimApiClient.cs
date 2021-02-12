using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace Unity.Simulation.Games.Editor
{
#if UNITY_2020_1_OR_NEWER
    [FilePath("GameSimApi", FilePathAttribute.Location.PreferencesFolder)]
#endif
    public class GameSimApiClient : ScriptableSingleton<GameSimApiClient>
    {
        [SerializeField]
        internal string gamesimUrl = "https://api.prd.gamesimulation.unity3d.com";

        /// <summary>
        /// Requests a build URL and uploads a zipped build. Only supports linux x86_64 target
        /// </summary>
        /// <param name="name">build name for display in Unity Game Simulation's web ui</param>
        /// <param name="location">zip file containing the build</param>
        /// <param name="simulationMetrics">list of metrics associated with this build</param>
        /// <returns>build id from Unity Game Simulation platform services</returns>
        public string UploadBuild(string name, string location, List<string> simulationMetrics)
        {
            Save(true);
            return Transaction.Upload($"{gamesimUrl}/v1/builds?projectId={Application.cloudProjectId}", name, location, simulationMetrics);
        }

        /// <summary>
        /// Lists all jobs under the current organization
        /// </summary>
        /// <returns>Web response to /v1/jobs/list api endpoint</returns>
        public UnityWebRequestAsyncOperation ListJobs()
        {
            var request = UnityWebRequest.Get($"{gamesimUrl}/v1/jobs/list?projectId={Application.cloudProjectId}");
            var headers = Utils.GetAuthHeader(CloudProjectSettings.accessToken);
            foreach (var kvp in headers)
            {
                request.SetRequestHeader(kvp.Key, kvp.Value);
            }

            return request.SendWebRequest();
        }

        /// <summary>
        /// Describes a job
        /// </summary>
        /// <param name="jobId">ID of the job to query</param>
        /// <returns>Web response for v1/jobs/{jobId}/describe api endpoint</returns>
        public UnityWebRequestAsyncOperation DescribeJob(string jobId)
        {
            var request = UnityWebRequest.Get($"{gamesimUrl}/v1/jobs/{jobId}/describe?projectId={Application.cloudProjectId}");
            var headers = Utils.GetAuthHeader(CloudProjectSettings.accessToken);
            foreach (var kvp in headers)
            {
                request.SetRequestHeader(kvp.Key, kvp.Value);
            }

            return request.SendWebRequest();
        }

        [Serializable]
        public class JobsList
        {
            public string projectId;
            public List<Job> jobs;
        }

        [Serializable]
        public class Job
        {
            public string id;
            public string name;
            public string buildId;
            public string stage;
            public string status;
            public long executionTimeSeconds;
            public bool hasStepData;
            public DateTime createdAt;
            public DateTime updatedAt;
        }

        /// <summary>
        /// Get Builds
        /// </summary>
        /// <returns>Web response for /v1/builds/list api endpoint</returns>
        public UnityWebRequestAsyncOperation GetBuilds()
        {
            var url = $"{gamesimUrl}/v1/builds/list?projectId={Application.cloudProjectId}";
            var request = UnityWebRequest.Get(url);
            var headers = Utils.GetAuthHeader(CloudProjectSettings.accessToken);
            foreach (var kvp in headers)
            {
                request.SetRequestHeader(kvp.Key, kvp.Value);
            }

            return request.SendWebRequest();
        }

        List<DESetting> GetTemplateSettings(Dictionary<string, Tuple<string, string>> parameters)
        {
            var CreateSimulationParameters = new List<DESetting>();
            foreach (var key in parameters.Keys)
            {
                var type =  parameters[key].Item1;
                var values = parameters[key].Item2;
                List<string> valList = values.Split(',').ToList().Select(s => s.Trim()).ToList();

                var NewSetting = new DESetting();
                NewSetting.key = key;
                NewSetting.type = type;
                NewSetting.values = valList;
                CreateSimulationParameters.Add(NewSetting);
            };
            return CreateSimulationParameters;
        }

        [Serializable]
        public class DESetting
        {
			public string key;
			public string type;
			public List<string> values;
        }

        [Serializable]
        public class DecisionEngineMetadata
        {
            public string engineType = "gridsearch";
            public int runsPerParamCombo;
            public int parallelism = 10;
            public List<DESetting> settings;
        }

        [Serializable]
        public class CreateJobPayload
        {
            public string jobName;
            public string buildId;
            public string maxRuntimeSeconds;
            public string[] testInfo = new string[] { };
            public DecisionEngineMetadata decisionEngineMetadata;
        }

        // <summary>
        // Create Job
        // </summary>
        // <param name = "projectId" > ID of the project to create a simulation job for</param>
        // <returns>Web response for v1/jobs/{jobId}/describe api endpoint</returns>
        public string CreateJob(string jobName, string buildId, Dictionary<string, Tuple<string, string>> parameters, string maxRuns, string maxRuntime)
        {
            var maxRuntimeSeconds = Int32.Parse(maxRuntime) * 60;
            List<DESetting> deSettings = GetTemplateSettings(parameters);
            DecisionEngineMetadata deMetadata = new DecisionEngineMetadata();
            deMetadata.runsPerParamCombo = Int32.Parse(maxRuns);
            deMetadata.settings = deSettings;

            CreateJobPayload requestBody = new CreateJobPayload();
            requestBody.jobName = jobName;
            requestBody.buildId = buildId;
            requestBody.maxRuntimeSeconds = maxRuntimeSeconds.ToString();
            requestBody.decisionEngineMetadata = deMetadata;

			var requestBodyJSON = JsonConvert.SerializeObject(requestBody);
			var url = $"{gamesimUrl}/v1/jobs?projectId={Application.cloudProjectId}";
            byte[] jsonBytes = new System.Text.UTF8Encoding().GetBytes(requestBodyJSON);
            return CreateJobTransaction.Post(url, requestBodyJSON);
        }
    }

    [Serializable]
    internal class CreateJobResponse
    {
        public string id;
    }

    internal static class CreateJobTransaction
    {
        public static string Post(string url, string payload)
        {
            using (var webrx = UnityWebRequest.Post(url, payload))
            {
                var headers = Utils.GetAuthHeader(CloudProjectSettings.accessToken);
                foreach (var k in headers)
                    webrx.SetRequestHeader(k.Key, k.Value);

                webrx.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                webrx.timeout = 10;
                webrx.SendWebRequest();
                while (!webrx.isDone)
                {
                }

                if (webrx.isNetworkError || webrx.isHttpError)
                {
                    throw new Exception("Failed to create simulation with error: " + webrx.error + "\n" + webrx.downloadHandler.text);
                }

				CreateJobResponse data =  JsonUtility.FromJson<CreateJobResponse>(webrx.downloadHandler.text);

                Debug.Assert(!string.IsNullOrEmpty(data.id));
                return data.id;
            }

        }
    }

internal static class Transaction
{
    public static string Upload(string url, string name, string inFile, List<string> simulationMetrics, bool useTransferUrls = true)
    {
        return Upload(url, name, File.ReadAllBytes(inFile), simulationMetrics, useTransferUrls);
    }

    public static string Upload(string url, string name, byte[] data, List<string> simulationMetrics, bool useTransferUrls = true)
    {
        string entityId = null;

        Action<UnityWebRequest> action = (UnityWebRequest webrx) =>
        {
            var headers = Utils.GetAuthHeader(CloudProjectSettings.accessToken);
            foreach (var k in headers)
                webrx.SetRequestHeader(k.Key, k.Value);

            webrx.uploadHandler = new UploadHandlerRaw(data);
            webrx.SendWebRequest();
            while (!webrx.isDone)
            {
            }

            if (webrx.isNetworkError || webrx.isHttpError)
            {
                Debug.LogError("Failed to upload with error \n" + webrx.error + "\n" + webrx.downloadHandler.text);
                return;

            }

            if (!string.IsNullOrEmpty(webrx.downloadHandler.text))
            {
                Debug.Assert(false, "Need to pull id from response");
                // set entity return id here
            }
        };

        if (useTransferUrls)
        {
            var tuple = GetUploadURL(url, name, simulationMetrics);
            entityId = tuple.Item2;
            using (var webrx = UnityWebRequest.Put(tuple.Item1, data))
            {
                action(webrx);
            }
        }
        else
        {
            using (var webrx = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                action(webrx);
            }
        }

        Debug.Assert(!string.IsNullOrEmpty(entityId));

        return entityId;
    }

    public static Tuple<string, string> GetUploadURL(string url, string path, List<string> simulationMetrics)
    {
        var payload = JsonUtility.ToJson(new UploadInfo(Path.GetFileName(path), "Placeholder description", simulationMetrics));

        using (var webrx = UnityWebRequest.Post(url, payload))
        {
            var headers = Utils.GetAuthHeader(CloudProjectSettings.accessToken);
            foreach (var k in headers)
                webrx.SetRequestHeader(k.Key, k.Value);

            webrx.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            webrx.timeout = 30;
            webrx.SendWebRequest();
            while (!webrx.isDone)
            {
            }

            if (webrx.isNetworkError || webrx.isHttpError)
            {
                throw new Exception("Failed to generate upload URL with error: " + webrx.error + "\n" + webrx.downloadHandler.text);
            }

            var data = JsonUtility.FromJson<UploadUrlData>(webrx.downloadHandler.text);
            return new Tuple<string, string>(data.upload_uri, data.id);
        }
    }
}
[Serializable]
    internal struct UploadInfo
    {
        public string name;
        public string description;
        public List<string> simulationMetrics;
        public UploadInfo(string name, string description, List<string> simulationMetrics)
        {
            this.name = name;
            this.description = description;
            this.simulationMetrics = simulationMetrics;
        }
    }

#pragma warning disable CS0649
[Serializable]
internal struct UploadUrlData
{
    public string id;
    public string upload_uri;
}
#pragma warning restore CS0649

internal static class Utils
{
    internal const string pacakgeVersion = "0.4.7-preview.1";

    internal static Dictionary<string, string> GetAuthHeader(string tokenString)
    {
        var dict = new Dictionary<string, string>();
        AddUserAgent(dict);
        AddContentTypeApplication(dict);
        AddAuth(dict, tokenString);
        return dict;
    }

    static void AddContentTypeApplication(Dictionary<string, string> dict)
    {
        dict["Content-Type"] = "application/json";
    }

    //TODO: Solve this somehow
    static void AddAuth(Dictionary<string, string> dict, string tokenString)
    {
        dict["Authorization"] = "Bearer " + tokenString;
    }

    static void AddUserAgent(Dictionary<string, string> dict)
    {
        dict["User-Agent"] = "gamesim/" + pacakgeVersion;
    }
}
    }
