using WikiGraph.Api.Application.Models;

namespace WikiGraph.Api.Application.Abstractions;

public interface IRagRetrievalService
{
    IReadOnlyList<RetrievedContext> RetrieveContext(string sessionId, string prompt, int topK = 3);
}
