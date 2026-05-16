> **Note (2026-05-15):** Mod renamed from "Baubles" to "Relics of the Drift" (modid `driftrelics`). Original doc references "Baubles" throughout; treat those as historical.

# Baubles Mod Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Vintage Story mod that adds four accessory slots to the character screen, an affix-based naming system with randomized rolls, an unidentified state with scrambled glyphs, and a Scholar's Lectern workstation that identifies baubles over time.

**Architecture:** A universal C# mod targeting VS 1.20.x and .NET 8. An `EntityBehavior` attached to the player entity holds the bauble `Inventory` (mirroring `EntityBehaviorSeraphInventory`). A new tab on `GuiDialogCharacterBase` is registered via the existing `RenderTabHandlers` extension point. Affix rolls and scrambled names are pure-logic modules that are unit-tested independently; the rest of the surface is exercised via a manual playtest checklist in-game.

**Tech Stack:** .NET 8, Vintage Story 1.20.x API (`VintagestoryAPI.dll`, `VintagestoryLib.dll`, `Vintagestory.dll`, `VSSurvivalMod.dll`), xUnit for the pure-logic test project.

**Reference material:**
- Design spec: `docs/superpowers/specs/2026-05-15-baubles-design.md`
- Decompiled VS source on macgyver: `ssh macgyver`, then `grep -n` files under `~/workspace/vs-api-reference/`.
- Pattern reference for VS modding scaffold/structure: `ssh macgyver`, then `~/workspace/VS-Moding-Projects/IceSkates/`.

**Conventions for this plan:**
- All paths are absolute under `/home/illmatix/workspace/Baubles/`.
- All C# code blocks compile in their final form — copy them in directly.
- Every task ends with a `dotnet build` + a manual-checklist or test-run step + a commit.
- Build commands assume the env var setup IceSkates uses: `DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH`. On hosts where the system `dotnet` already resolves SDK 8.x, you can drop the prefix.
- After Task 9 ships the first runnable state, every subsequent task can be verified in-game by following the checklist embedded in that task. Do not skip the in-game verification — the manual checklist is the test suite for game-bound code.

---

## Task 1: Add xUnit test project for pure-logic modules

**Files:**
- Create: `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/Baubles.Tests.csproj`
- Create: `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/SmokeTest.cs`
- Modify: `/home/illmatix/workspace/Baubles/Baubles.slnx`

The main `Baubles.csproj` references VS DLLs and cannot be tested in isolation, but several modules in this plan (scramble generator, roller, affix loader) are pure C# with no VS dependency. We isolate those into a sibling `Baubles.Tests` project that does not reference the main project — instead, the pure modules will live in `src/` and be added to both projects via `Compile Include` glob in the test project. This avoids dragging VS DLLs into the test runtime.

- [x] **Step 1: Create the test project**

```bash
mkdir -p /home/illmatix/workspace/Baubles/tests/Baubles.Tests
```

Create `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/Baubles.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <RootNamespace>Baubles.Tests</RootNamespace>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.6" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.6" />
  </ItemGroup>

  <!--
    Pure-logic source files are included here directly so the test
    runtime never loads the VS DLLs. As each pure module is added
    to src/, add it to this Compile Include list.
  -->
  <ItemGroup>
    <!-- Populated by subsequent tasks -->
  </ItemGroup>

</Project>
```

- [x] **Step 2: Add a smoke test that proves the runner works**

Create `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/SmokeTest.cs`:

```csharp
using Xunit;

namespace Baubles.Tests;

public class SmokeTest
{
    [Fact]
    public void Runner_Is_Wired_Up()
    {
        Assert.Equal(4, 2 + 2);
    }
}
```

- [x] **Step 3: Add the test project to the solution**

Replace `/home/illmatix/workspace/Baubles/Baubles.slnx` with:

```xml
<Solution>
  <Project Path="Baubles.csproj" />
  <Project Path="tests/Baubles.Tests/Baubles.Tests.csproj" />
</Solution>
```

- [x] **Step 4: Verify the test runs**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Baubles.Tests/Baubles.Tests.csproj 2>&1 | tail -10
```

Expected output (last few lines):
```
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1
```

- [x] **Step 5: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git init  # if not already initialized
git add tests/ Baubles.slnx
git commit -m "test: add xUnit test project for pure-logic modules"
```

---

## Task 2: Define core data types — `BaubleSlotType`, `BaubleInstance`, `IBaubleItem`

**Files:**
- Create: `/home/illmatix/workspace/Baubles/src/Api/BaubleSlotType.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Api/BaubleInstance.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Api/IBaubleItem.cs`
- Modify: `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/Baubles.Tests.csproj`
- Test: `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/BaubleInstanceTests.cs`

These are pure types with no VS dependency. They form the contract that the rest of the mod and downstream mods consume.

- [x] **Step 1: Add the failing test**

Create `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/BaubleInstanceTests.cs`:

```csharp
using Baubles.Api;
using Xunit;

namespace Baubles.Tests;

public class BaubleInstanceTests
{
    [Fact]
    public void BaubleInstance_Equality_Is_Value_Based()
    {
        var a = new BaubleInstance(BaubleSlotType.Ring, "burning", "of_swiftness", 42L, true);
        var b = new BaubleInstance(BaubleSlotType.Ring, "burning", "of_swiftness", 42L, true);

        Assert.Equal(a, b);
    }

    [Fact]
    public void BaubleSlotType_Has_Expected_Members()
    {
        Assert.Equal(0, (int)BaubleSlotType.Ring);
        Assert.Equal(1, (int)BaubleSlotType.Bracelet);
        Assert.Equal(2, (int)BaubleSlotType.Trinket);
    }
}
```

- [x] **Step 2: Run the test and verify it fails with a compile error**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Baubles.Tests/Baubles.Tests.csproj 2>&1 | tail -15
```

Expected: compilation error referencing `Baubles.Api`.

- [x] **Step 3: Create the type files**

Create `/home/illmatix/workspace/Baubles/src/Api/BaubleSlotType.cs`:

```csharp
namespace Baubles.Api;

public enum BaubleSlotType
{
    Ring = 0,
    Bracelet = 1,
    Trinket = 2
}
```

Create `/home/illmatix/workspace/Baubles/src/Api/BaubleInstance.cs`:

```csharp
namespace Baubles.Api;

public sealed record BaubleInstance(
    BaubleSlotType SlotType,
    string? PrefixCode,
    string? SuffixCode,
    long Seed,
    bool Identified);
```

Create `/home/illmatix/workspace/Baubles/src/Api/IBaubleItem.cs`:

```csharp
namespace Baubles.Api;

public interface IBaubleItem
{
    BaubleSlotType SlotType { get; }
}
```

- [x] **Step 4: Add the new pure files to the test project compile list**

Edit `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/Baubles.Tests.csproj`. Replace the `<ItemGroup><!-- Populated by subsequent tasks --></ItemGroup>` block with:

```xml
  <ItemGroup>
    <Compile Include="../../src/Api/BaubleSlotType.cs" Link="Api/BaubleSlotType.cs" />
    <Compile Include="../../src/Api/BaubleInstance.cs" Link="Api/BaubleInstance.cs" />
    <Compile Include="../../src/Api/IBaubleItem.cs" Link="Api/IBaubleItem.cs" />
  </ItemGroup>
```

- [x] **Step 5: Run the tests, expect green**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Baubles.Tests/Baubles.Tests.csproj 2>&1 | tail -10
```

Expected: `Passed: 3, Failed: 0`.

- [x] **Step 6: Build the main project to ensure the new files compile against VS DLLs too**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build Baubles.csproj 2>&1 | tail -5
```

Expected: `0 Error(s)`.

- [x] **Step 7: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add src/Api/ tests/Baubles.Tests/
git commit -m "feat(api): add BaubleSlotType, BaubleInstance, IBaubleItem"
```

---

## Task 3: Define affix and modifier POCOs

**Files:**
- Create: `/home/illmatix/workspace/Baubles/src/Modifier/ModifierOp.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Modifier/ModifierEntry.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Affix/AffixKind.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Affix/Affix.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Affix/AffixConfig.cs`
- Modify: `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/Baubles.Tests.csproj`
- Test: `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/AffixPocoTests.cs`

Pure data types that describe an affix and its modifier list. No VS dependency yet; this lets us test the roller and loader in isolation.

- [x] **Step 1: Write the failing test**

Create `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/AffixPocoTests.cs`:

```csharp
using Baubles.Affix;
using Baubles.Api;
using Baubles.Modifier;
using Xunit;

namespace Baubles.Tests;

public class AffixPocoTests
{
    [Fact]
    public void Affix_Defaults_Are_Sensible()
    {
        var affix = new Affix
        {
            Code = "burning",
            LangKey = "baubles:affix-prefix-burning",
            Kind = AffixKind.Prefix
        };

        Assert.Equal(10, affix.Weight);
        Assert.Null(affix.AllowedSlots);
        Assert.NotNull(affix.Mods);
        Assert.Empty(affix.Mods);
    }

    [Fact]
    public void ModifierEntry_Defaults_To_Add()
    {
        var mod = new ModifierEntry { Key = "maxHealth", Value = 4 };
        Assert.Equal(ModifierOp.Add, mod.Op);
    }

    [Fact]
    public void Affix_Filters_By_Slot_When_AllowedSlots_Set()
    {
        var affix = new Affix
        {
            Code = "of_warding",
            Kind = AffixKind.Suffix,
            AllowedSlots = new[] { BaubleSlotType.Trinket }
        };

        Assert.True(affix.Allows(BaubleSlotType.Trinket));
        Assert.False(affix.Allows(BaubleSlotType.Ring));
    }

    [Fact]
    public void Affix_Allows_All_When_AllowedSlots_Null()
    {
        var affix = new Affix { Code = "burning", Kind = AffixKind.Prefix };

        Assert.True(affix.Allows(BaubleSlotType.Ring));
        Assert.True(affix.Allows(BaubleSlotType.Bracelet));
        Assert.True(affix.Allows(BaubleSlotType.Trinket));
    }
}
```

- [x] **Step 2: Run and verify it fails (compile error)**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Baubles.Tests/Baubles.Tests.csproj 2>&1 | tail -10
```

Expected: error CS0234, missing `Baubles.Affix` / `Baubles.Modifier`.

- [x] **Step 3: Implement the POCOs**

Create `/home/illmatix/workspace/Baubles/src/Modifier/ModifierOp.cs`:

```csharp
namespace Baubles.Modifier;

public enum ModifierOp
{
    Add = 0,
    Mul = 1
}
```

Create `/home/illmatix/workspace/Baubles/src/Modifier/ModifierEntry.cs`:

```csharp
namespace Baubles.Modifier;

public sealed class ModifierEntry
{
    public string Key { get; set; } = "";
    public double Value { get; set; }
    public ModifierOp Op { get; set; } = ModifierOp.Add;
}
```

Create `/home/illmatix/workspace/Baubles/src/Affix/AffixKind.cs`:

```csharp
namespace Baubles.Affix;

public enum AffixKind
{
    Prefix = 0,
    Suffix = 1
}
```

Create `/home/illmatix/workspace/Baubles/src/Affix/Affix.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Baubles.Api;
using Baubles.Modifier;

namespace Baubles.Affix;

public sealed class Affix
{
    public string Code { get; set; } = "";
    public string LangKey { get; set; } = "";
    public AffixKind Kind { get; set; }
    public int Weight { get; set; } = 10;
    public BaubleSlotType[]? AllowedSlots { get; set; }
    public List<ModifierEntry> Mods { get; set; } = new();

    public bool Allows(BaubleSlotType slot)
        => AllowedSlots == null || AllowedSlots.Contains(slot);
}
```

Create `/home/illmatix/workspace/Baubles/src/Affix/AffixConfig.cs`:

```csharp
using System.Collections.Generic;

namespace Baubles.Affix;

public sealed class AffixConfig
{
    public AffixRollChances RollChances { get; set; } = new();
    public List<Affix> Prefixes { get; set; } = new();
    public List<Affix> Suffixes { get; set; } = new();
}

