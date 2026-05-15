# Baubles Mod for Vintage Story — Design

**Date:** 2026-05-15
**Target game version:** Vintage Story 1.20.x
**Mod type:** Universal (client + server)
**Loader:** C# code mod with assets (`modinfo.json` `type: "code"`)

## Goal

Add a "Baubles" system to Vintage Story:

1. A small set of accessory inventory slots that appear as a new tab on the
   existing character screen (next to Character and Traits), persist with
   the player, and sync over the network.
2. An affix-based naming model — every bauble has a randomly-rolled
   **Prefix + Base Type + Suffix** name (e.g. *Burning Ring of the Bear*)
   driven by JSON-configured affix pools, with each affix carrying stat
   modifiers that apply on equip and are removed on unequip.
3. An **unidentified** state for newly-rolled baubles: until studied at a
   research lectern, the player sees only a deterministic scrambled name
   and no mods.
4. A new workstation block — the **Scholar's Lectern** — that takes an
   unidentified bauble as input and produces an identified version after
   a timed research process.
5. A public API so other mods can register their own bauble items, define
   their own affixes, and react to equip/unequip.

The mod ships the framework plus three base bauble item types (ring,
bracelet, trinket), a starter affix pool, the Scholar's Lectern, and a
creative-tab "Roll Unidentified Bauble" debug item to produce test rolls.

## Non-Goals (v1)

- Rendering equipped baubles on the player model.
- Survival-mode loot generation (creature drops, structure loot). The only
  v1 source is the creative debug roll item and a single grid recipe; loot
  hooks come in v1.1.
- Configurable slot counts or runtime-defined slot types.
- Cosmetic vs. functional split.
- Bauble-on-corpse rendering or recovery UI beyond standard inventory drop.
- Affix rarity tiers or weighted "magic / rare / legendary" classes —
  v1 has a flat weighted pool. Tiers can come later.
- Re-rolling or socketing.

## Reference Points (decompiled VS source)

All paths refer to the reference repo at
`macgyver:~/workspace/vs-api-reference/`.

- `VintagestoryAPI/VintagestoryAPI.decompiled.cs:57117` —
  `GuiDialogCharacterBase` exposes
  `List<GuiTab> Tabs` and `List<Action<GuiComposer>> RenderTabHandlers`. This
  is the public extension point for adding tabs.
- `VSSurvivalMod/VSSurvivalMod.decompiled.cs:145672` — survival mod's
  `CharacterSystem.StartClientSide` adds the "Traits" tab via
  `charDlg.Tabs.Add(new GuiTab { Name = ..., DataInt = 1 })` and
  `charDlg.RenderTabHandlers.Add(composeTraitsTab)`. We mirror this exactly.
- `VintagestoryAPI/VintagestoryAPI.decompiled.cs:135592` —
  `InventoryBasePlayer : InventoryBase, IOwnedInventory` is the base class for
  player-attached inventories. `RemoveOnClose = false`, owner-only access.
- `VSSurvivalMod/VSSurvivalMod.decompiled.cs:101104` —
  `EntityBehaviorSeraphInventory` is the canonical example of an
  `EntityBehavior` that owns an `InventoryBase` and calls
  `inv.LateInitialize("<class>-" + entity.EntityId, Api)` in `Initialize`. We
  copy this lifecycle.
- `VSSurvivalMod/VSSurvivalMod.decompiled.cs:153001` —
  `RegisterEntityBehaviorClass("seraphinventory", ...)` is invoked from
  `ModSystem.Start(ICoreAPI)`. We register `"baubles"` the same way.

## Architecture

```
┌────────────────────────────────────────────────────────────────┐
│  BaublesModSystem (Universal)                                  │
│   - Start():                                                   │
│       register entity behavior, item class, block class,       │
│       block-entity class                                       │
│   - AssetsFinalize():                                          │
│       load affix pool from assets/baubles/config/affixes.json  │
│   - StartClientSide():                                         │
│       hook character dialog, add "Baubles" tab                 │
│   - StartServerSide():                                         │
│       open bauble inventory on PlayerJoin (mirror Seraph)      │
│   - Holds: AffixRegistry, ModifierRegistry, IBaublesAPI        │
└────────────────────────────────────────────────────────────────┘
        │                  │                  │              │
        ▼                  ▼                  ▼              ▼
┌──────────────────┐  ┌─────────────┐  ┌────────────┐  ┌──────────────┐
│ EntityBehavior   │  │ Client GUI  │  │ Scholar's  │  │ Item rolling │
│ Baubles          │  │ Baubles tab │  │ Lectern    │  │ pipeline     │
│  - Inventory     │  │ on char dlg │  │  - Block   │  │ - BaubleRoll │
│  - on slot       │  │             │  │  - BE w/   │  │   er         │
│    modified →    │  │ Reads:      │  │    timer + │  │ - Applies    │
│    apply/remove  │  │ - inv       │  │    1 slot  │  │   prefix,    │
│    affix mods    │  │ - rendering │  │  - GUI:    │  │   suffix,    │
│  - holds         │  │   uses      │  │   identify │  │   seed,      │
│    AppliedMods   │  │   GetHeld   │  │   timer +  │  │   identified │
│    tree for      │  │   ItemName  │  │   progress │  │   = false to │
│    cleanup       │  │   override  │  │   bar      │  │   stack      │
└──────────────────┘  └─────────────┘  └────────────┘  └──────────────┘
```

