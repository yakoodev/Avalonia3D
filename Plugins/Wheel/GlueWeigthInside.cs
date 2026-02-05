using Avalonia3D.Model.Workflow;

namespace Avalonia3D.Plugins.Wheel
{
    public class GlueWeigthInside : InsideWeigth
    {
        public override bool IsActive
        {
            get => (Parent.WeightScheme == WeightScheme.S3
                || Parent.WeightScheme == WeightScheme.S4
                || Parent.WeightScheme == WeightScheme.S5) 
                && Parent.Context.LookState == Look.Left;
            set => base.IsActive = value;
        }        
        public GlueWeigthInside(Wheel scene3D) : base(scene3D)
        {
            EmissionColor = new System.Numerics.Vector3(0.8f, 0.8f, 0.2f);
            BaseColor = new System.Numerics.Vector3(1f, 0.8f, 0.2f);
        }
    }
}
