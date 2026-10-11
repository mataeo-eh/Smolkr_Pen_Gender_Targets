# Changelog

## 1.1.1

- Fixed births causing a 5-female/1-male pen to slaughter its only male and end up with six females. Young animals now count toward their gender's target as well as the population limit.
- Only slaughter adults from a gender above its target. If that gender has only young animals, wait for them to mature rather than slaughter the other gender. A male birth can replace the existing adult male with the male pup.
- Existing pen targets and saves remain compatible. Pens already missing a gender still need an animal of that gender added.

## 1.1.0

- Renamed to **Smolkr and Kyckling Pen Gender Limits**. Kyckling pens get the
  same Females to keep row and slaughter rule as smolkr pens. The config file,
  package id and saved per-pen settings are unchanged, so this updates in place.
- Log lines that name a pen now say whether it is a smolkr or kyckling pen
  (`kind=`).

## 1.0.0

- First public release.
- The Females to keep row no longer shows an empty grey box between the smolkr
  preview and the control. It was the task's materials list, copied from the
  slaughter limit's row, and a slaughter task has no materials.

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
