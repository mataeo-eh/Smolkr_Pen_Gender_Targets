# Design: how the smolkr pen chooses what to slaughter

This records how ASKA's pen slaughtering actually works, so the patch point is
not a guess. Everything below comes from disassembling the game build of
2026-09-21 (`GameAssembly.dll`), with call targets and metadata references
resolved to names through Cpp2IL. The interop assemblies only contain method
signatures, not their bodies, so this was the only way to see the logic.

If a game update changes any of the methods named here, re-check this document
before trusting the mod.

## The chain, from villager to dead smolkr

```text
Villager working the pen
  -> SmolkrPenSlaughterQuest.GetPriority        "should I run the slaughter quest?"
  -> FSM_Slaughter.OnStateUpdate, START stage   "which smolkr?"
       -> AnimalPen.GetSmolkrReadyForSlaughter(out DomesticSmolkr)
            false -> attempt ends, nothing killed
            true  -> tool / carry checks, then chase and kill that smolkr
```

### `SmolkrPenSlaughterQuest.GetPriority` never looks at the animals

It returns zero unless the quest data reports `IsWorking` (vtable slot 10 of
`WorkstationQuest.WorkstationQuestData`), the quest is not in status `0x10`, and
one of the pen's containers is not full. Otherwise it returns the quest's work
priority plus a constant. Pet counts, the slaughter limit and genders do not
appear. So the quest is offered whenever a villager is working the pen, and
the decision to kill anything is made later.

### `AnimalPen.GetSmolkrReadyForSlaughter` is the whole decision

It is called from exactly one place, the START stage of
`FSM_Slaughter.OnStateUpdate`, and nothing else in the binary makes either the
"whether" or the "which" choice. Reconstructed:

```csharp
bool GetSmolkrReadyForSlaughter(out DomesticSmolkr result)
{
    result = null;
    int count = _ownedPets.Count;                          // pups included
    if (count < _slaughterTaskData.itemInfoQuantity.quantity + 1) return false;
    if (count < minimumPetPopulation) return false;

    int females = 0, males = 0;
    DomesticSmolkr lastFemale = null, lastMale = null;
    foreach (var wrapper in _ownedPets)
    {
        var pet = wrapper.pet;
        if (pet == null || pet.gameObject == null) continue;
        if (!pet._mature) continue;                        // adults only
        if (!pet.TryGetComponent(out DomesticSmolkr s)) continue;
        if (s.isFemale) { females++; lastFemale = s; }
        else            { males++;   lastMale   = s; }
    }
    result = females <= males ? lastMale : lastFemale;     // majority gender; tie -> male
    return result != null;
}
```

In words: once the pen holds more animals than the slaughter limit, vanilla
slaughters the last adult of whichever gender currently has more adults. That
pulls every pen toward a 50/50 split, and the game has no notion of a target
ratio anywhere. A pen that ends up all one gender most likely gets there because
the limit counts pups but only adults can be culled. A litter of one gender
pushes the pen over the limit, and the majority adults are removed to make room
for pups that later grow up the same gender. That is an inference; the
disassembly only shows that the selection itself always takes the majority.

Relevant layout (for anyone re-checking the disassembly):

| Type | Offset | Field |
| --- | --- | --- |
| `Kennel` | `0x260` | `List<PetWrapper> _ownedPets` |
| `Kennel.PetWrapper` | `0x10` | `Pet pet` |
| `AnimalPen` | `0x278` | `int minimumPetPopulation` |
| `AnimalPen` | `0x308` | `AnimalSlaughterTaskData _slaughterTaskData` |
| `WorkstationTaskData` | `0x10` | `ItemInfoQuantity itemInfoQuantity` (the limit) |
| `Pet` | `0x56A` | `bool _mature` |
| `DomesticSmolkr` | `0x20` | `bool isFemale` |

## The patch

A Harmony prefix on `AnimalPen.GetSmolkrReadyForSlaughter`:

- **Pen without gender targets, or mod disabled:** returns `true`, and vanilla
  runs untouched.
- **Pen with gender targets:** reads the pen the same way vanilla does
  (`Runtime/PenReader`), asks `Core/CullPlanner` for a victim, writes it to the
  `out` parameter, and skips vanilla.
- **Any exception:** logged once, then vanilla runs.

Because Harmony on IL2CPP detours the *native* function, the native call from
`FSM_Slaughter` reaches the patch. No inlined copy of the selection logic
exists elsewhere: the only other methods that touch
`TryGetComponent<DomesticSmolkr>` are the milking, breeding and smilk code.

The `out` parameter has to be copied back into native memory by
Il2CppInterop's trampoline. Il2CppInterop 1.5.3 (the version in the profile)
generates that write-back, and `Patches/SmolkrDeathPatch` exists to prove it in
game. See the in-game test.

## Pups: what vanilla does

Checked with `scripts/disasm.ps1` (a `G` query for every method that edits
`_ownedPets`, plus `DomesticSmolkr.Breed` and `Kennel.PetWrapper._OnPetFullyGrown`):