### Persistence and sync

`InventoryBasePlayer` already integrates with the player save data:
`ToTreeAttributes`/`FromTreeAttributes` are called by the base game when the
player is saved/loaded, and slot-level changes are batched via `dirtySlots`
and synced through the standard inventory channel once the inventory has been
`Open`ed for the owning player. We do NOT register a custom network channel
for slot contents.

The entity behavior:
1. Constructs the inventory in its constructor.
2. Calls `inv.LateInitialize("baubles-" + entity.EntityId, Api)` in
   `Initialize`.
3. On `ICoreClientAPI`/`ICoreServerAPI` `PlayerJoin`, calls
   `player.InventoryManager.OpenInventory(inv)` server-side (mirror of how
   survival opens the character/seraph inventory).
4. Calls `loadInv()` to pull state from the entity's `WatchedAttributes`
   tree under key `"baublesInv"`, and registers a modified listener so
   server-driven changes propagate to the client behavior.

(Step 4 is only necessary if standard inventory sync proves insufficient for
our cross-side equip events; if base sync is enough we drop the watched
attribute mirror. We will validate during implementation. The spec records
**both** the simple "rely on base sync" path and the watched-attribute mirror;
the implementer chooses based on what survives a multiplayer reconnect test.)

## Components

### 1. `InventoryBaubles : InventoryBasePlayer`

- Class name (for registration): `"baubles"`.
- Fixed slot count: 4.
- Slot layout (by index):
  | Index | Slot     | Accepts `slotType` value |
  |-------|----------|--------------------------|
  | 0     | Ring 1   | `ring`                   |
  | 1     | Ring 2   | `ring`                   |
  | 2     | Bracelet | `bracelet`                |
  | 3     | Trinket  | `trinket`                |
- Each slot is an `ItemSlotBauble` that overrides `CanHold(ItemSlot from)` to
  check the source stack via `BaublesUtil.GetSlotType(stack)` against the
  slot's declared `AllowedSlotType`.
- `FromTreeAttributes` / `ToTreeAttributes` use the standard
  `SlotsFromTreeAttributes` / `SlotsToTreeAttributes` helpers used by
  `InventoryGear`.
- `baseWeight = 1.5f` (cosmetic — affects auto-sort suitability).
- `OnItemSlotModified(slot)` calls
  `BaublesModSystem.OnSlotModified(Owner, slotIndex, slot)` so the mod system
  can fire `OnBaubleEquipped` / `OnBaubleUnequipped`.

### 2. `ItemSlotBauble : ItemSlot`

- Constructor takes the parent inventory and the `BaubleSlotType` it accepts.
- `BackgroundIcon` set to a string like `"ring"`, `"bracelet"`, `"trinket"`
  so empty slots show a hint icon (we ship matching textures under
  `assets/baubles/textures/gui/itemslotbg/`).
- `MaxSlotStackSize = 1` (baubles never stack).

### 3. `EntityBehaviorBaubles : EntityBehavior`

- `PropertyName()` returns `"baubles"`.
- `InventoryClassName` returns `"baubles"`.
- Constructor builds `InventoryBaubles(null, null)`.
- `Initialize`:
  1. `Api = entity.World.Api`
  2. `inv.LateInitialize("baubles-" + entity.EntityId, Api)`
  3. Reads `entity.WatchedAttributes.GetTreeAttribute("baublesInv")` into
     `inv` if present (mirror approach, see Persistence section above).
  4. Registers a modified listener on `"baublesInv"`.
  5. Re-applies modifiers for every already-equipped, identified bauble.
     (Mods are computed from affixes — they are not persisted on the
     player, only their **provenance** is, via a small `AppliedMods` tree
     under `WatchedAttributes["baublesAppliedMods"]`. On reload we trust
     the slot contents and recompute.)
- `OnEntityDespawn`: detaches the modified listener, removes any modifiers
  it applied so they don't leak across re-spawn/world-reload edge cases.
