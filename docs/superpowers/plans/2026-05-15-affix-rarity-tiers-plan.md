# Affix Rarity Tiers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 4 rarity tiers (mundane/curious/notable/drift-touched) to the affix system — drives affix count, value scaling, signature implicits per slot type, colored identified names, and a pre-identify tier-colored sigil overlay.

**Architecture:** Tier is a new tree attribute on each relic ItemStack. `RelicRoller` rolls tier from a weighted config, filters the affix pool by `minTier`, rolls N affixes per the tier's `affixCount` rule, and attaches a signature implicit if the tier flag says so. Value scaling is applied at modifier-apply time so source affix `value` stays clean. Display layers richtext color on identified names and an icon overlay on pre-identify stacks.

**Tech Stack:** C# / .NET 10, xUnit, Vintage Story API 1.22, Newtonsoft.Json, Cairo (for the sigil overlay).

**Spec reference:** `docs/superpowers/specs/2026-05-15-affix-rarity-tiers-design.md`. Engineers should read the spec first for design rationale.

---

## File Structure

**New files:**
- `src/Affix/TierConfig.cs` — POCO for a single tier entry
- `src/Affix/SignatureAffix.cs` — POCO for legendary signature implicits
- `src/Affix/TierRoller.cs` — weighted tier selection (pure logic)
- `tests/DriftRelics.Tests/TierConfigLoaderTests.cs`
- `tests/DriftRelics.Tests/TierRollerTests.cs`
- `tests/DriftRelics.Tests/RelicRollerTierTests.cs`
- `tests/DriftRelics.Tests/ModifierScalingTests.cs`

**Modified:**
- `src/Api/RelicInstance.cs` — add `Tier`
- `src/Api/RelicsUtil.cs` — `GetTier` / `SetTier`, scaling helper
- `src/Affix/Affix.cs` — add `MinTier`
- `src/Affix/AffixConfig.cs` — add `Tiers` and `Signatures` blocks; remove `RollChances` (deprecated)
- `src/Affix/AffixConfigLoader.cs` — parse new sections, default tiers if missing
- `src/Affix/AffixPool.cs` — replace `RollChances` with `Tiers` + `Signatures`, expose `FilterByTier`
- `src/Affix/AffixRegistry.cs` — store `Tiers` and `Signatures`, hand them to `BuildPool`
- `src/Affix/RelicRoller.cs` — integrate tier roll + filter + count + signature
- `src/Api/IRelicsAPI.cs` + `src/Api/RelicsAPI.cs` — `GetTier`, `Tiers` property
- `src/Api/RelicsDisplay.cs` — wrap identified name in tier color
- `src/Entity/EntityBehaviorRelics.cs` — `EnumerateMods` yields signature when drift-touched and scales values
- `src/Items/ItemRelic.cs` — pre-identify sigil overlay
- `src/DriftRelicsModSystem.cs` — wire Tiers + Signatures into registry on `AssetsFinalize`
- `assets/driftrelics/config/affixes.json` — add tiers + signatures, mark legacy affixes with `minTier`, add tier-locked content
- `assets/driftrelics/lang/en.json` — tier names, signature flavor, new affix names
- `README.md` — features section update

---

## Task 1: POCOs — RelicInstance.Tier and Affix.MinTier

**Files:**
- Modify: `src/Api/RelicInstance.cs`
- Modify: `src/Affix/Affix.cs`
- Test: `tests/DriftRelics.Tests/AffixPocoTests.cs`

- [ ] **Step 1: Add the failing tests**

Append to `tests/DriftRelics.Tests/AffixPocoTests.cs`:

```csharp
[Fact]
public void RelicInstance_DefaultTier_Is_Mundane()
{
    var instance = new RelicInstance(RelicSlotType.Ring, null, null, 42L, Identified: false);
    Assert.Equal("mundane", instance.Tier);
}

[Fact]
public void RelicInstance_Carries_Explicit_Tier()
{
    var instance = new RelicInstance(RelicSlotType.Ring, "burning", null, 42L, Identified: true, Tier: "drift-touched");
    Assert.Equal("drift-touched", instance.Tier);
}

[Fact]
public void Affix_DefaultMinTier_Is_Mundane()
{
    var affix = new Affix { Code = "burning", Kind = AffixKind.Prefix };
    Assert.Equal("mundane", affix.MinTier);
}
```

- [ ] **Step 2: Run tests, expect compile failure**

Run: `dotnet test tests/DriftRelics.Tests/DriftRelics.Tests.csproj --nologo --filter "FullyQualifiedName~AffixPocoTests"`
Expected: build error — `RelicInstance` has no `Tier`, `Affix` has no `MinTier`.

- [ ] **Step 3: Add `Tier` to RelicInstance**

Replace `src/Api/RelicInstance.cs` with:

```csharp
namespace DriftRelics.Api;

public sealed record RelicInstance(
    RelicSlotType SlotType,
    string? PrefixCode,
    string? SuffixCode,
    long Seed,
    bool Identified,
    string Tier = "mundane");
```

- [ ] **Step 4: Add `MinTier` to Affix**

In `src/Affix/Affix.cs`, add after the `Weight` property:

```csharp
public string MinTier { get; set; } = "mundane";
```

- [ ] **Step 5: Run tests, expect pass**

Run: `dotnet test tests/DriftRelics.Tests/DriftRelics.Tests.csproj --nologo --filter "FullyQualifiedName~AffixPocoTests"`
Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Api/RelicInstance.cs src/Affix/Affix.cs tests/DriftRelics.Tests/AffixPocoTests.cs
git commit -m "feat(affix): add Tier to RelicInstance and MinTier to Affix"
```

---

## Task 2: TierConfig and SignatureAffix POCOs + AffixConfig schema

**Files:**
- Create: `src/Affix/TierConfig.cs`
- Create: `src/Affix/SignatureAffix.cs`
- Modify: `src/Affix/AffixConfig.cs`
- Test: `tests/DriftRelics.Tests/AffixPocoTests.cs` (extend)

- [ ] **Step 1: Add the failing tests**

Append to `AffixPocoTests.cs`:

```csharp
[Fact]
public void TierConfig_Defaults_Are_Sensible()
{
    var t = new TierConfig { Code = "mundane" };
    Assert.Equal(50, t.Weight);
    Assert.Equal("#aaaaaa", t.Color);
    Assert.Equal(1, t.AffixCount);
    Assert.Equal(1.0, t.ValueScale);
    Assert.False(t.Signature);
}

[Fact]
public void SignatureAffix_Defaults_Are_Sensible()
{
    var s = new SignatureAffix();
    Assert.NotNull(s.Mods);
    Assert.Empty(s.Mods);
}
```

- [ ] **Step 2: Run tests, expect compile failure**

Run the same dotnet test command; expect missing-type errors.

- [ ] **Step 3: Create `src/Affix/TierConfig.cs`**

```csharp
namespace DriftRelics.Affixes;

public sealed class TierConfig
{
    public string Code { get; set; } = "";
    public int Weight { get; set; } = 50;
    public string Color { get; set; } = "#aaaaaa";
    public int AffixCount { get; set; } = 1;
    public double ValueScale { get; set; } = 1.0;
    public bool Signature { get; set; } = false;
}
```

- [ ] **Step 4: Create `src/Affix/SignatureAffix.cs`**

```csharp
using System.Collections.Generic;
using DriftRelics.Modifier;

namespace DriftRelics.Affixes;

