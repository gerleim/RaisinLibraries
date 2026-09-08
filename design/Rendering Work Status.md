# Rendering and scrolling: what is done, what is not

*Status: audit, 8 September 2026. A scan of every design note across RaisinDocs, RaisinTerminal,
RaisinLibraries and StockRaisin2 that touches rendering, scrolling, pacing or repaint cost. Each
claim below was checked against the code, not against the note's own status line - two of the notes
turned out to be describing an architecture that no longer exists.*

Companion to [WPF Presentation Timing](WPF%20Presentation%20Timing.md), which holds the mechanism,
and to `Raisin.WPF.Base/docs/High-Frequency-Rendering.md`, which holds the rules that came out of it.

---

## Done

### 1. The composition-clock pacing fault

WPF's `CompositionTarget.Rendering` is driven by the **primary** display's clock. Any repaint gate
built on a display-period accumulator therefore starves a window on any other panel: it banks
arrears against the wrong refresh rate and drops frames that were already composed.

Measured before the fix, as frames never shown: **2.6% on the primary panel, 34.9% on a 60Hz
secondary, 35.4% on a 144Hz secondary.** Removing the gate gave **10-17x** on the non-primary
panels.

Fixed in three places:

| Where | Change |
|---|---|
| `Raisin.WPF.Base/SmoothScroller.cs` | `DisplayPeriod` property and `_sinceDisplayFrame` removed outright |
| `RaisinDocs/ScrollController.cs` | repaint gate removed; `_displayPeriod` kept with remarks saying why it deliberately does *not* throttle |
| `RaisinTerminal2/Controls/TerminalScrollController.cs` | gate removed |

A false lead is recorded because it cost a day: the first hypothesis was that the accumulator banked
arrears and that a nearest-tick paint gate would fix it. It did not. The fault is not in the paint
side's arithmetic, it is in which clock the paint side is being told about.

The analyser had to be fixed before any of this was trustworthy - `Test-Shown` fell through to
`return true` and reported a fabricated 0.0% drop rate. It now falls back to
`MsBetweenDisplayChange` and **throws** rather than assuming a frame was displayed.

### 2. Sub-pixel hinting never disabled during a wheel scroll (RaisinTerminal2)

`TextFormattingMode.Display` snaps glyph advances, which is right for static text and wrong for
motion. It stayed on through the whole coast, because the check only looked at
`_smoother.IsAnimating` - false while the wheel is coasting. `TerminalCanvas` now takes an
`IsWheelCoasting` callback:

```csharp
private bool IsScrollAnimating => _smoother.IsAnimating || IsWheelCoasting?.Invoke() == true;
bool useGlyphRun = IsScrollAnimating || !UseHintedText;
```

**3.7x** on the scroll path.

### 3. Minimap rebuild cost (RaisinDocs)

The cached band's validity was tested in document Y against a bitmap addressed in minimap Y, so it
rebuilt far more often than it had to. `MinimapScrollbar.cs` now tests in minimap Y.

### 4. Row-array reuse (RaisinTerminal2)

A snapshot allocated a fresh `CellData[cols]` per visible row per frame - about 160 KB a frame,
~30 MB/s - while measuring **0.03 rows changed per frame** scrolling and 0.5-0.9 under live output,
out of about fifty visible. It never appeared in a timer, because allocation is a pointer bump; it
arrived later as GC. `SnapshotRowCache` (generational young/old rotation, no sweep) fixed it:

- snapshot stage **0.07-0.14 ms -> 0.01-0.02 ms**
- gen1 collections **7-12 -> 2-3** per interval

Two lessons are worth keeping. First, the initial version *swept* one dictionary and made things
worse - frames of 12-16 ms where the worst had been 1.7 ms - because a periodic allocating O(n)
pass is the wrong shape for the render path. Second, the tail spikes were mis-attributed twice; an
A/B showed the same ~19 ms spike landing in `snapshot` with the cache off and in `visible-lines`
with it on. It was GC, and **a per-stage timer attributes external pauses to whatever it happened
to be timing**.

Two correctness bugs were found after the merge and fixed in `c3e73ec`: a screen row served back as
scrollback (a row keeps its absolute number when it scrolls off, so the two kinds need disjoint key
spaces - screen keys are now the bitwise complement), and `ClearScrollback` renumbering absolute
rows (now guarded by `ScrollbackEpoch`).

### 5. Unattended measurement

Both RaisinDocs and RaisinTerminal2 can now be measured with nobody at the keyboard.

- PresentMon 2.5.1 pinned by `Tools/get-presentmon.ps1`
- `Raisin.WPF.Automation/Displays.cs` - enumerate panels, refresh rate, pick by largest overlap
  (note: `LUID` is two 4-byte fields; declaring it `long` mis-aligns `VidPnSourceId`)
- `Raisin.WPF.Automation/RunGuard.cs` - ESC cancels a run mid-flight, and foreground /
  window-moved / pointer-moved interference is detected and reported rather than silently
  spoiling a capture
- RaisinTerminal2's `--automation` file-drop command channel plus `Tools/terminal-automation.ps1`,
  because synthetic keyboard input does not reach a terminal control at all - neither
  `KEYEVENTF_UNICODE` nor real virtual keys
- `--dev` isolated profile, so a run cannot inherit the user's font size or replay the previous
  session's command
- a self-driving redraw loop as the TUI instrument (54-68% of rows changed, against 0.00 for
  scrolling); a pager was the wrong instrument and measured nothing

### 6. Independent flip is reachable

Fullscreen borderless gets `Hardware: Independent Flip` and **0.019 ms** animation error. That is
the evidence that the remaining ceiling is DWM composition, not our code.

### 7. Older RaisinDocs work, confirmed shipped