public sealed class AffixRollChances
{
    public double Prefix { get; set; } = 0.75;
    public double Suffix { get; set; } = 0.75;
}
```

- [x] **Step 4: Add files to the test project's compile list**

Edit `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/Baubles.Tests.csproj`. Replace the existing `<ItemGroup>` (the one with `Compile Include` entries) so that it reads:

```xml
  <ItemGroup>
    <Compile Include="../../src/Api/BaubleSlotType.cs" Link="Api/BaubleSlotType.cs" />
    <Compile Include="../../src/Api/BaubleInstance.cs" Link="Api/BaubleInstance.cs" />
    <Compile Include="../../src/Api/IBaubleItem.cs" Link="Api/IBaubleItem.cs" />
    <Compile Include="../../src/Modifier/ModifierOp.cs" Link="Modifier/ModifierOp.cs" />
    <Compile Include="../../src/Modifier/ModifierEntry.cs" Link="Modifier/ModifierEntry.cs" />
    <Compile Include="../../src/Affix/AffixKind.cs" Link="Affix/AffixKind.cs" />
    <Compile Include="../../src/Affix/Affix.cs" Link="Affix/Affix.cs" />
    <Compile Include="../../src/Affix/AffixConfig.cs" Link="Affix/AffixConfig.cs" />
  </ItemGroup>
```

- [x] **Step 5: Run tests, expect green**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Baubles.Tests/Baubles.Tests.csproj 2>&1 | tail -5
```

Expected: 7 passed.

- [x] **Step 6: Build the main project**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build Baubles.csproj 2>&1 | tail -3
```

Expected: `0 Error(s)`.

- [x] **Step 7: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add src/Modifier/ src/Affix/ tests/Baubles.Tests/
git commit -m "feat(affix): add Affix, ModifierEntry, AffixConfig POCOs"
```

---

## Task 4: Implement `ScrambleNameGenerator` (pure, TDD)

**Files:**
- Create: `/home/illmatix/workspace/Baubles/src/Affix/ScrambleNameGenerator.cs`
- Modify: `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/Baubles.Tests.csproj`
- Test: `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/ScrambleNameGeneratorTests.cs`

Pure deterministic name generator. Takes a long seed; returns a gibberish-looking 2–5-syllable name. Must be stable across calls and across .NET 8 runtimes (uses `System.Random` with a seed derived from the input).

- [x] **Step 1: Write the failing tests**

Create `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/ScrambleNameGeneratorTests.cs`:

```csharp
using Baubles.Affix;
using Xunit;

namespace Baubles.Tests;

public class ScrambleNameGeneratorTests
{
    [Fact]
    public void SameSeed_SameOutput()
    {
        var a = ScrambleNameGenerator.Generate(12345L);
        var b = ScrambleNameGenerator.Generate(12345L);
        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentOutputs_For_Sample()
    {
        var seen = new System.Collections.Generic.HashSet<string>();
        for (long s = 1; s <= 100; s++)
        {
            seen.Add(ScrambleNameGenerator.Generate(s));
        }
        Assert.True(seen.Count > 80,
            $"expected >80 distinct names from 100 seeds, got {seen.Count}");
    }

    [Fact]
    public void Output_Starts_Capitalised()
    {
        var name = ScrambleNameGenerator.Generate(7L);
        Assert.True(char.IsUpper(name[0]),
            $"first char of '{name}' should be uppercase");
    }

    [Fact]
    public void Output_Is_Reasonably_Sized()
    {
        for (long s = 1; s <= 50; s++)
        {
            var name = ScrambleNameGenerator.Generate(s);
            Assert.InRange(name.Length, 4, 40);
        }
    }
}
```

- [x] **Step 2: Run, expect compile failure**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Baubles.Tests/Baubles.Tests.csproj 2>&1 | tail -10
```

Expected: missing `ScrambleNameGenerator`.

- [x] **Step 3: Implement the generator**

Create `/home/illmatix/workspace/Baubles/src/Affix/ScrambleNameGenerator.cs`:

```csharp
using System;
using System.Text;

namespace Baubles.Affix;

public static class ScrambleNameGenerator
{
    private static readonly string[] Consonants =
    {
        "th", "sk", "vr", "dr", "kr", "mk", "ven", "ul", "drai", "sko",
        "zh", "fn", "gr", "pl", "qor", "rha"
    };

    private static readonly string[] Vowels =
    {
        "ai", "ul", "ok", "oo", "ae", "io", "an", "or", "ei", "uu"
    };

    public static string Generate(long seed)
    {
        var rng = new Random(SeedToInt(seed));
        int syllableCount = rng.Next(2, 5);
        var sb = new StringBuilder();

        for (int i = 0; i < syllableCount; i++)
        {
            sb.Append(Consonants[rng.Next(Consonants.Length)]);
            sb.Append(Vowels[rng.Next(Vowels.Length)]);
        }

        // Optional " of " connector that splits the name into two halves.
        if (rng.NextDouble() < 0.4)
        {
            int extraSyllables = rng.Next(1, 3);
            sb.Append(" of ");
            for (int i = 0; i < extraSyllables; i++)
            {
                sb.Append(Consonants[rng.Next(Consonants.Length)]);
                sb.Append(Vowels[rng.Next(Vowels.Length)]);
            }
            // Capitalise the second half too.
            int spaceIdx = sb.ToString().LastIndexOf(' ');
            sb[spaceIdx + 1] = char.ToUpperInvariant(sb[spaceIdx + 1]);
        }

        sb[0] = char.ToUpperInvariant(sb[0]);
        return sb.ToString();
    }

    private static int SeedToInt(long seed) => (int)(seed ^ (seed >>> 32));
}
```

- [x] **Step 4: Add to test project compile list**

Edit `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/Baubles.Tests.csproj`, append inside the existing `Compile Include` ItemGroup:

```xml
    <Compile Include="../../src/Affix/ScrambleNameGenerator.cs" Link="Affix/ScrambleNameGenerator.cs" />
```

- [x] **Step 5: Run tests, expect green**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Baubles.Tests/Baubles.Tests.csproj --filter "FullyQualifiedName~ScrambleNameGeneratorTests" 2>&1 | tail -5
```

Expected: 4 passed.

- [x] **Step 6: Eyeball a few outputs (manual sanity check)**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet run --project tests/Baubles.Tests/Baubles.Tests.csproj -- /dev/null 2>/dev/null || true
# Print 5 sample names directly via a quick csharp eval:
cat > /tmp/scramble-eyeball.csx <<'EOF'
#r "tests/Baubles.Tests/bin/Debug/net8.0/Baubles.Tests.dll"
for (long s = 1; s <= 5; s++)
    System.Console.WriteLine($"{s}: {Baubles.Affix.ScrambleNameGenerator.Generate(s)}");
EOF
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet script /tmp/scramble-eyeball.csx 2>/dev/null || echo "(skip dotnet-script if not installed; tests are sufficient)"
```

Expected: five distinct gibberish names. If `dotnet script` is not installed, skip this — the unit tests already prove determinism and distinctness.

- [x] **Step 7: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add src/Affix/ScrambleNameGenerator.cs tests/Baubles.Tests/
git commit -m "feat(affix): add deterministic ScrambleNameGenerator"
```

---

## Task 5: Implement `BaubleRoller` (pure, TDD)

**Files:**
- Create: `/home/illmatix/workspace/Baubles/src/Affix/BaubleRoller.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Affix/AffixPool.cs`
- Modify: `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/Baubles.Tests.csproj`
- Test: `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/BaubleRollerTests.cs`

`BaubleRoller.Roll(slotType, seed, pool)` is a pure function that returns a `BaubleInstance`. The pool is a small abstraction over the affix lists so the roller doesn't need the full `AffixRegistry` (which we'll define later, on top of `AffixPool`).

- [x] **Step 1: Write the failing tests**

Create `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/BaubleRollerTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Baubles.Affix;
using Baubles.Api;
using Xunit;

namespace Baubles.Tests;

public class BaubleRollerTests
{
    private static AffixPool MakePool() => new AffixPool(
        new AffixRollChances { Prefix = 1.0, Suffix = 1.0 },
        new List<Affix>
        {
            new() { Code = "burning",  Kind = AffixKind.Prefix, Weight = 1 },
            new() { Code = "hardened", Kind = AffixKind.Prefix, Weight = 1 },
        },
        new List<Affix>
        {
            new() { Code = "of_swiftness", Kind = AffixKind.Suffix, Weight = 1 },
            new() { Code = "of_the_bear",  Kind = AffixKind.Suffix, Weight = 1 },
        });

    [Fact]
    public void Roll_Is_Deterministic_For_Seed()
    {
        var pool = MakePool();
        var a = BaubleRoller.Roll(BaubleSlotType.Ring, 42L, pool);
        var b = BaubleRoller.Roll(BaubleSlotType.Ring, 42L, pool);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Roll_With_RollChance_Zero_Yields_No_Affixes()
    {
        var pool = new AffixPool(
            new AffixRollChances { Prefix = 0.0, Suffix = 0.0 },
            MakePool().Prefixes, MakePool().Suffixes);

        var roll = BaubleRoller.Roll(BaubleSlotType.Ring, 1L, pool);
        Assert.Null(roll.PrefixCode);
        Assert.Null(roll.SuffixCode);
    }

    [Fact]
    public void Roll_Respects_AllowedSlots()
    {
        var pool = new AffixPool(
            new AffixRollChances { Prefix = 1.0, Suffix = 1.0 },
            new List<Affix>
            {
                new() { Code = "ring_only", Kind = AffixKind.Prefix, Weight = 1,
                        AllowedSlots = new[] { BaubleSlotType.Ring } }
            },
            new List<Affix>
            {
                new() { Code = "trinket_only", Kind = AffixKind.Suffix, Weight = 1,
                        AllowedSlots = new[] { BaubleSlotType.Trinket } }
            });

        var ringRoll = BaubleRoller.Roll(BaubleSlotType.Ring, 1L, pool);
        Assert.Equal("ring_only", ringRoll.PrefixCode);
        Assert.Null(ringRoll.SuffixCode);   // trinket_only filtered out

        var trinketRoll = BaubleRoller.Roll(BaubleSlotType.Trinket, 1L, pool);
        Assert.Null(trinketRoll.PrefixCode);
        Assert.Equal("trinket_only", trinketRoll.SuffixCode);
    }

    [Fact]
    public void Roll_Statistical_Weight_Bias()
    {
        var pool = new AffixPool(
            new AffixRollChances { Prefix = 1.0, Suffix = 0.0 },
            new List<Affix>
            {
                new() { Code = "common", Kind = AffixKind.Prefix, Weight = 9 },
                new() { Code = "rare",   Kind = AffixKind.Prefix, Weight = 1 }
            },
            new List<Affix>());

        int common = 0, rare = 0;
        for (long s = 1; s <= 10_000; s++)
        {
            var r = BaubleRoller.Roll(BaubleSlotType.Ring, s, pool);
            if (r.PrefixCode == "common") common++;
            else if (r.PrefixCode == "rare") rare++;
        }

        // With weights 9:1 expect ~9000:1000. Allow wide slack.
        Assert.InRange(common, 8500, 9500);
        Assert.InRange(rare,    500, 1500);
    }

    [Fact]
    public void Roll_Result_Is_Unidentified()
    {
        var pool = MakePool();
        var roll = BaubleRoller.Roll(BaubleSlotType.Bracelet, 7L, pool);
        Assert.False(roll.Identified);
        Assert.Equal(7L, roll.Seed);
        Assert.Equal(BaubleSlotType.Bracelet, roll.SlotType);
    }
}
```

- [x] **Step 2: Run, expect compile failure**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Baubles.Tests/Baubles.Tests.csproj 2>&1 | tail -10
```

Expected: missing `BaubleRoller` / `AffixPool`.

- [x] **Step 3: Implement `AffixPool` and `BaubleRoller`**

Create `/home/illmatix/workspace/Baubles/src/Affix/AffixPool.cs`:

```csharp
using System.Collections.Generic;

namespace Baubles.Affix;

public sealed class AffixPool
{
    public AffixRollChances RollChances { get; }
    public IReadOnlyList<Affix> Prefixes { get; }
    public IReadOnlyList<Affix> Suffixes { get; }

    public AffixPool(AffixRollChances rollChances,
                     IReadOnlyList<Affix> prefixes,
                     IReadOnlyList<Affix> suffixes)
    {
        RollChances = rollChances;
        Prefixes = prefixes;
        Suffixes = suffixes;
    }
}
```

Create `/home/illmatix/workspace/Baubles/src/Affix/BaubleRoller.cs`:

```csharp
using System;
using System.Collections.Generic;
using Baubles.Api;

namespace Baubles.Affix;

public static class BaubleRoller
{
    public static BaubleInstance Roll(BaubleSlotType slotType, long seed, AffixPool pool)
    {
        var rng = new Random(SeedToInt(seed));

        string? prefix = null;
        if (rng.NextDouble() < pool.RollChances.Prefix)
        {
            prefix = WeightedPick(pool.Prefixes, slotType, rng)?.Code;
        }

        string? suffix = null;
        if (rng.NextDouble() < pool.RollChances.Suffix)
        {
            suffix = WeightedPick(pool.Suffixes, slotType, rng)?.Code;
        }

        return new BaubleInstance(slotType, prefix, suffix, seed, Identified: false);
    }

