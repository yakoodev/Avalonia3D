using System;
using System.Collections.Generic;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;

namespace Avalonia3D.Animation
{
    public sealed class AnimationMaterialTargetResolver
    {
        private readonly SceneGraph _sceneGraph;

        public AnimationMaterialTargetResolver(SceneGraph sceneGraph)
        {
            _sceneGraph = sceneGraph ?? throw new ArgumentNullException(nameof(sceneGraph));
        }

        public IReadOnlyList<MeshObject> ResolveByMaterialKey(string materialKey)
        {
            if (string.IsNullOrWhiteSpace(materialKey))
            {
                return [];
            }

            var result = new List<MeshObject>();
            foreach (var rootObject in _sceneGraph.RootObjects)
            {
                CollectByMaterialKey(rootObject, materialKey, result);
            }

            return result;
        }

        public MeshObject? ResolveByNodeKey(string nodeKey)
        {
            if (string.IsNullOrWhiteSpace(nodeKey))
            {
                return null;
            }

            var node = _sceneGraph.FindNodeByKey(nodeKey);
            return node == null ? null : ResolveByNode(node);
        }

        public MeshObject? ResolveByNode(SceneNode node)
        {
            if (node == null)
            {
                return null;
            }

            foreach (var rootObject in _sceneGraph.RootObjects)
            {
                var match = ResolveInObjectTree(rootObject, node);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void CollectByMaterialKey(SceneObject obj, string materialKey, List<MeshObject> result)
        {
            if (obj is MeshObject mesh && string.Equals(mesh.MaterialKey, materialKey, StringComparison.Ordinal))
            {
                result.Add(mesh);
            }

            if (obj is not MeshGroup group)
            {
                return;
            }

            foreach (var child in group)
            {
                CollectByMaterialKey(child, materialKey, result);
            }
        }

        private static MeshObject? ResolveInObjectTree(SceneObject obj, SceneNode targetNode)
        {
            if (ReferenceEquals(obj.Node, targetNode))
            {
                return ResolveMaterialHolder(obj);
            }

            if (obj is not MeshGroup group)
            {
                return null;
            }

            foreach (var child in group)
            {
                var match = ResolveInObjectTree(child, targetNode);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static MeshObject? ResolveMaterialHolder(SceneObject nodeOwner)
        {
            if (nodeOwner is MeshObject meshObject && meshObject.Material != null)
            {
                return meshObject;
            }

            if (nodeOwner is not MeshGroup group)
            {
                return null;
            }

            foreach (var child in group)
            {
                if (child.Material != null)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
