using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Blazor.Services.Plugins;

public interface INodePlugin
{
    string Name { get; }
    string Id { get; }
    Version Version { get; }
    Version MinApiVersion { get; }

    void Register(NodeRegistryService registry);

    void ConfigureServices(IServiceCollection services) { }

    Task OnLoadAsync(CancellationToken token = default) => Task.CompletedTask;

    Task OnInitializeAsync(IServiceProvider services, CancellationToken token = default) => Task.CompletedTask;

    Task OnUnloadAsync(CancellationToken token = default) => Task.CompletedTask;

    void OnError(Exception exception) { }

    void Unload() { }
}
