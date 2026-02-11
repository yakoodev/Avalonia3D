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

        public MeshObject? ResolveByNodeKey(string nodeKey)
        {
            if (string.IsNullOrWhiteSpace(nodeKey))
            {
                return null;
            }

            var node = _sceneGraph.FindNodeByKey(nodeKey);
            if (node == null)
            {
                return null;
            }

            return ResolveByNode(node);
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

        private static MeshObject? ResolveInObjectTree(SceneObject obj, SceneNode targetNode)
        {
            if (obj == null)
            {
                return null;
            }

            if (ReferenceEquals(obj.Node, targetNode))
            {
                return ResolveMaterialHolder(obj);
            }

            if (obj is MeshGroup group)
            {
                foreach (var child in group)
                {
                    var match = ResolveInObjectTree(child, targetNode);
                    if (match != null)
                    {
                        return match;
                    }
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

            if (nodeOwner is MeshGroup group)
            {
                foreach (var child in group)
                {
                    if (child.Material != null)
                    {
                        return child;
                    }
                }
            }

            return null;
        }
    }
}
