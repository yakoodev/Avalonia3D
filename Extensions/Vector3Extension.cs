using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Avalonia3D.Extensions
{
    public class Vector3Extension : MarkupExtension
    {
        public Vector3Extension(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
        }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider) =>
            new Vector3(X, Y, Z);
    }
}
