using Avalonia3D.Interfaces;
using System;
using System.Numerics;

namespace Avalonia3D.Model.StandObjects
{
    public abstract class SceneObject : IDisposable
    {
        public string? Name { get; set; }
        public Vector3 Position { get; set; } = Vector3.Zero;
        public Vector3 EmissionColor { get; set; } = Vector3.One;
        public Vector3 BaseColor { get; set; } = Vector3.One;
        public float Opacity { get; set; } = 1f;
        public Quaternion Rotation { get; set; } = Quaternion.Identity;
        public Vector3 Scale { get; set; } = Vector3.One;
        public SceneObject? Parent { get; set; }
        public virtual bool IsVisible { get; set; } = true;
        public Vector3 Gravity { get; protected set; }
        public abstract void Dispose();
        public virtual Matrix4x4 CreateModelMatrix()
        {
            var pMatrix = Matrix4x4.Identity;

            if (Parent != null)
                pMatrix = Parent.CreateModelMatrix();

            return Matrix4x4.CreateScale(Scale)
                 * Matrix4x4.CreateFromQuaternion(Rotation)
                 * Matrix4x4.CreateTranslation(Position) * pMatrix;
        }

        public abstract void Render(IRenderContext context);        
        
    }
}
