# Implementation

The following overall steps are required to implement the Game Simulation package.

1. Installing the Game Simulation package.
2. Creating parameters in Game Simulation for each grid search parameter.
3. Loading parameters for grid search.
4. Enabling tracking of metrics.
5. Testing your implementation.
6. Uploading your build to Game Simulation.

These steps are described in detail below.

## Step 1. Installing the Game Simulation package
1. To download the Game Simulation package, add the following line to your project's dependencies in your `manifest.json` file:<br /> `"com.unity.simulation.games": "0.4.1-preview.6",`

## Step 2. Creating parameters in Game Simulation for each grid search parameter
1. From the Editor, open the **Game Simulation** window (**Window** > **Game Simulation**).
2. Add an entry for each parameter that you would like to be able to test with Game Simulation.
   1. Choose a name, type, and default value for each parameter.<br />**Note**: If you’ve already done this in the **Remote Config** window, you don't need to re-create them.
3. Press **Save** when done.<br />
**Note**: Game Simulation updates the values for these parameters in each simulation instance, based on the options specified in the Game Simulation web UI. If your build is run outside of Game Simulation, these parameters use the value from the **Default Value** field. Therefore, put an appropriate default value for each parameter.

## Step 3. Loading parameters for grid search
Before each run of your simulation, Game Simulation decides on a set of parameter values to evaluate. At runtime, your game must retrieve the set of parameter values for evaluation and then set variables in your game to those values. To fetch the parameter values, call `GameSimManager.Instance.FetchConfig(Action<GameSimConfigResponse>)`. This is included in the Game Simulation package. 

To load parameters into your game:
1. Fetch this run’s set of parameter values with `GameSimManager.Instance`’s `FetchConfig` method at game start. This stores this run’s parameter values in a `GameSimConfigResponse` object.
2. Set game variables to the values now stored in the `GameSimConfigResponse` object. Access the variables stored in `GameSimConfigResponse` with
`GameSimConfigResponse.Get[variable type]("key name");`

## Step 4. Enabling tracking of metrics
Game Simulation uses a counter to track metrics throughout each run of your game. When the simulation ends, you can download these metrics in both raw and aggregated forms from the Game Simulation web UI.

### Example implementation
This example uses a racing game which tracks lap count and finishing time.

1. Call `IncrementCounter` or `SetCounter` to update counter values. If no counter is found with the supplied `name`, it is created, initialized to 0, then either incremented or set as appropriate.
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

2. Call `Application.Quit()`.
```
   void OnFinish()
   {        
      GameSimManager.Instance.Reset("finishingTime", getFinishingTime());
      Application.Quit()
   }
```

## Step 5. Testing your implementation
1. Build the game targeted to your operating system with the Game Simulation SDK implemented.
2. Run the executable and verify that your gameplay script or bot plays through the game with no external input and quits on its own.
3. Verify that there is a file called `counters_0.json` in your system’s default `Application.persistentDataPath`. If you are using a Mac, this should be `~/Library/Application Support/Unity Technologies/`.

## Step 6. Uploading your build to Game Simulation
1. In the **Project Settings** window (**Edit** > **Project Settings**), select **Player** in the menu. Make sure **Run in Background** is checked and **Display Resolution Dialog** is set to "Disabled".
2. In the **Build Settings** window (**File** > **Build Settings**), make sure the **Scenes in Build** area lists all the scenes you would like to include in your build.
   1. To add scenes, open the scenes in the Editor and click **Add Open Scenes** in the **Build Settings** window.
3. Close the **Build Settings** window.
4. Click the **Build Upload** tab in the **Game Simulation** window.
5. Select the scenes to include in your build.
6. Name your build.
7. Click **Build and Upload**.
8. Click **Create Simulation** (or navigate directly to the [Dashboard](https://gamesimulation.unity3d.com)) to run a simulation from the Web UI. 

In order to execute a simulation from the dashboard, your Unity organization must be turned on to the free tier. It usually takes about 1 week after signing up.  If you receive a "Maximum Simulation Minutes Exceeded" warning in the dashboard, please email [gamesimulation@unity3d.com](mailto:gamesimulation@unity3d.com).

## Step 7. Access the Dashboard
Please refer to the [Dashboard Guide](https://unity-technologies.github.io/gamesimulation/Docs/dashboard.html)

## GameSim APIs
If you need more information on the GameSim APIs, please refer to the GameSim APIs [document](https://unity-technologies.github.io/gamesimulation/Docs/gamesim-apis.html)
