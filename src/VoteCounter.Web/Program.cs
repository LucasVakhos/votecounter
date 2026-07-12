using VoteCounter.Web.Components;
using VoteCounter.Web.Services;
using VoteCounter.Core.DependencyInjection;
using VoteCounter.Core.Data;
using VoteCounter.Core.Services;
using VoteCounter.Data.DependencyInjection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register VoteCounter services
builder.Services.AddVoteCounterCore();
builder.Services.AddVoteCounterData();

// Configure Entity Framework Core with SQLite
var dbPath = Path.Combine(AppContext.BaseDirectory, "data", "votecounter.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContext<VoteCounterDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Add custom services
builder.Services.AddScoped<ContestService>();
builder.Services.AddScoped<VoteService>();
builder.Services.AddScoped<ModerationWebService>();
builder.Services.AddScoped<AuthorizationWebService>();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var persistenceService = scope.ServiceProvider.GetRequiredService<PersistenceService>();
    await persistenceService.InitializeDatabaseAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
