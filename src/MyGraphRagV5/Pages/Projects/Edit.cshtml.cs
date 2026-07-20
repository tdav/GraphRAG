using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyGraphRagV5.Data;

namespace MyGraphRagV5.Pages.Projects;

public sealed class EditModel(AppDbContext db) : PageModel
{
    private readonly AppDbContext db = db;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await this.db.RagProjects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null)
        {
            return this.NotFound();
        }

        this.Input = new InputModel
        {
            Id = project.Id,
            Name = project.Name,
            SourceFolder = project.SourceFolder,
            FilePattern = project.FilePattern,
            GraphName = project.GraphName,
            VectorCollection = project.VectorCollection,
        };

        return this.Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        this.Input.Name = this.Input.Name.Trim();
        this.Input.SourceFolder = this.Input.SourceFolder.Trim();
        this.Input.FilePattern = string.IsNullOrWhiteSpace(this.Input.FilePattern)
            ? CreateModel.DefaultFilePattern
            : this.Input.FilePattern.Trim();

        if (string.IsNullOrEmpty(this.Input.Name))
        {
            this.ModelState.AddModelError("Input.Name", "Укажите название проекта.");
        }
        else if (await this.db.RagProjects.AnyAsync(p => p.Name == this.Input.Name && p.Id != this.Input.Id, cancellationToken))
        {
            this.ModelState.AddModelError("Input.Name", "Проект с таким названием уже существует.");
        }

        if (string.IsNullOrEmpty(this.Input.SourceFolder))
        {
            this.ModelState.AddModelError("Input.SourceFolder", "Укажите исходную папку.");
        }

        if (!this.ModelState.IsValid)
        {
            // GraphName/VectorCollection aren't posted back (read-only fields) -- reload them for display.
            var project = await this.db.RagProjects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == this.Input.Id, cancellationToken);
            if (project is not null)
            {
                this.Input.GraphName = project.GraphName;
                this.Input.VectorCollection = project.VectorCollection;
            }

            return this.Page();
        }

        var updated = await this.db.RagProjects
            .Where(p => p.Id == this.Input.Id)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(p => p.Name, this.Input.Name)
                    .SetProperty(p => p.SourceFolder, this.Input.SourceFolder)
                    .SetProperty(p => p.FilePattern, this.Input.FilePattern),
                cancellationToken);

        if (updated == 0)
        {
            return this.NotFound();
        }

        return this.RedirectToPage("Details", new { id = this.Input.Id });
    }

    public sealed class InputModel
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = "";

        public string SourceFolder { get; set; } = "";

        public string FilePattern { get; set; } = "";

        public string GraphName { get; set; } = "";

        public string VectorCollection { get; set; } = "";
    }
}