- **A pup joins `_ownedPets` at birth.** `DomesticSmolkr.Breed` picks a random
  entry from the pen's `tameableCreatures` (so a pup's gender is random), adds it
  as an item to the pen's container, and `Kennel._AddDogBox` appends it to
  `_ownedPets`.
- **Adults stay in the list.** `_OnPetFullyGrown` only removes a grown animal
  when the kennel's `releasePetsWhenFullyGrown` flag is set, which is the dog
  kennel's behaviour, not the pen's.

So vanilla's trigger, `_ownedPets.Count > slaughter limit`, **counts pups**. A
pup can never be the victim, and the gender comparison counts adults only.

## The rule

Implemented in `src/SmolkrPenGenderTargets.Core/CullPlanner.cs`. It is
vanilla's method with one step changed:

| Step | Vanilla | This mod |
| --- | --- | --- |
| When | pen population (pups included) > limit, and >= minimum population | identical |
| Who is eligible | adults only | identical |
| Which gender | the one with more adults; tie -> male | the one furthest above its target (adults - adults wanted); tie -> male |
| Which individual | last adult of that gender in `_ownedPets` | identical |

The player sets **females to keep** per pen. **Males to keep = slaughter limit -
females to keep**, so the targets can never disagree with the game's own limit.

The unit tests prove the "identical" rows against `VanillaReference`, a line-by-
line transcription of the decompiled method. On 20,000 random pens, even-split
targets pick the **same individual** as vanilla. For any targets, *whether* a
slaughter happens always matches vanilla. The only effect of the targets is the
ratio the pen settles at.

A consequence worth knowing, inherited from vanilla: a pen sitting exactly at
its limit is never thinned, even if its ratio is off. The ratio corrects itself
as births push the pen over the limit. A newborn also counts, so with a herd
exactly on target (say 5F/1M at limit 6), a birth puts the pen over with both
genders at surplus 0. Vanilla's tie-break then takes the adult **male**, and the
herd goes without a breeding male until a male pup grows up.

## Saving

`AnimalPen.SerializeTaskData` returns the object that holds the pen's task
settings (vanilla writes the limit into it as `tdCount`).
`DeserializeTaskData` receives the same object on load. The mod adds one integer
key, `spgt_femalesToKeep`, only for pens with targets on. Vanilla reads its keys
by name, so the extra key is harmless if the mod is removed: the pen reverts to
vanilla.

## The "Females to keep" row

**Which panel.** The workstation menu edits tasks in `SSSGame.UI.TaskDataPanel`,
driven by `WorkstationMenu.CreateTaskDataPanel` and `CreateOrEditTaskPage.Show`.
(0.1.0 hooked `WorkstationTaskPanel`; an `X` query shows nothing in the game
calls it any more, so its IMGUI window could never appear.)

**How the slaughter limit is drawn.** `TaskDataPanel._SetQuantity`, for an
`AnimalSlaughterTaskData`, fills the panel's `_quantityDiv`: it sets the
`_quantityControlPanel` (an `IncreaseDecreasePanel`) range from
`GetQuantityRange()`, sets its value to the limit, and sets `_quantityDivTitle` to
`Loc("misc.smolkrPen_SlaughterTaskName")`.

**The clone.** A postfix on `_SetQuantity` clones `_quantityDiv` into a second
row. What makes the clone safe:

- `TaskDataPanel.Start` attaches `_onQuantityChanged` to the limit control with
  `AddListener`, a *runtime* listener. `Instantiate` does not copy runtime
  listeners, so the clone cannot move the slaughter limit. Serialized listeners
  on the clone are switched off as well.
- `IncreaseDecreasePanel.Awake` registers the control with its
  `IncreaseDecreaseGroup`, and `set_Value` updates that group. The clone is built
  under an inactive holder and its `group` is cleared before `Awake` runs.
- `IncreaseDecreasePanel.Setup` clears and re-adds its own button listeners, so
  the clone's buttons drive the clone.
- `SetValue` invokes listeners, *then* calls `RefreshAutomaticText`, which rewrites
  the number. A postfix on `RefreshAutomaticText` writes "Off" for the row's -1.

**Placement.** The prefab layout is not in the binary. `CreateOrEditTaskPage`
keeps a `_quantityDivInitialPos` and moves the div for markets, which suggests
absolute positioning. So the row goes directly after the div in sibling order,
and if the parent has no `LayoutGroup` it is also positioned one row-height
below. The first build logs the parent's children (`SPGT event=row_layout`), and
`RowOffsetX/Y` in the config adjust the position without a rebuild.

## Known limits

- **Multiplayer:** villager AI runs on the host, and the targets live in the
  host's save. A client's row changes only the client's copy.
- **Kyckling pens** are also `AnimalPen`s. The mod treats them identically. If
  kycklings carry no `DomesticSmolkr` component, both vanilla and the mod
  slaughter nothing there, so turning targets on cannot make it worse.
- **The row's text is English.** The game's own strings are localized; this one
  is not.
