# Baubles Mod for Vintage Story

Accessory slots for the character screen, randomly-rolled affix names, and a research lectern for identification.

## Features (0.1.0)

- **4 new accessory slots** on the character screen — Ring × 2, Bracelet, Trinket.
- **Affix-based naming** — Prefix + Base + Suffix (e.g. *Burning Ring of Swiftness*) driven by a JSON-defined pool.
- **Unidentified state** — fresh baubles show a deterministic scrambled name and grant no modifiers until studied.
- **Scholar's Lectern** — a workstation block that identifies a single bauble over 60 seconds.
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

baubles.Affixes.Register(new Baubles.Affixes.Affix { /* ... */ });
baubles.Modifiers.Register("myKey", (player, value, op, code, apply) => { /* ... */ });
```

Note: the affix namespace is `Baubles.Affixes` (plural) — collides with the `Affix` class otherwise.

## Compatibility

- Vintage Story 1.22.x (.NET 10). Earlier versions targeted .NET 8; this mod's target framework was bumped to net10.0 for VS 1.22.

## Documentation

- Design spec: [docs/superpowers/specs/2026-05-15-baubles-design.md](docs/superpowers/specs/2026-05-15-baubles-design.md)
- Implementation plan: [docs/superpowers/plans/2026-05-15-baubles-implementation.md](docs/superpowers/plans/2026-05-15-baubles-implementation.md)

## Final manual checklist before tagging 0.1.0

Singleplayer:
- [ ] Baubles tab visible alongside Character and Traits.
- [ ] Slot type enforcement: ring rejects bracelet, etc.
- [ ] Persistence across save/load with same prefix/suffix/identified state.
- [ ] Scrambled name is deterministic by seed (re-roll same seed → same name).
- [ ] Identified bauble shows "[Prefix] [Base] [of Suffix]" with localised affix names.
- [ ] Equip an identified bauble → `EntityPlayer.Stats` shows the expected modifier code.
- [ ] Unequip → modifier disappears.
- [ ] Lectern: place unidentified bauble → wait 60s → identified, name resolves.
- [ ] Lectern: already-identified bauble passes through without progress.
- [ ] Save during research → progress persists across reload.

Multiplayer (host + one client):
- [ ] Each player has their own bauble inventory; reconnects preserve state.
- [ ] Client cannot interact with host's bauble inventory.
- [ ] Both players see the same scrambled name for the same stack.
- [ ] Identifying on the server flips the client tooltip without reconnect.

## Things explicitly deferred to a later version

- Survival loot generation (drops from creatures / structures).
- Custom shape/model for the Scholar's Lectern (currently a placeholder cube).
- Bespoke Scholar's Lectern dialog with a progress bar (v1 uses the default container UI).
- Affix rarity tiers (magic / rare / legendary).
- Re-rolling, socketing, transmutation.
- Player-model rendering of equipped baubles.
- ConfigLib integration for in-game affix editing.
