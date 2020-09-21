using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.RemoteConfig.Editor;
using UnityEngine;

namespace Unity.Simulation.Games.Editor
{
    public static class GameSimEditorUtilities
    {
        private static readonly Dictionary<string, Type> typeLookup = new Dictionary<string, Type>
        {
            {
                "string", typeof(string)
            },
            {
                "bool", typeof(bool)
            },
            {
                "float", typeof(float)
            },
            {
                "int", typeof(int)
            },
            {
                "long", typeof(long)
            },
            {
                "json", typeof(string)
            }
        };
        
        private static Dictionary<string, Type> typeDict = new Dictionary<string, Type>();
        private static bool hasInitialized = false;

        /// <summary>
        /// Requests parameter type information from remote config and stores it
        /// </summary>
        public static void Init()
        {
            RemoteConfigWebApiClient.fetchEnvironmentsFinished += RemoteConfigEnvironmentsFetched;
            RemoteConfigWebApiClient.FetchEnvironments(Application.cloudProjectId);
        }

        
        /// <summary>
        /// Returns true when the Init method's request has successfully executed
        /// </summary>
        /// <returns></returns>
        public static bool IsInitialized()
        {
            return hasInitialized;
        }

        /// <summary>
        /// Returns the type for a given parameter based on the key.
        /// </summary>
        /// <param name="key">The name of the parameter.</param>
        /// <returns>Type of parameter. Valid types are: string, bool, float, int, long, string. Parameters with the json type return string.</returns>
        /// <exception cref="Exception"></exception>
        public static Type TypeFor(string key)
        {
            return typeDict[key];
        }

        private static void RemoteConfigEnvironmentsFetched(JArray environments)
        {
            RemoteConfigWebApiClient.fetchEnvironmentsFinished -= RemoteConfigEnvironmentsFetched;

            JObject gsEnv = null;
            string environmentId;
            foreach (var environment in environments)
            {
                if (environment["name"].Value<string>() == "GameSim")
                {
                    gsEnv = (JObject) environment;
                    environmentId = gsEnv["id"].Value<string>();
                    FetchConfig(environmentId);
                    break;
                }
            }
        }

        private static void FetchConfig(string envId)
        {
            RemoteConfigWebApiClient.fetchConfigsFinished += RemoteConfigFetched;
            RemoteConfigWebApiClient.FetchConfigs(Application.cloudProjectId, envId);
        }


        private static void RemoteConfigFetched(JObject config)
        {
            RemoteConfigWebApiClient.fetchConfigsFinished -= RemoteConfigFetched;
            typeDict.Clear();

            if (config.HasValues)
            {
                var arr = (JArray) config["value"];
                foreach (var t in arr)
                {
                    var obj = (JObject) t;

                    if (obj.ContainsKey("key") && obj["key"] != null && obj["type"] != null)
                    {
                        var type = obj["type"].Value<string>();

                        if (typeLookup.ContainsKey(type))
                        {
                            typeDict[obj["key"].Value<string>()] = typeLookup[type];
                        }
                    }
                }
            }

            hasInitialized = true;
        }
    }
}
