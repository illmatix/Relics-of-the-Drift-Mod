# Affix Rarity Tiers — Design Spec

**Target version:** 0.2.0
**Date:** 2026-05-15
**Status:** Design proposal — awaiting user review

## Overview

Layer four rarity tiers on top of the existing affix system so that each rolled relic carries a tier value influencing affix count, mod magnitude, and (for the top tier) a signature implicit affix tied to the base type. Tier is hidden until identification at the lectern; an unidentified relic shows only a subtle tier-colored aura.

## Tier model

Drift-themed names, ARPG-style colors. Standard ordering left-to-right.

| Tier | `code` | Roll weight | Affix count | Value scale | Signature | Color |
|---|---|---:|---|---:|---|---|
| Mundane | `mundane` | 50 | 1 (prefix XOR suffix, 50/50) | 1.0× | — | `#aaaaaa` (light gray) |
| Curious | `curious` | 30 | 2 (prefix + suffix) | 1.0× | — | `#5b9aff` (cyan-blue) |
| Notable | `notable` | 15 | 2 (prefix + suffix) | 1.3× | — | `#ffcc44` (gold) |
| Drift-touched | `drift-touched` | 5 | 2 (prefix + suffix) | 1.6× | ✅ per base type | `#a855f7` (purple) |

Weights are configurable in `affixes.json` (see Data section).

## Roll mechanics

`RelicRoller` flow becomes:

1. Roll tier — weighted random over `tiers[].weight`.
2. Look up tier's `affixCount`, `valueScale`, `signature`.
3. Filter affix pool to entries where `minTier ≤ rolledTier` (tier order: mundane < curious < notable < drift-touched).
4. Roll `affixCount` affixes following existing weight-based selection. For mundane (1 affix), flip 50/50 between prefix vs suffix pool.
5. If `signature == true`, attach the slot-type's signature implicit affix.
6. Persist `relic.tier` on the stack.
7. Value scaling is **not** applied at roll time; applied at modifier-apply time so the source affix `value` stays clean. `ModifierRegistry.Apply` reads `relic.tier`, looks up the scale, and multiplies numeric mod values before invoking each handler.

## Affix pool changes

### Per-affix `minTier`

Each affix entry in `affixes.json` gets an optional `"minTier"` field (default `"mundane"`). Drives the filter step.

```jsonc
{ "code": "burning",       "minTier": "mundane",     ... }
{ "code": "ancient",       "minTier": "notable",     ... }
{ "code": "of_the_drift",  "minTier": "drift-touched", ... }
```

### Signatures (new section)

New `"signatures"` block in `affixes.json`, keyed by `RelicSlotType` enum name (lowercased):

```jsonc
"signatures": {
  "ring":     { "code": "drift_mark",        "langKey": "driftrelics:signature-drift_mark",
                "mods": [{ "key": "meleeDamage", "value": 0.10, "op": "Mul" }] },
  "bracelet": { "code": "deep_vigor",        "langKey": "driftrelics:signature-deep_vigor",
                "mods": [{ "key": "maxHealth",   "value": 8 }] },
  "trinket":  { "code": "whispered_insight", "langKey": "driftrelics:signature-whispered_insight",
                "mods": [{ "key": "rangedDamageResist", "value": 0.08, "op": "Mul" }] }
}
```

Signature mods get the same `valueScale` treatment as rolled affixes (1.6× at drift-touched).

### Tier config (new section)

```jsonc
"tiers": [
  { "code": "mundane",       "weight": 50, "color": "#aaaaaa", "affixCount": 1, "valueScale": 1.0 },
  { "code": "curious",       "weight": 30, "color": "#5b9aff", "affixCount": 2, "valueScale": 1.0 },
  { "code": "notable",       "weight": 15, "color": "#ffcc44", "affixCount": 2, "valueScale": 1.3 },
  { "code": "drift-touched", "weight":  5, "color": "#a855f7", "affixCount": 2, "valueScale": 1.6, "signature": true }
]
```

The existing top-level `"rollChances"` block is **removed** — tier rules supersede it. Migration note: any in-the-wild save data without a tier defaults to `mundane` on read.

## Data model

New tree attribute on every relic ItemStack:

| Key | Type | Notes |
|---|---|---|
| `relic.tier` | string | One of `mundane`, `curious`, `notable`, `drift-touched`. Backfill default = `mundane` for legacy stacks. |

No new storage for the signature affix code — derived from `(slotType, tier)` lookup at apply/display time.

`RelicsUtil` gains:
- `GetTier(ItemStack) → string` (with default fallback)
- `SetTier(ItemStack, string)`

`RelicInstance` POCO gains a `Tier` field. `WriteInstance` / `GetInstance` extended accordingly.

## Display

### Identified name

Identified name format unchanged ("[Prefix] [Base] [of Suffix]") but wrapped in a Cairo richtext color tag matching `tiers[].color`. For drift-touched, append the signature flavor in a smaller line (e.g., a subtitle "drift-marked" via richtext break).

Color is applied at `RelicsDisplay.AssembleName` (or equivalent). `GetHeldItemName` returns plain string; `GetHeldItemInfo` (or our hook into the in-world item label) returns the colored version. Investigate which VS hook supports richtext color before locking implementation — fallback is the plain name with tier name suffixed.

