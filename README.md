# Celestellaris Installer

One-click Windows installer for the Celestellaris mod for Celeste. It finds Celeste (Steam, Epic, GOG, itch app
or a manual folder), installs [Everest](https://github.com/EverestAPI/Everest) if missing (SHA-256 verified
against the checksum GitHub publishes), copies the mod files, and downloads the required helper mods from the
public Everest mod database (xxHash64 verified). Celeste itself is never included. You need your own copy.

## Code signing policy
Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by
[SignPath Foundation](https://signpath.org). Releases are built from this repository by GitHub Actions.
The installer does not collect or transmit personal data. It only downloads Everest and helper mods
from their public sources.

Build: `dotnet publish -c Release -o publish`. See `TRUST.md` (signing), `NOTICE.md` (licences) and
`UPLOAD.md` (release steps). MIT licensed.
