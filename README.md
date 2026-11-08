# Virtuality Account Manager

Ein Account Manager für Roblox: alle Accounts in einem Fenster, Login mit einem Klick, mehrere Roblox-Fenster gleichzeitig, Gruppen mit eigenem Bild, Join in denselben Server.
Basiert auf dem [Roblox Account Manager von ic3w0lf22](https://github.com/ic3w0lf22/Roblox-Account-Manager) (GPL-3.0, siehe `LICENSE` und `README-Original.md`), komplett neu gestaltet.

## ⬇️ Download

**[» Virtuality Account Manager herunterladen «](https://github.com/VerifiedNick/virtuality-manager/raw/main/Download/Virtuality%20Manager.exe)**

Klick auf den Link oben, dann startet der Download der fertigen App (`Virtuality Manager.exe`).
Alternativ im Repo unter `Download/` → auf die Datei klicken → **Download**.

> Windows oder der Browser warnt vielleicht „unbekannter Herausgeber“, weil die App nicht teuer signiert ist. Das ist bei selbstgebauten Programmen normal: **Behalten** bzw. **Trotzdem ausführen** wählen.

## Installieren

1. Die App über den Download-Link oben herunterladen.
2. Auf dem Desktop einen **eigenen, leeren Ordner** anlegen (z. B. „Virtuality Manager“) und die exe dort hineinlegen.
3. Doppelklick. Beim ersten Start legst du eine **PIN** fest. Mit ihr werden deine Accounts verschlüsselt gespeichert. Ohne PIN kommt niemand an die Daten, auch du nicht, also nicht vergessen.

Die App ist leer, wenn du sie startest. Es sind keine Accounts von jemand anderem dabei.

## Benutzen

- **Add account** (unten links): Account hinzufügen. Roblox-Login öffnet sich, du machst nur das Captcha.
- **Gruppen sind freiwillig.** Ohne Gruppen ist alles einfach unter „All accounts“. Willst du sortieren: **New group** → Name → Bild aussuchen. Eine Gruppe darf auch leer sein, darin gibt es dann direkt „Add account“.
- **Join selected** (oben rechts): die ausgewählten Accounts joinen das Spiel aus dem Feld „Game“. Mehrere auswählen mit Strg/Shift oder Rahmen ziehen.
- **„Join …“-Button auf jedem Account**: joint einen bestimmten Spieler. Welchen, stellst du rechts bei „Join button joins … Change“ ein (oder Rechtsklick auf den Button): einen deiner Accounts (nach Gruppen sortiert) oder irgendeinen Roblox-Namen.
- **Main account** (rechts „Friend requests go to … Change“): an diesen Account schicken die ausgewählten Accounts eine Freundschaftsanfrage. Kannst du jederzeit ändern.
- **Settings** (Zahnrad): Multi Roblox, „alle joinen denselben Server“, Backups usw.

## Sicherheit

- Account-Daten (`Daten\AccountData.json`) sind mit deiner PIN verschlüsselt.
- Automatische Backups (ebenfalls verschlüsselt) liegen in `Daten\Backups` **und** in `%LocalAppData%\Virtuality Manager\Backups`. Wird der App-Ordner gelöscht, bietet eine neue Virtuality Account Manager beim Start an, das Backup zurückzuholen.
- Auch die Browser-Logins (`Daten\LoginProfiles`) werden mit deiner PIN verschlüsselt (als `.vault`-Dateien), sobald die App zu oder gesperrt ist. Nur das eine Profil, mit dem du dich gerade einloggst, liegt kurz offen. Abschaltbar unter Settings → Data & security.
- **Gib trotzdem niemandem deinen `Daten`-Ordner.** Verschlüsselung schützt deine Daten, wenn jemand den Ordner kopiert – aber nicht vor einem Virus, der läuft, während die App offen ist.
- Die App sucht keine Updates beim Original-Projekt. Updates gibt es nur hier als neue exe.

## Selbst bauen

.NET Framework 4.7.2, Lösung `RBX Alt Manager.sln` in Visual Studio öffnen, Release / x86 bauen.
