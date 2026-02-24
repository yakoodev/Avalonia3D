using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;

namespace Avalonia3D.Model
{
    public class Camera : INotifyPropertyChanged
    {
        private const float AbsoluteMinDistance = 0.05f;
        private Vector3 _position = new(0, 0, 5);
        private Vector3 _target = Vector3.Zero;
        private float _distance = 15.0f;
        private float _pitch = 0.0f;
        private float _yaw = 0.0f;

        public Matrix4x4 Projection { get => Matrix4x4.CreatePerspectiveFieldOfView(Fov, Width / (float)Height, Near, Far); }
        public Matrix4x4 View { get => Matrix4x4.CreateLookAt(Position, Target, Vector3.UnitY); }       
        public int Height { get; set; }
        public int Width { get; set; }
        public float Near { get; set; }
        public float Far { get; set; }
        public float Fov { get; set; }
        public static float DefaultDistance { get; set; } = 150;
        public float? MinDistance { get; set; }
        public float? MaxDistance { get; set; }
        public float RotationSensitivity { get; set; } = 0.01f;
        public float PanSensitivity { get; set; } = 0.01f;

        public float Pitch
        {
            get => _pitch;
            set
            {
                _pitch = Math.Clamp(value, -MathF.PI / 2 + 0.1f, MathF.PI / 2 - 0.1f);
                UpdatePosition();
            }
        }

        public float Yaw
        {
            get => _yaw;
            set
            {
                _yaw = value;
                UpdatePosition();
            }
        }

        [Category("Camera")]
        public Vector3 Position
        {
            get => _position;
            private set
            {
                _position = value;
                OnPositionChanged?.Invoke();
            }
        }

        [Category("Camera")]
        public Vector3 Target
        {
            get => _target;
            set
            {
                _target = value;
                OnTargetChanged?.Invoke();
                UpdatePosition();
            }
        }

        [Category("Camera")]
        public float Distance
        {
            get => _distance;
            set
            {
                var sanitized = float.IsFinite(value) ? value : _distance;

                if (MinDistance.HasValue)
                {
                    sanitized = Math.Max(sanitized, MinDistance.Value);
                }

                if (MaxDistance.HasValue)
                {
                    sanitized = Math.Min(sanitized, MaxDistance.Value);
                }

                sanitized = Math.Max(sanitized, AbsoluteMinDistance);

                _distance = sanitized;
                OnDistanceChanged?.Invoke();
                UpdatePosition();
            }
        }

        public Action? OnPositionChanged;
        public Action? OnTargetChanged;
        public Action? OnDistanceChanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Camera()
        {
            UpdatePosition();
        }

        /// <summary>
        /// Пересчитывает позицию камеры на основе yaw/pitch/distance.
        /// </summary>
        public void UpdatePosition()
        {
            Position = new Vector3
            {
                X = Target.X + Distance * MathF.Cos(Pitch) * MathF.Sin(Yaw),
                Y = Target.Y + Distance * MathF.Sin(Pitch),
                Z = Target.Z + Distance * MathF.Cos(Pitch) * MathF.Cos(Yaw)
            };
        }

        /// <summary>
        /// Вращение камеры вокруг цели.
        /// </summary>
        public void Rotate(Avalonia.Vector delta)
        {
            Yaw -= (float)delta.X * RotationSensitivity;
            Pitch += (float)delta.Y * RotationSensitivity;
        }

        /// <summary>
        /// Панорамирование камеры (сдвиг цели).
        /// </summary>
        public void Pan(Avalonia.Vector delta)
        {
            var cameraDirection = Vector3.Normalize(Position - Target);
            var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, cameraDirection));
            var up = Vector3.Normalize(Vector3.Cross(cameraDirection, right));

            Target -= right * (float)delta.X * PanSensitivity;
            Target += up * (float)delta.Y * PanSensitivity;
        }
    }
}
