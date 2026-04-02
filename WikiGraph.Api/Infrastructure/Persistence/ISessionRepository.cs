using WikiGraph.Api.Application.Models;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Infrastructure.Persistence;

public interface ISessionRepository
{
    SessionSummary CreateSession(string title);
    void EnsureSession(string sessionId, string title, DateTime accessedUtc);
    IReadOnlyList<SessionSummary> GetSessions();
    SessionDetailDto? GetSession(string sessionId);
    IReadOnlyList<GraphDto>? GetGraphs(string sessionId);
    void SaveQueryArtifacts(QueryArtifacts artifacts);
}
