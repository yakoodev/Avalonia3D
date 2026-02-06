using Avalonia3D.Interfaces;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Rendering;
using System.Numerics;
using Xunit;

namespace Avalonia3D.Tests;

public class ShaderSelectionPolicyTests
{
    [Fact]
    public void Select_Prefers_MaterialShader_Instance()
    {
        var scene = CreateScene();
        var materialShader = new StubShader();
        var material = new Material { Shader = materialShader };

        var selected = scene.ShaderSelectionPolicy.Select(material, scene, gl: null);

        Assert.Same(materialShader, selected);
    }

    [Fact]
    public void Select_Uses_MaterialShaderId_WhenAvailable()
    {
        var scene = CreateScene();
        var material = new Material { ShaderId = "material-shader" };

        var selected = scene.ShaderSelectionPolicy.Select(material, scene, gl: null);

        Assert.Same(scene.ShaderRegistry.Get("material-shader"), selected);
    }

    [Fact]
    public void Select_Uses_ActiveShaderId_Before_Default()
    {
        var scene = CreateScene();
        scene.ActiveShaderId = "scene-active";

        var selected = scene.ShaderSelectionPolicy.Select(material: null, scene, gl: null);

        Assert.Same(scene.ShaderRegistry.Get("scene-active"), selected);
    }

    [Fact]
    public void Select_Uses_Default_WhenNoOtherMatch()
    {
        var scene = CreateScene();

        var selected = scene.ShaderSelectionPolicy.Select(material: null, scene, gl: null);

        Assert.Same(scene.ShaderRegistry.GetDefault(), selected);
    }

    private static Scene3D CreateScene()
    {
        var scene = new Scene3D();
        scene.ShaderRegistry.RegisterInstance("default", new StubShader());
        scene.ShaderRegistry.RegisterInstance("material-shader", new StubShader());
        scene.ShaderRegistry.RegisterInstance("scene-active", new StubShader());
        scene.ShaderRegistry.SetDefault("default");
        return scene;
    }

    private sealed class StubShader : IShader3D
    {
        public uint Handle => 0;
        public void BindMaterial(RenderResources resources, Material? material, uint? shadowMapId = null) { }
        public void Dispose() { }
        public void SetUniforms(IRenderContext renderContext, SceneObject sceneObject, Matrix4x4 lightSpaceMatrix = default) { }
        public void Use() { }
    }
}
