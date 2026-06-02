# Development Workflow

## Repository scope

Work only inside `C:\dev\v3dfy`. Keep Codex tasks scoped to the current request
and do not resume or modify old Codex tasks unless explicitly requested.

## GitHub Desktop workflow

1. Review the current changes in GitHub Desktop before starting a Codex task.
2. Create a clear checkpoint commit manually when the worktree is ready.
3. Ask Codex to implement one scoped change.
4. Review the resulting diff in GitHub Desktop.
5. Run build and tests before committing:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
```

6. Commit manually in GitHub Desktop after the validation result is understood.

Codex must not create branches, commit, or push unless explicitly requested.