- On every slot modification (`OnSlotModified` callback from the inventory):
  - If the slot was previously holding stack `oldStack` and `oldStack` was
    identified, call `ModifierRegistry.RemoveMods(player, oldStack)` and
    fire `OnBaubleUnequipped`.
  - If the new stack is identified, call `ModifierRegistry.ApplyMods(player,
    newStack)` and fire `OnBaubleEquipped`. If it's unidentified, no mods
    are applied and only `OnBaubleEquipped` fires (with `identified=false`
    in event args).
- The behavior is attached to the player via a JSON patch:
  `assets/baubles/patches/entityplayer-behaviors.json`, applied to
  `game:entities/humanoid/seraph-male.json` and
  `game:entities/humanoid/seraph-female.json` (and any other player entity
  files that exist in 1.20.x — verify during implementation), inserting
  `{ code: "baubles" }` into the `server.behaviors` AND `client.behaviors`
  arrays.

### 4. `BaublesModSystem : ModSystem`

- Public fields:
  - `public IBaublesAPI Api { get; private set; }` — assigned in `Start()`
    to a concrete `BaublesAPI` instance.
  - `public AffixRegistry Affixes { get; private set; }`
  - `public ModifierRegistry Modifiers { get; private set; }`
- `Start(ICoreAPI api)`:
  - `Affixes = new AffixRegistry()`
  - `Modifiers = new ModifierRegistry(api)`
  - `Api = new BaublesAPI(api, Affixes, Modifiers)`
  - `api.RegisterEntityBehaviorClass("baubles", typeof(EntityBehaviorBaubles))`
  - `api.RegisterItemClass("ItemBauble", typeof(ItemBauble))`
  - `api.RegisterBlockClass("BlockScholarsLectern", typeof(BlockScholarsLectern))`
  - `api.RegisterBlockEntityClass("BEScholarsLectern", typeof(BEScholarsLectern))`
- `AssetsFinalize(ICoreAPI api)`:
  - Load `assets/baubles/config/affixes.json` via
    `api.Assets.Get(...)`, resolve each affix's `mods` keys against the
    known `ModifierRegistry` entries, and populate `Affixes`. Log a warning
    for any unknown modifier key (we don't hard-fail; an external mod may
    add the modifier later).
- `StartClientSide(ICoreClientAPI capi)`:
  - On `capi.Event.LevelFinalize` (after dialogs are loaded), find the
    character dialog: `capi.Gui.LoadedGuis.OfType<GuiDialogCharacterBase>().FirstOrDefault()`.
  - Add a tab: `dlg.Tabs.Add(new GuiTab { Name = Lang.Get("charactertab-baubles"), DataInt = 2 })`.
  - Add the renderer: `dlg.RenderTabHandlers.Add(ComposeBaublesTab)`.
- `StartServerSide(ICoreServerAPI sapi)`:
  - On `sapi.Event.PlayerJoin`, walk the player entity's bauble behavior and
    `player.InventoryManager.OpenInventory(behavior.Inventory)` so the
    inventory is registered for sync. (Mirrors what the base game does for
    the seraph inventory.)
- Public methods (the API surface — see section below).

### 5. `IBaublesAPI` (public surface for other mods)

```csharp
public interface IBaublesAPI
{
    InventoryBaubles GetBaubles(EntityPlayer player);
    bool IsBauble(ItemStack stack);
    BaubleSlotType? GetSlotType(ItemStack stack);

    // Per-stack helpers
    bool IsIdentified(ItemStack stack);
    BaubleInstance? GetInstance(ItemStack stack);   // null = not a bauble
    string GetDisplayName(ItemStack stack);         // scrambled or assembled

    // Affix / modifier registries (other mods register their own here)
    IAffixRegistry Affixes { get; }
    IModifierRegistry Modifiers { get; }

    // Rolling
    ItemStack RollUnidentifiedBauble(BaubleSlotType slotType, int seed);
    void Identify(ItemStack stack);   // server-side; sets identified=true

    // Events
    event Action<EntityPlayer, ItemStack, BaubleSlotType> OnBaubleEquipped;
    event Action<EntityPlayer, ItemStack, BaubleSlotType> OnBaubleUnequipped;
    event Action<EntityPlayer, ItemStack> OnBaubleIdentified;
}

public enum BaubleSlotType { Ring, Bracelet, Trinket }

public sealed record BaubleInstance(
    BaubleSlotType SlotType,
    string PrefixCode,           // may be null (no prefix)
    string SuffixCode,           // may be null (no suffix)
    long   Seed,
    bool   Identified);
```

Other mods access it via:
```csharp
var baublesApi = api.ModLoader.GetModSystem<BaublesModSystem>().Api;
```

A stack is a bauble if either:
- Its `Collectible` implements `IBaubleItem` (returns its `SlotType`), OR
- Its `Collectible.Attributes["bauble"]["slotType"]` is a string matching a
  `BaubleSlotType` value. This lets mods JSON-patch vanilla items into being
  baubles without writing C#.

