using System.Collections.Generic;

namespace Avalonia3D.Rendering
{
    public sealed class RenderPipelineFactory
    {
        private readonly IPostProcessingEffectRegistry _postProcessingEffects;

        public RenderPipelineFactory(IPostProcessingEffectRegistry? postProcessingEffects = null)
        {
            _postProcessingEffects = postProcessingEffects ?? PostProcessingEffectRegistry.CreateDefault();
        }

        public IReadOnlyList<IRenderPass> CreatePasses(GraphicsProfile profile)
        {
            var validated = (profile ?? GraphicsProfile.Medium).Validate();
            var passes = new List<IRenderPass>();

            if (validated.Shadows.Enabled)
            {
                passes.Add(new ShadowPass(validated));
            }

            if (validated.Reflections.Enabled && validated.Reflections.Mode != ReflectionMode.Off)
            {
                passes.Add(new EnvironmentLightingPass(validated));
            }

            passes.Add(new ForwardPass(validated));

            foreach (var postProcessingPass in _postProcessingEffects.CreateEnabledPasses(validated))
            {
                passes.Add(postProcessingPass);
            }

            if (validated.PostFx.Effects.HasFlag(PostEffectsFlags.ToneMapping) || validated.PostFx.Effects.HasFlag(PostEffectsFlags.GammaCorrection))
            {
                passes.Add(new PostEffectsPass(validated));
            }

            return passes;
        }
    }
}
