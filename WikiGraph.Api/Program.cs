using WikiGraph.Api.Configuration;
using WikiGraph.Contracts;

var builder = WebApplication.CreateBuilder(args);

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
app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program;
