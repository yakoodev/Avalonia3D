using Avalonia3D.Animation;
using Avalonia3D.Interfaces;
using Avalonia3D.Interaction.Behaviors;
using Avalonia3D.Lights;
using Avalonia3D.Loaders;
using Avalonia3D.Memory;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Model.Workflow;
using Avalonia3D.Rendering;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Avalonia3D.Model
{

    public static class Scene3DDefault
    {
        internal const float DistantionBase = 150;
        internal const float PitchBase = 0;
        internal const float YawBase = 0;
    }

    public enum Unit
    {
        g,
        inch
    }

    public class Scene3D
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly List<ISceneModule> _modules = [];
        private readonly List<ISceneBehavior> _behaviors = [];
        private readonly List<IUpdatableBehavior> _updatableBehaviors = [];
        private double _lastTime = 0;

        private RenderResourceManager? _resourceManager;
        private readonly Dictionary<ShaderRenderMode, string> _renderModeBindings = new();

        public SceneGraph SceneGraph { get; private set; } = new();
        public GltfSceneImporter Importer { get; } = new();
        public IReadOnlyList<ISceneModule> Modules => _modules;
        public IReadOnlyList<ISceneBehavior> Behaviors => _behaviors;

        public List<Light> Lights { get; set; } = [];
        public Camera Camera { get; set; } = new();
        public Unit Unit { get; set; }
        [Obsolete("Use ShaderRegistry instead of direct shader list.")]
        public List<IShader3D> Shaders { get; } = [];
        public ShaderRegistry ShaderRegistry { get; } = new();
        public ShaderSelectionPolicy ShaderSelectionPolicy { get; } = new();
        public string? ActiveShaderId { get; set; }
        public ShaderRenderMode RenderMode { get; set; } = ShaderRenderMode.Default;
        public EnvironmentLightingSettings EnvironmentLighting { get; set; } = EnvironmentLightingSettings.FromGraphicsProfile(GraphicsProfile.Medium);
        public GraphicsProfile ActiveGraphicsProfile { get; private set; } = GraphicsProfile.Medium.Validate();
        internal Animator Animator { get; private set; } = new();
        public AnimatorComponent AnimatorComponent { get; private set; }
        public SceneImportReport LastImportReport { get; private set; } = SceneImportReport.Success;
        public SceneCommandBus CommandBus { get; } = new();

        public Scene3D()
        {
            Importer.ValidationPolicy = ImportValidationConfiguration.CurrentPolicy;
            AnimatorComponent = new AnimatorComponent(SceneGraph, Animator);
            ApplyGraphicsProfile(GraphicsProfile.Medium);
        }

        public void BindRenderMode(ShaderRenderMode mode, string shaderId)
        {
            if (string.IsNullOrWhiteSpace(shaderId))
            {
                return;
            }

            _renderModeBindings[mode] = shaderId;
        }

        public string? GetShaderIdForMode(ShaderRenderMode mode)
        {
            return _renderModeBindings.TryGetValue(mode, out var shaderId) ? shaderId : null;
        }

        public void Init(GL gl)
        {
            _resourceManager = new RenderResourceManager(gl);
            MemoryManager.Initialize(_resourceManager);
        }


        public void ApplyGraphicsProfile(GraphicsProfile profile)
        {
            var validatedProfile = (profile ?? GraphicsProfile.Medium).Validate();
            ActiveGraphicsProfile = validatedProfile;
            EnvironmentLighting = EnvironmentLightingSettings.FromGraphicsProfile(validatedProfile);
        }

        public event EventHandler<Look>? LookChanged;

        private Look _lookState;

        public Look LookState
        {
            get => _lookState;
            set
            {
                _lookState = value;
                LookChanged?.Invoke(this, _lookState);
            }
        }

        public void RegisterModule(ISceneModule module)
        {
            if (module == null || _modules.Contains(module))
            {
                return;
            }

            _modules.Add(module);
            module.Attach(this);
        }

        public void UnregisterModule(ISceneModule module)
        {
            if (module == null)
            {
                return;
            }

            if (_modules.Remove(module))
            {
                module.Detach(this);
            }
        }

        public T? GetModule<T>() where T : class, ISceneModule
        {
            return _modules.OfType<T>().FirstOrDefault();
        }

        public void RegisterBehavior(ISceneBehavior behavior)
        {
            if (behavior == null || _behaviors.Contains(behavior))
            {
                return;
            }

            _behaviors.Add(behavior);
            if (behavior is IUpdatableBehavior updatable)
            {
                _updatableBehaviors.Add(updatable);
            }

            if (behavior is ISceneCommandHandler commandHandler)
            {
                CommandBus.RegisterHandler(commandHandler);
            }

            behavior.Attach(this);
        }

        public void UnregisterBehavior(ISceneBehavior behavior)
        {
            if (behavior == null)
            {
                return;
            }

            if (!_behaviors.Remove(behavior))
            {
                return;
            }

            if (behavior is IUpdatableBehavior updatable)
            {
                _updatableBehaviors.Remove(updatable);
            }

            if (behavior is ISceneCommandHandler commandHandler)
            {
                CommandBus.UnregisterHandler(commandHandler);
            }

            behavior.Detach(this);
        }

        public bool DispatchCommand(SceneCommand command)
        {
            return CommandBus.Publish(command);
        }

        private float GetDeltaTime()
        {
            double now = _stopwatch.Elapsed.TotalSeconds;
            float deltaTime = (float)(now - _lastTime);
            _lastTime = now;
            return deltaTime;
        }

        public void UpdateFrame()
        {
            float deltaTime = GetDeltaTime();
            Animator.Update(deltaTime);
            foreach (var behavior in _updatableBehaviors)
            {
                behavior.Update(deltaTime);
            }
        }

        public void Render(IRenderContext context)
        {
            UpdateFrame();
            foreach (var obj in SceneGraph.RootObjects)
            {
                if (obj.IsVisible)
                {
                    obj.Render(context);
                }
            }
        }

        public void Clear()
        {
            ResetSceneGraph();
            MemoryManager.Shutdown();
        }

        internal void ResetSceneGraph()
        {
            foreach (var item in SceneGraph.RootObjects)
            {
                item.Dispose();
            }

            SceneGraph.Clear();
            _resourceManager?.ClearAll();
            ModelLoader.ClearAllCaches();
        }

        public SceneGraph LoadScene(string gltfPath)
        {
            ResetSceneGraph();
            var importResult = Importer.ImportWithAnimations(gltfPath);
            LastImportReport = new SceneImportReport(importResult.Status, importResult.Issues);
            SceneGraph = importResult.Graph;
            AnimatorComponent.SetSceneGraph(SceneGraph);
            foreach (var clip in importResult.Clips)
            {
                AnimatorComponent.RegisterClip(clip);
            }

            ReattachBehaviors();
            BuildRenderResources();
            LookChanged?.Invoke(this, _lookState);
            return SceneGraph;
        }

        internal void BuildRenderResources()
        {
            if (_resourceManager == null)
            {
                return;
            }

            foreach (var obj in SceneGraph.RootObjects)
            {
                BuildRenderResourcesRecursive(obj);
            }
        }

        private void BuildRenderResourcesRecursive(SceneObject obj)
        {
            if (obj is MeshObject meshObject)
            {
                meshObject.BuildRenderResources(_resourceManager);
            }

            if (obj is MeshGroup meshGroup)
            {
                foreach (var child in meshGroup)
                {
                    BuildRenderResourcesRecursive(child);
                }
            }
        }

        private void ReattachBehaviors()
        {
            foreach (var behavior in _behaviors)
            {
                behavior.Detach(this);
                behavior.Attach(this);
            }
        }

        internal void UpdateLook()
        {
            LookChanged?.Invoke(this, _lookState);
        }
    }

    public readonly record struct SceneImportReport(SceneImportStatus Status, IReadOnlyList<string> Issues)
    {
        public static SceneImportReport Success => new(SceneImportStatus.Success, []);
        public bool IsDegraded => Status == SceneImportStatus.Degraded;
    }
}
