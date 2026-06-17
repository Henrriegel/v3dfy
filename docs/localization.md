# Localization design

## Purpose

v3dfy currently supports English and Spanish through hardcoded two-language
pairs. This document defines the target file-based localization architecture.
P10A was the audit and design checkpoint. P10B added the first real foundation:
bundled JSON files, a loader/service, language metadata discovery, and per-key
English fallback. In short, P10B implements per-key English fallback. It still
does not migrate the full UI.

P10C connects the WPF language selector to the localization service and starts
the visible migration with shell, sidebar, common action labels, Settings modal
chrome, and shared modal footer labels. It intentionally leaves most Image and
Video feature text on the legacy helper until P10D/P10E.

P10D migrates Image conversion feature text to the file-based localization
files. The migrated Image scope includes workflow selection, Step 1/Step 2/Step
3 labels, 2.5D/Parallax setup, Stereoscopic image setup, Image model help,
Image output/result/readiness text, Image-specific buttons/tooltips, key-backed
Image option labels, and ViewModel-authored Image logs/progress. P10D does not
migrate Video feature text or unrelated installer/documentation text.

P10E migrates Video conversion feature text to the same file-based
localization files. The migrated Video scope includes Home Video card/status
entry text, source selection, analysis, recommended setup/profile summaries,
setup/output labels, preview required/ready/accepted/outdated UI, preview player
labels, final conversion/status/output labels, Video-specific buttons/tooltips,
key-backed Video option labels, and ViewModel-authored Video logs/progress that
can be resolved safely at creation time.

P10F completes the desktop-app localization hardening pass. The legacy
`Text("English", "Spanish")` helper is removed from `MainWindowViewModel`.
Remaining user-facing app text is routed through `LocalizationKeys` and the
bundled JSON files, or through a ViewModel mapper that converts DTO status,
summary, readiness, progress, and output-open messages to localization keys.

The implementation must start with English and Spanish, but it must allow more
languages to be added later by adding bundled JSON files rather than redesigning
the app.

Do not suggest a commit until the localization/i18n migration is fully
functional at 100%.

## Current audit summary

Current user-facing text is spread across several patterns:

- XAML hardcoded text: `MainWindow.xaml` is mostly bound to ViewModel
  properties. P10D moved Image-specific badge labels such as `MP4 1080p` and
  `Loop-friendly motion` behind ViewModel localization keys.
- ViewModel hardcoded text: `MainWindowViewModel.cs` contains the majority of
  visible text.
- Two-language helper calls: `MainWindowViewModel.Text("English", "Spanish")`
  is used hundreds of times. `SelectedLanguage` is a string and `IsSpanish`
  decides which value is returned.
- Localized option models: `LocalizedOptionViewModel<T>` supports both legacy
  English/Spanish display names and key-backed labels resolved through the
  localization service. P10D uses key-backed labels for Image option lists.
- Logs and progress messages: `LogEntryViewModel` stores message pairs for
  legacy compatibility. P10D/P10E ViewModel-authored Image and Video log lines
  are generated from localization keys and stored as resolved text at creation
  time, so existing log entries remain as written after a language switch.
- Errors and blockers: readiness, missing-tool, engine, model, process, and
  conversion failures are also mostly hardcoded as English/Spanish pairs.
- Technical details: model keys, file paths, command lines, module names,
  ffmpeg/iw3 option names, and runtime diagnostics are mixed with user-facing
  labels. The technical values themselves should generally not be translated.
- Packaging and docs text: installer and documentation text can be localized in
  later phases, but P10B-P10F should focus on the desktop app first.

This works for two languages, but it does not scale to a third language because
each new string requires another code change and review inside ViewModels or
XAML.

## Target file layout

Localization files should live under the app project:

```text
src/V3dfy.App/Localization/
  en.json
  es.json
```

P10B created these initial files with a small seed set of common keys. P10C
expands the seed set for shell/common/settings/modal chrome. Later phases will
continue expanding them as Image and Video text is migrated.

Future languages should be addable by placing more JSON files in the same
folder and bundling them with the app:

```text
src/V3dfy.App/Localization/fr.json
src/V3dfy.App/Localization/de.json
src/V3dfy.App/Localization/pt-BR.json
src/V3dfy.App/Localization/ja.json
```

Runtime must load these files from the published/installed app layout. The app
must not download language files, require external language folders, or depend
on internet access.

The core loader/service is implemented in `V3dfy.Core.Localization` so it can be
tested without coupling tests to WPF. The runtime files remain app-local under
`src/V3dfy.App/Localization/`.

