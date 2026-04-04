using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WikiGraph.Client;
using WikiGraph.Client.Services;

const string DefaultApiBaseUrl = "http://localhost:5052/";

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var hostBaseUri = new Uri(builder.HostEnvironment.BaseAddress, UriKind.Absolute);

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped(_ =>
{
    var configuredApiBaseUrl = builder.Configuration["ApiBaseUrl"];
    var apiBaseUri = string.IsNullOrWhiteSpace(configuredApiBaseUrl)
        ? new Uri(DefaultApiBaseUrl, UriKind.Absolute)
        : Uri.TryCreate(configuredApiBaseUrl, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri
            : new Uri(hostBaseUri, configuredApiBaseUrl);

    return new ApiClient(new HttpClient { BaseAddress = apiBaseUri });
});

await builder.Build().RunAsync();
