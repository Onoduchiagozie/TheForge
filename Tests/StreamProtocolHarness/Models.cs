namespace TheForge.Models;
public sealed class QueryRequest { public string Query { get; set; } = ""; }
public sealed class QueryResponse { public string Response { get; set; } = ""; }
public sealed class HealthPayload {}
public sealed class InfoPayload {}
public sealed class SourceListPayload {}
public sealed class BookListPayload {}
public sealed class SourceChunksPayload {}