public sealed class SignatureAffix
{
    public string Code { get; set; } = "";
    public string LangKey { get; set; } = "";
    public List<ModifierEntry> Mods { get; set; } = new();
}
```

- [ ] **Step 5: Replace `src/Affix/AffixConfig.cs`**

```csharp
using System.Collections.Generic;
using DriftRelics.Api;

namespace DriftRelics.Affixes;

public sealed class AffixConfig
{
    public List<TierConfig> Tiers { get; set; } = new();
    public Dictionary<string, SignatureAffix> Signatures { get; set; } = new();
    public List<Affix> Prefixes { get; set; } = new();
    public List<Affix> Suffixes { get; set; } = new();
}
```

Notes for the engineer:
- `Signatures` is keyed by `RelicSlotType.ToString().ToLowerInvariant()` (`"ring"`, `"bracelet"`, `"trinket"`).
- Old `AffixRollChances` class and `RollChances` property are gone. References elsewhere in the codebase will fail to compile in later tasks; that's expected and will be cleaned up.

- [ ] **Step 6: Run tests, expect pass**

```
dotnet test tests/DriftRelics.Tests/DriftRelics.Tests.csproj --nologo --filter "FullyQualifiedName~AffixPocoTests"
```

Main project will not build yet because of references to `RollChances`. That's fine — fixed in next tasks.

- [ ] **Step 7: Commit**

```bash
git add src/Affix/TierConfig.cs src/Affix/SignatureAffix.cs src/Affix/AffixConfig.cs tests/DriftRelics.Tests/AffixPocoTests.cs
git commit -m "feat(affix): add TierConfig + SignatureAffix POCOs to AffixConfig"
```

---

## Task 3: AffixConfigLoader parses tiers + signatures + minTier

**Files:**
- Modify: `src/Affix/AffixConfigLoader.cs`
- Test: Create `tests/DriftRelics.Tests/TierConfigLoaderTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/DriftRelics.Tests/TierConfigLoaderTests.cs`:

```csharp
using System.Linq;
using DriftRelics.Affixes;
using Xunit;

namespace DriftRelics.Tests;

