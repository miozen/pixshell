[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$testsDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$testProject = Join-Path $testsDir 'PixShell.Tests.csproj'

Write-Host '=== 1. Isolated Integration Environment Check ==='
$hasDocker = [bool](Get-Command docker -ErrorAction SilentlyContinue)
$hasWinSshd = Test-Path (Join-Path $env:WINDIR 'System32\OpenSSH\sshd.exe')
$hasWinSftp = Test-Path (Join-Path $env:WINDIR 'System32\OpenSSH\sftp-server.exe')
$hasWsl = [bool](Get-Command wsl -ErrorAction SilentlyContinue)
$hasWinTar = [bool](Get-Command tar -ErrorAction SilentlyContinue)

Write-Host "Windows built-in tar.exe : $hasWinTar"
Write-Host "Docker CLI               : $hasDocker"
Write-Host "Windows OpenSSH sshd     : $hasWinSshd"
Write-Host "Windows sftp-server.exe  : $hasWinSftp"
Write-Host "WSL CLI                  : $hasWsl"

$wslSshdReady = $false
if ($hasWsl) {
    $wslProbe = wsl -d Ubuntu -- bash -lc 'echo WSL_USER=$(id -un); echo WSL_TAR=$(if command -v tar >/dev/null; then echo True; else echo False; fi); echo WSL_SSHD=$(if test -x /usr/sbin/sshd; then echo True; else echo False; fi); echo WSL_SFTP=$(if test -x /usr/lib/openssh/sftp-server; then echo True; else echo False; fi)' 2>&1
    $wslProbe | ForEach-Object { Write-Host $_ }
    if (($wslProbe -join "`n") -match 'WSL_SSHD=True' -and ($wslProbe -join "`n") -match 'WSL_SFTP=True') {
        $wslSshdReady = $true
    }
}

Write-Host ''
Write-Host '=== 2. Running .NET Isolated Unit + Local Archive Integration Tests ==='
dotnet restore $testProject | Out-Host
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test $testProject -c $Configuration --no-restore --verbosity normal -nologo | Out-Host
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($hasWsl) {
    Write-Host ''
    Write-Host '=== 3. Running WSL Ubuntu Isolated GNU tar / POSIX Staging / ls -la Verification ==='
    $wslScript = @'
set -euo pipefail
root=$(mktemp -d /tmp/pixshell_itest_XXXXXX)
trap 'chmod -R u+rwX "$root" 2>/dev/null || true; rm -rf "$root"' EXIT

mkdir -p "$root/src/sub dir" "$root/stage" "$root/dst" "$root/ro_dst"
printf 'ok\n' > "$root/src/normal file.txt"
printf 'dot\n' > "$root/src/.env"
printf 'dd\n' > "$root/src/..weird"
printf 'old\n' > "$root/dst/existing.txt"
printf 'new\n' > "$root/src/existing.txt"

tar -czf "$root/test.tar.gz" -C "$root/src" "normal file.txt" ".env" "..weird" "existing.txt"

# Case A: staged extract skips conflict and moves dotfiles + normal files cleanly
stage="$root/stage"
dst="$root/dst"
arc="$root/test.tar.gz"
tar -xzf "$arc" -C "$stage"
rc=$?
if [ $rc -eq 0 ]; then
  for p in "$stage"/* "$stage"/.[!.]* "$stage"/..?*; do
    [ -e "$p" ] || [ -L "$p" ] || continue
    n=$(basename "$p")
    if [ -e "$dst/$n" ] || [ -L "$dst/$n" ]; then
      echo "SKIP:$n"
    else
      mv_err=$(mv "$p" "$dst"/ 2>&1); mrc=$?
      if [ $mrc -ne 0 ]; then rc=$mrc; echo "MV_ERR:$n:$mv_err"; break; fi
    fi
  done
fi
rm -rf "$stage"
test $rc -eq 0
test "$(cat "$dst/existing.txt")" = "old"
test "$(cat "$dst/.env")" = "dot"
test "$(cat "$dst/..weird")" = "dd"
test "$(cat "$dst/normal file.txt")" = "ok"
echo "PASS: WSL staged extract preserves existing conflicts, .env, ..weird, and spaced filenames"

# Case B: read-only destination triggers non-zero mv failure
mkdir -p "$root/stage2"
chmod 555 "$root/ro_dst"
tar -xzf "$arc" -C "$root/stage2"
rc2=0
for p in "$root/stage2"/* "$root/stage2"/.[!.]* "$root/stage2"/..?*; do
  [ -e "$p" ] || [ -L "$p" ] || continue
  n=$(basename "$p")
  mv_err=$(mv "$p" "$root/ro_dst"/ 2>&1) || { rc2=$?; echo "MV_ERR:$n:$mv_err"; break; }
done
rm -rf "$root/stage2"
test $rc2 -ne 0
echo "PASS: WSL read-only destination returns non-zero rc=$rc2 instead of false success"

# Case C: real POSIX mode / symlink / unreadable dir ls -la output
mkdir -p "$root/ls_case/unreadable"
printf 'x' > "$root/ls_case/secret.env"
chmod 600 "$root/ls_case/secret.env"
ln -s "secret.env" "$root/ls_case/link to env"
chmod 000 "$root/ls_case/unreadable"
LC_ALL=C ls -la "$root/ls_case" | grep -E 'secret\.env|link to env' >/dev/null
if LC_ALL=C ls -la "$root/ls_case/unreadable" >/dev/null 2>"$root/ls.err"; then
  echo "FAIL: unreadable directory unexpectedly succeeded" >&2
  exit 1
fi
echo "PASS: WSL real ls -la generated 0600, symlink-with-spaces, and unreadable-dir stderr cleanly"
'@
    ($wslScript -replace "`r`n", "`n") | wsl -d Ubuntu -- bash -s
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host ''
Write-Host '=== 4. Live SSH/SFTP/SCP Loopback Protocol Integration Status ==='
if (-not ($hasDocker -or ($hasWinSshd -and $hasWinSftp) -or $wslSshdReady)) {
    Write-Warning 'ENVIRONMENT-LIMITED: Neither Docker, Windows OpenSSH Server (sshd + sftp-server.exe), nor WSL Ubuntu openssh-server (/usr/sbin/sshd + /usr/lib/openssh/sftp-server) is installed. Live SSH/SFTP/SCP daemon protocol tests were NOT executed.'
    exit 0
}

Write-Host 'SSH/SFTP daemon binaries detected. Configure PIXSHELL_ITEST_SSH_PORT/USER/KEY to wire daemon tests.'
