using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyGraphRagV5.Data;
using MyGraphRagV5.Query;

namespace MyGraphRagV5.Pages.Chat;

/// <summary>
/// Project Q&amp;A. Each question is answered by <see cref="RagQueryService.AskAsync"/> and the
/// result is appended to the on-page conversation via htmx (<c>hx-swap="beforeend"</c>) instead of
/// a full page reload. No server-side conversation state is kept between questions here -- a fresh
/// question flow is sufficient per the Task 8c brief; RagQueryService still persists every exchange
/// to ChatSession/ChatMessage on its own.
/// </summary>
public sealed class IndexModel(AppDbContext db, RagQueryService ragQueryService) : PageModel
{
    private readonly AppDbContext db = db;
    private readonly RagQueryService ragQueryService = ragQueryService;

    [BindProperty(SupportsGet = true)]
    public Guid? ProjectId { get; set; }

    public IReadOnlyList<RagProject> Projects { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        this.Projects = await this.db.RagProjects.AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        if (this.ProjectId is null || !this.Projects.Any(p => p.Id == this.ProjectId))
        {
            this.ProjectId = this.Projects.Count > 0 ? this.Projects[0].Id : null;
        }
    }

    /// <summary>
    /// Answers one question and returns the "_Exchange" partial to append to the conversation.
    /// The TEI embed/rerank containers are offline in this environment, so AskAsync throws at the
    /// embed step -- caught here and rendered as a friendly error card instead of a 500.
    /// </summary>
    public async Task<IActionResult> OnPostAskAsync(Guid projectId, string question, CancellationToken cancellationToken)
    {
        question = question?.Trim() ?? string.Empty;
        if (question.Length == 0)
        {
            return this.Content(string.Empty);
        }

        ChatExchangeViewModel exchange;
        try
        {
            var answer = await this.ragQueryService.AskAsync(projectId, question, ct: cancellationToken);
            exchange = new ChatExchangeViewModel(question, answer, null);
        }
        catch (Exception ex)
        {
            exchange = new ChatExchangeViewModel(question, null, $"Сервис эмбеддингов недоступен: {ex.Message}");
        }

        return this.Partial("_Exchange", exchange);
    }

    public sealed record ChatExchangeViewModel(string Question, RagAnswer? Answer, string? Error);
}
