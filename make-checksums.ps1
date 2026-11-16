# Run from the release folder. Writes SHA256SUMS.txt; publish its contents on your itch/GameJolt page.
Get-ChildItem -Recurse -File | Where-Object Name -ne 'SHA256SUMS.txt' | ForEach-Object {
  "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), (Resolve-Path $_.FullName -Relative)
} | Set-Content SHA256SUMS.txt
