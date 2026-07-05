# Dependency Injection Setup

## Overview

The VoteCounter solution uses Microsoft.Extensions.DependencyInjection for inversion of control and loose coupling between layers.

## Architecture

### Service Layers

**VoteCounter.Core** - Business Logic & Processing
- Vote parsing and validation services
- Contest management and results generation
- Text processing utilities
- Import and export functionality

**VoteCounter.Data** - Data Access Layer
- Database connectivity
- Data store implementations
- Legacy system importers

**VoteCounter.Api** - REST API
- HTTP Controllers with injected services
- Stateless request processing

**VoteCounter.Web** - Blazor Web Interface
- Server components with scoped services
- UI business logic

## Service Registration

### Core Services (Singleton)

```csharp
builder.Services.AddVoteCounterCore();
```

Registers:
- `VoteParser` - Parses vote text into structured data
- `VoteRuleService` - Validates votes against contest rules
- `ContestResultsService` - Generates result reports
- `VoteAuditService` - Audits vote processing
- `WorkTextImporter` - Imports work submissions
- `ContestTextImporter` - Imports contest definitions
- `SingleWorkSubmissionImporter` - Imports single submissions
- `PrivateMessageWorkImporter` - Imports works from messages
- `AuthorDisclosureImporter` - Handles author disclosure
- `ContestReportExportService` - Exports reports in various formats
- `ExcelResultBuilder` - Builds Excel reports
- `ContestRulesAutoFixService` - Auto-fixes contest rule violations
- `WorkSpellChecker` - Spell checks work submissions

### Data Services (Singleton)

```csharp
builder.Services.AddVoteCounterData();
```

Registers:
- `LocalStore` - Local file storage operations
- `RhymeMachineStore` - Specialized storage for rhyme machine
- `VoteImportReportService` - Generates import reports
- `FirebirdLegacyImporter` - Imports from legacy Firebird databases

### Web Services (Scoped)

**VoteCounter.Api/Program.cs:**
```csharp
builder.Services.AddVoteCounterCore();
builder.Services.AddVoteCounterData();
```

**VoteCounter.Web/Program.cs:**
```csharp
builder.Services.AddVoteCounterCore();
builder.Services.AddVoteCounterData();
builder.Services.AddScoped<ContestService>();
builder.Services.AddScoped<VoteService>();
```

## Service Lifetimes

- **Singleton**: Core services are registered as singletons because they are stateless and thread-safe
- **Scoped**: Blazor services use scoped lifetime for per-request isolation
- **Transient**: Not used in current architecture (can be added if needed)

## Usage Examples

### API Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class VotesController : ControllerBase
{
    private readonly VoteParser _voteParser;
    private readonly VoteRuleService _voteRuleService;
    private readonly ContestResultsService _resultsService;

    public VotesController(
        VoteParser voteParser,
        VoteRuleService voteRuleService,
        ContestResultsService resultsService)
    {
        _voteParser = voteParser;
        _voteRuleService = voteRuleService;
        _resultsService = resultsService;
    }

    [HttpPost("import")]
    public ActionResult<ImportResult> ImportVotes([FromBody] ImportVotesRequest request)
    {
        var result = _voteParser.Parse(request.VoteText, request.ContestId);
        _voteRuleService.Apply(contest, result);
        var report = _resultsService.BuildReport(contest, votes);
        return Ok(report);
    }
}
```

### Blazor Services

```csharp
public class VoteService
{
    private readonly VoteParser _voteParser;
    private readonly VoteRuleService _voteRuleService;
    private readonly ContestResultsService _resultsService;

    public VoteService(
        VoteParser voteParser,
        VoteRuleService voteRuleService,
        ContestResultsService resultsService)
    {
        _voteParser = voteParser;
        _voteRuleService = voteRuleService;
        _resultsService = resultsService;
    }

    public async Task<ContestResultsReport?> GetResultsAsync(
        Contest contest,
        List<VoteEntry> votes)
    {
        var results = _resultsService.BuildReport(contest, votes);
        return await Task.FromResult(results);
    }
}
```

### Desktop Application (when using WinForms)

```csharp
var services = new ServiceCollection();
services.AddVoteCounterCore();
services.AddVoteCounterData();
var provider = services.BuildServiceProvider();

var voteParser = provider.GetRequiredService<VoteParser>();
var results = voteParser.Parse(voteText, contestId);
```

## Benefits of DI

1. **Loose Coupling** - Services depend on abstractions, not implementations
2. **Testability** - Easy to inject mock services for unit testing
3. **Centralized Configuration** - All dependencies configured in one place
4. **Scalability** - Easy to add new services or change implementations
5. **Thread Safety** - Singleton services are shared safely across requests
6. **Dependency Resolution** - Framework automatically resolves service graphs

## Custom Extension Methods

You can create custom registration methods for specific scenarios:

```csharp
public static IServiceCollection AddMinimalServices(this IServiceCollection services)
{
    // Register only essential services for specific use case
    return services
        .AddVoteParser()
        .AddVoteRuleService()
        .AddContestResultsService();
}
```

## Testing with DI

```csharp
[Fact]
public void ImportVotes_WithValidText_ReturnsResult()
{
    // Arrange
    var mockParser = new Mock<VoteParser>();
    var services = new ServiceCollection();
    services.AddSingleton(mockParser.Object);
    var provider = services.BuildServiceProvider();

    // Act
    var controller = ActivatorUtilities.CreateInstance<VotesController>(provider);
    var result = controller.ImportVotes(new ImportVotesRequest(...));

    // Assert
    Assert.NotNull(result);
}
```

## Static Utilities (Not in DI)

Some utilities are intentionally static and not registered in DI:

- `NameNormalizer` - Stateless text normalization
- `DelphiVoteTextTools` - Legacy format handling utilities
- `LocalDatabase` - Static database connection management

These can be used directly without injection.

## Future Enhancements

1. **Configuration-Based Registration** - Load service definitions from configuration
2. **Decorators** - Add cross-cutting concerns (logging, caching)
3. **Factories** - Complex service creation logic
4. **Service Locator Pattern** - If needed for dynamic service retrieval
5. **Scoped Registrations** - For features requiring per-request state
