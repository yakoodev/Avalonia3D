using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia3D.Model.Workflow;
using Avalonia3D.Plugins.Wheel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Avalonia3D.Controls
{
    [DesignTimeVisible(true)]
    public class WheelPresenter3D : Grid
    {
        #region Переменные
        private readonly IControl3D _model3DControl;
        private readonly Image _inputOverlay;

        // Для отслеживания мультитач-событий
        private readonly Dictionary<int, Point> _activePointers = [];
        private Point _prevCenter;
        private double _prevDistance;
        private bool _ignoreSingleFinger; // Флаг для игнорирования одиночного пальца после мультитач
        private bool _isTwoFingerGesture; // Флаг для отслеживания активного жеста двумя пальцами
        #endregion
        public WheelPresenter3D()
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _model3DControl = new Model3DControl
                {
                    [RowProperty] = 0,
                    [ColumnProperty] = 0,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
                };
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _model3DControl = new Model3DEmbeddedControl
                {
                    [RowProperty] = 0,
                    [ColumnProperty] = 0,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
                };
            }
            Children.Add((Control)_model3DControl);

            _inputOverlay = new Image
            {                
                IsHitTestVisible = true,
                [RowProperty] = 0,
                [ColumnProperty] = 0,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
            };

            _model3DControl.FrameReady += _model3DControl_FrameReady;

            _inputOverlay.PointerPressed += OnPointerPressed;
            _inputOverlay.PointerReleased += OnPointerReleased;
            _inputOverlay.PointerMoved += OnPointerMoved;
            _inputOverlay.PointerCaptureLost += OnPointerCaptureLost;
            _inputOverlay.PointerWheelChanged += OnPointerWheelChanged;

            Children.Add(_inputOverlay);
            SizeChanged += OnSizeChanged;
        }

        private void _model3DControl_FrameReady(WriteableBitmap writeable)
        {
            _inputOverlay.Source = writeable;
            _inputOverlay.InvalidateVisual();
            InvalidateVisual();          
        } 

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            _activePointers.Clear();
            _inputOverlay.PointerPressed -= OnPointerPressed;
            _inputOverlay.PointerReleased -= OnPointerReleased;
            _inputOverlay.PointerMoved -= OnPointerMoved;
            _inputOverlay.PointerCaptureLost -= OnPointerCaptureLost;
            _inputOverlay.PointerWheelChanged -= OnPointerWheelChanged;
            SizeChanged -= OnSizeChanged;
            base.OnUnloaded(e);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
           // _model3DControl.RequestNextFrameRendering();
        }

        #region Вспомогательные методы для жестов
        private Point GetCenterPoint(IEnumerable<Point> points)
        {
            double x = 0, y = 0;
            int count = 0;

            foreach (var p in points)
            {
                x += p.X;
                y += p.Y;
                count++;
            }

            return count > 0 ? new Point(x / count, y / count) : new Point();
        }

        private double GetDistance(Point a, Point b)
        {
            return Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
        }

        // Сбрасываем состояние жеста двумя пальцами
        private void ResetTwoFingerGesture()
        {
            _prevDistance = 0;
            _prevCenter = new Point();
            _isTwoFingerGesture = false;
        }
        #endregion

        #region Проксируемые свойства      

        private WheelSceneModule? WheelModule => _model3DControl.Scene.GetModule<WheelSceneModule>();

       
        [Category("Interaction")]
        public float RotationSensitivity
        {
            get => _model3DControl.RotationSensitivity;
            set => _model3DControl.RotationSensitivity = value;
        }

        [Category("Interaction")]
        public float PanSensitivity
        {
            get => _model3DControl.PanSensitivity;
            set => _model3DControl.PanSensitivity = value;
        }

        [Category("Interaction")]
        public float WheelAngle
        {
            get => WheelModule?.Wheel?.Angle ?? 0f;
            set
            {
                if (WheelModule?.Wheel != null)
                {
                    WheelModule.Wheel.Angle = value;
                }
            }
        }
        

        [Category("Interaction")]        
        public Look LookState
        {
            get => _model3DControl.Scene.LookState;
            set => _model3DControl.Scene.LookState = value;
        }

        [Category("Interaction")]
        public Complex InsideWeigth
        {
            get => WheelModule?.Wheel?.InsideWeigth ?? default;
            set
            {
                if (WheelModule?.Wheel != null)
                {
                    WheelModule.Wheel.InsideWeigth = value;
                }
            }
        }

        [Category("Interaction")]
        public Complex OutsideWeigth
        {
            get => WheelModule?.Wheel?.OutsideWeigth ?? default;
            set
            {
                if (WheelModule?.Wheel != null)
                {
                    WheelModule.Wheel.OutsideWeigth = value;
                }
            }
        }

        [Category("Interaction")]
        public WeightScheme WeightScheme
        {
            get => WheelModule?.Wheel?.WeightScheme ?? default;
            set
            {
                if (WheelModule?.Wheel != null)
                {
                    WheelModule.Wheel.WeightScheme = value;
                }
            }
        }

        #endregion

        #region Обработчики событий
        private void OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            _activePointers[point.Pointer.Id] = point.Position;
            e.Pointer.Capture(_inputOverlay);

            // Сбрасываем состояние при каждом новом касании
            if (_activePointers.Count == 1)
            {
                _ignoreSingleFinger = false;
                ResetTwoFingerGesture();
            }
            // Сбрасываем состояние жеста двумя пальцами при начале нового жеста
            else if (_activePointers.Count == 2)
            {
                ResetTwoFingerGesture();
            }

            // Для touch эмулируем левую кнопку мыши
            if (e.Pointer.Type != PointerType.Mouse)
            {
                var args = new PointerPressedEventArgs(
                    e.Source,
                    e.Pointer,
                    _inputOverlay,
                    point.Position,
                    e.Timestamp,
                    new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed),
                    e.KeyModifiers
                );
                _model3DControl.HandlePointerPressed(args);
            }
            else // Для мыши используем реальные свойства
            {
                _model3DControl.HandlePointerPressed(e);
            }
            e.Handled = true;
        }

        private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            int countBefore = _activePointers.Count;
            bool wasMultiTouch = countBefore >= 2;

            if (_activePointers.ContainsKey(e.Pointer.Id))
            {
                _activePointers.Remove(e.Pointer.Id);
            }

            // Сбрасываем состояние жеста при полном отпускании
            if (_activePointers.Count == 0)
            {
                ResetTwoFingerGesture();
                _ignoreSingleFinger = false;
            }
            // Установка флага игнорирования одиночного пальца
            else if (wasMultiTouch && _activePointers.Count == 1)
            {
                _ignoreSingleFinger = true;
                ResetTwoFingerGesture();
            }

            // Для touch эмулируем левую кнопку мыши
            if (e.Pointer.Type != PointerType.Mouse)
            {
                var point = e.GetCurrentPoint(this);
                var args = new PointerReleasedEventArgs(
                    e.Source,
                    e.Pointer,
                    _inputOverlay,
                    point.Position,
                    e.Timestamp,
                    new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
                    e.KeyModifiers,
                    MouseButton.Left
                );
                _model3DControl.HandlePointerReleased(args);
            }
            else // Для мыши используем реальные свойства
            {
                _model3DControl.HandlePointerReleased(e);
            }
            e.Handled = true;
        }

        private void OnPointerCaptureLost(object sender, PointerCaptureLostEventArgs e)
        {
            int countBefore = _activePointers.Count;
            bool wasMultiTouch = countBefore >= 2;

            if (_activePointers.ContainsKey(e.Pointer.Id))
            {
                _activePointers.Remove(e.Pointer.Id);
            }

            // Сбрасываем состояние жеста при полном отпускании
            if (_activePointers.Count == 0)
            {
                ResetTwoFingerGesture();
                _ignoreSingleFinger = false;
            }
            // Установка флага игнорирования одиночного пальца
            else if (wasMultiTouch && _activePointers.Count == 1)
            {
                _ignoreSingleFinger = true;
                ResetTwoFingerGesture();
            }
        }

        private void OnPointerMoved(object sender, PointerEventArgs e)
        {
            var currentPoint = e.GetCurrentPoint(this);
            if (_activePointers.ContainsKey(currentPoint.Pointer.Id))
            {
                _activePointers[currentPoint.Pointer.Id] = currentPoint.Position;
            }

            // Обработка разных сценариев касаний
            switch (_activePointers.Count)
            {
                case 1: // Один палец - вращение
                    // Игнорировать движение одиночного пальца после мультитач
                    if (_ignoreSingleFinger && e.Pointer.Type != PointerType.Mouse)
                    {
                        break;
                    }

                    if (e.Pointer.Type != PointerType.Mouse)
                    {
                        // Для touch эмулируем левую кнопку
                        var args = new PointerEventArgs(
                            PointerMovedEvent,
                            e.Source,
                            e.Pointer,
                            _inputOverlay,
                            currentPoint.Position,
                            e.Timestamp,
                            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other),
                            e.KeyModifiers
                        );
                        _model3DControl.HandlePointerMoved(args);
                    }
                    else
                    {
                        // Для мыши передаем реальное событие
                       _model3DControl.HandlePointerMoved(e);
                    }
                    break;

                case 2: // Два пальца - масштабирование и панорамирование
                    _isTwoFingerGesture = true;
                    var points = _activePointers.Values.ToArray();
                    var center = GetCenterPoint(points);
                    double distance = GetDistance(points[0], points[1]);

                    // Первое движение двумя пальцами - инициализация
                    if (_prevDistance <= 0)
                    {
                        _prevDistance = distance;
                        _prevCenter = center;
                        break;
                    }

                    // Масштабирование
                    double scaleFactor = distance / _prevDistance;
                    double deltaY = (scaleFactor - 1) * 100;
                    _model3DControl.Scene.Camera.Distance -= (float)(deltaY * _model3DControl.ZoomSensitivity);

                    // Панорамирование
                    var delta = center - _prevCenter;
                    _model3DControl.Scene.Camera.Target -= new Vector3(
                        (float)(delta.X * _model3DControl.PanSensitivity),
                        (float)(-delta.Y * _model3DControl.PanSensitivity),
                        0
                    );

                    // Обновляем состояние
                    _prevDistance = distance;
                    _prevCenter = center;
                   // _model3DControl.RequestNextFrameRendering();
                    break;

                default:
                    // Сбрасываем состояние при изменении количества пальцев
                    if (_isTwoFingerGesture)
                    {
                        ResetTwoFingerGesture();
                    }
                    break;
            }
            e.Handled = true;
        }

        private void OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            _model3DControl.HandlePointerWheelChanged(e);
            e.Handled = true;
        }
        #endregion
    }
}