## JSON shape

Use one JSON file per language. Each file includes metadata and a flat string
table keyed by stable localization keys.

```json
{
  "meta": {
    "code": "en",
    "displayName": "English",
    "nativeName": "English",
    "culture": "en",
    "fallback": "en",
    "visible": true
  },
  "strings": {
    "App.Title": "v3dfy",
    "Common.Close": "Close",
    "Common.OpenFolder": "Open output folder",
  "Image.Parallax.DepthIntensity.Label": "Depth intensity",
  "Image.Log.ParallaxFrames": "Parallax frames: {current} / {total}"
  }
}
```

Spanish starts as:

```json
{
  "meta": {
    "code": "es",
    "displayName": "Spanish",
    "nativeName": "Español",
    "culture": "es",
    "fallback": "en",
    "visible": true
  },
  "strings": {
    "App.Title": "v3dfy",
    "Common.Close": "Cerrar",
    "Common.OpenFolder": "Abrir carpeta de salida",
    "Image.Parallax.DepthIntensity.Label": "Intensidad de profundidad",
    "Image.Log.ParallaxFrames": "Fotogramas parallax: {current} / {total}"
  }
}
```

Notes:

- Files should be UTF-8. Native names may use accents when the file is created
  as UTF-8 and the test suite verifies parsing.
- A flat string table keeps lookup fast and reviewable. Namespaces in keys
  provide structure without requiring nested JSON traversal.
- JSON comments are not supported; translation notes should live in docs or a
  future sidecar file if needed.

## Key naming

Keys should be stable identifiers, not English sentences. Use namespaces by
feature and reuse common keys for shared actions.

Examples:

- `App.Title`
- `Sidebar.Home`
- `Sidebar.ImageConversion`
- `Sidebar.VideoConversion`
- `Common.Close`
- `Common.Cancel`
- `Common.Convert`
- `Common.OpenFolder`
- `Settings.Title`
- `Settings.Models.Title`
- `Settings.ToolsEngine.Refresh`
- `Image.Step.Setup.Title`
- `Image.Parallax.DepthIntensity.Label`
- `Image.Parallax.MotionDirection.Label`
- `Image.Stereo.OutputFormat.Label`
- `Video.Preview.Required.Title`
- `Image.Log.WorkflowReady`
- `Image.Log.ParallaxFrames`
- `Image.Error.ExportFailed.Format`

Policy:

- Use `Common.*` only for labels with identical intent across features.
- Use feature-specific keys when the same English word has different context.
- Keep feature log keys under the feature namespace, such as `Image.Log.*`, and
  feature blockers/errors under feature namespaces such as `Image.Readiness.*`
  and `Image.Error.*`.
- Keep technical model keys, command switches, file paths, codecs, and runtime
  identifiers untranslated unless there is a separate user-facing label.
- Model display names from local model metadata may remain data-driven. Do not
  invent translations for model names unless the model catalog supplies them.

## Placeholders

Formatted strings should use named placeholders when possible because they are
easier to translate and reorder than positional placeholders.

Examples:

```json
{
  "Output.GeneratedFile": "Generated file: {path}",
  "Logs.Image.ParallaxFrames": "Parallax frames: {current} / {total}",
  "Logs.Image.DepthIntensityChanged": "Depth intensity changed: {old} -> {new}"
}
```

Positional placeholders such as `{0}` are acceptable for simple legacy
interop, but new keys should prefer named placeholders. Formatting should use
the active language culture for numbers and durations when that becomes useful.

## Runtime service design

Recommended app-layer components:

- `ILocalizationService`: lookup API for strings, active
  language state, and available language metadata.
- `LocalizationCatalog`: stores loaded language metadata and string tables.
- `JsonLocalizationCatalogLoader`: loads and validates app-local JSON files from the
  `Localization` folder.
- `JsonLocalizationService`: owns active language, per-key fallback resolution,
  and missing-key reporting.
- `LocalizationKeys`: constants for the seed keys and future key expansion.
- `MissingLocalizationReporter`: aggregates missing file/key diagnostics for
  status panels and tests.

P10B implements the pure foundation classes in `src/V3dfy.Core/Localization/`.
P10C adds `AppLanguageOptionViewModel` and wires `MainWindowViewModel` to the
service while keeping the legacy `Text("English", "Spanish")` helper for
unmigrated strings.

Development and runtime loading:

