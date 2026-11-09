# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.7.0]

### Added

- **Keyless Azure OpenAI (Microsoft Entra ID).** Set `azure.auth` to
  `"azureCli"` and shelly sends a Microsoft Entra ID token from the Azure CLI
  (`az login`) instead of `AZURE_OPENAI_API_KEY`, for resources that use
  role-based access instead of keys. Optional `azure.tenantId` and
  `azure.tokenScope` settings pick the tenant and scope. The token is fetched
  on the first request and reused until shortly before it expires.
  `"apiKey"` stays the default, so existing configs are unchanged.
- `shelly --config` asks for the Azure authentication method. For the Azure
  CLI it asks for an optional tenant (`-` clears it), skips the API key step,
  and instead reports whether `az` is on PATH and prints the `az login`
  command and the role the account needs.
- `shelly --info` reports the Azure authentication method, and for the Azure
  CLI the tenant, scope, az's location and the sign-in command.
- The main menu's "needs setup" list names `az` when the Azure CLI isn't on
  PATH, and `azure.auth`, `azure.tenantId` or `azure.tokenScope` when one is
  invalid.

### Changed

- Requires `JDMallen.Toolbox.AI` 4.5.0.

## [1.6.1]

### Changed

- Token usage tracking now comes from `JDMallen.Toolbox.AI` 4.4.0
  (`TokenUsageTally`, `TokenUsageLog`) instead of shelly's own copy, shared
  with committy. `usage.json` keeps the same location and layout, so existing
  totals carry over.

## [1.6.0]

### Added

- **Token usage.** After the suggestion menu, faded text shows the tokens the
  current run has used so far (suggestions, retries and explanations).
  Each run's total is added to `usage.json` in the local app data directory,
  and `shelly --info` reports the lifetime total and the average per run,
  split into input and output tokens. Providers that don't report usage are
  left out of both. Set `"showTokenUsage": false` in `config.json` to hide
  the faded count; the totals are still recorded.

### Changed

- Requires `JDMallen.Toolbox.AI` 4.3.0, using its `GetCompletionAsync`
  (completion plus usage and metadata) in place of the deprecated
  `CompleteAsync`.

## [1.5.0]

### Added

- **`shelly --config`**: an interactive setup wizard. Pick the provider
  (Anthropic, OpenAI-compatible or Azure OpenAI), then fill in its settings;
  each prompt shows the current value and Enter keeps it. Numbers and URLs
  are validated as you type them. The wizard asks before saving
  `config.json`, and writes it via a temporary file so an interrupted save
  can't truncate it. A config file that fails to parse is backed up as
  `config.json.bak_<UTC timestamp>` before it is replaced.
- **API key help in the wizard.** It reports whether the provider's key
  variable is set (never its value). On Windows it offers to save the key as
  a user environment variable, with `*` shown while typing, saved only when
  you save the config. On Linux and
  macOS it prints the exact line for the detected shell and the startup file
  it belongs in (`set -Ux` for fish, which needs no file), and edits nothing.
- **Main menu.** `shelly` with no arguments now shows the version and a
  numbered menu: Ask a question (default, Enter), Configure, Info, and
  0 (or Esc) to exit. In the wizard, 0 (or Esc) at the provider menu goes
  back to the main menu without changing anything.
  When the active provider isn't ready, Configure is followed by what it
  still needs, e.g. `(needs setup: ANTHROPIC_API_KEY_SHELLY)`.
- **`azure.endpoint`** and **`azure.deployment`** config settings, so only
  the API key stays in the environment.

### Changed

- `shelly` with no arguments shows the main menu instead of prompting
  straight away. Piped input skips the menu, so scripts behave as before.
- A missing Azure endpoint or deployment now names the config keys
  (`azure.endpoint`, `azure.deployment`) and suggests `shelly --config`.

### Deprecated

- **`AZURE_OPENAI_ENDPOINT_HOST`** and **`AZURE_OPENAI_DEPLOYMENT`**. They
  are still read when the matching config setting is empty, with a warning;
  the config file wins when both are set. `shelly --config` pre-fills their
  values, so saving once migrates them. `--info` marks values that came from
  them as deprecated.

## [1.4.1]

### Added

- **`--info`** prints the active configuration without calling the model:
  config file path, provider, endpoint, model or deployment, token budget,
  whether the provider's API key variable is set (never its value), and the
  detected shell.
- **`azure.maxTokens`** in the config file sets the Azure generation budget
  (default `500`, as before). Reasoning deployments spend their hidden
  thinking from it, so they may need more to return an answer.
- **`azure.reasoningModel`** (default `false`): for reasoning deployments,
  stops sending `temperature`, which they reject.

### Changed

- **JDMallen.Toolbox.AI 3.1.0 → 4.2.1.** Azure requests now send
  `max_completion_tokens` instead of `max_tokens`, and no longer send `top_p`
  or the penalty parameters (they were the service defaults). Fixes `400
  unsupported_parameter` from reasoning deployments such as `gpt-6-luna`.

