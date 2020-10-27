using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

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
        internal const string pacakgeVersion = "0.4.5-preview.3";

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
