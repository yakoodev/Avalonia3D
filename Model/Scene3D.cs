using Avalonia3D.Animation;
using Avalonia3D.Interfaces;
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
        private double _lastTime = 0;

        private RenderResourceManager? _resourceManager;

        public SceneGraph SceneGraph { get; private set; } = new();
        public GltfSceneImporter Importer { get; } = new();
        public IReadOnlyList<ISceneModule> Modules => _modules;

        public List<Light> Lights { get; set; } = [];
        public Camera Camera { get; set; } = new();
        public Unit Unit { get; set; }
        public List<IShader3D> Shaders { get; } = [];
        internal Animator Animator { get; private set; } = new();

        public void Init(GL gl)
        {
            _resourceManager = new RenderResourceManager(gl);
            MemoryManager.Initialize(_resourceManager);
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

        private float GetDeltaTime()
        {
            double now = _stopwatch.Elapsed.TotalSeconds;
            float deltaTime = (float)(now - _lastTime);
            _lastTime = now;
            return deltaTime;
        }

        public void Render(IRenderContext context)
        {
            foreach (var shader in context.Scene.Shaders)
            {
                shader.Use();
                float deltaTime = GetDeltaTime();
                Animator.Update(deltaTime);
                foreach (var obj in SceneGraph.RootObjects)
                {
                    if (obj.IsVisible)
                    {
                        obj.Render(context);
                    }
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
            SceneGraph = Importer.Import(gltfPath);
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

        internal void UpdateLook()
        {
            LookChanged?.Invoke(this, _lookState);
        }
    }
}
