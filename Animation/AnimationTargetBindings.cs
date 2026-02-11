using System;
using System.Collections.Generic;
using System.Numerics;
using Avalonia3D.Model;
using Avalonia3D.Model.StandObjects;
using Avalonia3D.Rendering;
using Serilog;

namespace Avalonia3D.Animation
{
    public sealed class NodeTransformBinding : IAnimationTargetBinding
    {
        private readonly string _nodeKey;
        private readonly AnimationTargetProperty _property;
        private SceneNode? _node;

        public NodeTransformBinding(string nodeKey, AnimationTargetProperty property)
        {
            _nodeKey = nodeKey;
            _property = property;
        }

        public void Rebind(SceneGraph sceneGraph)
        {
            _node = sceneGraph.FindNodeByKey(_nodeKey);
        }

        public void Apply(AnimationChannel channel, float time)
        {
            if (_node == null)
            {
                return;
            }

            switch (_property)
            {
                case AnimationTargetProperty.Position when channel.Vector3Keyframes.Count > 0:
                    _node.Position = channel.SampleVector3(time);
                    break;
                case AnimationTargetProperty.Scale when channel.Vector3Keyframes.Count > 0:
                    _node.Scale = channel.SampleVector3(time);
                    break;
                case AnimationTargetProperty.Rotation when channel.QuaternionKeyframes.Count > 0:
                    _node.Rotation = channel.SampleQuaternion(time);
                    break;
            }
        }
    }

    public sealed class NodeMorphBinding : IAnimationTargetBinding
    {
        private readonly string _nodeKey;
        private SceneNode? _node;
        private IReadOnlyList<MeshObject> _targets = [];
        private bool _loggedBinding;
        private bool _loggedMissingTargets;

        public NodeMorphBinding(string nodeKey)
        {
            _nodeKey = nodeKey;
        }

        public void Rebind(SceneGraph sceneGraph)
        {
            _node = sceneGraph.FindNodeByKey(_nodeKey);
            var resolver = new AnimationMorphTargetResolver(sceneGraph);
            _targets = resolver.ResolveTargets(_nodeKey);
            _loggedBinding = false;
            _loggedMissingTargets = false;
        }

        public void Apply(AnimationChannel channel, float time)
        {
            if (_node == null || channel.FloatArrayKeyframes.Count == 0)
            {
                return;
            }

            var weights = channel.SampleFloatArray(time);
            _node.MorphWeights = weights;

            if (!_loggedBinding)
            {
                _loggedBinding = true;
                Log.Debug("Applying morph channel for node '{NodeKey}': weights={WeightCount}, keyframes={KeyframeCount}",
                    _nodeKey,
                    weights?.Length ?? 0,
                    channel.FloatArrayKeyframes.Count);
            }

            if (_targets.Count == 0 && !_loggedMissingTargets)
            {
                _loggedMissingTargets = true;
                Log.Warning("Morph channel for node '{NodeKey}' resolved no mesh targets at runtime.", _nodeKey);
            }

            foreach (var target in _targets)
            {
                target.SetMorphWeights(weights);
            }
        }
    }

    public sealed class MaterialPropertyBinding : IAnimationTargetBinding
    {
        private readonly string _materialKey;
        private readonly AnimationTargetProperty _property;
        private IReadOnlyList<MeshObject> _targets = [];

        public MaterialPropertyBinding(string materialKey, AnimationTargetProperty property)
        {
            _materialKey = materialKey;
            _property = property;
        }

        public void Rebind(SceneGraph sceneGraph)
        {
            var resolver = new AnimationMaterialTargetResolver(sceneGraph);
            _targets = resolver.ResolveByMaterialKey(_materialKey);
        }

        public void Apply(AnimationChannel channel, float time)
        {
            if (_targets.Count == 0)
            {
                return;
            }

            foreach (var target in _targets)
            {
                var material = target.Material;
                if (material == null)
                {
                    continue;
                }

                switch (_property)
                {
                    case AnimationTargetProperty.EmissiveIntensity when channel.FloatKeyframes.Count > 0:
                        material.EmissiveIntensity = channel.SampleFloat(time);
                        break;
                    case AnimationTargetProperty.EmissiveColor when channel.Vector3Keyframes.Count > 0:
                        material.EmissiveFactor = channel.SampleVector3(time);
                        break;
                    case AnimationTargetProperty.BaseColorFactor when channel.Vector3Keyframes.Count > 0:
                        var color = channel.SampleVector3(time);
                        material.BaseColorFactor = new Vector4(color, material.BaseColorFactor.W);
                        break;
                }
            }
        }
    }

    public sealed class NodeMaterialPropertyBinding : IAnimationTargetBinding
    {
        private readonly string _nodeKey;
        private readonly AnimationTargetProperty _property;
        private MeshObject? _target;