- In source/development builds, load from `src/V3dfy.App/Localization` when
  running from the project layout.
- In published/installed builds, load from
  `<app root>/Localization/*.json`.
- The runtime lookup path must be app-local. It must not use
  `C:\v3dfy-iw3-intake`, user profile language folders, internet downloads, or
  PATH-dependent tools.

Language discovery:

- Enumerate `Localization/*.json`.
- Parse `meta`.
- Include files with `visible: true` in the selector.
- Sort by display name or an optional future `sortOrder`.
- Select the persisted language code if present and valid.
- Fall back to `en`.

P10C selector behavior:

- `LanguageOptions` is built from localization metadata rather than a hardcoded
  two-item string list.
- The ComboBox displays each option label from metadata and stores the selected
  language code.
- Selecting Spanish activates `es`; selecting English activates `en`.
- Future bundled files such as `fr.json` can be discovered by the service
  without redesigning the selector architecture.

ViewModel and XAML updates:

- ViewModels should expose localized properties by key, for example
  `T(LocalizationKeys.Common.Close)`.
- On language change, raise property changes for visible localized properties
  without resetting workflow state.
- XAML should continue to bind to ViewModel properties unless a future markup
  extension is introduced. A ViewModel-first approach keeps tests simple and
  avoids WPF resource churn.
- Option lists should store language-neutral values and localization keys, not
  English/Spanish pairs.
- P10D Image option lists keep language-neutral runtime values for exporter
  compatibility and resolve display labels from keys such as
  `Image.Parallax.DepthIntensity.Low` and `Image.Stereo.OutputFormat.SBS`.
- P10E Video option lists keep language-neutral runtime values such as
  `TargetDevicePreset`, `AiQualityPreset`, `ThreeDIntensity`, and
  `ThreeDOutputFormat`, and resolve display labels from keys such as
  `Video.Option.OutputProfile.Recommended`,
  `Video.Option.Quality.Balanced`, and
  `Video.Option.OutputFormat.HalfTopBottom`.

P10C migrated scope:

- app title/tagline
- sidebar Home/Image conversion/Video conversion/settings labels
- sidebar expand/collapse label
- common Back/Next/Convert/Browse/Refresh/Clear/View models style actions
- Settings modal title and section chrome
- language/theme labels in Settings
- shared modal close/cancel/copy-full-log/open-output-folder/view-models labels

P10D migrated scope:

- Home Image conversion card/status entry-point labels
- Image conversion section title and introduction
- Image workflow selection cards and summary/change controls
- Image Step 1 source/analyze/metadata labels
- Image Step 2 setup labels for 2.5D/Parallax and Stereoscopic workflows
- Image Step 3 preview/export, output/result/readiness labels
- Image model selector/help labels and Image-specific help suffixes
- Image-specific buttons, badges, tooltips, warnings, and errors generated in
  the app ViewModel
- Image setup option labels for parallax depth, motion, zoom, duration,
  smoothing, layer behavior, stereo output format, eye separation, convergence,
  swap-eyes, and anaglyph mode
- ViewModel-authored Image activity log entries and setup-change log entries
  that can be resolved safely at log creation time

P10E migrated scope:

- Home Video conversion card/status entry-point labels
- Video conversion section subtitle and wizard step labels
- Video source selection, source dialog labels, analysis status, and analysis
  result labels
- Recommended setup/profile summary, model selector/help text, estimate labels,
  setup labels, output path labels, LG compatibility labels, and conversion
  plan labels
- Preview required/ready/accepted/outdated labels, preview time range labels and
  validation text, preview player labels/tooltips, preview metrics labels, and
  preview modal text
- Final conversion progress/status/summary/readiness labels and completion
  modal text
- Video-specific buttons, badges, tooltips, warnings, and errors generated in
  the app ViewModel
- Video setup option labels for output profiles, quality, 3D intensity, 3D
  layout, confidence labels, and profile detail text
- ViewModel-authored Video activity log entries, setup-change log entries,
  preview cleanup logs, conversion cleanup logs, output-path logs, and analysis
  error logs that can be resolved safely at log creation time

P10F migrated/finalized scope:

- remaining Home, Settings, model inventory, model-pack import, technical
  details, activity-log chrome, profile details, system/tool status, and modal
  text
- context-menu copy/select-all labels in `MainWindow.xaml`
- Image exporter readiness/progress/result DTO text through ViewModel mapping
- Video recommendation compatibility notes through ViewModel mapping
- preview gate/detail, conversion start-gate, conversion readiness, conversion
  execution status/detail, preview/conversion result summaries, runtime-download
  warnings, and output-open warnings through ViewModel mapping
