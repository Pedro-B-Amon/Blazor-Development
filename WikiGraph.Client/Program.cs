using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WikiGraph.Client;
using WikiGraph.Client.Services;

const string DefaultApiBaseUrl = "http://localhost:5052/";

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped(_ =>
{
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? DefaultApiBaseUrl;
    return new ApiClient(new HttpClient { BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute) });
});

await builder.Build().RunAsync();