### Fixed

- **Errors exit non-zero.** A failed model request, or an empty suggestion,
  now exits `1` instead of `0`.

## [1.4.0]

### Added

- **Shell-aware suggestions.** shelly now detects the shell it was launched
  from (bash, zsh, POSIX sh, KornShell, fish, tcsh, PowerShell 7, Windows
  PowerShell 5.1, `cmd.exe`, Nushell, Elvish) by walking its parent
  processes, and tells the model to write commands for that shell, with
  syntax hints for the ones a model tends to answer in bash (fish, POSIX sh,
  PowerShell, `cmd.exe`, csh, Nushell, Elvish). `[p]` explains the command
  in terms of that shell. Set `SHELLY_SHELL` to override detection.

### Changed

- **`[x]` runs the command in the detected shell** instead of `$SHELL` (or
  `cmd.exe` on Windows), so a command written for the shell you're in runs in
  that shell. PowerShell runs with `-NoProfile -Command`. A shell that can't
  be started now reports an error instead of crashing.

## [1.3.0]

### Added

- **Localized UI.** Every user-facing string moved into
  `Resources/Strings.resx`, with translations for 19 cultures: `zh-Hans`,
  `zh-Hant`, `es`, `pt-BR`, `de`, `ja`, `fr`, `ru`, `ko`, `id`, `it`, `pl`,
  `tr`, `uk`, `vi`, `nl`, `cs`, `sv` and `th`. The culture comes from the OS
  UI culture (`LC_ALL`/`LC_MESSAGES`/`LANG` on Unix); anything untranslated
  falls back to English. Menu keys are unchanged in every language.
- **Explanations in your language.** The model prompts stay in English, which
  models follow most reliably; under a translated culture the explain prompt
  adds "Write your explanation in {language}." The translations are compiled
  into the Native AOT binary, which grows by about 153 KB on linux-x64.

## [1.2.0]

### Added

- **`--` ends option parsing**, so a prompt may start with a dash, e.g.
  `shelly -- -la but sorted by size`. `-h` and `-?` are also accepted as
  aliases for `--help`.

### Changed

- **Release binaries are now Native AOT.** The linux-x64 binary drops from
  13,973,876 to 6,886,504 bytes (archive 6,995,595 to about 3.2 MB), `--version`
  from about 0.12 s to under 0.01 s, and peak memory from about 23.5 MB to about
  12.9 MB.
- **`--help` and `--version` now exit 0** (they used to exit 1). Unknown options
  still exit 1.
- **Release workflow builds per OS on native runners** (AOT cannot cross-compile
  between operating systems) and gains a `dry_run` dispatch input that builds
  and uploads per-RID artifacts without tagging or releasing.

### Removed

- The `CommandLineParser` dependency, replaced by a small built-in parser.

## [1.1.0]

Adds a third backend: any endpoint speaking the OpenAI `/v1/chat/completions`
API, which covers openai.com and — the motivating case — models you host
yourself via llama.cpp, llama-swap, Ollama, vLLM, or LM Studio. Nothing about
the existing Anthropic and Azure setups changes.

### Added

- **`openai` provider (alias `local`).** Set `provider` to `"openai"` and fill in
  the new `openai` block in the config file: `baseUrl` (the API root including
  the version segment, e.g. `http://10.10.0.20:8080/v1`), `model` (whatever your
  server reports at `/v1/models`), plus optional `timeoutSeconds` and
  `maxTokens`. Both `baseUrl` and `model` are deployment-specific, so neither has
  a default; leaving either blank fails with a message naming the missing key and
  the config file's path.
- **Optional API key.** `OPENAI_API_KEY_SHELLY` is sent as a bearer token when
  set and omitted entirely when not, because self-hosted runners generally do not
  authenticate.
- **Reasoning-model support.** Thinking blocks are stripped before the
  suggestion is shown, whether the server splits them into `reasoning_content` or
  inlines them as `<think>…</think>`. If generation is truncated mid-thought,
  shelly reports "No suggestion received from API" instead of offering raw
  reasoning as a command to execute.

### Changed

- **The HTTP timeout is now per-provider.** Anthropic and Azure keep the previous
  30 seconds; the `openai` provider defaults to `timeoutSeconds: 300`, because a
  self-hosted model may have to load its weights from disk before it produces a
  single token.
- **The token budget is now per-provider.** Anthropic and Azure keep the previous
  500; the `openai` provider defaults to `maxTokens: 2048`, since a reasoning
  model spends most of its output on a thinking block that never reaches the
  caller and a 500-token budget leaves no room for an answer.
- **`JDMallen.Toolbox.AI`** upgraded to **3.1.0**, which supplies the underlying
  `OpenAICompatibleChatClient`.

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
