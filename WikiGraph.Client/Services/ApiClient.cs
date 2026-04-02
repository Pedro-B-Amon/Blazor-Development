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

    public async Task<QueryResponse?> SubmitQueryAsync(QueryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/query", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QueryResponse>(cancellationToken);
    }
}
