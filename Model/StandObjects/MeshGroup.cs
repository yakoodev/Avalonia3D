using Avalonia3D.Interfaces;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;

namespace Avalonia3D.Model.StandObjects
{
    public class MeshGroup : MeshObject, IEnumerable<MeshObject>
    {
        private readonly List<MeshObject> _children = [];       

        public int Count => _children.Count;
        public bool IsReadOnly => false;

        private void RecalculateGravity()
        {
            if (_children.Count == 0)
            {
                Gravity = Vector3.Zero;
                return;
            }

            Vector3 sum = Vector3.Zero;
            foreach (var child in _children)
                sum += child.Gravity;

            Gravity = sum / _children.Count;
        }

        public MeshGroup(IEnumerable<MeshObject> meshObjects)
        {
            if (meshObjects != null)
                _children.AddRange(meshObjects);
            RecalculateGravity();
        }
        public MeshGroup()
        {
        }

        public void Add(MeshObject obj)
        {
            obj.Parent = this;
            _children.Add(obj);
            RecalculateGravity();
        }     

        /// <summary>
        /// Применяет локальную трансформацию группы к всем дочерним объектам при рендере
        /// </summary>
        public override void Render(IRenderContext context)
        {
            foreach (var child in _children)
            {
                if (child.IsVisible)
                {
                  
                    child.BaseColor = BaseColor;
                    child.EmissionColor = EmissionColor;
                    child.Render(context);
                }
            }
        }       
      

        public override void Dispose()
        {
            foreach (var child in _children)
                child.Dispose();
            _children.Clear();
        }     

        public IEnumerator<MeshObject> GetEnumerator()
        {
            return _children.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
