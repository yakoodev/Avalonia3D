using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Collections.Generic;

namespace Avalonia3D.Sandbox.Controls;

public static class PointerForwardingBehavior
{
    public static readonly AttachedProperty<SandboxModel3DControl?> TargetProperty =
        AvaloniaProperty.RegisterAttached<Control, Border, SandboxModel3DControl?>("Target");

    private static readonly Dictionary<Border, IDisposable> Subscriptions = new();

    static PointerForwardingBehavior()
    {
        TargetProperty.Changed.AddClassHandler<Border>((border, _) => OnTargetChanged(border, GetTarget(border)));
    }

    public static void SetTarget(Border element, SandboxModel3DControl? value) => element.SetValue(TargetProperty, value);

    public static SandboxModel3DControl? GetTarget(Border element) => element.GetValue(TargetProperty);

    private static void OnTargetChanged(Border border, SandboxModel3DControl? target)
    {
        if (Subscriptions.Remove(border, out var oldSubscription))
        {
            oldSubscription.Dispose();
        }

        if (target == null)
        {
            return;
        }

        void Pressed(object? _, PointerPressedEventArgs e) => target.HandlePointerPressed(e);
        void Released(object? _, PointerReleasedEventArgs e) => target.HandlePointerReleased(e);
        void Moved(object? _, PointerEventArgs e) => target.HandlePointerMoved(e);
        void Wheel(object? _, PointerWheelEventArgs e) => target.HandlePointerWheelChanged(e);

        border.PointerPressed += Pressed;
        border.PointerReleased += Released;
        border.PointerMoved += Moved;
        border.PointerWheelChanged += Wheel;

        Subscriptions[border] = Disposable.Create(() =>
        {
            border.PointerPressed -= Pressed;
            border.PointerReleased -= Released;
            border.PointerMoved -= Moved;
            border.PointerWheelChanged -= Wheel;
        });
    }

    private sealed class Disposable : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;

        private Disposable(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public static IDisposable Create(Action onDispose) => new Disposable(onDispose);

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _onDispose();
        }
    }
}