- process metric display text, preview metric status text, iw3 CLI capability
  diagnostic labels, and preview/conversion operation warnings through
  localization keys
- publish validation checks for bundled localization files

Remaining non-translatable technical values:

- raw file paths, output paths, command lines, stdout/stderr lines, environment
  variable names, command switches, codec names, file extensions, model file
  names, model identifiers, JSON field names, and runtime identifiers such as
  FFmpeg, FFprobe, iw3, Python, CPU, GPU, RAM, and VRAM
- engine diagnostic lines may preserve raw technical detail text when the value
  is intended for troubleshooting rather than prose UI. The surrounding visible
  app labels and known status/warning messages are localized.

Logs:

- Future log entries should generally store the resolved display string at the
  time the log line was written, plus optional structured metadata for
  diagnostics.
- Existing logs should not be retroactively translated after a language switch
  unless a future UX decision explicitly requires it. This preserves the exact
  operator-visible history.
- P10D follows that policy for Image logs: new ViewModel-authored Image log
  lines are generated from `Image.Log.*` keys and stored as resolved text at
  creation time. Existing Image log entries remain as written.
- Image exporter result summaries, progress updates, and readiness issue text
  still arrive from engine DTOs as English/Spanish pairs for compatibility.
  P10F maps the known user-facing messages to `Image.*` keys in the ViewModel
  before display. Raw technical details remain data.
- P10E follows the same policy for Video logs: new ViewModel-authored Video log
  lines are generated from `Video.Log.*` or `Video.Error.*` keys and stored as
  resolved text at creation time. Existing Video log entries remain as written.
- Video executor summaries, preview executor result logs, readiness issues,
  preview/conversion gate status text, output-open warnings, process metric
  labels, and iw3 CLI capability diagnostic labels are mapped to localization
  keys before display. Raw stdout/stderr and engine diagnostic lines remain
  untranslated technical data.
- The model-pack import confirmation DTO still carries English/Spanish title
  and message fields for coordinator/service compatibility. The desktop app's
  in-app confirmation modal uses the DTO `Preparation` payload and rebuilds the
  visible title/message from `ModelPack.*` localization keys.

## Fallback and missing keys

Fallback behavior must be deterministic and per missing key:

1. If the selected language file is missing or invalid, fall back to English.
2. If the selected language is missing a key, fall back to the English value for
   that key.
3. If English is missing the key, return a visible diagnostic fallback:
   `[Missing: Key.Name]`.
4. Missing files or keys should be reported through diagnostics and tests.
5. Normal runtime should not crash because one translation is missing.

Per-key fallback means the selected language does not switch wholesale to
English just because one key is missing. For example, if the active language is
Spanish and `es.json` contains `Common.Close` but not `Common.OpenFolder`, then
`Common.Close` returns `Cerrar`, `Common.OpenFolder` returns the English value,
and the active language remains `es`.

This also applies to P10C, P10D, P10E, and P10F migrated UI keys. A missing Spanish key
changes only that UI text to English; the active selected language remains
Spanish and other Spanish keys continue to display Spanish. For Image keys, if
`es.json` is missing `Image.Workflow.Parallax.Title`, only that label falls back
to English while other Spanish Image labels still display Spanish.
For Video keys, if `es.json` is missing `Video.Preview.Required.Title`, only
that label falls back to English while other Spanish Video labels still display
Spanish.

Once the service exists, tests should fail if bundled languages are missing
required keys or if JSON files cannot be parsed.

## Language switching state preservation

Changing language must be a UI-only change. It must not reset:

- selected video
- selected image
- selected image workflow
- selected model
- selected output settings
- prepared conversion plan
- accepted preview
- generated output state
- conversion progress
- image or video logs
- theme
- tool readiness
- model inventory
- modal state, unless a future UX decision explicitly says otherwise

Language switching may refresh visible labels, status text, option display
names, and non-persistent UI copy.

## Build, publish, and installer bundling

Localization files are runtime content and must be bundled/offline. P10B adds
this project rule to `src/V3dfy.App/V3dfy.App.csproj`:

```xml
<ItemGroup>
  <Content Include="Localization\*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
  </Content>
</ItemGroup>
```

Publish validation verifies:

- `Localization/en.json` exists in the published app root.
- `Localization/es.json` exists in the published app root.
- All bundled localization files parse.
- English and Spanish have matching key sets.
- Each bundled localization file includes metadata and a non-empty string table.
- Future additional JSON files parse and declare metadata; non-English files
  should declare a fallback path.

