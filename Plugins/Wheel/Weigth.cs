using Avalonia3D.Controls2D;
using Avalonia3D.Helpers;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using SkiaSharp;
using System.Collections.Generic;
using System.Numerics;

namespace Avalonia3D.Plugins.Wheel
{
    public class InsideWeigth : Weigth
    {
        public InsideWeigth(Wheel parent) : base(parent)
        {
            Plane = Plane.Left;
        }

        public override float Value { get => (float)Parent.InsideWeigth.Magnitude; }
        public override float Angle { get => (float)Parent.InsideWeigth.Phase; }
    }

    public class OutsideWeigth : Weigth
    {
        public override float Value { get => (float)Parent.OutsideWeigth.Magnitude; }
        public override float Angle { get => (float)Parent.OutsideWeigth.Phase; }
        public OutsideWeigth(Wheel parent) : base(parent)
        {
            Plane = Plane.Right;
        }       
    }

    public enum Plane
    {
        Left,
        Right
    }

    public class Weigth : MeshGroup
    {
        protected Wheel Parent;
        protected CalloutBox CalloutBox;
        public override bool IsVisible
        {
            get => base.IsVisible && IsActive;
            set => base.IsVisible = value;
        }
        public Plane Plane { get; set; }        
        public virtual float Value { get; }
        public virtual float Angle { get; }
        public virtual bool IsActive { get; set; } = true;

        public Vector3 ExtraAxis = Vector3.UnitX; // ось

        public Weigth(Wheel scene) : base(null)
        {
            Init(scene);
        }

        public Weigth(Wheel scene, IEnumerable<MeshObject> meshObjects) : base(meshObjects)
        {
            Init(scene);
        }

        public override Matrix4x4 CreateModelMatrix()
        {
            var pMatrix = Matrix4x4.Identity;

            if (Parent != null)
                pMatrix = Parent.CreateModelMatrix();

            var axisRotation = Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(ExtraAxis), Angle);

            return Matrix4x4.CreateScale(Scale)
                 * Matrix4x4.CreateFromQuaternion(Rotation)
                 * axisRotation
                 * Matrix4x4.CreateTranslation(Position) 
                 * pMatrix;
        }

        private void Init(Wheel parent)
        {
            Parent = parent;            
            CalloutBox = new CalloutBox
            {
                CornerRadius = 15,
                BorderColor = SKColors.WhiteSmoke,
                BorderWidth = 3,
                FillColor = SKColors.LightBlue,
                Text = "Привет, мир!",
                ArrowSize = 20
            };
        }

        

        public virtual void RenderSurface(Scene3D scene, SKCanvas canvas, int width, int height)
        {
            var camera = scene.Camera;

            // Пример рисования текста
            using var paint = new SKPaint
            {
                Color = SKColors.Red,
                IsAntialias = true
            };

            foreach (var model in this)
            {
                if (!CalloutBox.IsVisible) continue;
                var modelMatrix = model.CreateModelMatrix();
                var screen = MathHelper.ProjectToScreen(model.Gravity, modelMatrix, camera.View, camera.Projection, width, height);
                var target = new SKPoint(screen.X, screen.Y);
                CalloutBox.ArrowTarget = target;
                CalloutBox.CreateRectFromCenter(new SKPoint(target.X, target.Y - 100), 150, 50);
                CalloutBox.Text = Value.ToString();
                CalloutBox.Draw(canvas);
            }
        }
    }
}
