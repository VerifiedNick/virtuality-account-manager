# Virtuality Account Manager

A Roblox account manager by Nick: all your accounts in one window, one-click login, several Roblox windows at once, groups with their own picture, optional "same server" joining.

Credits and thanks to **ic3w0lf22**, the original founder: this is based on the [Roblox Account Manager by ic3w0lf22](https://github.com/ic3w0lf22/Roblox-Account-Manager) (GPL-3.0, see `LICENSE` and `README-Original.md`), completely redesigned.

## ⬇️ Download

**[» Download Virtuality Account Manager «](https://github.com/VerifiedNick/virtuality-manager/raw/main/Download/Virtuality%20Manager.exe)**

Click the link above and the finished app (`Virtuality Manager.exe`) downloads. You can also open the `Download/` folder in this repo, click the file and press **Download**.

> Windows or your browser may warn about an "unknown publisher" because the app is not code-signed. That is normal for self-built programs: choose **Keep** / **Run anyway**.

## Install

1. Download the app with the link above.
2. Create **your own empty folder** (for example "Virtuality Manager") and put the exe in it.
3. Double-click it. On the first start you choose a **PIN**. It encrypts your saved accounts. Without the PIN nobody can read the data, not even you, so don't forget it.

The app starts empty. No accounts of anybody else are included.

## Updates

The app updates itself: in **Settings → Updates** press **Check now**, or leave **Check automatically** on and it offers new versions when it starts. Nothing else to do, your accounts stay as they are.

## Using it

- **Add account** (bottom left): add an account. The Roblox login opens, you only solve the captcha.
- **Groups are optional.** Without groups everything is simply under "All accounts". To organise: **New group** → name → pick a picture. A group may be empty and then has its own "Add account".
- **Join selected** (top right): the selected accounts join the game from the "Game" field. Select several with Ctrl/Shift or by dragging a box.
- **"Join …" button on each account**: joins a specific player. Choose who under "Join button joins … Change" (or right-click the button): one of your accounts (sorted by group) or any Roblox username.
- **Main account** ("Friend requests go to … Change"): the selected accounts send a friend request to it. You can change it at any time.
- **Settings** (gear): Multi Roblox, "All accounts join the same server" (off by default), backups, updates and more.

## Multi Roblox

Turn it on in Settings. A tiny background helper holds Roblox's single-instance lock, so every account opens in its own window. Restarting or updating the manager never closes games that are already running. If a join cannot start (for example an expired login) the app tells you which account it was instead of silently skipping it.

## Security

- Account data (`Daten\AccountData.json`) is encrypted with your PIN.
- Automatic backups (also encrypted) are kept in `Daten\Backups` **and** in `%LocalAppData%\Virtuality Manager\Backups`. If the app folder is deleted, a fresh Virtuality Account Manager offers to restore the backup on start.
- Browser logins (`Daten\LoginProfiles`) are encrypted with your PIN too (as `.vault` files) while the app is closed or locked. Only the one profile you are logging into is open for a moment. Can be switched off under Settings → Data & security.
- **Still never give anybody your `Daten` folder.** Encryption protects your data if someone copies the folder, but not against malware that runs while the app is open.
- The app does not check the original project for updates; it only updates from this repository.

## Build it yourself

.NET Framework 4.7.2. Open `RBX Alt Manager.sln` in Visual Studio and build Release / x86.
