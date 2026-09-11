# Raisin.Audio

Notification sounds for a WPF app: what to play, how to find it, how often to let it through, and the settings editors that let someone choose.

## What it is

A **sound spec** is a single string stored in the app's settings. It is one of:

| Spec | Means |
|---|---|
| `""` | silent |
| `notification` | that event name in the active theme |
| `arcade/success` | that event pinned to a named theme |
| `system:Beep` | a Windows system sound (also `Asterisk`, `Exclamation`, `Hand`, `Question`) |
| `C:\path\to\file.mp3` | that file |
| `say:{symbol} filled\|\|{count} orders filled` | words spoken, singular and plural |

A **theme** is a folder of audio files. Every bundled theme carries the same 78 event names, so changing theme never invalidates a stored spec. Twelve are bundled; they land in `Sounds\<theme>\` beside the consuming app's executable.

`SoundResolver` searches, most specific first: the user's `<AppData>\sounds\<theme>\`, their `<AppData>\sounds\`, the bundled `Sounds\<theme>\`, the bundled `Sounds\`, and finally `%windir%\Media`. That order is how someone replaces one sound of a theme without forking the theme.

## Using it

```csharp
AudioLog.OnWarning = msg => myLogger.Warn(msg);   // optional, once at startup

var sounds = new SoundService(new MySoundSettings());
sounds.Play("notification");
sounds.Play(settings.OrderFilledSound, new SoundContext("AAPL", new() { ["symbol"] = "AAPL" }));
```

`ISoundSettings` is the one thing a host must supply — six properties read straight through to whatever the app persists. It is read on every call rather than cached, which is why a volume change is heard on the next chime with no notification plumbing anywhere.

Every member of `ISoundService` is safe to call from any thread. File playback marshals itself to the WPF dispatcher; with no `Application` (a test, a headless host) file sounds are skipped rather than queued onto a dispatcher that will never run, while system sounds and speech still work.

## The settings editors

`Settings/` holds `SettingItemViewModel` subclasses that plug into `Raisin.WPF.Base`'s settings framework — a per-event picker (source dropdown, then a value control that follows it), a volume slider, a theme dropdown and a voice dropdown, each with a play button. `Themes/SoundEditorTemplates.xaml` holds their rows, and `SoundSettingTemplateSelector` wires them in; it falls through to its base for every type it does not know, so an app can add editors of its own on top.

The app supplies the registry — which events exist, what they are called, what they default to — and the policy that decides when to play them. Neither belongs here.
