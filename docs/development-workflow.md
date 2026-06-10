# Development Workflow

## Repository scope

Work only inside this repository checkout. In the current development
environment that checkout is `C:\dev\v3dfy`, but that path is a development
example only and must not appear in production runtime logic. Keep
implementation tasks scoped to the current request and do not resume or modify
unrelated previous work unless explicitly requested.

## GitHub Desktop workflow

1. Review the current changes in GitHub Desktop before starting an implementation
   task.
2. Create a clear checkpoint commit manually when the worktree is ready.
3. Implement one scoped change.
4. Review the resulting diff in GitHub Desktop.
5. Run build and tests before committing:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
```

6. Commit manually in GitHub Desktop after the validation result is understood.

Do not create branches, commit, or push from automation or scripts unless
explicitly requested.
