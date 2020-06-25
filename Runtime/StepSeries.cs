using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Unity.Simulation.Games
{
    internal class StepSeries
    {
        [JsonIgnore]
        private Counter _counter;

        [JsonProperty("values")]
        private List<Int64> _values = new List<Int64>();
        
        [JsonProperty("interval")]
        private int _resetAmount;

        [JsonIgnore]
        private int _currentAmount;

        [JsonProperty("unit")]
        [JsonConverter(typeof(StringEnumConverter))]
        private StepSeriesInterval _interval;
        
        internal StepSeries(StepSeriesInterval interval, int intervalAmount, Counter counter)
        {
            _interval = interval;
            _resetAmount = intervalAmount;
            switch (interval)
            {
                case StepSeriesInterval.Seconds: TickManager.EverySecond += Tick;
                    break;
            }
            _counter = counter;
            CaptureValue();
        }

        private void Tick()
        {
            _currentAmount--;
            if (!(_currentAmount <= 0)) return;
            
            _currentAmount = _resetAmount;
            CaptureValue();
        }
        
        private void CaptureValue()
        {
            if (_counter != null)
            {
                _values.Add(_counter.Value);
            }
        }
        
        ~StepSeries()
        {
            switch (_interval)
            {
                case StepSeriesInterval.Seconds: TickManager.EverySecond -= Tick;
                    break;
            }
        }
    }
}