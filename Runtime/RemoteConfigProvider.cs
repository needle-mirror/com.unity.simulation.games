using System;
using System.IO;
using System.Text;
using UnityEngine;
using Unity.RemoteConfig;


namespace Unity.Simulation.Games
{
    internal class RemoteConfigProvider
    {
#if UNITY_GAME_SIMULATION || UNITY_EDITOR
        internal ConfigManagerImpl configManager;
        
        private RemoteConfigProvider()
        {
            configManager = new ConfigManagerImpl("gamesim", "RemoteConfigGS.json", "RemoteConfigHeadersGS.json");
        }

        public static RemoteConfigProvider Instance { get; } = new RemoteConfigProvider();

        public struct UserAttributes
        {
            public long gameSimInstanceId;
            public string gameSimDefinitionId;
            public string gameSimExecutionId;
            public string gameSimDecisionEngineId;
            public string gameSimDecisionEngineType;
        }

        [Serializable]
        public struct GameSimAppParams
        {
            public string gameSimDecisionEngineId;
            public string gameSimDecisionEngineType;
            public string environmentId;
        }

        private Action<GameSimConfigResponse> ConfigHandler;

        public void FetchRemoteConfig(Action<GameSimConfigResponse> remoteConfigFetchComplete = null)
        {
            Log.I("Fetching App Config from Remote Config");
            configManager.FetchCompleted -= Instance.ApplyRemoteConfigChanges;
            configManager.FetchCompleted += Instance.ApplyRemoteConfigChanges;
            long num = 0;

            UserAttributes _userAtt = default(UserAttributes);
            if (Configuration.Instance.IsSimulationRunningInCloud())
            {
                string execution_id = Configuration.Instance.SimulationConfig.execution_id.Split(':')[2];
                string definition_id = Configuration.Instance.SimulationConfig.definition_id.Split(':')[2];


                var sb = new StringBuilder();
                sb.Append($"instance id = {Configuration.Instance.GetInstanceId()}\n");
                sb.Append($"execution id - {execution_id}\n");
                sb.Append($"definition id - {definition_id}\n");

                var appParams = Configuration.Instance.GetAppParams<GameSimAppParams>();

                sb.Append($"app params\n");
                sb.Append($"decision engine id - {appParams.gameSimDecisionEngineId}\n");
                sb.Append($"decision engine type - {appParams.gameSimDecisionEngineType}\n");
                sb.Append($"environment id - {appParams.environmentId}\n");

                var simConfig = Unity.Simulation.Configuration.Instance.SimulationConfig;

                sb.Append($"storage_uri_prefix; {simConfig.storage_uri_prefix}\n");
                sb.Append($"app_param_uri; {simConfig.app_param_uri}\n");
                sb.Append($"current_attempt; {simConfig.current_attempt}\n");
                sb.Append($"chunk_size_bytes; {simConfig.chunk_size_bytes}\n");
                sb.Append($"chunk_timeout_ms; {simConfig.chunk_timeout_ms}\n");
                sb.Append($"instance_id; {simConfig.instance_id}\n");
                sb.Append($"time_logging_timeout_sec; {simConfig.time_logging_timeout_sec}\n");
                sb.Append($"heartbeat_timeout_sec; {simConfig.heartbeat_timeout_sec}\n");
                sb.Append($"app_param_id; {simConfig.app_param_id}\n");
                sb.Append($"signlynx_host; {simConfig.signlynx_host}\n");
                sb.Append($"signlynx_port; {simConfig.signlynx_port}\n");
                sb.Append($"bearer_token; {simConfig.bearer_token}\n");
                sb.Append($"execution_id; {simConfig.execution_id}\n");
                sb.Append($"definition_id; {simConfig.definition_id}\n");
                sb.Append($"bucketName; {simConfig.bucketName}\n");
                sb.Append($"storagePath; {simConfig.storagePath}\n");
                sb.Append($"storagePathOther; {simConfig.storagePathOther}\n");

                Log.I(sb.ToString());

                string filePath = new Uri(simConfig.app_param_uri).AbsolutePath;
                string appConfig;
                if (File.Exists(filePath))
                {
                    appConfig = File.ReadAllText(filePath);
                    appParams = JsonUtility.FromJson<GameSimAppParams>(appConfig);
                }


                if (Int64.TryParse(Configuration.Instance.GetInstanceId(), out num))
                {
                    _userAtt = new UserAttributes()
                    {
                        gameSimInstanceId = num,
                        gameSimExecutionId = execution_id,
                        gameSimDefinitionId = definition_id,
                        gameSimDecisionEngineId = appParams.gameSimDecisionEngineId,
                        gameSimDecisionEngineType = appParams.gameSimDecisionEngineType
                    };
                }
                configManager.SetEnvironmentID(appParams.environmentId);
            }

            ConfigHandler = remoteConfigFetchComplete;
            configManager.FetchConfigs(_userAtt, new UserAttributes());
        }

        void ApplyRemoteConfigChanges(ConfigResponse response)
        {
            Log.I("Remote Config fetched with response " + response.status + " with origin " + response.requestOrigin);
            switch (response.requestOrigin)
            {
                case ConfigOrigin.Default:
                    Log.I("No settings loaded this session; using default values.");
                    break;
                case ConfigOrigin.Cached:
                    Log.I("No settings loaded this session; using cached values from a previous session.");
                    break;
                case ConfigOrigin.Remote:
                    Log.I("Remote Config fetch was completed successfully with the server");
                    Log.I("Config fetched: " + configManager.appConfig.config.ToString(Newtonsoft.Json.Formatting.None));
                    GameSimManager.Instance.AddMetaData = () => configManager.appConfig.config.ToString(Newtonsoft.Json.Formatting.None);
                    ConfigHandler?.Invoke(new GameSimConfigResponse());
                    break;
            }
        }
#endif
    }

}


