using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using System;
using System.Numerics;

namespace Avalonia3D.Model.StandObjects
{
    public abstract class SceneObject : IDisposable
    {
        protected SceneObject()
        {
            Node = new SceneNode();
        }

        public SceneNode Node { get; }
        public string? Name
        {
            get => Node.Name;
            set => Node.Name = value;
        }
        public Vector3 Position
        {
            get => Node.Position;
            set => Node.Position = value;
        }
        // Must be opt-in. Non-zero default causes full-frame emissive bias and bloom washout.
        public Vector3 EmissionColor { get; set; } = Vector3.Zero;
        public Vector3 BaseColor { get; set; } = Vector3.One;
        public float Opacity { get; set; } = 1f;
        public Quaternion Rotation
        {
            get => Node.Rotation;
            set => Node.Rotation = value;
        }
        public Vector3 Scale
        {
            get => Node.Scale;
            set => Node.Scale = value;
        }
        public virtual bool IsVisible { get; set; } = true;
        public Vector3 Gravity { get; protected set; }
        public abstract void Dispose();
        public virtual Matrix4x4 CreateModelMatrix()
        {
            return Node.CreateModelMatrix();
        }

        public abstract void Render(IRenderContext context);        
        
    }
}