    private static Affix? WeightedPick(IReadOnlyList<Affix> source,
                                       BaubleSlotType slot, Random rng)
    {
        int totalWeight = 0;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i].Allows(slot)) totalWeight += source[i].Weight;
        }
        if (totalWeight <= 0) return null;

        int roll = rng.Next(totalWeight);
        int acc = 0;
        for (int i = 0; i < source.Count; i++)
        {
            var a = source[i];
            if (!a.Allows(slot)) continue;
            acc += a.Weight;
            if (roll < acc) return a;
        }
        return null;
    }

    private static int SeedToInt(long seed) => (int)(seed ^ (seed >>> 32));
}
```

- [x] **Step 4: Add to test project compile list**

Append to the `Compile Include` ItemGroup in `tests/Baubles.Tests/Baubles.Tests.csproj`:

```xml
    <Compile Include="../../src/Affix/AffixPool.cs" Link="Affix/AffixPool.cs" />
    <Compile Include="../../src/Affix/BaubleRoller.cs" Link="Affix/BaubleRoller.cs" />
```

- [x] **Step 5: Run tests**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Baubles.Tests/Baubles.Tests.csproj --filter "FullyQualifiedName~BaubleRoller" 2>&1 | tail -5
```

Expected: 5 passed.

- [x] **Step 6: Build main project**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build Baubles.csproj 2>&1 | tail -3
```

Expected: `0 Error(s)`.

- [x] **Step 7: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add src/Affix/ tests/Baubles.Tests/
git commit -m "feat(affix): add BaubleRoller and AffixPool"
```

---

## Task 6: Implement `AffixConfigLoader` (JSON → POCOs, pure)

**Files:**
- Create: `/home/illmatix/workspace/Baubles/src/Affix/AffixConfigLoader.cs`
- Modify: `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/Baubles.Tests.csproj`
- Test: `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/AffixConfigLoaderTests.cs`

Takes a JSON string and returns an `AffixConfig`. We use `Newtonsoft.Json` because VS ships it (and we link the same DLL at runtime), but for tests we install the NuGet package locally so we don't link the VS-bound DLL into the test project.

- [x] **Step 1: Add Newtonsoft.Json to the test project**

Edit `tests/Baubles.Tests/Baubles.Tests.csproj` and add to the existing `PackageReference` `ItemGroup`:

```xml
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

- [x] **Step 2: Write the failing tests**

Create `/home/illmatix/workspace/Baubles/tests/Baubles.Tests/AffixConfigLoaderTests.cs`:

```csharp
using Baubles.Affix;
using Baubles.Modifier;
using Xunit;

namespace Baubles.Tests;

public class AffixConfigLoaderTests
{
    private const string SampleJson = @"{
      ""rollChances"": { ""prefix"": 0.8, ""suffix"": 0.6 },
      ""prefixes"": [
        { ""code"": ""burning"",  ""langKey"": ""baubles:affix-prefix-burning"",
          ""kind"": ""Prefix"", ""weight"": 10,
          ""mods"": [
            { ""key"": ""heatResist"",  ""value"": 2 },
            { ""key"": ""meleeDamage"", ""value"": 0.05, ""op"": ""Mul"" }
          ]}
      ],
      ""suffixes"": [
        { ""code"": ""of_swiftness"", ""langKey"": ""baubles:affix-suffix-of_swiftness"",
          ""kind"": ""Suffix"", ""weight"": 10,
          ""mods"": [ { ""key"": ""moveSpeed"", ""value"": 0.05, ""op"": ""Mul"" } ] }
      ]
    }";

    [Fact]
    public void Loads_RollChances()
    {
        var cfg = AffixConfigLoader.LoadFromJson(SampleJson);
        Assert.Equal(0.8, cfg.RollChances.Prefix);
        Assert.Equal(0.6, cfg.RollChances.Suffix);
    }

    [Fact]
    public void Loads_Prefixes_And_Suffixes()
    {
        var cfg = AffixConfigLoader.LoadFromJson(SampleJson);
        Assert.Single(cfg.Prefixes);
        Assert.Single(cfg.Suffixes);

        var burning = cfg.Prefixes[0];
        Assert.Equal("burning", burning.Code);
        Assert.Equal(AffixKind.Prefix, burning.Kind);
        Assert.Equal(10, burning.Weight);
        Assert.Equal(2, burning.Mods.Count);
        Assert.Equal("heatResist", burning.Mods[0].Key);
        Assert.Equal(2.0, burning.Mods[0].Value);
        Assert.Equal(ModifierOp.Add, burning.Mods[0].Op);
        Assert.Equal(ModifierOp.Mul, burning.Mods[1].Op);
    }

    [Fact]
    public void Fills_Default_When_Field_Missing()
    {
        var cfg = AffixConfigLoader.LoadFromJson("{}");
        Assert.NotNull(cfg.RollChances);
        Assert.Equal(0.75, cfg.RollChances.Prefix);
        Assert.Equal(0.75, cfg.RollChances.Suffix);
        Assert.Empty(cfg.Prefixes);
        Assert.Empty(cfg.Suffixes);
    }
}
```

- [x] **Step 3: Run, expect compile failure**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Baubles.Tests/Baubles.Tests.csproj 2>&1 | tail -10
```

Expected: missing `AffixConfigLoader`.

- [x] **Step 4: Implement the loader**

Create `/home/illmatix/workspace/Baubles/src/Affix/AffixConfigLoader.cs`:

```csharp
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Baubles.Affix;

public static class AffixConfigLoader
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Converters = { new StringEnumConverter() },
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    public static AffixConfig LoadFromJson(string json)
    {
        var cfg = JsonConvert.DeserializeObject<AffixConfig>(json, Settings)
                  ?? new AffixConfig();
        cfg.RollChances ??= new AffixRollChances();
        cfg.Prefixes ??= new System.Collections.Generic.List<Affix>();
        cfg.Suffixes ??= new System.Collections.Generic.List<Affix>();

        // Force Kind on entries — JSON authors shouldn't have to repeat it.
        foreach (var a in cfg.Prefixes) a.Kind = AffixKind.Prefix;
        foreach (var a in cfg.Suffixes) a.Kind = AffixKind.Suffix;

        return cfg;
    }
}
```

- [x] **Step 5: Add to test compile list**

Append to the compile `ItemGroup` in `tests/Baubles.Tests/Baubles.Tests.csproj`:

```xml
    <Compile Include="../../src/Affix/AffixConfigLoader.cs" Link="Affix/AffixConfigLoader.cs" />
```

- [x] **Step 6: Run tests**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Baubles.Tests/Baubles.Tests.csproj --filter "FullyQualifiedName~AffixConfigLoader" 2>&1 | tail -5
```

Expected: 3 passed.

- [x] **Step 7: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add src/Affix/AffixConfigLoader.cs tests/Baubles.Tests/
git commit -m "feat(affix): add AffixConfigLoader (Newtonsoft.Json → POCOs)"
```

---

## Task 7: First in-game checkpoint — `BaublesModSystem` skeleton

**Files:**
- Create: `/home/illmatix/workspace/Baubles/src/BaublesModSystem.cs`

Wire up the minimum mod system so that the build deploys a loadable mod even before any features exist. This is the first task whose verification step requires launching the game.

- [x] **Step 1: Create the mod system skeleton**

Create `/home/illmatix/workspace/Baubles/src/BaublesModSystem.cs`:

```csharp
using Vintagestory.API.Common;

namespace Baubles;

public class BaublesModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.Logger.Notification("[Baubles] mod system starting");
    }
}
```

- [x] **Step 2: Build**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build Baubles.csproj 2>&1 | tail -5
```

Expected: `0 Error(s)`. The post-build target deploys to `~/.config/VintagestoryData/Mods/Baubles/`.

- [x] **Step 3: In-game verification**

Launch Vintage Story (`~/.local/share/vintagestory/Vintagestory --tracelog`), open the main menu's "Mods" panel, confirm "Baubles" appears in the list and the log contains:
```
[Baubles] mod system starting
```

- [x] **Step 4: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add src/BaublesModSystem.cs
git commit -m "feat: add BaublesModSystem skeleton (loads, logs notification)"
```

---

## Task 8: `InventoryBaubles` + `ItemSlotBauble` (slot-type enforcement)

**Files:**
- Create: `/home/illmatix/workspace/Baubles/src/Inventory/ItemSlotBauble.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Inventory/InventoryBaubles.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Api/BaublesUtil.cs`

These VS-bound types cannot easily be unit-tested — they take real `ItemStack` and `Collectible` instances. They are verified via Task 9's in-game checkpoint.

- [x] **Step 1: Create `BaublesUtil` (helpers for stack inspection)**

Create `/home/illmatix/workspace/Baubles/src/Api/BaublesUtil.cs`:

```csharp
using System;
using Baubles.Affix;
using Vintagestory.API.Common;

namespace Baubles.Api;

public static class BaublesUtil
{
    private const string AttrSlotType   = "slotType";
    private const string AttrSeed       = "bauble.seed";
    private const string AttrPrefix     = "bauble.prefix";
    private const string AttrSuffix     = "bauble.suffix";
    private const string AttrIdentified = "bauble.identified";

    public static BaubleSlotType? GetSlotType(ItemStack? stack)
    {
        if (stack?.Collectible == null) return null;

        if (stack.Collectible is IBaubleItem bi) return bi.SlotType;

        var attr = stack.Collectible.Attributes?["bauble"]?[AttrSlotType];
        if (attr == null || !attr.Exists) return null;

        var raw = attr.AsString(null);
        if (raw == null) return null;
        return Enum.TryParse<BaubleSlotType>(raw, ignoreCase: true, out var t) ? t : null;
    }

    public static bool IsBauble(ItemStack? stack) => GetSlotType(stack) != null;

    public static bool IsIdentified(ItemStack? stack)
        => stack?.Attributes?.GetBool(AttrIdentified, false) ?? false;

    public static long GetSeed(ItemStack? stack)
        => stack?.Attributes?.GetLong(AttrSeed, 0L) ?? 0L;

    public static string? GetPrefixCode(ItemStack? stack)
    {
        var s = stack?.Attributes?.GetString(AttrPrefix, null);
        return string.IsNullOrEmpty(s) ? null : s;
    }

    public static string? GetSuffixCode(ItemStack? stack)
    {
        var s = stack?.Attributes?.GetString(AttrSuffix, null);
        return string.IsNullOrEmpty(s) ? null : s;
    }

    public static BaubleInstance? GetInstance(ItemStack? stack)
    {
        var slot = GetSlotType(stack);
        if (slot == null) return null;
        return new BaubleInstance(
            slot.Value,
            GetPrefixCode(stack),
            GetSuffixCode(stack),
            GetSeed(stack),
            IsIdentified(stack));
    }

    public static void WriteInstance(ItemStack stack, BaubleInstance instance)
    {
        stack.Attributes.SetLong(AttrSeed, instance.Seed);
        stack.Attributes.SetString(AttrPrefix, instance.PrefixCode ?? "");
        stack.Attributes.SetString(AttrSuffix, instance.SuffixCode ?? "");
        stack.Attributes.SetBool(AttrIdentified, instance.Identified);
    }
}
```

- [x] **Step 2: Create `ItemSlotBauble`**

Create `/home/illmatix/workspace/Baubles/src/Inventory/ItemSlotBauble.cs`:

```csharp
using Baubles.Api;
using Vintagestory.API.Common;

namespace Baubles.Inventory;

public class ItemSlotBauble : ItemSlot
{
    public BaubleSlotType AllowedSlotType { get; }

    public ItemSlotBauble(InventoryBase inventory, BaubleSlotType allowedSlotType)
        : base(inventory)
    {
        AllowedSlotType = allowedSlotType;
        MaxSlotStackSize = 1;
        BackgroundIcon = allowedSlotType.ToString().ToLowerInvariant();
    }

    public override bool CanHold(ItemSlot sourceSlot)
    {
        var stack = sourceSlot?.Itemstack;
        if (stack == null) return false;
        var slotType = BaublesUtil.GetSlotType(stack);
        return slotType == AllowedSlotType;
    }

    public override bool CanTakeFrom(ItemSlot sourceSlot,
                                     EnumMergePriority priority = EnumMergePriority.AutoMerge)
    {
        if (sourceSlot?.Itemstack == null) return false;
        var slotType = BaublesUtil.GetSlotType(sourceSlot.Itemstack);
        return slotType == AllowedSlotType
            && base.CanTakeFrom(sourceSlot, priority);
    }
}
```

