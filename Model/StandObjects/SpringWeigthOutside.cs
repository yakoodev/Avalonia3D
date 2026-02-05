using Avalonia3D.Model.Workflow;

namespace Avalonia3D.Model.StandObjects
{
    public class SpringWeigthOutside : OutsideWeigth
    {
        public override float Angle { get => (float)(-Parent.OutsideWeigth).Phase; }
        public override bool IsActive
        {            get => (Parent.WeightScheme == WeightScheme.S1
                || Parent.WeightScheme == WeightScheme.S5)
                && Parent.Context.LookState == Look.Right;
            set => base.IsActive = value;
        }
        public SpringWeigthOutside(Wheel scene3D) : base(scene3D)
        {
            EmissionColor = new System.Numerics.Vector3(0.8f, 0.8f, 0.2f);
            BaseColor = new System.Numerics.Vector3(1f, 0.8f, 0.2f);
        }
    }
}
