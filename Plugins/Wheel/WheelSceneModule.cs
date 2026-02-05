using Avalonia3D.Interfaces;
using Avalonia3D.Loaders;
using Avalonia3D.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Avalonia3D.Plugins.Wheel
{
    public class WheelSceneModule : ISceneModule
    {
        private Scene3D? _scene;

        public Wheel? Wheel { get; private set; }
        public WheelCut? WheelCut { get; private set; }

        public IReadOnlyList<Weigth> Weigths => Wheel?.Weigths ?? Array.Empty<Weigth>().ToList();

        public void Attach(Scene3D scene)
        {
            _scene = scene;
        }

        public void Detach(Scene3D scene)
        {
            if (_scene == scene)
            {
                _scene = null;
            }
        }

        public SceneGraph Load(string source, GltfSceneImporter importer)
        {
            if (_scene == null)
            {
                throw new InvalidOperationException("Module is not attached to a Scene3D instance.");
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException("Source path is empty.", nameof(source));
            }

            if (importer == null)
            {
                throw new ArgumentNullException(nameof(importer));
            }

            _scene.ResetSceneGraph();

            Wheel = new Wheel(_scene);
            WheelCut = new WheelCut(_scene);

            var wheelPath = Path.Combine(source, "wheel.glb");
            Wheel.Name = Path.GetFileNameWithoutExtension(wheelPath);
            importer.ImportInto(wheelPath, Wheel);
            _scene.SceneGraph.AddRoot(Wheel);

            var glueWeigthOutside = new GlueWeigthOutside(Wheel);
            var glueWeigthOutsidePath = Path.Combine(source, "GlueWeigthOutside.glb");
            glueWeigthOutside.Name = Path.GetFileNameWithoutExtension(glueWeigthOutsidePath);
            importer.ImportInto(glueWeigthOutsidePath, glueWeigthOutside);
            _scene.SceneGraph.AddRoot(glueWeigthOutside);

            var glueWeigthInside = new GlueWeigthInside(Wheel);
            var glueWeigthInsidePath = Path.Combine(source, "GlueWeigthInside.glb");
            glueWeigthInside.Name = Path.GetFileNameWithoutExtension(glueWeigthInsidePath);
            importer.ImportInto(glueWeigthInsidePath, glueWeigthInside);
            _scene.SceneGraph.AddRoot(glueWeigthInside);

            var springWeigthInside = new SpringWeigthInside(Wheel);
            var springWeigthInsidePath = Path.Combine(source, "SpringWeigthInside.glb");
            springWeigthInside.Name = Path.GetFileNameWithoutExtension(springWeigthInsidePath);
            importer.ImportInto(springWeigthInsidePath, springWeigthInside);
            _scene.SceneGraph.AddRoot(springWeigthInside);

            var springWeigthOutside = new SpringWeigthOutside(Wheel);
            var springWeigthOutsidePath = Path.Combine(source, "SpringWeigthOutside.glb");
            springWeigthOutside.Name = Path.GetFileNameWithoutExtension(springWeigthOutsidePath);
            importer.ImportInto(springWeigthOutsidePath, springWeigthOutside);
            _scene.SceneGraph.AddRoot(springWeigthOutside);

            var springWeigthInnerOutside = new SpringWeigthInnerOutside(Wheel);
            var springWeigthInnerOutsidePath = Path.Combine(source, "SpringWeigthInnerOutside.glb");
            springWeigthInnerOutside.Name = Path.GetFileNameWithoutExtension(springWeigthInnerOutsidePath);
            importer.ImportInto(springWeigthInnerOutsidePath, springWeigthInnerOutside);
            _scene.SceneGraph.AddRoot(springWeigthInnerOutside);

            Wheel.Weigths.Add(glueWeigthInside);
            Wheel.Weigths.Add(glueWeigthOutside);
            Wheel.Weigths.Add(springWeigthInside);
            Wheel.Weigths.Add(springWeigthOutside);
            Wheel.Weigths.Add(springWeigthInnerOutside);

            _scene.BuildRenderResources();
            _scene.UpdateLook();

            return _scene.SceneGraph;
        }
    }
}