#### Per-stack tree attributes

Every rolled bauble stores its instance state under `stack.Attributes`:

| Key                         | Type    | Notes                                |
|-----------------------------|---------|--------------------------------------|
| `bauble.identified`         | bool    | false until decoded at the lectern   |
| `bauble.seed`               | long    | seeds both affix roll and scramble   |
| `bauble.prefix`             | string  | affix code or empty                  |
| `bauble.suffix`             | string  | affix code or empty                  |

(`bauble.slotType` lives on the item type's `Collectible.Attributes`, not
per-stack — base type is fixed at craft/roll time.)

### 6. `ItemBauble : Item` and `IBaubleItem`

```csharp
public interface IBaubleItem
{
    BaubleSlotType SlotType { get; }
}

public class ItemBauble : Item, IBaubleItem
{
    public BaubleSlotType SlotType => Enum.Parse<BaubleSlotType>(
        Attributes["bauble"]["slotType"].AsString("Trinket"), true);

    public override string GetHeldItemName(ItemStack itemStack)
        => BaublesUtil.GetDisplayName(itemStack, fallback: base.GetHeldItemName(itemStack));

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc,
                                        IWorldAccessor world, bool withDebugInfo)
    {
        var stack = inSlot.Itemstack;
        if (BaublesUtil.IsIdentified(stack))
        {
            BaublesUtil.AppendIdentifiedDescription(stack, dsc, world);
        }
        else
        {
            dsc.AppendLine(Lang.Get("baubles:unidentified-hint"));
        }
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
    }
}
```

### 7. `AffixRegistry` and affix configuration

```csharp
public sealed class Affix
{
    public string Code;                       // "burning"
    public string LangKey;                    // "baubles:affix-prefix-burning"
    public AffixKind Kind;                    // Prefix | Suffix
    public int Weight = 10;                   // weighted random pick
    public BaubleSlotType[] AllowedSlots;     // null = any
    public List<ModifierEntry> Mods;          // applied on equip
}

public enum AffixKind { Prefix, Suffix }

public sealed class ModifierEntry
{
    public string Key;        // "moveSpeed", "maxHealth", ...
    public double Value;      // +0.05, +4, ...
    public ModifierOp Op = ModifierOp.Add;    // Add | Mul
}
```

`assets/baubles/config/affixes.json`:

```json
{
  "rollChances": { "prefix": 0.75, "suffix": 0.75 },
  "prefixes": [
    { "code": "burning",   "langKey": "baubles:affix-prefix-burning",
      "weight": 10, "mods": [
        { "key": "heatResist",   "value": 2 },
        { "key": "meleeDamage",  "value": 0.05, "op": "Mul" }
    ]},
    { "code": "hardened",  "langKey": "baubles:affix-prefix-hardened",
      "weight": 10, "mods": [
        { "key": "maxHealth",    "value": 2 }
    ]},
    { "code": "swift",     "langKey": "baubles:affix-prefix-swift",
      "weight": 10, "mods": [
        { "key": "moveSpeed",    "value": 0.03, "op": "Mul" }
    ]}
  ],
  "suffixes": [
    { "code": "of_the_bear",  "langKey": "baubles:affix-suffix-of_the_bear",
      "weight": 5,  "mods": [
        { "key": "maxHealth",    "value": 4 }
    ]},
    { "code": "of_swiftness", "langKey": "baubles:affix-suffix-of_swiftness",
      "weight": 10, "mods": [
        { "key": "moveSpeed",    "value": 0.05, "op": "Mul" }
    ]},
    { "code": "of_warding",   "langKey": "baubles:affix-suffix-of_warding",
      "weight": 8,  "mods": [
        { "key": "rangedDamageResist", "value": 0.04, "op": "Mul" }
    ]}
  ]
}
```

External mods extend the pool via either:
- A JSON patch against `baubles:config/affixes.json` (adds entries), or
- Imperatively at `AssetsFinalize`:
  `baublesApi.Affixes.Register(new Affix { ... });`

### 8. `ModifierRegistry` (stat applicator)

A small registry of known modifier keys mapped to `EntityPlayer.Stats`
modifier names. The bauble system never touches gameplay stats directly —
it goes through `EntityPlayer.Stats.Set(category, code, value, persistent)`
so that VS's existing stat-stacking and tooltip systems handle the rest.

Each `(stack-seed, modifier-key)` pair becomes a unique stat modifier code
of the form `baubles:<modkey>:<seedHex>` (where `seedHex` is the
hexadecimal representation of `stack.Attributes.GetLong("bauble.seed")`)
so the same modifier from two different baubles stacks cleanly and so
removal can target the exact code that was applied.

v1 ships these canonical modifier keys:

| Key                    | Stats category   | Notes                       |
|------------------------|------------------|-----------------------------|
| `moveSpeed`            | `walkspeed`      | Mul preferred               |
| `maxHealth`            | `maxhealth`      | Add (flat HP)               |
| `meleeDamage`          | `meleeWeaponsDamage` | Mul preferred           |
| `rangedDamage`         | `rangedWeaponsDamage` | Mul preferred          |
| `hungerRate`           | `hungerrate`     | Mul; negative = slower       |
| `coldResist`           | `bodyTempHotMin` | Add — biases temp band      |
| `heatResist`           | `bodyTempHotMax` | Add — biases temp band      |
| `rangedDamageResist`   | `rangedWeaponsDamageReceived` | Mul; negative = less |

`ApplyMods(player, stack)` and `RemoveMods(player, stack)` iterate the
stack's affixes' mod entries and call into `EntityPlayer.Stats` with a
stable per-stack code. Other mods can register new keys via
`api.Modifiers.Register("myKey", (player, value, op, add) => { ... })`.

### 9. Name resolution and scrambled glyphs

`BaublesUtil.GetDisplayName(stack, fallback)`:

- If `stack` is not a bauble → return `fallback`.
- If `bauble.identified == false` → return
  `ScrambleNameGenerator.Generate(stack.Attributes.GetLong("bauble.seed"))`.
- Otherwise assemble: `[prefixName] [baseName] [of suffixName]` where
  `baseName = Lang.Get(stack.Collectible.Code.ToShortString())` and the
  affix names come from `Lang.Get(affix.langKey)`.

`ScrambleNameGenerator` is deterministic — given the same seed it always
returns the same string. Algorithm (v1):

```
seed → System.Random rng = new(seed)
chooseFromTable = pick a row from a fixed consonant/vowel cluster table:
  consonants:  ["th","sk","vr","dr","kr","mk","ven","ul","drai","sko"]
  vowels:      ["ai","ul","ok","oo","ae","io","an","or","ei"]
syllables: rng.Next(2,5)
firstUpper = capitalise the very first letter
optional connector " of " between two name halves: rng.NextDouble() < 0.4
```

Example outputs for seeds 1, 2, 3:
```
1 → "Drai-Skul Venmok"
2 → "Thaivr Skor of Aithven"
3 → "Mkul Drai"
```

The same seed must produce the same name on every client — `System.Random`
seeded explicitly is deterministic across .NET 8 runtimes, which is fine
for our purposes.

### 10. `BaubleRoller`

Pure function: `BaubleRoller.Roll(slotType, seed, affixRegistry) → BaubleInstance`.

```
rng = new System.Random(seed)

prefix = (rng.NextDouble() < rollChances.prefix)
           ? WeightedPick(affixes.prefixes filtered by slotType, rng)
           : null

suffix = (rng.NextDouble() < rollChances.suffix)
           ? WeightedPick(affixes.suffixes filtered by slotType, rng)
           : null

return new BaubleInstance(slotType, prefix?.Code, suffix?.Code, seed,
                         Identified: false)
```

`BaublesAPI.RollUnidentifiedBauble(slotType, seed)`:
- Resolves the base `ItemBauble` for `slotType` via collectible attribute
  lookup (`Item.Code.Domain == "baubles"` &&
  `Item.Attributes["bauble"]["slotType"] == slotType`).
- Creates an `ItemStack` from that item, writes `bauble.{seed,prefix,
  suffix,identified=false}` into `stack.Attributes`, returns the stack.

### 11. Scholar's Lectern

A new block + block entity that decodes one bauble at a time.

`assets/baubles/blocktypes/scholarslectern.json`:

- Shape: ships a placeholder cube for v1 (`game:block/cube`) with a custom
  texture. (A proper lectern shape is a v1.1 nice-to-have; not blocking.)
- 1 m x 1 m x 1 m footprint, drops itself on break.
- `entityClass: "BEScholarsLectern"`.

`BlockScholarsLectern : Block`:
- `OnBlockInteractStart`: opens `BEScholarsLectern.OpenGuiFor(player)`.

`BEScholarsLectern : BlockEntity`:
- One slot `InventoryGeneric inventory (1 slot, "scholarslectern-<x,y,z>")`.
- Tick listener (server-only) running every 100ms:
  - If the slot contains an unidentified bauble and `researchProgress <
    researchDurationSeconds`, increment by `dt`.
  - When `researchProgress >= researchDurationSeconds`, call
    `baublesApi.Identify(stack)` and reset progress to 0.
- `MarkDirty(true)` whenever progress or identified state changes so the
  GUI animates.
- Persistence:
  - `ToTreeAttributes` writes inventory + `researchProgress`.
  - `FromTreeAttributes` restores both.
- `researchDurationSeconds` default = 60 (configurable in
  `assets/baubles/config/lectern.json`).

