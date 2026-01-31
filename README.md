# URDF Importer 

[![Version](https://img.shields.io/github/v/tag/Unity-Technologies/URDF-Importer)](https://github.com/Unity-Technologies/URDF-Importer/releases)
[![License](https://img.shields.io/badge/license-Apache--2.0-green.svg)](LICENSE.md)
![Unity](https://img.shields.io/badge/unity-2020.2+-brightgreen)

---

We're currently working on lots of things! Please take a short moment fill out our [survey](https://unitysoftware.co1.qualtrics.com/jfe/form/SV_0ojVkDVW0nNrHkW) to help us identify what products and packages to build next.

---

## Introduction

URDF Importer allows you to import a robot defined in [URDF](http://wiki.ros.org/urdf/XML) format in a Unity scene. URDF defines the geometry, visual meshes, kinematic and dynamic attributes of a Robot. Importer parses a URDF file and imports it into Unity using PhyX 4.0 articulation bodies.

## Using the Package

### Adding the URDF package

1. Open the Package Manager from Unity Menu. Click `Window -> Package Manager`. A new package manager window will appear.

2. Click on the `+` sign on the top left corner of the package manager window and click on `Add Package from Git URL`. 

<img src = "images~/Package_manager_add.png">

3. Enter the git URL for the URDF Importer with the latest version tag (currently v0.5.2) `https://github.com/hijimasa/URDF-Importer.git?path=/com.unity.robotics.urdf-importer#dev-hijimasa` in the text box and press `Enter`.

### Importing the robot using URDF file

1. Copy the URDF and the associated files in the assets folder in the Project window. Make sure the [location](https://github.com/Unity-Technologies/Unity-Robotics-Hub/blob/main/tutorials/urdf_importer/urdf_appendix.md#file-hierarchy) of the mesh files is correct.

2. Right Click on the URDF file and click `Import Robot from Selected URDF file`.

<img src = "images~/URDF%20Menu.png" width = 40% height = 40%>

3. A window will appear with the Import settings for the Robot. First setting mentions the orientation of the mesh files. The second setting is used to select the algorithm to be used in Collision mesh Decomposition. For more information click [here](https://github.com/Unity-Technologies/Unity-Robotics-Hub/blob/main/tutorials/urdf_importer/urdf_appendix.md#convex-mesh-collider)

<img src = "images~/URDF%20Import%20Menu.png" width = 80% height = 80%>

4. Click `Import URDF`


## Tutorials

Instructions for using URDF importer can be found [here](https://github.com/Unity-Technologies/Unity-Robotics-Hub/blob/main/tutorials/urdf_importer/urdf_tutorial.md).

## URDF Joint Configuration for Unity

This section explains how to configure URDF joint parameters for Unity's ArticulationBody physics simulation.

### Parameter Mapping Overview

| URDF Element | Attribute | Unity Property | Description |
|--------------|-----------|----------------|-------------|
| `<dynamics>` | `damping` | `linearDamping` / `angularDamping` | Global motion resistance (like air drag) |
| `<dynamics>` | `friction` | `jointFriction` | Static friction at the joint |
| `<limit>` | `effort` | `xDrive.forceLimit` | Maximum force/torque |
| `<limit>` | `velocity` | `maxAngularVelocity` / `maxLinearVelocity` | Maximum joint velocity |
| `<limit>` | `lower` / `upper` | `xDrive.lowerLimit` / `xDrive.upperLimit` | Position limits |
| `<drive>` | `stiffness` | `xDrive.stiffness` | PD controller P gain (Unity extension) |
| `<drive>` | `damping` | `xDrive.damping` | PD controller D gain (Unity extension) |

### Understanding the Two Types of Damping

Unity's ArticulationBody has two different damping concepts that serve different purposes:

#### 1. Body Damping (`linearDamping` / `angularDamping`)
- **Source:** `<dynamics damping="..."/>`
- **Purpose:** Global resistance to motion, similar to air drag or viscous friction
- **Effect:** Slows down the entire body's motion in world space
- **Use case:** Simulating environmental resistance

#### 2. Drive Damping (`xDrive.damping`)
- **Source:** `<drive damping="..."/>`
- **Purpose:** Part of the PD controller for joint position/velocity control
- **Effect:** Controls how the joint motor reaches its target
- **Formula:** `Effect = stiffness * (targetPos - currentPos) + damping * (targetVel - currentVel)`
- **Use case:** Motor control, position holding

### Example URDF Configuration

```xml
<joint name="wheel_joint" type="continuous">
  <parent link="base_link"/>
  <child link="wheel_link"/>
  <axis xyz="0 1 0"/>

  <!-- Standard URDF elements -->
  <dynamics damping="0.5" friction="0.1"/>
  <limit effort="100" velocity="10"/>

  <!-- Unity extension: PD controller settings for xDrive -->
  <drive stiffness="1000" damping="100"/>
</joint>
```

### Parameter Guidelines

#### For Position-Controlled Joints (e.g., robot arms)
```xml
<dynamics damping="0.1" friction="0.05"/>
<limit effort="1000" velocity="3.14"/>
<drive stiffness="10000" damping="1000"/>
```
- High `stiffness`: Strong position holding
- Moderate `damping`: Smooth motion without oscillation

#### For Velocity-Controlled Joints (e.g., wheels)
```xml
<dynamics damping="0.5" friction="0.1"/>
<limit effort="100" velocity="20"/>
<drive stiffness="0" damping="100"/>
```
- Zero `stiffness`: No position target
- Non-zero `damping`: Velocity control

#### For Free-Moving Joints (e.g., passive casters)
```xml
<dynamics damping="0.01" friction="0.001"/>
<limit effort="0" velocity="100"/>
<!-- No <drive> element needed -->
```

### Notes

- The `<drive>` element is a **Unity-specific extension** and is not part of the standard URDF specification.
- If `<drive>` is not specified, `xDrive.stiffness` and `xDrive.damping` will remain at their default values (typically 0).
- The `<limit effort="...">` value is also used for `xDrive.forceLimit`.

## ROS#

Special thanks to the Siemens [ROS# Project Team]( https://github.com/siemens/ros-sharp/wiki/Info_Acknowledgements) for driving the ROS-Unity Integration Open Source since 2017.

This repo is a fork of [ROS#](https://github.com/siemens/ros-sharp) and was a great starting point for our development.

## Community and Feedback

The Unity Robotics projects are open-source and we encourage and welcome contributions. 
If you wish to contribute, be sure to review our [contribution guidelines](CONTRIBUTING.md) 
and [code of conduct](CODE_OF_CONDUCT.md).

## Support
For questions or discussions about Unity Robotics package installations or how to best set up and integrate your robotics projects, please create a new thread on the [Unity Robotics forum](https://forum.unity.com/forums/robotics.623/) and make sure to include as much detail as possible.

For feature requests, bugs, or other issues, please file a [GitHub issue](https://github.com/Unity-Technologies/URDF-Importer/issues) using the provided templates and the Robotics team will investigate as soon as possible.

For any other questions or feedback, connect directly with the 
Robotics team at [unity-robotics@unity3d.com](mailto:unity-robotics@unity3d.com).

## License
[Apache License 2.0](LICENSE)
