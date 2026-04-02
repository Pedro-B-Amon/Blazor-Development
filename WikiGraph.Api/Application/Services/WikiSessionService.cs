using WikiGraph.Api.Application.Models;
using WikiGraph.Api.Infrastructure.Persistence;
using WikiGraph.Api.Infrastructure.Wikipedia;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Application.Services;

public sealed class WikiSessionService
{
    private readonly WikipediaService _wikipediaService;
    private readonly GeminiService _geminiService;
    private readonly SqliteSessionRepository _sessionRepository;
    private readonly SqliteVectorStore _vectorStore;

    public WikiSessionService(
        WikipediaService wikipediaService,
        GeminiService geminiService,
        SqliteSessionRepository sessionRepository,
        SqliteVectorStore vectorStore)
    {
        _wikipediaService = wikipediaService;
        _geminiService = geminiService;
        _sessionRepository = sessionRepository;
        _vectorStore = vectorStore;
    }

    public async Task<SessionDetailDto> AddArticleAsync(
        string sessionId,
        AddWikiArticleRequest request,
        CancellationToken cancellationToken = default)
    {
        var topic = TextTools.Clean(request.Topic);
        var wikipediaUrl = TextTools.Clean(request.WikipediaUrl);
        var sessionHistory = _sessionRepository.GetSession(sessionId)?.Messages ?? [];
        var article = await _wikipediaService.GetArticleAsync(topic, wikipediaUrl, cancellationToken);
        var prompt = string.IsNullOrWhiteSpace(topic) ? article.Title : topic;
        var nowUtc = DateTime.UtcNow;

        _sessionRepository.EnsureSession(sessionId, InferTitle(prompt), nowUtc);
        await _vectorStore.UpsertArticleAsync(sessionId, article, cancellationToken);

        var matches = await _vectorStore.SearchAsync(sessionId, BuildSearchText(prompt, sessionHistory), 4, cancellationToken);
        var reply = await _geminiService.GenerateReplyAsync(prompt, article, sessionHistory, matches, cancellationToken);
        var citations = BuildCitations(article, matches);
        var graphs = BuildGraphs(prompt, article, reply.RelatedTopics, matches);

        _sessionRepository.SaveTurn(
            sessionId,
            InferTitle(prompt),
            new MessageDto("user", BuildUserMessage(topic, wikipediaUrl, article.Title), nowUtc),
            new MessageDto("assistant", reply.Answer, nowUtc),
            citations,
            graphs);

        return _sessionRepository.GetSession(sessionId)!;
    }

