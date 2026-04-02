# Pinta Code Quality & Refactoring Suggestions

> **Date:** 2026-03-30
> **Scope:** Full codebase analysis (~487 C# files, ~68,800 LOC)
> **Target:** .NET 8.0 / C# 12, GTK4 via GirCore, Mono.Addins extensibility

---

## Table of Contents

1. [Architecture & Design](#architecture--design)
2. [Code Organization & Large Files](#code-organization--large-files)
3. [Error Handling](#error-handling)
4. [Async & Threading](#async--threading)
5. [Naming & Style Consistency](#naming--style-consistency)
6. [Documentation](#documentation)
7. [C# Modernization](#c-modernization)
8. [Performance](#performance)
9. [Settings & Configuration](#settings--configuration)
10. [Static Analysis & Tooling](#static-analysis--tooling)
11. [Extensibility & Interfaces](#extensibility--interfaces)
12. [Miscellaneous](#miscellaneous)
13. [Summary Scorecard](#summary-scorecard)

---

## Architecture & Design

### S-001: Split `ChromeManager` Into Focused Interfaces

**File:** `Pinta.Core/Managers/ChromeManager.cs`

`ChromeManager` exposes 15+ public properties (`Application`, `MainWindow`, `Dock`, `MainToolBar`, `ToolToolBar`, `ToolBox`, `StatusBar`, `AdjustmentsMenu`, `EffectsMenu`, etc.) making it a God class that conflates application lifecycle, menus, toolbars, dialogs, and status bar management.

**Suggestion:** Extract into focused interfaces:
- `IApplicationProvider` — provides `Gtk.Application` and `Gtk.Window`
- `IMenuProvider` — provides `Gio.Menu` objects for effects/adjustments
- `IToolbarProvider` — provides toolbar widgets
- `IDialogService` — provides progress and error dialogs

This would improve Interface Segregation and allow consumers to depend only on what they need.

---

### S-002: Split `EditActions` Into Smaller Action Groups

**File:** `Pinta.Core/Actions/EditActions.cs` (629 lines, 20 Command properties, 5 manager dependencies)

This class manages undo/redo, clipboard operations, selection operations, **and** palette I/O — violating the Single Responsibility Principle.

**Suggestion:** Split into:
- `EditActions` — undo, redo, cut, copy, paste (~8 commands)
- `SelectionActions` — select all, deselect, invert selection (~5 commands)
- `PaletteActions` — load/save palette (~4 commands)

---

### S-003: Consider Splitting `WorkspaceManager`

**File:** `Pinta.Core/Managers/WorkspaceManager.cs` (470 lines)

Manages documents, active document state, canvas manipulation, and coordinates with multiple other managers.

**Suggestion:** Evaluate whether document lifecycle management (`OpenDocument`, `CloseDocument`, `NewDocument`) could be separated from canvas/viewport coordination (`ZoomToWindow`, `InvalidateWindowRect`, `ScrollCanvas`).

---

### S-004: Reduce Reliance on Static `PintaCore` Facade

**Files:** `Pinta.Core/PintaCore.cs` and numerous consumers

While `PintaCore` provides convenient static access to all managers, it creates tight coupling and makes unit testing difficult. The codebase already has `IServiceProvider`-based injection for tools and effects, which is the better pattern.

**Suggestion:** Gradually migrate remaining direct `PintaCore.XYZ` access (especially in the main `Pinta` project and `Pinta.Core` itself) to constructor-injected `IServiceProvider` dependencies. Prioritize new code; no need for a Big Bang refactor.

---

### S-005: Formalize the Manager Dependency Graph

**File:** `Pinta.Core/PintaCore.cs` (static constructor)

Manager initialization order is implicitly encoded in the static constructor with complex cross-dependencies (e.g., `ActionManager` depends on 7 other managers). This is fragile and hard to reason about.

**Suggestion:** Document the dependency graph explicitly (even as a code comment or diagram in the codebase), and consider using a lightweight DI container (e.g., `Microsoft.Extensions.DependencyInjection`) to make the dependency graph declarative rather than imperative.

---

## Code Organization & Large Files

### S-006: Refactor `BaseEditEngine.cs`

**File:** `Pinta.Tools/Editable/EditEngines/BaseEditEngine.cs` (1,766 lines, 72 public/protected methods)

This is the largest file in the codebase. It mixes shape management, mouse input handling, UI rendering, undo/redo coordination, and drawing logic in a single class.

**Suggestion:** Extract responsibilities into:
- `ShapeStateManager` — manages selected shapes, shape indices
- `ShapeInputHandler` — handles mouse down/move/up for shapes
- `ShapeRenderer` — draws shapes to the canvas
- Keep `BaseEditEngine` as a thin coordinator

---

### S-007: Refactor `TextTool.cs`

**File:** `Pinta.Tools/Tools/TextTool.cs` (1,455 lines)

Combines text rendering, keyboard input handling, clipboard integration, font management, and layout logic.

**Suggestion:** Extract `TextInputHandler` (keyboard/IME), `TextRenderer` (drawing), and `TextToolbarBuilder` (font controls) into separate classes.

---

### S-008: Refactor `ColorPickerDialog.cs`

**File:** `Pinta.Gui.Widgets/Widgets/ColorPickerDialog.cs` (999 lines, 27 public methods, 11 event handlers)

**Suggestion:** Split the color model conversion logic, the UI widget building, and the event handling into separate classes.

---

### S-009: Refactor Large Dialog Files

**Files:**
- `Pinta/Dialogs/NewImageDialog.cs` (636 lines)
- `Pinta.Effects/Dialogs/Effects.LevelsDialog.cs` (670 lines)
- `Pinta.Effects/Dialogs/Effects.CurvesDialog.cs` (664 lines)
- `Pinta.Gui.Widgets/Widgets/SimpleEffectDialog.cs` (683 lines)

**Suggestion:** For each dialog, separate the UI construction from the business logic. Consider a lightweight ViewModel or Presenter class for each dialog's data manipulation logic.

---

## Error Handling

### S-010: Eliminate Empty `catch` Blocks

**Files:**
- `Pinta.Core/ImageFormats/OraFormat.cs` (line ~122): `catch { }` silently swallows file deletion errors
- `Pinta.Core/Extensions/Cairo/CairoExtensions.ColorType.cs` (line ~82): `catch { return null; }` hides parsing errors
- `Pinta.Gui.Addins/InstallDialog.cs` (line ~196): empty catch on addin installation

**Suggestion:** At minimum, log a warning message. Consider defining a simple logging abstraction so the codebase can evolve from `Console.Error.WriteLine` toward structured logging.

---

### S-011: Replace Broad `catch (Exception)` With Specific Exception Types

**Files:** Found in 13+ files including `Main.cs`, `WorkspaceManager.cs`, `SettingsManager.cs`, `AsyncEffectRenderer.cs`, `ResourceManager.cs`, `MacInterop/ApplicationEvents.cs`.

**Suggestion:** Where possible, catch specific exception types (e.g., `IOException`, `FormatException`, `XmlException`) and only fall back to `catch (Exception)` at outermost boundaries. This prevents swallowing unexpected errors and makes debugging easier.

---

### S-012: Introduce a Logging Abstraction

**Current state:** Errors are logged via `Console.Error.WriteLine` in some places and silently swallowed in others.

**Suggestion:** Introduce a simple `ILogger` interface (or adopt `Microsoft.Extensions.Logging`) that can be injected into managers. This would provide consistent error reporting, optional log levels, and a future path to file/structured logging if desired.

---

## Async & Threading

### S-013: Fix Sync-over-Async `.Wait()` Call in `TextTool.cs`

**File:** `Pinta.Tools/Tools/TextTool.cs`
```csharp
CurrentTextEngine.PerformPaste(GdkExtensions.GetDefaultClipboard()).Wait();
```

This blocks the UI thread waiting on an async paste operation. It should be converted to `await` with the containing method made `async`.

---

### S-014: Change `LivePreviewManager.Start()` Return Type From `async void` to `async Task`

**File:** `Pinta.Core/Managers/LivePreviewManager.cs` (line ~70)

`public async void Start(BaseEffect effect)` is fire-and-forget and cannot be awaited by callers. Since this is not an event handler, it should return `Task` so callers can await it, handle errors, and coordinate lifecycle.

---

### S-015: Add Error Handling to `AddinManagerDialog` Fire-and-Forget Pattern

**File:** `Pinta.Gui.Addins/AddinManagerDialog.cs` (line ~122)

```csharp
Task.Run(() => {
    setup_service.Repositories.UpdateAllRepositories(progress_bar);
}).ContinueWith(_ => {
    GLib.Functions.IdleAdd(0, () => { ... });
});
```

Exceptions from `UpdateAllRepositories` are silently swallowed. The `ContinueWith` pattern is also outdated.

**Suggestion:** Convert to `async/await` with try-catch, and use the GLib SynchronizationContext (already set up in `Main.cs`) to marshal back to the UI thread naturally.

---

### S-016: Document Intentional Event Subscription Lifetime

**Observation:** The codebase has 140+ event subscriptions (`+=`) with zero explicit unsubscriptions (`-=`). This appears to be intentional since most objects have application lifetime scope.

**Suggestion:** Add a comment in `MainWindow.cs` and `PintaCore.cs` documenting that event handlers are intentionally never unsubscribed because subscriber and publisher share the same (application) lifetime. This prevents future contributors from seeing it as a bug.

---

## Naming & Style Consistency

### S-017: Standardize Field Naming to `_camelCase`

**Current state:** Private/protected fields use `snake_case` throughout (e.g., `old_surface`, `history_stack`, `image_formats`, `recent_files`).

**C# convention:** `_camelCase` for private fields (e.g., `_oldSurface`, `_historyStack`).

**Note:** The `.editorconfig` currently specifies `_underscore_separator` format for instance fields. This is internally consistent but deviates from mainstream C# style.

**Suggestion:** If the team wants to align with the broader .NET ecosystem, update `.editorconfig` and gradually migrate. If the current style is an intentional project convention, no change needed — but document it.

---

### S-018: Standardize Local Variable Naming

**Current state:** Local variables mix `camelCase` and `snake_case` (e.g., `h_adjust` vs. `lineCount`).

**Suggestion:** Enforce one style for local variables. Since `.editorconfig` specifies `camelCase` for parameters, extending that to locals would be consistent.

---

### S-019: Migrate Remaining `string.Format()` to String Interpolation

**Files:** `Pinta.Core/PaletteFormats/GimpPalette.cs`, `Pinta.Core/ImageFormats/OraFormat.cs`, `Pinta.Core/PaletteFormats/PaintDotNetPalette.cs`, `Pinta.Core/Managers/ToolManager.cs`

**Suggestion:** Replace legacy `string.Format(...)` calls with `$"..."` interpolation for consistency, except where composite format strings are needed for localization or padding alignment.

---

### S-020: Replace String Concatenation With Interpolation

**File:** `Pinta.Core/ImageFormats/OraFormat.cs` (line ~242)
```csharp
writer.WriteAttributeString("src", "data/layer" + i.ToString() + ".png");
```

**Suggestion:** Use `$"data/layer{i}.png"` for readability.

---

## Documentation

### S-021: Add XML Documentation to Public API Surfaces

**Current state:** ~2,590 XML doc comments exist, but many public APIs lack documentation — especially properties in Action classes (`LayerActions`, `EditActions`, etc.) and service interfaces.

**Suggestion:** Prioritize documenting:
1. All `IServiceProvider`-registered interfaces (`IWorkspaceService`, `IToolService`, etc.)
2. `BaseTool` and `BaseEffect` virtual methods (these are extension points for addins)
3. Public properties on Action classes

---

### S-022: Document Extension Points for Addin Developers

**Files:** `Pinta.Core/Classes/BaseTool.cs`, `Pinta.Core/Effects/BaseEffect.cs`

These classes are decorated with `[TypeExtensionPoint]` and are the primary extension mechanism for third-party addins.

**Suggestion:** Add comprehensive XML documentation explaining:
- Which virtual methods should be overridden
- Expected behavior contracts
- The lifecycle of tools/effects (initialize → activate → deactivate → uninitialize)

---

### S-023: Triage and Clean Up TODO Comments

**Current state:** 65+ `TODO` comments exist throughout the codebase, many related to GTK4 binding gaps.

**Suggestion:** Triage the TODOs into categories:
- **GTK4 binding issues** — track upstream and link to GirCore issues
- **Feature work** — convert to GitHub Issues for tracking
- **Code quality** — address or remove stale ones
- **Disabled features** (e.g., `PrintDocumentAction` wrapped in `#if false`) — decide if they should be tracked or removed

---

## C# Modernization

### S-024: Convert `switch` Statements to `switch` Expressions

**Current state:** 48 traditional `switch` statements, 0 switch expressions.

**Example candidate** (`Pinta.Core/ImageFormats/OraFormat.cs`):
```csharp
// Before
switch (mode) {
    case "svg:src-over": return BlendMode.Normal;
    case "svg:multiply": return BlendMode.Multiply;
    // ...
}

// After
return mode switch {
    "svg:src-over" => BlendMode.Normal,
    "svg:multiply" => BlendMode.Multiply,
    // ...
    _ => BlendMode.Normal,
};
```

**Suggestion:** Convert pure mapping switches to switch expressions. Leave complex switches with side effects as-is.

---

### S-025: Use `is null` / `is not null` Consistently

**Current state:** ~48 instances of modern `is null`/`is not null`, but many files still use `== null`/`!= null`.

**Suggestion:** Configure `.editorconfig` to enforce `dotnet_style_prefer_is_null_check_over_reference_equality_method = true:warning` (currently `suggestion`), then run `dotnet format` to auto-fix.

---

### S-026: Add `global using` Directives

**Current state:** No `GlobalUsings.cs` files exist. Every file manually imports common namespaces.

**Suggestion:** Create a `GlobalUsings.cs` in each project for frequently used namespaces:
```csharp
global using System;
global using Cairo;
global using Pinta.Core;
```

This would reduce boilerplate in every file. Start with `Pinta.Core` where `using System;` and `using Cairo;` appear in nearly every file.

---

### S-027: Evaluate Primary Constructors for Simple Classes

**Current state:** Not used anywhere. Records with primary constructors are used.

**Suggestion:** For classes that simply accept and store dependencies (common in tool and manager constructors), primary constructors (C# 12) could reduce boilerplate. Evaluate on a case-by-case basis since primary constructor parameters are mutable captures, not fields.

---

### S-028: Use `required` Members Where Appropriate

**Current state:** Not used anywhere.

**Suggestion:** For data classes with mandatory properties that are currently set via object initializers, consider adding `required` to enforce correct initialization at compile time (C# 11+).

---

## Performance

### S-029: Extract Color Channel Constants

**Files:** `Pinta.Core/Algorithms/PixelOps/UnaryPixelOps.cs`, `Pinta.Core/ImageFormats/OraFormat.cs`, `Pinta.Core/Extensions/Cairo/CairoExtensions.Drawing.cs`

The magic numbers `255`, `256`, `0xff000000`, `0x00ffffff` are repeated extensively in pixel manipulation code.

**Suggestion:** Define constants:
```csharp
private const int MaxChannelValue = 255;
private const int BlendingDivisor = 256;
private const uint AlphaMask = 0xFF000000;
private const uint ColorMask = 0x00FFFFFF;
```

This improves readability, reduces error risk, and documents intent.

---

### S-030: Use `stackalloc` for Small Temporary Buffers

**Current state:** The codebase uses `Span<T>` and `ReadOnlySpan<T>` extensively (excellent), but `stackalloc` is not used for small temporary allocations in algorithms.

**Suggestion:** In performance-critical effect rendering code, consider `stackalloc` for small, known-size temporary buffers (e.g., kernel weights, lookup indices) to avoid GC pressure.

---

### S-031: Consider Reverse Lookup Dictionary for `UserBlendOps`

**File:** `Pinta.Core/Algorithms/PixelOps/UserBlendOps.cs` (line ~66)
```csharp
return blend_modes.Where(p => p.Value == mode).First().Key;
```

This performs a linear search over the dictionary values.

**Suggestion:** Maintain a reverse `Dictionary<BlendMode, string>` for O(1) lookup. Low impact since it's not in a hot path, but it's a clean improvement.

---

## Settings & Configuration

### S-032: Add Type Validation to `SettingsManager.GetSetting<T>()`

**File:** `Pinta.Core/Managers/SettingsManager.cs`
```csharp
return (T)value; // Unsafe cast without verification
```

If a setting is stored as a `string` but requested as `int`, this throws an `InvalidCastException` at runtime.

**Suggestion:** Add a type check before casting, and log a warning if types mismatch, falling back to the default value.

---

### S-033: Consider Thread-Safety for `SettingsManager`

**File:** `Pinta.Core/Managers/SettingsManager.cs`

The internal `Dictionary<string, object>` is not thread-safe. While settings are typically accessed from the UI thread, future changes (e.g., background save) could introduce races.

**Suggestion:** Use `ConcurrentDictionary<string, object>` or add explicit locking as a defensive measure.

---

### S-034: Expand Supported Setting Types

**File:** `Pinta.Core/Managers/SettingsManager.cs`

Currently supports only `int`, `bool`, and `string`. Other types are stored via `ToString()` which loses fidelity.

**Suggestion:** Add support for `double`, `enum` (via name), and `Color` as first-class types. Alternatively, use JSON serialization for the settings file, which would support arbitrary types naturally.

---

## Static Analysis & Tooling

### S-035: Add Roslyn Analyzers

**Current state:** No Roslyn analyzers configured (no StyleCop, no `Microsoft.CodeAnalysis.NetAnalyzers` package, no `.globalconfig`). The only analysis is `dotnet format` in CI.

**Suggestion:** Add `Microsoft.CodeAnalysis.NetAnalyzers` (included with .NET SDK) and enable recommended rules. This catches common bugs, security issues, and performance problems at build time. Start with `AnalysisLevel = latest-recommended` in `Directory.Build.props`.

---

### S-036: Enforce Code Style in CI More Strictly

**Current state:** `dotnet format --verify-no-changes` runs only on .NET 9.0 builds and excludes `CA1416`.

**Suggestion:** Run `dotnet format` on all CI builds (not just .NET 9.0) and consider elevating key `.editorconfig` rules from `suggestion` to `warning` for automated enforcement.

---

### S-037: Add a `.globalconfig` for Analyzer Severity

**Current state:** Analyzer severities are configured only in `.editorconfig`.

**Suggestion:** Create a `.globalconfig` file to configure analyzer severities globally, keeping `.editorconfig` focused on formatting. This is the recommended approach for .NET 5+ projects.

---

## Extensibility & Interfaces

### S-038: Define `IChromeService` More Granularly

**File:** `Pinta.Core/Managers/ChromeManager.cs`

`IChromeService` exposes only 2 members, while `ChromeManager` (its implementation) exposes 15+. Consumers frequently cast or access the concrete type directly.

**Suggestion:** Either expand `IChromeService` with the members consumers actually need, or create additional fine-grained interfaces (`IMenuService`, `IStatusBarService`) so consumers can depend on specific capabilities.

---

### S-039: Consider Replacing Mono.Addins With a Simpler Plugin Model

**Current state:** Mono.Addins (v1.4.2-alpha.4) is used for extensibility. It's a mature but heavy framework, and the version used is a pre-release alpha.

**Suggestion:** Evaluate whether the full Mono.Addins infrastructure is needed. If Pinta only uses `TypeExtensionPoint` for tool/effect discovery, a simpler `Assembly.LoadFrom` + reflection approach or `System.Composition` (MEF2) could suffice with fewer dependencies. This is a significant effort and should only be pursued if Mono.Addins causes maintenance burden.

---

### S-040: Seal More Classes

**Current state:** Many concrete implementations are already `sealed` (good practice).

**Suggestion:** Audit remaining non-sealed, non-abstract classes and seal them unless they are explicitly designed for inheritance. This improves performance (devirtualization) and makes the API contract clearer.

---

## Miscellaneous

### S-041: Remove Disabled `PrintDocumentAction` Code

**File:** `Pinta/Actions/File/PrintDocumentAction.cs`

The entire printing feature is wrapped in `#if false`. If printing is not planned for the near term, remove the dead code and track the feature as a GitHub Issue instead.

**Suggestion:** Remove the `#if false` block and create a tracking issue.

---

### S-042: Address the 65+ TODO Comments Systematically

**Files:** Throughout the codebase

Many TODOs reference GTK4 binding gaps (`TODO-GTK4`), performance improvements, and deferred refactoring.

**Suggestion:** Run a one-time triage:
- Create GitHub Issues for actionable TODOs
- Remove stale TODOs that are no longer relevant
- Add issue references to TODOs that must remain (e.g., `// TODO(#123): ...`)

---

### S-043: Clean Up `ColorPickerDialog` TODO Debt

**File:** `Pinta.Gui.Widgets/Widgets/ColorPickerDialog.cs`

Contains multiple `// TODO: Get rid of this` comments for the `primary_selected` field — repeated 3 times.

**Suggestion:** Either address the TODO or remove the duplicate comments and file an issue.

---

### S-044: Add `IAsyncDisposable` Where Appropriate

**Current state:** Only 1 `IDisposable` implementation exists (in test utilities). Many GTK objects are managed by the runtime.

**Suggestion:** For any future resources requiring cleanup (e.g., file handles, native interop), consider `IAsyncDisposable` alongside `IDisposable` to support async cleanup patterns.

---

### S-045: Consider Introducing a Result Type for Operations

**Current state:** Operations that can fail (file open, image save, addin install) use exceptions or return `bool`.

**Suggestion:** For operations with expected failure modes, consider a `Result<T>` pattern or `OneOf` discriminated union to make error handling explicit in the type system. This is a style choice and should be adopted gradually if at all.

---

---

## Summary Scorecard

| Category | Grade | Rationale |
|----------|-------|-----------|
| **Architecture & Layering** | **B+** | Clean project dependency graph with no circular dependencies. Good use of Mono.Addins for extensibility. The `PintaCore` static facade and some God classes (`ChromeManager`, `EditActions`) prevent an A grade. |
| **SOLID Principles** | **B** | Open/Closed is excellent (9/10). Liskov Substitution is good with sealed classes. SRP is the weak area — several classes exceed their single responsibility. Interface Segregation has room to improve. |
| **Error Handling** | **C+** | Empty catch blocks and broad `catch (Exception)` handlers in 13+ files. No logging abstraction. Console-based error reporting. Functional but fragile under edge cases. |
| **Async & Threading** | **B** | Correct GTK4 SynchronizationContext usage. Good `Task.Run` patterns. One `.Wait()` antipattern and a public `async void` non-event-handler bring the grade down. Thread-safe collections used well. |
| **Naming & Conventions** | **B-** | Consistent PascalCase for types/methods/properties. `snake_case` fields deviate from mainstream C# but are internally consistent. Mixed local variable naming. String handling inconsistencies. |
| **Documentation** | **C** | ~2,590 XML doc comments exist but coverage is spotty. Public APIs, extension points, and action classes lack documentation. No architecture documentation. 65+ undocumented TODOs. |
| **Code Modernization** | **B+** | File-scoped namespaces, records, init-only setters, and collection expressions are adopted. Switch expressions, global usings, `required` members, and `is null` patterns are underutilized. |
| **Performance** | **A-** | Excellent `Span<T>` and `ReadOnlySpan<T>` usage. Row-based pixel processing. Dedicated benchmarks. Minimal unsafe code. Only minor opportunities for `stackalloc` and constant extraction. |
| **Build & CI** | **A-** | Multi-platform CI (Ubuntu, macOS, Windows). `dotnet format` enforcement. Centralized package management. Dependabot configured. Could benefit from Roslyn analyzers and stricter format enforcement. |
| **Extensibility** | **A-** | Well-designed Mono.Addins extension system with `BaseTool` and `BaseEffect` extension points. Clean addin lifecycle. Pre-release alpha dependency is a minor concern. |
| **Code Organization** | **B-** | Several files exceed 600-1,700 lines. 72+ methods in a single class. Good project-level separation but class-level decomposition needs work. |
| **Security & Safety** | **A-** | Nullable reference types enabled globally with excellent adherence. Only 2 null-forgiving operators in the entire codebase. `AllowUnsafeBlocks` enabled but unsafe code is minimal and justified. |

### Overall Grade: **B**

Pinta is a mature, well-structured project with strong fundamentals. The architecture is clean at the project level, performance-critical code is well-optimized, and modern C# features are being adopted progressively. The main areas for improvement are class-level decomposition (several God classes/large files), error handling (empty catches and missing logging), and documentation coverage.
