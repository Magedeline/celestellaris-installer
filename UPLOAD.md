# Celestellaris one-click installer: build and upload

## 1. Build the release folder
```
dotnet publish -c Release -o publish
mkdir release\payload
copy publish\Install-Celestellaris.exe release\
copy payload\KirbyHelperMechanics.zip release\payload\
copy ..\celeste\Mods\CEL\Celestellaris.zip release\payload\     (rebuild/refresh this zip first)
```
`release\` is what you ship. Players run `Install-Celestellaris.exe`. It finds Celeste
(Steam, itch app, Epic or a manual folder), installs Everest if it's missing, copies every zip in
`payload\`, then downloads the 19 public helper mods from the Everest mod mirror.

## 2. itch.io (butler, supports big files and delta updates)
```
butler login
butler push release YOURUSER/celestellaris:windows
```
Create the page first at itch.io/game/new (Kind of project: Game or Tool; set the platform to Windows).

## 3. GameJolt
GameJolt has no CLI. Zip `release\` and upload it in the dashboard under Game > Packages > Add build.
Check GameJolt's current per-file size limit before you upload. The mod zip is about 1.8 GB, so you may need to
split it, or have the installer download it from itch instead.

## Notes
- Celeste itself is never included. Players need their own copy.
- Anything in `payload\` is shipped by you, so only include mods you own or have permission to redistribute.
  The public helpers are downloaded at install time instead.
- Unsigned installers can trigger Windows SmartScreen warnings. Code-signing removes it.
- To refresh the mod, replace the zip in `payload\` and run `butler push` again. It only uploads the changed parts.
