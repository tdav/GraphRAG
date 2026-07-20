# xUnit → TUnit migration cheat-sheet

Convert both test projects (`tests/MyGraphRagV5.Tests` and `tests/ManagedCode.GraphRag.Tests`) from xUnit v2 to TUnit. Source: TUnit official docs (thomhurst/tunit).

## Golden rules
- **Every TUnit assertion is awaited.** `await Assert.That(actual)...`. A forgotten `await` = a vacuous test. TUnit analyzers + the build catch most; still, be careful. Test methods therefore become `async Task`.
- **Argument order flips.** xUnit `Assert.Equal(expected, actual)` → TUnit `await Assert.That(actual).IsEqualTo(expected)`. The value under test goes inside `That(...)`; the expected goes in the matcher.
- **Preserve every test.** Do not drop or merge tests. The converted project must expose the SAME number of test cases (count `[Fact]`+`[Theory]` cases before/after).
- Keep test bodies/logic identical — only the framework surface (attributes, assertions, lifecycle) changes.

## Attributes
| xUnit | TUnit |
|---|---|
| `[Fact]` | `[Test]` |
| `[Theory]` + `[InlineData(a, b)]` | `[Test]` + `[Arguments(a, b)]` (one `[Arguments]` per `[InlineData]`) |
| `[Fact(Skip = "reason")]` | `[Test, Skip("reason")]` |
| `[Trait("k","v")]` | `[Property("k","v")]` (or drop if unused) |
| `[MemberData]` / `[ClassData]` | `[MethodDataSource(nameof(X))]` / `[ClassDataSource<T>]` — check TUnit docs if encountered |

Method signature: `public void Foo()` → `public async Task Foo()`. If a test was already `async Task`, keep it.

## Assertions (xUnit → TUnit)
- `Assert.Equal(exp, act)` → `await Assert.That(act).IsEqualTo(exp)`
- `Assert.NotEqual(exp, act)` → `await Assert.That(act).IsNotEqualTo(exp)`
- `Assert.True(x)` → `await Assert.That(x).IsTrue()`
- `Assert.False(x)` → `await Assert.That(x).IsFalse()`
- `Assert.Null(x)` → `await Assert.That(x).IsNull()` (may need `(object?)x` when type is a non-nullable value/ambiguous)
- `Assert.NotNull(x)` → `await Assert.That(x).IsNotNull()`
- `Assert.Same(a, b)` → `await Assert.That(b).IsSameReferenceAs(a)`; `Assert.NotSame` → `IsNotSameReferenceAs`
- `Assert.Contains(sub, text)` (string) → `await Assert.That(text).Contains(sub)`
- `Assert.DoesNotContain(sub, text)` → `await Assert.That(text).DoesNotContain(sub)`
- `Assert.StartsWith/EndsWith/Matches` → `await Assert.That(text).StartsWith(..)/.EndsWith(..)/.Matches(..)`
- `Assert.Contains(item, collection)` → `await Assert.That(collection).Contains(item)`
- `Assert.Empty(coll)` → `await Assert.That(coll).IsEmpty()`; `Assert.NotEmpty` → `IsNotEmpty()`
- `Assert.Single(coll)` → `await Assert.That(coll).HasSingleItem()` (or `.HasCount().EqualTo(1)` — verify against TUnit docs); if it returns the element (`var x = Assert.Single(c)`), instead do `await Assert.That(c).HasSingleItem();` then `var x = c.Single();`
- `Assert.IsType<T>(x)` → `await Assert.That(x).IsTypeOf<T>()`; `Assert.IsAssignableFrom<T>(x)` → `IsAssignableTo<T>()`
- `Assert.Throws<T>(() => code)` → `await Assert.That(() => code).Throws<T>()` OR keep `Assert.Throws<T>(...)` (TUnit provides it) — prefer the awaited form. `Assert.ThrowsAsync<T>(async () => ...)` → `await Assert.That(async () => ...).ThrowsExactly<T>()` / `.Throws<T>()`. When the test captures the exception (`var ex = Assert.Throws<T>(...)`) then asserts on it, TUnit: `var ex = await Assert.That(() => code).Throws<T>();` then `await Assert.That(ex.ParamName).IsEqualTo(...)`.
- `Assert.Contains`/`Assert.All`/`Assert.Collection` with lambdas → use TUnit collection assertions; if a clean equivalent isn't obvious, keep the logic as explicit `await Assert.That(...)` checks. When in doubt about an exact matcher name, consult the TUnit docs — do NOT guess a matcher that may not exist (a wrong matcher is a compile error you must resolve, never leave it).

If any assertion has no clean TUnit matcher, rewrite it as an equivalent boolean check: `await Assert.That(<condition>).IsTrue()` — never weaken what the test verifies.

## Lifecycle
- Constructor setup (per-test) — **keep the constructor**; it still runs per test in TUnit. `IDisposable.Dispose()` — keep it.
- `IAsyncLifetime` (`InitializeAsync`/`DisposeAsync`) → `[Before(Test)] public async Task Setup()` and `[After(Test)] public async Task Cleanup()`. Remove the `IAsyncLifetime` interface.
- Class-wide one-time setup → `[Before(Class)] public static async Task ...` / `[After(Class)]`.