- [x] **Step 3: Create `InventoryBaubles`**

Create `/home/illmatix/workspace/Baubles/src/Inventory/InventoryBaubles.cs`:

```csharp
using Baubles.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Baubles.Inventory;

public class InventoryBaubles : InventoryBasePlayer
{
    public const string ClassName = "baubles";
    public const int Size = 4;

    private readonly ItemSlot[] slots;

    public override int Count => slots.Length;
    public override ItemSlot this[int slotId]
    {
        get => slots[slotId];
        set => slots[slotId] = value;
    }

    public InventoryBaubles(string className, string playerUID, ICoreAPI api)
        : base(className, playerUID, api)
    {
        slots = BuildSlots();
        baseWeight = 1.5f;
    }

    public InventoryBaubles(string inventoryId, ICoreAPI api)
        : base(inventoryId, api)
    {
        slots = BuildSlots();
        baseWeight = 1.5f;
    }

    private ItemSlot[] BuildSlots() => new ItemSlot[]
    {
        new ItemSlotBauble(this, BaubleSlotType.Ring),
        new ItemSlotBauble(this, BaubleSlotType.Ring),
        new ItemSlotBauble(this, BaubleSlotType.Bracelet),
        new ItemSlotBauble(this, BaubleSlotType.Trinket),
    };

    public override void FromTreeAttributes(ITreeAttribute tree)
    {
        var dirty = new System.Collections.Generic.List<ItemSlot>();
        SlotsFromTreeAttributes(tree, slots, dirty);
        for (int i = 0; i < dirty.Count; i++) DidModifyItemSlot(dirty[i], null);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
        => SlotsToTreeAttributes(slots, tree);

    protected override ItemSlot NewSlot(int slotId)
    {
        // Layout determines slot type. NewSlot is rarely called for fixed
        // layouts; if the base resizes for any reason, fall back to ring.
        return new ItemSlotBauble(this,
            slotId switch
            {
                0 or 1 => BaubleSlotType.Ring,
                2      => BaubleSlotType.Bracelet,
                _      => BaubleSlotType.Trinket
            });
    }
}
```

- [x] **Step 4: Build**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build Baubles.csproj 2>&1 | tail -5
```

Expected: `0 Error(s)`. (Warnings about VS DLL framework mismatch are expected and ignorable.)

- [x] **Step 5: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add src/Inventory/ src/Api/BaublesUtil.cs
git commit -m "feat(inventory): add InventoryBaubles, ItemSlotBauble, BaublesUtil"
```

---

## Task 9: `EntityBehaviorBaubles` + player entity JSON patch + character tab — first playable

**Files:**
- Create: `/home/illmatix/workspace/Baubles/src/Entity/EntityBehaviorBaubles.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Gui/GuiBaublesTab.cs`
- Create: `/home/illmatix/workspace/Baubles/assets/baubles/patches/entityplayer-behaviors.json`
- Modify: `/home/illmatix/workspace/Baubles/src/BaublesModSystem.cs`

After this task, the character screen will have a Baubles tab with four empty slots. No items yet.

- [x] **Step 1: Create the entity behavior**

Create `/home/illmatix/workspace/Baubles/src/Entity/EntityBehaviorBaubles.cs`:

```csharp
using Baubles.Inventory;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Baubles.Entity;

public class EntityBehaviorBaubles : EntityBehavior
{
    public const string Code = "baubles";

    public InventoryBaubles Inventory { get; private set; } = null!;

    public EntityBehaviorBaubles(Vintagestory.API.Common.Entities.Entity entity)
        : base(entity)
    {
        Inventory = new InventoryBaubles(null!, entity.WatchedAttributes.GetString("playerUID") ?? "", null!);
    }

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        var api = entity.World.Api;
        Inventory = new InventoryBaubles(InventoryBaubles.ClassName,
            entity is Vintagestory.API.Common.Entities.EntityPlayer ep ? ep.PlayerUID : entity.EntityId.ToString(),
            api);
        Inventory.LateInitialize($"{InventoryBaubles.ClassName}-{entity.EntityId}", api);
        LoadFromTree();
        entity.WatchedAttributes.RegisterModifiedListener("baublesInv", LoadFromTree);
        base.Initialize(properties, attributes);
    }

    private void LoadFromTree()
    {
        var tree = entity.WatchedAttributes.GetTreeAttribute("baublesInv");
        if (tree != null) Inventory.FromTreeAttributes(tree);
    }

    public void SaveToTree()
    {
        var tree = new TreeAttribute();
        Inventory.ToTreeAttributes(tree);
        entity.WatchedAttributes["baublesInv"] = tree;
        entity.WatchedAttributes.MarkPathDirty("baublesInv");
    }

    public override string PropertyName() => Code;
}
```

- [x] **Step 2: Create the player entity JSON patch**

Create `/home/illmatix/workspace/Baubles/assets/baubles/patches/entityplayer-behaviors.json`:

```json
[
  {
    "file": "game:entities/humanoid/seraph-male.json",
    "op": "add",
    "path": "/server/behaviors/-",
    "value": { "code": "baubles" }
  },
  {
    "file": "game:entities/humanoid/seraph-male.json",
    "op": "add",
    "path": "/client/behaviors/-",
    "value": { "code": "baubles" }
  },
  {
    "file": "game:entities/humanoid/seraph-female.json",
    "op": "add",
    "path": "/server/behaviors/-",
    "value": { "code": "baubles" }
  },
  {
    "file": "game:entities/humanoid/seraph-female.json",
    "op": "add",
    "path": "/client/behaviors/-",
    "value": { "code": "baubles" }
  }
]
```

- [x] **Step 3: Create the GUI tab composer**

Create `/home/illmatix/workspace/Baubles/src/Gui/GuiBaublesTab.cs`:

```csharp
using Baubles.Entity;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Baubles.Gui;

public static class GuiBaublesTab
{
    public static void Compose(GuiComposer compo, ICoreClientAPI capi)
    {
        var player = capi.World.Player;
        var beh = player?.Entity?.GetBehavior<EntityBehaviorBaubles>();
        if (beh == null) return;

        var inv = beh.Inventory;

        var titleBounds  = ElementBounds.Fixed(0, 25, 385, 25);
        var slotBounds   = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 60, 2, 2);
        var hintBounds   = ElementBounds.Fixed(0, 60 + 2 * (GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGridBase.unscaledSlotPadding) + 20, 385, 50);

        compo.AddStaticText(Lang.Get("baubles:tab-title"),
            CairoFont.WhiteSmallishText(), titleBounds);

        compo.AddItemSlotGrid(inv, dummy => { },
            cols: 2, slotsForGrid: new[] { 0, 1, 2, 3 },
            bounds: slotBounds, key: "baublesGrid");

        compo.AddRichtext(Lang.Get("baubles:tab-hint"),
            CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15), hintBounds);
    }
}
```

- [x] **Step 4: Wire mod system to register behavior, hook character dialog, open inventory on join**

Replace `/home/illmatix/workspace/Baubles/src/BaublesModSystem.cs` with:

```csharp
using System.Linq;
using Baubles.Entity;
using Baubles.Gui;
using Baubles.Inventory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Baubles;

public class BaublesModSystem : ModSystem
{
    private ICoreClientAPI? capi;
    private bool tabRegistered;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterEntityBehaviorClass(EntityBehaviorBaubles.Code, typeof(EntityBehaviorBaubles));
        api.Logger.Notification("[Baubles] mod system starting");
    }

    public override void StartClientSide(ICoreClientAPI capi)
    {
        this.capi = capi;
        capi.Event.LevelFinalize += TryRegisterCharacterTab;
        capi.Event.PlayerJoin += _ => TryRegisterCharacterTab();
    }

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        sapi.Event.PlayerJoin += player =>
        {
            var beh = player.Entity?.GetBehavior<EntityBehaviorBaubles>();
            if (beh?.Inventory != null)
            {
                player.InventoryManager.OpenInventory(beh.Inventory);
            }
        };
    }

    private void TryRegisterCharacterTab()
    {
        if (tabRegistered || capi == null) return;

        var dlg = capi.Gui.LoadedGuis.OfType<GuiDialogCharacterBase>().FirstOrDefault();
        if (dlg == null) return;

        var tabName = Lang.Get("baubles:charactertab-baubles");
        if (dlg.Tabs.Any(t => t.Name == tabName)) return;

        dlg.Tabs.Add(new GuiTab { Name = tabName, DataInt = dlg.Tabs.Count });
        dlg.RenderTabHandlers.Add(compo => GuiBaublesTab.Compose(compo, capi));
        tabRegistered = true;
    }
}
```

- [x] **Step 5: Add the tab labels to the lang file**

Replace `/home/illmatix/workspace/Baubles/assets/baubles/lang/en.json`:

```json
{
  "charactertab-baubles": "Baubles",
  "tab-title": "Baubles",
  "tab-hint": "Hover an empty slot to see what it accepts."
}
```

- [x] **Step 6: Build**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build Baubles.csproj 2>&1 | tail -5
```

Expected: `0 Error(s)`.

- [x] **Step 7: In-game verification**

1. Launch VS: `~/.local/share/vintagestory/Vintagestory --tracelog`
2. Create a new singleplayer world (creative mode).
3. Press the character key (default `C` or `K`).
4. Verify: a "Baubles" tab appears alongside "Character" and "Traits".
5. Click the Baubles tab. Verify: four empty slots are visible (2 rings on top, bracelet + trinket below), each with a placeholder background icon name (the icon textures aren't shipped yet — they'll appear as the slot type string).
6. Quit, reload world. Verify: tab is still there.

If the tab doesn't appear, check the log for `[Baubles]` lines and confirm the entity patch applied (search log for `entityplayer-behaviors.json`).

- [x] **Step 8: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add src/Entity/ src/Gui/ src/BaublesModSystem.cs assets/baubles/patches/ assets/baubles/lang/
git commit -m "feat: add Baubles tab to character screen with empty slots"
```

---

## Task 10: Base `ItemBauble` class + three base item types

**Files:**
- Create: `/home/illmatix/workspace/Baubles/src/Items/ItemBauble.cs`
- Create: `/home/illmatix/workspace/Baubles/assets/baubles/itemtypes/ring.json`
- Create: `/home/illmatix/workspace/Baubles/assets/baubles/itemtypes/bracelet.json`
- Create: `/home/illmatix/workspace/Baubles/assets/baubles/itemtypes/trinket.json`
- Create: `/home/illmatix/workspace/Baubles/assets/baubles/textures/item/ring.png`
- Create: `/home/illmatix/workspace/Baubles/assets/baubles/textures/item/bracelet.png`
- Create: `/home/illmatix/workspace/Baubles/assets/baubles/textures/item/trinket.png`
- Modify: `/home/illmatix/workspace/Baubles/src/BaublesModSystem.cs`
- Modify: `/home/illmatix/workspace/Baubles/assets/baubles/lang/en.json`

After this task we have three real items that can be equipped into their matching slots. No affixes yet.

- [x] **Step 1: Create the `ItemBauble` class**

Create `/home/illmatix/workspace/Baubles/src/Items/ItemBauble.cs`:

```csharp
using System;
using Baubles.Api;
using Vintagestory.API.Common;

namespace Baubles.Items;

public class ItemBauble : Item, IBaubleItem
{
    public BaubleSlotType SlotType
    {
        get
        {
            var raw = Attributes?["bauble"]?["slotType"]?.AsString("Trinket") ?? "Trinket";
            return Enum.TryParse<BaubleSlotType>(raw, ignoreCase: true, out var t)
                ? t
                : BaubleSlotType.Trinket;
        }
    }
}
```

- [x] **Step 2: Register `ItemBauble` in the mod system**

Edit `/home/illmatix/workspace/Baubles/src/BaublesModSystem.cs`. In `Start(ICoreAPI api)`, after the `RegisterEntityBehaviorClass` line, add:

```csharp
        api.RegisterItemClass("ItemBauble", typeof(Baubles.Items.ItemBauble));
```

- [x] **Step 3: Create the three base item JSONs**

Create `/home/illmatix/workspace/Baubles/assets/baubles/itemtypes/ring.json`:

```json
{
  "code": "ring",
  "class": "ItemBauble",
  "maxstacksize": 1,
  "creativeinventory": { "general": ["*"], "items": ["*"] },
  "attributes": { "bauble": { "slotType": "Ring" } },
  "textures": { "all": { "base": "baubles:item/ring" } },
  "shape": { "base": "game:item/basic/coin" }
}
```

Create `/home/illmatix/workspace/Baubles/assets/baubles/itemtypes/bracelet.json`:

