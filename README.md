# Relics of the Drift — Vintage Story Mod

Accessory slots for the character screen, randomly-rolled affix names, and a research lectern for identification.

**Mod ID:** `driftrelics`

## Features (0.1.0)

- **4 new accessory slots** on the character screen — Ring × 2, Bracelet, Trinket.
- **Affix-based naming** — Prefix + Base + Suffix (e.g. *Burning Ring of Swiftness*) driven by a JSON-defined pool.
- **Rarity tiers** — every relic rolls one of four tiers (mundane / curious / notable / drift-touched), each with its own affix count, value scaling, and (at the top tier) a per-slot-type signature implicit affix. Tier is hidden until identified; unidentified relics show a subtle tier-colored aura hint in the tooltip.
- **Unidentified state** — fresh relics show a deterministic scrambled name and grant no modifiers until studied.
- **Scholar's Lectern** — a freestanding podium with a closed book on a slanted reading surface; identifies a single relic over 60 seconds via a bespoke parchment-themed dialog with a live progress bar, amber rune accents around the slot, and ember motes drifting up during research.
- **Themed UI** — the Relics character tab uses the same procedurally-drawn parchment styling (radial vignette, fold creases, ink stains, braided amber accent) so the two screens read as one mod.
- **Modifier framework** — affixes carry stat modifiers (move speed, max health, melee damage, etc.) that apply on equip and remove on unequip.
- **Public API** — other mods can register affixes, modifier handlers, or react to equip/unequip events.

## Installation

Drop the `DriftRelics` folder into your `VintagestoryData/Mods/` directory.

## Configuration

- `assets/driftrelics/config/affixes.json` — affix pool. Edit to add/remove prefixes and suffixes, change weights, or rebalance modifier values.
- `assets/driftrelics/config/lectern.json` — research duration in seconds (default 60).

## API for other mods

```csharp
var relics = api.ModLoader.GetModSystem<DriftRelics.DriftRelicsModSystem>().Api;

relics.OnRelicEquipped   += (player, stack, slot) => { /* ... */ };
relics.OnRelicUnequipped += (player, stack, slot) => { /* ... */ };
relics.OnRelicIdentified += (player, stack)       => { /* ... */ };

relics.Affixes.Register(new DriftRelics.Affixes.Affix { /* ... */ });
relics.Modifiers.Register("myKey", (player, value, op, code, apply) => { /* ... */ });
```

Note: the affix namespace is `DriftRelics.Affixes` (plural) — collides with the `Affix` class otherwise.

## Compatibility

- Vintage Story 1.22.x (.NET 10). Earlier versions targeted .NET 8; this mod's target framework was bumped to net10.0 for VS 1.22.

## Documentation

- Design spec: [docs/superpowers/specs/2026-05-15-baubles-design.md](docs/superpowers/specs/2026-05-15-baubles-design.md)
- Implementation plan: [docs/superpowers/plans/2026-05-15-baubles-implementation.md](docs/superpowers/plans/2026-05-15-baubles-implementation.md)

## Final manual checklist before tagging 0.1.0

Singleplayer:
- [x] Relics tab visible alongside Character and Traits.
- [x] Slot type enforcement: ring rejects bracelet, etc.
- [x] Persistence across save/load with same prefix/suffix/identified state.
- [x] Scrambled name is deterministic by seed (re-roll same seed → same name).
- [x] Identified relic shows "[Prefix] [Base] [of Suffix]" with localised affix names.
- [x] Equip an identified relic → `EntityPlayer.Stats` shows the expected modifier code.
- [x] Unequip → modifier disappears.
- [x] Lectern: place unidentified relic → wait 60s → identified, name resolves.
- [x] Lectern: already-identified relic passes through without progress.
- [x] Save during research → progress persists across reload.

Multiplayer (host + one client):
- [ ] Each player has their own relic inventory; reconnects preserve state.
- [ ] Client cannot interact with host's relic inventory.
- [ ] Both players see the same scrambled name for the same stack.
- [ ] Identifying on the server flips the client tooltip without reconnect.

## Things explicitly deferred to a later version

- Survival loot generation (drops from creatures / structures).
- Re-rolling, socketing, transmutation.
- Player-model rendering of equipped relics.
- ConfigLib integration for in-game affix editing.
