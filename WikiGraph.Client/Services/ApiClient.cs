using System.Net.Http.Json;
using WikiGraph.Contracts;

namespace WikiGraph.Client.Services;

public sealed class ApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<SessionSummary>> GetSessionsAsync(CancellationToken cancellationToken = default) =>
        await _httpClient.GetFromJsonAsync<List<SessionSummary>>("api/sessions", cancellationToken) ?? [];

    public async Task<SessionSummary> CreateSessionAsync(CreateSessionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/sessions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionSummary>(cancellationToken))!;
    }

    public Task<SessionDetailDto?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default) =>
        _httpClient.GetFromJsonAsync<SessionDetailDto>($"api/sessions/{sessionId}", cancellationToken);

    public async Task<IReadOnlyList<GraphDto>> GetGraphsAsync(string sessionId, CancellationToken cancellationToken = default) =>
        await _httpClient.GetFromJsonAsync<List<GraphDto>>($"api/sessions/{sessionId}/graphs", cancellationToken) ?? [];

    public async Task<SessionDetailDto?> AddArticleAsync(string sessionId, AddWikiArticleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/sessions/{sessionId}/articles", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionDetailDto>(cancellationToken);
    }
}