```json
{
  "code": "bracelet",
  "class": "ItemBauble",
  "maxstacksize": 1,
  "creativeinventory": { "general": ["*"], "items": ["*"] },
  "attributes": { "bauble": { "slotType": "Bracelet" } },
  "textures": { "all": { "base": "baubles:item/bracelet" } },
  "shape": { "base": "game:item/basic/coin" }
}
```

Create `/home/illmatix/workspace/Baubles/assets/baubles/itemtypes/trinket.json`:

```json
{
  "code": "trinket",
  "class": "ItemBauble",
  "maxstacksize": 1,
  "creativeinventory": { "general": ["*"], "items": ["*"] },
  "attributes": { "bauble": { "slotType": "Trinket" } },
  "textures": { "all": { "base": "baubles:item/trinket" } },
  "shape": { "base": "game:item/basic/coin" }
}
```

- [x] **Step 4: Create placeholder PNG textures**

```bash
cd /home/illmatix/workspace/Baubles/assets/baubles/textures/item
# 16x16 single-color PNGs, one per slot type. Use ImageMagick if available;
# otherwise hand-paint in Krita/Aseprite. For a first pass:
which convert >/dev/null && {
  convert -size 16x16 xc:'#c9a227' ring.png
  convert -size 16x16 xc:'#8b6c1a' bracelet.png
  convert -size 16x16 xc:'#5e3d8f' trinket.png
} || echo "Install ImageMagick or create three 16x16 PNGs manually."
ls -la
```

If ImageMagick is unavailable on this host, copy any 16x16 PNG you have into each path; the item will load with placeholder visuals.

- [x] **Step 5: Add language strings**

Replace `/home/illmatix/workspace/Baubles/assets/baubles/lang/en.json`:

```json
{
  "charactertab-baubles": "Baubles",
  "tab-title": "Baubles",
  "tab-hint": "Hover an empty slot to see what it accepts.",
  "item-ring": "Ring",
  "item-bracelet": "Bracelet",
  "item-trinket": "Trinket"
}
```

- [x] **Step 6: Build**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build Baubles.csproj 2>&1 | tail -5
```

Expected: `0 Error(s)`.

- [x] **Step 7: In-game verification**

1. Launch VS in creative mode.
2. Open creative inventory, search for "ring", "bracelet", "trinket". All three appear.
3. Pick up a ring, open character screen → Baubles tab.
4. Drop the ring on Ring 1 → accepted.
5. Try to drop it on the Bracelet slot → rejected (slot does not accept it).
6. Drop a bracelet on the Bracelet slot → accepted.
7. Drop a trinket on the Trinket slot → accepted.
8. Save world, reload. Verify all three are still equipped.

- [x] **Step 8: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add src/Items/ src/BaublesModSystem.cs assets/baubles/itemtypes/ assets/baubles/textures/item/ assets/baubles/lang/en.json
git commit -m "feat(items): add ring, bracelet, trinket base bauble items"
```

---

## Task 11: Display-name resolution — scrambled vs assembled

**Files:**
- Modify: `/home/illmatix/workspace/Baubles/src/Items/ItemBauble.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Api/BaublesDisplay.cs`
- Modify: `/home/illmatix/workspace/Baubles/assets/baubles/lang/en.json`

`GetHeldItemName` decides what string to show. Stack with `bauble.identified=false` → scrambled. Identified → "[Prefix] [Base] [of Suffix]". The actual roll logic comes later; this task just wires the display path so a hand-edited stack can be inspected.

- [x] **Step 1: Create `BaublesDisplay` (assembles the visible name)**

Create `/home/illmatix/workspace/Baubles/src/Api/BaublesDisplay.cs`:

```csharp
using System.Text;
using Baubles.Affix;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Baubles.Api;

public static class BaublesDisplay
{
    public static string GetDisplayName(ItemStack stack, string fallback)
    {
        if (!BaublesUtil.IsBauble(stack)) return fallback;

        if (!BaublesUtil.IsIdentified(stack))
        {
            return ScrambleNameGenerator.Generate(BaublesUtil.GetSeed(stack));
        }

        var baseName = Lang.Get("baubles:item-" + stack.Collectible.LastCodePart());
        var prefix = BaublesUtil.GetPrefixCode(stack);
        var suffix = BaublesUtil.GetSuffixCode(stack);

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(prefix))
        {
            sb.Append(Lang.Get("baubles:affix-prefix-" + prefix));
            sb.Append(' ');
        }
        sb.Append(baseName);
        if (!string.IsNullOrEmpty(suffix))
        {
            sb.Append(' ');
            sb.Append(Lang.Get("baubles:affix-suffix-" + suffix));
        }
        return sb.ToString();
    }
}
```

- [x] **Step 2: Override `GetHeldItemName` and `GetHeldItemInfo` on `ItemBauble`**

Replace `/home/illmatix/workspace/Baubles/src/Items/ItemBauble.cs`:

```csharp
using System;
using System.Text;
using Baubles.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Baubles.Items;

public class ItemBauble : Item, IBaubleItem
{
    public BaubleSlotType SlotType
    {
        get
        {
            var raw = Attributes?["bauble"]?["slotType"]?.AsString("Trinket") ?? "Trinket";
            return Enum.TryParse<BaubleSlotType>(raw, ignoreCase: true, out var t)
                ? t
                : BaubleSlotType.Trinket;
        }
    }

    public override string GetHeldItemName(ItemStack itemStack)
        => BaublesDisplay.GetDisplayName(itemStack, base.GetHeldItemName(itemStack));

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc,
                                         IWorldAccessor world, bool withDebugInfo)
    {
        var stack = inSlot.Itemstack;
        if (BaublesUtil.IsBauble(stack) && !BaublesUtil.IsIdentified(stack))
        {
            dsc.AppendLine(Lang.Get("baubles:unidentified-hint"));
        }
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
    }
}
```

- [x] **Step 3: Add language strings**

Replace `/home/illmatix/workspace/Baubles/assets/baubles/lang/en.json`:

```json
{
  "charactertab-baubles": "Baubles",
  "tab-title": "Baubles",
  "tab-hint": "Hover an empty slot to see what it accepts.",
  "item-ring": "Ring",
  "item-bracelet": "Bracelet",
  "item-trinket": "Trinket",
  "unidentified-hint": "An unidentified bauble. Research it at a Scholar's Lectern to reveal its true nature.",
  "affix-prefix-burning": "Burning",
  "affix-prefix-hardened": "Hardened",
  "affix-prefix-swift": "Swift",
  "affix-suffix-of_the_bear": "of the Bear",
  "affix-suffix-of_swiftness": "of Swiftness",
  "affix-suffix-of_warding": "of Warding"
}
```

- [x] **Step 4: Build**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build Baubles.csproj 2>&1 | tail -5
```

Expected: `0 Error(s)`.

- [x] **Step 5: In-game verification (mid-feature — limited until rolling exists)**

The default ring/bracelet/trinket items still appear as "Ring", "Bracelet", "Trinket" because they have neither `bauble.identified` nor `bauble.seed` set. They are baubles, so the not-identified branch fires and a scrambled name shows. To verify the scrambled path works without the roller yet:

1. In a creative world, give yourself a ring via `/giveitem baubles:ring`.
2. Use the command `/entity attribute "(target self)" bauble.seed 12345` (or the equivalent in your VS version) to set `bauble.seed` on the held stack. If the in-game attribute command is unavailable in 1.20.x, skip this step — the next task wires real rolls in.

Continue to Task 12 either way; full verification of display naming lives there.

- [x] **Step 6: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add src/Items/ItemBauble.cs src/Api/BaublesDisplay.cs assets/baubles/lang/en.json
git commit -m "feat(items): override GetHeldItemName for scrambled/assembled display"
```

---

## Task 12: `IBaublesAPI` + `BaublesAPI` implementation + `RollUnidentifiedBauble`

**Files:**
- Create: `/home/illmatix/workspace/Baubles/src/Api/IBaublesAPI.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Api/IAffixRegistry.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Api/IModifierRegistry.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Affix/AffixRegistry.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Modifier/ModifierRegistry.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Api/BaublesAPI.cs`
- Modify: `/home/illmatix/workspace/Baubles/src/BaublesModSystem.cs`

Wire up the API surface and the in-memory registries. The affix registry starts empty; Task 14 populates it from JSON.

- [x] **Step 1: Create the public API interface**

Create `/home/illmatix/workspace/Baubles/src/Api/IBaublesAPI.cs`:

```csharp
using System;
using Baubles.Inventory;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Baubles.Api;

public interface IBaublesAPI
{
    InventoryBaubles? GetBaubles(EntityPlayer player);
    bool IsBauble(ItemStack? stack);
    BaubleSlotType? GetSlotType(ItemStack? stack);
    bool IsIdentified(ItemStack? stack);
    BaubleInstance? GetInstance(ItemStack? stack);
    string GetDisplayName(ItemStack stack);

    IAffixRegistry Affixes { get; }
    IModifierRegistry Modifiers { get; }

    ItemStack? RollUnidentifiedBauble(BaubleSlotType slotType, long seed);
    void Identify(ItemStack stack);

    event Action<EntityPlayer, ItemStack, BaubleSlotType> OnBaubleEquipped;
    event Action<EntityPlayer, ItemStack, BaubleSlotType> OnBaubleUnequipped;
    event Action<EntityPlayer, ItemStack> OnBaubleIdentified;
}
```

- [x] **Step 2: Create `IAffixRegistry` and `AffixRegistry`**

Create `/home/illmatix/workspace/Baubles/src/Api/IAffixRegistry.cs`:

```csharp
using Baubles.Affix;

namespace Baubles.Api;

public interface IAffixRegistry
{
    void Register(Affix affix);
    Affix? GetByCode(string code);
    AffixPool BuildPool();
    AffixRollChances RollChances { get; set; }
}
```

Create `/home/illmatix/workspace/Baubles/src/Affix/AffixRegistry.cs`:

```csharp
using System.Collections.Generic;
using Baubles.Api;

namespace Baubles.Affix;

public sealed class AffixRegistry : IAffixRegistry
{
    private readonly Dictionary<string, Affix> byCode = new();
    private readonly List<Affix> prefixes = new();
    private readonly List<Affix> suffixes = new();

    public AffixRollChances RollChances { get; set; } = new();

    public void Register(Affix affix)
    {
        if (string.IsNullOrEmpty(affix.Code))
            throw new System.ArgumentException("Affix.Code must be non-empty", nameof(affix));
        byCode[affix.Code] = affix;
        var list = affix.Kind == AffixKind.Prefix ? prefixes : suffixes;
        list.RemoveAll(a => a.Code == affix.Code);
        list.Add(affix);
    }

    public Affix? GetByCode(string code) => byCode.TryGetValue(code, out var a) ? a : null;

    public AffixPool BuildPool() => new(RollChances, prefixes, suffixes);
}
```

- [x] **Step 3: Create `IModifierRegistry` and `ModifierRegistry`**

Create `/home/illmatix/workspace/Baubles/src/Api/IModifierRegistry.cs`:

```csharp
using Baubles.Modifier;
using Vintagestory.API.Common.Entities;

namespace Baubles.Api;

public delegate void ModifierApplyDelegate(EntityPlayer player, double value, ModifierOp op, string code, bool apply);

public interface IModifierRegistry
{
    void Register(string key, ModifierApplyDelegate handler);
    bool TryApply(EntityPlayer player, ModifierEntry entry, string code);
    bool TryRemove(EntityPlayer player, ModifierEntry entry, string code);
}
```

Create `/home/illmatix/workspace/Baubles/src/Modifier/ModifierRegistry.cs`:

