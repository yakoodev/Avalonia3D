using System;
using System.Collections.Generic;
using Avalonia3D.Model.StandObjects;

namespace Avalonia3D.Model
{
    public class SceneGraph
    {
        private readonly List<SceneObject> _rootObjects = [];

        public SceneGraph()
        {
            Root = new SceneNode { Name = "Root", StableId = "Root" };
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
            return Root.FindByName(name);
        }

        public SceneNode? FindNode(string name)
        {
            return Root.FindByName(name);
        }

        public SceneNode? FindNodeByStableId(string stableId)
        {
            return FindNodeByPredicate(node => string.Equals(node.StableId, stableId, StringComparison.Ordinal));
        }

        public SceneNode? FindNodeByPath(string path)
        {
            return FindNodeByPredicate(node => string.Equals(node.GetPath(), path, StringComparison.Ordinal));
        }

        public SceneNode? FindNodeByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return FindNodeByStableId(key)
                ?? FindNodeByPath(key)
                ?? FindNode(key);
        }

        private SceneNode? FindNodeByPredicate(Func<SceneNode, bool> predicate)
        {
            return FindNodeByPredicate(Root, predicate);
        }

        private static SceneNode? FindNodeByPredicate(SceneNode node, Func<SceneNode, bool> predicate)
        {
            if (predicate(node))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                var match = FindNodeByPredicate(child, predicate);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
