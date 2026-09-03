# High-frequency rendering in WPF

How to build animation and continuous-repaint work in these apps: scrolling, but equally any
timeline, live chart, dragged overlay, or anything else that redraws every frame.

Everything here was measured on RaisinDocs and RaisinTerminal2 between 2026-08 and 2026-09-03,
mostly by getting it wrong first. Where a number appears, it came from a log.

---

## 1. What WPF actually does with a frame

Four stages, and confusing them is the root of most of the mistakes below.

| Stage | Thread | Can you measure it? |
|-------|--------|---------------------|
| `OnRender` builds a display list | UI | Yes - a `Stopwatch` around it |
| The display list is rasterised | WPF render thread | **No** - nothing managed reaches it |
| DWM composes the desktop | DWM | No |
| The panel presents | GPU/display | Indirectly, via frame stamps |

`OnRender` does not draw. It records drawing instructions. A `Stopwatch` around it measures how
long it took to *describe* the frame, which is usually a small fraction of what it costs to
*produce* it. Cheap `OnRender` and dropped frames are entirely consistent - that combination is
the normal case for text-heavy content.

WPF's milcore renders through **Direct3D 9Ex** (`d3d9.dll` is loaded, `dxgi.dll` and `d3d11.dll`
are not), blt-model, no flip model, no waitable swapchain. There is no supported way to change
that; a DirectX upgrade is "long-term vision" on the WPF roadmap with no dates. `RenderTargetBitmap`
is software-only through WIC and never touches the GPU (dotnet/wpf#9021).

Practical consequence: **you cannot make WPF present faster than it wants to.** You can only
give the render thread less to rasterise, and avoid wasting the frames it does give you.

---

## 2. CompositionTarget.Rendering

The only sane clock for per-frame work, and full of traps.

### It free-runs if you invalidate from inside it

Repainting inside the handler makes WPF schedule another render pass, which raises `Rendering`
again immediately. Measured at ~500 callbacks a second, with bursts to 3000Hz. Each one costs an
`OnRender`, so the UI thread saturates, the message pump starves, and Windows starts coalescing
input - 276 wheel notches arrived as 83 messages, half carrying 2 to 12 notches each, every one
becoming an oversized velocity impulse. The scroll then lurches, and it looks like a physics bug.

### Use the frame stamp, not a clock

`RenderingEventArgs.RenderingTime` is the composition engine's frame stamp. It **repeats** when
the event fires more than once for the same frame, and can regress when the UI and render
threads desync. That repetition is exactly what identifies a free-run duplicate:

```csharp
var stamp = (e as RenderingEventArgs)?.RenderingTime ?? TimeSpan.MinValue;
bool newFrame = stamp == TimeSpan.MinValue || stamp != _lastRenderingTime;
_lastRenderingTime = stamp;
```

Paint only on `newFrame` and you get one repaint per composed frame, at the compositor's cadence,
and that repaint drives the next one.

### Never gate a repaint on wall-clock alone

Against a free-running loop, a wall-clock threshold fires on whichever tick first crosses it, so
the repaint lands within one tick of the intended time - about 2ms - at a **different phase every
frame**. Each repaint then independently catches or misses a composition deadline. That is what
"jagged scroll" looked like.

If you also need to cap the rate (see §4), the frame stamp must still decide *when* to paint and
the interval only *which frames to skip*. Then the phase comes from the compositor, not from your
accumulator.

### A frame being new must never decide whether the animation is finished

This shipped as a bug. The duplicate-frame guard returned from the callback outright:

```csharp
if (elapsed <= 0) return;   // WRONG - skips CheckStop()
```

An animation whose settling frame happened to be a duplicate never stopped: it stayed flagged as
animating for ever, so every later `Start()` no-opped and the scroller was inert until something
called `Cancel()`. It hid well - a click or keypress cancels, so the next interaction cleared it,
and the visible symptom was an animation ending a fraction early. It surfaced only because
gesture diagnostics stopped recording: eight gestures in one run, then one, then none.

**Whether a frame is new decides what to redraw. It must not decide whether you are done.**

### Skipping a repaint does not stall the loop

`Rendering` keeps firing while a handler is attached, whether or not you invalidate. Measured:
3710 callbacks against 1236 repaints in one 21-second gesture. So capping repaints is safe.

---

## 3. Integrating motion

### Use the closed-form integral, not forward Euler

For velocity decaying as `v0 · e^(-D·t)`, the distance across a frame is **not** `v · dt`. Moving
at the start-of-frame velocity for the whole frame overshoots by `D·dt / (1 - e^(-D·dt))`: 2% at
a 5ms frame, 27% at a 50ms one. That makes how far a gesture travels depend on how fast the
machine happens to be drawing.

```csharp
double decay   = Math.Exp(-dt * Damping);
double deltaPx = velocity * (1 - decay) / Damping;   // exact at any dt
velocity *= decay;                                   // same exp, reused
```

### Never quantise decay into fixed steps

Exponential decay composes: `exp(-a) · exp(-b) == exp(-(a+b))`. So decaying by the real elapsed
time gives *exactly* the same value at every boundary a fixed-step loop would have landed on, and
a smooth value in between. A loop applying one 1/60 step per frame's worth of accumulated time
moved in 60 discrete jumps a second however fast the display refreshed - visible stepping on
anything above 60Hz. Removing it took a scrollbar drag from 60 to 195 paints a second.

### Clamp long frames, never drop them

Dropping a frame whose `dt` is large loses the motion it represented, which reads as a stall
followed by a jump. Clamping costs precision on that one frame and nothing else:

```csharp
if (dt <= 0) return;                          // duplicate frame stamp
if (dt > MaxFrameDelta) dt = MaxFrameDelta;   // 0.05 works well
```

### Name your thresholds

A stop threshold borrowed a damping constant's value - 10 - because both were "about the right
size". That made a coast crawl on at 10px/s, and since the renderer rounds to whole pixels the
image changed 10 times a second at the end of every gesture, on a panel showing 280. A named
`SnapVelocity = 40` ended it sooner. Constants that mean different things must not share a value
by accident.

---

## 4. Multi-monitor: WPF does not pace a window to the display it occupies

**This is the single most surprising finding, and it silently wastes most of your frames.**

With an app started on a 280Hz display and its window dragged to a 60Hz one, composition frames
kept arriving at 320-357 a second, and a control repainting once per composed frame drew **279
frames a second for a panel that shows 60**. Four in five were composed and discarded. In 9.55
seconds of scrolling that cost 46 gen0, 20 gen1 and 17 gen2 collections.

The frame stamp carries the *compositor's* rate, not the *panel's*. Pacing to
`CompositionTarget.Rendering` alone is therefore not enough: you must ask which display the
window is on and cap against it.

`WindowDisplayInfo` does this. After capping, with the window fully on each panel:

| Panel | Paints/sec before | after |
|-------|------------------|-------|
| 280Hz | 271 | 267 |
| 144Hz | 279 | 136 |
| 100Hz | 279 | 96 |
| 60Hz  | 279 | 54 |

Scrolling looks identical - the panel could never have shown the skipped frames.

### Detecting a move

`WM_WINDOWPOSCHANGED` is the only message that catches it. `WM_DPICHANGED` fires only when the
two displays scale differently, so two panels at 100% produce nothing. `WM_DISPLAYCHANGE` is
about the configuration changing, not the window moving - handle it too, for someone changing a
refresh rate while the app runs.

Recompute *which* monitors the window touches on every move (cheap), and only query refresh rates
when that set changes (not cheap).

### A window spanning two displays

Take the **fastest** of the intersecting monitors, not the one holding most of the window. A
window 51% on a 60Hz panel and 49% on a 280Hz one is presented on both; pacing to the slower
degrades the half on the faster, which is the half someone dragging a window between monitors is
looking at. Overshooting on the slow half only wastes work.

### Per window, not per app

One instance per top-level window, keyed by `HwndSource` and held weakly. An app with floating
panes gets one per window, and a pane dragged onto a slow monitor slows down while the docked
panes keep their own rate. Re-resolve on every `Loaded`: floating a pane moves the view into a
different top-level window, which raises `Unloaded`/`Loaded` again.

### Push the value; don't hand out a callback

`SmoothScroller.DisplayPeriod` is a plain `double` that the host sets. It was briefly a
`Func<double>`, which was wrong three ways: it is a *pull* for something that changes by *push*,
it does work at the one moment latency matters, and a closure capturing a window is how a
long-lived animator keeps that window alive.

Route the *value* by direct reference, and use the event bus only to *announce* the change
(`DisplayChangedArgs`, carrying the HWND so subscribers can filter). A bus is the wrong transport
here: a control created later would have no current value, and `EventSystem.Invoke` puts a
`SynchronizationContext` hop between a display change and a read inside a render loop.

---

## 5. What actually limits the composition rate

Rasterisation load, not window area and not `OnRender` cost. Measured on one panel, one filled
terminal, only the window size changing:

| Window | Content width | Composition | `OnRender` | Frames late |
|--------|---------------|-------------|-----------|-------------|
| 2540x1400 | 2020px | 140/s | 1.40ms | 25% |
| 1800x1200 | 1280px | 140/s | 0.77ms | 2% |
| 1200x900 | 680px | 140/s | 0.34ms | 0% |
| 760x560 | 240px | **280/s** | 0.06ms | 2% |

`OnRender` never came close to the 3.57ms budget, yet composition halved. Note the shape of the
failure: at 1200x900 it sits at 140/s with *zero* late frames - steady at half rate, not
straining. At 2540x1400 it misses a quarter of its frames *while already at 140*, so it is headed
for the next divisor.

A panel presents on exact divisors of its rate - 280, 140, 93, 70. Seeing your frame interval
land on one of those is the signal that the render thread is missing deadlines.

**If you need more frames, give the render thread less to rasterise.** Fewer or simpler visuals,
smaller dirty regions, less overdraw. Nothing you do on the UI thread will help.

---

## 6. Whole-pixel stepping

If the renderer positions content at whole pixels, the image changes only when the offset crosses
a boundary. At V pixels a second the image changes V times a second *however often you paint*. So
the end of any decaying motion looks stepped no matter how good the pacing is - and every
worst-case frame gap we ever measured fell in that tail, not mid-gesture.

Painting only when the rounded position changes is therefore correct **and** an optimisation -
but only if your renderer really does round. Check before copying it: a renderer using a snapshot
cache or a sub-pixel reference offset can move the image without the rounded value changing, and
the same gate would drop real frames. RaisinDocs has this gate; RaisinTerminal2 deliberately does
not.

The real fix is sub-pixel positioning, which needs a renderer you control. Ending the motion
sooner (`SnapVelocity`) shortens the visibly stepped stretch without fixing it.

---

## 7. Measuring it

**Everything above that turned out to be true was measured. Everything reasoned from first
principles was wrong.** Refuted this way: "the minimap is the bottleneck" (turning it off changed
nothing), "GC pauses cause the gaps" (a gesture with zero collections had the same gaps), "60Hz
constants break on a 280Hz panel" (the 280Hz panel had the cleanest cadence of the three tested),
and "window area sets the composition rate" (it was the amount of text).

`ScrollGestureRecorder` in this library records a gesture's frame and paint intervals, pixel
steps, GC counts and per-piece costs. Both apps expose it behind `--scroll-diag`.

Hard-won rules for any diagnostic of this kind:

- **Write a marker when recording starts.** Otherwise "diagnostics off", "nothing happened" and
  "the work never finished" all look like an empty file. Three test rounds went on telling those
  apart.
- **Record everything; never suppress small samples.** A threshold discarding short gestures
  discarded exactly the short gestures a slow panel produces. Say "too few samples" in the output
  instead of writing nothing.
- **Record facts, don't infer them.** Log the display's *device name*, not just its rate: the
  failure you are hunting produces the old rate on the new monitor, so inferring the monitor from
  the rate assumes the answer.
- **Measure Release.** Debug inflated costs about threefold - `OnRender` 0.30ms Debug against
  0.07ms Release - which buries exactly the differences you are looking for.
- **Don't allocate when disabled.** `Time(label, () => work())` builds a closure before it can
  check whether it is enabled - hundreds a second in a render loop. Branch at the call site, or
  use `Record(label, ms)` for code that already times itself.
- **Look at the screen.** Three separate conclusions were drawn from indirect signals - a cost
  spike "proving" that input had landed, a gesture count "proving" content existed - and all
  three were wrong. A screenshot settled each one in a single look.

### Automating UI tests

For anything driving the app from outside:

- `SetForegroundWindow` is **refused** unless the calling process is already in the foreground.
  It fails silently. Use `AttachThreadInput` to the foreground thread around the call, and then
  verify with `GetForegroundWindow`.
- WPF routes `WM_MOUSEWHEEL` to whatever is under the **physical cursor** and ignores the
  coordinates in a posted message. Move the real cursor.
- Synthetic keyboard input never reached RaisinTerminal2's terminal, by either
  `KEYEVENTF_UNICODE` packets or real virtual keys via `VkKeyScan`. Design tests that do not need
  typing.
- Aim at the control, not the window centre - docked panels take fixed widths, so at small window
  sizes the centre is a panel.
- Run a second instance with a flag that separates its state (`--dev` in RaisinTerminal2: separate
  mutex, settings, sessions and data directory) so tests never touch the copy someone is using.
- Terminal history does not reflow on resize. Fill at the widest size and measure downward, or
  every smaller window is measured with blank space where text should be.

---

## 8. Approaches that did not work

Recorded so nobody spends the time again.

- **A second renderer (Direct2D/DirectWrite) presenting alongside WPF.** The presenter itself was
  fine - a paced swapchain held 280/s with zero late frames, and DirectWrite drew a line in
  3.4µs. The *hybrid* failed: two renderers sharing one surface produced a visible seam,
  hand-off flicker, and freezes. Abandoned after ~4200 lines. Kept on
  `scroll-presenter-prototype` in RaisinDocs.
- **Per-line `BitmapCache` to make scrolling cheaper.** It works, but ClearType needs an opaque
  backdrop and a cached layer does not have one, so text visibly degrades to greyscale
  antialiasing. `RenderOptions.ClearTypeHint` overrides WPF's check without supplying a backdrop.
- **`RenderTargetBitmap` for cheap snapshots.** Software-only, ~20.8ms for a real canvas, flat in
  area. Not a shortcut.
- **Patching or configuring WPF's renderer.** It is D3D9Ex with no upgrade path, and its vsync
  locking paces via a `DispatcherTimer` estimate with a +1ms fudge.

---

## 9. Checklist for new per-frame work

1. Drive from `CompositionTarget.Rendering`; treat a repeated `RenderingTime` as a duplicate.
2. Repaint only on a new frame, and only when the output would actually differ.
3. Never let duplicate-frame handling skip your completion check.
4. Integrate with the closed-form solution; clamp long frames rather than dropping them.
5. Get the display period from the window's `WindowDisplayInfo` and cap repaints to it. Subscribe
   to `Changed`; re-resolve on `Loaded`.
6. Take a value, not a callback, and let the host push it.
7. Assume the render thread, not your code, sets the ceiling - and check by varying how much
   there is to draw, not how much code runs.
8. Instrument before theorising, measure in Release, and look at the screen.
