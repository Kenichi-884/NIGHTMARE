
// Sewer Underground Modular Pack v4.2
// For support and questions : vencious.games@gmail.com

1.	How to setup for different pipelines

	NOTE :	at first when you have the package from the package manager imported, you will only find the "Textures" and "Pipelines" folder available.
			So in order to get the package working in any pipeline make sure to do the followng:
	
	*	Built-In - navigate to folder "Sewerage_Pack/Pipelines" and double click and import the "Built-In" package.
		You have to import the Post-Processing package from the package manager and the "Built-In_PostProcessing" package from the "Sewerage_Pack/Pipelines" folder,
		in order to get the post-processing working in the project and the demo scene.
	*	URP - navigate to folder "Sewerage_Pack/Pipelines" and double click and import the "URP" package.
	*	HDRP - navigate to folder "Sewerage_Pack/Pipelines" and double click and import the "HDRP" package.

2.	Models
	
	*	This package contains 6 sub modules each with a bit different look and design.
		You can use any of them separately to create a separate scene or you can combine them as it's done in the demo scene !
		Most of the assets have separate textures ( materials ) and others are using common materials like concrete.

	*	Contains two "Pipes" modules

3.	Prefas
	
	*	The prefabs included in the different pipeline support packages are setup to use the materials that come with the same 
		support package you use.

4.	Textures
	
	*	In the main package, you will find only the "Textures" folder and a "Pipelines" folder.
		All the texures are shared between all the pipelines.

		NOTE : if you use the package with URP or HDRP, you can safely find and delete all textures in the Textures folder
		that has the suffix "_AO", as the AO is also included into the Green channel of the MaskMap texture for use with URP and HDRP.

	*	All textures are 2048x2048 so every user can decide where to compensate from,
		by compressing down any texture in the import settings !
		All textures are stored in the "Textures" folder and are gouped in sub folders.

	*	Warning signs - there are a few types of signs - vertical, horizonal and triangular
		and each group has its own atlas of textures. The full atlas space wont be used and only
		a few signs will be provided at first. This way you can add your own to it by editing the textres in any image editor !

5.	Lighting

	*	Some of the light prefabs having a light objcet as a child. In the demo scene though, some lights are having different setting
		so they can better lit the scene. Some of the lights in the scene are also setup to use the Mixed lighting mode and are included in the GI baking.
		This is up to the Level Designers and Lighting Artists to decide how to lit up a scene.
		You can go the the ligh prefabs and remove the light object if you don't need the attached to the light models.

	*	GI : light bkaking. Only bigger models are setup to contribute to lightmapping as the small objects are lit by using Light Probes,
		which gives much better result and smaller lightmaps size.

6.	Particle Systems

	*	Particle Systems are using some legacy shader for Built-In pipeline.
		There're custom Shader Graph shaders for URP and HDRP.


7.	Water

	*	Flowing Water planes are using a FlowMaps shader created in Shader Forge for the Built-In pipeline.
		For URP and HDRP there's a FlowMap shader created using Shader Graph based on this tutorial:
		- https://www.youtube.com/watch?v=SA6Y3L-X0Po

		NOTE :	there's a flow map tool out there in internet for creating the maps. You can download it and
				create the flowmaps you want. You can find the tool at: http://teckartist.com/?page_id=107 or use 
				any other way you know for creating flow maps.


8.	Decals
	
	*	Built-In pipeline - decals are not using any special decal shader but rather the standard shader set to "Fade" rendering mode.
		They should be placed a little further than the surfaces and should not overlap with each other.
	*	URP - 
	*	HDRP - decals are using te decal shader that comes with the HDRP package.
		

9.	Occlusion Culling

	*	It is recommended to use occlusion culling for better performance.
		
		There's a disasbles group in the demo scenes for each pipeline "Occlusion_Culling_Helpers".
		Enable the group when you build the occlusion culling for the demo scene.
		Be sure to adjust the wall boxes and the Occlusion Areas in case you make some changes to the demo scene or follow the placement logic
		of occlusion areas and the wall boxes if you create a new scene.


10. Post Processing
	
	*	Build-In pipeline - in order to get the the post processing workin for the built in pipeline,
		first import the Post-Processing package from the package manager and then import the Built-In package from the "Pipelines" folder.

		NOTE : you can find some more post processing profiles for the BUilt-In pipelin in "Sewerage_Pack/Misc/PostProcessingProfiles".
	
	*	URP - the post processing is pre-setup and should be visible right away after importin the assets from the URP support package from the "Pipelines" folder.
	*	HDRP - the post processing is pre-setup and should be visible right away after importin the assets from the HDRP support package from the "Pipelines" folder.