```csharp
using System.Collections.Generic;
using Baubles.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Baubles.Modifier;

public sealed class ModifierRegistry : IModifierRegistry
{
    private readonly Dictionary<string, ModifierApplyDelegate> handlers = new();

    public ModifierRegistry(ICoreAPI api)
    {
        // v1 canonical keys → EntityPlayer.Stats
        Register("moveSpeed",          MakeStatHandler("walkspeed"));
        Register("maxHealth",          MakeStatHandler("maxhealth"));
        Register("meleeDamage",        MakeStatHandler("meleeWeaponsDamage"));
        Register("rangedDamage",       MakeStatHandler("rangedWeaponsDamage"));
        Register("hungerRate",         MakeStatHandler("hungerrate"));
        Register("coldResist",         MakeStatHandler("bodyTempHotMin"));
        Register("heatResist",         MakeStatHandler("bodyTempHotMax"));
        Register("rangedDamageResist", MakeStatHandler("rangedWeaponsDamageReceived"));
    }

    public void Register(string key, ModifierApplyDelegate handler)
        => handlers[key] = handler;

    public bool TryApply(EntityPlayer player, ModifierEntry entry, string code)
    {
        if (!handlers.TryGetValue(entry.Key, out var h)) return false;
        h(player, entry.Value, entry.Op, code, apply: true);
        return true;
    }

    public bool TryRemove(EntityPlayer player, ModifierEntry entry, string code)
    {
        if (!handlers.TryGetValue(entry.Key, out var h)) return false;
        h(player, entry.Value, entry.Op, code, apply: false);
        return true;
    }

    private static ModifierApplyDelegate MakeStatHandler(string statCategory) =>
        (EntityPlayer player, double value, ModifierOp op, string code, bool apply) =>
        {
            if (apply)
            {
                // For ModifierOp.Mul we use the value as a multiplicative delta (e.g. 0.05 = +5%).
                // VS's Stats system stacks all named modifiers additively into the category
                // value, which already approximates Mul-as-bonus-fraction.
                player.Stats.Set(statCategory, code, (float)value, persistent: true);
            }
            else
            {
                player.Stats.Remove(statCategory, code);
            }
        };
}
```

- [x] **Step 4: Create `BaublesAPI` (the concrete implementation)**

Create `/home/illmatix/workspace/Baubles/src/Api/BaublesAPI.cs`:

```csharp
using System;
using Baubles.Affix;
using Baubles.Entity;
using Baubles.Inventory;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Baubles.Api;

public sealed class BaublesAPI : IBaublesAPI
{
    private readonly ICoreAPI api;
    public IAffixRegistry Affixes { get; }
    public IModifierRegistry Modifiers { get; }

    public event Action<EntityPlayer, ItemStack, BaubleSlotType>? OnBaubleEquipped;
    public event Action<EntityPlayer, ItemStack, BaubleSlotType>? OnBaubleUnequipped;
    public event Action<EntityPlayer, ItemStack>? OnBaubleIdentified;

    public BaublesAPI(ICoreAPI api, IAffixRegistry affixes, IModifierRegistry modifiers)
    {
        this.api = api;
        Affixes = affixes;
        Modifiers = modifiers;
    }

    public InventoryBaubles? GetBaubles(EntityPlayer player)
        => player?.GetBehavior<EntityBehaviorBaubles>()?.Inventory;

    public bool IsBauble(ItemStack? stack) => BaublesUtil.IsBauble(stack);
    public BaubleSlotType? GetSlotType(ItemStack? stack) => BaublesUtil.GetSlotType(stack);
    public bool IsIdentified(ItemStack? stack) => BaublesUtil.IsIdentified(stack);
    public BaubleInstance? GetInstance(ItemStack? stack) => BaublesUtil.GetInstance(stack);
    public string GetDisplayName(ItemStack stack)
        => BaublesDisplay.GetDisplayName(stack, stack.GetName());

    public ItemStack? RollUnidentifiedBauble(BaubleSlotType slotType, long seed)
    {
        var code = new AssetLocation("baubles", slotType.ToString().ToLowerInvariant());
        var item = api.World.GetItem(code);
        if (item == null) return null;

        var stack = new ItemStack(item);
        var pool = ((AffixRegistry)Affixes).BuildPool();
        var instance = BaubleRoller.Roll(slotType, seed, pool);
        BaublesUtil.WriteInstance(stack, instance);
        return stack;
    }

    public void Identify(ItemStack stack)
    {
        if (!IsBauble(stack) || IsIdentified(stack)) return;
        stack.Attributes.SetBool("bauble.identified", true);
    }

    // Public so internal consumers (EntityBehaviorBaubles, BEScholarsLectern)
    // can fire events without reflection. External mods should subscribe to
    // OnBaubleEquipped / OnBaubleUnequipped / OnBaubleIdentified instead of
    // calling these directly.
    public void FireEquipped(EntityPlayer player, ItemStack stack, BaubleSlotType type)
        => OnBaubleEquipped?.Invoke(player, stack, type);

    public void FireUnequipped(EntityPlayer player, ItemStack stack, BaubleSlotType type)
        => OnBaubleUnequipped?.Invoke(player, stack, type);

    public void FireIdentified(EntityPlayer player, ItemStack stack)
        => OnBaubleIdentified?.Invoke(player, stack);
}
```

- [x] **Step 5: Update `BaublesModSystem` to construct registries and API**

Replace `/home/illmatix/workspace/Baubles/src/BaublesModSystem.cs`:

```csharp
using System.Linq;
using Baubles.Affix;
using Baubles.Api;
using Baubles.Entity;
using Baubles.Gui;
using Baubles.Inventory;
using Baubles.Modifier;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Baubles;

public class BaublesModSystem : ModSystem
{
    private ICoreClientAPI? capi;
    private bool tabRegistered;

    public AffixRegistry Affixes { get; private set; } = null!;
    public ModifierRegistry Modifiers { get; private set; } = null!;
    public BaublesAPI Api { get; private set; } = null!;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        Affixes   = new AffixRegistry();
        Modifiers = new ModifierRegistry(api);
        Api       = new BaublesAPI(api, Affixes, Modifiers);

        api.RegisterEntityBehaviorClass(EntityBehaviorBaubles.Code, typeof(EntityBehaviorBaubles));
        api.RegisterItemClass("ItemBauble", typeof(Baubles.Items.ItemBauble));
        api.Logger.Notification("[Baubles] mod system starting");
    }

    public override void StartClientSide(ICoreClientAPI capi)
    {
        this.capi = capi;
        capi.Event.LevelFinalize += TryRegisterCharacterTab;
        capi.Event.PlayerJoin += _ => TryRegisterCharacterTab();
    }

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        sapi.Event.PlayerJoin += player =>
        {
            var beh = player.Entity?.GetBehavior<EntityBehaviorBaubles>();
            if (beh?.Inventory != null)
                player.InventoryManager.OpenInventory(beh.Inventory);
        };
    }

    private void TryRegisterCharacterTab()
    {
        if (tabRegistered || capi == null) return;
        var dlg = capi.Gui.LoadedGuis.OfType<GuiDialogCharacterBase>().FirstOrDefault();
        if (dlg == null) return;
        var tabName = Lang.Get("baubles:charactertab-baubles");
        if (dlg.Tabs.Any(t => t.Name == tabName)) return;
        dlg.Tabs.Add(new GuiTab { Name = tabName, DataInt = dlg.Tabs.Count });
        dlg.RenderTabHandlers.Add(compo => GuiBaublesTab.Compose(compo, capi));
        tabRegistered = true;
    }
}
```

- [x] **Step 6: Build**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build Baubles.csproj 2>&1 | tail -5
```

Expected: `0 Error(s)`.

- [x] **Step 7: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add src/Api/ src/Affix/AffixRegistry.cs src/Modifier/ModifierRegistry.cs src/BaublesModSystem.cs
git commit -m "feat(api): add IBaublesAPI, AffixRegistry, ModifierRegistry, BaublesAPI"
```

---

## Task 13: `ItemUnidentifiedRoller` — creative debug roller

**Files:**
- Create: `/home/illmatix/workspace/Baubles/src/Items/ItemUnidentifiedRoller.cs`
- Create: `/home/illmatix/workspace/Baubles/assets/baubles/itemtypes/unidentified-roller.json`
- Create: `/home/illmatix/workspace/Baubles/assets/baubles/textures/item/unidentified-roller.png`
- Modify: `/home/illmatix/workspace/Baubles/src/BaublesModSystem.cs`
- Modify: `/home/illmatix/workspace/Baubles/assets/baubles/lang/en.json`

A creative-only item that, when right-clicked in hand, produces a freshly-rolled unidentified bauble of the current slot type, cycling through ring → bracelet → trinket on each subsequent press. Stack attribute `rollerSlotType` tracks the cycle position.

- [x] **Step 1: Create the roller item class**

Create `/home/illmatix/workspace/Baubles/src/Items/ItemUnidentifiedRoller.cs`:

```csharp
using System;
using Baubles.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace Baubles.Items;

public class ItemUnidentifiedRoller : Item
{
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel, bool firstEvent,
        ref EnumHandHandling handling)
    {
        if (api.Side != EnumAppSide.Server)
        {
            handling = EnumHandHandling.PreventDefault;
            return;
        }

        var player = (byEntity as EntityPlayer)?.Player as IServerPlayer;
        if (player == null) { handling = EnumHandHandling.NotHandled; return; }

        var stack = slot.Itemstack;
        var current = (BaubleSlotType)(stack.Attributes.GetInt("rollerSlotType", 0));
        var next = (BaubleSlotType)(((int)current + 1) % 3);
        stack.Attributes.SetInt("rollerSlotType", (int)next);
        slot.MarkDirty();

        var modSystem = api.ModLoader.GetModSystem<BaublesModSystem>();
        var seed = (long)Guid.NewGuid().GetHashCode() ^ ((long)player.Entity.EntityId << 32);
        var rolled = modSystem.Api.RollUnidentifiedBauble(current, seed);
        if (rolled != null)
        {
            if (!player.InventoryManager.TryGiveItemstack(rolled, true))
            {
                api.World.SpawnItemEntity(rolled, player.Entity.SidedPos.XYZ);
            }
            player.SendMessage(0, $"Rolled {current} (seed {seed:X}). Next press → {next}.",
                EnumChatType.Notification);
        }

        handling = EnumHandHandling.PreventDefault;
    }
}
```

- [x] **Step 2: Register the item class in `BaublesModSystem.Start`**

In `/home/illmatix/workspace/Baubles/src/BaublesModSystem.cs`, immediately after `api.RegisterItemClass("ItemBauble", ...)`, add:

```csharp
        api.RegisterItemClass("ItemUnidentifiedRoller", typeof(Baubles.Items.ItemUnidentifiedRoller));
```

- [x] **Step 3: Create the item JSON**

Create `/home/illmatix/workspace/Baubles/assets/baubles/itemtypes/unidentified-roller.json`:

```json
{
  "code": "unidentified-roller",
  "class": "ItemUnidentifiedRoller",
  "maxstacksize": 1,
  "creativeinventory": { "general": ["*"], "items": ["*"] },
  "textures": { "all": { "base": "baubles:item/unidentified-roller" } },
  "shape": { "base": "game:item/basic/coin" }
}
```

- [x] **Step 4: Create the texture**

```bash
cd /home/illmatix/workspace/Baubles/assets/baubles/textures/item
which convert >/dev/null && convert -size 16x16 xc:'#3aa1a1' unidentified-roller.png \
                       || echo "Install ImageMagick or hand-paint a 16x16 PNG."
```

- [x] **Step 5: Add lang entry**

In `assets/baubles/lang/en.json`, add inside the JSON object:

```json
  "item-unidentified-roller": "Unidentified Roller (debug)",
```

- [x] **Step 6: Build**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build Baubles.csproj 2>&1 | tail -5
```

Expected: `0 Error(s)`.

- [x] **Step 7: In-game verification**

1. Launch a creative world.
2. `/giveitem baubles:unidentified-roller` (or fish it out of the creative inventory).
3. Right-click in hand → chat shows "Rolled Ring (seed …). Next press → Bracelet." A new ring appears in inventory.
4. Hover the ring in inventory → name is a gibberish string ("Drai-Skul Venmok" or similar). Tooltip says "An unidentified bauble. Research it at a Scholar's Lectern to reveal its true nature."
5. Right-click roller again → bracelet rolled with a different seed and different scrambled name.
6. Drop the ring into Ring 1 slot on the Baubles tab → accepted.
7. Save + reload world → ring still in slot, same gibberish name (seed-determinism check).

- [x] **Step 8: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add src/Items/ItemUnidentifiedRoller.cs src/BaublesModSystem.cs assets/baubles/itemtypes/unidentified-roller.json assets/baubles/textures/item/unidentified-roller.png assets/baubles/lang/en.json
git commit -m "feat(items): add ItemUnidentifiedRoller (creative debug)"
```

---

## Task 14: Load affix config from JSON at `AssetsFinalize`

**Files:**
- Create: `/home/illmatix/workspace/Baubles/assets/baubles/config/affixes.json`
- Modify: `/home/illmatix/workspace/Baubles/src/BaublesModSystem.cs`

Until now the `AffixRegistry` has been empty, so all rolls produce baubles with no prefix/suffix. This task loads the v1 starter pool from assets.

- [x] **Step 1: Create the affixes config asset**

Create `/home/illmatix/workspace/Baubles/assets/baubles/config/affixes.json`:

