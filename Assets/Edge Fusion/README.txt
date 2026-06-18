***************************************
*         E D G E    F U S I O N      *
* Created by Kronnect Technologies SL * 
*             README FILE             *
***************************************


Quick help: how to use this asset?
----------------------------------

Edge Fusion is a post-processing effect for Unity's Universal Render Pipeline (URP) that seamlessly blends edges between objects to eliminate harsh transitions and create smooth, natural-looking scenes.

1) Basic Setup:
  - Ensure your project uses Universal Render Pipeline (URP)
  - Add the Edge Fusion Render Feature to your URP Renderer
  - Create a Global Volume or add Edge Fusion to an existing Volume in your scene

2) Configuring Global Edge Fusion Settings:
  - Select your Volume and add the "Kronnect/Edge Fusion" component
  - Optionally set blend layers to specify which objects should participate in edge fusion
  - Adjust intensity and radius to control the strength and reach of the blending effect
  - Use quality presets to balance performance and visual quality

3) Per-Object Customization:
  - Add the "Edge Fusion Object" component to specific GameObjects for custom settings
  - Set a custom blend radius (0 disables blending for that object)
  - Choose whether to include children objects in the blending effect

4) Advanced Options:
  - Enable "Intra-Object Fusion" to blend edges within the same object based on normal/depth discontinuities
  - Use noise parameters to add natural variation to the blending
  - Enable jitter for temporal anti-aliasing of the effect
  - Use debug modes to visualize different aspects of the edge detection process

5) Performance Optimization:
  - Adjust sample count using quality presets (Very Low to Very High)
  - Set maximum blend distance to limit effect range
  - Use binary search steps for more accurate edge detection
  - Enable Jitter to produce smoother results or disable to improve performance


Help & Support Forum
--------------------

Check the Documentation folder for detailed instructions:

Have any question or issue?
* Support-Web: https://kronnect.com/support
* Support-Discord: https://discord.gg/EH2GMaM
* Email: contact@kronnect.com
* Twitter: @Kronnect

If you like Edge Fusion, please rate it on the Asset Store. It encourages us to keep improving it! Thanks!



Universal Rendering Pipeline
----------------------------

Edge Fusion is designed specifically for Unity's Universal Render Pipeline (URP) on Unity 6.0 or higher.


Future updates
--------------

All our assets follow an incremental development process by which a few beta releases are published on our support forum (kronnect.com).
We encourage you to signup and engage our forum. The forum is the primary support and feature discussions medium.

Of course, all updates of Edge Fusion will be eventually available on the Asset Store.



More Cool Assets!
-----------------
Check out our other assets here:
https://assetstore.unity.com/publishers/15018



Version history
---------------

Version 3.3.1
- Edge Fusion now explicitly excludes overlay cameras
- [Fix] Skybox and transparents no longer disappear when Edge Fusion runs on a Base camera with stacked Overlay cameras (Forward/Forward+)

Version 3.3
- Added "Anti-Flicker" option. Stabilises the blending when two edge candidates are near-tie in distance.
- Shader variants optimization
- [Fix] Fixes Single Pass Stereo rendering (but no MSAA support yet)

Version 3.2
- Added support for Unity 6.4

Version 3.1.3
- Added "Distance Compensation" option. Scales the effective radius with distance from camera, keeping blending smooth up close while increasing reach for distant objects.
- Internal improvements and fixes

Version 3.1.2
- [Fix] Reflection probes rendering magenta on Mac/Metal - effect now skips non-game cameras

Version 3.1.1
- Minor inspector changes and internal fixes

Version 3.1
- Added option for Intra-Object Fusion so it only applies to objects that contain the Edge Fusion Object script
- Added option to Edge Fusion Object script to disallow intra-object fusion per object
- Added DOTS support
- Fixes

Version 3.0.3
- Special group can now be blended even with blend layers set to nothing
- [Fix] Fixed an issue when excluding objects by overriding the radius to zero

Version 3.0
- Added "Exclusion Pairs" option. Exclude blending between specific objects with custom Ids.
- Added "Blend With Others" option. Defaults to true, can be used to prevent selected layers to blend with anything else.

Version 2.4
- Added "Cameras Layer Filter" setting to the rendere feature. Useful to restrict the effect to certain cameras.

Version 2.3
- Improved object-id hash generation
- Added support for the new MeshLOD system

Version 2.2
- Edge Fusion Object: new options for object-id generation
- Blend layers: increased flexibility when combining layers of different sections
- Added MSAA Edge Fix option under Debug section (only useful in forward and if MSAA is needed)

Version 2.1
- Intra-object fusion improvements

Version 2.0
- Added "Compare Mode" option
- Added option to set custom object id to the Edge Fusion Object component
- Internal improvements

Version 1.5
- Improved falloff when "Max Screen Radius" is in effect
- Added custom selectors: default objects, double-sided selectors and special group selectors with custom rendering layer filters
- Added "Rendering Layer Mask" filter to object selection settings

Version 1.4.1
- Added support for orthographic camera

Version 1.4
- Added "Double Sided Layers" option. Useful to compute object id on double-sided objects.

Version 1.3
- Additional inspector tips and checks

Version 1.2
- Quality improvements
- Added "Early Exit Hits" optimization parameter 
- Added "Concavity Test" option to intra-object fusion method

Version 1.1
- Added DOTs support
- Added noise contrast option
- [Fix] Fixed a display issue with blend layers parameter in the inspector on Unity 6.2

Version 1.0 - Oct/2025
- Initial release
