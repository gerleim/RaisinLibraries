# WPF presentation timing on multiple monitors

Shared findings, measured 2026-09-05/06 on a four-monitor machine (280 Hz primary, plus 144, 100
and 60 Hz panels) using RaisinDocs as the subject. They are properties of WPF rather than of that
application, and they cost real smoothness in any app that paces its own repaints.

Consumers: `RaisinDocs/design/Scroll Frame Pacing.md`,
`StockRaisin2/design/High-Frequency Rendering.md`.

## The one-paragraph version

WPF composes on a single clock derived from the primary display, and a window on any other panel
gets that clock rather than its own. If you then rate-limit your repaints with a wall-clock
interval — the obvious thing to do, and the thing to avoid — the repaint lands at a different phase
against the panel's refresh every frame, and what the panel shows is stale by anything from nothing
to a whole refresh period. Measured cost: **77% to 118% of the frame budget** on non-primary
panels, against 8-22% on the primary. Removing the limit cut it by ten to seventeen times.

## What was measured

The subject application paces a scroll animation and repaints on `CompositionTarget.Rendering`. It
capped repaints at the panel's refresh rate, on the reasoning that the panel cannot show more.

Animation error — PresentMon's `MsAnimationError`, how far displayed content is from where it
should be for the moment it is shown — with and without that cap, maximised window, quiet machine:

| panel | frame budget | rate-limited | limit removed | as share of budget |
|---|---|---|---|---|
| 60 Hz | 16.67 ms | 12.87 ms | **1.27 ms** | 77% → 8% |
| 100 Hz | 10.00 ms | 11.80 ms | **0.70 ms** | 118% → 7% |
| 144 Hz | 6.94 ms | 4.00 ms | **0.40 ms** | 58% → 6% |
| 280 Hz (primary) | 3.57 ms | 0.78 ms | 0.78 ms | 22% (unchanged) |

The primary is unaffected because its composition period and its refresh period are the same
3.57 ms, so every tick is already the right tick. Everywhere else the two differ and the limit was
aiming blind.

## Why the obvious fix is the wrong direction

The instinct — and it is written into more than one plan — is to pace each window to the refresh
rate of the display it is on. **That is precisely the configuration measured above as broken.** The
subject application already did exactly that, resolving the correct per-window rate and pacing to
it, and produced 12.87 ms of error on a 60 Hz panel.

Knowing the right *rate* is not the problem. The problem is phase: you know the panel refreshes
every 16.67 ms but not *when*, so a paint issued once per 16.67 ms lands anywhere within the
period, and drifts. Resolving the rate from the correct monitor rather than the primary makes the
number right and the behaviour no better.

## The two arrangements that work

**If you can afford to repaint on every composition tick, do that.** Nothing is stale by more than
one tick whenever the panel samples, and no phase information is needed. This is what the subject
application now does. It costs presents: about four times more than a 60 Hz panel can show, three
quarters of them never displayed. Free when a frame costs 0.01 ms; not free otherwise.

**If you cannot, keep a rate limit but align it to the panel's vblank.** Same number of repaints,
each landing on the composition tick immediately before a refresh instead of anywhere in the
period. This is the arrangement for anything whose frames are genuinely expensive — a chart, a
depth ladder — where painting on every tick is not on the table.

Note the asymmetry, because it reverses which of these looks like the refinement. Where painting
every tick is affordable, vblank alignment adds **no accuracy at all** — the frame the panel shows
is the one from the last tick before its vblank, and that is the same frame either way — so it is
purely a saving in work and battery. Where painting every tick is *not* affordable, the alternative
is a drifting gate, and alignment is the whole benefit.

## The accuracy floor inside WPF

WPF puts pixels on screen only on its composition ticks. On this machine those are 3.57 ms apart,
and a 60 Hz refresh period is 4.67 of them — not a whole number — so the last tick before any
vblank falls 0 to 3.57 ms before it, drifting every frame. **Knowing the vblank time does not let
you move a tick.** That spread is the floor: 1.27 ms median error at 60 Hz is what a uniform
0-3.57 ms spread produces.

