var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "MyGraphRagV5");

app.Run();
