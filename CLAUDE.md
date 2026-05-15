# Baubles Mod — Project Instructions

## Karpathy-Inspired Claude Code Guidelines

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

### 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

### 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

### 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

### 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

---

## Build & Test
- **Build:** `dotnet build` (auto-deploys DLL + assets to `~/.config/VintagestoryData/Mods/Baubles/`)
- **Quick test:** Drop folder with `.cs` files into `Mods/` — Vintage Story compiles at runtime
- **SDK:** .NET 10, pinned via `global.json`. Use the system `dotnet` directly (no DOTNET_ROOT prefix).

## Project Structure
- `src/` — C# source files
- `assets/baubles/` — Item/block types, recipes, shapes, lang, affix config
- `docs/` — Detailed reference documentation
- `docs/superpowers/specs/` — Design specs (do not delete or reformat without owner approval)
- `.run/` — Rider run configs (gitignored)

## Key Source Files
(Populated during implementation. See `docs/superpowers/specs/2026-05-15-baubles-design.md`.)

## Conventions
- Target Vintage Story 1.20.x+
- Bauble affixes stored as tree attributes on itemstack (per-stack, not per-itemtype)
- Bauble inventory accessed via `EntityBehaviorBaubles.Inventory` on `EntityPlayer` — never look it up by index in the player's inventory list
- `EntityPlayer.Player` can be null; always fallback to `World.PlayerByUid()`
- Scrambled item names must be deterministic from `stack.Attributes.GetLong("baubleSeed")` — same seed → same gibberish across save/load and across clients
- Inventory class name `"baubles"` is reserved by this mod — document it in any compat guide

## Reference Material
- Decompiled VS sources live on `macgyver:~/workspace/vs-api-reference/` — grep these when looking up API surface (`GuiDialogCharacterBase`, `InventoryBasePlayer`, `EntityBehaviorSeraphInventory`, etc.).

## Documentation
- `README.md` is player-facing — keep it concise
- Detailed docs live in `docs/` — update them when adding features

## Pre-Approved Commands (safe to run without asking)

### Build
- `dotnet build`
- `dotnet clean`
- `dotnet restore`

### Git (read-only)
- `git status`, `git diff`, `git log`, `git branch`, `git show`
- `git stash list`

### Git (write — local only)
- `git add`, `git commit`, `git stash`, `git stash pop`
- `git checkout -b` (create branch)
- `git switch`, `git merge` (local branches)

### GitHub CLI (read-only)
- `gh pr list`, `gh pr view`, `gh pr status`, `gh pr checks`
- `gh issue list`, `gh issue view`
- `gh repo view`
- `gh run list`, `gh run view`
- `gh api` (GET requests)

### GitHub CLI (write — ask first for destructive ops)
- `gh pr create`, `gh pr comment`
- `gh issue create`, `gh issue comment`

### Filesystem
- `ls`, `mkdir -p`
- `wc`, `file`, `stat`

### Always ask before running
- `git push`, `git push --force`, `git reset`, `rm -rf`
- `gh pr close`, `gh pr merge`, `gh issue close`
- Any command that modifies remote state or deletes files
