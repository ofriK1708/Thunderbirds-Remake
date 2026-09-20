# Collaboration Rules — Thunderbirds: Heavy Lift

Rules for **both teammates and any AI assistant** working in this repo (Claude Code, Copilot, Rider AI, …).
They exist because Unity projects break in specific, expensive ways when two people work in parallel.

Most of these are checked automatically — see [Running the checks](#running-the-checks).
`[CHECKED]` = the script fails your PR. `[WARNED]` = the script warns but lets it through.

---

## 1. Ownership

| Thing | Owner | Why |
|---|---|---|
| `Assets/Scenes/Game.unity` | **Ofri** (`ofriK1708`) | Scene files cannot be merged by git |
| `Assets/Scenes/MainMenu.unity` | **Rotem** (`rotem444`) | Same |
| `ProjectSettings/*` | Whoever is changing it — **announce it first** | Merge conflicts in YAML settings are silent and nasty |
| Everything else (`.cs`, `.prefab`, `LevelData`, docs) | Shared | Merges fine |

**R1 [CHECKED] — Never edit a scene you don't own.**
A `.unity` file is one long list of objects with numeric IDs. Two people editing it on two branches produce a file
git "merges" into something Unity can't open, or that silently loses objects. There is no fix except redoing the work.

**R2 [CHECKED] — Need something in the other person's scene? Ship a prefab.**
Build your UI panel or object as its own `.prefab` file (prefabs merge fine, and only one line changes in the scene).
The scene owner drags it in. Example: `HowToPlayPanel.prefab` is built by Ofri (#20) and wired into `MainMenu.unity` by Rotem (#19).

**R3 [CHECKED] — Don't edit a scene that `main` has also changed since you branched.**
If both your branch and `main` touched the same scene, stop and rebase *before* the PR, not after.

---

## 2. Architecture rules from the GDD

**R4 [CHECKED] — `Assets/Scripts/Rules/` must never reference Unity.**
No `using UnityEngine`, no MonoBehaviour, no `Time.deltaTime` (time arrives as a `Tick(dt)` argument).
This is the rule that makes the rules layer unit-testable in milliseconds and lets both of us work in parallel.
Need maths? `System.Math`, or a small helper of our own.

**R5 [CHECKED] — Gameplay code never reads a key directly.**
No `Keyboard.current`, `Input.GetKey`, `Input.GetAxis`. Input arrives through the Input System action map
(`Move`, `SwitchShip`, `Restart`, `Pause`), which is what makes gamepad and later Android work for free.

**R6 [CHECKED] — Only `GameManager` and `AudioManager` are singletons.**
Rules classes are created with `new` so every test and every restart starts clean.

**R7 — Views never contain rules.**
A view may read simulation state and listen to simulation events. If you find yourself deciding *whether a move is legal*
inside a `*View`, it belongs in the rules layer.

---

## 3. Repo hygiene

**R8 [CHECKED] — Every asset is committed together with its `.meta` file.**
A missing `.meta` makes Unity generate a new GUID on the other machine, which silently breaks every reference to that asset.
No orphan `.meta` files either (a `.meta` whose asset was deleted).

**R9 [CHECKED] — Never commit generated or downloaded content.**
`Library/`, `Temp/`, `Logs/`, `Build/`, `.idea/`, asset-pack zips. Copy only the sprites we actually use into
`Assets/Art/`, together with the pack's licence file.

**R10 [CHECKED] — Images go through Git LFS.** `.gitattributes` already routes `*.png` / `*.jpg`; don't bypass it.

**R11 [CHECKED] — Unity serialization settings stay put:** Asset Serialization = **Force Text**,
Version Control = **Visible Meta Files**. Anything else makes scenes and prefabs unreadable to git.

---

## 4. Workflow

**R12 — One issue, one branch, one PR.** Branch name: `feature/<issue-number>-short-slug`, e.g. `feature/7-push-chains`.

**R13 — The other person reviews every PR.** This is not ceremony: the lecturer can ask either of us about any file,
and reviewing is how you learn the half you didn't write.

**R14 [WARNED] — Change a rule, update the GDD in the same PR** (CLAUDE.md rule 1). The GDD is the graded document.

**R15 [WARNED] — Bump `bundleVersion` after a tested feature** (CLAUDE.md rule 2).

**R16 — Tests before the PR.** Edit Mode tests must pass. Every rules-layer issue ships with tests; a rules PR with no
test changes needs a reason in the PR description.

**R17 — Never force-push `main`,** and never `git push --force` a branch someone else has reviewed.

---

## 5. Rules for AI assistants

An AI assistant working in this repo must:

1. **Read `docs/GDD.md` before changing gameplay code**, and follow it rather than inventing rules. If the code
   and the GDD disagree, say so instead of picking one.
2. **Obey R1–R11 exactly.** They are mechanical and non-negotiable. In particular: never edit a scene file, never add
   `using UnityEngine` to the rules layer, never add a singleton.
3. **Ask before**: deleting assets, changing `ProjectSettings/`, changing `.gitattributes` or `.gitignore`,
   rewriting git history, or force-pushing.
4. **Run `tools/check-rules.ps1` before proposing a commit** and report what it printed.
5. **Keep changes scoped to one issue.** Spotted something else? Mention it; don't fix it in the same PR.
6. **Never commit or push unless asked**, and never open a PR straight to `main` without the owner's go-ahead.
7. **State uncertainty plainly.** A wrong confident answer costs more than a question, especially with one week left.

---

## Running the checks

```powershell
pwsh ./tools/check-rules.ps1
```

Useful options:

```powershell
pwsh ./tools/check-rules.ps1 -Base origin/main   # compare against a different branch
pwsh ./tools/check-rules.ps1 -Actor rotem444     # check as if you were the other teammate
```

It prints one line per rule and exits non-zero if anything failed. The same script runs automatically on every
pull request to `main` (`.github/workflows/rules-check.yml`), so a violation shows up on the PR before review.

**If a check fails and you believe the rule is wrong**, don't work around it: change the rule here, in a PR, with the
reason. That's how a two-person project keeps rules it actually follows.
