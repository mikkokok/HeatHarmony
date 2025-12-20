# Copilot Instructions for HeatHarmony

## General Style & Framework

- Language: C# 12 on .NET 8.
- Use minimal APIs and endpoint routing (`WebApplication`, `MapGet`, `MapGroup`, endpoint filters, etc.).
- Prefer fluent configuration on endpoints (`WithTags`, `WithName`, `Produces`, `RequireAuthorization`, etc.).
- Follow existing namespace patterns, e.g. `HeatHarmony.Routes`, `HeatHarmony.Providers`, `HeatHarmony.Routes.Filters`.

## Routing & Endpoints

- Group related endpoints using `MapGroup` under `ApiMapper` partial classes in `HeatHarmony.Routes`.
  - Example: `app.MapGroup("/appstatus").WithTags("AppStatusEndpoints");`
- When adding new endpoints:
  - Use `Results.*` helpers for responses (e.g. `Results.Ok`, `Results.BadRequest`).
  - Set a descriptive name via `.WithName("SomeDescriptiveName")`.
  - Specify typical response codes using `.Produces(StatusCodes.Status200OK)` etc.
  - If the endpoint should show up in Swagger under a specific group, add `.WithTags("SomeGroupName")`.

## Swagger / OpenAPI & Filters

- Swagger is configured using Swashbuckle with custom operation filters.
- Endpoint metadata is important:
  - Tags are applied via `.WithTags("TagName")` and become `TagsAttribute` entries in `EndpointMetadata`.
  - Attributes like `AllowAnonymous` or auth policies appear there too.
- When writing or modifying filters (e.g. `AppStatusFilter`):
  - Access metadata via `context.ApiDescription.ActionDescriptor.EndpointMetadata`.
  - Never assume a fixed index for metadata; use `.OfType<TAttribute>()` to find what you need.
  - Example to detect app status endpoints:
    ```csharp
    var isAppStatusEndpoint = context.ApiDescription.ActionDescriptor?
        .EndpointMetadata?
        .OfType<Microsoft.AspNetCore.Http.TagsAttribute>()
        .Any(t => t.Tags.Contains("AppStatusEndpoints")) == true;
    ```
  - Example to detect anonymous endpoints:
    ```csharp
    var hasAllowAnonymous = context.ApiDescription.ActionDescriptor?
        .EndpointMetadata?
        .OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>()
        .Any() == true;
    ```

## AppStatus Endpoints

- `Routes\AppStatusEndpoints.cs` defines the `/appstatus` group:
  - Base route: `/appstatus`.
  - Tag: `"AppStatusEndpoints"`.
  - `GET /appstatus/ping`:
    - Returns `{ status = "pong", serverTime = DateTime.Now }`.
    - Name: `"GetAppHealthStatus"`.
  - `GET /appstatus/uptime`:
    - Injects `HeatAutomationWorkerProvider` via `[FromServices]`.
    - Calculates uptime from `provider.StartupLocal`.
    - Returns startup time, server time, and an uptime object.
    - Name: `"GetAppUptime"`.
- When adding new health/status endpoints:
  - Place them in `MapAppStatusEndpoints`.
  - Reuse the `"AppStatusEndpoints"` tag.
  - Keep responses simple and serializable with anonymous types or DTOs.

## Providers & Dependency Injection

- `HeatAutomationWorkerProvider` and `OumanProvider` are DI services:
  - When endpoints need them, inject via parameters with `[FromServices]`.
  - Avoid `new`ing providers manually; rely on DI.
- When Copilot suggests new services:
  - Make them `sealed` where appropriate.
  - Use constructor injection for dependencies.
  - Expose state via properties rather than public fields, unless consistent with existing code.

## Error Handling & Logging

- Use `ILogger<T>` via DI for logging in providers and any background logic.
- For endpoints:
  - Prefer returning proper HTTP status codes over throwing where possible.
  - Use simple validation and `Results.BadRequest` / `Results.Problem` as needed.

## Testing & Safety

- Generate small, focused methods.
- Avoid adding heavy dependencies without clear need.
- Any new public behavior should be easy to unit test (pure methods or endpoints with clearly injected dependencies).

## How to Use This File

- Copilot should follow these conventions when:
  - Generating new endpoints.
  - Modifying Swagger filters or metadata-based logic.
  - Creating new providers or background logic.
- Human maintainers: update this file when architectural decisions change.