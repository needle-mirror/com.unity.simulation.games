using System;

namespace Unity.Simulation.Games
{
    /// <summary>
    /// Struct used to fetch values from keys in the GameSim run.
    /// </summary>
    public struct GameSimConfigResponse
    {
        /// <summary>
        /// Retrieves the int value of a corresponding key, if one exists.
        /// </summary>
        /// <param name="key">The key identifying the corresponding setting.</param>
        /// <param name="defaultValue">The default value to use if the specified key cannot be found or is unavailable.</param>
        /// <returns>Integer representing the value of the key. Defaults to 0 if the key is not found.</returns>
        public int GetInt(string key, int defaultValue = 0)
        {
#if UNITY_GAME_SIMULATION || UNITY_EDITOR
            return RemoteConfigProvider.Instance.configManager.appConfig.GetInt(key, defaultValue);
#else
            return 0;
#endif
        }

        /// <summary>
        /// Retrieves the boolean value of a corresponding key, if one exists.
        /// </summary>
        /// <param name="key">The key identifying the corresponding setting.</param>
        /// <param name="defaultValue">The default value to use if the specified key cannot be found or is unavailable.</param>
        /// <returns>Bool representing the value of the key. Defaults to false if the key is not found.</returns>
        public bool GetBool(string key, bool defaultValue = false)
        {
#if UNITY_GAME_SIMULATION || UNITY_EDITOR
            return RemoteConfigProvider.Instance.configManager.appConfig.GetBool(key, defaultValue);
#else
            return false;
#endif
        }

        /// <summary>
        /// Retrieves the float value of a corresponding key from the remote service, if one exists.
        /// </summary>
        /// <param name="key">The key identifying the corresponding setting.</param>
        /// <param name="defaultValue">The default value to use if the specified key cannot be found or is unavailable.</param>
        /// <returns>Float representing the value of the key. Defaults to 0f if the key is not found.</returns>
        public float GetFloat(string key, float defaultValue = 0f)
        {
#if UNITY_GAME_SIMULATION || UNITY_EDITOR
            return RemoteConfigProvider.Instance.configManager.appConfig.GetFloat(key, defaultValue);
#else
            return float.NaN;
#endif
        }

        /// <summary>
        /// Retrieves the long value of a corresponding key from the remote service, if one exists.
        /// </summary>
        /// <param name="key">The key identifying the corresponding setting.</param>
        /// <param name="defaultValue">The default value to use if the specified key cannot be found or is unavailable.</param>
        /// <returns>Long representing the value of the key. Defaults to 0L if the key is not found.</returns>
        public long GetLong(string key, long defaultValue = 0L)
        {
#if UNITY_GAME_SIMULATION || UNITY_EDITOR
            return RemoteConfigProvider.Instance.configManager.appConfig.GetLong(key, defaultValue);
#else
            return 0;
#endif
        }

        /// <summary>
        /// Retrieves the string value of a corresponding key from the remote service, if one exists.
        /// </summary>
        /// <param name="key">The key identifying the corresponding setting.</param>
        /// <param name="defaultValue">The default value to use if the specified key cannot be found or is unavailable.</param>
        /// <returns>String representing the value of the key. Defaults to "" if the key is not found.</returns>
        public string GetString(string key, string defaultValue = "")
        {
#if UNITY_GAME_SIMULATION || UNITY_EDITOR
            return RemoteConfigProvider.Instance.configManager.appConfig.GetString(key, defaultValue);
#else
            return string.Empty;
#endif
        }
    }
}