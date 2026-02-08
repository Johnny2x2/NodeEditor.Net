using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Plugins;

public interface INodePlugin
{
    string Name { get; }
    string Id { get; }
    Version Version { get; }
    Version MinApiVersion { get; }

    void Register(INodeRegistryService registry);

    void ConfigureServices(IServiceCollection services) { }

    Task OnLoadAsync(CancellationToken token = default) => Task.CompletedTask;

    Task OnInitializeAsync(IServiceProvider services, CancellationToken token = default) => Task.CompletedTask;

    Task OnUnloadAsync(CancellationToken token = default) => Task.CompletedTask;

    void OnError(Exception exception) { }

    void Unload() { }
}
