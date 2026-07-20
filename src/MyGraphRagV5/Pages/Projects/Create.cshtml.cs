using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyGraphRagV5.Data;

namespace MyGraphRagV5.Pages.Projects;

public sealed class CreateModel(AppDbContext db) : PageModel
{
    public const string DefaultFilePattern = @".*\.(cs|md)$";

    private readonly AppDbContext db = db;

    [BindProperty]
    public InputModel Input { get; set; } = new() { FilePattern = DefaultFilePattern };

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        this.Input.Name = this.Input.Name.Trim();
        this.Input.SourceFolder = this.Input.SourceFolder.Trim();
        this.Input.FilePattern = string.IsNullOrWhiteSpace(this.Input.FilePattern)
            ? DefaultFilePattern
            : this.Input.FilePattern.Trim();

        if (string.IsNullOrEmpty(this.Input.Name))
        {
            this.ModelState.AddModelError("Input.Name", "Укажите название проекта.");
        }
        else if (await this.db.RagProjects.AnyAsync(p => p.Name == this.Input.Name, cancellationToken))
        {
            this.ModelState.AddModelError("Input.Name", "Проект с таким названием уже существует.");
        }

        if (string.IsNullOrEmpty(this.Input.SourceFolder))
        {
            this.ModelState.AddModelError("Input.SourceFolder", "Укажите исходную папку.");
        }

        if (!this.ModelState.IsValid)
        {
            return this.Page();
        }

        var (graphName, vectorCollection) = await ProjectNaming.DeriveUniqueAsync(this.db, this.Input.Name, cancellationToken);

        var project = new RagProject
        {
            Id = Guid.NewGuid(),
            Name = this.Input.Name,
            SourceFolder = this.Input.SourceFolder,
            GraphName = graphName,
            VectorCollection = vectorCollection,
            FilePattern = this.Input.FilePattern,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        this.db.RagProjects.Add(project);
        await this.db.SaveChangesAsync(cancellationToken);

        return this.RedirectToPage("Details", new { id = project.Id });
    }

    public sealed class InputModel
    {
        public string Name { get; set; } = "";

        public string SourceFolder { get; set; } = "";

        public string FilePattern { get; set; } = DefaultFilePattern;
    }
}
