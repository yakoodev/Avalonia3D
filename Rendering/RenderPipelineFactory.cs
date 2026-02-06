using System.Collections.Generic;

namespace Avalonia3D.Rendering
{
    public sealed class RenderPipelineFactory
    {
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

            if (validated.PostFx.Effects != PostEffectsFlags.None)
            {
                passes.Add(new PostEffectsPass(validated));
            }

            return passes;
        }
    }
}
