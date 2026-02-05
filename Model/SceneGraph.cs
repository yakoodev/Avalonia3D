using System.Collections.Generic;
using Avalonia3D.Model.StandObjects;

namespace Avalonia3D.Model
{
    public class SceneGraph
    {
        private readonly List<SceneObject> _rootObjects = [];

        public SceneGraph()
        {
            Root = new SceneNode { Name = "Root" };
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
    }
}
