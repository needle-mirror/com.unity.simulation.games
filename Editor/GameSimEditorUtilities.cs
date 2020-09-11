using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.RemoteConfig.Editor;
using UnityEngine;
using UnityEngine.Rendering;

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

        private static readonly object typeForTaskMut = new object();
        private static TaskCompletionSource<Type> _typeForTask;
        private static string _typeKey = null;

        private static BlockingCollection<Tuple<string, TaskCompletionSource<Type>>> _taskCompletionSources = new BlockingCollection<Tuple<string, TaskCompletionSource<Type>>>();
        private static bool _running = false;

        private static TaskScheduler unityTaskScheduler;

        /// <summary>
        /// Initializes the editor utilities; must be called on the main thread. Captures the UnitySynchronizationContext
        /// </summary>
        /// <exception cref="Exception">Called from outside the main thread</exception>
        public static void Init()
        {
            if (Thread.CurrentThread.ManagedThreadId != 1)
            {
                throw new Exception("Init must be called from the main thread");
            }

            unityTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        }

        /// <summary>
        /// Returns the type for a given parameter based on the key.
        /// </summary>
        /// <param name="key">The name of the parameter.</param>
        /// <returns>Type of parameter. Valid types are: string, bool, float, int, long, string. Parameters with the json type return string.</returns>
        /// <exception cref="Exception"></exception>
        public static Task<Type> TypeFor(string key)
        {
            if (unityTaskScheduler == null)
            {
                throw new Exception("Init must be called from the main thread prior to calling TypeFor");
            }

            var next = new Tuple<string, TaskCompletionSource<Type>>(key, new TaskCompletionSource<Type>());
            _taskCompletionSources.TryAdd(next);

            var cloudProjectId = Task.Factory.StartNew(() =>
            {
                return Application.cloudProjectId;
            }, CancellationToken.None, TaskCreationOptions.None, unityTaskScheduler).Result;

            lock (typeForTaskMut)
            {
                if (!_running)
                {
                    if (_taskCompletionSources.TryTake(out var taken))
                    {
                        _running = true;

                        _typeForTask = taken.Item2;
                        _typeKey = taken.Item1;

                        RemoteConfigWebApiClient.fetchEnvironmentsFinished += RemoteConfigEnvironmentsFetched;
                        RemoteConfigWebApiClient.FetchEnvironments(cloudProjectId);

                        return Task.Run(async () =>
                        {
                            var ret = await _typeForTask.Task;
                            lock (typeForTaskMut)
                            {
                                if (_taskCompletionSources.TryTake(out var taskTaken))
                                {
                                    _typeForTask = taskTaken.Item2;
                                    _typeKey = taskTaken.Item1;

                                    RemoteConfigWebApiClient.fetchEnvironmentsFinished += RemoteConfigEnvironmentsFetched;
                                    RemoteConfigWebApiClient.FetchEnvironments(cloudProjectId);
                                }
                                else
                                {
                                    _running = false;
                                }
                            }

                            return ret;
                        });
                    }

                    // err
                    throw new Exception("not running but already ran");
                }

                return next.Item2.Task;
            }
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

            if (gsEnv == null)
            {
                lock (typeForTaskMut)
                {
                    if (_typeForTask != null)
                    {
                        _typeForTask.SetCanceled();
                        _typeForTask = null;
                    }

                    _typeKey = null;
                }

                throw new InvalidDataException();
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

            if (config.HasValues)
            {
                var arr = (JArray) config["value"];
                foreach (var t in arr)
                {
                    var obj = (JObject) t;

                    if (obj.ContainsKey("key") && obj["key"] != null && obj["key"].Value<string>().Equals(_typeKey))
                    {
                        var type = obj["type"].Value<string>();

                        lock (typeForTaskMut)
                        {
                            _typeKey = null;

                            Type ret = null;

                            if (typeLookup.ContainsKey(type))
                            {
                                ret = typeLookup[type];
                            }

                            _typeForTask.SetResult(ret);
                            return;
                        }
                    }
                }
            }

            lock (typeForTaskMut)
            {
                if (_typeForTask != null)
                {
                    _typeForTask.SetCanceled();
                    _typeForTask = null;
                }

                _typeKey = null;
            }
        }
    }
}
