using Avalonia3D.Animation;
using System;
using System.Collections.Generic;

namespace Avalonia3D.Loaders;

internal enum GltfAnimationPointerValueType
{
    Float,
    Vec3,
    Weights
}

internal enum GltfAnimationPointerTargetKind
{
    Node,
    Material,
    Texture
}

internal readonly record struct GltfAnimationPointerRegistration(
    string PointerPattern,
    GltfAnimationPointerValueType ValueType,
    GltfAnimationPointerTargetKind TargetKind,
    AnimationTargetProperty RuntimeProperty,
    TextureSlot? TextureSlot = null);

internal static class GltfAnimationPointerRegistry
{
    private static readonly IReadOnlyList<GltfAnimationPointerRegistration> ExactSuffixRegistrations =
    [
        new("/materials/*/emissiveFactor", GltfAnimationPointerValueType.Vec3, GltfAnimationPointerTargetKind.Material, AnimationTargetProperty.EmissiveColor),
        new("/materials/*/extensions/KHR_materials_emissive_strength/emissiveStrength", GltfAnimationPointerValueType.Float, GltfAnimationPointerTargetKind.Material, AnimationTargetProperty.EmissiveIntensity),
        new("/materials/*/emissiveStrength", GltfAnimationPointerValueType.Float, GltfAnimationPointerTargetKind.Material, AnimationTargetProperty.EmissiveIntensity),
        new("/materials/*/emissiveIntensity", GltfAnimationPointerValueType.Float, GltfAnimationPointerTargetKind.Material, AnimationTargetProperty.EmissiveIntensity),
        new("/materials/*/pbrMetallicRoughness/baseColorFactor", GltfAnimationPointerValueType.Vec3, GltfAnimationPointerTargetKind.Material, AnimationTargetProperty.BaseColorFactor),
        new("/materials/*/baseColorFactor", GltfAnimationPointerValueType.Vec3, GltfAnimationPointerTargetKind.Material, AnimationTargetProperty.BaseColorFactor)
    ];

    private static readonly (string PointerPatternPrefix, TextureSlot Slot)[] TextureSlotRegistrations =
    [
        ("/materials/*/pbrMetallicRoughness/baseColorTexture/extensions/KHR_texture_transform", TextureSlot.BaseColor),
        ("/materials/*/emissiveTexture/extensions/KHR_texture_transform", TextureSlot.Emissive)
    ];

    private static readonly (string PointerPatternSuffix, GltfAnimationPointerValueType ValueType, AnimationTargetProperty RuntimeProperty)[] TextureTransformPropertyRegistrations =
    [
        ("/offset", GltfAnimationPointerValueType.Vec3, AnimationTargetProperty.TextureTransformOffset),
        ("/scale", GltfAnimationPointerValueType.Vec3, AnimationTargetProperty.TextureTransformScale),
        ("/rotation", GltfAnimationPointerValueType.Float, AnimationTargetProperty.TextureTransformRotation)
    ];

    internal static bool TryResolve(string? pointerPath, out GltfAnimationPointerRegistration registration)
    {
        registration = default;

        if (!IsMaterialPointer(pointerPath))
        {
            return false;
        }

        foreach (var candidate in ExactSuffixRegistrations)
        {
            if (PointerMatchesSuffix(pointerPath!, candidate.PointerPattern))
            {
                registration = candidate;
                return true;
            }
        }

        if (TryResolveTextureTransform(pointerPath!, out registration))
        {
            return true;
        }

        return false;
    }

    private static bool IsMaterialPointer(string? pointerPath)
    {
        return !string.IsNullOrWhiteSpace(pointerPath)
            && pointerPath.Contains("/materials/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool PointerMatchesSuffix(string pointerPath, string pointerPattern)
    {
        return MatchesPattern(pointerPath, pointerPattern);
    }

    private static bool MatchesPattern(string pointerPath, string pointerPattern)
    {
        const string wildcard = "*";
        var wildcardIndex = pointerPattern.IndexOf(wildcard, StringComparison.Ordinal);
        if (wildcardIndex < 0)
        {
            return pointerPath.Equals(pointerPattern, StringComparison.OrdinalIgnoreCase);
        }

        var prefix = pointerPattern[..wildcardIndex];
        var suffix = pointerPattern[(wildcardIndex + wildcard.Length)..];
        return pointerPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && pointerPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            && pointerPath.Length >= prefix.Length + suffix.Length;
    }

    private static bool TryResolveTextureTransform(string pointerPath, out GltfAnimationPointerRegistration registration)
    {
        registration = default;

        if (!pointerPath.Contains("KHR_texture_transform", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        TextureSlot? slot = null;
        string? slotPattern = null;
        foreach (var (patternPrefix, registeredSlot) in TextureSlotRegistrations)
        {
            if (!MatchesPatternPrefix(pointerPath, patternPrefix))
            {
                continue;
            }

            slot = registeredSlot;
            slotPattern = patternPrefix;
            break;
        }

        if (slot == null || slotPattern == null)
        {
            return false;
        }

        foreach (var (propertySuffix, valueType, runtimeProperty) in TextureTransformPropertyRegistrations)
        {
            if (!pointerPath.EndsWith(propertySuffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            registration = new GltfAnimationPointerRegistration(
                $"{slotPattern}{propertySuffix}",
                valueType,
                GltfAnimationPointerTargetKind.Texture,
                runtimeProperty,
                slot);

            return true;
        }

        return false;
    }

    private static bool MatchesPatternPrefix(string pointerPath, string pointerPatternPrefix)
    {
        const string wildcard = "*";
        var wildcardIndex = pointerPatternPrefix.IndexOf(wildcard, StringComparison.Ordinal);
        if (wildcardIndex < 0)
        {
            return pointerPath.StartsWith(pointerPatternPrefix, StringComparison.OrdinalIgnoreCase);
        }

        var prefix = pointerPatternPrefix[..wildcardIndex];
        var suffix = pointerPatternPrefix[(wildcardIndex + wildcard.Length)..];

        if (!pointerPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffixIndex = pointerPath.IndexOf(suffix, prefix.Length, StringComparison.OrdinalIgnoreCase);
        return suffixIndex >= prefix.Length;
    }
}
