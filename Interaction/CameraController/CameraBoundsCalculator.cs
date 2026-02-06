using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using System.Collections.Generic;
using System.Numerics;

namespace Avalonia3D.Interaction.CameraController;

public static class CameraBoundsCalculator
{
    public static bool TryComputeWorldBounds(SceneGraph sceneGraph, out Vector3 min, out Vector3 max)
    {
        if (sceneGraph == null)
        {
            min = Vector3.Zero;
            max = Vector3.Zero;
            return false;
        }

        return TryComputeWorldBounds(sceneGraph.RootObjects, out min, out max);
    }

    public static bool TryComputeWorldBounds(IReadOnlyList<SceneObject> roots, out Vector3 min, out Vector3 max)
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
