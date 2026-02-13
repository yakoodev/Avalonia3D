using System;

namespace Avalonia3D.Memory
{
    public interface IMemoryPressurePolicy
    {
        void NotifyActivity(string source);
        void OnMemoryPressure(string source);
        void OnImportCompleted(string source);
    }
}