Going below it would mean presenting outside WPF, on your own swapchain. **That was tested, and it
does not work.**

A paced-presenter prototype — child HWND, flip-model swapchain, `FRAME_LATENCY_WAITABLE_OBJECT`,
`MaximumFrameLatency = 1`, `Present(1, ...)` — was opened *positioned on the 60 Hz panel before the
window was shown*, so the swapchain was created there rather than moved onto it. `GetContainingOutput`
confirms DXGI associated it correctly:

```
asked for : \.\DISPLAY8
bound to  : \.\DISPLAY8
present gap  median 3.57ms (280/s)   over 1.5x median 0.0%
```

**Correctly bound to the 60 Hz output, and still presenting at 280/s** — the primary's rate. Display
change comes back at 16.666 ms, the panel's own rate, with 77.6% of presents never shown and
animation error at 1.202 ms against WPF's 1.27 ms. The same floor, reached the same way.

The reason is in the capture: `PresentMode` is **`Composed: Flip`**. DWM composites the swapchain,
and DWM composes on one clock. Owning the swapchain changes who draws the pixels, not who decides
when composition happens. Escaping that needs independent flip — fullscreen exclusive, or a hardware
overlay plane — which a windowed pane in a docked application does not get.

**So a custom presenter in a normal window does not fix the multi-monitor cadence problem.** It is
bound by the same thing WPF is, for the same reason.

### Except that independent flip is reachable, and it does fix it

The paragraph above originally ended the matter, on the reasoning that independent flip needs a
surface owning a whole output and a windowed pane cannot have one. The first half is right. The
second was never tested, and testing it changes the answer.

A bare Win32 `WS_POPUP` covering the entire 60 Hz output — no border, over the taskbar, no other
content, opaque, flip model — measured for 26 s:

| on the 60 Hz panel | PresentMode | present gap | animation error | never shown |
|---|---|---|---|---|
| WPF, repainting every tick | `Composed: Copy with GPU GDI` | 3.57 ms | 1.27 ms | 74.6% |
| windowed swapchain, child HWND | `Composed: Flip` | 3.57 ms | 1.20 ms | 77.6% |
| **fullscreen borderless swapchain** | **`Hardware: Independent Flip`** | **16.666 ms** | **0.019 ms** | **0.0%** |

**It paces to the panel's own vblank.** 60 presents a second on a 60 Hz panel, every one displayed,
animation error at 0.019 ms — sixty-seven times better than WPF and two orders below the composition
floor. The primary control behaves the same way at its own rate: 3.572 ms, 0.012 ms error, 0.0%
unshown, reported as `Hardware Composed: Independent Flip`.

So the floor described above is the floor **of DWM composition**, not of the machine. A surface that
DWM hands to the display plane is not subject to it, and that surface can live on a secondary
monitor.

**What it costs to qualify.** Independent flip is granted, not requested, and the conditions are
strict: the swapchain's window must cover the whole output, be opaque, use the flip model, and have
nothing composited above it. That last one is the awkward one — a tooltip, a menu, a notification
toast, or any other window overlapping that monitor drops the surface back to composed, silently and
at runtime. There is no clean API to ask whether you currently have it; PresentMon can see it from
outside, the process cannot easily see it from inside.

**Where that leaves it.** Not a general answer for a docked, multi-pane window — that arrangement
cannot qualify. It is a real answer for anything that can own a whole display: a chart given a
dedicated monitor, a presentation or reading mode, a single-purpose window on a trading desk. On
that shape it is not a marginal improvement over WPF, it is a different regime.

## What the presenter *is* better at, which is a different thing

The same test on the primary, where the panel's refresh and the composition clock are both 3.57 ms:

| on the 280 Hz primary | animation error p50 | never shown |
|---|---|---|
| WPF (RaisinDocs, full screen) | 0.78 ms | 2.6% |
| paced presenter prototype | **0.029 ms** | **0.0%** |