- **Opaque line visuals** - all five phases. A `BitmapCache` is a transparent surface and ClearType
  cannot filter against a background it does not know, so each line paints its own opaque fill and
  nothing may be drawn beneath the content layer.
- **Typing performance** - merged as `d2a75a1`.
- **Scroll pre-buffering** - phases 1-3 done and measured; phase 4 written off deliberately.

### 8. Older RaisinTerminal2 work, confirmed shipped

- **The rendering/scrolling rewrite** - `TerminalSession` exists in `RaisinTerminal.Core`,
  `BufferSnapshot` and `TerminalViewport` exist, and empty-line compression is exercised by five
  test files. The five planning notes are now history.
- **Minimap/scrollbar simplification** - all three redundant `CompositionTarget.Rendering` loops
  (`_vpAnimating`, `_scrollBarAnimating`, and the split `_currentIntOffset` model) are gone from
  the codebase.
- **Scrollbar visuals** - recreated in `Raisin.WPF.Base/Themes/SharedThemeCore.xaml` without
  MaterialDesign. The analysis note's suggestion to move to `VisualStateManager` was *not* adopted:
  the shipped template animates `Bg` opacity 0.5 -> 0.8 with an `EventTrigger` `DoubleAnimation`.
  The issue is resolved; the architectural preference was declined.

---

## Not done

| Item | Written up in | State |
|---|---|---|
| **StockRaisin2 still has the pacing gate** | `StockRaisin2/design/Composition Pacing Findings.md` | Live at `ChartViewModel.Rendering.cs:207`. No PresentMon capture of that app exists at all. |
| **RaisinTerminal2 pacing fix measured on one panel** | `RaisinTerminal/design/Composition Pacing Findings.md` | 60Hz confirmed; 100Hz and 144Hz never captured. |
| **Row visuals** (a `DrawingVisual` + `BitmapCache` per row) | `RaisinTerminal/design/Repaint Cost.md` | Not started. Q1 answered, Q2-Q4 open. |
| **Sub-pixel scrolling** | `RaisinDocs/design/Sub-Pixel Scrolling.md` | Planned, not started. Third attempt; two previous ones reverted (`40db97a`, `b23a387`). |
| **Our own presenter / vblank-driven frames** | `RaisinDocs/design/Rendering Direction.md`, `RaisinLibraries/design/WPF Presentation Timing.md` | Research. Only worth anything in a configuration that reaches independent flip. |
| **StockRaisin2 rendering harness** | `StockRaisin2/design/Rendering Test Harness.md` | Part built - isolation, layout fixture and feed recording done; one question marked open. |
| **RT2 command strip** | `RaisinTerminal/design/Plan-CommandNavAndMinimap.md` | Minimap shipped. The command strip exists only as RT1's `CommandHistoryService`; nothing in RT2. |
| **Chart last-bar dropout** | `StockRaisin2/chart-last-bar-dropout-analysis.md` | Diagnosed, three remedies proposed, none applied. Adjacent to this thread rather than in it, but open. |
| **RaisinDocs test failures** | - | 22 pre-existing failures, untouched throughout this work. |

---

## What should be done next

**1. Remove the StockRaisin2 pacing gate and capture it.** It is the same one-line fault already
fixed twice, it is the only one of the three apps still carrying it, and the app has never been put
under PresentMon. Highest value per hour on this list, and the tooling to measure it already exists.

**2. Close the RaisinTerminal2 measurement matrix** at 100Hz and 144Hz. The fix is believed correct
on the strength of one panel plus the shared mechanism; a capture makes it known.

**3. Answer Q2-Q4 in `Repaint Cost.md` before building row visuals.** Building first is how the
sweep in `SnapshotRowCache` and the nearest-tick paint gate both happened - the cost model was
assumed, and in both cases only a measurement caught that the assumption was wrong.

**4. Leave sub-pixel scrolling and the own-presenter alone until 1-3 land.** Both are large, both
depend on the pacing picture being complete, and the presenter in particular is only worth building
where independent flip is reachable. `Scroll Frame Pacing.md` carries an explicit warning at the top
not to build its section C without re-reading the bottom of the note; that warning still stands.

---

## Files moved to `_done/`

Complete, implemented, or describing an architecture that no longer exists.

**`RaisinDocs/design/_done/`**
- `Opaque Line Visuals.md` - all five phases shipped
- `Typing Performance.md` - merged `d2a75a1`
- `Scroll Pre-Buffering.md` - phases 1-3 done, phase 4 written off

These three are cited from source comments and from `CLAUDE.md`; every citation was rewritten to the
new path.

**`RaisinTerminal/design/_done/`**
- `RT2-Minimap-Scrollbar-Simplification.md` - verified: all three loops gone
- `scroll-mechanisms-code-review.md` - every fix checked off, and the scroll path has since been
  rewritten past the code it reviewed
- `ScrollbarColorBreakdown.md` - values locked in and recreated
- `ScrollbarIssue-Analysis.md` - resolved (see the note above about `VisualStateManager`)
- `Scrolling-Architecture-and-Evolution.md` - describes the split-offset model and the separate
  minimap animation loop, neither of which exists any more

**`RaisinTerminal/design/RenderingAndScrolling/_done/`**
- `01-Requirements.md` through `05-PlannedArchitecture.md` - the rewrite they plan is in the code

**Kept in place despite being answered:** `RaisinDocs/design/Scroll Frame Pacing.md`. It is the
citation target of this note, of both projects' pacing notes and of the shared timing note, and it
carries the standing "do not build section C" warning. Moving it would bury the one paragraph most
likely to save someone a week.

**Not moved, out of scope:** `RaisinTerminal/design/SessionState-WakingStatus-Refactor-COMPLETED.md`
is plainly finished but belongs to a different topic; left for whoever audits that thread.
