using System;
using System.Collections.Generic;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;

namespace Avalonia3D.Animation
{
    public sealed class AnimationMorphTargetResolver
    {
        private readonly SceneGraph _sceneGraph;

        public AnimationMorphTargetResolver(SceneGraph sceneGraph)
        {
            _sceneGraph = sceneGraph ?? throw new ArgumentNullException(nameof(sceneGraph));
        }

        public IReadOnlyList<MeshObject> ResolveTargets(string nodeKey)
        {
            if (string.IsNullOrWhiteSpace(nodeKey))
            {
                return [];
            }

            var node = _sceneGraph.FindNodeByKey(nodeKey);
            if (node == null)
            {
                return [];
            }

            foreach (var rootObject in _sceneGraph.RootObjects)
            {
                if (!ContainsNode(rootObject.Node, node))
                {
                    continue;
                }

                var result = new List<MeshObject>();
                CollectMeshObjects(rootObject, node, result);
                return result;
            }

            return [];
        }

        private static bool ContainsNode(SceneNode root, SceneNode target)
        {
            if (ReferenceEquals(root, target)) return true;
            foreach (var c in root.Children)
            {
                if (ContainsNode(c, target)) return true;
            }
            return false;
        }

        private static void CollectMeshObjects(SceneObject obj, SceneNode targetNode, List<MeshObject> result)
        {
            if (ReferenceEquals(obj.Node, targetNode) && obj is MeshObject m && m.SupportsMorphTargets)
            {
                result.Add(m);
            }

            if (obj is MeshGroup group)
            {
                if (ReferenceEquals(group.Node, targetNode))
                {
                    foreach (var child in group)
                    {
                        if (child.SupportsMorphTargets)
                        {
                            result.Add(child);
                        }
                    }
                    return;
                }

                foreach (var child in group)
                {
                    CollectMeshObjects(child, targetNode, result);
                }
            }
        }
    }
}
