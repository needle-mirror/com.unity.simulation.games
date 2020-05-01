# About Unity Game Simulation
The Unity Game Simulation service helps you spend less time testing and balancing, and more time innovating new in-game experiences for your users.

The Game Simulation package helps you create a build of your game for use with the Game Simulation service. At a high level, the package:
1. Fetches parameter values for simulation and updates class values accordingly before they are required in game.
2. Updates counters for each event that is tracked.
3. Calls `Application.Quit()` at the end of gameplay.

When you've uploaded your build to Game Simulation, designers or other users at your studio can run simulations from the Game Simulation dashboard.

If you have any issues with implementation, contact [gamesimulation@unity3d.com](mailto:gamesimulation@unity3d.com).

## Preview package
This package is available as a preview, so it is not ready for production use. The features and documentation in this package might change before it is verified for release.

<a name="Installation"></a>
## Installation

To install this package, follow the instructions in [Implementation](ImplementationGuide.md).

## Requirements
This version of Game Simulation is compatible with the following versions of the Unity Editor:
* 2018.3 and later

Additional requirements to implement and use Game Simulation:
* You must have [Unity Services](https://docs.unity3d.com/Manual/SettingUpProjectServices.html) enabled on your project.
* Your game must:
  * Be compiled for Linux (that is, you need to be able to build for Linux from the Editor)
  * Use OpenGL
  * Be configured to auto-run on open (that is, it contains a bot or playthrough script)
  * Call `Application.Quit()` when gameplay is finished during runtime

Additionally, make sure you have the following design and experimentation questions:
* A list of all parameters you would like to evaluate
* A list of all metrics you would like to measure, which will show up on the Game Simulation results page in the Web UI.<br /> **Note**: only metrics stored as type Long are supported

## Additional documentation
* [FAQ](https://unity-technologies.github.io/gamesimulation/Docs/FAQ.html)
* [Dashboard Documentation](https://unity-technologies.github.io/gamesimulation/Docs/dashboard.html)
