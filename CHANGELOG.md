# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0]

The first stable release. It adds an `[e]dit` action with zsh shell integration,
replaces the bundled clipboard library with a runtime-detected system clipboard,
and—most significantly—swaps the two hand-rolled, SDK-backed chat providers for a
single thin provider built on the **`JDMallen.Toolbox.AI`** package. The major
version bump reflects that provider rearchitecture and the removal of the
`Anthropic`, `Azure.AI.OpenAI`, and `TextCopy` dependencies; the user-facing
configuration (the `shelly` config file, the `ANTHROPIC_API_KEY_SHELLY` /
`AZURE_OPENAI_*` environment variables, and the CLI itself) is unchanged.

### Added

- **`[e]dit` action with shell integration.** A new suggestion-menu action drops
  the suggested command onto your **next shell prompt**, unexecuted, so you can
  tweak it before running. Because a program can't type into its parent shell's
  input line, this ships as a small zsh wrapper (`shell/shelly.zsh`) that runs
  the real `shelly` and, on `e`, pushes the command onto the next prompt via
  zsh's `print -z`. The app and wrapper coordinate through an environment-variable
  handoff (`EditHandoff`): the option only appears when the wrapper is active, and
  the wrapper is bundled into the non-Windows release archives. bash/fish are not
  supported yet (neither can reliably prefill the next prompt the way `print -z`
  does).
- **Runtime-detected system clipboard.** The `[c]opy` action now shells out to a
  platform-appropriate clipboard tool, detecting availability at startup
  (`wl-clipboard`/`xclip`/`xsel` on Linux, with a graphical `WAYLAND_DISPLAY`/
  `DISPLAY` session). When no clipboard is reachable—e.g. a headless or SSH
  session—the `[c]opy` option is hidden and `shelly` says so; every other action
  still works.
- **Dynamic suggestion menu.** The menu now only offers actions that can actually
  work in the current environment: `[e]dit` appears only when the shell wrapper is
  active, and `[c]opy` only when a clipboard is available. The explain key moved
  from `e` to `p`, so the full menu reads
  `e[x]ecute  [e]dit  ex[p]lain  [c]opy  [r]etry  [q]uit`.
- **`global.json`** pinning the .NET SDK (`10.0.300`, `latestMinor` roll-forward)
  for reproducible builds, and a **`nuget.config`** pinning `nuget.org` as the
  sole package source.
- **Solution Items folder** in `JDMallen.Shelly.slnx` surfacing `.gitignore`,
  `LICENSE`, `README.md`, `scripts/publish.sh`, and `shell/shelly.zsh` in the IDE.
- Expanded **README** sections covering the `[e]dit` shell integration, the Linux
  clipboard requirements, and the dynamic menu behavior.

### Changed

- **Chat providers rearchitected onto `JDMallen.Toolbox.AI` (3.0.0).** The two
  hand-rolled providers (`AnthropicChatProvider`, `AzureChatProvider`), which each
  spoke their vendor's wire format directly, are replaced by a single
  `ChatProvider` that owns shelly's prompt construction, token budget, and
  answer post-processing and delegates the provider-specific HTTP/JSON to a
  Toolbox.AI `IChatCompletionClient` (`AnthropicChatClient` /
  Azure client). Provider selection, env-var configuration, and behavior are
  unchanged. All chat completions now share a single `HttpClient` (30s timeout)
  for the REPL session.
- **Publishing refactored to the shared engine.** `scripts/publish.sh` is now a
  thin wrapper that sets per-app variables (exe name, runtimes, the extra
  `shell/shelly.zsh` file for non-Windows archives) and sources a vendored,
  shared `scripts/publish-dotnet.sh` engine; its behavior and the default RID set
  (`linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`)
  are unchanged.
- **Release workflow rewritten to publish on merge.** `.github/workflows/release.yml`
  now triggers on pushes to `main` instead of on `v*` tags: it reads `<Version>`
  from `Directory.Build.props`, no-ops if that tag already exists, builds the
  archives with `scripts/publish.sh`, then creates the GitHub release and its tag
  (raw semver, no `v` prefix) in one atomic `gh` call ordered after a successful
  build, with `SHA256SUMS.txt` attached. Release notes are the matching
  `CHANGELOG.md` section (extracted with an `awk` script) rather than GitHub's
  auto-generated notes. Routine merges that don't bump `<Version>` never
  re-release.
- **Single version source of truth in `Directory.Build.props`.** `<Version>` is
  set there (so it applies to every project, app and tests) and read by both
  `scripts/publish.sh` and the release workflow; it is bumped to `1.0.0`.

### Removed

- **`Anthropic` (12.21.0) and `Azure.AI.OpenAI` (2.1.0) SDK dependencies**,
  replaced by the lightweight Toolbox.AI client. With them gone, the
  `JsonSerializerIsReflectionEnabledByDefault` trimming workaround they required
  is no longer needed and has been dropped.
- **`TextCopy` (6.2.1) dependency**, replaced by the runtime-detected system
  clipboard.

## [0.1.0]

Initial release. A tiny, self-contained CLI that turns plain-English descriptions
into shell commands using an LLM, modeled on the (discontinued) GitHub Copilot
`suggest` / `explain` CLI.

### Added

- **Natural-language-to-shell REPL.** Given a description, `shelly` asks an LLM
  for a command and presents an interactive suggestion menu:
  `e[x]ecute  [e]xplain  [c]opy  [r]etry  [q]uit`. Execute runs the command in the
  user's `$SHELL` (or `cmd.exe` on Windows); retry can take a refinement to nudge
  the model.
- **Provider-swappable backends.** Anthropic (Claude) and Azure OpenAI providers
  (`AnthropicChatProvider`, `AzureChatProvider` behind `IChatProvider`), selected
  by the config file and configured via environment variables
  (`ANTHROPIC_API_KEY_SHELLY`; `AZURE_OPENAI_API_KEY`, `AZURE_OPENAI_ENDPOINT_HOST`,
  `AZURE_OPENAI_DEPLOYMENT`).
- **Testable, abstracted core.** A `ReplLoop` built on `IConsoleIO`, `IClipboard`,
  and `IChatProvider` abstractions; a `CommandSanitizer` that strips code fences
  and backticks from model output; shell/OS-aware `ShellContext`; system/explain
  `Prompts`; and a source-generated JSON `ShellyConfig` loaded from the user
  config path. Clipboard support via `TextCopy`; argument parsing via
  `CommandLineParser`.
- **Self-contained distribution.** .NET 10, published as single-file,
  self-contained, trimmed binaries for six RIDs (`linux-x64`, `linux-arm64`,
  `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`) via `scripts/publish.sh`.
- **CI and release automation.** A GitHub Actions CI workflow plus a `v*`-tag /
  `workflow_dispatch`-triggered release workflow that builds the per-RID archives
  on a matrix, generates `SHA256SUMS.txt`, and creates a GitHub release with
  auto-generated notes.
- **Unit test suite** (`test/JDMallen.Shelly.Tests`, xunit v3) covering the
  command sanitizer and the REPL loop, with console, clipboard, and chat-provider
  fakes.
- A **README** documenting installation (release downloads and build-from-source),
  configuration, usage, and the suggestion-menu keys.
