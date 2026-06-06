using System.Reflection;
using System.Text.RegularExpressions;
using V3dfy.Core.Processes;

namespace V3dfy.Infrastructure.Processes;

public sealed class WindowsPerformanceCounterGpuCounterReader : IWindowsGpuCounterReader
{
    private const string GpuEngineCategory = "GPU Engine";
    private const string GpuEngineUsageCounter = "Utilization Percentage";
    private const string GpuProcessMemoryCategory = "GPU Process Memory";
    private const string GpuProcessDedicatedUsageCounter = "Dedicated Usage";

    private static readonly Regex ProcessIdRegex = new(
        @"(?:^|_)pid_(?<pid>\d+)(?:_|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public WindowsGpuCounterSnapshot Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return WindowsGpuCounterSnapshot.Failed(
                ProcessGpuMetricReading.WindowsMetricsUnavailableStatus);
        }

        if (!PerformanceCounterReflectionFacade.TryCreate(
            out var facade,
            out var failureReason))
        {
            return WindowsGpuCounterSnapshot.Failed(failureReason);
        }

        try
        {
            var engineValues = facade.ReadCounterInstances(
                GpuEngineCategory,
                GpuEngineUsageCounter);
            if (engineValues.Count == 0)
            {
                return WindowsGpuCounterSnapshot.Failed(
                    ProcessGpuMetricReading.WindowsMetricsUnavailableStatus);
            }

            var memoryValues = facade.ReadCounterInstances(
                GpuProcessMemoryCategory,
                GpuProcessDedicatedUsageCounter);

            return new(
                EngineCounters: engineValues
                    .Select(value => new GpuEngineCounterSample(
                        ProcessId: TryExtractProcessId(value.InstanceName),
                        InstanceName: value.InstanceName,
                        EngineType: ExtractEngineType(value.InstanceName),
                        UtilizationPercent: value.Value))
                    .ToArray(),
                MemoryCounters: memoryValues
                    .Select(value => new
                    {
                        ProcessId = TryExtractProcessId(value.InstanceName),
                        value.InstanceName,
                        DedicatedUsageBytes = (long)Math.Max(0, value.Value),
                    })
                    .Where(value => value.ProcessId is not null)
                    .Select(value => new GpuProcessMemoryCounterSample(
                        ProcessId: value.ProcessId!.Value,
                        InstanceName: value.InstanceName,
                        DedicatedUsageBytes: value.DedicatedUsageBytes))
                    .ToArray());
        }
        catch (UnauthorizedAccessException)
        {
            return WindowsGpuCounterSnapshot.Failed(
                ProcessGpuMetricReading.PermissionUnavailableStatus);
        }
        catch (Exception)
        {
            return WindowsGpuCounterSnapshot.Failed(
                ProcessGpuMetricReading.WindowsMetricsUnavailableStatus);
        }
    }

    private static int? TryExtractProcessId(string instanceName)
    {
        var match = ProcessIdRegex.Match(instanceName);
        return match.Success &&
            int.TryParse(match.Groups["pid"].Value, out var processId)
                ? processId
                : null;
    }

    private static string ExtractEngineType(string instanceName)
    {
        const string marker = "engtype_";
        var markerIndex = instanceName.IndexOf(
            marker,
            StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return string.Empty;
        }

        var start = markerIndex + marker.Length;
        return instanceName[start..];
    }

    private sealed record CounterInstanceValue(string InstanceName, double Value);

    private sealed class PerformanceCounterReflectionFacade
    {
        private readonly Type _categoryType;
        private readonly Type _counterType;
        private readonly MethodInfo _categoryExistsMethod;
        private readonly ConstructorInfo _categoryConstructor;
        private readonly ConstructorInfo _counterConstructor;
        private readonly MethodInfo _getInstanceNamesMethod;
        private readonly MethodInfo _nextValueMethod;

        private PerformanceCounterReflectionFacade(
            Type categoryType,
            Type counterType,
            MethodInfo categoryExistsMethod,
            ConstructorInfo categoryConstructor,
            ConstructorInfo counterConstructor,
            MethodInfo getInstanceNamesMethod,
            MethodInfo nextValueMethod)
        {
            _categoryType = categoryType;
            _counterType = counterType;
            _categoryExistsMethod = categoryExistsMethod;
            _categoryConstructor = categoryConstructor;
            _counterConstructor = counterConstructor;
            _getInstanceNamesMethod = getInstanceNamesMethod;
            _nextValueMethod = nextValueMethod;
        }

        public static bool TryCreate(
            out PerformanceCounterReflectionFacade facade,
            out string failureReason)
        {
            facade = null!;
            failureReason = ProcessGpuMetricReading.WindowsMetricsUnavailableStatus;

            Assembly assembly;
            try
            {
                assembly = Assembly.Load("System.Diagnostics.PerformanceCounter");
            }
            catch
            {
                return false;
            }

            var categoryType = assembly.GetType(
                "System.Diagnostics.PerformanceCounterCategory");
            var counterType = assembly.GetType("System.Diagnostics.PerformanceCounter");
            if (categoryType is null || counterType is null)
            {
                return false;
            }

            var categoryExistsMethod = categoryType.GetMethod(
                "CategoryExists",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(string)],
                modifiers: null);
            var categoryConstructor = categoryType.GetConstructor([typeof(string)]);
            var counterConstructor = counterType.GetConstructor(
                [typeof(string), typeof(string), typeof(string), typeof(bool)]);
            var getInstanceNamesMethod = categoryType.GetMethod(
                "GetInstanceNames",
                Type.EmptyTypes);
            var nextValueMethod = counterType.GetMethod("NextValue", Type.EmptyTypes);

            if (categoryExistsMethod is null ||
                categoryConstructor is null ||
                counterConstructor is null ||
                getInstanceNamesMethod is null ||
                nextValueMethod is null)
            {
                return false;
            }

            facade = new(
                categoryType,
                counterType,
                categoryExistsMethod,
                categoryConstructor,
                counterConstructor,
                getInstanceNamesMethod,
                nextValueMethod);
            return true;
        }

        public IReadOnlyList<CounterInstanceValue> ReadCounterInstances(
            string categoryName,
            string counterName)
        {
            if (_categoryExistsMethod.Invoke(null, [categoryName]) is not true)
            {
                return [];
            }

            var category = _categoryConstructor.Invoke([categoryName]);
            var instanceNames = _getInstanceNamesMethod.Invoke(category, null) as string[];
            if (instanceNames is null || instanceNames.Length == 0)
            {
                return [];
            }

            var values = new List<CounterInstanceValue>();
            foreach (var instanceName in instanceNames)
            {
                try
                {
                    using var counter = _counterConstructor.Invoke(
                        [categoryName, counterName, instanceName, true]) as IDisposable;
                    if (counter is null)
                    {
                        continue;
                    }

                    var value = _nextValueMethod.Invoke(counter, null);
                    if (value is float singleValue)
                    {
                        values.Add(new(instanceName, singleValue));
                    }
                    else if (value is double doubleValue)
                    {
                        values.Add(new(instanceName, doubleValue));
                    }
                }
                catch
                {
                    // GPU counter instances can disappear while a process exits.
                }
            }

            return values;
        }
    }
}
