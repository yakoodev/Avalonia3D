using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Avalonia3D.Model;

namespace Avalonia3D.Animation
{
    public readonly record struct ClipPlaybackState(string ClipName, bool IsRegistered, bool IsPlaying, bool IsPaused, bool Loop, float Speed, float Time, float Duration);

    public sealed class ClipPlaybackCompletedEventArgs : EventArgs
    {
        public ClipPlaybackCompletedEventArgs(string clipName)
        {
            ClipName = clipName;
        }

        public string ClipName { get; }
    }

    public class AnimatorComponent
    {
        private readonly Animator _animator;
        private SceneGraph _sceneGraph;
        private readonly Dictionary<string, AnimationClip> _clips = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AnimationClipPlayer> _activePlayers = new(StringComparer.Ordinal);

        public event EventHandler<ClipPlaybackCompletedEventArgs>? ClipCompleted;
        public bool HasActiveClips => _activePlayers.Count > 0;

        public AnimatorComponent(SceneGraph sceneGraph, Animator animator)
        {
            _sceneGraph = sceneGraph ?? throw new ArgumentNullException(nameof(sceneGraph));
            _animator = animator ?? throw new ArgumentNullException(nameof(animator));
        }

        public void SetSceneGraph(SceneGraph sceneGraph)
        {
            _sceneGraph = sceneGraph ?? throw new ArgumentNullException(nameof(sceneGraph));
            foreach (var player in _activePlayers.Values)
            {
                player.SetSceneGraph(_sceneGraph);
            }
        }

        public void ResetForScene(SceneGraph sceneGraph)
        {
            _sceneGraph = sceneGraph ?? throw new ArgumentNullException(nameof(sceneGraph));
            _activePlayers.Clear();
            _clips.Clear();
            _animator.Clear();
        }

        public void RegisterClip(AnimationClip clip)
        {
            if (clip == null || string.IsNullOrWhiteSpace(clip.Name))
            {
                return;
            }

            _clips[clip.Name] = clip;
        }

        public IReadOnlyList<string> GetClipNames()
        {
            return _clips.Keys.OrderBy(static x => x, StringComparer.Ordinal).ToArray();
        }

        public ClipPlaybackState GetClipState(string clipName)
        {
            if (string.IsNullOrWhiteSpace(clipName))
            {
                return default;
            }

            if (!_clips.TryGetValue(clipName, out var clip))
            {
                return new ClipPlaybackState(clipName, false, false, false, false, 1f, 0f, 0f);
            }

            if (_activePlayers.TryGetValue(clipName, out var player))
            {
                return new ClipPlaybackState(
                    clipName,
                    true,
                    true,
                    player.IsPaused,
                    player.Loop,
                    player.Speed,
                    player.Time,
                    clip.Duration);
            }

            return new ClipPlaybackState(clipName, true, false, false, false, 1f, 0f, clip.Duration);
        }

        public bool PlayClip(string clipName, bool loop = false, float speed = 1f)
        {
            if (string.IsNullOrWhiteSpace(clipName))
            {
                return false;
            }

            if (!_clips.TryGetValue(clipName, out var clip))
            {
                return false;
            }

            if (_activePlayers.TryGetValue(clipName, out var existing))
            {
                existing.Play(loop, speed);
                return true;
            }

            var player = new AnimationClipPlayer(clip, _sceneGraph, OnPlayerCompleted);
            player.Play(loop, speed);
            if (!_animator.TryAdd(player))
            {
                return false;
            }

            _activePlayers[clipName] = player;
            return true;
        }

        public bool PauseClip(string clipName)
        {
            if (_activePlayers.TryGetValue(clipName, out var player))
            {
                player.Pause();
                return true;
            }

            return false;
        }

        public bool ResumeClip(string clipName)
        {
            if (_activePlayers.TryGetValue(clipName, out var player))
            {
                player.Resume();
                return true;
            }

            return false;
        }

        public bool StopClip(string clipName)
        {
            if (_activePlayers.TryGetValue(clipName, out var player))
            {
                player.Stop();
                _activePlayers.Remove(clipName);
                return true;
            }

            return false;
        }

        public bool SetNodePosition(string nodeName, Vector3 position)
        {
            var node = _sceneGraph.FindNodeByKey(nodeName);
            if (node == null)
            {
                return false;
            }

            node.Position = position;
            return true;
        }

        public bool SetNodeRotation(string nodeName, Quaternion rotation)
        {
            var node = _sceneGraph.FindNodeByKey(nodeName);
            if (node == null)
            {
                return false;
            }

            node.Rotation = rotation;
            return true;
        }

        public bool SetNodeScale(string nodeName, Vector3 scale)
        {
            var node = _sceneGraph.FindNodeByKey(nodeName);
            if (node == null)
            {
                return false;
            }

            node.Scale = scale;
            return true;
        }


        public bool SetNodeMaterialEmissiveIntensity(string nodeName, float intensity)
        {
            if (string.IsNullOrWhiteSpace(nodeName))
            {
                return false;
            }

            var resolver = new AnimationMaterialTargetResolver(_sceneGraph);
            var target = resolver.ResolveByNodeKey(nodeName);
            if (target?.Material == null)
            {
                return false;
            }

            target.Material.EmissiveIntensity = intensity;
            return true;
        }

        public bool SetNodeMaterialEmissiveColor(string nodeName, Vector3 emissiveColor)
        {
            if (string.IsNullOrWhiteSpace(nodeName))
            {
                return false;
            }

            var resolver = new AnimationMaterialTargetResolver(_sceneGraph);
            var target = resolver.ResolveByNodeKey(nodeName);
            if (target?.Material == null)
            {
                return false;
            }

            target.Material.EmissiveFactor = emissiveColor;
            return true;
        }

        private void OnPlayerCompleted(AnimationClipPlayer player)
        {
            if (player == null)
            {
                return;
            }

            _activePlayers.Remove(player.Clip.Name);
            ClipCompleted?.Invoke(this, new ClipPlaybackCompletedEventArgs(player.Clip.Name));
        }
    }
}
