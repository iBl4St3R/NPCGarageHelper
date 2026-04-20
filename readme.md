# NPC Garage Helper

A MelonLoader mod for **Car Mechanic Simulator 2026** that adds a hireable NPC worker to your garage.  
The NPC automatically repairs parts from an input storage and moves them to an output storage — while visually patrolling between workstations.

---

## Features

- **Hire / Fire NPC** — spawn a worker cloned from the player model
- **Automatic repair** — fetches parts from INPUT storage, repairs them, deposits to OUTPUT storage
- **Real repair mechanic** — sets `Condition = 255` and `Dent = 0` on each item via IL2CPP cast
- **Failure system** — repair can fail based on skill level and item damage; failed items are destroyed
- **Skill slider** — affects repair speed, success chance, and damage threshold
- **4-point patrol** — NPC walks between RepairTable → UpgradeTable → InputStorage → OutputStorage while working
- **Work hours** — NPC is active 08:00–16:00 (game time); model hidden outside hours
- **Daily wage** — 600 CR deducted from player account each in-game day
- **E-key interaction** — open panel by pressing E while looking at the UpgradeTable or the NPC
- **SimpleConsole commands** — `ngh_open`, `ngh_scan`, `ngh_status`

---

## Requirements

| Dependency | Version |
|---|---|
| MelonLoader | 0.7.2+ |
| _CMS2026_UITK_Framework | 0.2.1+ |
| CMS2026 Simple Console | 1.3.0+ (optional) |

---

## Installation

1. Install [MelonLoader](https://melonwiki.xyz/) for CMS2026
2. Place `_CMS2026_UITK_Framework.dll` in `Mods/`
3. Place `NPCGarageHelper.dll` in `Mods/`
4. Launch the game

---

## Setup in-game

1. Place **two storage units** within **10 meters** of the RepairTable (one INPUT, one OUTPUT)
2. Make sure the **UpgradeTable** is within **10 meters** of the RepairTable
3. Open the panel — press **E** while looking at the UpgradeTable, or use `ngh_open` in console
4. Click **⟳ Scan** to detect storages and tables
5. Add funds using **+500** or **+2000** buttons (deducted from player account)
6. Click **✓ Zatrudnij** to hire the NPC

---

## Project structure