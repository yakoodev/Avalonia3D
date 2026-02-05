using System;
using SkiaSharp;
namespace Avalonia3D.Controls2D 
{ 
    public class CalloutBox
    {
        public SKRect Rect { get; set; }          // Прямоугольник блока
        public float CornerRadius { get; set; }   // Радиус скругления углов
        public SKColor BorderColor { get; set; }  // Цвет границы
        public float BorderWidth { get; set; }    // Толщина границы
        public SKColor FillColor { get; set; }    // Цвет заливки
        public string Text { get; set; } = "Text";        // Текст внутри
        public SKPoint ArrowTarget { get; set; }  // Точка, куда идёт стрелка
        public float ArrowSize { get; set; } = 20f; // Длина стрелки
        public bool IsVisible {  get; set; } = false;

        private SKPaint _borderPaint;
        private SKPaint _fillPaint;
        private SKPaint _textPaint;
        public void CreateRectFromCenter(SKPoint center, float width, float height)
        {
            float halfWidth = width / 2f;
            float halfHeight = height / 2f;

            Rect = new SKRect(
                center.X - halfWidth,
                center.Y - halfHeight,
                center.X + halfWidth,
                center.Y + halfHeight
            );
        }

        public CalloutBox()
        {
            _borderPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };
            _fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            _textPaint = new SKPaint
            {
                IsAntialias = true,
                Color = SKColors.Black,
                TextSize = 20,
                TextAlign = SKTextAlign.Center
            };
        }

        public void Draw(SKCanvas canvas)
        {
            _borderPaint.Color = BorderColor;
            _borderPaint.StrokeWidth = BorderWidth;
            _fillPaint.Color = FillColor;

            // Рисуем стрелку от центра к ArrowTarget
            var arrowStart = new SKPoint(Rect.MidX, Rect.MidY);
            DrawArrow(canvas, arrowStart, ArrowTarget);

            // Рисуем прямоугольник с закругленными углами
            var path = new SKPath();
            path.AddRoundRect(Rect, CornerRadius, CornerRadius);

            canvas.DrawPath(path, _fillPaint);
            canvas.DrawPath(path, _borderPaint);

            // Рисуем текст по центру
            var textBounds = new SKRect();
            _textPaint.MeasureText(Text, ref textBounds);
            var textX = Rect.MidX;
            var textY = Rect.MidY - textBounds.MidY; // Центрируем по вертикали
            canvas.DrawText(Text, textX, textY, _textPaint);
        }

        private void DrawArrow(SKCanvas canvas, SKPoint start, SKPoint end)
        {
            // Прямая линия
            canvas.DrawLine(start, end, _borderPaint);

            // Направление
            var direction = new SKPoint(end.X - start.X, end.Y - start.Y);
            float length = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
            if (length == 0) return;

            direction.X /= length;
            direction.Y /= length;

            // Параметры стрелки
            float arrowHeadSize = ArrowSize;
            float angle = (float)(Math.PI / 6); // 30 градусов

            // Векторы для кончиков стрелки
            var left = new SKPoint(
                end.X - arrowHeadSize * (float)(Math.Cos(angle) * direction.X + Math.Sin(angle) * direction.Y),
                end.Y - arrowHeadSize * (float)(-Math.Sin(angle) * direction.X + Math.Cos(angle) * direction.Y)
            );

            var right = new SKPoint(
                end.X - arrowHeadSize * (float)(Math.Cos(angle) * direction.X - Math.Sin(angle) * direction.Y),
                end.Y - arrowHeadSize * (float)(Math.Sin(angle) * direction.X + Math.Cos(angle) * direction.Y)
            );

            // Залитый треугольник
            using (var path = new SKPath())
            {
                path.MoveTo(end);
                path.LineTo(left);
                path.LineTo(right);
                path.Close();

                using (var fillPaint = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    Color = _borderPaint.Color,
                    IsAntialias = true
                })
                {
                    canvas.DrawPath(path, fillPaint);
                }
            }
        }

    }

}
