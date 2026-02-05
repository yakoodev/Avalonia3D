using Avalonia.Media.Imaging;
using Avalonia3D.Helpers;
using SkiaSharp;

namespace Avalonia3D.Model
{
    public class TopCanvas
    {
        private Scene3D _scene;

        public void Draw(WriteableBitmap bitmap)
        {
            using var fbmp = bitmap.Lock();
            var info = new SKImageInfo(bitmap.PixelSize.Width,
                                       bitmap.PixelSize.Height,
                                       SKColorType.Bgra8888,
                                       SKAlphaType.Premul);

            // Создаём surface, который рисует прямо в WriteableBitmap
            using var surface = SKSurface.Create(info, fbmp.Address, fbmp.RowBytes);
            var canvas = surface.Canvas;

           //ViewCameraParams(canvas);

            foreach (var w in _scene.Wheel.Weigths)
                if (w.IsVisible)
                    w.RenderSurface(_scene, canvas, bitmap.PixelSize.Width, bitmap.PixelSize.Height);

            // Важно: фиксируем результат
            canvas.Flush();
        }

        private SKPaint ViewCameraParams(SKCanvas canvas)
        {
            // Пример рисования текста
           using var paint = new SKPaint
            {
                Color = SKColors.YellowGreen,
                IsAntialias = true
            };

            var pos = _scene.Camera.Position;
            canvas.DrawText($"Camera X:{pos.X:0.00} Y:{pos.Y:0.00} Z:{pos.Z:0.00}",
                50, 100, paint);

            var pitch = _scene.Camera.Pitch;
            var yaw = _scene.Camera.Yaw;
            var distance = _scene.Camera.Distance;
            canvas.DrawText($"Camera Pitch:{pitch * MathHelper.ToDeg:0.00} Yaw:{yaw * MathHelper.ToDeg:0.00} Distance:{distance:0.00}",
                50,120, paint);
            return paint;
        }

        internal void Init(Scene3D scene)
        {
            _scene = scene;
        }
    }
}
