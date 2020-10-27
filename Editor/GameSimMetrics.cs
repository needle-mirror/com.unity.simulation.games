using UnityEngine;
using System.Collections.Generic;
using UnityEditor.SettingsManagement;

namespace Unity.Simulation.Games.Editor
{
    [System.Serializable]
    internal class GameSimMetrics : ScriptableObject
    {
        private Settings gsSettings;

        public List<string> metrics;

        void OnValidate()
        {
            if (gsSettings != null)
            {
                gsSettings.Set("metricNames", metrics);
                gsSettings.Save();
            }
        }

        void Awake()
        {
            gsSettings = new Settings("com.unity.simulation.games");

            if (!gsSettings.ContainsKey<List<string>>("metricNames"))
            {
                gsSettings.Set("metricNames", new List<string>());
                gsSettings.Save();
            }

            metrics = gsSettings.Get<List<string>>("metricNames");
        }
    }
}