// File: MemoryManager.cs
using Avalonia3D.Rendering;
using Serilog;
using System;
using System.Buffers;
using System.Runtime;
using System.Threading;

namespace Avalonia3D.Memory
{
    public static class MemoryManager
    {
        private static Timer _cleanupTimer;
        private static readonly object _lock = new();
        private static bool _isInitialized = false;
        private static RenderResourceManager _resourceManager;

        // Настройки управления памятью
        public static class Settings
        {
            public static TimeSpan CacheCleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
            public static TimeSpan MaxCacheEntryAge { get; set; } = TimeSpan.FromMinutes(10);
            public static int MaxTextureDimension { get; set; } = 512;
            public static long MaxTotalMemoryMB { get; set; } = 500; // 500 MB лимит
            public static bool AggressiveGC { get; set; } = true;
        }

        public static void Initialize(RenderResourceManager resourceManager)
        {
            lock (_lock)
            {
                if (_isInitialized) return;

                _resourceManager = resourceManager;

                // Настройка сборщика мусора для 3D приложений
                GCSettings.LatencyMode = GCLatencyMode.Interactive;

                // Запуск таймера очистки кеша
                _cleanupTimer = new Timer(PerformCleanup, null,
                    Settings.CacheCleanupInterval,
                    Settings.CacheCleanupInterval);

                _isInitialized = true;
                LogMemoryState("MemoryManager Initialized");
            }
        }

        public static void Shutdown()
        {
            lock (_lock)
            {
                _cleanupTimer?.Dispose();
                _cleanupTimer = null;

                _resourceManager?.ClearAll();

                // Финальная очистка
                PerformAggressiveCleanup();
                _resourceManager = null;
                _isInitialized = false;
            }
        }

        private static void PerformCleanup(object state)
        {
            try
            {
                // Очистка старых записей в кеше геометрии
                _resourceManager?.CleanupOldCacheEntries(Settings.MaxCacheEntryAge);

                // Проверка общего потребления памяти
                var currentMemoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

                if (currentMemoryMB > Settings.MaxTotalMemoryMB)
                {
                     Log.Information($"Memory usage too high: {currentMemoryMB:F2} MB, performing cleanup...");
                    PerformAggressiveCleanup();
                }

                // Регулярная сборка мусора для поколения 0
                if (Settings.AggressiveGC)
                {
                    GC.Collect(0, GCCollectionMode.Optimized);
                }
            }
            catch (Exception ex)
            {
                 Log.Information($"Error during memory cleanup: {ex.Message}");
            }
        }

        public static void PerformAggressiveCleanup()
        {
             Log.Information("Performing aggressive memory cleanup...");

            var beforeMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

            // Принудительная очистка ArrayPool'ов
            ArrayPool<byte>.Shared.Return(ArrayPool<byte>.Shared.Rent(1), true);
            ArrayPool<ushort>.Shared.Return(ArrayPool<ushort>.Shared.Rent(1), true);

            // Агрессивная сборка мусора
            for (int i = 0; i < 3; i++)
            {
                GC.Collect(2, GCCollectionMode.Aggressive);
                GC.WaitForPendingFinalizers();
            }

            // Компактификация Large Object Heap если доступна
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive);

            var afterMB = GC.GetTotalMemory(true) / 1024.0 / 1024.0;
            var freedMB = beforeMB - afterMB;

             Log.Information($"Memory cleanup complete: {freedMB:F2} MB freed ({beforeMB:F2} -> {afterMB:F2} MB)");
        }

        public static void LogMemoryState(string context = "")
        {
            var totalMemory = GC.GetTotalMemory(false);
            var totalMemoryMB = totalMemory / 1024.0 / 1024.0;

            Log.Information($"=== Memory State {context} ===");
            Log.Information($"Total Memory: {totalMemoryMB:F2} MB ({totalMemory:N0} bytes)");
            Log.Information($"Gen 0 Collections: {GC.CollectionCount(0)}");
             Log.Information($"Gen 1 Collections: {GC.CollectionCount(1)}");
             Log.Information($"Gen 2 Collections: {GC.CollectionCount(2)}");

            _resourceManager?.LogCacheStats();

            // Проверка лимитов
            if (totalMemoryMB > Settings.MaxTotalMemoryMB)
            {
                 Log.Information($"⚠️  WARNING: Memory usage ({totalMemoryMB:F2} MB) exceeds limit ({Settings.MaxTotalMemoryMB} MB)!");
            }

             Log.Information("================================");
        }

        // Утилиты для профилирования
        public static IDisposable StartMemoryProfiling(string operationName)
        {
            return new MemoryProfiler(operationName);
        }

        private class MemoryProfiler : IDisposable
        {
            private readonly string _operationName;
            private readonly long _startMemory;
            private readonly DateTime _startTime;

            public MemoryProfiler(string operationName)
            {
                _operationName = operationName;
                _startMemory = GC.GetTotalMemory(false);
                _startTime = DateTime.UtcNow;

                 Log.Information($"🔍 Starting memory profiling: {_operationName}");
                 Log.Information($"   Initial memory: {_startMemory / 1024.0 / 1024.0:F2} MB");
            }

            public void Dispose()
            {
                var endMemory = GC.GetTotalMemory(false);
                var duration = DateTime.UtcNow - _startTime;
                var memoryDelta = endMemory - _startMemory;
                var memoryDeltaMB = memoryDelta / 1024.0 / 1024.0;

                 Log.Information($"✅ Memory profiling complete: {_operationName}");
                 Log.Information($"   Duration: {duration.TotalMilliseconds:F2} ms");
                 Log.Information($"   Final memory: {endMemory / 1024.0 / 1024.0:F2} MB");
                 Log.Information($"   Memory delta: {memoryDeltaMB:+F2} MB ({memoryDelta:+N0} bytes)");

                if (memoryDeltaMB > 50) // Больше 50 MB
                {
                     Log.Information($"⚠️  High memory usage detected for operation: {_operationName}");
                }
            }
        }

        // Методы для оптимизации конкретных операций
        public static void OptimizeForLargeModel()
        {
            // Подготовка к загрузке большой модели
            GC.Collect(2, GCCollectionMode.Aggressive);
            GC.WaitForPendingFinalizers();

            // Устанавливаем более консервативные настройки
            var oldMaxDim = Settings.MaxTextureDimension;
            Settings.MaxTextureDimension = Math.Min(oldMaxDim, 256);

             Log.Information("Optimized settings for large model loading");
        }

        public static void RestoreNormalSettings()
        {
            Settings.MaxTextureDimension = 512;
             Log.Information("Restored normal memory settings");
        }
    }
}
