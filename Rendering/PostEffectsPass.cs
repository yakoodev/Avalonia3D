namespace Avalonia3D.Rendering
{
    public sealed class PostEffectsPass : IRenderPass
    {
        public string Name => "PostEffectsPass";

        public void Execute(RenderPipelineContext context)
        {
            // Точка расширения для пост-эффектов. Пока пусто.
        }
    }
}