        public NodeMaterialPropertyBinding(string nodeKey, AnimationTargetProperty property)
        {
            _nodeKey = nodeKey;
            _property = property;
        }

        public void Rebind(SceneGraph sceneGraph)
        {
            var resolver = new AnimationMaterialTargetResolver(sceneGraph);
            _target = resolver.ResolveByNodeKey(_nodeKey);
        }

        public void Apply(AnimationChannel channel, float time)
        {
            var material = _target?.Material;
            if (material == null)
            {
                return;
            }

            switch (_property)
            {
                case AnimationTargetProperty.EmissiveIntensity when channel.FloatKeyframes.Count > 0:
                    material.EmissiveIntensity = channel.SampleFloat(time);
                    break;
                case AnimationTargetProperty.EmissiveColor when channel.Vector3Keyframes.Count > 0:
                    material.EmissiveFactor = channel.SampleVector3(time);
                    break;
                case AnimationTargetProperty.BaseColorFactor when channel.Vector3Keyframes.Count > 0:
                    var color = channel.SampleVector3(time);
                    material.BaseColorFactor = new Vector4(color, material.BaseColorFactor.W);
                    break;
            }
        }
    }

    public enum TextureSlot
    {
        BaseColor,
        Emissive,
        Normal,
        MetallicRoughness,
        Occlusion
    }

    public sealed class TexturePropertyBinding : IAnimationTargetBinding
    {
        private readonly string _materialKey;
        private readonly TextureSlot _slot;
        private readonly AnimationTargetProperty _property;
        private IReadOnlyList<MeshObject> _targets = [];

        public TexturePropertyBinding(string materialKey, TextureSlot slot, AnimationTargetProperty property)
        {
            _materialKey = materialKey;
            _slot = slot;
            _property = property;
        }

        public void Rebind(SceneGraph sceneGraph)
        {
            var resolver = new AnimationMaterialTargetResolver(sceneGraph);
            _targets = resolver.ResolveByMaterialKey(_materialKey);
        }

        public void Apply(AnimationChannel channel, float time)
        {
            foreach (var target in _targets)
            {
                var texture = ResolveTexture(target.Material);
                if (texture == null)
                {
                    continue;
                }

                var runtimeTransform = target.Material?.TextureRuntime.GetOrCreate(MapTextureSemantic(_slot));

                switch (_property)
                {
                    case AnimationTargetProperty.TextureTransformOffset when channel.Vector3Keyframes.Count > 0:
                        var offset = channel.SampleVector3(time);
                        var uvOffset = new Vector2(offset.X, offset.Y);
                        texture.Transform.Offset = uvOffset;
                        if (runtimeTransform != null)
                        {
                            runtimeTransform.UvOffset = uvOffset;
                        }
                        break;
                    case AnimationTargetProperty.TextureTransformScale when channel.Vector3Keyframes.Count > 0:
                        var scale = channel.SampleVector3(time);
                        var uvScale = new Vector2(scale.X, scale.Y);
                        texture.Transform.Scale = uvScale;
                        if (runtimeTransform != null)
                        {
                            runtimeTransform.UvScale = uvScale;
                        }
                        break;
                    case AnimationTargetProperty.TextureTransformRotation when channel.FloatKeyframes.Count > 0:
                        var rotation = channel.SampleFloat(time);
                        texture.Transform.Rotation = rotation;
                        if (runtimeTransform != null)
                        {
                            runtimeTransform.UvRotation = rotation;
                        }
                        break;
                    case AnimationTargetProperty.TextureTransformTexCoord when channel.FloatKeyframes.Count > 0:
                        texture.Transform.TexCoord = (int)MathF.Round(channel.SampleFloat(time));
                        break;
                }
            }
        }

        private static TextureSemantic MapTextureSemantic(TextureSlot slot)
        {
            return slot switch
            {
                TextureSlot.BaseColor => TextureSemantic.BaseColor,
                TextureSlot.Emissive => TextureSemantic.Emissive,
                TextureSlot.Normal => TextureSemantic.Normal,
                TextureSlot.MetallicRoughness => TextureSemantic.MetallicRoughness,
                TextureSlot.Occlusion => TextureSemantic.Occlusion,
                _ => TextureSemantic.Extension
            };
        }

        private TextureData? ResolveTexture(Material? material)
        {
            if (material == null)
            {
                return null;
            }

            return _slot switch
            {
                TextureSlot.BaseColor => material.BaseColorTexture,
                TextureSlot.Emissive => material.EmissiveTexture,
                TextureSlot.Normal => material.NormalTexture,
                TextureSlot.MetallicRoughness => material.MetallicRoughnessTexture,
                TextureSlot.Occlusion => material.OcclusionTexture,
                _ => null
            };
        }
    }
}
