# Baubles Mod for Vintage Story

Adds accessory slots — rings, bracelet, trinket — to the character screen, with affix-based randomized item names and a research lectern for identifying unknown baubles.

## Features (planned for 0.1.0)

- **4 new accessory slots** on the character screen: Ring × 2, Bracelet, Trinket
- **Affix-based naming** — Prefix + Base + Suffix, à la Diablo / Path of Exile (e.g. *Burning Ring of Swiftness*)
- **Unidentified state** — newly-rolled baubles display a scrambled, unreadable name until studied
- **Research lectern** — a workstation that decodes unidentified baubles over time
- **Modifier framework** — affixes apply stat changes on equip and remove them on unequip
- **Public API** — other mods can register their own bauble items and react to equip/unequip events

## Installation

Drop the `Baubles` folder into your `VintagestoryData/Mods/` directory.

## Documentation

- Design spec: [docs/superpowers/specs/2026-05-15-baubles-design.md](docs/superpowers/specs/2026-05-15-baubles-design.md)

## Compatibility

- Vintage Story 1.20.x+
