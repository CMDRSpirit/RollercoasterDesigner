
# Rollercoaster Designer v1.0
![Title](https://user-images.githubusercontent.com/33422437/214536005-8d2a673f-a1f5-427e-b0a1-c42204592086.png)

A toolset for creating and operating realistic rollercoasters in Unity. Includes a full integration into the Unity Editor.

## What this is not
- A replacement for more professional software, like NoLimits 2. The current physical simulation is very simple, but works for games and other applications.
- It not a standalone coaster editor for games, but it includes one for the Unity Editor to be simulated in standalone mode.
- Apart from example assets, there are no tracks or cars being delivered (for the moment). This is up to you to model your custom cars, tracks and scenery.

## Wiki
The Wiki contains information about how the toolset works and how to build a simple rollercoaster.

## Features
- Spline based track editor
- Custom track and car models (Procedural track generation)
- Block sections for multi train operation
- On track event triggering
- Full integration into Unity (Terrain system, programming, HDRP / URP, ...)
- Superior graphics capabilities compared to other coaster simulators (Baked Lighting, GI, Ray Tracing (HDRP), ...)

## Future work
- Improved physics
- Better spline handling (Position nodes not always 1t apart)
- Different spline types (NURBS, ...)
- Lightmap integration for track pieces (For now use light probes or [Probe Volumes](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@14.0/manual/probevolumes.html))
- Support generation
- ...

## Requirements
- Unity 2022.1+
- Unity.Mathematics 1.2.x+
- Unity.Cinemachine 2.8.x+ (Only used by the example scene)
- HDRP 13+ (Only used by example materials)

Older version should also work, I just didn't test it.

