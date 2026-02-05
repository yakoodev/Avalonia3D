using Avalonia3D.Animation;
using Avalonia3D.Helpers;
using Avalonia3D.Interfaces;
using Avalonia3D.Lights;
using Avalonia3D.Loaders;
using Avalonia3D.Memory;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Model.Workflow;
using Serilog;
using SharpGLTF.Schema2;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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
        private double _lastTime = 0;
        private readonly List<SceneObject> _objects = [];

        private static readonly Dictionary<string, List<Model>> _modelsCaches = [];

        public Scene3D()
        {
            Wheel = new(this);
            WheelCut = new(this);
        }
        public List<Light> Lights { get; set; } = [];
        public Camera Camera { get; set; } = new();
        public Wheel Wheel { get; private set; }
        public WheelCut WheelCut { get; private set; }
        public Unit Unit { get; set; }

        private GlueWeigthOutside? _glueWeigthOutside;
        private GlueWeigthInside? _glueWeigthInside;
        private SpringWeigthInside? _springWeigthInside;
        private SpringWeigthOutside? _springWeigthOutside;
        private SpringWeigthInnerOutside? _springWeigthInnerOutside;

        public List<IShader3D> Shaders { get; } = new List<IShader3D>();

        internal Animator Animator { get; private set; } = new();

        private GL _gl;

        public void Init(GL gl)
        {
            _gl = gl;
        }


        public event EventHandler<Look>? LookChanged;

        private Look _lookState;        

        public Look LookState
        {
            get
            {
                return _lookState;
            }
            set
            {
                _lookState = value;
                LookChanged?.Invoke(this, _lookState);
            }
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
            foreach(var shader in context.Scene.Shaders)
            {
                shader.Use();
                float deltaTime = GetDeltaTime();
                Animator.Update(deltaTime);
                foreach (var obj in _objects)
                    if (obj.IsVisible)
                        obj.Render(context);
            }
        }

        public void Clear()
        {
            // Очистка mesh объектов
            foreach (var item in _objects)
                item.Dispose();

            _objects.Clear();
            // Очистка кешей
            MeshObject.ClearGeometryCache(_gl);
            ModelLoader.ClearAllCaches();

            // Shutdown MemoryManager
            MemoryManager.Shutdown(_gl);
        }

        public void LoadModel(string source)
        {            
            var wPath = Path.Combine(source, "wheel.glb");
            Wheel.Name = Path.GetFileNameWithoutExtension(wPath);            
            LoadGltfModel(wPath, Wheel);
            _objects.Add(Wheel);           

            _glueWeigthOutside = new GlueWeigthOutside(Wheel);
            var glueWeigthOutsidePath = Path.Combine(source, "GlueWeigthOutside.glb");
            _glueWeigthOutside.Name = Path.GetFileNameWithoutExtension(glueWeigthOutsidePath);
            LoadGltfModel(glueWeigthOutsidePath, _glueWeigthOutside);
            _objects.Add(_glueWeigthOutside);


            _glueWeigthInside = new GlueWeigthInside(Wheel);
            var glueWeigthInsidePath = Path.Combine(source, "GlueWeigthInside.glb");
            _glueWeigthInside.Name = Path.GetFileNameWithoutExtension(glueWeigthInsidePath);
            LoadGltfModel(glueWeigthInsidePath, _glueWeigthInside);
            _objects.Add(_glueWeigthInside);


            _springWeigthInside = new SpringWeigthInside(Wheel);
            var springWeigthInsidePath = Path.Combine(source, "SpringWeigthInside.glb");
            _springWeigthInside.Name = Path.GetFileNameWithoutExtension(springWeigthInsidePath);
            LoadGltfModel(springWeigthInsidePath, _springWeigthInside);
            _objects.Add(_springWeigthInside);

            _springWeigthOutside = new SpringWeigthOutside(Wheel);
            var springWeigthOutsidePath = Path.Combine(source, "SpringWeigthOutside.glb");
            _springWeigthOutside.Name = Path.GetFileNameWithoutExtension(springWeigthOutsidePath);
            LoadGltfModel(springWeigthOutsidePath, _springWeigthOutside);
            _objects.Add(_springWeigthOutside);


            _springWeigthInnerOutside = new SpringWeigthInnerOutside(Wheel);
            var springWeigthInnerOutsidePath = Path.Combine(source, "SpringWeigthInnerOutside.glb");
            _springWeigthOutside.Name = Path.GetFileNameWithoutExtension(springWeigthInnerOutsidePath);
            LoadGltfModel(springWeigthInnerOutsidePath, _springWeigthInnerOutside);
            _objects.Add(_springWeigthInnerOutside);            

            Wheel.Weigths.Add(_glueWeigthInside);
            Wheel.Weigths.Add(_glueWeigthOutside);
            Wheel.Weigths.Add(_springWeigthInside);
            Wheel.Weigths.Add(_springWeigthOutside);
            Wheel.Weigths.Add(_springWeigthInnerOutside);
            LookChanged?.Invoke(this, _lookState);
        }

        private void LoadGltfModel(string path, MeshGroup holder)
        {

            if (!File.Exists(path))
                throw new FileNotFoundException($"Model file not found: {path}");

            Log.Information($"Loading GLTF model: {path}");
            MemoryManager.LogMemoryState("Before GLTF load");

            // Оптимизация для больших моделей
            var fileSize = new FileInfo(path).Length;
            if (fileSize > 5 * 1024 * 1024) // Больше 5 MB
            {
                MemoryManager.OptimizeForLargeModel();
            }

            try
            {
                List<Model>? loaded = null;

                if (!_modelsCaches.ContainsKey(path))
                {
                    var wheelmodel = ModelRoot.Load(path);
                    loaded = ModelLoader.LoadModels(wheelmodel);
                    _modelsCaches.Add(path, loaded);
                }
                else
                {
                    loaded = _modelsCaches[path];
                }

                Log.Information($"Loaded {loaded.Count} model parts");

                // Если GL уже инициализирован - сразу создаём GPU-ресурсы
                ProcessModelsImmediately(loaded, holder);

                MemoryManager.LogMemoryState("After GLTF load");
            }
            finally
            {
                if (fileSize > 5 * 1024 * 1024)
                {
                    MemoryManager.RestoreNormalSettings();
                }
            }
        }

        private void ProcessModelsImmediately(List<Model> models, MeshGroup holder)
        {
            foreach (var model in models)
            {
                Log.Information($"Processing model: {model.Name}");
                Log.Information($"  Vertices: {model.Vertices?.Length ?? 0}");
                Log.Information($"  Indices: {model.Indices?.Length ?? 0}");
                Log.Information($"  Has texture: {model.TextureData != null}");

                if (model.TextureData != null)
                {
                    Log.Information($"  Texture: {model.TextureData.Width}x{model.TextureData.Height}");
                }

                var meshObject = new MeshObject
                {
                    Position = model.LocalMatrix.Translation,
                    Scale = model.LocalMatrix.GetScale(),
                    Rotation = model.LocalMatrix.GetRotation(),
                };
                meshObject.Setup(_gl, model);
                holder.Add(meshObject);
            }
        
            // Очистка временных кешей
            ModelLoader.ClearAllCaches();

            // Принудительная сборка мусора после обработки
            MemoryManager.PerformAggressiveCleanup();
        }

        internal void UpdateLook()
        {
            LookChanged?.Invoke(this, _lookState);
        }
    }
}
