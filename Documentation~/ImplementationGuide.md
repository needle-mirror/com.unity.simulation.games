# Implementing Game Simulation

To implement the Game Simulation package:

1. Install the Game Simulation package
2. Create parameters in Game Simulation for each grid search parameter
3. Load parameters for grid search
4. Enable metrics tracking
5. Test your implementation
6. Upload your build to Game Simulation

These steps are described below.

## Step 1. Install the Game Simulation package
To download the Game Simulation package, add the following line to your project's dependencies in your `manifest.json` file: `"com.unity.simulation.games": "0.4.3-preview.7",`

Unity Game Simulation requires the following packages, and adds these dependencies for you:
* Remote Config: `"com.unity.remote-config" : "1.3.2-preview.6"`
* USim: `"com.unity.simulation.core" : "0.0.10-preview.14"`

If your project also requires these packages, the Game Simulation team recommends using these versions.
The Game Simulation team publishes notifications on updates to dependent versions in Unity Game Simulation's release notes.

## Step 2. Create parameters in Game Simulation for each grid search parameter
1. In the Unity Editor, open the **Game Simulation** window (**Window** > **Game Simulation**).
2. Add an entry for each parameter that you want to test with Game Simulation.
   - Choose a name, type, and default value for each parameter.<br />**Note**: Use a valid default value. This is used during Build Verification to ensure the build is working properly when you upload it to Game Simulation.
3. Click **Save** when done.<br />
**Note**: Game Simulation updates the values for these parameters in each simulation instance, based on the options specified in the Game Simulation web UI. If your build runs outside of Game Simulation, these parameters use the value from the **Default Value** field. Set an appropriate default value for each parameter.

## Step 3. Load parameters for grid search
Before each run of your simulation, Game Simulation decides on a set of parameter values to evaluate. At runtime, your game must retrieve the set of parameter values for evaluation and then set variables in your game to those values. To fetch the parameter values, call `GameSimManager.Instance.FetchConfig(Action<GameSimConfigResponse>)`. This is included in the Game Simulation package. 

To load parameters into your game:
1. Ensure you can access the Game Simulation types with `using Unity.Simulation.Games;`.
2. Fetch this run’s set of parameter values with `GameSimManager.Instance`’s `FetchConfig` method at game start. This stores this run’s parameter values in a `GameSimConfigResponse` object.
3. Set game variables to the values now stored in the `GameSimConfigResponse` object. Access the variables stored in `GameSimConfigResponse` with
`GameSimConfigResponse.Get[variable type]("key name");`

## Step 4. Enable metrics tracking
Game Simulation uses a counter to track metrics throughout each run of your game. You can set, increase, and reset a counter's value at any point of your game code. You can also take a snapshot of all the counters with a label at a specific point. For example, when a level or a session completes. You can also snapshot a counter at a specific time interval. The minimum interval is 15 seconds. 

When the simulation completes, you can download these metrics in both raw and aggregated forms from the Game Simulation [Dashboard](https://gamesimulation.unity3d.com).


### Example implementation
This example uses a racing game that tracks lap count and finishing time.

#### Call `IncrementCounter` or `SetCounter` to update counter values 

If Unity doesn't find a counter with the supplied `name`, Unity creates the counter, initializes it to 0, then either increments or sets it as appropriate.
```
   void OnLapFinish()
   {        
      GameSimManager.Instance.IncrementCounter("lapCount", 1);
   }

   void OnFinish()
   {        
      GameSimManager.Instance.SetCounter("finishingTime", GetFinishingTime());
   }
```

#### (Optional) Call `Reset` to reset the counter to 0
```
   void OnFinish()
   {        
      GameSimManager.Instance.Reset("finishingTime", getFinishingTime());
      Application.Quit()
   }
```

#### (Optional) Snapshot all counters at a specific point in time

Call `SnapshotCounters(label)` to snapshot all counters at that point in time and record their values marked with the provided label. The metrics are aggregated across all runs grouped by the label and counter name.

In this example, say you want to take a snapshot of all the counters when the racing game session 1 ends. OnSessionOneFinish is a dummy method created for the demo. When the `SnapshotCounter` method is called, all counters including lap count and the finishing time's current values are recorded with the label "session-1".
```
 void OnSessionOneFinish()
   {        
      GameSimManager.Instance.SnapshotCounters("session-1");
   }
```

#### (Optional) Snapshot specific counter at a specified interval

Call`CaptureStepSeries(intervalSeconds, <counterName>)` to capture the provided counter’s value at the specified cadence in seconds, with a minimum interval of 15 seconds. If the interval is shorter than 15 seconds, it is automatically increased to the minimum 15 seconds.

In this example, say you want to take snapshot of the lap count every 15 seconds when the game starts.
```
  void Start()
      {
          GameSimManager.Instance.CaptureStepSeries(15, "lapCount");
      }
```

## Step 5. Test your implementation
1. Build the game targeted to your operating system with the Game Simulation SDK implemented. Unity Game Simulation requires the following symbol defined: `UNITY_GAME_SIMULATION`.
2. Run the executable and verify that your gameplay script or bot plays through the game with no external input and quits on its own.
3. Verify that there is a file called `counters_0.json` in your system’s default `Application.persistentDataPath`. If you are using a Mac, this should be `~/Library/Application Support/Unity Technologies/`.

## Step 6. Upload your build to Game Simulation
1. In the Project Settings window (**Edit** > **Project Settings**), select **Player** in the menu. Enable **Run in Background** and set **Display Resolution Dialog** to "Disabled".
2. In the Build Settings window (**File** > **Build Settings**), make sure the **Scenes in Build** area lists all the scenes you would like to include in your build. To add scenes, open the scenes in the Unity Editor and in the Build Settings window click **Add Open Scenes**.
3. Close the Build Settings window.
4. In the Game Simulation window, click the **Build Upload** tab.
5. Select the scenes to include in your build.
6. Name your build.
7. Click **Build and Upload**.
8. Click **Create Simulation** (or navigate directly to the [Dashboard](https://gamesimulation.unity3d.com)) to run a simulation from the Web UI. 

To execute a simulation from the dashboard, your Unity organization must be on the free tier. This usually takes about one week after signing up.  If you receive a "Maximum Simulation Minutes Exceeded" warning in the dashboard, please email [gamesimulation@unity3d.com](mailto:gamesimulation@unity3d.com).

## Step 7. Access the dashboard
See the [Dashboard Guide](https://unity-technologies.github.io/gamesimulation/Docs/dashboard.html).

## GameSim APIs
For more information on the GameSim APIs, see the [GameSim APIs documentation](https://unity-technologies.github.io/gamesimulation/Docs/gamesim-apis.html).
