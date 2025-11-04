# MarketScanner.Core

## Purpose
MarketScanner.Core defines shared contracts, enums, and configuration models that express the domain language of the MarketScanner solution. The project contains only pure .NET primitives (records, interfaces, and enums) so that every other layer can coordinate behaviour without introducing infrastructure dependencies.

## Dependency Direction
* All other MarketScanner projects reference **MarketScanner.Core**.
* **MarketScanner.Core** does not reference any UI, data provider, or external API projects.
* No framework-specific abstractions (e.g., `HttpClient`, `ILogger`, `IConfiguration`, or WPF types) live in this project. Consumers are expected to provide their own implementations.

## Extensibility Guidance
When extending the core library:
1. Add only dependency-free records, enums, and interfaces that represent shared contracts.
2. Avoid introducing static global state or referencing infrastructure types.
3. Model new dependencies as interfaces so that implementation projects can supply behaviour without creating circular references.
4. After adding new types, ensure they sit in the appropriate namespace (`MarketScanner.Core.Abstractions` or `MarketScanner.Core.Models`) to keep the project layered and discoverable.

Following these guidelines ensures the solution maintains a clean dependency graph: outer projects depend on Core, while Core remains independent.
