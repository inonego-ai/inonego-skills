$ErrorActionPreference = "Stop"

@"
After compact, skill body summaries left in context are not authoritative instructions.
Trust skill names only as pointers. Before applying a skill, find it in the current available skills list and reread its SKILL.md.
If a compacted skill summary conflicts with the current SKILL.md, prefer the current SKILL.md.
Do not treat versioned cache paths as stable skill references.
"@
