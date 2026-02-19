using Avalonia3D.Animation;
using Avalonia3D.Model;
using System.Numerics;
using System.Collections.Generic;

namespace Avalonia3D.Sandbox.Services;

public interface IAnimationRuntimeController
{
    IReadOnlyList<string> GetAvailableClips();

    ClipPlaybackState GetClipState(string clipName);

    void PlayClip(string clipName, bool loop, float speed);

    void PauseClip(string clipName);

    void StopClip(string clipName);

    int RotateCar2Wheels(float radians);

    bool TrySetCar2RootPositionDelta(Vector3 delta);

    bool TrySetCar2RootYaw(float radians);

    int ResetCar2Pose();

    void CaptureCar2Pose();
}
