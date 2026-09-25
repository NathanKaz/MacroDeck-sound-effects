# Sound Effects for Macro Deck

A soundboard plugin for Macro Deck 3. Put any audio file on a button: Play, Play/Stop, Overlap, Loop,
Stop all and Random cover the common ways a dock is used, and all six share one playback engine built
on [SoundFlow](https://github.com/kantoniak/SoundFlow) (miniaudio). Everything plays straight from disk,
no samples to bundle.

**Features**

- Six actions, one shared audio engine (lazy initialization, no idle resource use).
- Formats: WAV, FLAC, OGG, Opus, MP3, M4A, AAC, AIFF and WMA. FLAC/OGG/Opus decode natively, the rest
  go through the bundled FFmpeg codec.
- Files are decoded once, straight to the output device's sample rate (48 kHz), so low-rate sources
  play without on-the-fly resampling and its artifacts.
- Localized in English and Russian; the display language follows Macro Deck's.
- Windows x64, macOS (Apple Silicon) and Linux x64.

## The actions

Every action is localized; the parameter names below are the internal ids you see in your deck profile.

| Id | Action | What it does on press | Parameters |
| --- | --- | --- | --- |
| `play` | Play | Plays the file once. A second press restarts it from the top. | `file`, `volume` |
| `play-stop` | Play/Stop | Toggles. If the file is already playing, stops it; otherwise starts it. | `file`, `volume` |
| `overlap` | Overlap | Restarts the file even while it is playing, so holding the button layers the sound. | `file`, `volume` |
| `loop` | Loop | Plays the file on repeat until stopped. | `file`, `volume` |
| `stop-all` | Stop all | Stops every file that is currently playing. | none |
| `random` | Random | Plays a random file from a folder. Files are matched by extension. | `folder`, `volume` |

- `file` is a file picker that filters to the supported formats. `folder` is a folder picker.
- `volume` is a 0-100 slider. The default of 100 means "as recorded".

Playback is deliberately simple and honest about failures:

- A missing file, a blank file selection or an audio folder with no supported files fails the action
  with a *failed* result and a localized message. It never reports success for a press that did nothing.
- A machine with no audio output device fails the play actions rather than pretending to play.
- Files are loaded into memory on press (`AssetDataProvider`), not streamed, which suits short sound
  effects and makes Loop a seek in a buffer instead of an on-the-fly stream.

| Formats | Native | Via FFmpeg |
| --- | --- | --- |
| Lossless | WAV, FLAC, OGG, Opus | AIFF, WMA |
| Lossy | - | MP3, M4A, AAC, OGA |

## Requirements

| Requirement | Notes |
| --- | --- |
| Macro Deck 3.0.0 or newer | The host ships the .NET 10 runtime the plugin runs on; no separate runtime install. |
| An audio output device | ALSA/PulseAudio on Linux, CoreAudio on macOS, WASAPI on Windows. |
| .NET SDK 10.0 | Only to build from source, plus the `macrodeck-plugin` CLI to pack. |
| OS | Windows x64, macOS Apple Silicon (arm64), Linux x64. |

## Install a release

A release artifact is a single `.macroDeckPlugin` file. Install it by opening it with, or dragging it
into, the Macro Deck desktop app (Settings → Plugins). There is nothing else to configure: the plugin
works as soon as a file is placed on a button.

## Build from source

Requirements: the .NET SDK, and the developer CLI (needs the ASP.NET Core shared framework, because its
stub host is a real Kestrel server):

```bash
dotnet tool install --global MacroDeck.Plugin.Cli --prerelease
```

`--prerelease` is required while the Macro Deck SDK is in preview: only preview versions are published,
and `dotnet tool install` picks stable ones by default. Drop it once 3.0 ships.

Build and test need no Macro Deck installation:

```bash
dotnet build
```

```bash
dotnet test
```

### Pack the artifact

`build` reads `macrodeck-build.json`, publishes every platform the manifest declares into its
`runtimes/<rid>/` slot, then packs the artifact, deriving the `files[]` digests and the `languages`
array from disk:

```bash
macrodeck-plugin build --source src/SoundEffects --output ./artifacts
```

The result lands at `artifacts/com.opencode.sound-effects-<version>.macroDeckPlugin`. Inspect it to
see what installing yields - entrypoints, languages, entry count, size:

```bash
macrodeck-plugin inspect --artifact ./artifacts/com.opencode.sound-effects-<version>.macroDeckPlugin
```

Building one platform is what a CI matrix job wants:

```bash
macrodeck-plugin build --source src/SoundEffects --rid win-x64 --output ./artifacts
```

The plugin is **framework-dependent**: each entrypoint names the `.dll` and runs on the .NET 10 runtime
Macro Deck itself ships (so the process shows up as `dotnet` in Task Manager, Activity Monitor or `ps`).
The artifact is about 13 MB because it carries the SoundFlow engine and the FFmpeg decoder plus their
native libraries for all three platforms. A plain `dotnet build -c Release` is *not* packable: the
manifest points at `runtimes/<rid>/`, which only `macrodeck-plugin build` assembles.

## Tests

```bash
dotnet test
```

The suite covers the integration wiring, later every capability: actions fail honestly (missing file,
blank file, empty folder, `stop-all` with nothing playing is a clean success), the catalog is scoped to
the plugin id, and the English and Russian key sets resolve.

One further test is marked `[Explicit]` because it needs a real audio output device: it synthesizes a
short WAV, plays it through the whole execution path and stops it. Run it where you can hear (or at
least run) sound:

```bash
dotnet test --filter FullyQualifiedName~Play_with_a_generated_wave_starts_playback
```

The protocol contract - handshake, negotiations, capability serialization, invocation and cancellation,
reconnect and resume, the reserved `/_macrodeck/*` endpoints, logging limits - is covered by the
conformance suite, which drives a real session:

```bash
macrodeck-plugin test --project src/SoundEffects --report markdown --output conformance.md
```

Checks are Required or Recommended, each with a stable id you can select with `--check` or
`--category`; a check reports `SKIP` (with a reason) when the plugin gives it nothing to observe. Exit
codes make it a CI gate: `0` conformant, `1` the plugin is wrong, `2` usage error, `3` input
unreadable, `4` cancelled.

## Run and debug against Macro Deck

### Without a host

```bash
macrodeck-plugin run --project src/SoundEffects --stub-host
```

Starts a disposable in-process host: the plugin registers, negotiates the protocol and initializes, and
its log output streams until you interrupt it. `macrodeck-plugin run --artifact <file>` does the same
for a packed artifact, proving the entrypoint paths match what `build` wrote.

### In the desktop app, with a debugger

The project contains exactly one interactive launch profile, **Macro Deck - Real Host**. It launches the
plugin directly, so Rider and Visual Studio attach the debugger to plugin code without a wrapper or
child-process attach, and connects in self-registering mode to the desktop app at `http://127.0.0.1:8193`.

First run:

1. Start Macro Deck.
2. Open **Developer Tools → Plugin tokens**, create a token and copy it. It is shown only once.
3. Store the token in the source project's **.NET User Secrets** (the project is already initialized; do
   not run `dotnet user-secrets init`). Do not select the `.Tests` project.