## Fixtures
- `IClassFixture<TFixture>` (ctor-injected) → put `[ClassDataSource<TFixture>(Shared = SharedType.PerClass)]` on the test class; inject `TFixture` via the primary constructor. Remove the `IClassFixture<>` interface.
- `[CollectionDefinition("name")]` + `ICollectionFixture<TFixture>` + `[Collection("name")]` → delete the CollectionDefinition class; on each test class in the collection put `[ClassDataSource<TFixture>(Shared = SharedType.Keyed, Key = "name")]` and inject the fixture via primary constructor.
- A fixture implementing `IAsyncLifetime` → make it implement `TUnit.Core.Interfaces.IAsyncInitializer` (`Task InitializeAsync()`) and/or `IAsyncDisposable`/`IDisposable`. (TUnit calls `IAsyncInitializer.InitializeAsync()` when constructing the data source.)

## Conditional skip (e.g. DockerAvailableFactAttribute)
Replace a custom skip-Fact attribute with a `[Before(Test)]` (or `[Before(Class)]`) hook that checks the condition and calls `Skip.Test("reason")`:
```csharp
[Before(Test)]
public void SkipIfNoDocker()
{
    if (!DockerAvailability.IsAvailable) Skip.Test("Docker is not available");
}
```
Apply `[Test]` normally; the hook skips them. (`using TUnit.Core;` for `Skip`.)

## csproj + packages
- Central: add `<PackageVersion Include="TUnit" Version="<latest>" />` to Directory.Packages.props (resolve the latest stable that supports net10.0 — e.g. via `dotnet add ... package TUnit` then pin). The `TUnit` package brings in Microsoft.Testing.Platform transitively. Once BOTH projects are converted and green, REMOVE the `xunit`, `xunit.runner.visualstudio`, and `Microsoft.NET.Test.Sdk` central PackageVersions (they're unused).
- Each test csproj:
  - add `<OutputType>Exe</OutputType>`, `<IsTestProject>true</IsTestProject>`;
  - MTP props: `<UseTestingPlatformProtocol>true</UseTestingPlatformProtocol>`, `<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>`, `<TestingPlatformCaptureOutput>false</TestingPlatformCaptureOutput>`;
  - replace `<PackageReference Include="xunit" />`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` with `<PackageReference Include="TUnit" />`;
  - remove `<Using Include="Xunit" />`; add `<Using Include="TUnit.Core" />` (and rely on TUnit's implicit usings). Keep other usings (Testcontainers, EF, etc.).
- Run with `dotnet test` (works with TUnit via MTP; on .NET 10 flags pass without `--`). `dotnet run --project <testproj>` runs a single TFM.

## CRITICAL GOTCHAS (learned from the MyGraphRagV5.Tests conversion)
- **Collections: use `IsEquivalentTo`, NOT `IsEqualTo`.** `await Assert.That(list).IsEqualTo(otherList)` compiles but does REFERENCE equality on arrays/lists and fails at runtime. For sequence/collection value comparison use `await Assert.That(actual).IsEquivalentTo(expected)`. (Applies to arrays, List<T>, etc.)
- **`dotnet test` needs a repo-root `global.json`** with `{"test":{"runner":"Microsoft.Testing.Platform"}}` on this .NET 10 SDK, else it errors "VSTest target no longer supported". This file is added once for the whole repo. To run a single converted project you can also build it and run the produced `.exe` directly.
- Every `Assert.That(...)` MUST be `await`ed. A grep for `Assert.That` not preceded by `await`/`=> `/`Throws` should return nothing.

## ManagedCode.GraphRag.Tests — specific fixture recipe
This project shares ONE Testcontainers fixture across many integration classes via an xUnit collection:
- `GraphRagApplicationFixture : IAsyncLifetime` (Neo4j/Postgres/Cosmos/Janus containers; `InitializeAsync`/`DisposeAsync`).
- `GraphRagApplicationCollection` = `[CollectionDefinition(nameof(GraphRagApplicationCollection))]` + `ICollectionFixture<GraphRagApplicationFixture>`.
- 11 classes use `[Collection(nameof(GraphRagApplicationCollection))]` and receive the fixture via their constructor.

Convert:
1. `GraphRagApplicationFixture`: change `: IAsyncLifetime` → `: TUnit.Core.Interfaces.IAsyncInitializer, IAsyncDisposable`. Keep `public async Task InitializeAsync()` as-is (that's the `IAsyncInitializer` member). Change `public async Task DisposeAsync()` → `public async ValueTask DisposeAsync()` (IAsyncDisposable returns ValueTask; the body's `await ...DisposeAsync()` calls are fine). All container logic stays identical.
2. DELETE `GraphRagApplicationCollection.cs` (no longer needed).
3. On each of the 11 classes: replace `[Collection(nameof(GraphRagApplicationCollection))]` with `[ClassDataSource<GraphRagApplicationFixture>(Shared = SharedType.PerAssembly)]` (one shared instance for the whole assembly, matching ICollectionFixture). KEEP their constructor that takes `GraphRagApplicationFixture` (TUnit injects it) — or convert to a primary constructor. Then apply the normal `[Fact]→[Test]` + assertion conversion to the test methods.

## Verification
- `dotnet build GraphRag.slnx` green (TUnit analyzers flag un-awaited assertions — treat any such warning/error as a real bug and fix by awaiting).
- Run the converted project's tests; **assert the pass COUNT matches the pre-migration count** (MyGraphRagV5.Tests: 67 unit + 5 integration; ManagedCode.GraphRag.Tests: 244 — many need Docker/emulators and may skip, so verify compile + the container-free subset, and that nothing is silently dropped).
