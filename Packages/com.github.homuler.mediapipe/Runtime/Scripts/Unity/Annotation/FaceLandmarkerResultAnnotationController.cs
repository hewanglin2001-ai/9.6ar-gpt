// Copyright (c) 2023 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using UnityEngine;

using Mediapipe.Tasks.Vision.FaceLandmarker;

namespace Mediapipe.Unity
{
  public class FaceLandmarkerResultAnnotationController : AnnotationController<MultiFaceLandmarkListAnnotation>
  {
    [SerializeField] private bool _visualizeZ = false;

    private readonly object _currentTargetLock = new object();
    private FaceLandmarkerResult _currentTarget;

    // Main-thread snapshot for effects. It advances only after annotation transforms
    // have been drawn; consumers must not read mutable native callback results.
    public int ResultVersion { get; private set; }
    public float LastResultTime { get; private set; } = -100f;
    public bool HasFace { get; private set; }
    public bool HasFacePose { get; private set; }
    public Matrix4x4 FacePose { get; private set; } = Matrix4x4.identity;

    public void DrawNow(FaceLandmarkerResult target)
    {
      target.CloneTo(ref _currentTarget);
      SyncNow();
    }

    public void DrawLater(FaceLandmarkerResult target) => UpdateCurrentTarget(target);

    protected void UpdateCurrentTarget(FaceLandmarkerResult newTarget)
    {
      lock (_currentTargetLock)
      {
        newTarget.CloneTo(ref _currentTarget);
        isStale = true;
      }
    }

    protected override void SyncNow()
    {
      lock (_currentTargetLock)
      {
        isStale = false;
        annotation.Draw(_currentTarget.faceLandmarks, _visualizeZ);
        HasFace = _currentTarget.faceLandmarks != null && _currentTarget.faceLandmarks.Count > 0
          && _currentTarget.faceLandmarks[0].landmarks != null
          && _currentTarget.faceLandmarks[0].landmarks.Count >= 468;
        HasFacePose = HasFace && _currentTarget.facialTransformationMatrixes != null
          && _currentTarget.facialTransformationMatrixes.Count > 0;
        if (HasFacePose) { FacePose = _currentTarget.facialTransformationMatrixes[0]; }
        LastResultTime = Time.unscaledTime;
        unchecked { ResultVersion++; }
      }
    }
  }
}
