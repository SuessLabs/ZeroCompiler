---
layout: default
title: "Notepad Project — Test and Extend"
---

# Notepad Project — Test and Extend

Section 4 — Creating a Notepad Project · Chapter 4.5

[Previous](zasm-04-04-notepad-open-and-save-files.md) · [Contents](index.md) · [Next](zasm-05-01-zero-cli-compiler.md)

Validate one behavior at a time. Compare with the original sample to distinguish
existing behavior from regressions caused by edits.

| Milestone | Check | Expected result |
| --- | --- | --- |
| Window | Launch and close repeatedly | Responsive window; process exits |
| Editing | Type, select, copy, paste, Enter | Multiline input works |
| Preview | Read fewer than 256 bytes | Popup matches entered text |
| Bounds | Preview a longer document | Bounded text; no stack corruption |
| Open extension | Missing or oversized file | Error; current document retained |
| Save extension | Empty and multiline text | Successful round trip |
| Save extension | Unwritable destination | Error; no false success state |

Set a source breakpoint at `window_proc` or `window_command`, press F5, and
step over API calls with F10. Observe EAX results and ESP around calls. A
callback breakpoint can fire frequently as Windows delivers messages.

Prioritize file persistence, error reporting, unsaved changes, resizing, and
encoding after the editing milestone. These are extensions to implement and
test: menu entries alone do not complete them. A production editor also needs
a deliberate large-file policy.

**Checkpoint:** Record which milestones your copy implements. Keep a known
working copy until each new behavior passes its checks.

---

[Previous](zasm-04-04-notepad-open-and-save-files.md) · [Contents](index.md) · [Next](zasm-05-01-zero-cli-compiler.md)
