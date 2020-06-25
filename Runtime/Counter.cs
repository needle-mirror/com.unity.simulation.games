using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

[assembly: InternalsVisibleTo("Unity.Simulation.Games.Tests")]
namespace Unity.Simulation.Games
{
    [Serializable]
    internal class Counter
    {
        [JsonProperty]
        string _name;

        [JsonIgnore]
        public string Name { get { return _name; } }

        [JsonProperty]
        internal Int64 _value;
        
        [JsonProperty]
        internal OrderedDictionary _snapshots;

        [JsonProperty]
        internal StepSeries _stepSeries;

        internal Int64 _count;

        [JsonIgnore]
        public Int64 Value { get { return _value; } }

        public Counter(string name)
        {
            _name = name;
            Reset();
        }

        internal Int64 Increment(Int64 amount)
        {
            return Interlocked.Add(ref _value, amount);
        }

        internal void Reset(Int64 value = 0)
        {
            Interlocked.Exchange(ref _value, value);
        }

        internal void Snapshot(String label)
        {
            if (_snapshots == null)
            {
                _snapshots = new OrderedDictionary();
            }
            _snapshots.Add(label, _value);
        }

        internal void CaptureStepSeries(int intervalSeconds)
        {
            if (intervalSeconds <= 0)
            {
                Debug.LogError("Interval seconds must be greater than 0");
                return;
            }

            if (_stepSeries != null)
            {
                Debug.LogError("Step Series has already been enabled for Counter " + _name);
            }

            _stepSeries = new StepSeries(StepSeriesInterval.Seconds, intervalSeconds, this);
        }
    }
}
