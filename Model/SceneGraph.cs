using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia3D.Model.StandObjects;

namespace Avalonia3D.Model
{
    public class SceneGraph
    {
        private readonly List<SceneObject> _rootObjects = [];

        public SceneGraph()
        {
            Root = new SceneNode { Name = "Root", StableId = "Root", SemanticId = "Root" };
        }

        public SceneNode Root { get; }

        public IReadOnlyList<SceneObject> RootObjects => _rootObjects;

        public void AddRoot(SceneObject obj)
        {
            if (obj == null)
            {
                return;
            }

            Root.AddChild(obj.Node);
            if (!_rootObjects.Contains(obj))
            {
                _rootObjects.Add(obj);
            }
        }

        public void RemoveRoot(SceneObject obj)
        {
            if (obj == null)
            {
                return;
            }

            Root.RemoveChild(obj.Node);
            _rootObjects.Remove(obj);
        }

        public void Clear()
        {
            foreach (var obj in _rootObjects)
            {
                Root.RemoveChild(obj.Node);
            }

            _rootObjects.Clear();
            Root.ClearChildren();
        }

        public SceneNode? FindByName(string name)
        {
            return FindNode(name);
        }

        public SceneNode? FindNode(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return FindNodes(node => string.Equals(node.Name, name, StringComparison.Ordinal)).FirstOrDefault();
        }

        public IEnumerable<SceneNode> EnumerateNodes()
        {
            return TraverseNodes(Root);
        }

        public IEnumerable<SceneNode> FindNodesByNameContains(
            string value,
            StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (var node in TraverseNodes(Root))
            {
                if (!string.IsNullOrEmpty(node.Name) && node.Name.Contains(value, comparison))
                {
                    yield return node;
                }
            }
        }

        public IEnumerable<SceneNode> FindNodesByNameStartsWith(
            string value,
            StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (var node in TraverseNodes(Root))
            {
                if (!string.IsNullOrEmpty(node.Name) && node.Name.StartsWith(value, comparison))
                {
                    yield return node;
                }
            }
        }

        public IEnumerable<SceneNode> FindNodes(Func<SceneNode, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            foreach (var node in TraverseNodes(Root))
            {
                if (predicate(node))
                {
                    yield return node;
                }
            }
        }


        public SceneNode? FindNodeBySemanticId(string semanticId)
        {
            return FindNodes(node => string.Equals(node.SemanticId, semanticId, StringComparison.Ordinal)).FirstOrDefault();
        }

        public SceneNode? FindNodeByStableId(string stableId)
        {
            return FindNodes(node => string.Equals(node.StableId, stableId, StringComparison.Ordinal)).FirstOrDefault();
        }

        public SceneNode? FindNodeByPath(string path)
        {
            return FindNodes(node => string.Equals(node.GetPath(), path, StringComparison.Ordinal)).FirstOrDefault();
        }

        public SceneNode? FindNodeByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return FindNodeBySemanticId(key)
                ?? FindNodeByStableId(key)
                ?? FindNodeByPath(key)
                ?? FindNode(key);
        }

        private IEnumerable<SceneNode> TraverseNodes(SceneNode startNode)
        {
            ArgumentNullException.ThrowIfNull(startNode);

            yield return startNode;

            foreach (var child in startNode.Children)
            {
                foreach (var descendant in TraverseNodes(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}
