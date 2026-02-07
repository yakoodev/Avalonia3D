using System;
using System.Collections.Generic;

namespace Avalonia3D.Rendering
{
    public interface IPostProcessingEffect
    {
        string Key { get; }
        bool IsEnabled(GraphicsProfile profile);
        IRenderPass CreatePass(GraphicsProfile profile);
    }

    public interface IPostProcessingEffectRegistry
    {
        IEnumerable<IRenderPass> CreateEnabledPasses(GraphicsProfile profile);
    }

    public sealed class PostProcessingEffectRegistry : IPostProcessingEffectRegistry
    {
        private readonly List<IPostProcessingEffect> _effects = new();

        public PostProcessingEffectRegistry Register(IPostProcessingEffect effect)
        {
            if (effect == null)
            {
                throw new ArgumentNullException(nameof(effect));
            }

            _effects.Add(effect);
            return this;
        }

        public IEnumerable<IRenderPass> CreateEnabledPasses(GraphicsProfile profile)
        {
            var validated = (profile ?? GraphicsProfile.Medium).Validate();
            foreach (var effect in _effects)
            {
                if (effect.IsEnabled(validated))
                {
                    yield return effect.CreatePass(validated);
                }
            }
        }

        public static PostProcessingEffectRegistry CreateDefault() => new PostProcessingEffectRegistry()
            .Register(new BloomPostProcessingEffect());
    }

    public sealed class BloomPostProcessingEffect : IPostProcessingEffect
    {
        public string Key => "Bloom";

        public bool IsEnabled(GraphicsProfile profile)
        {
            var postFx = profile?.PostFx;
            return postFx != null
                && postFx.Effects.HasFlag(PostEffectsFlags.Bloom)
                && postFx.Bloom.Enabled
                && postFx.Bloom.Intensity > 0f
                && postFx.Bloom.Iterations > 0;
        }

        public IRenderPass CreatePass(GraphicsProfile profile) => new BloomPass(profile);
    }
}
