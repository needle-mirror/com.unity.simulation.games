using System;
using UnityEngine;

namespace Unity.Simulation.Games
{
    internal class TickManager : MonoBehaviour
    {
        private static TickManager _instance;
        public static TickManager Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;
            
                var obj = new GameObject("GameSimTickManager");
                _instance = obj.AddComponent<TickManager>();
                DontDestroyOnLoad(obj);
                return _instance;
            }
        }

        private bool _active = false;
        
        /// <summary>
        /// Is the TickManager listening for step series events?
        /// </summary>
        public static bool Active => Instance._active;
        
        /// <summary>
        /// Enable recording of step series data
        /// </summary>
        public static void Enable() => Instance._active = true;
        
        /// <summary>
        /// Disable recording of step series data
        /// </summary>
        public static void Disable() => Instance._active = false;

        public static event Action EverySecond;

        private float _secondsLeft = 1f;

        private void Update()
        {
            if (!Active)
                return;

            _secondsLeft -= Time.deltaTime;

            while (_secondsLeft < 0)
            {
                _secondsLeft += 1f;
                EverySecond?.Invoke();
            }
        }
    }
}
