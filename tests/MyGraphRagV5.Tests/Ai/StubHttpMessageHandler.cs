namespace MyGraphRagV5.Tests.Ai;

/// <summary>Records requests and answers with a canned response, for hermetic HttpClient tests.</summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        this.Requests.Add((request, body));
        return await responder(request);
    }
}
