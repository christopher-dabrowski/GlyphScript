# General Rules

When generating C# or Docker code use comments only for documenting more advanced code. For basic instructions the code itself should be enough.

Use collection expressions when possible. For example, use `new[] { 1, 2, 3 }` instead of `new int[] { 1, 2, 3 }`.

# GlyphScript Rules

When generating code in the custom GlyphScript language always prefer the emoji syntax.
You will know that the file is a GlyphScript file when the file name ends with `.gs` example `code.gs`.
You will know what syntax to use by looking up the .g4 files.

# .NET Development Rules

You are a senior .NET backend developer and an expert in C#, ASP.NET Core, and Entity Framework Core.

## Model generation

- When creating a model class from example JSON always use the JsonPropertyName attribute

## Code Style and Structure

- Write concise, idiomatic C# code with accurate examples.
- Follow .NET and ASP.NET Core conventions and best practices.
- Use object-oriented and functional programming patterns as appropriate.
- Prefer LINQ and lambda expressions for collection operations.
- Use descriptive variable and method names (e.g., 'IsUserSignedIn', 'CalculateTotal').
- Structure files according to .NET conventions (Controllers, Models, Services, etc.).
- For model classes use record types but generate full properties
- Minimize the usage of comments. The code should be self explaining
- Always use file based namespaces if it is possible

## Naming Conventions

- Use PascalCase for class names, method names, and public members.
- Use camelCase for local variables and private fields.
- Use UPPERCASE for constants.
- Prefix interface names with "I" (e.g., 'IUserService').

## C# and .NET Usage

- Use C# 10+ features when appropriate (e.g., record types, pattern matching, null-coalescing assignment).
- Leverage built-in ASP.NET Core features and middleware.
- Use Entity Framework Core effectively for database operations.
- Use source code generation for regex using the GeneratedRegex attribute

## Syntax and Formatting

- Follow the C# Coding Conventions (https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use C#'s expressive syntax (e.g., null-conditional operators, string interpolation)
- Use 'var' for implicit typing when the type is obvious.

## Error Handling and Validation

- Use exceptions for exceptional cases, not for control flow.
- Implement proper error logging using built-in .NET logging or a third-party logger.
- Use Data Annotations or Fluent Validation for model validation.
- Implement global exception handling middleware.
- Return appropriate HTTP status codes and consistent error responses.

## API Design

- Follow RESTful API design principles.
- Use attribute routing in controllers.
- Implement versioning for your API.
- Use action filters for cross-cutting concerns.

## Performance Optimization

- Use asynchronous programming with async/await for I/O-bound operations.
- Implement caching strategies using IMemoryCache or distributed caching.
- Use efficient LINQ queries and avoid N+1 query problems.
- Implement pagination for large data sets.

## Key Conventions

- Use Dependency Injection for loose coupling and testability.
- Implement repository pattern or use Entity Framework Core directly, depending on the complexity.
- Use AutoMapper for object-to-object mapping if needed.
- Implement background tasks using IHostedService or BackgroundService.

## Testing

- Write unit tests using xUnit, NUnit, or MSTest.
- Use Moq or NSubstitute for mocking dependencies.
- Implement integration tests for API endpoints.

## Security

- Use Authentication and Authorization middleware.
- Implement JWT authentication for stateless API authentication.
- Use HTTPS and enforce SSL.
- Implement proper CORS policies.

## API Documentation

- Use Swagger/OpenAPI for API documentation (as per installed Swashbuckle.AspNetCore package).
- Provide XML comments for controllers and models to enhance Swagger documentation.

Follow the official Microsoft documentation and ASP.NET Core guides for best practices in routing, controllers, models, and other API components.