Twenty-seven times more accurate, with every present displayed. A tight render loop woken by the
waitable object, computing its offset per present, keeps content and presentation in step far more
closely than WPF's arrangement of "update on a composition tick, composite and present some time
later".

Read this with the workload in mind: the prototype clears the screen to a sweeping colour and
nothing else. The C2 text-presenter measurement suggests the cadence survives real work — 0.14 ms
of DirectWrite drawing per frame at 280/s, "not one frame late" — but that was measured on the
primary too, and never against animation error.

**This is not the multi-monitor problem and does not solve it.** It is a separate observation: on a
display whose refresh already matches composition, a presenter is much more accurate than WPF. Whether
0.78 ms matters is an application question — it is 22% of a 3.57 ms budget, and about a pixel at
1000 px/s.

## Getting a panel's vblank phase

`DwmGetCompositionTimingInfo` is not a route: it fails with `0x88980090` even given a real window
handle.

`D3DKMTWaitForVerticalBlankEvent` is. Open a DC for the display name, `D3DKMTOpenAdapterFromHdc`,
`D3DKMTCreateDevice`, then wait on the returned adapter, device and `VidPnSourceId`. Measured:

| display | reported | actual |
|---|---|---|
| 100 Hz panel | 9.998 ms = 100.0 Hz | 100 Hz |
| 60 Hz panel | 16.666 ms = 60.0 Hz | 60 Hz |
| 144 Hz panel | 6.945 ms = 144.0 Hz | 144 Hz |
| 280 Hz primary | 3.565 ms = 280.5 Hz | 280 Hz |

Exact on every panel, min and max inside 1-2% of the median, so the phase is predictable rather
than merely the rate.

**It degrades under CPU load, worst at the highest rate.** The first run of this probe was taken
immediately after the project compiled, with build processes still busy, and the primary read
12.607 ms — about 3.5 refreshes — ranging over 2.8 to 4.7 of them. The waiting thread was missing
vblanks and timing multiples. The three slower panels were unaffected in the same run, because 10
to 17 ms of slack absorbs the scheduling jitter that 3.57 ms does not. Quiet, the primary reads
3.565-3.568 ms across repeated runs.

That matters to anyone planning to pace off this: **a vblank-waiting thread has to be able to wake
reliably, and a loaded machine at a high refresh rate is exactly where it cannot.** Which is also
where the pacing was wanted. Treat a reading far from the panel's known rate as load, not as truth,
and cross-check against `Displays.RefreshHz`.

`Tools/vblank-probe` runs this. Two mistakes in writing it are worth knowing, because both produced
confident wrong numbers rather than errors: `LUID` is two 4-byte fields and aligns to 4, so
declaring it as a `long` pushes `VidPnSourceId` past the end of the struct and every display
reports source 0; and the wait needs a real device handle from `D3DKMTCreateDevice`, since passing
0 succeeds and waits on nothing in particular.

## Measuring any of this

`Tools/get-presentmon.ps1` fetches PresentMon at a pinned version. Read `MsAnimationError`; it is
the metric that describes what an eye sees, and it is the one unaffected by the pitfalls below.

**A capture is worthless without its conditions.** Refresh rate, window size, which panel, and
whether the machine was quiet. Machine state moved these numbers more than any code change did —
a video application once, leftover build processes twice.

**Two traps in reading a capture**, both of which produced published wrong numbers here:

- PresentMon renames columns between major versions. A reader looking for `DisplayedTime` or
  `Dropped` finds neither in 2.5.1, which has `MsBetweenDisplayChange` — `NA` when a present was
  never shown. A fallback of "no column matched, assume it was displayed" reports 0.0% dropped
  unconditionally. Make it throw.
- Once presents outnumber refreshes, captures contain sub-refresh display-change events, in pairs
  summing to one period. They drag the median display interval below the true panel period — a
  100 Hz panel reporting 7.15 ms — so that median, and anything computed against it, is unreliable
  in exactly the regime these findings create.
