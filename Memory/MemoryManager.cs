// File: MemoryManager.cs
using Avalonia3D.Rendering;
using Serilog;
using System;
using System.Runtime;
using System.Threading;

namespace Avalonia3D.Memory
{
    public static class MemoryManager
    {
        private static Timer? _cleanupTimer;
        private static readonly object _lock = new();
        private static bool _isInitialized = false;
        private static RenderResourceManager? _resourceManager;

        // Настройки управления памятью
        public static class Settings
        {
            public static TimeSpan CacheCleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
            public static TimeSpan MaxCacheEntryAge { get; set; } = TimeSpan.FromMinutes(10);
            public static int MaxTextureDimension { get; set; } = 512;
            public static long MaxDecodedTextureCacheMemoryMB { get; set; } = 128;
            public static bool PersistOriginalTextureBytes { get; set; } = false;
            public static long MaxPersistedTextureCacheMemoryMB { get; set; } = 64;
            public static TimeSpan PersistedTextureCacheMaxAge { get; set; } = TimeSpan.FromDays(1);
            public static long MaxTotalMemoryMB { get; set; } = 500; // 500 MB лимит
            public static bool AggressiveGC { get; set; } = false;
            public static TimeSpan CleanupCooldown { get; set; } = TimeSpan.FromSeconds(30);
            public static TimeSpan IdleThreshold { get; set; } = TimeSpan.FromSeconds(15);
            public static double HighPressureFactor { get; set; } = 1.15;
        }

        private static DateTime _lastCleanupUtc = DateTime.MinValue;
        private static DateTime _lastActivityUtc = DateTime.MinValue;
        private static Timer? _deferredCleanupTimer;
        private static string? _deferredCleanupSource;

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

                _lastActivityUtc = DateTime.UtcNow;
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

                _deferredCleanupTimer?.Dispose();
                _deferredCleanupTimer = null;
                _deferredCleanupSource = null;

                _resourceManager?.ClearAll();

                PerformSoftCleanup("shutdown");
                _resourceManager = null;
                _isInitialized = false;
            }
        }

        private static void PerformCleanup(object? state)
        {
            try
            {
                // Очистка старых записей в кеше геометрии
                _resourceManager?.CleanupOldCacheEntries(Settings.MaxCacheEntryAge);

                // Проверка общего потребления памяти
                var currentMemoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

                if (currentMemoryMB > Settings.MaxTotalMemoryMB)
                {
                    RequestMemoryPressureCleanup("timer-threshold", requireIdle: true, allowGen2: true);
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

        public static bool RequestMemoryPressureCleanup(string source, bool requireIdle = true, bool allowGen2 = false, DateTime? nowUtc = null)
        {
            var now = nowUtc ?? DateTime.UtcNow;

            var sinceCleanup = now - _lastCleanupUtc;
            if (sinceCleanup < Settings.CleanupCooldown)
            {
                Log.Debug("Skipping cleanup from {Source}: cooldown {CooldownMs}ms not elapsed.", source, Settings.CleanupCooldown.TotalMilliseconds);
                return false;
            }

            if (requireIdle && Settings.IdleThreshold > TimeSpan.Zero)
            {
                var idleTime = now - _lastActivityUtc;
                if (idleTime < Settings.IdleThreshold)
                {
                    ScheduleDeferredCleanup(source, Settings.IdleThreshold - idleTime, allowGen2);
                    Log.Debug("Deferred cleanup from {Source}: idle threshold not reached yet.", source);
                    return false;
                }
            }

            PerformSoftCleanup(source, allowGen2);
            return true;
        }

        public static void MarkActivity(DateTime? activityUtc = null)
        {
            _lastActivityUtc = activityUtc ?? DateTime.UtcNow;
        }

        public static void PerformSoftCleanup(string source, bool allowGen2 = false)
        {
            Log.Information("Performing soft memory cleanup ({Source})...", source);

            var beforeMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

            GC.Collect(0, GCCollectionMode.Optimized);
            GC.Collect(1, GCCollectionMode.Optimized);

            var highPressureThreshold = Settings.MaxTotalMemoryMB * Settings.HighPressureFactor;
            if (allowGen2 || beforeMB > highPressureThreshold)
            {
                if (beforeMB > highPressureThreshold)
                {
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                }

                GC.Collect(2, GCCollectionMode.Optimized, blocking: false, compacting: beforeMB > highPressureThreshold);
            }

            var afterMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
            var freedMB = beforeMB - afterMB;
            _lastCleanupUtc = DateTime.UtcNow;

            Log.Information("Soft memory cleanup complete: {FreedMb:F2} MB freed ({BeforeMb:F2} -> {AfterMb:F2} MB)", freedMB, beforeMB, afterMB);
        }

        private static void ScheduleDeferredCleanup(string source, TimeSpan delay, bool allowGen2)
        {
            var due = delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
            lock (_lock)
            {
                _deferredCleanupSource = source;
                _deferredCleanupTimer ??= new Timer(DeferredCleanupCallback, allowGen2, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                _deferredCleanupTimer.Change(due, Timeout.InfiniteTimeSpan);
            }
        }

        private static void DeferredCleanupCallback(object? state)
        {
            var allowGen2 = state is bool b && b;
            var source = _deferredCleanupSource ?? "deferred";
            RequestMemoryPressureCleanup($"{source}:deferred", requireIdle: true, allowGen2: allowGen2);
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
            MarkActivity();

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
