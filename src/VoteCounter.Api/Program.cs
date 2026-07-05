using VoteCounter.Api;
using VoteCounter.Core.DependencyInjection;
using VoteCounter.Data.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Register VoteCounter services
builder.Services.AddVoteCounterCore();
builder.Services.AddVoteCounterData();

// Add Swagger/OpenAPI
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "VoteCounter API",
        Version = "v1",
        Description = "REST API for VoteCounter - Contest Vote Management System",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "VoteCounter",
            Email = "support@votecounter.local"
        }
    });

    var xmlFile = "VoteCounter.Api.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "VoteCounter API v1");
        options.RoutePrefix = string.Empty;
        options.DefaultModelsExpandDepth(2);
        options.DefaultModelExpandDepth(2);
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow })
    .WithName("Health Check")
    .WithOpenApi();

// API version endpoint
app.MapGet("/api/version", () => new { 
    version = "1.0.0",
    name = "VoteCounter API",
    timestamp = DateTime.UtcNow
})
    .WithName("API Version")
    .WithOpenApi();

app.Run();