`GuiDialogScholarsLectern : GuiDialogBlockEntity`:
- Single item slot bound to the BE's inventory.
- Progress bar showing `researchProgress / researchDurationSeconds`.
- Text:
  - empty slot → "Place an unidentified bauble to research."
  - identified bauble → "Already identified."
  - unidentified bauble, in progress → "Deciphering… N%".
  - unidentified bauble, idle → progress = 0; first tick begins on next
    server tick.

Only the player who placed the bauble can interact (standard
`BlockEntity.OnPlayerRightClick` access pattern; we do not lock the
lectern long-term — placement and retrieval are open like a barrel).

### 12. v1 source of unidentified baubles

- **Creative tab debug item:** `ItemUnidentifiedRoller`. Right-click in
  hand rolls a fresh unidentified bauble of a slot type chosen via
  right-click cycle (ring → bracelet → trinket → ring). Hand-only; not
  obtainable in survival.
- **Grid recipe (single):** 1 gear + 1 paper + 1 ink + 1 quartz →
  produces one unidentified bauble with a random slot type. This is a
  placeholder so survival players have a v1 source of rolls. Replaced
  with proper loot in v1.1.

## Asset Layout

```
Baubles/
  Baubles.csproj
  modinfo.json
  src/
    BaublesModSystem.cs
    Api/
      IBaublesAPI.cs
      BaubleSlotType.cs
      IBaubleItem.cs
      BaubleInstance.cs
      BaublesUtil.cs                 // GetSlotType / IsBauble / GetDisplayName
    Affix/
      Affix.cs
      AffixKind.cs
      AffixRegistry.cs
      AffixConfig.cs                 // POCOs for affixes.json
      BaubleRoller.cs
      ScrambleNameGenerator.cs
    Modifier/
      ModifierEntry.cs
      ModifierOp.cs
      ModifierRegistry.cs            // canonical mod keys + EntityPlayer.Stats glue
    Inventory/
      InventoryBaubles.cs
      ItemSlotBauble.cs
    Entity/
      EntityBehaviorBaubles.cs
    Gui/
      GuiBaublesTab.cs               // ComposeBaublesTab(GuiComposer)
      GuiDialogScholarsLectern.cs
    Items/
      ItemBauble.cs
      ItemUnidentifiedRoller.cs      // creative debug roller
    Blocks/
      BlockScholarsLectern.cs
      BEScholarsLectern.cs
  assets/baubles/
    itemtypes/
      ring.json                       // base type, slotType=ring
      bracelet.json                   // base type, slotType=bracelet
      trinket.json                    // base type, slotType=trinket
      unidentified-roller.json        // creative debug item
    blocktypes/
      scholarslectern.json
    recipes/grid/
      unidentified-bauble.json        // grid recipe → unidentified roller
    config/
      affixes.json
      lectern.json                    // researchDurationSeconds, future knobs
    lang/en.json
    textures/
      item/ring.png
      item/bracelet.png
      item/trinket.png
      item/unidentified-roller.png
      block/scholarslectern.png
      gui/itemslotbg/ring.svg
      gui/itemslotbg/bracelet.svg
      gui/itemslotbg/trinket.svg
    patches/
      entityplayer-behaviors.json     // JSON-patch adds "baubles" behavior
```

## modinfo.json

```json
{
  "type": "code",
  "name": "Baubles",
  "modid": "baubles",
  "version": "0.1.0",
  "authors": ["chad"],
  "description": "Accessory slots, affix-rolled item names, and a research lectern.",
  "side": "Universal",
  "dependencies": { "game": "1.20.0" }
}
```

## Implementation Order

To keep each step verifiable, build the mod in this order. Each step ends
with a checkpoint that can be exercised in-game before moving on.

1. **Inventory + entity behavior + character tab.** Empty slots only. No
   items yet — verify the tab appears, slots exist, persist, and sync.
2. **Base `ItemBauble` + three base types (ring, bracelet, trinket).**
   No affixes; pure identified items. Verify slot type enforcement and
   equip/unequip events fire.
3. **Per-stack instance attributes + `BaubleRoller` + scramble.** Items
   roll affixes on creation; `GetHeldItemName` returns scrambled or
   assembled names. No effects yet.
4. **Modifier registry + `EntityPlayer.Stats` glue.** Equipping an
   identified bauble applies its mods; unequipping removes them.
5. **Affix JSON loader.** Move the pool out of code into
   `assets/baubles/config/affixes.json`.
6. **Scholar's Lectern block, BE, and GUI.** Decoding flips identified
   = true and re-fires the equip path if the bauble was already in a
   slot.
7. **`ItemUnidentifiedRoller` + grid recipe.** Survival-mode source.
8. **Tests + manual checklist + README updates.**

