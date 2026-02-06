using Avalonia3D.Interfaces;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;

namespace Avalonia3D.Rendering;

public sealed class ShaderRegistry
{
    private readonly Dictionary<string, Func<GL, IShader3D>> _factories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IShader3D> _instances = new(StringComparer.Ordinal);
    private string? _defaultShaderId;

    public string? DefaultShaderId => _defaultShaderId;

    public void Register(string id, Func<GL, IShader3D> factory)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Shader id cannot be null or whitespace.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(factory);
        _factories[id] = factory;
    }

    public void RegisterInstance(string id, IShader3D shader)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Shader id cannot be null or whitespace.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(shader);
        _instances[id] = shader;
        _factories[id] = _ => shader;
    }

    public bool Contains(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        return _factories.ContainsKey(id);
    }

    public void SetDefault(string id)
    {
        if (!Contains(id))
        {
            throw new InvalidOperationException($"Shader '{id}' is not registered.");
        }

        _defaultShaderId = id;
    }

    public IShader3D? Get(string id, GL? gl = null)
    {
        if (string.IsNullOrWhiteSpace(id) || !_factories.TryGetValue(id, out var factory))
        {
            return null;
        }

        if (_instances.TryGetValue(id, out var cached))
        {
            return cached;
        }

        if (gl == null)
        {
            return null;
        }

        var shader = factory(gl);
        _instances[id] = shader;
        return shader;
    }

    public IShader3D? GetDefault(GL? gl = null)
    {
        if (_defaultShaderId == null)
        {
            return null;
        }

        return Get(_defaultShaderId, gl);
    }
}
