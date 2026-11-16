# Making the installer trusted (legitimate routes only)

## 1. Code-sign it (the biggest single factor)
- **SignPath Foundation** gives free signing to open-source projects. Requirements: a public GitHub repo with an
  OSI licence, builds done by CI (GitHub Actions), and an application at signpath.org. Put the installer source
  in a public repo, add a licence, then apply.
- **Paid options:** Azure Trusted Signing (cheapest, about $10/month, needs identity verification) or an OV/EV
  certificate from a CA. EV gives instant SmartScreen reputation. OV and Trusted Signing build it over time.
- Sign with `.\sign.ps1 -Thumbprint <thumbprint>`, then rebuild `SHA256SUMS.txt` (signing changes the file).

## 2. Tell Microsoft it's clean
Submit the signed exe at https://www.microsoft.com/en-us/wdsi/filesubmission as a developer, "software
developer" option, and say it is a false positive. Do this for each new version if Defender flags it.

## 3. Be transparent
- Keep the real publisher name in the exe properties (set `Company` in the csproj).
- Publish the source and `SHA256SUMS.txt` next to the download on itch and GameJolt.
- Reputation builds as signed downloads accumulate. Keep the same certificate and file name across releases.

## Don't
Don't pack, obfuscate or encrypt the exe to avoid detection. It makes false positives more likely, and
it is exactly what malware does.
