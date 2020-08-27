# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.4.4-preview.2] - 2020-08-27
## This is the v0.4.4-preview.1 release of *Unity Game Simulation*
- Fix issue for builds where UNITY_GAME_SIMULATION is not defined

## [0.4.4-preview.1] - 2020-08-25
## This is the v0.4.4-preview.1 release of *Unity Game Simulation*
- Updated dependencies

## [0.4.3-preview.5] - 2020-08-05
## This is the v0.4.3-preview.5 release of *Unity Game Simulation*
- Address an issue which could result in parameters not correctly forwarding to the simulation.

## [0.4.3-preview.4] - 2020-07-30
## This is the v0.4.3-preview.4 release of *Unity Game Simulation*
- Update documentation

## [0.4.3-preview.3] - 2020-07-27
## This is the v0.4.3-preview.3 release of *Unity Game Simulation*
- Uses a separate remote config environment, allowing users to use remote config without polluting their game environment from their game sim environment
- Remote Config package requirement updated to 1.3.2-preview.6
- Builds will no longer attempt to upload on failure
- Add additional error messages
- Add scripting define: UNITY_GAME_SIMULATION -- if this define is not present, all public API are no-op's. This is handled by the Game Sim Editor Window's "Build and Upload" button. See https://docs.unity3d.com/ScriptReference/PlayerSettings.SetScriptingDefineSymbolsForGroup.html for integration into CI/CD.

## [0.4.3-preview.1] - 2020-06-25

## This is the v0.4.3-preview.1 release of *Unity Game Simulation*
- Added support for step series metrics
- Updated build name validation to match Unity Simulation

## [0.4.2-preview.1] - 2020-05-05

## This is the v0.4.2-preview.1 release of *Unity Game Simulation*
- On build & upload will now set `PlayerSettings.runInBackground` to true, then set it back to what it was previously after the build is complete.
- On build & upload in Unity versions <2019.1 will now set `PlayerSettings.displayResolutionDialog` to false, then set it back to what it was previously after the build is complete.
- Added logging of values fetched from Remote Config to the player log for debugging purposes.
- Updated Unity Simulation Core package to `0.0.10-preview.14` due to a fps bug in `0.0.10-preview.11`.

## [0.4.1-preview.7] - 2020-05-04

## This is the v0.4.1-preview.6 release of *Unity Game Simulation*
- Actually updated Unity Simulation Core package to `0.0.10-preview.11` due to a bug in `0.0.10-preview.10`.

## [0.4.1-preview.6] - 2020-05-01

## This is the v0.4.1-preview.6 release of *Unity Game Simulation*
- Updated Unity Simulation Core package to `0.0.10-preview.11` due to a bug in `0.0.10-preview.10`.

## [0.4.1-preview.5] - 2020-05-01

## This is the v0.4.1-preview.5 release of *Unity Game Simulation*
- Updated Unity Simulation Core package to `0.0.10-preview.10` due to a bug in `0.0.10-preview.9`.
- Updated documentation to reflect that `runInBackground` needs to enabled and `displayResolutionDialog` needs to be disabled for simulations to run.

## [0.4.1-preview.4] - 2020-04-17

## This is the v0.4.1-preview.4 release of *Unity Game Simulation*
- Updated documentation

## [0.4.1-preview.3] - 2020-04-17

## This is the v0.4.1-preview release of *Unity Game Simulation*
- Added XML docs for public APIs.
- Added build name validation and warning if the build name is invalid.
- Fixed bug where scenes from build settings window would not appear in the Game Simulation window.

## [0.4.0-preview] - 2020-03-13

## This is the v0.4.0-preview release of *Unity Game Simulation*
- Removed the request timeout from build & upload from the editor. Note, this does mean the editor will lock up while the upload it happening.
- Backend errors for uploading builds are now surfaced as console log errors.


## [0.3.0-preview] - 2020-02-26

## This is v0.3.0-preview release of *Unity Game Simulation*
- Added GUI for uploading builds and adding parameters to your config
   - Can be accessed by Window > Game Simulation
   - If scenes are not showing up in the build window, ensure they are added to your build settings first.
- Fixed bug where zips for builds generated in Windows used backwards slashes, instead of forward ones, which caused an error on the backend, since Linux requires Unix paths.
- Added a timeout to the build upload web request (hardcoded to 10 minutes for now)
- Added better internal logging of runtime operations within the Game Simulation package.


## [0.2.0] - 2020-02-05

## This is v0.2.0-preview release of *Unity Simulation for Games (GameSim)*
- Updated namepsaces: `Unity.AI.GameSim` is now `Unity.Simulations.Games` to be more descriptive, and it doesn't belong under the `AI` namespace, since the package does not provide code that will be the AI in a simulation.
- Made the `Counter` class internal, since APIs are now exposed through `GameSimManager` to manipulate counters.
- The following APIs have been updated in `GameSimManager`:
	- `Counter GetCounter(string name)` is now internal, since the new APIs for counters no longer require a developer to get the object.
	- `public void IncrementCounter(string name, Int64 amount)` has now been renamed to `IncrementCounter` to be more descriptive.
	- `public void Reset(string name, Int64 value = 0)` has now been refactored to `ResetCounter(string name)`, which is more desciptive and single use in what it does: resets a specific counter to 0.
	- Added `public void SetCounter(string name, Int64 amount)` so that a counter can be set a specific value.
	- `string RunId` is now internal, since it does not need to be implemented by developers.
	- `int InstanceId` is now internal, since it does not need to be implemented by developers.
	- `Func<string> AddMetaData` is now internal, since it does not need to be implemented by developers.
	- `string AttemptId` is now internal, since it does not need to be implemented by developers.
	- `T GetAppParams<T>()` is now internal, since it does not need to be implemented by developers.
	- `void ResetAndFlushCounterToDisk` is now internal, since it does not need to be implemented by developers.
	- `void FlushAllCountersToDiskAndReset` is now internal, since it does not need to be implemented by developers.
	- Added new API: `public void FetchConfig(Action<GameSimConfigResponse> configFetchCompleted)`.
		- Call this to fetch the config for the simulation instance, pass in a method that will take `GameSimConfigResponse` as a parameter, and use that to get the key value pairs from the the config.
- The `RemoteConfigProvider` class is now internal, since the APIs are exposed through a new `GameSimConfigRepsonse` struct.
- Added a new struct `GameSimConfigResponse` with the following APIs:
	- `public int GetInt(string key, int defaultValue = 0)` retrieves the int value of a corresponding key, if one exists. Returns `defaultValue` if the key does not exist.
	- `public bool GetBool(string key, bool defaultValue = false)` retrieves the bool value of a corresponding key, if one exists.  Returns `defaultValue` if the key does not exist.
	- `public float GetFloat(string key, float defaultValue = 0f)` retrieves the float value of a corresponding key, if one exists.  Returns `defaultValue` if the key does not exist.
	- `public long GetLong(string key, long defaultValue = 0L)` retrieves the int value of a corresponding key, if one exists.  Returns `defaultValue` if the key does not exist.
	- `public string GetString(string key, string defaultValue = "")` retrieves the int value of a corresponding key, if one exists.  Returns `defaultValue` if the key does not exist.
- Fixed typos in the package license.

## [0.1.0] - 2019-09-27

## This is v0.1.0-preview release of *Unity Simulation for Games (GameSim)*
- This package contains funtionality to create Counters for playtesting service.
