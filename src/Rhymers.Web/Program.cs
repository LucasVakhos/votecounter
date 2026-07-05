using Rhymers.Web.Components;
using Rhymers.Web.Services;
using Rhymers.Core.DependencyInjection;
using Rhymers.Core.Data;
using Rhymers.Core.Services;
using Rhymers.Data.DependencyInjection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register Rhymers services
builder.Services.AddRhymersCore();
builder.Services.AddRhymersData();

// Configure Entity Framework Core with SQLite
var dbPath = Path.Combine(AppContext.BaseDirectory, "data", "rhymers.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContext<RhymersDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Add custom services
builder.Services.AddScoped<ContestService>();
builder.Services.AddScoped<VoteService>();
builder.Services.AddScoped<ModerationWebService>();
builder.Services.AddScoped<SorrowChatWebService>();
builder.Services.AddScoped<AuthorizationWebService>();
builder.Services.AddScoped<OdnoklassnikiOAuthService>();
builder.Services.AddHttpClient<OdnoklassnikiOAuthService>();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHostedService<ContestStageAutomationHostedService>();

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

app.UseSession();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
