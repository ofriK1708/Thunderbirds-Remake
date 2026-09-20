# Project Overview
Unity game development project for university final assignment.
Tech Stack: Unity, C#

# Context & Mind Map
Do not read all documentation at once. Reference `docs/mindmap.md` to find the exact file you need for your current task.

# Hard Rules & Workflow
1. **GDD Synchronization:** Before creating any git commit, you MUST review `docs/GDD.md`. If the new code alters gameplay mechanics, entity logic, or architecture, update the GDD to reflect the change.
2. **Version Control:** After successfully testing a new feature, bump the version number in `ProjectSettings/ProjectSettings.asset` (or your chosen version file) before committing.
3. **Collaboration Rules:** This is a two-person project. `docs/COLLABORATION.md` is binding for humans and AI alike — scene ownership, the Unity-free rules layer, no direct key reads, no stray singletons, meta files, LFS. Run `pwsh tools/check-rules.ps1` before proposing a commit and report the result. Never edit a scene file you do not own.
4. **Unity Best Practices:** Prioritize design patterns like object pooling for spawned entities. If you are unsure how these are implemented in this course, check the syllabus mapping in `docs/mindmap.md` to find the correct lecture file (e.g., Lecture 3 for Instantiation, Lecture 6 for Design Patterns) and read it before writing the code. Strictly separate logic from MonoBehaviours where possible.