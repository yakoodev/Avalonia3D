using System;
using System.Collections.Generic;
using System.Numerics;

namespace Avalonia3D.Model
{
    public class SceneNode
    {
        private readonly List<SceneNode> _children = [];

        public string? Name { get; set; }
        public string? StableId { get; set; }
        public string? ExternalId { get; set; }
        public string? SemanticId { get; set; }
        public Vector3 Position { get; set; } = Vector3.Zero;
        public Quaternion Rotation { get; set; } = Quaternion.Identity;
        public Vector3 Scale { get; set; } = Vector3.One;
        public float[] MorphWeights { get; set; } = [];
        public SceneNode? Parent { get; private set; }
        public IReadOnlyList<SceneNode> Children => _children;

        public void AddChild(SceneNode child)
        {
            if (child == null || child == this)
            {
                return;
            }

            if (child.Parent != null)
            {
                child.Parent.RemoveChild(child);
            }

            child.Parent = this;
            _children.Add(child);
        }

        public bool RemoveChild(SceneNode child)
        {
            if (child == null)
            {
                return false;
            }

            if (_children.Remove(child))
            {
                child.Parent = null;
                return true;
            }

            return false;
        }

        public void ClearChildren()
        {
            foreach (var child in _children)
            {
                child.Parent = null;
            }

            _children.Clear();
        }

        public SceneNode? FindByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (string.Equals(Name, name, StringComparison.Ordinal))
            {
                return this;
            }

            foreach (var child in _children)
            {
                var match = child.FindByName(name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        public Matrix4x4 CreateModelMatrix()
        {
            var parentMatrix = Parent?.CreateModelMatrix() ?? Matrix4x4.Identity;

            return Matrix4x4.CreateScale(Scale)
                * Matrix4x4.CreateFromQuaternion(Rotation)
                * Matrix4x4.CreateTranslation(Position)
                * parentMatrix;
        }

        public string GetPath()
        {
            var segments = new Stack<string>();
            var current = this;

            while (current != null)
            {
                segments.Push(string.IsNullOrWhiteSpace(current.Name) ? "$node" : current.Name!);
                current = current.Parent;
            }

            return string.Join('/', segments);
        }
    }
}
