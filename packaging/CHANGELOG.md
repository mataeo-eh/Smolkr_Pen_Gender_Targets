# Changelog

## 0.2.0

- The target is now a **Females to keep** row in the pen's own slaughter task,
  directly under the slaughter limit, replacing the separate window. The window
  was attached to task UI the game no longer uses, so in 0.1.0 it never appeared.
- Slaughter timing now matches vanilla exactly: pups count toward the slaughter
  limit, as they do in the base game. Only the choice of gender changes. The
  `CountPupsTowardTargets` option is gone.
- New options `ShowRow`, `RowOffsetX` and `RowOffsetY`; `ShowPanel` is gone.

## 0.1.0

- Per-pen gender targets: set females to keep; males to keep is the slaughter
  limit minus that.
- Targets are saved with the pen.
