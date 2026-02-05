using System.Collections.Generic;
using System.Linq;

namespace Avalonia3D.Animation
{
    public class AnimationClip
    {
        public AnimationClip(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public List<AnimationChannel> Channels { get; } = [];

        public float Duration
        {
            get
            {
                if (Channels.Count == 0)
                {
                    return 0;
                }

                return Channels.Max(channel => channel.Duration);
            }
        }
    }
}
