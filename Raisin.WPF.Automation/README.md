# Raisin.WPF.Automation

Drives a WPF app from **outside its process**, for measurement runs that have to be repeatable.

Not a general automation API and not a UI Automation wrapper. It exists so that a harness can put a
window where it wants it, bring it to the front, and deliver a real gesture — and so that the two
or three things that are easy to get catastrophically wrong are got right once.

## Why outside the process

The tempting alternative is a script mode inside the app, driving the gesture directly. It is much
easier and it is wrong whenever the measurement involves input latency or frame pacing: a gesture
raised inside the app never enters the input queue, so measuring it that way assumes the answer.

Everything that is *not* the measured gesture is fair game for an in-process API — asking where
things are, selecting a tab, setting a range. Only the gesture under measurement has to be real.

## What it does

| | |
|---|---|
| `ForegroundWindow.Ensure` / `EnsureOrThrow` | bring a window to the front, and **know whether it worked** |
| `SyntheticInput.WheelAt` | wheel notches over a point, cursor moved first |
| `SyntheticInput.Drag` | press, stepped moves, release |
| `SyntheticInput.PreservingCursor` | put the pointer back afterwards |
| `TargetWindow` | bounds, placement, working-area fill, and a fractional aim point |

No package references. It is user32 and kernel32, so a driver that only needs to move a cursor does
not drag a UI Automation stack in behind it. A consumer that also needs to *find* elements adds
FlaUI itself and uses both.

## The four traps this exists to stop

**`SetForegroundWindow` fails silently.** Windows refuses it unless the calling process is already
in the foreground, and reports the refusal by returning `false`. A caller that does not check sends
its whole gesture to whatever window was in front — and the run completes, the log fills, and the
numbers describe an application nobody was driving. `Ensure` attaches to the foreground thread's
input queue to get through, then verifies against `GetForegroundWindow` rather than trusting the
return value.

**The wheel follows the physical cursor.** WPF routes a wheel message to whatever sits under the
pointer and ignores coordinates in a posted message. The cursor has to actually move, which is why
`WheelAt` takes a point.

**A drag is a stream of moves.** Jumping from start to finish delivers one mouse-move message. The
continuous repaint a real drag causes — usually the load being measured — never happens.

**Aim at the control, not the centre.** Docked panes take fixed widths, so at small window sizes the
geometric centre lands on a splitter or a neighbour. `TargetWindow.PointAt` takes fractions so a
caller can aim deliberately.

Synthetic **keyboard** input is deliberately absent: it was tried against a terminal control and
never arrived, by either `KEYEVENTF_UNICODE` or real virtual keys. Design around needing to type.

## The trap it cannot save you from

A gesture that runs perfectly and changes nothing. A chart dragged past its last bar, a document
scrolled at the end where it cannot move — the cursor moves, the app receives real input, every
assertion passes, and the run measures input pressure with no work behind it.

Ask the app what state it is in before driving it, and refuse the gesture when it would do nothing.
That question belongs to the app, not here.