`scripts/validate-iw3-bundle.ps1` performs the localization publish validation
when `-BundleRoot` points at the published `engine/iw3` layout. When the same
script is run against a standalone candidate iw3 bundle, localization checks are
skipped with a warning because there is no app publish root to inspect.

The Inno installer currently includes the publish output recursively, so
localization files should be included automatically once they are copied to
publish output.

## Migration phases

- P10A: audit the current localization system and document the file-based
  architecture.
- P10B: add the localization service, JSON loading, per-key fallback behavior,
  language metadata, `en.json`, and `es.json` with a small seeded key set.
- P10C: migrate shell, common, settings, modal, and activity-log chrome text.
- P10C status: language selector integration and shell/common/settings/modal
  chrome migration are in progress; activity-log message migration remains for
  later because log behavior needs a separate compatibility decision.
- P10D: migrate Image conversion text.
- P10D status: Image conversion feature-specific ViewModel and XAML text is
  migrated to `Image.*` keys; Image option labels are key-backed; Image
  ViewModel-authored logs/progress are key-backed at creation time; engine DTO
  summaries/progress/readiness text remain documented exceptions.
- P10E: migrate Video conversion text.
- P10E status: Video conversion feature-specific ViewModel and XAML-bound text
  is migrated to `Video.*` keys; shared log chrome needed by Video uses
  `Common.*` keys; Video option labels are key-backed; ViewModel-authored Video
  logs/progress are key-backed at creation time; engine/core DTO summaries,
  progress/readiness text, and technical process output remain documented
  compatibility boundaries.
- P10F: focus on global completeness, hardcoded string detection, DTO boundary
  cleanup or mapping, diagnostics, publish validation, final manual validation,
  and dynamic language discovery.
- P10F status: the desktop-app ViewModel no longer exposes the legacy
  `Text("English", "Spanish")` helper; remaining visible app text is key-backed
  or mapped from DTO boundaries in the app layer; process metrics and iw3 CLI
  capability diagnostics use localization keys; raw technical diagnostics stay
  untranslated as data; publish validation checks the bundled localization
  files. Do not suggest a commit until the user completes visual validation and
  gives final approval.

Each phase must preserve existing visual behavior and app state.

## Test strategy

P10A uses documentation/source tests only. Later implementation tests should
cover:

- JSON parse and metadata validation.
- Available language discovery from bundled files.
- fallback to English when a selected file is missing.
- fallback to English when a selected key is missing.
- `[Missing: Key.Name]` when English is missing a key.
- completeness across bundled visible languages.
- no downloads or external runtime language dependencies.
- language switching does not reset selected video, selected image, workflow,
  model, output settings, prepared plans, generated output state, logs, theme,
  tool readiness, model inventory, or modal state.
- P10D adds Image-specific coverage for key presence in `en.json` and
  `es.json`, key-backed Image option labels, no hardcoded Image
  `Text("English", "Spanish")` pairs in the migrated Image ViewModel surface,
  per-key Spanish fallback for an Image key, and language switching that does
  not reset Image state.
- P10E adds Video-specific coverage for key presence in `en.json` and
  `es.json`, key-backed Video option labels, no hardcoded Video
  `Text("English", "Spanish")` pairs in the migrated Video ViewModel-authored
  surface outside documented DTO boundaries, per-key Spanish fallback for a
  Video key, and language switching that does not reset Video source, analysis,
  preview, output, conversion, or log state.
- P10F adds global hardcoded-text guards for the migrated app surface, matching
  `en.json`/`es.json` key coverage, `LocalizationKeys` coverage, publish
  validation script coverage, and source-level checks that DTO boundary messages
  are mapped through localization keys before display.

## Project rules for future text changes

Future UI and user-facing text work should avoid adding hardcoded strings where
practical. Once P10B provides the localization service, new user-facing text
should use localization keys and update the bundled JSON files.

Rules:

- Keep app localization files under `src/V3dfy.App/Localization/`.
- Start with `en.json` and `es.json`.
- Support future languages by adding more bundled JSON files.
- Preserve app state on language changes.
- Keep localization files bundled/offline.
- Do not download localization files at runtime.
- Do not require external runtime language folders.
- Keep paths, model keys, command lines, codecs, and runtime identifiers
  untranslated unless they have separate user-facing labels.
- Add or update localization tests when adding new keys.