## GUI Layout (Baubles tab)

```
┌──────────────────────────────────┐
│  Baubles                         │
│                                  │
│   [Ring1]  [Ring2]               │
│                                  │
│   [Bracelet]  [Trinket]          │
│                                  │
│  Hover an empty slot to see what │
│  it accepts.                     │
└──────────────────────────────────┘
```

Built with `GuiComposerHelpers.AddItemSlotGrid` bound to the player's
`InventoryBaubles` (resolved from
`capi.World.Player.Entity.GetBehavior<EntityBehaviorBaubles>().Inventory`).
The grid is 2 columns × 2 rows. Each slot's `BackgroundIcon` shows the
allowed type.

## Public API Contract

External mods that want to participate:

```csharp
var baubles = api.ModLoader.GetModSystem<BaublesModSystem>().Api;

// React to equip/unequip — note: unidentified equips still fire the event
// with the same signature; check baubles.IsIdentified(stack) inside the
// handler if you only want to react to identified items.
baubles.OnBaubleEquipped   += (player, stack, slotType) => { /* ... */ };
baubles.OnBaubleUnequipped += (player, stack, slotType) => { /* ... */ };
baubles.OnBaubleIdentified += (player, stack)           => { /* ... */ };

// Promote a vanilla item via JSON patch to its attributes:
//   "bauble": { "slotType": "Ring" }
// or in code: implement IBaubleItem on your Item subclass.

// Add new affixes (in your ModSystem.AssetsFinalize):
baubles.Affixes.Register(new Affix {
    Code = "ironbound", Kind = AffixKind.Prefix,
    LangKey = "mymod:affix-prefix-ironbound", Weight = 5,
    Mods = new() { new ModifierEntry { Key = "meleeDamage", Value = 0.10, Op = ModifierOp.Mul } }
});

// Add new modifier keys (in ModSystem.Start):
baubles.Modifiers.Register("myCustomKey", (player, value, op, isAdd) => {
    // your stat application logic
});
```

## Testing Strategy

### Automated (xUnit-style, targeting the API surface only — does not run
the game loop)

1. `InventoryBauble_Size_IsFour`
2. `InventoryBauble_RingSlot_RejectsTrinketStack`
3. `InventoryBauble_TrinketSlot_AcceptsTrinketStack`
4. `InventoryBauble_RoundTrip_ToTreeAttributes_Restores_Slots`
5. `BaublesUtil_GetSlotType_FromIBaubleItem`
6. `BaublesUtil_GetSlotType_FromCollectibleAttributes`
7. `BaublesModSystem_Equip_Fires_OnBaubleEquipped`
8. `BaublesModSystem_Unequip_Fires_OnBaubleUnequipped`
9. `BaubleRoller_Roll_IsDeterministic_For_Seed`
10. `BaubleRoller_Roll_RespectsRollChances` (statistical, 10k samples)
11. `BaubleRoller_Roll_RespectsAffixWeights` (statistical)
12. `BaubleRoller_Roll_RespectsAllowedSlots`
13. `ScrambleNameGenerator_SameSeed_SameOutput`
14. `ScrambleNameGenerator_DifferentSeeds_DifferentOutputs` (sampled)
15. `BaublesUtil_GetDisplayName_Unidentified_ReturnsScrambled`
16. `BaublesUtil_GetDisplayName_Identified_AssemblesPrefixBaseSuffix`
17. `ModifierRegistry_Apply_Then_Remove_NetZero` (apply then remove a mod
    → final stat = original)
18. `EntityBehaviorBaubles_Identified_Equip_Applies_Mods`
19. `EntityBehaviorBaubles_Unidentified_Equip_Skips_Mods`
20. `AffixRegistry_LoadsFromJson_AndResolvesModifierKeys`

These tests stub `ICoreAPI` and `EntityPlayer` minimally; we are not running
the full VS server in-process.

### Manual playtest checklist (run before tagging 0.1.0)

- Singleplayer — slots and persistence:
  - [ ] Open character screen → "Baubles" tab is visible alongside
    Character and Traits.
  - [ ] Use creative roller → drop result into Ring 1 → it accepts.
  - [ ] Drop a ring into Bracelet slot → rejected.
  - [ ] Drop a trinket into Trinket slot → accepted.
  - [ ] Quit game → relaunch → load world → baubles still equipped, same
    prefix/suffix/identified state.
- Singleplayer — affixes and names:
  - [ ] Roll 10 unidentified baubles → each shows a different scrambled
    name; same seed re-rolled yields the same name.
  - [ ] Hover an unidentified bauble → tooltip shows "Unknown — research
    at a Scholar's Lectern" (or equivalent localised string).
  - [ ] Identify one at the lectern → name becomes "[Prefix] [Base] [of
    Suffix]" with affix names resolved from lang file.
  - [ ] Equip identified bauble → `EntityPlayer.Stats` shows the
    corresponding modifier with code `baubles:<modkey>:<stack-id>`.
  - [ ] Unequip identified bauble → modifier disappears from
    `EntityPlayer.Stats`.