### Pre-identify aura hint

Unidentified relic stacks show no name color (still scrambled gibberish in default color) but a small **tier-colored sigil** is rendered as an overlay on the item icon in any slot. Higher tiers = more saturated / brighter overlay.

Implementation candidates (pick at impl time):
- **Attribute renderer** — VS has a system for per-stack icon overlays. Investigate `IRenderInfo` / item attribute renderer hooks.
- **Custom `OnRender`** on `ItemRelic` — manually draw a colored corner sigil after the base item renders.
- **Cairo overlay in slot composer** — last resort, more invasive.

Spec MVP: a small colored dot/diamond in the bottom-right corner of the item icon. Mundane = no overlay (default), curious = small dot, notable = larger dot, drift-touched = pulsing dot or distinct shape.

If the overlay implementation turns out to require deep VS-internal work, fall back to a tooltip line ("Aura: faint curious shimmer") — still visible pre-identify, just not as a glyph.

### Lectern dialog

After identification, the dialog displays the relic with tier color on the name. Optionally — and only as polish — vary the amber rune-ring / glow tint by tier:
- mundane / curious: amber (current)
- notable: gold (warmer)
- drift-touched: purple

Treat as deferred polish; ship the colored name first.

## Modifier scaling

`ModifierRegistry` currently invokes per-mod handlers with the raw `value`. To honor `valueScale`, the apply-time path reads `relic.tier` from the stack, looks up the scale, and passes `scaledValue = mod.value * scale` to the handler.

`Mul` op uses `1 + (value × scale)` style if that's how the registry interprets it — verify in impl. `Add` op straightforwardly scales the additive amount. Integer mods need a rounding rule (round-half-up to avoid 0-value rounds).

For the signature affix specifically: same scaling path applies. Signatures are only present on drift-touched (1.6×), so a `value: 0.10 Mul` becomes `0.16 Mul` after scaling.

## Lectern interaction

No mechanical change. Identifying a relic resolves both affixes AND tier (the tier is already on the stack — identify is just "reveal what's there"). Once `relic.identified` flips true, the colored name renders and any post-identify polish (signature affix description in tooltip, etc.) kicks in.

## Testing strategy

Pure-logic tests (extend existing xUnit project):
- Tier roll distribution: with 10,000 rolls, observed frequencies ≈ configured weights within tolerance.
- Affix filtering by minTier: a drift-touched-only affix never rolls on curious.
- Affix count per tier: drift-touched always rolls exactly 2 + signature.
- `valueScale` arithmetic for Add and Mul ops, with rounding rule.
- Signature lookup by `(slotType, tier)`.
- Legacy stack (no `relic.tier`) reads as `mundane`.

Integration / manual:
- Identify a drift-touched relic; observe purple name + signature mod active in `EntityPlayer.Stats`.
- Unidentified stacks of different tiers show different overlay sigils.
- Save/load preserves tier.
- Public API: third-party mods can read tier via `IRelicsAPI` (new method) and register tier-aware modifier handlers.

## Public API additions

`IRelicsAPI` extensions:
- `string GetTier(ItemStack)`
- `event OnRelicRolled(ItemStack, tier)` (optional — for mods that want to react to a drift-touched roll)
- Tier config exposed read-only as `ITierConfig[] Tiers { get; }`

## Affix content additions

Author 2-3 new tier-locked affixes per non-mundane tier to give roll variety meaning. Initial content:

- **curious-only**
  - prefix `dappled` — small perception bonus
- **notable-only**
  - prefix `ancient` — moderate combined defense
  - suffix `of_resolve` — moderate fortify
- **drift-touched-only**
  - prefix `drift_marked` — large mixed combat
  - suffix `of_the_drift` — large mixed defense

Plus the 3 signatures listed above. All values left as `TBD-balance` for the implementation pass — exact numbers tuned during the spec → plan stage.

## Migration

Legacy 0.1.x stacks have no `relic.tier`. On first read by `RelicsUtil.GetTier`, default to `mundane`. No retroactive re-roll — old relics stay at mundane forever (their affixes were rolled without tier constraints, so they're effectively legacy stock).

## Open questions / decisions for implementation

1. **Richtext color support** — confirm which VS display hook (`GetHeldItemName` vs `GetHeldItemInfo` vs in-world label) accepts inline color tags. Determines whether the colored name is in the held-item name or only in the tooltip block.
2. **Overlay rendering hook** — best place to draw the pre-identify sigil. Probably `IRenderInfo`-style attribute renderer, but needs verification.
3. **Tier name localisation** — lang keys: `driftrelics:tier-mundane`, etc. Color sourced from JSON; name from lang.
4. **Lectern progress speed** — should drift-touched take longer to identify? Probably yes (gameplay reward for finding rares). Defer to a later pass; keep flat 60s for 0.2.0.
5. **Drop rates for survival loot** — relevant when survival drops land (later). Spec assumes creative-debug-roller use only.

## Scope estimate

- ~2-3 implementation sessions split as: data + roll mechanics (1) + display (1) + signature affixes + new content + polish (1).
- ~6-10 commits.

## Deferred to a later version

- Re-roll / reforge stations (turn mundane into curious, etc.)
- Tier-locked socketing
- Player visible aura/particle effect on equipped drift-touched relics
- Lectern dialog tier-specific glow color
