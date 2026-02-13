using System;

namespace Avalonia3D.Memory
{
    public sealed class DefaultMemoryPressurePolicy : IMemoryPressurePolicy
    {
        public static DefaultMemoryPressurePolicy Instance { get; } = new();

        private DefaultMemoryPressurePolicy()
        {
        }

        public void NotifyActivity(string source)
        {
            MemoryManager.MarkActivity();
        }

        public void OnMemoryPressure(string source)
        {
            MemoryManager.RequestMemoryPressureCleanup(source);
        }

        public void OnImportCompleted(string source)
        {
            MemoryManager.RequestMemoryPressureCleanup($"{source}:import-complete");
        }
    }
}