    private static string BuildSearchText(string prompt, IReadOnlyList<MessageDto> messages)
    {
        var recentUserMessages = messages
            .Where(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
            .TakeLast(2)
            .Select(message => message.Content)
            .ToArray();

        return recentUserMessages.Length == 0
            ? prompt
            : $"{prompt} {string.Join(' ', recentUserMessages)}";
    }

    private static string BuildUserMessage(string topic, string wikipediaUrl, string articleTitle)
    {
        if (string.IsNullOrWhiteSpace(wikipediaUrl))
        {
            return string.IsNullOrWhiteSpace(topic) ? articleTitle : topic;
        }

        if (string.IsNullOrWhiteSpace(topic))
        {
            return $"Wikipedia URL: {wikipediaUrl}";
        }

        return $"{topic}{Environment.NewLine}Wikipedia URL: {wikipediaUrl}";
    }

    private static IReadOnlyList<CitationDto> BuildCitations(WikiArticle article, IReadOnlyList<WikiMatch> matches)
    {
        var citations = matches.Take(3)
            .Select(match => new CitationDto(
                match.Section.Equals("Overview", StringComparison.OrdinalIgnoreCase) ? article.Title : $"{article.Title} {match.Section}",
                match.Section.Equals("Overview", StringComparison.OrdinalIgnoreCase) ? article.SourceUrl : $"{article.SourceUrl}#{TextTools.Slugify(match.Section)}",
                match.Section,
                match.ChunkId))
            .Distinct()
            .ToArray();

        if (citations.Length > 0)
        {
            return citations;
        }

        return
        [
            new CitationDto(article.Title, article.SourceUrl, "Overview", null),
            new CitationDto($"{article.Title} related topics", $"{article.SourceUrl}#related-topics", "Related topics", null),
            new CitationDto($"{article.Title} references", $"{article.SourceUrl}#references", "References", null)
        ];
    }

    private static IReadOnlyList<GraphDto> BuildGraphs(
        string prompt,
        WikiArticle article,
        IReadOnlyList<string> relatedTopics,
        IReadOnlyList<WikiMatch> matches)
    {
        var topic = string.IsNullOrWhiteSpace(prompt) ? article.Title : prompt;
        // First ring: the main related topics directly connected to the requested/base topic.
        var primaryTopics = new[] { article.Title }
            .Concat(relatedTopics)
            .Concat(article.RelatedTopicDetails.Select(item => item.Title))
            .Concat(article.RelatedArticles)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(TextTools.Clean)
            .Where(label => !string.Equals(label, topic, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
        var supportingTopics = BuildSupportingTopics(article, matches);

        var nodes = new List<GraphNodeDto> { new("topic", topic, 5) };
        var edges = new List<GraphEdgeDto>();

        for (var index = 0; index < primaryTopics.Length; index++)
        {
            var primaryTopic = primaryTopics[index];
            var primaryId = $"topic-{index + 1}";
            nodes.Add(new GraphNodeDto(primaryId, primaryTopic, Math.Max(3, 4 - Math.Min(index, 1))));
            edges.Add(new GraphEdgeDto("topic", primaryId, "related"));

            var detailLabels = supportingTopics
                .Where(item => !string.Equals(item, primaryTopic, StringComparison.OrdinalIgnoreCase))
                .Where(item => !string.Equals(item, topic, StringComparison.OrdinalIgnoreCase))
                .Skip(index)
                .Take(2)
                .ToArray();

            for (var detailIndex = 0; detailIndex < detailLabels.Length; detailIndex++)
            {
                var detailLabel = detailLabels[detailIndex];
                var detailId = $"{primaryId}-detail-{detailIndex + 1}";
                if (nodes.Any(node => string.Equals(node.Label, detailLabel, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                nodes.Add(new GraphNodeDto(detailId, detailLabel, 2));
                edges.Add(new GraphEdgeDto(primaryId, detailId, "supports"));
            }
        }

        if (nodes.Count == 1)
        {
            nodes.Add(new GraphNodeDto("topic-1", article.Title, 4));
            edges.Add(new GraphEdgeDto("topic", "topic-1", "related"));
        }

        return [new GraphDto(topic, nodes, edges)];
    }

    private static IReadOnlyList<string> BuildSupportingTopics(WikiArticle article, IReadOnlyList<WikiMatch> matches)
    {
        // Second ring: short supporting labels derived from related-topic summaries and retrieved section names.
        return article.RelatedTopicDetails
            .SelectMany(item => new[] { item.Summary, item.Title })
            .Concat(matches.Select(match => match.Section))
            .Concat(article.Sections.Select(section => section.Heading))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => SplitGraphLabels(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    private static IEnumerable<string> SplitGraphLabels(string value)
    {
        var cleaned = TextTools.Clean(value);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return [];
        }

        var labels = cleaned
            .Split([",", ";", ".", ":"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(TextTools.Clean)
            .Where(label => label.Length > 2)
            .Take(3)
            .ToArray();

        return labels.Length == 0 ? [cleaned] : labels;
    }

    private static string InferTitle(string prompt)
    {
        var title = prompt
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(5);

        return string.Join(' ', title) is { Length: > 0 } value ? value : "New session";
    }
}