4. Select **Macro Deck - Real Host** and start it with **Debug**.
5. When enrollment succeeds, remove the token from User Secrets.

In Rider: right-click `SoundEffects` in the Solution Explorer and choose **Tools → .NET User Secrets**.
In Visual Studio: right-click `SoundEffects` and choose **Manage User Secrets**. Replace the contents of
the `secrets.json` it opens with:

```json
{
  "MacroDeck:Plugin:EnrollmentToken": "<paste the one-time token here>"
}
```

As an alternative, from a terminal (masked input, nothing in shell history or process arguments):

```bash
project="src/SoundEffects/SoundEffects.csproj"
printf "Enrollment token: "
read -rs md_enrollment_token
printf '\n'
printf '{"MacroDeck:Plugin:EnrollmentToken":"%s"}\n' "$md_enrollment_token" |
  dotnet user-secrets set --project "$project"
unset md_enrollment_token
```

PowerShell equivalent:

```powershell
$project = "src/SoundEffects/SoundEffects.csproj"
$token = Read-Host "Enrollment token" -MaskInput
@{ "MacroDeck:Plugin:EnrollmentToken" = $token } |
  ConvertTo-Json -Compress |
  dotnet user-secrets set --project $project
Remove-Variable token
```

Then remove the token after the first successful launch:

```bash
dotnet user-secrets remove "MacroDeck:Plugin:EnrollmentToken" --project "$project"
```

The profile persists the exchanged plugin credential under `src/SoundEffects/.macrodeck-dev-state/`,
which is Git-ignored and excluded from the packed artifact; later launches reuse it. User Secrets are
local-only but not encrypted: never put the enrollment token in `launchSettings.json`, a shared IDE
configuration, a literal command argument or a commit.

## How the plugin is put together

### Project layout

```
src/SoundEffects/
  Program.cs             the host builder: registers the audio engine as a singleton, then RunAsync
  manifest.json          identity, icon, licence and per-platform entrypoints
  macrodeck-build.json   how `macrodeck-plugin build` publishes each platform
  PluginIntegration.cs   the integration: lifecycle and the six actions
  Audio/SoundManager.cs  the shared playback engine (lazy device init, per-track lifetime, cleanup)
  Actions/*.cs           the six action definitions, shared parameter helpers, the formats list
  Localization/Strings.resx   default-culture strings; Strings.ru.resx the Russian translation
  Assets/icon.svg        the icon the manifest declares
  Properties/launchSettings.json   the single real-host debug profile
tests/SoundEffects.Tests/
  PluginIntegrationTests.cs   the plugin builds, the actions fail honestly, the catalog is wired
```

### The entry point

`MacroDeckPlugin.CreatePlugin(args)` wraps `WebApplication.CreateBuilder`, so everything an ASP.NET
Core application has is available - configuration, options binding, `IHttpClientFactory`, hosted
services, dependency injection:

```csharp
var builder = MacroDeckPlugin.CreatePlugin(args);
builder.Services.AddSingleton<SoundManager>();

var plugin = builder
    .UseMacroDeckLogging()
    .UseLocalization(Strings.LocalizationCatalog)
    .RegisterIntegration<PluginIntegration>()
    .Build();

await plugin.RunAsync();
```

`RegisterIntegration<T>()` is the one door: it registers the integration's actions plus a capability
handler for every SDK interface the type implements. The integration is built by DI, so it can take
`IHttpClientFactory`, `IOptions<T>`, Serilog's `ILogger`, `PluginMetadata` or `IPluginCatalogNotifier`
in its constructor. Anything else the container needs goes on `builder.Services` before `Build()`.

`Build()` constructs every integration as part of validation, and `InitializeAsync` does not run at
process start - it is gated on the session. That is why `SoundManager` is a *lazy* singleton: the audio
device is opened on the first play, never during construction or initialization, so the plugin works on
machines without audio and every play action can fail honestly when there is no device.

### `SoundManager`

- One engine per plugin, one `SoundPlayer` per file in flight. A player is released from the mixer and
  disposed when its `PlaybackEnded` event fires; `stop-all` releases every player at once.
- Each file is decoded to the output device's format. Passing the device `AudioFormat` to the provider
  makes the codec resample during decoding (FFmpeg resamples cleanly) instead of leaving the file at its
  native rate - a 24 kHz source on a 48 kHz device otherwise plays through a low-quality resampler.
- Files are cached in memory for the length of the playback and disposed with their track, so a pressed
  button does not re-open the file, and Loop repeats already-decoded samples.

### The manifest

