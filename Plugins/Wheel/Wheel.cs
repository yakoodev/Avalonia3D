using Avalonia3D.Animation;
using Avalonia3D.Helpers;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Model.Workflow;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Avalonia3D.Plugins.Wheel
{
    public enum AnimationMode
    {
        Rotate,
        SchemeWork
    }

    public class Wheel : MeshGroup
    {
        public Scene3D Context { get; private set; }

        private WeightScheme _weightScheme;
        public WeightScheme WeightScheme
        {
            get => _weightScheme;
            set
            {
                _weightScheme = value;
                Update();
            }
        }

        private AnimationMode _animation = AnimationMode.SchemeWork;
        public AnimationMode Animation
        {
            get => _animation; set
            {
                _animation = value;
                Update();
            }
        }      

        private void Update()
        {

            switch (Animation)
            {
                case AnimationMode.Rotate:
                    Context.Animator.TryAdd(new RotationLoopAnimation(this, Vector3.UnitY, 0.5f));
                    break;
                case AnimationMode.SchemeWork:
                    SchemeChange();
                    break;
                default:
                    break;
            }            
        }

        private void SchemeChange()
        {
            var w = Weigths.Where(w => w.IsActive).FirstOrDefault();
            if (w != null)
            {
                switch (w)
                {
                    case GlueWeigthInside gwi:
                        GlueWeigthInside();
                        break;
                    case GlueWeigthOutside gwo:
                        GlueWeigthOutside();
                        break;
                    case SpringWeigthInnerOutside swio:
                        SpringWeigthInnerOutside();
                        break;
                    case SpringWeigthInside swi:
                        SpringWeigthInside();
                        break;
                    case SpringWeigthOutside swo:
                        SpringWeigthOutside();
                        break;
                }
            }
        }

        public Wheel(Scene3D scene3D)
        {
            Context = scene3D;
            Context.LookChanged += SceneLookChanged;
        }

        public override bool IsVisible
        {
            get => base.IsVisible;
            set
            {
                base.IsVisible = value;
                foreach (var w in Weigths)
                    w.IsVisible = value;
            }
        }

        private void SpringWeigthOutside()
        {
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Yaw, (s) => Context.Camera.Yaw = s, 45 * MathHelper.ToRad, 1, Interpolators.LerpFloat));
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Distance, (s) => Context.Camera.Distance = s, Scene3DDefault.DistantionBase, 1, Interpolators.LerpFloat));
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Pitch, (s) => Context.Camera.Pitch = s, Scene3DDefault.PitchBase, 1, Interpolators.LerpFloat));
        }

        private void SpringWeigthInnerOutside()
        {
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Yaw, (s) => Context.Camera.Yaw = s, 70 * MathHelper.ToRad, 1, Interpolators.LerpFloat));
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Pitch, (s) => Context.Camera.Pitch = s, 10 * MathHelper.ToRad, 1, Interpolators.LerpFloat));
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Distance, (s) => Context.Camera.Distance = s, Scene3DDefault.DistantionBase, 1, Interpolators.LerpFloat));
        }

        private void SceneLookChanged(object? sender, Look e)
        {
            if (e == Look.Profile)
                Profile();
            else
                Update();
        }

        private void SpringWeigthInside()
        {
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Distance, (s) => Context.Camera.Distance = s, Scene3DDefault.DistantionBase, 1, Interpolators.LerpFloat));
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Pitch, (s) => Context.Camera.Pitch = s, Scene3DDefault.PitchBase, 1, Interpolators.LerpFloat));
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Yaw, (s) => Context.Camera.Yaw = s, -45 * MathHelper.ToRad, 1, Interpolators.LerpFloat));
        }

        private void GlueWeigthInside()
        {
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Yaw, (s) => Context.Camera.Yaw = s, -70 * MathHelper.ToRad, 1, Interpolators.LerpFloat));
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Pitch, (s) => Context.Camera.Pitch = s, 33 * MathHelper.ToRad, 1, Interpolators.LerpFloat));
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Distance, (s) => Context.Camera.Distance = s, 106, 1, Interpolators.LerpFloat));
        }

        private void GlueWeigthOutside()
        {
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Yaw, (s) => Context.Camera.Yaw = s, -70 * MathHelper.ToRad, 1, Interpolators.LerpFloat));
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Pitch, (s) => Context.Camera.Pitch = s, 33 * MathHelper.ToRad, 1, Interpolators.LerpFloat));
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Distance, (s) => Context.Camera.Distance = s, 72, 1, Interpolators.LerpFloat));
        }

        private void Profile()
        {
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Yaw, (s) => Context.Camera.Yaw = s, 0, 1, Interpolators.LerpFloat));
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Pitch, (s) => Context.Camera.Pitch = s, 0, 1, Interpolators.LerpFloat));
            Context.Animator.TryAdd(new PropertyAnimation<float>(() => Context.Camera.Distance, (s) => Context.Camera.Distance = s, Camera.DefaultDistance, 1, Interpolators.LerpFloat));
        }

        private float _angle;

        public float Angle
        {
            get => _angle;
            set
            {
                _angle = value;
                var axis = Vector3.Normalize(Vector3.UnitX);
                Rotation = Quaternion.CreateFromAxisAngle(axis, (float)(_angle * Math.PI / 180));
            }
        }

        public List<Weigth> Weigths { get; private set; } = [];

        public Complex InsideWeigth { get; set; }
        public Complex OutsideWeigth { get; set; }
    }
}
