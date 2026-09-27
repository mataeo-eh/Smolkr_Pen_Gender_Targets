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

## The rule

Implemented and unit-tested in `src/SmolkrPenGenderTargets.Core`.

- The player sets **females to keep** per pen. **Males to keep = slaughter
  limit - females to keep**, so the targets can never disagree with the game's
  own limit.
- A gender whose adult count is over its target has a **surplus**. An adult of
  a surplus gender is slaughtered. When both genders are over target, the larger
  surplus goes first, and ties go to males (vanilla's tie-break).
- **Pups are ignored by default** and judged once grown. Counting them
  (config `CountPupsTowardTargets`) is closer to vanilla's limit, but a newborn
  male then gets the herd's only adult male culled while the pup grows up.
- If the only surplus is pups, **nothing** is slaughtered. Killing an adult of
  the other gender would move the pen further from the requested ratio.
- The pen's `minimumPetPopulation` floor is respected, as in vanilla.
- Within the chosen gender, the **last** adult in the pen's list goes, as in
  vanilla. Turning targets on changes which gender dies and nothing else.

With adults-only counting, a surplus can only exist when adults outnumber the
limit, which means the pen is over the limit. Vanilla's "pen is over the limit"
precondition still holds automatically.

## Saving

`AnimalPen.SerializeTaskData` returns the object that holds the pen's task
settings (vanilla writes the limit into it as `tdCount`).
`DeserializeTaskData` receives the same object on load. The mod adds one integer
key, `spgt_femalesToKeep`, only for pens with targets on. Vanilla reads its keys
by name, so the extra key is harmless if the mod is removed: the pen reverts to
vanilla.

## The panel

`WorkstationTaskPanel.SetPanel` / `RefreshPanel` postfixes note when the panel
is showing an `AnimalSlaughterTaskData` (whose `SmolkrPen` names the pen). An
IMGUI window is drawn beside it while that panel stays active and on that task.
IMGUI was chosen over injecting a row into ASKA's task panel for the same reason
AskaEnemyScaling chose it: it cannot be broken by a change to the game's UI
prefabs, and it can be built without running the game.

## Known limits

- **Multiplayer:** villager AI runs on the host, and the targets live in the
  host's save. A client's panel changes only the client's copy.
- **Kyckling pens** are also `AnimalPen`s. The mod treats them identically. If
  kycklings carry no `DomesticSmolkr` component, both vanilla and the mod
  slaughter nothing there, so turning targets on cannot make it worse.
