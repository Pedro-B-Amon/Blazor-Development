using WikiGraph.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Load local secrets before configuration binding so `.env` works during development.
LoadEnvironmentFile(builder.Environment.ContentRootPath);
builder.Services.AddWikiGraphApi(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.MapControllers();

app.Run();

// Loads the nearest .env file while walking up the directory tree.
static void LoadEnvironmentFile(string startPath)
{
    for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
    {
        var envPath = Path.Combine(directory.FullName, ".env");
        if (!File.Exists(envPath))
        {
            continue;
        }

        foreach (var line in File.ReadLines(envPath))
        {
            var text = line.Trim();
            if (text.Length == 0 || text.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = text.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = text[..separatorIndex].Trim();
            var value = text[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key) || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                continue;
            }

            if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                value = value[1..^1];
            }

            Environment.SetEnvironmentVariable(key, value);
        }

        return;
    }
}

public partial class Program;
