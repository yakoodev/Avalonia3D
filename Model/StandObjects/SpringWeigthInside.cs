using Avalonia3D.Model.Workflow;
using System.Numerics;

namespace Avalonia3D.Model.StandObjects
{
    public class SpringWeigthInside : InsideWeigth
    {
        public override float Angle { get => (float)(-Parent.InsideWeigth).Phase; }

        public override bool IsActive
        {
            get => (Parent.WeightScheme == WeightScheme.S1
                || Parent.WeightScheme == WeightScheme.S2
                || Parent.WeightScheme == WeightScheme.S6)
                && Parent.Context.LookState == Look.Left;
            set => base.IsActive = value;
        }

        public SpringWeigthInside(Wheel scene3D) : base(scene3D)
        {
            EmissionColor = new Vector3(0.8f, 0.8f, 0.2f);
            BaseColor = new Vector3(1f, 0.8f, 0.2f);
        }      
    }
}
