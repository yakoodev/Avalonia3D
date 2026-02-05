
using Serilog;
using System.Collections.Generic;
using System.Linq;

namespace Avalonia3D.Animation
{
    public class Animator
    {
        private readonly List<IAnimation> _animations = new();

        public bool TryAdd(IAnimation animation)
        {
            if (animation.IsSingtone)
            {
                if (_animations.Any(a => a.GetType() == animation.GetType()))
                {                    
                    return false;
                }
            }

            _animations.Add(animation);            
            return true;
        }       

        public void Update(float deltaTime)
        {
            for (int i = _animations.Count - 1; i >= 0; i--)
            {
                if (!_animations[i].Update(deltaTime))
                {
                    var a = _animations[i];                    
                    _animations.RemoveAt(i);
                }
            }
        }
    }
}
