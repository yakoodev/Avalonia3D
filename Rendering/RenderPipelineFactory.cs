using System.Collections.Generic;

namespace Avalonia3D.Rendering
{
    public sealed class RenderPipelineFactory
    {
        public IReadOnlyList<IRenderPass> CreatePasses(RenderQualitySettings settings)
        {
            var validated = settings.Validate();
            var passes = new List<IRenderPass>();

            if (validated.ShadowsEnabled)
            {
                passes.Add(new ShadowPass(validated));
            }

            passes.Add(new ForwardPass(validated));

            if (validated.PostEffects != PostEffectsFlags.None)
            {
                passes.Add(new PostEffectsPass(validated));
            }

            return passes;
        }
    }
}