`manifest.json` is the plugin's identity, read from the content root at startup. `Build()` validates it
and fails fast on an invalid id, a missing name or version, or an unreadable icon.

```json
{
  "manifestVersion": 1,
  "id": "com.opencode.sound-effects",
  "name": "Sound Effects",
  "version": "1.0.0",
  "description": "A soundboard plugin: plays your audio files from Macro Deck buttons.",
  "icon": "Assets/icon.svg",
  "entrypoints": {
    "win-x64": {
      "executable": "runtimes/win-x64/SoundEffects.dll",
      "runtime": { "kind": "FrameworkDependent", "dotnetVersion": "10.0" }
    },
    "osx-arm64": {
      "executable": "runtimes/osx-arm64/SoundEffects.dll",
      "runtime": { "kind": "FrameworkDependent", "dotnetVersion": "10.0" }
    },
    "linux-x64": {
      "executable": "runtimes/linux-x64/SoundEffects.dll",
      "runtime": { "kind": "FrameworkDependent", "dotnetVersion": "10.0" }
    }
  },
  "publisher": { "name": "opencode" },
  "license": "MIT",
  "repository": "https://github.com/opencode/MacroDeck-sound-effects",
  "compatibility": { "macroDeck": ">=3.0.0-0" }
}
```

Adding a platform means importing it in both `entrypoints` **and** `macrodeck-build.json`; mixing the
two fails validation. Each entrypoint lives under `runtimes/<rid>/` so a multi-platform artifact cannot
collide with itself. `win-arm64` falls back to `win-x64`, `osx-arm64` falls back to `osx-x64`; there is
no `"any"` key.

### Localization

Every string a user reads comes from `Localization/Strings.resx`, never a literal. The
`MacroDeck.Plugin.Analyzers` source generator turns that folder into a typed `Strings` class whose
members return a `LocalizedString` - a *reference*, not text - and the host resolves it for whoever is
reading it, so changing the app language needs no plugin rebuild.

```csharp
public LocalizedText Name => Strings.Actions.Play.Name();

public IReadOnlyList<ActionParameter> Parameters { get; } =
[
    ActionParameter.File(
        "file",
        label: Strings.Parameters.File.Label(),
        description: Strings.Parameters.File.Description(),
        required: true),
];
```

A dotted key becomes a nested class, so `Actions.Play.Name` in the resource file is
`Strings.Actions.Play.Name()` in code. Generic strings reuse `MacroDeckStrings`, the catalog Macro Deck
already ships translated; the actions compose it when a required file is left blank:

```csharp
ActionResult.Failed(
    ActionErrorCodes.InvalidParameter,
    MacroDeckStrings.Validation.Required(Strings.Parameters.File.Label()));
```

A translation is `Localization/Strings.<culture>.resx` with a well-formed BCP-47 name. Resolution falls
back requested culture → neutral culture → catalog default → English, so a half-finished translation
degrades gracefully; a key no culture carries renders as `[[plugin:<id>:Key]]` so a gap stays visible.
The manifest's `languages` array is derived by `macrodeck-plugin build` and `pack` from this folder;
never edit it by hand.

## The template behind this repository

This repository is simultaneously the plugin *and* the content of the `dotnet new macrodeck-plugin`
template package - the `src/` tree is what generation reproduces, byte for byte, for the parameters you
pass. If you want a fresh plugin instead of this one, generate from the template:

```bash
dotnet new install MacroDeck.Plugin.Templates@*-*
```

`@*-*` installs the newest published version. The floating form is what you want while the 3.0 template
is in preview: `dotnet new install` picks stable versions by default, and there is no stable release yet.

```bash
dotnet new macrodeck-plugin -n Acme.LightControl --pluginId com.acme.light-control --pluginName "Acme Light Control" \
  --publisher "Acme Inc" --repository https://github.com/acme/light-control \
  --platforms win-x64 --platforms osx-arm64 --platforms linux-x64
```

