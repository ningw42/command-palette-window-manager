# Agent guidance

## Design

- Keep Command Palette commands/pages thin; put targeting, geometry, and native operations in `WindowManager.Windowing`.
- Target the last-active application. Exclude palette popups and their transient UI, while allowing PowerToys Settings and Command Palette Settings—even when they share the palette's process.
- Fail closed on unknown or stale targets. Synchronize foreground observations before capturing a target and revalidate its identity before mutation; z-order is not activation history.
- Calculate placement in physical pixels using the target monitor's work area. Account for DWM visible-frame bounds and keep workspace coordinates separate. Preserve focus and z-order unless a command explicitly changes them.
- Keep target history in memory, bound cross-process waits, and release foreground hooks during extension disposal.

For targeting or lifecycle changes, read `ForegroundWindowMonitor.cs`, `WindowController.cs`, and `WindowPolicy.cs` in `WindowManager.Windowing`, plus their tests, before editing.

## Validation

For implementation changes, run:

```powershell
dotnet test WindowManager.Tests/WindowManager.Tests.csproj -p:Platform=x64
dotnet build WindowManager.sln -c Debug -p:Platform=x64
dotnet build WindowManager.sln -c Release -p:Platform=x64
```

For interop or project-configuration changes, also build Debug with `-p:Platform=ARM64`. After adding projects, retain only x64/ARM64 configurations; `dotnet sln add` can reintroduce x86/Any CPU.

Use fakes for placement tests; native smoke tests must leave the user's windows unchanged. Obtain approval for deployment or live window manipulation. Report build warnings and untested runtime behavior explicitly.

## Development workflow

- Building does not register the extension. Testing in Command Palette requires **Deploy** with the packaged profile, then **Reload Command Palette extensions**. After reload, focus a target application once before invoking a window command.
- Verify SDK changes against the pinned packages and current Microsoft documentation; official template examples can be stale. Preserve COM identity consistency, full-trust registration, and PerMonitorV2 awareness.
- Keep `README.md` title-only.

## Commit messages

Every commit message must follow **Conventional Commits**: `type(scope): summary`, with an optional scope and a concise imperative summary. For example: `feat(windowing): add centered resizing` or `docs: add agent guidance`.