```json
{
  "rollChances": { "prefix": 0.75, "suffix": 0.75 },
  "prefixes": [
    { "code": "burning",  "langKey": "baubles:affix-prefix-burning",  "weight": 10,
      "mods": [
        { "key": "heatResist",   "value": 2 },
        { "key": "meleeDamage",  "value": 0.05, "op": "Mul" }
      ]},
    { "code": "hardened", "langKey": "baubles:affix-prefix-hardened", "weight": 10,
      "mods": [
        { "key": "maxHealth", "value": 2 }
      ]},
    { "code": "swift",    "langKey": "baubles:affix-prefix-swift",    "weight": 10,
      "mods": [
        { "key": "moveSpeed", "value": 0.03, "op": "Mul" }
      ]}
  ],
  "suffixes": [
    { "code": "of_the_bear",  "langKey": "baubles:affix-suffix-of_the_bear",  "weight": 5,
      "mods": [ { "key": "maxHealth", "value": 4 } ]},
    { "code": "of_swiftness", "langKey": "baubles:affix-suffix-of_swiftness", "weight": 10,
      "mods": [ { "key": "moveSpeed", "value": 0.05, "op": "Mul" } ]},
    { "code": "of_warding",   "langKey": "baubles:affix-suffix-of_warding",   "weight": 8,
      "mods": [ { "key": "rangedDamageResist", "value": 0.04, "op": "Mul" } ]}
  ]
}
```

- [x] **Step 2: Hook `AssetsFinalize` in the mod system**

In `/home/illmatix/workspace/Baubles/src/BaublesModSystem.cs`, add the following method to the class (after `Start`):

```csharp
    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        var asset = api.Assets.TryGet(new AssetLocation("baubles", "config/affixes.json"));
        if (asset == null)
        {
            api.Logger.Warning("[Baubles] affixes.json not found — no affixes will roll");
            return;
        }
        var json = asset.ToText();
        var cfg = Baubles.Affix.AffixConfigLoader.LoadFromJson(json);
        Affixes.RollChances = cfg.RollChances;
        foreach (var a in cfg.Prefixes) Affixes.Register(a);
        foreach (var a in cfg.Suffixes) Affixes.Register(a);
        api.Logger.Notification(
            $"[Baubles] loaded {cfg.Prefixes.Count} prefixes, {cfg.Suffixes.Count} suffixes");
    }
```

- [x] **Step 3: Build**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build Baubles.csproj 2>&1 | tail -5
```

Expected: `0 Error(s)`.

- [x] **Step 4: In-game verification**

1. Launch a creative world; check log for `[Baubles] loaded 3 prefixes, 3 suffixes`.
2. Roll 10 baubles with the roller. Inspect their tree attributes via `/entity attribute` or by reading the save file — about ~75% should have a non-empty prefix code, ~75% a non-empty suffix code.
3. Names still appear scrambled (correct — unidentified). Determinism: re-roll the same item type with the same seed (restart world to reset RNG state? — easier to set seed manually via a test). For now, trust the unit tests.

- [x] **Step 5: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add assets/baubles/config/affixes.json src/BaublesModSystem.cs
git commit -m "feat(affix): load starter affix pool from assets/baubles/config/affixes.json"
```

---

## Task 15: Apply / remove modifiers on slot change

**Files:**
- Modify: `/home/illmatix/workspace/Baubles/src/Entity/EntityBehaviorBaubles.cs`
- Modify: `/home/illmatix/workspace/Baubles/src/Inventory/InventoryBaubles.cs`

The bauble inventory must notify the behavior on slot changes so it can apply or remove modifiers and fire API events. We use the existing `OnItemSlotModified` virtual on `InventoryBase`, but we need to know the *previous* stack to remove its mods — that's stashed before the slot change in our subclass.

- [x] **Step 1: Track previous slot contents in `InventoryBaubles`**

In `/home/illmatix/workspace/Baubles/src/Inventory/InventoryBaubles.cs`, add this field inside the class:

```csharp
    public System.Action<int, ItemStack?, ItemStack?>? SlotChanged;
    private readonly ItemStack?[] previousStacks = new ItemStack?[Size];
```

Then override `OnItemSlotModified` and the helper to track previous stacks. Replace the existing `OnItemSlotModified` (if absent, add it) and adjust the existing methods so the final class body reads:

```csharp
    public override void OnItemSlotModified(ItemSlot slot)
    {
        int idx = System.Array.IndexOf(slots, slot);
        if (idx >= 0)
        {
            var oldStack = previousStacks[idx];
            var newStack = slot.Itemstack;
            previousStacks[idx] = newStack?.Clone();
            SlotChanged?.Invoke(idx, oldStack, newStack);
        }
        base.OnItemSlotModified(slot);
    }
```

Also, in `FromTreeAttributes`, after `SlotsFromTreeAttributes(...)`, snapshot the loaded stacks:

```csharp
        for (int i = 0; i < slots.Length; i++)
        {
            previousStacks[i] = slots[i].Itemstack?.Clone();
        }
```

- [x] **Step 2: React to slot changes in `EntityBehaviorBaubles`**

Replace `/home/illmatix/workspace/Baubles/src/Entity/EntityBehaviorBaubles.cs`:

```csharp
using System.Collections.Generic;
using Baubles.Api;
using Baubles.Inventory;
using Baubles.Modifier;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Baubles.Entity;

public class EntityBehaviorBaubles : EntityBehavior
{
    public const string Code = "baubles";

    public InventoryBaubles Inventory { get; private set; } = null!;
    private BaublesModSystem? modSystem;

    public EntityBehaviorBaubles(Vintagestory.API.Common.Entities.Entity entity)
        : base(entity) { }

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        var api = entity.World.Api;
        modSystem = api.ModLoader.GetModSystem<BaublesModSystem>();

        var uid = (entity as EntityPlayer)?.PlayerUID ?? entity.EntityId.ToString();
        Inventory = new InventoryBaubles(InventoryBaubles.ClassName, uid, api);
        Inventory.LateInitialize($"{InventoryBaubles.ClassName}-{entity.EntityId}", api);

        Inventory.SlotChanged = OnSlotChanged;

        LoadFromTree();
        entity.WatchedAttributes.RegisterModifiedListener("baublesInv", LoadFromTree);

        // Re-apply modifiers for every currently-equipped, identified bauble.
        if (api.Side == EnumAppSide.Server)
        {
            for (int i = 0; i < Inventory.Count; i++)
            {
                var stack = Inventory[i].Itemstack;
                if (stack != null && BaublesUtil.IsIdentified(stack))
                {
                    ApplyMods(stack);
                }
            }
        }

        base.Initialize(properties, attributes);
    }

    private void OnSlotChanged(int index, ItemStack? oldStack, ItemStack? newStack)
    {
        if (entity.World.Side != EnumAppSide.Server) return;
        if (entity is not EntityPlayer ep) return;

        if (oldStack != null && BaublesUtil.IsBauble(oldStack))
        {
            if (BaublesUtil.IsIdentified(oldStack)) RemoveMods(oldStack);
            var slotType = BaublesUtil.GetSlotType(oldStack)!.Value;
            modSystem?.Api.FireUnequipped(ep, oldStack, slotType);
        }

        if (newStack != null && BaublesUtil.IsBauble(newStack))
        {
            if (BaublesUtil.IsIdentified(newStack)) ApplyMods(newStack);
            var slotType = BaublesUtil.GetSlotType(newStack)!.Value;
            modSystem?.Api.FireEquipped(ep, newStack, slotType);
        }

        SaveToTree();
    }

    private void ApplyMods(ItemStack stack)
    {
        if (entity is not EntityPlayer ep) return;
        foreach (var entry in EnumerateMods(stack))
        {
            var code = ModifierCode(stack, entry.Key);
            modSystem?.Modifiers.TryApply(ep, entry, code);
        }
    }

    private void RemoveMods(ItemStack stack)
    {
        if (entity is not EntityPlayer ep) return;
        foreach (var entry in EnumerateMods(stack))
        {
            var code = ModifierCode(stack, entry.Key);
            modSystem?.Modifiers.TryRemove(ep, entry, code);
        }
    }

    private IEnumerable<ModifierEntry> EnumerateMods(ItemStack stack)
    {
        var prefix = BaublesUtil.GetPrefixCode(stack);
        var suffix = BaublesUtil.GetSuffixCode(stack);
        if (prefix != null)
        {
            var a = modSystem?.Affixes.GetByCode(prefix);
            if (a != null) foreach (var m in a.Mods) yield return m;
        }
        if (suffix != null)
        {
            var a = modSystem?.Affixes.GetByCode(suffix);
            if (a != null) foreach (var m in a.Mods) yield return m;
        }
    }

    private static string ModifierCode(ItemStack stack, string key)
        => $"baubles:{key}:{BaublesUtil.GetSeed(stack):X}";

    private void LoadFromTree()
    {
        var tree = entity.WatchedAttributes.GetTreeAttribute("baublesInv");
        if (tree != null) Inventory.FromTreeAttributes(tree);
    }

    private void SaveToTree()
    {
        var tree = new TreeAttribute();
        Inventory.ToTreeAttributes(tree);
        entity.WatchedAttributes["baublesInv"] = tree;
        entity.WatchedAttributes.MarkPathDirty("baublesInv");
    }

    public override string PropertyName() => Code;
}
```

(`FireEquipped` / `FireUnequipped` / `FireIdentified` on `BaublesAPI` are
deliberately `public` so internal consumers can fire events without
reflection. External mods should subscribe to the event surface
(`OnBaubleEquipped` etc.) instead of calling these directly.)

- [x] **Step 3: Build**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build Baubles.csproj 2>&1 | tail -5
```

Expected: `0 Error(s)`.

- [x] **Step 4: In-game verification (mid-feature — needs the lectern to identify)**

You can't see modifier effects until you can identify a bauble. To verify the wiring without the lectern, hand-set a bauble's `bauble.identified=true` via console:
- If your VS version exposes a `/entity attribute` admin command, use it.
- Otherwise, skip — Task 16's verification confirms the apply path end-to-end.

What you CAN verify now: equipping an unidentified ring fires `OnBaubleEquipped` and unequipping fires `OnBaubleUnequipped`. Add a one-line `api.Logger.Notification("[Baubles] equip event")` subscription in `StartServerSide` (temporary) if you want a visible signal.

- [x] **Step 5: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add src/Entity/EntityBehaviorBaubles.cs src/Inventory/InventoryBaubles.cs
git commit -m "feat: apply/remove modifiers and fire equip events on slot change"
```

---

## Task 16: Scholar's Lectern — block + block entity + GUI

**Files:**
- Create: `/home/illmatix/workspace/Baubles/src/Blocks/BlockScholarsLectern.cs`
- Create: `/home/illmatix/workspace/Baubles/src/Blocks/BEScholarsLectern.cs`
- Create: `/home/illmatix/workspace/Baubles/assets/baubles/blocktypes/scholarslectern.json`
- Create: `/home/illmatix/workspace/Baubles/assets/baubles/textures/block/scholarslectern.png`
- Create: `/home/illmatix/workspace/Baubles/assets/baubles/config/lectern.json`
- Modify: `/home/illmatix/workspace/Baubles/src/BaublesModSystem.cs`
- Modify: `/home/illmatix/workspace/Baubles/assets/baubles/lang/en.json`

A placeholder cube block with a single-slot inventory and a 60s timed
identify action. v1 uses the default `BlockEntityOpenableContainer` UI
(slot only, no progress bar) — the bespoke progress-bar dialog is
deferred to v1.1 because it requires a custom network channel to push
the dialog open from server to client.

- [x] **Step 1: Create the lectern config asset**

Create `/home/illmatix/workspace/Baubles/assets/baubles/config/lectern.json`:

```json
{ "researchDurationSeconds": 60 }
```

- [x] **Step 2: Create the block class**

Create `/home/illmatix/workspace/Baubles/src/Blocks/BlockScholarsLectern.cs`:

```csharp
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Baubles.Blocks;

public class BlockScholarsLectern : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer,
        BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position)
                is BEScholarsLectern be)
        {
            be.OnPlayerInteract(byPlayer);
            return true;
        }
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
}
```

- [x] **Step 3: Create the block entity**

Create `/home/illmatix/workspace/Baubles/src/Blocks/BEScholarsLectern.cs`:

