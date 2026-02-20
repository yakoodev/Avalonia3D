namespace Avalonia3D.Sandbox.Rendering;

public enum FramebufferResolutionMode
{
    PreferIncoming,
    ForceDefault
}

public sealed class FramebufferTargetResolver
{
    private readonly int _switchStabilizationFrames;
    private readonly FramebufferResolutionMode _mode;
    private int? _stableFramebufferId;
    private int? _candidateFramebufferId;
    private int _candidateHitCount;

    public FramebufferTargetResolver(
        FramebufferResolutionMode mode = FramebufferResolutionMode.PreferIncoming,
        int switchStabilizationFrames = 2)
    {
        _mode = mode;
        _switchStabilizationFrames = switchStabilizationFrames < 1 ? 1 : switchStabilizationFrames;
    }

    public void Reset()
    {
        _stableFramebufferId = null;
        _candidateFramebufferId = null;
        _candidateHitCount = 0;
    }

    public uint Resolve(int incomingFramebufferId)
    {
        if (_mode == FramebufferResolutionMode.ForceDefault)
        {
            _stableFramebufferId = 0;
            ResetCandidate();
            return 0;
        }

        var normalizedIncoming = incomingFramebufferId >= 0
            ? incomingFramebufferId
            : _stableFramebufferId ?? 0;

        if (!_stableFramebufferId.HasValue)
        {
            _stableFramebufferId = normalizedIncoming;
            return (uint)_stableFramebufferId.Value;
        }

        if (normalizedIncoming == _stableFramebufferId.Value)
        {
            ResetCandidate();
            return (uint)_stableFramebufferId.Value;
        }

        if (IsSwitchBetweenDefaultAndOffscreen(_stableFramebufferId.Value, normalizedIncoming))
        {
            RegisterCandidate(normalizedIncoming);
            if (_candidateHitCount < _switchStabilizationFrames)
            {
                return (uint)_stableFramebufferId.Value;
            }
        }

        _stableFramebufferId = normalizedIncoming;
        ResetCandidate();
        return (uint)_stableFramebufferId.Value;
    }

    private static bool IsSwitchBetweenDefaultAndOffscreen(int currentFramebufferId, int nextFramebufferId)
    {
        return (currentFramebufferId == 0 && nextFramebufferId > 0)
            || (currentFramebufferId > 0 && nextFramebufferId == 0);
    }

    private void RegisterCandidate(int candidateFramebufferId)
    {
        if (_candidateFramebufferId == candidateFramebufferId)
        {
            _candidateHitCount++;
            return;
        }

        _candidateFramebufferId = candidateFramebufferId;
        _candidateHitCount = 1;
    }

    private void ResetCandidate()
    {
        _candidateFramebufferId = null;
        _candidateHitCount = 0;
    }
}
