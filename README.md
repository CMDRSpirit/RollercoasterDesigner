
# Rollercoaster Designer v1.1
![Title](https://user-images.githubusercontent.com/33422437/228783876-e99047ac-571a-4592-9801-ed93a4c37a73.png)

A toolset for creating and operating realistic rollercoasters in Unity. Includes a full integration into the Unity Editor.

## What this is not
- A replacement for more professional software, like NoLimits 2. The current physical simulation is very simple, but works for games and other applications.
- It not a standalone coaster editor for games, but it includes one for the Unity Editor to be simulated in standalone mode.
- Apart from example assets, there are no tracks or cars being delivered (for the moment). This is up to you to model your custom cars, tracks and scenery.

## Wiki
The [Wiki](https://github.com/CMDRSpirit/RollercoasterDesigner/wiki) contains information about how the toolset works, how to build a simple rollercoaster and importing NoLimits 2 tracks.

## Example Coasters
- Expedition Geforce Recreation (Holiday Park)

Download Location: https://github.com/CMDRSpirit/RollercoasterDesigner/releases

## Features
- Spline based track editor
- Multiple spline types (Cubic, NURBS)
- Importing NoLimits 2 tracks
- Custom track and car models (Procedural track generation)
- Block sections for multi train operation
- On track event triggering
- Train and track audio (WIP)
- Track supports generation (WIP)
- Physics model (Rolling / Air resistance)
- Full integration into Unity (Terrain system, programming, HDRP / URP, ...)
- Superior graphics capabilities compared to other coaster simulators (Baked Lighting, GI, Ray Tracing (HDRP), ...)

## Future work
- Improved physics and audio
- Better spline handling (Position nodes not always 1t apart)
- Lightmap integration for track pieces (For now use light probes or [Probe Volumes](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@14.0/manual/probevolumes.html))
- Track support generation
- ...

## Requirements
- Unity 2022.1+
- Unity.Mathematics 1.2.x+
- Unity.Burst 1.7.4+
- Unity.Cinemachine 2.8.x+ (Only used by the example scene)
- HDRP 13+ (Only used by example materials)
- [Unity NURBS](https://github.com/komietty/unity-nurbs) (Already bundled in `Libraries` folder)

Older versions should also work, I just didn't test it.

## Changelog
### v1.1
Additions:
- NURBS splines
- NoLimits 2 track importing
- Train and track audio
- Track supports generation
- Needs [Unity NURBS](https://github.com/komietty/unity-nurbs) from now on

Improvements:
- Editor performance 
- Automatic generation of sensor events for block sections
- Changed definition of trains using attached scripts instead of structs
