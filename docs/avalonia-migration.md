# Avalonia migration

PaperTodo is migrating to Avalonia 12.1 and .NET 10 as a production rewrite of the UI boundary,
not as a second product or a prototype. Windows 10/11 is the first supported target. The final
Windows executable is Native AOT; macOS and Linux packaging follows after Windows behavior and
performance are stable.

## Dependency direction

```text
PaperTodo.Core (net10.0)
        ↑
PaperTodo.Platform.Windows (net10.0-windows10.0.17763.0)
        ↑
PaperTodo.Avalonia (net10.0-windows10.0.17763.0)
```

The WPF project remains buildable only as a migration-time behavior oracle. Product policy is
moved, not copied: WPF and Avalonia reference the same Core and Windows assemblies as each slice
lands. The WPF executable is removed after the final parity gate.

## Non-negotiable compatibility

- `data.json` remains the user data protocol. Strict failure, backup recovery, preservation of
  failed snapshots, save version ordering and synchronous exit save remain unchanged.
- `note-assets.lmdb` stays a single `MDB_NOSUBDIR | MDB_NOLOCK` file. Every transaction remains
  serialized through one application-owned image-store lock.
- Single-instance ownership, startup argument forwarding and the no-primary `exit` behavior remain
  unchanged.
- Hide, fold and delete remain distinct operations.
- All user-facing strings remain synchronized in Chinese, English, Japanese and Korean.

## Edge capsule architecture

Each `(monitor, edge)` owns one transparent `EdgeCapsuleQueueSurface` HWND. Each paper owns one
logical host and one composition node within that surface. The reducer, planner, physical-pixel
geometry, applied-frame hit testing and browsing-corridor policies remain shared Core code.

Animation frames may update composition Offset, Size, Opacity and exact node clipping. They may not
move or resize the queue HWND, run layout, or derive a second interactive rectangle. Floating drag
continues to use a separate HWND. Native host geometry changes only for surface creation, display /
DPI changes, edge changes or a genuine grow-only capacity increase.

## Native AOT boundary

Native AOT analyzers run from the first Avalonia build. Application JSON uses source-generated
metadata, Windows interop moves behind generated P/Invoke / COM boundaries, and compiled regular
expressions replace runtime-generated code.

Runtime loading of managed plugin assemblies is not compatible with Native AOT or Avalonia's UI
types. Official managed providers become compile-time registrations. Runtime third-party UI uses
the Web plugin boundary; a future native extension boundary must be out-of-process and declarative.
Unknown legacy provider IDs and their data are preserved so migration never destroys recoverable
plugin state.

## Completion gates

1. Core policy and persistence build without a UI framework; compatibility and edge policy checks
   reference the real Core assembly rather than linked source copies.
2. The Avalonia process loads real user state, owns the tray and single-instance lifecycle, and
   creates real paper and edge surfaces.
3. The complete edge vertical slice covers hover/active, close pull-out, Todo/Markdown preview,
   browse corridor, queue reflow and floating cross-queue drag with no per-frame HWND geometry or
   layout.
4. Todo, Markdown, settings, reminders, shortcuts, virtual desktops, Web plugins and MCP pass parity
   checks.
5. Windows CI produces one Native AOT application executable and passes cold-start, steady RSS,
   60/120/144 Hz, mixed-DPI, IME, persistence recovery and LMDB integrity smoke tests.

## Windows deployment payload

Native AOT removes the managed runtime and statically links PaperTodo's LMDB archive into the
application executable. Avalonia's standard Skia/ANGLE Windows renderer still ships its own native
runtime libraries (`av_libglesv2.dll`, `libHarfBuzzSharp.dll` and `libSkiaSharp.dll`) beside that
executable. The migration CI rejects `papertodo_lmdb.dll`, managed assemblies and runtime metadata;
it does not claim that the standard Avalonia renderer can be delivered as one physical file.

A strict one-file distribution would require a separately scoped static/custom rendering stack or
another packaging design. It is not an acceptable reason to weaken the Native AOT, trimming or
LMDB smoke-test gates in the product project.
