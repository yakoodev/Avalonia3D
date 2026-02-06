using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Avalonia3D.Sandbox.Services;

public static class SceneCameraFramer
{
    public static bool TryFrame(SceneGraph sceneGraph, Camera camera, float minDistance = 3f)
    {
        if (sceneGraph == null || camera == null)
        {
            return false;
        }

        var hasBounds = TryComputeWorldBounds(sceneGraph.RootObjects, out var min, out var max);
        if (!hasBounds)
        {
            return false;
        }

        var center = (min + max) * 0.5f;
        var extent = (max - min) * 0.5f;
        var radius = MathF.Max(extent.Length(), 0.5f);

        var halfFov = MathF.Max(camera.Fov * 0.5f, 0.1f);
        var fitDistance = radius / MathF.Tan(halfFov);

        var requiredDistance = MathF.Max(fitDistance * 1.35f, minDistance);
        Camera.DefaultDistance = MathF.Max(Camera.DefaultDistance, requiredDistance * 2f);

        camera.Target = center;
        camera.Distance = requiredDistance;
        camera.Near = MathF.Max(0.01f, camera.Distance * 0.02f);
        camera.Far = MathF.Max(camera.Distance + radius * 4f, camera.Distance * 3f);
        return true;
    }

    private static bool TryComputeWorldBounds(IReadOnlyList<SceneObject> roots, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var hasBounds = false;

        foreach (var root in roots)
        {
            AccumulateBounds(root, ref min, ref max, ref hasBounds);
        }

        return hasBounds;
    }

    private static void AccumulateBounds(SceneObject sceneObject, ref Vector3 min, ref Vector3 max, ref bool hasBounds)
    {
        if (sceneObject is MeshGroup group)
        {
            foreach (var child in group)
            {
                AccumulateBounds(child, ref min, ref max, ref hasBounds);
            }

            return;
        }

        if (sceneObject is not MeshObject mesh || !mesh.HasGeometryBounds)
        {
            return;
        }

        var matrix = mesh.CreateModelMatrix();
        foreach (var corner in GetCorners(mesh.LocalBoundsMin, mesh.LocalBoundsMax))
        {
            var world = Vector3.Transform(corner, matrix);
            min = Vector3.Min(min, world);
            max = Vector3.Max(max, world);
            hasBounds = true;
        }
    }

    private static IEnumerable<Vector3> GetCorners(Vector3 min, Vector3 max)
    {
        yield return new Vector3(min.X, min.Y, min.Z);
        yield return new Vector3(max.X, min.Y, min.Z);
        yield return new Vector3(min.X, max.Y, min.Z);
        yield return new Vector3(max.X, max.Y, min.Z);
        yield return new Vector3(min.X, min.Y, max.Z);
        yield return new Vector3(max.X, min.Y, max.Z);
        yield return new Vector3(min.X, max.Y, max.Z);
        yield return new Vector3(max.X, max.Y, max.Z);
    }
}
