# Rhymers - Refactored Architecture

## Project Structure

The Rhymers project has been refactored into a multi-project solution with clear separation of concerns:

```
/src/
  ├── Rhymers.Core/          # Business Logic & Data Models
  │   ├── Models/                # Domain models (Contest, Vote, etc.)
  │   ├── Services/              # Business logic services
  │   │   ├── VoteParser.cs
  │   │   ├── VoteRuleService.cs
  │   │   ├── ContestResultsService.cs
  │   │   ├── ExcelResultBuilder.cs
  │   │   └── ... (other services)
  │   └── GlobalUsings.cs
  │
  ├── Rhymers.Data/          # Data Access Layer
  │   ├── Database/              # Database-related services
  │   │   ├── LocalDatabase.cs   # SQLite connection & management
  │   │   ├── LocalStore.cs      # Local data persistence
  │   │   ├── RhymeMachineStore.cs
  │   │   ├── FirebirdLegacyImporter.cs
  │   │   └── GlobalUsings.cs
  │   ├── Schema/                # SQL schema files
  │   └── Rhymers.Data.csproj
  │
  └── Rhymers.Desktop/       # WinForms UI Application
      ├── Forms/                 # UI forms
      │   ├── MainForm.cs
      │   ├── MainForm.resx
      │   └── ContestRulesCenterForm.cs
      ├── Resources/Images/      # Application icons & images
      ├── Samples/               # Sample data files
      ├── Help/                  # Help documentation
      ├── LayoutIniStore.cs      # UI layout persistence
      ├── Program.cs             # Application entry point
      ├── GlobalUsings.cs
      └── Rhymers.Desktop.csproj
```

## Projects

### Rhymers.Core
- **Type:** Class Library (.NET 8)
- **Dependencies:** None (no external references)
- **Purpose:** Domain models and business logic
- **Contents:**
  - Models: All data model classes
  - Services: Business logic for vote processing, contest management, etc.

### Rhymers.Data
- **Type:** Class Library (.NET 8)
- **Dependencies:** Rhymers.Core, Microsoft.Data.Sqlite, FirebirdSql.Data.FirebirdClient
- **Purpose:** Data access and database management
- **Contents:**
  - Database services for SQLite and Firebird
  - SQL schema definitions
  - Data persistence and import logic

### Rhymers.Desktop
- **Type:** WinForms Application (.NET 8 Windows)
- **Dependencies:** Rhymers.Core, Rhymers.Data, DevExpress
- **Purpose:** User interface and application runtime
- **Contents:**
  - WinForms UI components
  - Layout management
  - Application entry point

## Namespace Structure

```
Rhymers.Core.Models        → Core domain models
Rhymers.Core.Services      → Core business logic services
Rhymers.Data.Database      → Data access services
Rhymers.Desktop            → UI and application
```

### Rhymers.Tests
- **Type:** Unit Tests Project (.NET 9)
- **Dependencies:** Rhymers.Core, Rhymers.Data, xUnit, Moq, FluentAssertions
- **Purpose:** Unit testing for all layers
- **Contents:**
  - Models Tests: VoteEntry, Contest, etc.
  - Services Tests: VoteParser, VoteRuleService, NameNormalizer, etc.
  - Fixtures and test utilities

### Rhymers.Web
- **Type:** Blazor Web App (.NET 9)
- **Dependencies:** Rhymers.Core, Rhymers.Data, Blazor Runtime
- **Purpose:** Web user interface for contest management and voting
- **Key Components:**
  - Services/ContestService.cs: Business logic for contest management
  - Services/VoteService.cs: Business logic for vote processing
  - Components/Pages/Contests.razor: Contest management interface
  - Components/Pages/VoteImport.razor: Vote import interface
  - Components/Pages/ContestResults.razor: Results display interface
- **Architecture:** Blazor Server Components with service-based architecture

### Rhymers.Api
- **Type:** ASP.NET Core Web API (.NET 9)
- **Dependencies:** Rhymers.Core, Rhymers.Data, Swagger/OpenAPI
- **Purpose:** REST API endpoints for external applications
- **Key Controllers:**
  - ContestsController: CRUD operations for contests
  - VotesController: Vote import, validation, and results endpoints
- **Features:**
  - Swagger/OpenAPI documentation (auto-generated at `/`)
  - Comprehensive logging
  - CORS enabled for web integration
  - Health check endpoints (`/health`, `/api/version`)
- **Architecture:** RESTful API with layered service architecture

## Building & Running

### Build All Projects
```bash
dotnet build Rhymers.sln
```

### Run Tests
```bash
dotnet test Rhymers.sln
```

### Run API Server
```bash
dotnet run --project src/Rhymers.Api/Rhymers.Api.csproj
# Open: https://localhost:7070 (Swagger UI)
# Open: http://localhost:5070 (development)
```

### Run Blazor Web
```bash
dotnet run --project src/Rhymers.Web/Rhymers.Web.csproj
# Open: https://localhost:7070 (Blazor interface)
```

## Dependency Injection

All services are registered using Microsoft.Extensions.DependencyInjection:

### Core Services (Singleton)
```csharp
builder.Services.AddRhymersCore();
```

### Data Services (Singleton)
```csharp
builder.Services.AddRhymersData();
```

See [DEPENDENCY_INJECTION.md](DEPENDENCY_INJECTION.md) for complete DI documentation.

## Project Dependencies

```
Rhymers.Web ──┐
Rhymers.Api ──┼──> Rhymers.Core
Rhymers.Tests ┤    Rhymers.Data
                  └──> Rhymers.Data
```

```bash
# Build all projects
dotnet build

# Run the application
dotnet run --project src/Rhymers.Desktop/Rhymers.Desktop.csproj

# Build solution
dotnet build Rhymers.sln
```

## Benefits of This Architecture

1. **Separation of Concerns**
   - Core business logic is independent of UI
   - Data access is isolated in its own layer
   - UI depends on Core and Data, not vice versa

2. **Reusability**
   - Core and Data projects can be used in other applications (console, web, etc.)
   - Easy to add Rhymers.Web or Rhymers.Console projects

3. **Testability**
   - Each project can be tested independently
   - Mock implementations can be created for dependencies

4. **Maintainability**
   - Clear dependency graph: Desktop → Data → Core
   - Easier to understand and modify specific layers

## Future Improvements

- Add `Rhymers.Tests` project for unit tests
- Add `Rhymers.Web` project for web interface (ASP.NET/Blazor)
- Implement dependency injection containers
- Add API project for REST endpoints
