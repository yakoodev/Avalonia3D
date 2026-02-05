using Avalonia3D.Model.Workflow;

namespace Avalonia3D.Model.StandObjects
{
    public class SpringWeigthInnerOutside : OutsideWeigth
    {
        public override bool IsActive
        {
            get => (Parent.WeightScheme == WeightScheme.S2
                || Parent.WeightScheme == WeightScheme.S4)
                && Parent.Context.LookState == Look.Right;
            set => base.IsActive = value;
        }        
        public SpringWeigthInnerOutside(Wheel scene3D) : base(scene3D)
        {
            EmissionColor = new System.Numerics.Vector3(0.8f, 0.8f, 0.2f);
            BaseColor = new System.Numerics.Vector3(1f, 0.8f, 0.2f);
        }     
    }
}