```csharp
using Baubles.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Baubles.Blocks;

public class BEScholarsLectern : BlockEntityOpenableContainer
{
    private const string DialogTitleKey = "baubles:lectern-title";
    private const int TickMs = 250;

    public InventoryGeneric InventoryRef { get; private set; } = null!;
    public override InventoryBase Inventory => InventoryRef;
    public override string InventoryClassName => "baubles-lectern";

    public float ResearchProgressSeconds { get; private set; }
    public float ResearchDurationSeconds { get; private set; } = 60f;

    public BEScholarsLectern()
    {
        InventoryRef = new InventoryGeneric(1, null, null);
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        InventoryRef.LateInitialize($"baubles-lectern-{Pos.X}/{Pos.Y}/{Pos.Z}", api);

        var cfgAsset = api.Assets.TryGet(new AssetLocation("baubles", "config/lectern.json"));
        if (cfgAsset != null)
        {
            var cfg = Newtonsoft.Json.JsonConvert.DeserializeObject<LecternConfig>(cfgAsset.ToText());
            if (cfg != null) ResearchDurationSeconds = cfg.ResearchDurationSeconds;
        }

        if (api.Side == EnumAppSide.Server) RegisterGameTickListener(OnTick, TickMs);
    }

    private void OnTick(float dt)
    {
        var slot = InventoryRef[0];
        var stack = slot.Itemstack;
        if (stack == null) { Reset(); return; }
        if (!BaublesUtil.IsBauble(stack)) { Reset(); return; }
        if (BaublesUtil.IsIdentified(stack)) { Reset(); return; }

        ResearchProgressSeconds += dt;
        if (ResearchProgressSeconds >= ResearchDurationSeconds)
        {
            var modSystem = Api.ModLoader.GetModSystem<BaublesModSystem>();
            modSystem.Api.Identify(stack);
            slot.MarkDirty();
            MarkDirty(true);
            ResearchProgressSeconds = 0;
        }
        else
        {
            MarkDirty(true);
        }
    }

    private void Reset()
    {
        if (ResearchProgressSeconds != 0)
        {
            ResearchProgressSeconds = 0;
            MarkDirty(true);
        }
    }

    public void OnPlayerInteract(IPlayer byPlayer)
    {
        if (Api.Side == EnumAppSide.Client)
        {
            (Api as Vintagestory.API.Client.ICoreClientAPI)?.Network
                .GetChannel("baubles-lectern")?.SendPacket(new LecternOpenPacket
                {
                    X = Pos.X, Y = Pos.Y, Z = Pos.Z
                });
        }
        if (byPlayer is IServerPlayer sp)
        {
            sp.InventoryManager.OpenInventory(InventoryRef);
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        var invTree = new TreeAttribute();
        InventoryRef.ToTreeAttributes(invTree);
        tree["inventory"] = invTree;
        tree.SetFloat("researchProgress", ResearchProgressSeconds);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        var invTree = tree.GetTreeAttribute("inventory");
        if (invTree != null) InventoryRef.FromTreeAttributes(invTree);
        ResearchProgressSeconds = tree.GetFloat("researchProgress", 0);
    }

    private sealed class LecternConfig { public float ResearchDurationSeconds { get; set; } = 60f; }
    public sealed class LecternOpenPacket { public int X, Y, Z; }
}
```

(Note: opening the inventory server-side via `InventoryManager.OpenInventory` pushes VS's default container dialog onto the client. That's the v1 UI — a slot with no progress bar. The implementer can read `BEScholarsLectern.ResearchProgressSeconds` if they want to add the bar later, but the network plumbing is non-trivial and is explicitly deferred to v1.1.)

- [x] **Step 4: Register block + BE in mod system**

In `/home/illmatix/workspace/Baubles/src/BaublesModSystem.cs`, inside `Start(ICoreAPI api)`, after the existing `RegisterItemClass` lines, add:

```csharp
        api.RegisterBlockClass("BlockScholarsLectern", typeof(Baubles.Blocks.BlockScholarsLectern));
        api.RegisterBlockEntityClass("BEScholarsLectern", typeof(Baubles.Blocks.BEScholarsLectern));
```

- [x] **Step 5: Create the block JSON**

Create `/home/illmatix/workspace/Baubles/assets/baubles/blocktypes/scholarslectern.json`:

```json
{
  "code": "scholarslectern",
  "class": "BlockScholarsLectern",
  "entityClass": "BEScholarsLectern",
  "shape": { "base": "game:block/basic/cube" },
  "textures": { "all": { "base": "baubles:block/scholarslectern" } },
  "creativeinventory": { "general": ["*"], "decorative": ["*"] },
  "blockmaterial": "Wood",
  "drawtype": "Cube",
  "sidesolid": { "all": true },
  "sidesopaque": { "all": true },
  "resistance": 3.5,
  "sounds": { "place": "game:block/planks", "break": "game:block/planks" }
}
```

- [x] **Step 6: Create a placeholder texture**

```bash
cd /home/illmatix/workspace/Baubles/assets/baubles/textures/block
which convert >/dev/null && convert -size 32x32 xc:'#6b4f2a' scholarslectern.png \
                       || echo "Install ImageMagick or hand-paint a 32x32 PNG."
```

- [x] **Step 7: Add lang strings**

In `assets/baubles/lang/en.json` add:

```json
  "block-scholarslectern": "Scholar's Lectern",
  "lectern-title": "Scholar's Lectern",
  "lectern-hint-empty": "Place an unidentified bauble to research.",
  "lectern-hint-identified": "Already identified.",
  "lectern-hint-progress": "Deciphering…",
```

- [x] **Step 8: Build**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build Baubles.csproj 2>&1 | tail -5
```

Expected: `0 Error(s)`.

- [x] **Step 9: In-game verification**

1. Creative world, give yourself a Scholar's Lectern: `/giveblock baubles:scholarslectern`.
2. Place it. Right-click → a single-slot dialog opens (default container UI; the bespoke progress-bar dialog is a polish step — for v1 the default UI is acceptable).
3. Roll an unidentified bauble with the roller, drop it in the lectern slot.
4. Wait 60 seconds (or temporarily set `researchDurationSeconds` to 5 in `assets/baubles/config/lectern.json` for faster iteration).
5. Verify: the bauble's name flips from gibberish to "[Prefix] [Base] [of Suffix]" with affix names resolved from the lang file. `bauble.identified` is now true on the stack.
6. Take it out, drop it into a Ring slot → modifiers apply (check `EntityPlayer.Stats` via `/stats` if available, or notice movement speed change for a `swift`/`of_swiftness` roll).
7. Save + reload → still identified, still has mods applied.

- [x] **Step 10: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add src/Blocks/ src/BaublesModSystem.cs assets/baubles/blocktypes/ assets/baubles/textures/block/ assets/baubles/config/lectern.json assets/baubles/lang/en.json
git commit -m "feat: add Scholar's Lectern block and timed identify"
```

---

## Task 17: Grid recipe for "Unidentified Bauble"

**Files:**
- Create: `/home/illmatix/workspace/Baubles/assets/baubles/recipes/grid/unidentified-bauble.json`

A simple grid recipe so survival players have a v1 source. The result is the `unidentified-roller` item — when held and right-clicked it produces an unidentified bauble of the cycling slot type. (We don't ship per-slot-type recipes; the roller is the v1 affordance.)

- [x] **Step 1: Create the recipe**

Create `/home/illmatix/workspace/Baubles/assets/baubles/recipes/grid/unidentified-bauble.json`:

```json
{
  "ingredientPattern": "GIP_QI_",
  "ingredients": {
    "G": { "type": "item", "code": "game:gear-temporal" },
    "I": { "type": "item", "code": "game:ink-and-quill" },
    "P": { "type": "item", "code": "game:paper-parchment" },
    "Q": { "type": "block", "code": "game:loose-stones-rocktyped-chert" }
  },
  "width": 3,
  "height": 3,
  "output": { "type": "item", "code": "baubles:unidentified-roller", "quantity": 1 }
}
```

(Note: the exact `game:` codes may differ between 1.20 versions. If a code fails to resolve, log will show the failure and you can substitute — `game:gear-temporal` may simply be `game:gear-temporal-meteoriciron` or similar in your installed version. The recipe is a placeholder for the v1.1 loot-table replacement; don't agonise over ingredient choice.)

- [x] **Step 2: Build & verify**

```bash
cd /home/illmatix/workspace/Baubles
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build Baubles.csproj 2>&1 | tail -5
```

In a survival world, place the ingredients in the crafting grid and verify the unidentified roller comes out. If ingredient codes fail, check `~/.config/VintagestoryData/Logs/server-main.log` for "Unable to resolve" lines and substitute in the JSON.

- [x] **Step 3: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add assets/baubles/recipes/grid/unidentified-bauble.json
git commit -m "feat(recipes): add grid recipe for unidentified-roller"
```

---

## Task 18: README polish + final manual checklist

**Files:**
- Modify: `/home/illmatix/workspace/Baubles/README.md`

Update the README to reflect the shipped feature set and document the manual checklist for tagging 0.1.0.

- [x] **Step 1: Replace `/home/illmatix/workspace/Baubles/README.md` with:**

```markdown
# Baubles Mod for Vintage Story

Accessory slots for the character screen, randomly-rolled affix names, and a research lectern for identification.

## Features (0.1.0)

- **4 new accessory slots** on the character screen — Ring × 2, Bracelet, Trinket.
- **Affix-based naming** — Prefix + Base + Suffix (e.g. *Burning Ring of Swiftness*) driven by a JSON-defined pool.
- **Unidentified state** — fresh baubles show a scrambled name and grant no modifiers until studied.
- **Scholar's Lectern** — a workstation that identifies a single bauble over 60 seconds.
- **Modifier framework** — affixes carry stat modifiers (move speed, max health, melee damage, etc.) that apply on equip and remove on unequip.
- **Public API** — other mods can register affixes, modifier handlers, or react to equip/unequip events.

## Installation

Drop the `Baubles` folder into your `VintagestoryData/Mods/` directory.

## Configuration

- `assets/baubles/config/affixes.json` — affix pool. Edit to add/remove prefixes and suffixes, change weights, or rebalance modifier values.
- `assets/baubles/config/lectern.json` — research duration in seconds (default 60).

## API for other mods

```csharp
var baubles = api.ModLoader.GetModSystem<Baubles.BaublesModSystem>().Api;

baubles.OnBaubleEquipped   += (player, stack, slot) => { /* ... */ };
baubles.OnBaubleUnequipped += (player, stack, slot) => { /* ... */ };
baubles.OnBaubleIdentified += (player, stack)       => { /* ... */ };

baubles.Affixes.Register(new Baubles.Affix.Affix { /* ... */ });
baubles.Modifiers.Register("myKey", (player, value, op, code, apply) => { /* ... */ });
```

## Compatibility

- Vintage Story 1.20.x

## Documentation

- Design spec: [docs/superpowers/specs/2026-05-15-baubles-design.md](docs/superpowers/specs/2026-05-15-baubles-design.md)
- Implementation plan: [docs/superpowers/plans/2026-05-15-baubles-implementation.md](docs/superpowers/plans/2026-05-15-baubles-implementation.md)

## Final manual checklist before tagging 0.1.0

Singleplayer:
- [x] Baubles tab visible alongside Character and Traits.
- [x] Slot type enforcement: ring rejects bracelet, etc.
- [x] Persistence across save/load with same prefix/suffix/identified state.
- [x] Scrambled name is deterministic by seed (re-roll same seed → same name).
- [x] Identified bauble shows "[Prefix] [Base] [of Suffix]" with localised affix names.
- [x] Equip an identified bauble → `EntityPlayer.Stats` shows the expected modifier code.
- [x] Unequip → modifier disappears.
- [x] Lectern: place unidentified bauble → progress fills → identified, name resolves.
- [x] Lectern: already-identified bauble passes through without progress.
- [x] Save during research → progress persists across reload.

Multiplayer (host + one client):
- [ ] Each player has their own bauble inventory; reconnects preserve state.
- [ ] Client cannot interact with host's bauble inventory.
- [ ] Both players see the same scrambled name for the same stack.
- [ ] Identifying on the server flips the client tooltip without reconnect.
```

- [x] **Step 2: Commit**

```bash
cd /home/illmatix/workspace/Baubles
git add README.md
git commit -m "docs: update README for 0.1.0 features and manual checklist"
```

---

## End of plan

When all 18 tasks are checked off and the manual checklist in Task 18 passes, the mod is ready to tag as `0.1.0`.

Things explicitly deferred to a later version:
- Survival loot generation (drops from creatures/structures).
- Custom shape/model for the Scholar's Lectern (currently a placeholder cube).
- Bespoke Scholar's Lectern dialog with a progress bar (v1 uses the default
  container UI; the progress is server-side and the player learns the
  bauble is done because its name changes when re-opened or re-extracted).
- Affix rarity tiers (magic / rare / legendary).
- Re-rolling, socketing, transmutation.
- Player-model rendering of equipped baubles.
- ConfigLib integration for in-game affix editing.
