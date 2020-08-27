using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Unity.Simulation.Games
{
    public class GameSimManager
    {
        private static GameSimManager _instance;
        public static GameSimManager Instance => _instance ?? (_instance = new GameSimManager());

#if UNITY_GAME_SIMULATION || UNITY_EDITOR
        internal string RunId { get; }

        internal int InstanceId { get; }

        private int _countersSequence = 0;

        private bool _isShuttingDown = false;

        internal Func<string> AddMetaData;

        internal string AttemptId
        {
            get
            {
                return Configuration.Instance.GetAttemptId();
            }
        }

        internal T GetAppParams<T>()
        {
            return Configuration.Instance.GetAppParams<T>();
        }

        internal Counter GetCounter(string name)
        {
            if (!_counters.ContainsKey(name))
            {
                lock (_mutex)
                {
                    // Verify the counter still doesn't exist after acquiring the lock
                    if (!_counters.ContainsKey(name))
                    {
                        var counter = new Counter(name);
                        _counters[name] = counter;
                    }
                }
            }
            return _counters[name];
        }
        
#endif

        /// <summary>
        /// Increment the counter with a certain number
        /// </summary>
        /// <param name="name">Name of the counter to increment.</param>
        /// <param name="amount">Amount by which to increment the counter.</param>
        public void IncrementCounter(string name, Int64 amount)
        {
#if UNITY_GAME_SIMULATION || UNITY_EDITOR
            if (!_isShuttingDown)
            {
                GetCounter(name).Increment(amount);
            }
#endif
        }

        /// <summary>
        /// Sets the counter to the value passed in
        /// </summary>
        /// <param name="name">Name of the counter to increment.</param>
        /// <param name="value">Value to set the counter to.</param>
        public void SetCounter(string name, Int64 value)
        {
#if UNITY_GAME_SIMULATION || UNITY_EDITOR
            if (!_isShuttingDown)
            {
                GetCounter(name).Reset(value);
            }
#endif
        }

        /// <summary>
        /// Resets the counter back to 0
        /// </summary>
        /// <param name="name">Name of the counter</param>
        public void ResetCounter(string name)
        {
#if UNITY_GAME_SIMULATION || UNITY_EDITOR
            SetCounter(name, 0);
#endif
        }

        /// <summary>
        /// Snapshots the current values of all counters and labels them. 
        /// Label will append a numeric value if it is not unique.
        /// </summary>
        /// <param name="label">Label to tag counter values with</param>
        public void SnapshotCounters(string label)
        {
#if UNITY_GAME_SIMULATION || UNITY_EDITOR
            int i = 0;
            string uniqueLabel = label;
            while (_snapshotLabels.Contains(uniqueLabel))
            {
                uniqueLabel = label + '-' + i;
                i++;
            }
            _snapshotLabels.Add(uniqueLabel);
            
            lock (_mutex)
            {
                foreach (var kvp in _counters)
                {
                    Counter c = GetCounter(kvp.Key);
                    c.Snapshot(uniqueLabel);
                }
            }
#endif
        }
        
        /// <summary>
        /// Enable step series metrics for a list of counters captured at the specified cadence.
        /// The minimum interval is 15 seconds, if a smaller value is provided it will be increased to the minimum.
        /// </summary>
        /// <param name="intervalSeconds">The number of seconds between each snapshot. Must be 15 or more seconds.</param>
        /// <param name="counterNames">A list of counter names to track at the same cadence</param>
        public void CaptureStepSeries(int intervalSeconds, string counterName)
        {
#if UNITY_GAME_SIMULATION || UNITY_EDITOR
            if (intervalSeconds < 15)
            {
                Debug.LogWarning("Interval seconds must be at least 15, using the minimum value instead.");
                intervalSeconds = 15;
            }

            var counter = GetCounter(counterName);
            counter.CaptureStepSeries(intervalSeconds);
            TickManager.Enable();
#endif
        }

        //
        // Non-public
        //

#if UNITY_GAME_SIMULATION || UNITY_EDITOR
        object _mutex = new object();

        Dictionary<string, Counter> _counters = new Dictionary<string, Counter>();

        HashSet<string> _snapshotLabels = new HashSet<string>();

        private GameSimManager()
        {
            Log.I($"Initializing the Game Simulation package");
            Manager.Instance.ShutdownNotification += ShutdownHandler;
        }

        [Serializable]
        private struct CountersData
        {
            public Metadata metadata;
            public Counter[] items;

            public CountersData(Dictionary<string,Counter> counters, Metadata metadata)
            {
                items = new Counter[counters.Count];
                int index = 0;
                foreach (var kvp in counters)
                    items[index++] = counters[kvp.Key];
                this.metadata = metadata;
            }
        }

        [Serializable]
        private struct Metadata
        {
            public string instanceId;
            public string attemptId;
            public string gameSimSettings;

            public Metadata(string instanceId, string attemptId, string gameSimSettings)
            {
                this.instanceId = instanceId;
                this.attemptId = attemptId;
                this.gameSimSettings = gameSimSettings;
            }
        }

        /// <summary>
        /// This function flushes a particular counter to the file system. You can choose to not reset the counter if required.
        /// </summary>
        /// <param name="counter">Name of the counter. It is also the name of the file.</param>
        /// <param name="consumer">If you want to perform any operations on the file once it is generated. Write to the FS happens on a background thread.</param>
        /// <param name="resetCounter">Resets the counter. Set to true by default.</param>
        [Obsolete("This method is not currently used and may be removed in a future version.")]
        internal void ResetAndFlushCounterToDisk(string counter, Action<string> consumer = null, bool resetCounter = true)
        {
            var asyncWriteRequest = Manager.Instance.CreateRequest<AsyncRequest<String>>();
            Int64 currentCount = -1;

            lock (_mutex)
            {
                if (!_counters.ContainsKey(counter))
                    return;
                asyncWriteRequest.data = JsonUtility.ToJson(_counters[counter]);
                currentCount = _counters[counter]._count++;
                if (resetCounter)
                {
                    _counters[counter].Reset();
                }
            }

            asyncWriteRequest.Start((request) =>
            {
                var filePath = Path.Combine(Manager.Instance.GetDirectoryFor("GameSim"),
                    counter + "_" + currentCount + ".json");
                FileProducer.Write(
                    filePath,
                    Encoding.ASCII.GetBytes(request.data));
                consumer?.Invoke(filePath);
                return AsyncRequest.Result.Completed;
            });

        }

        /// <summary>
        /// Flush all Counters in memory to the file system. This will create a file named counters_{sequencenumber}.
        /// This can be called at any time you want to capture the state of the counters.
        /// </summary>
        /// <param name="resetCounters">This tells if the counters needs to be reset. By default it's set to true.</param>
        [Obsolete("This method is not currently used and may be removed in a future version.")]
        internal void FlushAllCountersToDiskAndReset(bool resetCounters = true)
        {
            FlushCountersToDisk();

            if (resetCounters)
            {
                ResetCounters();
            }
        }

        private void ResetCounters()
        {
            foreach (var kvp in _counters)
            {
                Counter c = GetCounter(kvp.Key);
                c.Reset();
            }
        }

        private void ShutdownHandler()
        {
            _isShuttingDown = true;

            FlushCountersToDisk();
        }

        private void FlushCountersToDisk()
        {
            Log.I("Flushing counters to disk");
            string json = null;

            Metadata metadata;
            CountersData counters;
            lock (_mutex)
            {
                metadata = new Metadata(
                    Configuration.Instance.GetInstanceId(),
                    Configuration.Instance.GetAttemptId(),
                    AddMetaData?.Invoke()
                );

                counters = new CountersData(_counters, metadata);
            }

            json = JsonConvert.SerializeObject(counters, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            Log.I("Writing the GameSim Counters files..");
            if (json != null)
            {
                var filename = "counters_" + _countersSequence + ".json";
                FileProducer.Write(Path.Combine(Manager.Instance.GetDirectoryFor("GameSim"), filename), Encoding.ASCII.GetBytes(json));
                _countersSequence++;
            }
        }
#endif

        /// <summary>
        /// Fetch the GameSim config for this instance.
        /// </summary>
        /// <param name="configFetchCompleted">When the fetch is completed, the action is called with a GameSimConfigResponse object
        /// that can be used to get GameSim values for the keys in the simulation.</param>
        public void FetchConfig(Action<GameSimConfigResponse> configFetchCompleted)
        {
#if UNITY_GAME_SIMULATION || UNITY_EDITOR
            RemoteConfigProvider.Instance.FetchRemoteConfig(configFetchCompleted);
#endif
        }

        /// <summary>
        /// Returns the value associated with the given key.
        /// </summary>
        /// <param name="key">Name of the parameter to fetch</param>
        /// <param name="defaultValue">If not found, the value returned by this function. Defaults to the default value for the given type.</param>
        /// <typeparam name="T">Type of the parameter. At edit time you can infer this with GameSimEditorUtilities.TypeFor(string key).</typeparam>
        /// <returns>Value associated with the key</returns>
        public T Get<T>(string key, T defaultValue = default)
        {
#if UNITY_GAME_SIMULATION || UNITY_EDITOR
            var config = RemoteConfigProvider.Instance.configManager.appConfig.config[key];
            if (config == null)
            {
                Debug.LogWarning("failed to fetch configuration");
                return defaultValue;
            }
            return config.Value<T>();
#else
            return default;
#endif
        }

        /// <summary>
        /// Returns whether or not the given key exists in the parameter configuration
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool HasKey(string key)
        {
#if UNITY_GAME_SIMULATION || UNITY_EDITOR
            return RemoteConfigProvider.Instance.configManager.appConfig.HasKey(key);
#else
            return false;
#endif
        }
        
        /// <summary>
        /// Returns a list of all keys for parameters in the game simulation environment
        /// </summary>
        /// <returns></returns>
        public string[] GetKeys()
        {
#if UNITY_GAME_SIMULATION || UNITY_EDITOR
            return RemoteConfigProvider.Instance.configManager.appConfig.GetKeys();
#else
            return null;
#endif
        }
    }
}