- Singleplayer — Scholar's Lectern:
  - [ ] Place lectern → right-click opens GUI with 1 slot + progress bar.
  - [ ] Put unidentified bauble in → progress fills over 60s → bauble
    becomes identified, name resolves, can be picked up.
  - [ ] Put an already-identified bauble in → message "Already
    identified", no progress, slot returns immediately.
  - [ ] Take bauble mid-research → progress resets to 0 next time you
    deposit (no exploit re-pickup to skip time).
  - [ ] Save + reload world during research → progress persists.
- Multiplayer (host + one connected client):
  - [ ] Each player has their own bauble inventory; reconnects preserve
    state.
  - [ ] Client cannot interact with host's bauble inventory.
  - [ ] Both players see the same scrambled name for the same stack
    (seed-based determinism check).
  - [ ] Identifying on the server propagates: client tooltip flips from
    scrambled to assembled name without reconnect.

## Risks and Mitigations

1. **`PlayerJoin` timing for tab registration on the client.**
   `GuiDialogCharacterBase` may not be in `capi.Gui.LoadedGuis` until after
   `LevelFinalize`. Mitigation: register the tab in
   `capi.Event.LevelFinalize`, and if the dialog isn't there yet, defer to
   `capi.Event.RegisterCallback(_ => ..., 100)`. Survival's `CharacterSystem`
   adds it in `StartClientSide`, which works because the dialog is
   constructed unconditionally by core early on — verify during
   implementation that the same is true in 1.20.x.

2. **Behavior attachment via JSON patch must match all player entity
   files.** VS 1.20 ships seraph-male and seraph-female; future updates
   could add more. Mitigation: use a JSON-patch with a wildcard target
   selector matching all entity files whose `class` is `EntityPlayer`. If
   wildcard selectors aren't supported, list the known files and add an
   integration check on mod startup: if no player entity has the behavior,
   log a warning.

3. **Inventory not synced on first join.** New players never had a
   `baublesInv` tree attribute. Mitigation: `EntityBehaviorBaubles.Initialize`
   creates the inventory empty and writes a fresh tree attribute on first
   modification; the seraph behavior handles this the same way and works
   correctly.

4. **Duplicate tab registration on re-login.** If `StartClientSide` is
   re-entered (e.g., the player disconnects and reconnects to a different
   server in the same session), we'd add the tab twice. Mitigation: before
   adding, check `dlg.Tabs.Any(t => t.Name == Lang.Get("charactertab-baubles"))`.

5. **Inventory class name collision.** If another mod registers an inventory
   named `"baubles"`, we both lose. Low likelihood; we accept the risk and
   document the inventory class name in the README.

6. **Affix mod application across reload.** When a world is loaded, the
   player's bauble slots already contain identified items but their stat
   modifiers aren't on the player yet. Mitigation: re-apply all mods in
   `EntityBehaviorBaubles.Initialize` after `loadInv()` completes.

7. **Tooltip caching of `GetHeldItemName`.** VS caches tooltips; flipping
   from scrambled → identified should invalidate. Mitigation: after
   `BaublesAPI.Identify(stack)` mutates `stack.Attributes`, call
   `stack.Attributes.MarkPathDirty("bauble")` (or whatever the equivalent
   helper is in 1.20 — verify) and broadcast a stack change.

8. **Roller item cycling in inventory vs hand.** `ItemUnidentifiedRoller`
   uses right-click in hand to roll. If a player right-clicks it while
   the cursor is over a container, vanilla shift-right-click semantics
   take precedence. Acceptable — players need to either equip the roller
   or use the alternate cycle via shift+scroll. Implementation should
   match the existing pattern from `ItemIceRinkCreator` in the IceSkates
   mod.

9. **Lectern GUI sync.** The progress bar must read live state from the
   server. Mitigation: standard `BlockEntity.MarkDirty(redrawOnClient:
   true)` per tick already pushes updates. If perf becomes a problem,
   throttle to every 4th tick (~400ms).

10. **Determinism of `System.Random`.** Two clients with the same seed
    must produce the same scrambled name. `System.Random` constructed
    with an `int` seed is deterministic in .NET 8+; we explicitly cast
    the `long` seed via `(int)(seed ^ (seed >>> 32))`. The unscrambled
    affix roll uses the full `long` seed via the same construction —
    server is authoritative regardless, but cross-platform determinism
    keeps client-side preview rendering identical.

## Open Questions

None. (User requested no clarifying questions; defaults are documented in
Non-Goals and Components.)
