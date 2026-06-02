# shelly

A tiny CLI that turns plain-English descriptions into shell commands using an
LLM. Modeled on the (now-discontinued) GitHub Copilot `suggest` / `explain`
CLI, with provider-swappable backends — Anthropic (Claude) or Azure OpenAI.

```text
$ shelly "find the five largest files in this directory"
Thinking...

Suggestion:
du -ah . | sort -rh | head -n 5

e[x]ecute  [e]dit  ex[p]lain  [c]opy  [r]etry  [q]uit
```

## Install

### Download a release

Grab the archive for your platform from the
[Releases](https://github.com/jdmallen/shelly/releases) page and extract the
binary onto your `PATH`. Each archive contains a single self-contained
executable — no .NET runtime needed.

| OS      | Architecture | Archive                                |
| ------- | ------------ | -------------------------------------- |
| Linux   | x64          | `shelly-<version>-linux-x64.tar.gz`    |
| Linux   | arm64        | `shelly-<version>-linux-arm64.tar.gz`  |
| macOS   | x64 (Intel)  | `shelly-<version>-osx-x64.tar.gz`      |
| macOS   | arm64 (M1+)  | `shelly-<version>-osx-arm64.tar.gz`    |
| Windows | x64          | `shelly-<version>-win-x64.zip`         |
| Windows | arm64        | `shelly-<version>-win-arm64.zip`       |

Verify the download against `SHA256SUMS.txt` published alongside the release.

**Linux / macOS:**

```sh
tar -xzf shelly-<version>-<rid>.tar.gz
chmod +x shelly
sudo mv shelly /usr/local/bin/
```

**macOS only:** the first run will be blocked by Gatekeeper because the binary
is unsigned. Either right-click → Open once, or run:

```sh
xattr -d com.apple.quarantine /usr/local/bin/shelly
```

**Windows:** extract `shelly.exe` and place it somewhere on `%PATH%`.

**Linux clipboard:** the `[c]opy` action shells out to a system clipboard tool.
Install one of `wl-clipboard` (Wayland), `xclip`, or `xsel` (X11) — whichever
your desktop uses. The clipboard lives in the graphical session, so copy only
works from a local desktop terminal where `WAYLAND_DISPLAY` or `DISPLAY` is set;
over a headless/SSH session there's no clipboard to copy into and `shelly` will
say so. Either way, every other action still works.

### Build from source

Requires the **.NET 10 SDK** (https://dotnet.microsoft.com/download).

```sh
git clone https://github.com/jdmallen/shelly.git
cd shelly
dotnet build -c Release
```

For a single-file self-contained binary like the released ones:

```sh
dotnet publish src/JDMallen.Shelly -c Release -r <rid> -o ./out
```

Replace `<rid>` with one of `linux-x64`, `linux-arm64`, `osx-x64`,
`osx-arm64`, `win-x64`, `win-arm64`.

To produce archives for every supported platform at once:

```sh
scripts/publish.sh 0.1.0
```

Artifacts land in `dist/`.

## Configure

### API credentials (environment variables)

Set these in your shell profile (`~/.zshrc`, `~/.bashrc`, PowerShell
`$PROFILE`, etc.).

**Anthropic (Claude) — default provider:**

```sh
export ANTHROPIC_API_KEY_SHELLY="sk-ant-..."
```

> The key is named `ANTHROPIC_API_KEY_SHELLY` (not `ANTHROPIC_API_KEY`) so it
> doesn't collide with other tools — notably Claude Code — that read the
> generic name.

**Azure OpenAI:**

```sh
export AZURE_OPENAI_API_KEY="..."
export AZURE_OPENAI_ENDPOINT_HOST="https://<your-resource>.openai.azure.com"
export AZURE_OPENAI_DEPLOYMENT="<your-deployment-name>"
```

### Provider selection (config file)

A config file is auto-created on first run at:

- **Linux / macOS:** `~/.config/shelly/config.json`
- **Windows:** `%APPDATA%\shelly\config.json`

Default contents:

```json
{
  "provider": "anthropic",
  "anthropic": {
    "model": "claude-haiku-4-5-20251001"
  }
}
```

To switch to Azure, change `provider` to `"azure"`. Azure has no other config
file fields — all of its settings come from the environment variables above.

You can also pin a different Anthropic model by editing `anthropic.model`.

## Use

```sh
shelly describe what you want to do
```

Or invoke with no arguments to be prompted.

Quotation marks around the prompt are optional — `shelly` joins all positional
arguments into a single prompt. Use quotes only when the prompt contains
characters your shell would otherwise interpret (pipes, redirects, semicolons,
`!`, glob patterns, etc.):

```sh
shelly "find files modified in the last 24h and pipe to wc -l"
```

At the suggestion menu:

| Key     | Action                                                       |
| ------- | ------------------------------------------------------------ |
| `x`     | Execute the suggested command in your `$SHELL` (or `cmd.exe` on Windows) |
| `e`     | Edit — drop the command onto your next prompt, unexecuted, to tweak before running (requires [shell integration](#edit-on-the-command-line-shell-integration)) |
| `p`     | Explain what the command does                                |
| `c`     | Copy the command to the clipboard and quit                   |
| `r`     | Retry — optionally type a refinement to nudge the model      |
| `q` / Esc | Quit without doing anything                                |

The `[e]dit` and `[c]opy` options only appear when they can actually work:
`edit` requires the shell wrapper below, and `copy` requires a graphical
session with a clipboard tool (so it's hidden over headless/SSH sessions).

### Edit on the command line (shell integration)

The `[e]dit` action puts the suggested command onto your **next prompt**
without running it, so you can adjust it first. A program can't type into its
parent shell's input line on its own, so this needs a small wrapper function
that shelly ships with.

**zsh** — source the wrapper from your `~/.zshrc`:

```sh
source /path/to/shelly/shell/shelly.zsh
```

(If you installed only the binary, grab `shell/shelly.zsh` from this repo.) The
wrapper runs the real `shelly`, and when you press `e` it pushes the command
onto your next prompt via zsh's `print -z`. Once it's sourced, the `[e]dit`
option appears automatically.

**bash / fish:** not supported yet. Neither can reliably prefill the next
prompt from a plain function call the way zsh's `print -z` does, so the edit
option simply won't show. `copy` remains the portable alternative.

## Develop

```sh
# Restore + build everything
dotnet build

# Run tests (xunit v3)
dotnet test

# Run locally without publishing
dotnet run --project src/JDMallen.Shelly -- "list files newer than yesterday"
```

Layout:

```
src/JDMallen.Shelly/       # main app
test/JDMallen.Shelly.Tests # xunit v3 unit tests
shell/shelly.zsh           # zsh wrapper enabling the [e]dit action
scripts/publish.sh         # multi-RID release script
.github/workflows/         # CI + release automation
```

### Releasing

CI builds release artifacts when a `v*` tag is pushed:

```sh
git tag v0.1.0
git push origin v0.1.0
```

The `release.yml` workflow builds on Linux/Windows/macOS runners, packages
each RID, generates `SHA256SUMS.txt`, and attaches everything to a GitHub
release. You can also trigger it manually from the Actions tab via
**workflow_dispatch**.

## License

MIT
