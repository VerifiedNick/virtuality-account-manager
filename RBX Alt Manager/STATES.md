# Virtuality Manager – state machines

Every place where the app is "in a state" and moves between states. If you change behavior, change it here first.
Log prefixes in `log.txt` (next to the exe) are given so you can follow each machine at runtime.

## 1. Account session — `Classes/AccountSession.cs`  · log `[Status]`
Saved in `AccountData.json` as `Fields["BanStatus"]` (absent = Active) – same strings as the original RAM.

| From \ Source | Login | SessionCheck | BanCheck |
|---|---|---|---|
| Active | – | → NeedsLogin / TempBanned | → Banned |
| NeedsLogin | → Active | → Active / TempBanned | → Banned |
| TempBanned | – (only a session check ends it) | → Active / NeedsLogin | → Banned |
| Banned | – (sticky) | – (sticky) | → Active (not banned any more) |

Only `AccountSession.Set / LoggedIn` may write the field. `LoggedIn` (login window) also stamps `SessionChecked`; a cookie import (`AddAccount`) does not, so the quiet check still verifies it. Stamps: `SessionChecked`, `BanChecked`, `EmailChecked` via `AccountSession.Stamp`.
UI mapping (`AccountRowRenderer.StateOf`): Banned/TempBanned → red, NeedsLogin → orange, else presence (InGame → green, Online, Offline).

## 2. Background jobs — `Classes/Activities.cs`  · log `[Activity]`
Idle (empty list) ⇄ running jobs. Kinds: `Login` (one login window, also blocks a second one), `LoginQueue` (group login),
`BanCheck`, `FriendRequests`, `Joining`. Rule: `Begin` → `Update`* → `End` in a `finally`.
The header shows the newest job (blinking square) on every page; clicking there offers "Stop" via the job's CancellationTokenSource.

## 3. Group login queue — `AccountManager.Modern.cs` `LoginGroupQueue`
Hidden (no logged-out accounts in the open group) → "Log in group (n)" → per account: login window → pause (`LoginQueueDelay`, default 20 s) → next → summary.
Second click = stop (cancels after the current window).

## 4. Joining / same server — `LaunchAccounts`, `ServerPicker`  · log `[SameServer]`
Job ID empty + several accounts: pick a server with room for all + 2 (`ServerPicker.PickFor`) → all join it.
Before each further account: server full? → pick a new one for the rest. Fallback: first account joins, `WaitForServer` reads its server.
The server is written into the Job ID field (`ShowSameServer`), so later single joins use it. Double-join guard: `GuardDoubleJoin` (in game / started < 2 min).

## 5. Lock screen — `AccountManager.Pages.cs` `PinField`
`Idle → Checking → Wrong` (shake + reason) or the panel closes → "Unlocked" overlay → accounts.
The decryption itself stays in the original `UnlockButton_Click` / `LoadAccounts`; the PIN screen only drives it.

## 6. Pages — `AccountManager.Pages.cs`
`CurrentPage` = null (accounts) | settings | servers | recent | tools | control. One page at a time; header icon of the open page is `Active`; Esc / "← Accounts" closes.

## 7. Multi Roblox — `EnsureMultiRoblox` / `UpdateMultiRoblox`  · log `[MultiRoblox]`
Not owned → (Roblox open & `CloseRobloxForMulti`) close Roblox → retry up to 10 s → owned. A failed attempt releases the handle so the next try works.

## 8. Data folder — `Program.SetupDataFolder`, `Backups`  · log `[Backup]`
Flat folder (AccountData.json next to the exe) → data there. Otherwise `Daten\`; `migrate-from.txt` → copy once (never overwrite).
Every start: copy of AccountData.json to `Daten\Backups` if it changed, newest 10 kept; restore = "Before restore …" copy + restart (`SavingBlocked`).