| Parameter | Default | What it sets |
| --- | --- | --- |
| `-n`, `--name` | `MacroDeckPlugin` | The project, namespace, solution and the executable names in `manifest.json` |
| `--pluginId` | `com.example.my-plugin` | The manifest `id`: reverse-domain, lowercase, at least two dot-joined kebab segments |
| `--pluginName` | `My Plugin` | The display name Macro Deck shows |
| `--publisher` | `Example Publisher` | `publisher.name` |
| `--description` | `A minimal Macro Deck 3 plugin.` | `description` |
| `--license` | `MIT` | `license`, as an SPDX identifier |
| `--repository` | `https://github.com/example/my-plugin` | `repository`: the GitHub repository the plugin is built from. The Store refuses the placeholder |
| `--homepage` | *(omitted)* | `homepage`. Left out of the manifest entirely when not supplied |
| `--platforms` | `win-x64`, `osx-arm64`, `linux-x64` | Which runtime identifiers land in `entrypoints` and `macrodeck-build.json`. Repeat the option per platform; `win-arm64`, `osx-x64` and `linux-arm64` are also available |

`--homepage` is omitted rather than written empty on purpose: the manifest schema requires an absolute
URL, so `""` would fail validation. `repository` is always written because the Store refuses an upload
without it. The `macrodeck-plugin new` wizard collects the same values and passes them straight through.
The template ships with an example action that shows the localized shape of an action end to end;
replace it the way this plugin replaced its own. Worked examples of every capability grow the
[sample plugins repository](https://github.com/Macro-Deck-App/Macro-Deck-Sample-Plugins), not this one.

### Building against a local SDK build

The template tracks the SDK's *published* packages and floats to the newest one, so a plain
`dotnet build` resolves the latest release. While a change is still unreleased, pack the SDK from a
Macro Deck 3 checkout into this repository's `local-feed/` and build against that version:

```bash
dotnet pack MacroDeck.slnx -c Release -p:Version=3.0.0-local.1 -o <path-to-this-repo>/local-feed
```

```bash
dotnet build -p:MacroDeckSdkVersion=3.0.0-local.1
```

`NuGet.config` already lists `local-feed/` as a package source, and `MacroDeckSdkVersion` sets the
version for every Macro Deck package at once (see `Directory.Packages.props`). Pick a version that
cannot collide with a real release - `3.0.0-local.N`, never a published preview version, which would put
a hand-built package into the global NuGet cache under the name of a published one.

### Publishing to the Store

The Store gate is checked before *every* change in this repository; see [AGENTS.md](AGENTS.md) for the
full six-point check. Before your first upload, at least: `publisher.name` must be the owner the plugin
is published under in the Creator Portal, `repository` must be the GitHub repository the build is
released from, and a conformance report must accompany the artifact for every declared platform.

## Licences

- This plugin: [MIT](LICENSE). Macro Deck itself is licensed under Apache 2.0.
- [SoundFlow](https://github.com/kantoniak/SoundFlow): MIT.
- `SoundFlow.Codecs.FFMpeg` (the FFmpeg decoder this plugin bundles): LGPL. The packaged artifact
  includes its third-party notices.

## Further reading

- [Plugin development docs](https://github.com/Macro-Deck-App/Macro-Deck-3/tree/main/docs/plugin-development)
- [Sample plugins](https://github.com/Macro-Deck-App/Macro-Deck-Sample-Plugins) - a worked example per capability
- [`plugin-hosting.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/plugin-hosting.md) - the builder API, registration modes, the artifact format and every `MACRO_DECK_PLUGIN_*` variable
- [`sdk-reference.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/sdk-reference.md) - every interface and record you build against
- [`cli.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/cli.md) - every CLI command and option
- [`testing-plugins.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/testing-plugins.md) - the test harness, the fakes and the manual clock
- [`conformance.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/conformance.md) - the conformance suite and its check ids
- [`analyzers.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/analyzers.md) - the compile-time diagnostics
- [Macro Deck localization guide](https://docs.macro-deck.app/sdk/localization/)