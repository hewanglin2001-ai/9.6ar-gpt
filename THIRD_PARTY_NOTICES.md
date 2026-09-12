# Third-party notices

## Google MediaPipe canonical face topology

The triangle index data in
`Assets/MediaPipeUnity/Samples/Scenes/Face Landmark Detection/FacelessSurface.cs`
is derived from the canonical face model distributed by Google MediaPipe.
Only its 468-vertex topology (898 triangle index triples) is embedded in this
implementation. The source model's canonical positions, texture coordinates,
and texture assets are not embedded. Additional eye and inner-lip fan triangles
and the Unity rendering implementation were added for this project.

- Upstream project: https://github.com/google-ai-edge/mediapipe
- Source model: https://github.com/google-ai-edge/mediapipe/blob/master/mediapipe/modules/face_geometry/data/canonical_face_model.obj
- Downloaded source model SHA-256: `8bac80443397e113f41a8b565ea72c59390bc031d9defab289dba7bc0c54e618`
- License: Apache License, Version 2.0.
- Full license copy: [ThirdParty/MediaPipe-LICENSE.txt](ThirdParty/MediaPipe-LICENSE.txt)
- Upstream license: https://github.com/google-ai-edge/mediapipe/blob/master/LICENSE

This notice applies to the copied topology used by `FacelessSurface`; it does
not replace notices or licenses accompanying other MediaPipe components,
packages, models, or assets already present in the Unity project.
