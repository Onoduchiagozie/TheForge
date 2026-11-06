using TheForge.Models;

namespace TheForge.Services.Interfaces;

public interface IForgeApiService
{
    Task<HealthPayload?> GetHealthAsync(CancellationToken ct = default);
    Task<SourceListPayload?> GetSourcesAsync(CancellationToken ct = default);
    Task<BookListPayload?> GetBooksAsync(CancellationToken ct = default);
    Task<QueryResponse?> QueryExploreAsync(QueryRequest request, CancellationToken ct = default);
    Task<QueryResponse?> QueryFocusAsync(QueryRequest request, CancellationToken ct = default);
    Task<HttpResponseMessage> BeginNarrateStreamAsync(QueryRequest request, CancellationToken ct = default);
}