public class TierConfigLoaderTests
{
    private const string Json = @"{
      ""tiers"": [
        { ""code"": ""mundane"",       ""weight"": 50, ""color"": ""#aaaaaa"", ""affixCount"": 1, ""valueScale"": 1.0 },
        { ""code"": ""drift-touched"", ""weight"":  5, ""color"": ""#a855f7"", ""affixCount"": 2, ""valueScale"": 1.6, ""signature"": true }
      ],
      ""signatures"": {
        ""ring"": { ""code"": ""drift_mark"", ""langKey"": ""driftrelics:signature-drift_mark"",
                    ""mods"": [{ ""key"": ""meleeDamage"", ""value"": 0.10, ""op"": ""Mul"" }] }
      },
      ""prefixes"": [
        { ""code"": ""burning"",  ""langKey"": ""x"", ""weight"": 10, ""mods"": [] },
        { ""code"": ""drift_marked"", ""langKey"": ""x"", ""weight"": 5, ""minTier"": ""drift-touched"", ""mods"": [] }
      ],
      ""suffixes"": []
    }";

    [Fact]
    public void Loads_Tiers_With_Weights_And_Flags()
    {
        var cfg = AffixConfigLoader.LoadFromJson(Json);
        Assert.Equal(2, cfg.Tiers.Count);
        var drift = cfg.Tiers.Single(t => t.Code == "drift-touched");
        Assert.Equal(5, drift.Weight);
        Assert.Equal(2, drift.AffixCount);
        Assert.Equal(1.6, drift.ValueScale);
        Assert.True(drift.Signature);
    }

    [Fact]
    public void Loads_Signature_For_Slot_Type()
    {
        var cfg = AffixConfigLoader.LoadFromJson(Json);
        Assert.True(cfg.Signatures.ContainsKey("ring"));
        var sig = cfg.Signatures["ring"];
        Assert.Equal("drift_mark", sig.Code);
        Assert.Single(sig.Mods);
        Assert.Equal("meleeDamage", sig.Mods[0].Key);
    }

    [Fact]
    public void Affixes_Inherit_MinTier_From_Json_Default_Mundane()
    {
        var cfg = AffixConfigLoader.LoadFromJson(Json);
        var burning      = cfg.Prefixes.Single(a => a.Code == "burning");
        var driftMarked  = cfg.Prefixes.Single(a => a.Code == "drift_marked");

        Assert.Equal("mundane", burning.MinTier);
        Assert.Equal("drift-touched", driftMarked.MinTier);
    }

    [Fact]
    public void Missing_Tiers_Section_Yields_Empty_List()
    {
        var cfg = AffixConfigLoader.LoadFromJson(@"{ ""prefixes"": [], ""suffixes"": [] }");
        Assert.NotNull(cfg.Tiers);
        Assert.Empty(cfg.Tiers);
        Assert.NotNull(cfg.Signatures);
        Assert.Empty(cfg.Signatures);
    }
}
```

- [ ] **Step 2: Run, expect compile error**

```
dotnet test tests/DriftRelics.Tests/DriftRelics.Tests.csproj --nologo --filter "FullyQualifiedName~TierConfigLoaderTests"
```

May fail to build because the test project will pull in main `Affix*` types. If `AffixConfigLoader.LoadFromJson` references the old `RollChances`, those references need to be removed in this task too.

- [ ] **Step 3: Update `AffixConfigLoader.LoadFromJson`**

Replace `src/Affix/AffixConfigLoader.cs` with:

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DriftRelics.Affixes;

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
        cfg.Tiers      ??= new List<TierConfig>();
        cfg.Signatures ??= new Dictionary<string, SignatureAffix>();
        cfg.Prefixes   ??= new List<Affix>();
        cfg.Suffixes   ??= new List<Affix>();

        foreach (var a in cfg.Prefixes) a.Kind = AffixKind.Prefix;
        foreach (var a in cfg.Suffixes) a.Kind = AffixKind.Suffix;

        return cfg;
    }
}
```

- [ ] **Step 4: Add `tests/DriftRelics.Tests/TierConfigLoaderTests.cs` to the test compile list**

If the test project uses `<Compile Include>` patterns, ensure the new file is picked up by the glob. If the project file is missing wildcard `**/*.cs`, add:

```xml
<ItemGroup>
  <Compile Include="TierConfigLoaderTests.cs" />
</ItemGroup>
```

(Skip this step if the test project already globs `*.cs`.)

- [ ] **Step 5: Run, expect pass**

```
dotnet test tests/DriftRelics.Tests/DriftRelics.Tests.csproj --nologo --filter "FullyQualifiedName~TierConfigLoaderTests"
```

Expected: 4 tests pass. Main project still won't compile (downstream references to `AffixRollChances`). That gets fixed in Task 5 — keep going.

- [ ] **Step 6: Commit**

```bash
git add src/Affix/AffixConfigLoader.cs tests/DriftRelics.Tests/TierConfigLoaderTests.cs
git commit -m "feat(affix): loader reads tiers, signatures, and per-affix minTier"
```

---

## Task 4: RelicsUtil.GetTier / SetTier + WriteInstance / GetInstance round-trip

**Files:**
- Modify: `src/Api/RelicsUtil.cs`
- Test: Append to `tests/DriftRelics.Tests/BaubleInstanceTests.cs` (the file kept its pre-rebrand name; that's fine)

- [ ] **Step 1: Add the failing tests**

Append to `tests/DriftRelics.Tests/BaubleInstanceTests.cs`:

```csharp
[Fact]
public void GetTier_Defaults_To_Mundane_For_Legacy_Stack()
{
    var stack = MakeStack();
    Assert.Equal("mundane", RelicsUtil.GetTier(stack));
}

[Fact]
public void SetTier_RoundTrip()
{
    var stack = MakeStack();
    RelicsUtil.SetTier(stack, "drift-touched");
    Assert.Equal("drift-touched", RelicsUtil.GetTier(stack));
}

[Fact]
public void WriteInstance_Persists_Tier()
{
    var stack = MakeStack();
    var inst = new RelicInstance(RelicSlotType.Ring, "burning", null, 42L, true, "notable");
    RelicsUtil.WriteInstance(stack, inst);

    var roundTrip = RelicsUtil.GetInstance(stack)!;
    Assert.Equal("notable", roundTrip.Tier);
}
```

`MakeStack()` should already exist in the test file as a helper that builds a ring ItemStack with the slot-type attribute set. If not, mirror the pattern in the surrounding tests.

- [ ] **Step 2: Run, expect compile failure**

```
dotnet test tests/DriftRelics.Tests/DriftRelics.Tests.csproj --nologo --filter "FullyQualifiedName~BaubleInstanceTests"
```

Missing `GetTier` / `SetTier`.

- [ ] **Step 3: Extend `RelicsUtil`**

In `src/Api/RelicsUtil.cs`, add a new attribute key and helpers:

```csharp
private const string AttrTier = "relic.tier";

public static string GetTier(ItemStack? stack)
    => stack?.Attributes?.GetString(AttrTier, "mundane") ?? "mundane";

public static void SetTier(ItemStack stack, string tier)
    => stack.Attributes.SetString(AttrTier, tier);
```

Update `GetInstance`:

```csharp
public static RelicInstance? GetInstance(ItemStack? stack)
{
    var slot = GetSlotType(stack);
    if (slot == null) return null;
    return new RelicInstance(
        slot.Value,
        GetPrefixCode(stack),
        GetSuffixCode(stack),
        GetSeed(stack),
        IsIdentified(stack),
        GetTier(stack));
}
```

Update `WriteInstance`:

```csharp
public static void WriteInstance(ItemStack stack, RelicInstance instance)
{
    stack.Attributes.SetLong(AttrSeed, instance.Seed);
    stack.Attributes.SetString(AttrPrefix, instance.PrefixCode ?? "");
    stack.Attributes.SetString(AttrSuffix, instance.SuffixCode ?? "");
    stack.Attributes.SetBool(AttrIdentified, instance.Identified);
    stack.Attributes.SetString(AttrTier, instance.Tier);
}
```

- [ ] **Step 4: Run tests, expect pass**

```
dotnet test tests/DriftRelics.Tests/DriftRelics.Tests.csproj --nologo --filter "FullyQualifiedName~BaubleInstanceTests"
```

- [ ] **Step 5: Commit**

```bash
git add src/Api/RelicsUtil.cs tests/DriftRelics.Tests/BaubleInstanceTests.cs
git commit -m "feat(api): RelicsUtil reads/writes relic.tier; default mundane for legacy"
```

---

## Task 5: AffixRegistry stores Tiers + Signatures; BuildPool extension

**Files:**
- Modify: `src/Affix/AffixRegistry.cs`
- Modify: `src/Affix/AffixPool.cs`
- Modify: `src/Api/IAffixRegistry.cs` (only to expose the read-only registries if the interface defines them)
- Test: Add to `tests/DriftRelics.Tests/AffixPocoTests.cs`

- [ ] **Step 1: Add the failing tests**

Append to `AffixPocoTests.cs`:

```csharp
[Fact]
public void AffixRegistry_Stores_Tiers_And_Signatures()
{
    var reg = new AffixRegistry();
    reg.SetTiers(new List<TierConfig>
    {
        new() { Code = "mundane", Weight = 50 },
        new() { Code = "drift-touched", Weight = 5, Signature = true }
    });
    reg.SetSignature("ring", new SignatureAffix { Code = "drift_mark" });

    var pool = reg.BuildPool();
    Assert.Equal(2, pool.Tiers.Count);
    Assert.NotNull(pool.GetSignatureFor("ring"));
    Assert.Null(pool.GetSignatureFor("trinket"));
}
```

You'll need `using System.Collections.Generic;` at the top of the test file.

- [ ] **Step 2: Replace `src/Affix/AffixPool.cs`**

```csharp
using System.Collections.Generic;

namespace DriftRelics.Affixes;

public sealed class AffixPool
{
    public IReadOnlyList<TierConfig> Tiers { get; }
    public IReadOnlyList<Affix> Prefixes { get; }
    public IReadOnlyList<Affix> Suffixes { get; }
    private readonly IReadOnlyDictionary<string, SignatureAffix> signatures;

    public AffixPool(IReadOnlyList<TierConfig> tiers,
                     IReadOnlyList<Affix> prefixes,
                     IReadOnlyList<Affix> suffixes,
                     IReadOnlyDictionary<string, SignatureAffix> signatures)
    {
        Tiers = tiers;
        Prefixes = prefixes;
        Suffixes = suffixes;
        this.signatures = signatures;
    }

    public SignatureAffix? GetSignatureFor(string slotTypeKey)
        => signatures.TryGetValue(slotTypeKey, out var s) ? s : null;

    public TierConfig? GetTier(string code)
    {
        for (int i = 0; i < Tiers.Count; i++) if (Tiers[i].Code == code) return Tiers[i];
        return null;
    }
}
```

- [ ] **Step 3: Replace `src/Affix/AffixRegistry.cs`**

```csharp
using System.Collections.Generic;
using DriftRelics.Api;

namespace DriftRelics.Affixes;

public sealed class AffixRegistry : IAffixRegistry
{
    private readonly Dictionary<string, Affix> byCode = new();
    private readonly List<Affix> prefixes = new();
    private readonly List<Affix> suffixes = new();
    private List<TierConfig> tiers = new();
    private readonly Dictionary<string, SignatureAffix> signatures = new();

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

    public void SetTiers(IEnumerable<TierConfig> source)
    {
        tiers = new List<TierConfig>(source);
    }

    public void SetSignature(string slotTypeKey, SignatureAffix sig)
    {
        signatures[slotTypeKey] = sig;
    }

    public IReadOnlyList<TierConfig> Tiers => tiers;
    public SignatureAffix? GetSignatureFor(string slotTypeKey)
        => signatures.TryGetValue(slotTypeKey, out var s) ? s : null;

    public AffixPool BuildPool() => new(tiers, prefixes, suffixes, signatures);
}
```

- [ ] **Step 4: Extend `src/Api/IAffixRegistry.cs`**

Add (preserving existing signatures):

```csharp
IReadOnlyList<TierConfig> Tiers { get; }
SignatureAffix? GetSignatureFor(string slotTypeKey);
void SetTiers(IEnumerable<TierConfig> source);
void SetSignature(string slotTypeKey, SignatureAffix sig);
```

If `IAffixRegistry` lives outside `DriftRelics.Affixes`, add the `using DriftRelics.Affixes;` at the top.

- [ ] **Step 5: Run tests**

```
dotnet test tests/DriftRelics.Tests/DriftRelics.Tests.csproj --nologo --filter "FullyQualifiedName~AffixPocoTests"
```

Should pass. Main project still won't compile because `RelicsAPI.RollUnidentifiedRelic` calls `Affixes.BuildPool()` which now has a different shape — fixed in Task 7.

- [ ] **Step 6: Commit**

```bash
git add src/Affix/AffixRegistry.cs src/Affix/AffixPool.cs src/Api/IAffixRegistry.cs tests/DriftRelics.Tests/AffixPocoTests.cs
git commit -m "feat(affix): registry stores tiers + signatures; pool exposes lookups"
```

---

## Task 6: Weighted tier roller (pure-logic)

**Files:**
- Create: `src/Affix/TierRoller.cs`
- Create: `tests/DriftRelics.Tests/TierRollerTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/DriftRelics.Tests/TierRollerTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using DriftRelics.Affixes;
using Xunit;

namespace DriftRelics.Tests;

public class TierRollerTests
{
    private static List<TierConfig> SampleTiers() => new()
    {
        new() { Code = "mundane",       Weight = 50 },
        new() { Code = "curious",       Weight = 30 },
        new() { Code = "notable",       Weight = 15 },
        new() { Code = "drift-touched", Weight =  5 },
    };

    [Fact]
    public void Roll_Distribution_Roughly_Matches_Weights()
    {
        var tiers = SampleTiers();
        var counts = new Dictionary<string, int>
        {
            ["mundane"] = 0, ["curious"] = 0, ["notable"] = 0, ["drift-touched"] = 0
        };

        var rng = new Random(12345);
        const int trials = 20000;
        for (int i = 0; i < trials; i++)
        {
            var tier = TierRoller.Roll(tiers, rng);
            counts[tier.Code]++;
        }

        // Expected: mundane=50%, curious=30%, notable=15%, drift-touched=5%
        // Allow 3 percentage points slack.
        Assert.InRange(counts["mundane"]       / (double)trials, 0.47, 0.53);
        Assert.InRange(counts["curious"]       / (double)trials, 0.27, 0.33);
        Assert.InRange(counts["notable"]       / (double)trials, 0.12, 0.18);
        Assert.InRange(counts["drift-touched"] / (double)trials, 0.03, 0.07);
    }

    [Fact]
    public void Roll_Returns_Single_Tier_When_Pool_Has_One()
    {
        var tiers = new List<TierConfig> { new() { Code = "mundane", Weight = 1 } };
        var tier = TierRoller.Roll(tiers, new Random(0));
        Assert.Equal("mundane", tier.Code);
    }

    [Fact]
    public void Roll_Empty_Pool_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => TierRoller.Roll(new List<TierConfig>(), new Random(0)));
    }

    [Fact]
    public void Roll_Zero_Weights_Throws()
    {
        var tiers = new List<TierConfig> { new() { Code = "x", Weight = 0 } };
        Assert.Throws<InvalidOperationException>(() => TierRoller.Roll(tiers, new Random(0)));
    }
}
```

- [ ] **Step 2: Run, expect compile failure**

`TierRoller` doesn't exist.

- [ ] **Step 3: Create `src/Affix/TierRoller.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace DriftRelics.Affixes;

public static class TierRoller
{
    public static TierConfig Roll(IReadOnlyList<TierConfig> tiers, Random rng)
    {
        if (tiers == null || tiers.Count == 0)
            throw new InvalidOperationException("TierRoller.Roll: tier list is empty");

        int total = 0;
        for (int i = 0; i < tiers.Count; i++) total += tiers[i].Weight;
        if (total <= 0)
            throw new InvalidOperationException("TierRoller.Roll: total tier weight is zero");

        int roll = rng.Next(total);
        int acc = 0;
        for (int i = 0; i < tiers.Count; i++)
        {
            acc += tiers[i].Weight;
            if (roll < acc) return tiers[i];
        }
        return tiers[tiers.Count - 1];
    }
}
```

- [ ] **Step 4: Run tests, expect pass**

```
dotnet test tests/DriftRelics.Tests/DriftRelics.Tests.csproj --nologo --filter "FullyQualifiedName~TierRollerTests"
```

- [ ] **Step 5: Commit**

```bash
git add src/Affix/TierRoller.cs tests/DriftRelics.Tests/TierRollerTests.cs
git commit -m "feat(affix): TierRoller pure-logic weighted selection"
```

---

## Task 7: RelicRoller — tier roll + filter + affix count + signature attachment

**Files:**
- Modify: `src/Affix/RelicRoller.cs`
- Modify: `src/Api/RelicsAPI.cs` (RollUnidentifiedRelic — now writes tier)
- Create: `tests/DriftRelics.Tests/RelicRollerTierTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/DriftRelics.Tests/RelicRollerTierTests.cs`:

```csharp
using System.Collections.Generic;
using DriftRelics.Affixes;
using DriftRelics.Api;
using DriftRelics.Modifier;
using Xunit;

namespace DriftRelics.Tests;

public class RelicRollerTierTests
{
    private static AffixPool BuildPool(params TierConfig[] tiers)
    {
        var prefixes = new List<Affix>
        {
            new() { Code = "burning",      Kind = AffixKind.Prefix, Weight = 10, MinTier = "mundane" },
            new() { Code = "drift_marked", Kind = AffixKind.Prefix, Weight = 10, MinTier = "drift-touched" },
        };
        var suffixes = new List<Affix>
        {
            new() { Code = "of_swiftness", Kind = AffixKind.Suffix, Weight = 10, MinTier = "mundane" },
        };
        var signatures = new Dictionary<string, SignatureAffix>
        {
            ["ring"] = new() { Code = "drift_mark",
                               Mods = { new ModifierEntry { Key = "meleeDamage", Value = 0.10, Op = ModifierOp.Mul } } }
        };
        return new AffixPool(tiers, prefixes, suffixes, signatures);
    }

    [Fact]
    public void Mundane_Roll_Has_Exactly_One_Affix()
    {
        var pool = BuildPool(new TierConfig { Code = "mundane", Weight = 100, AffixCount = 1, ValueScale = 1.0 });
        var rng = new System.Random(123);
        int withBoth = 0, withOne = 0, withNeither = 0;
        for (int i = 0; i < 200; i++)
        {
            var inst = RelicRoller.Roll(RelicSlotType.Ring, i, pool, rng);
            int affixCount = (inst.PrefixCode != null ? 1 : 0) + (inst.SuffixCode != null ? 1 : 0);
            if (affixCount == 2) withBoth++;
            else if (affixCount == 1) withOne++;
            else withNeither++;
            Assert.Equal("mundane", inst.Tier);
        }
        Assert.Equal(0, withBoth);
        Assert.Equal(0, withNeither);
        Assert.Equal(200, withOne);
    }

    [Fact]
    public void Curious_Roll_Has_Prefix_And_Suffix()
    {
        var pool = BuildPool(new TierConfig { Code = "curious", Weight = 100, AffixCount = 2, ValueScale = 1.0 });
        var inst = RelicRoller.Roll(RelicSlotType.Ring, 42, pool, new System.Random(0));
        Assert.NotNull(inst.PrefixCode);
        Assert.NotNull(inst.SuffixCode);
        Assert.Equal("curious", inst.Tier);
    }

    [Fact]
    public void Drift_Touched_Filters_To_Drift_Marked_Available()
    {
        var pool = BuildPool(new TierConfig
        {
            Code = "drift-touched", Weight = 100, AffixCount = 2, ValueScale = 1.6, Signature = true
        });
        var rng = new System.Random(7);
        int driftMarkedSeen = 0;
        for (int i = 0; i < 50; i++)
        {
            var inst = RelicRoller.Roll(RelicSlotType.Ring, i, pool, rng);
            if (inst.PrefixCode == "drift_marked") driftMarkedSeen++;
        }
        // 2 prefixes available at drift-touched (burning + drift_marked), equal weight,
        // so ~50% of rolls pick drift_marked.
        Assert.InRange(driftMarkedSeen, 15, 35);
    }

    [Fact]
    public void Filter_Excludes_Drift_Marked_At_Mundane()
    {
        var pool = BuildPool(new TierConfig { Code = "mundane", Weight = 100, AffixCount = 1, ValueScale = 1.0 });
        var rng = new System.Random(9);
        for (int i = 0; i < 200; i++)
        {
            var inst = RelicRoller.Roll(RelicSlotType.Ring, i, pool, rng);
            Assert.NotEqual("drift_marked", inst.PrefixCode);
        }
    }
}
```

- [ ] **Step 2: Run, expect compile failure**

`RelicRoller.Roll` doesn't accept a `Random` arg yet, and `AffixPool` constructor signature differs from earlier task. Expected.

- [ ] **Step 3: Replace `src/Affix/RelicRoller.cs`**

```csharp
using System;
using System.Collections.Generic;
using DriftRelics.Api;

namespace DriftRelics.Affixes;

public static class RelicRoller
{
    public static RelicInstance Roll(RelicSlotType slotType, long seed, AffixPool pool, Random? rngOverride = null)
    {
        var rng = rngOverride ?? new Random(SeedToInt(seed));

        var tier = TierRoller.Roll(pool.Tiers, rng);

        string? prefix = null;
        string? suffix = null;

        if (tier.AffixCount >= 2)
        {
            prefix = WeightedPick(FilterByTier(pool.Prefixes, tier.Code), slotType, rng)?.Code;
            suffix = WeightedPick(FilterByTier(pool.Suffixes, tier.Code), slotType, rng)?.Code;
        }
        else if (tier.AffixCount == 1)
        {
            // 50/50: prefix-or-suffix
            if (rng.Next(2) == 0)
            {
                prefix = WeightedPick(FilterByTier(pool.Prefixes, tier.Code), slotType, rng)?.Code;
            }
            else
            {
                suffix = WeightedPick(FilterByTier(pool.Suffixes, tier.Code), slotType, rng)?.Code;
            }
        }

        return new RelicInstance(slotType, prefix, suffix, seed, Identified: false, Tier: tier.Code);
    }

    private static IReadOnlyList<Affix> FilterByTier(IReadOnlyList<Affix> source, string tierCode)
    {
        var order = TierOrder(tierCode);
        var filtered = new List<Affix>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            if (TierOrder(source[i].MinTier) <= order) filtered.Add(source[i]);
        }
        return filtered;
    }

    private static int TierOrder(string code) => code switch
    {
        "mundane"       => 0,
        "curious"       => 1,
        "notable"       => 2,
        "drift-touched" => 3,
        _               => 0
    };

    private static Affix? WeightedPick(IReadOnlyList<Affix> source,
                                       RelicSlotType slot, Random rng)
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

- [ ] **Step 4: Update `RelicsAPI.RollUnidentifiedRelic`**

In `src/Api/RelicsAPI.cs`, replace the body of `RollUnidentifiedRelic` with:

```csharp
public ItemStack? RollUnidentifiedRelic(RelicSlotType slotType, long seed)
{
    var code = new AssetLocation("driftrelics", slotType.ToString().ToLowerInvariant());
    var item = api.World.GetItem(code);
    if (item == null) return null;

    var stack = new ItemStack(item);
    var pool = Affixes.BuildPool();
    var instance = RelicRoller.Roll(slotType, seed, pool);
    RelicsUtil.WriteInstance(stack, instance);
    return stack;
}
```

The body is unchanged, but the call resolves to the new `RelicRoller.Roll` signature.

- [ ] **Step 5: Run, expect pass**

```
dotnet test tests/DriftRelics.Tests/DriftRelics.Tests.csproj --nologo --filter "FullyQualifiedName~RelicRollerTierTests"
dotnet build DriftRelics.csproj --nologo
```

Main project should now compile.

- [ ] **Step 6: Commit**

```bash
git add src/Affix/RelicRoller.cs src/Api/RelicsAPI.cs tests/DriftRelics.Tests/RelicRollerTierTests.cs
git commit -m "feat(roller): tier roll + minTier filter + per-tier affix count"
```

---

## Task 8: Signatures yielded into EnumerateMods for drift-touched

**Files:**
- Modify: `src/Entity/EntityBehaviorRelics.cs` (`EnumerateMods`)
- Test: covered indirectly by Task 10's scaling tests; no new unit test here

- [ ] **Step 1: Read the existing `EnumerateMods`**

`src/Entity/EntityBehaviorRelics.cs` line ~117. Current body yields prefix + suffix mods only.

- [ ] **Step 2: Add signature yield**

Replace `EnumerateMods` with:

```csharp
private IEnumerable<ModifierEntry> EnumerateMods(ItemStack stack)
{
    var prefix = RelicsUtil.GetPrefixCode(stack);
    var suffix = RelicsUtil.GetSuffixCode(stack);
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

    var tier = RelicsUtil.GetTier(stack);
    if (modSystem?.Affixes is not Affixes.AffixRegistry reg) yield break;
    var tierCfg = reg.BuildPool().GetTier(tier);
    if (tierCfg == null || !tierCfg.Signature) yield break;

    var slotKey = (RelicsUtil.GetSlotType(stack) ?? RelicSlotType.Trinket)
                  .ToString().ToLowerInvariant();
    var sig = reg.GetSignatureFor(slotKey);
    if (sig == null) yield break;
    foreach (var m in sig.Mods) yield return m;
}
```

- [ ] **Step 3: Build to verify compile**

```
dotnet build DriftRelics.csproj --nologo
```

- [ ] **Step 4: Commit**

```bash
git add src/Entity/EntityBehaviorRelics.cs
git commit -m "feat(modifiers): signature implicits yielded on drift-touched relics"
```

---

## Task 9: Modifier value scaling at apply time

**Files:**
- Modify: `src/Modifier/ModifierRegistry.cs` (add a scaled-value overload)
- Modify: `src/Entity/EntityBehaviorRelics.cs` (`ApplyMods`/`RemoveMods` pass tier scale)
- Create: `tests/DriftRelics.Tests/ModifierScalingTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/DriftRelics.Tests/ModifierScalingTests.cs`:

```csharp
using DriftRelics.Modifier;
using Xunit;

namespace DriftRelics.Tests;

public class ModifierScalingTests
{
    [Theory]
    [InlineData(0.10, 1.6, 0.16)]
    [InlineData(0.05, 1.3, 0.065)]
    [InlineData(0.05, 1.0, 0.05)]
    public void Scale_Mul_Value_Is_Product(double v, double scale, double expected)
    {
        var entry = new ModifierEntry { Key = "k", Value = v, Op = ModifierOp.Mul };
        var scaled = ModifierRegistry.ScaleEntry(entry, scale);
        Assert.Equal(expected, scaled.Value, 6);
    }

    [Theory]
    [InlineData(2, 1.6, 3)]   // round-half-up: 2 * 1.6 = 3.2 → 3
    [InlineData(5, 1.6, 8)]   // 5 * 1.6 = 8.0
    [InlineData(4, 1.3, 5)]   // 4 * 1.3 = 5.2 → 5
    public void Scale_Add_Value_Rounds_Half_Up_For_Integer_Mods(int v, double scale, int expected)
    {
        var entry = new ModifierEntry { Key = "maxHealth", Value = v, Op = ModifierOp.Add };
        var scaled = ModifierRegistry.ScaleEntry(entry, scale);
        Assert.Equal(expected, (int)scaled.Value);
    }
}
```

- [ ] **Step 2: Add a static `ScaleEntry` helper on `ModifierRegistry`**

In `src/Modifier/ModifierRegistry.cs`, add:

```csharp
public static ModifierEntry ScaleEntry(ModifierEntry source, double scale)
{
    if (scale == 1.0) return source;
    double newValue = source.Op == ModifierOp.Add
        ? System.Math.Round(source.Value * scale, System.MidpointRounding.AwayFromZero)
        : source.Value * scale;
    return new ModifierEntry { Key = source.Key, Value = newValue, Op = source.Op };
}
```

(`Math.Round(_, MidpointRounding.AwayFromZero)` is the round-half-up rule.)

- [ ] **Step 3: Hook scaling into apply/remove**

In `src/Entity/EntityBehaviorRelics.cs`, replace `ApplyMods` and `RemoveMods` with:

```csharp
private void ApplyMods(ItemStack stack)
{
    if (entity is not Vintagestory.API.Common.EntityPlayer ep) return;
    var scale = LookupScale(stack);
    foreach (var entry in EnumerateMods(stack))
    {
        var scaled = ModifierRegistry.ScaleEntry(entry, scale);
        var code = ModifierCode(stack, scaled.Key);
        modSystem?.Modifiers.TryApply(ep, scaled, code);
    }
}

private void RemoveMods(ItemStack stack)
{
    if (entity is not Vintagestory.API.Common.EntityPlayer ep) return;
    var scale = LookupScale(stack);
    foreach (var entry in EnumerateMods(stack))
    {
        var scaled = ModifierRegistry.ScaleEntry(entry, scale);
        var code = ModifierCode(stack, scaled.Key);
        modSystem?.Modifiers.TryRemove(ep, scaled, code);
    }
}

private double LookupScale(ItemStack stack)
{
    var tier = RelicsUtil.GetTier(stack);
    if (modSystem?.Affixes is not Affixes.AffixRegistry reg) return 1.0;
    return reg.BuildPool().GetTier(tier)?.ValueScale ?? 1.0;
}
```

- [ ] **Step 4: Run tests, expect pass**

```
dotnet test tests/DriftRelics.Tests/DriftRelics.Tests.csproj --nologo --filter "FullyQualifiedName~ModifierScalingTests"
dotnet build DriftRelics.csproj --nologo
```

- [ ] **Step 5: Commit**

```bash
git add src/Modifier/ModifierRegistry.cs src/Entity/EntityBehaviorRelics.cs tests/DriftRelics.Tests/ModifierScalingTests.cs
git commit -m "feat(modifiers): scale mod values by tier ValueScale at apply time"
```

---

## Task 10: Public API additions — IRelicsAPI.GetTier and Tiers

**Files:**
- Modify: `src/Api/IRelicsAPI.cs`
- Modify: `src/Api/RelicsAPI.cs`

- [ ] **Step 1: Extend the interface**

In `src/Api/IRelicsAPI.cs`, add to the interface (after `IsIdentified`):

```csharp
string GetTier(ItemStack? stack);
```

and after the existing `Affixes` / `Modifiers` properties:

```csharp
System.Collections.Generic.IReadOnlyList<DriftRelics.Affixes.TierConfig> Tiers { get; }
```

- [ ] **Step 2: Implement on `RelicsAPI`**

In `src/Api/RelicsAPI.cs`:

```csharp
public string GetTier(ItemStack? stack) => RelicsUtil.GetTier(stack);

public System.Collections.Generic.IReadOnlyList<DriftRelics.Affixes.TierConfig> Tiers => Affixes.Tiers;
```

- [ ] **Step 3: Build**

```
dotnet build DriftRelics.csproj --nologo
```

- [ ] **Step 4: Commit**

```bash
git add src/Api/IRelicsAPI.cs src/Api/RelicsAPI.cs
git commit -m "feat(api): expose GetTier and Tiers on IRelicsAPI"
```

---

## Task 11: Colored identified name in display

**Files:**
- Modify: `src/Api/RelicsDisplay.cs`

The cleanest hook is `GetHeldItemName` (called for the in-world floating label and the inventory tooltip header). VS supports inline color via the `<font color="#RRGGBB">` tag in richtext contexts but **not** in `GetHeldItemName` (plain string). For the held-item NAME, fall back to plain text. The colored display goes in `GetHeldItemInfo`.

- [ ] **Step 1: Read the current display logic**

`src/Api/RelicsDisplay.cs` — `GetDisplayName` already assembles the name. Keep it returning plain string for use as the title. Add a new richtext-friendly variant.

- [ ] **Step 2: Extend `RelicsDisplay`**

Replace `src/Api/RelicsDisplay.cs` with:

```csharp
using System.Text;
using DriftRelics.Affixes;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace DriftRelics.Api;

public static class RelicsDisplay
{
    public static string GetDisplayName(ItemStack stack, string fallback)
    {
        if (!RelicsUtil.IsRelic(stack)) return fallback;
        if (!RelicsUtil.IsIdentified(stack))
            return ScrambleNameGenerator.Generate(RelicsUtil.GetSeed(stack));

        return AssembleIdentifiedName(stack);
    }

    /// Returns the identified name wrapped in a richtext color tag matching the relic's tier.
    /// Plain (non-tagged) for tier `mundane`. Caller is responsible for rendering richtext.
    public static string GetDisplayNameColored(ItemStack stack, string fallback,
                                               System.Collections.Generic.IReadOnlyList<TierConfig> tiers)
    {
        var plain = GetDisplayName(stack, fallback);
        if (!RelicsUtil.IsRelic(stack) || !RelicsUtil.IsIdentified(stack)) return plain;

        var tier = RelicsUtil.GetTier(stack);
        if (tier == "mundane") return plain;

        string color = "#ffffff";
        for (int i = 0; i < tiers.Count; i++)
        {
            if (tiers[i].Code == tier) { color = tiers[i].Color; break; }
        }
        return $"<font color=\"{color}\">{plain}</font>";
    }

    private static string AssembleIdentifiedName(ItemStack stack)
    {
        var baseName = Lang.Get("driftrelics:item-" + stack.Collectible.LastCodePart());
        var prefix = RelicsUtil.GetPrefixCode(stack);
        var suffix = RelicsUtil.GetSuffixCode(stack);

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(prefix))
        {
            sb.Append(Lang.Get("driftrelics:affix-prefix-" + prefix));
            sb.Append(' ');
        }
        sb.Append(baseName);
        if (!string.IsNullOrEmpty(suffix))
        {
            sb.Append(' ');
            sb.Append(Lang.Get("driftrelics:affix-suffix-" + suffix));
        }
        return sb.ToString();
    }
}
```

- [ ] **Step 3: Use the colored variant in `ItemRelic.GetHeldItemInfo`**

In `src/Items/ItemRelic.cs`, replace `GetHeldItemInfo` with:

```csharp
public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc,
                                     IWorldAccessor world, bool withDebugInfo)
{
    var stack = inSlot.Itemstack;
    if (RelicsUtil.IsRelic(stack))
    {
        if (!RelicsUtil.IsIdentified(stack))
        {
            dsc.AppendLine(Lang.Get("driftrelics:unidentified-hint"));
        }
        else
        {
            var modSystem = api.ModLoader.GetModSystem<DriftRelicsModSystem>();
            var colored = RelicsDisplay.GetDisplayNameColored(stack, base.GetHeldItemName(stack),
                                                              modSystem.Api.Tiers);
            // Append tier color line above the standard info so the colored title is visible.
            dsc.AppendLine(colored);
            var tierName = Lang.Get("driftrelics:tier-" + RelicsUtil.GetTier(stack));
            dsc.AppendLine(Lang.Get("driftrelics:tier-line", tierName));
        }
    }
    base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
}
```

The `api` field on `Item` is non-public on some VS branches; use `Item.api` if it exists, else fetch via `world.Api.ModLoader…` (the engineer should verify the available property at this point).

- [ ] **Step 4: Build**

```
dotnet build DriftRelics.csproj --nologo
```

- [ ] **Step 5: Commit**

```bash
git add src/Api/RelicsDisplay.cs src/Items/ItemRelic.cs
git commit -m "feat(display): colored identified name + tier line in tooltip"
```

---

## Task 12: Pre-identify sigil overlay (subtle visual hint)

**Files:**
- Modify: `src/Items/ItemRelic.cs` — override `OnRender` or use a custom attribute renderer

**This task involves a VS API surface that the engineer must inspect first.** Two candidate hooks:

1. `Item.OnRender(ItemRenderInfo, ICoreClientAPI, ItemStack, double posX, double posY, double size)` — not always available depending on render path.
2. `ItemStack.Attributes` driven texture overlay via `tesselator.AddOverlay` — most likely correct but requires deeper VS knowledge.

**Spec fallback if neither path works in one session:** drop a one-line aura description in `GetHeldItemInfo` (e.g., "Aura: faint curious shimmer") and ship without the icon overlay. This task can be deferred to a later patch release.

- [ ] **Step 1: Investigate VS API**

Read `~/workspace/vs-api-reference/VintagestoryAPI/VintagestoryAPI.decompiled.cs` for:
- `Item.OnRender` signature and call sites
- `ItemRenderInfo` and texture overlay paths
- Any examples in `VSSurvivalMod` of items that draw per-stack overlays

If a workable hook exists, proceed to step 2. Otherwise, jump to step 5 (fallback).

- [ ] **Step 2: Implement the overlay (if hook found)**

Add to `ItemRelic`:

```csharp
public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
{
    base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
    if (!RelicsUtil.IsRelic(itemstack)) return;
    if (RelicsUtil.IsIdentified(itemstack)) return;

    var tier = RelicsUtil.GetTier(itemstack);
    if (tier == "mundane") return;

    // Tint the inventory icon by tier color. Color is sourced from Tiers config.
    var modSystem = capi.ModLoader.GetModSystem<DriftRelicsModSystem>();
    var tiers = modSystem.Api.Tiers;
    for (int i = 0; i < tiers.Count; i++)
    {
        if (tiers[i].Code == tier)
        {
            renderinfo.OverlayOpacity = 0.35f;
            renderinfo.OverlayTexture = null; // tint-only; no texture asset required
            // Tint via ModelMat or RgbaTint property depending on what's exposed.
            // ENGINEER: verify the actual field — likely renderinfo.RgbaTint or similar.
            break;
        }
    }
}
```

ENGINEER NOTE: the exact tint field is the unknown. If `OverlayOpacity` + `OverlayTexture` doesn't drive a color tint, look for a `RgbaTint` / `ItemTint` / `ColorTint` field on `ItemRenderInfo`. Try-and-test in a creative world.

- [ ] **Step 3: Manual test**

Load mod. Spawn unidentified relics of varying tiers via the debug roller. Look at their inventory icons — should show a faint tier-colored tint.

- [ ] **Step 4: Iterate on visual until subtle but visible**

The spec says "subtle." Aim for tint that's noticeable on a focused look but doesn't shout. Adjust opacity 0.2–0.5.

- [ ] **Step 5 (fallback if no API hook works): Tooltip-only aura line**

In `src/Items/ItemRelic.cs` `GetHeldItemInfo`, just before the `unidentified-hint` line, add:

```csharp
var tier = RelicsUtil.GetTier(stack);
if (tier != "mundane")
{
    var tierName = Lang.Get("driftrelics:tier-" + tier);
    dsc.AppendLine(Lang.Get("driftrelics:aura-line", tierName));
}
```

Add `aura-line` and `tier-*` lang keys in Task 14.

- [ ] **Step 6: Commit**

```bash
git add src/Items/ItemRelic.cs
git commit -m "feat(items): pre-identify tier hint (icon overlay or tooltip fallback)"
```

---

## Task 13: Mod system glue — register tiers and signatures at AssetsFinalize

**Files:**
- Modify: `src/DriftRelicsModSystem.cs`

- [ ] **Step 1: Update `AssetsFinalize`**

Replace `AssetsFinalize` body in `src/DriftRelicsModSystem.cs`:

```csharp
public override void AssetsFinalize(ICoreAPI api)
{
    base.AssetsFinalize(api);
    var asset = api.Assets.TryGet(new AssetLocation("driftrelics", "config/affixes.json"));
    if (asset == null)
    {
        api.Logger.Warning("[DriftRelics] affixes.json not found — no affixes will roll");
        return;
    }
    var json = asset.ToText();
    var cfg = DriftRelics.Affixes.AffixConfigLoader.LoadFromJson(json);

    Affixes.SetTiers(cfg.Tiers);
    foreach (var kv in cfg.Signatures) Affixes.SetSignature(kv.Key, kv.Value);
    foreach (var a in cfg.Prefixes) Affixes.Register(a);
    foreach (var a in cfg.Suffixes) Affixes.Register(a);

    api.Logger.Notification(
        $"[DriftRelics] loaded {cfg.Tiers.Count} tiers, {cfg.Signatures.Count} signatures, " +
        $"{cfg.Prefixes.Count} prefixes, {cfg.Suffixes.Count} suffixes");
}
```

- [ ] **Step 2: Build**

```
dotnet build DriftRelics.csproj --nologo
```

- [ ] **Step 3: Commit**

```bash
git add src/DriftRelicsModSystem.cs
git commit -m "feat(modsystem): load tiers + signatures into affix registry"
```

---

## Task 14: Update `affixes.json` — tiers, signatures, minTier on existing affixes, new content

**Files:**
- Modify: `assets/driftrelics/config/affixes.json`

- [ ] **Step 1: Replace the affixes.json content**

```jsonc
{
  "tiers": [
    { "code": "mundane",       "weight": 50, "color": "#aaaaaa", "affixCount": 1, "valueScale": 1.0 },
    { "code": "curious",       "weight": 30, "color": "#5b9aff", "affixCount": 2, "valueScale": 1.0 },
    { "code": "notable",       "weight": 15, "color": "#ffcc44", "affixCount": 2, "valueScale": 1.3 },
    { "code": "drift-touched", "weight":  5, "color": "#a855f7", "affixCount": 2, "valueScale": 1.6, "signature": true }
  ],
  "signatures": {
    "ring":     { "code": "drift_mark",        "langKey": "driftrelics:signature-drift_mark",
                  "mods": [{ "key": "meleeDamage", "value": 0.10, "op": "Mul" }] },
    "bracelet": { "code": "deep_vigor",        "langKey": "driftrelics:signature-deep_vigor",
                  "mods": [{ "key": "maxHealth",   "value": 8 }] },
    "trinket":  { "code": "whispered_insight", "langKey": "driftrelics:signature-whispered_insight",
                  "mods": [{ "key": "rangedDamageResist", "value": 0.08, "op": "Mul" }] }
  },
  "prefixes": [
    { "code": "burning",  "langKey": "driftrelics:affix-prefix-burning",  "weight": 10,
      "mods": [
        { "key": "heatResist",  "value": 2 },
        { "key": "meleeDamage", "value": 0.05, "op": "Mul" }
      ]},
    { "code": "hardened", "langKey": "driftrelics:affix-prefix-hardened", "weight": 10,
      "mods": [ { "key": "maxHealth", "value": 2 } ]},
    { "code": "swift",    "langKey": "driftrelics:affix-prefix-swift",    "weight": 10,
      "mods": [ { "key": "moveSpeed", "value": 0.03, "op": "Mul" } ]},
    { "code": "dappled",      "langKey": "driftrelics:affix-prefix-dappled",      "weight": 6, "minTier": "curious",
      "mods": [ { "key": "rangedAccuracy", "value": 0.03, "op": "Mul" } ]},
    { "code": "ancient",      "langKey": "driftrelics:affix-prefix-ancient",      "weight": 4, "minTier": "notable",
      "mods": [ { "key": "maxHealth", "value": 3 }, { "key": "rangedDamageResist", "value": 0.03, "op": "Mul" } ]},
    { "code": "drift_marked", "langKey": "driftrelics:affix-prefix-drift_marked", "weight": 3, "minTier": "drift-touched",
      "mods": [ { "key": "meleeDamage", "value": 0.08, "op": "Mul" }, { "key": "rangedDamage", "value": 0.08, "op": "Mul" } ]}
  ],
  "suffixes": [
    { "code": "of_the_bear",   "langKey": "driftrelics:affix-suffix-of_the_bear",   "weight": 5,
      "mods": [ { "key": "maxHealth", "value": 4 } ]},
    { "code": "of_swiftness",  "langKey": "driftrelics:affix-suffix-of_swiftness",  "weight": 10,
      "mods": [ { "key": "moveSpeed", "value": 0.05, "op": "Mul" } ]},
    { "code": "of_warding",    "langKey": "driftrelics:affix-suffix-of_warding",    "weight": 8,
      "mods": [ { "key": "rangedDamageResist", "value": 0.04, "op": "Mul" } ]},
    { "code": "of_resolve",    "langKey": "driftrelics:affix-suffix-of_resolve",    "weight": 5, "minTier": "notable",
      "mods": [ { "key": "maxHealth", "value": 5 } ]},
    { "code": "of_the_drift",  "langKey": "driftrelics:affix-suffix-of_the_drift",  "weight": 3, "minTier": "drift-touched",
      "mods": [ { "key": "moveSpeed", "value": 0.06, "op": "Mul" }, { "key": "rangedDamageResist", "value": 0.05, "op": "Mul" } ]}
  ]
}
```

- [ ] **Step 2: Build and smoke-test in game**

```
dotnet build DriftRelics.csproj --nologo
```

In-game: spawn relics via the Unidentified Roller, identify them at a lectern. Expect a mix of tiers, occasional drift-touched relics with signature mods active.

- [ ] **Step 3: Commit**

```bash
git add assets/driftrelics/config/affixes.json
git commit -m "feat(content): tier-locked affixes + signatures for 0.2.0"
```

---

## Task 15: Lang strings + README

**Files:**
- Modify: `assets/driftrelics/lang/en.json`
- Modify: `README.md`

- [ ] **Step 1: Append lang strings**

Add to `assets/driftrelics/lang/en.json`:

```jsonc
"tier-mundane":       "Mundane",
"tier-curious":       "Curious",
"tier-notable":       "Notable",
"tier-drift-touched": "Drift-touched",
"tier-line":          "Tier: {0}",
"aura-line":          "Aura: faint {0} shimmer",
"affix-prefix-dappled":      "Dappled",
"affix-prefix-ancient":      "Ancient",
"affix-prefix-drift_marked": "Drift-marked",
"affix-suffix-of_resolve":   "of Resolve",
"affix-suffix-of_the_drift": "of the Drift",
"signature-drift_mark":        "etched with the drift's mark",
"signature-deep_vigor":        "humming with deep vigor",
"signature-whispered_insight": "whispering forgotten secrets"
```

- [ ] **Step 2: Update README**

In `README.md`, in the Features section add:

```markdown
- **Rarity tiers** — every relic rolls one of four tiers (mundane / curious / notable / drift-touched), each with its own affix count, value scaling, and (at the top tier) a per-slot-type signature implicit affix. Tier is hidden until identified; unidentified relics show only a subtle tier-colored aura.
```

In "Things explicitly deferred to a later version", remove "Affix rarity tiers (magic / rare / legendary)."

- [ ] **Step 3: Commit**

```bash
git add assets/driftrelics/lang/en.json README.md
git commit -m "docs+lang: tier names, signature flavor, new affix names; README"
```

---

## Task 16: Final integration verification + push

**Files:** none (verification only)

- [ ] **Step 1: Full build + tests**

```
dotnet build DriftRelics.csproj --nologo
dotnet test tests/DriftRelics.Tests/DriftRelics.Tests.csproj --nologo
```

Expected: all tests pass, build succeeds.

- [ ] **Step 2: In-game smoke test**

- Place a Scholar's Lectern.
- Spawn ~10 unidentified relics with the Unidentified Roller (vary seeds).
- Expect a visible mix of tiers (most mundane, occasional drift-touched).
- Identify each, observe colored names in tooltips, drift-touched relics show signature flavor line.
- Equip a drift-touched relic, verify the stat boost matches the 1.6× scale of its rolled affix value plus signature boost.
- Save → exit → reload. Verify tiers persist.

- [ ] **Step 3: Update implementation plan checkboxes**

Tick every `- [ ]` in this file once verified.

- [ ] **Step 4: Push and ship 0.2.0**

```bash
git push origin main
```

The Release workflow will see existing tag `v0.1.0`, bump to `v0.2.0` if a `version:minor` label is on any merged PRs since (default behavior is patch — so confirm with the user whether to manually bump or label-driven minor).

---

## Open implementation questions (already noted in spec)

1. Best VS hook for the pre-identify icon overlay — investigated in Task 12. Fallback path documented.
2. Richtext `<font color>` support in `GetHeldItemName` vs `GetHeldItemInfo` — Task 11 routes through `GetHeldItemInfo` only; if a richtext name hook is found later, lift the colored display into the name.
3. Round-half-up vs banker's rounding for `Add` mod scaling — Task 9 settles on half-away-from-zero. Tests assert this.

## Scope check (post-write self-review)

Spec sections vs tasks:
- Tier model table → Task 14 (content) + Task 5 (registry storage)
- Roll mechanics → Tasks 6, 7
- minTier filter → Task 7
- Signature attachment → Task 8
- Value scaling at apply-time → Task 9
- Data model attribute → Task 4
- Display colored name → Task 11
- Pre-identify aura → Task 12
- Lectern interaction (no mechanical change) → no task needed
- Testing strategy → Tasks 1-10 (unit), Task 16 (manual)
- Public API additions → Task 10
- Affix content additions → Task 14
- Migration → Task 4 (legacy stacks default mundane)

Estimated scope: 16 tasks, ~16 commits, ~2-3 sessions.
