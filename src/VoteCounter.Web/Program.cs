using VoteCounter.Web.Components;
using VoteCounter.Web.Services;
using VoteCounter.Core.DependencyInjection;
using VoteCounter.Data.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register VoteCounter services
builder.Services.AddVoteCounterCore();
builder.Services.AddVoteCounterData();

// Add custom services
builder.Services.AddScoped<ContestService>();
builder.Services.AddScoped<VoteService>();
builder.Services.AddScoped<ModerationWebService>();

var app = builder.Build();

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
