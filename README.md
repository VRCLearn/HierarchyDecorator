<h1 align="center">  
 <img width="824" alt="HierarchyDecoratorNew" src="https://user-images.githubusercontent.com/31889435/226486126-009081e1-44de-465c-8ff7-5641870fdcae.png">
 
 Hierarchy Decorator
</h1>

<h4 align="center"> Unity Editor plugin giving the Hierarchy a lick of paint.<br><br>
 
 Fully Customisable.<br>
 Toggle Everything.</h4>

<p align="center">
 <a href="https://unity3d.com/get-unity/download">
 <img src="https://img.shields.io/badge/unity-2018.4%2B-blue.svg" alt="Unity Download Link">
 <a href="https://github.com/VRCLearn/HierarchyDecorator/blob/main/LICENSE.md">
 <img src="https://img.shields.io/badge/License-MIT-brightgreen.svg" alt="License MIT">
</p>
  

<p align="center">
  <a href="#about">About</a> •
  <a href="#installation">Installation</a> •
  <a href="#features">Features</a> •
  <a href="#support">Support</a> •
  <a href="#license-and-credits">License and credits</a>
</p>

## About

This is an independently maintained VRCLearn fork of [WooshiiDev/HierarchyDecorator](https://github.com/WooshiiDev/HierarchyDecorator), originally created by Damian Slocombe (Wooshii).

Hierarchy Decorator is an extension for Unity 2018.4 and higher that extends Unity's hierarchy and takes it to the next level. With headers, component information and other features, it transforms the window into more than a plain list of objects. This can turn scene structures easier to read, understand and provide information on what is going on.

Everything is optional, and can be modified to the requirements of the project.

<p align="center">
<img width="372" alt="Unity_k2yhUSLugm" src="https://user-images.githubusercontent.com/31889435/226486583-8ad71e2b-1051-46b3-b00a-3880acffc413.png">
<img width="372" alt="Unity_myS52drEnl" src="https://user-images.githubusercontent.com/31889435/226486476-768a99ad-ae6f-4609-b8f8-8537c1f2393d.png">
</p>

## Installation

- **VPM (VCC / ALCOMD):** Open the [installation page](https://vrclearn.github.io/HierarchyDecorator/), add the repository, and install **HierarchyDecorator**.
- **UnityPackage:** Import the package from [Releases](https://github.com/VRCLearn/HierarchyDecorator/releases). It installs under `Assets/VRCLearn/HierarchyDecorator`.
- **Git:** In Unity's Package Manager, choose **Add package from git URL** and enter `https://github.com/VRCLearn/HierarchyDecorator.git`.
- **Source:** Download the [Source ZIP](https://github.com/VRCLearn/HierarchyDecorator/archive/refs/heads/main.zip) and copy the `HierarchyDecorator` folder and its `.meta` file into `Assets/VRCLearn/`.

Install only one distribution of this plugin per project.

## Features

### Current

| Feature                    | Hierarchy Decorator  | Other Hierachy Extensions |
| -------------------------- | :----------------: | :-------------:   |
| Hierarchy Headers/Styles   |         ✔️         |        ✔️        |
| Tag/Layer Selector         |         ✔️         |        ✔️        |
| Breadcrumbs                |         ✔️         |        ✔️        |
| Component Icons            |         ✔️         |        ❌        |
| GameObject Icons           |         ✔️         |        ✔️        |

### Planned 

| Feature                    | Hierarchy Decorator  | Other Hierachy Extensions |
| -------------------------- | :----------------: | :-------------:   |
| Folders                    |         Planned     |         ✔️       |
| Script Error/Warning Popup |         Planned     |        ❌       |
| Editor Flags Selector      |         Planned     |        ❌       |
| <a href="https://github.com/WooshiiDev/HierarchyDecorator/issues/25">Team/Individual Settings Mode</a>   |         Planned     |        ❌        |
  
## Settings
  
<p align="center">
 <img align="center" width="929" alt="chrome_hzlst44Z1X" src="https://user-images.githubusercontent.com/31889435/226493547-3ec3db89-bcdf-4412-b000-c90aa9ee30b7.png">
</p>

Open **Tools > HierarchyDecorator > Settings** or **Preferences > HierarchyDecorator** to edit settings. They are saved per project in `UserSettings/Settings.asset`.

New projects use one default layout with Hierarchy guide lines enabled and Tag, Layer, and alternating backgrounds hidden. Use **Tools > HierarchyDecorator > Restore Default Settings…** to restore these defaults.

On first use, Hierarchy Decorator automatically imports any available wooshii settings and removes the detected old installation. Migration runs without a dialog. Errors appear in the Unity Console.
Migration retains the main Hierarchy guide toggle. Line styles, color, and horizontal connectors use the new defaults.

Select a language in settings or **Tools > HierarchyDecorator > Language**. English, Japanese, Korean, Traditional Chinese, and Simplified Chinese are supported; **Auto** follows the operating system language.

### General
  
<details>
 <summary><b>Toggles</b></summary>
  
 <p align="center">
  <img width="915" alt="chrome_aClIcjH3wq" src="https://user-images.githubusercontent.com/31889435/226558578-78287342-711c-4b4b-acf3-18b316f3216b.gif">
  <img width="778" height="145" alt="Unity_GYpPOVFRrv" src="https://github.com/user-attachments/assets/fac34b54-684e-4499-b68b-a572d312bbd2" />
 </p>

  Toggles will simply display the state of the instance, can be clicked to toggle the instance active state.

  ```
  Show Active Toggles     Enable the toggles.
  Active Toggle Type      Choose between a checkbox or dot for the toggle icon.
  Active Swiping          Click and drag over check boxes to toggle them.
  Swipe Same State        Only toggle the instances with the same state as the first selected.
  Swipe Selection Only    If a selection exists, only toggle the selected instances.
  Depth Mode              The accepted criteria for selecting instances when swiping.
  ```

Ordinary clicks operate on the clicked object, including when Active Swiping is
disabled. Shift-click an active toggle on a selected scene object to apply its new state to
the whole selection, including objects outside the visible rows. Mixed states
follow the clicked object's new state. Shift-clicking an unselected object changes
only that object. Both click paths support Undo/Redo and prefab overrides.

</details>

<details>
 <summary><b>Layers</b></summary>

<p align="center">
  <img width="913" alt="chrome_szO7gPHVZ4" src="https://user-images.githubusercontent.com/31889435/226493749-b30ebd4a-bf89-4841-bde8-b78159ec6068.png">
</p>
  
 Display the current layer the instance is assigned to.
  
  ```
  Show Layers             Enable the toggles.
  Click To Select Layer   Clicking the layer label will display a layer dropdown to update it.
  Apply Child Layers      Change the child gameobjects when updating the layer above.
  ```
</details>

<details>
 <summary><b>Breadcrumbs</b></summary>

<p align="center">
   <img width="922" alt="chrome_DtNbO5Mimi" src="https://user-images.githubusercontent.com/31889435/226493794-e45fbb59-ec38-430a-a3ba-2cd137251f46.png">
</p>
  
  Breadcrumbs connect each visible child to its parent. The last visible sibling ends its branch.

  All branches share a color. Direct connections and ancestor lines can each use a solid, dashed, or dotted style.
  
  ```
  Show                    Show the breadcrumbs.
  Color                   The colour of the drawn lines.
  Direct Style            Line style for the branch connecting an object to its parent.
  Ancestor Style          Line style for ancestor branches passing through the current row.
  Display Horizontal      Draw the horizontal connector from a branch to each object.
  ```
</details>
 
### Visual

<details>
 <summary><b>Background</b></summary>
 
<p align="center">
 <img width="612" alt="chrome_Y3lak6Q0Dm" src="https://user-images.githubusercontent.com/31889435/226495114-e578f1c4-60d4-473e-8b42-b2c09c0fdeb1.png">
</p>

  The background can be enabled to alternate background colour between each hierarchy row. 

  ```
  Alternate Background    Show the breadcrumbs.
  Color One               The first colour for the theme.
  Color Two               The second colour for the theme.
  ```
</details>

<details>
 <summary><b>Styles</b></summary>
   
  The Style tab controls the design of the headers and seperators for the hierarchy. Colours are individual for light and dark mode providing accessibility. The **prefix** is the string to specify at the start of an instance name to apply the style.
  
  Layers and icons can be specifically disables on styles instances to remove clutter and information that is not required.

 <p align="center">
  <img width="510" height="450" alt="image" src="https://github.com/user-attachments/assets/1d727086-ea26-4ea9-ad66-26873b39d6b2" alt="Style Settings"/>
 </p>
</details>

  ### Icons

 Click the icon next to a GameObject's name or **Alt**-click its row in the classic Hierarchy to open its
 decoration palette. Pick a row color and an object icon independently.
 Hover over a color or icon to preview it in the Hierarchy before applying it. Hold **Alt** while previewing or
 picking a color to add a lighter tint to visible descendant rows without their own color. The object's own row
 has the same tint strength in either mode. Each object keeps its own color and icon settings.
 The row tint starts at the object's indentation and extends through the component area. Indentation to the left
 shows the enclosing region colors.
 Use **+** in Row Color to register a custom color. The palette stays open so you can click the new swatch to apply it.
 Rows and regions use colors from the palette. Right-click a custom swatch and choose **Remove from Palette**.
 Confirming clears all uses of that color in the project, including in closed scenes and prefabs.
 Use **+** in Object Icon to open Unity's texture picker. Selecting a texture applies it to the current object
 and closes the palette. Project textures are available through the picker whenever you need them.
 The first tile in each group restores the original color or icon independently.
 The icon presets omit Unity's automatic GameObject, Prefab, and scene icons.

 Object icons replace the icon before the GameObject name and use Unity's native Inspector and
 Scene view icon. Unity saves them with the scene or prefab. Row colors are personal project
 settings. For scenes, they persist after the scene is saved in Unity 2019.2 or later. For
 objects edited in Prefab Mode, row colors persist in Unity 2021.2 or later.

 Component icons appear on the right and represent components attached to the GameObject. Use the
 Icons settings tab to choose which components appear or show all eligible components.

 VRChat Avatar Descriptor uses a gray humanoid icon; PhysBone and PhysBone Collider share a diagonal blue bone icon, and Mirror has its own icon. Each icon represents a component attached to that GameObject; it does not indicate referenced roots, affected bones or components generated during an avatar build. These icons use the existing component icon settings and do not require the VRChat SDK to use the rest of the plugin.

 With **Click To Toggle Component** enabled, icons can toggle components that have Unity's Inspector enable checkbox. Clicking a stacked icon sets all eligible components represented by it to the opposite of the first eligible component's state. Components without that checkbox remain unchanged. The stacked icon reflects the first eligible component's state. Toggle changes support Undo/Redo and prefab instance overrides.

<p align="center">
 <img width="132" height="327" alt="Unity_emGzaT8YHM" src="https://user-images.githubusercontent.com/31889435/226554415-8bd0be96-6eb2-4217-8d56-e39d23dd7ffd.png"><img width="627" alt="Unity_iWzNrNwYKa" src="https://user-images.githubusercontent.com/31889435/226494920-6b78be6e-686d-42ac-a11f-629f270cb5bc.png">
</p>

<details>
 <summary><b>Settings</b></summary>
 
 - Enable Icons: Will toggle the icons on.
 - Stack Mono Behaviours - If there are any MonoBehaviour derived components, show only one script icon on the instance.
 - Show Missing Script Warning - Show an indicator if there's an invalid script or "missing" script that cannot find the source file.
 - Component Icon Limit - Set the maximum number of component icons per row after stacking duplicates. Use `0` for no limit. Hover over `+N` to see the hidden components.

</details>

<details>
 <summary><b>Icon Panel</b></summary>
 
**Show All**

Below show all are two labels - Unity & Custom. Both of these can be enabled to automatically show the respective components automatically on all instances.
Unity components refer to built in types, while custom are custom MonoBehaviour's outside of Unity's code base.

**Groups**

Unity components have been categorized into related groups to make it easier to filter through all of them that exist. The search can be used to extend this further.

Any component toggled on in Excluded will disable them completely even if show all Unity components is enabled. This is primarily to make it easier to remove types not required when **Show All** is on.

**Custom**

Custom components are for scripts created in the project, that are not a part of Unity's engine. Here scripts can be grouped together and enabled if **Show All** for custom components is not on. 

Scripts can also be dragged in from the project view and will be added to the group highlighted for easy organisation.

<p align="center">
 <img width="40%" alt="Unity_emGzaT8YHM" src="https://user-images.githubusercontent.com/31889435/226555645-85954060-c25e-4ae8-bf5b-c7313d1188ee.gif">
 <img width="40%" alt="Unity_emGzaT8YHM" src="https://user-images.githubusercontent.com/31889435/226556916-73888fe0-b8fa-4365-88ab-18d786aa7c37.gif">
</p>

</details>

## Support
Please submit questions, bugs, and feature requests to [VRCLearn/HierarchyDecorator issues](https://github.com/VRCLearn/HierarchyDecorator/issues).

For bug fixes, features, documentation, and translations, see the [contribution guidelines](CONTRIBUTING.md).

## License and credits

This fork is maintained by VRCLearn under the [MIT license](LICENSE.md). The original project was created by Damian Slocombe (Wooshii), with contributions from the [original contributors](https://github.com/WooshiiDev/HierarchyDecorator/graphs/contributors).

Copyright (c) 2020-2025 Damian Slocombe
