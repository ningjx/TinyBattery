using System.Reflection;
using System.Runtime.Loader;

namespace TinyBattery.Infrastructure.Plugins;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly string[] SharedAssemblies =
    [
        "TinyBattery.Abstractions",
        "Microsoft.Extensions.Logging.Abstractions",
    ];

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginAssemblyPath)
        : base(isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (SharedAssemblies.Contains(assemblyName.Name, StringComparer.OrdinalIgnoreCase))
        {
            return Assembly.Load(assemblyName);
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
