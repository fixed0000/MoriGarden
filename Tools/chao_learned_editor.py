"""Effective-bias drafts only; never invent training, timestamps or memory events."""
import math
import re
from decimal import Decimal, InvalidOperation, ROUND_HALF_UP
import tkinter as tk
from tkinter import ttk, messagebox

GROUPS = {
    "toys": ("toy-car", "rattle", "beach-ball", "flute", "drums", "electric-guitar", "synthesizer"),
    "fruits": ("Garden Fruit", "Heart Fruit", "Blue Square Fruit", "Yellow Triangle Fruit", "Red Round Fruit", "Green Diamond Fruit", "Moonblossom Fruit"),
    "habits": ("Play", "Socialize", "Leisure"),
}
KEYS = tuple((group, ident) for group, ids in GROUPS.items() for ident in ids)
STEP = Decimal(".001")


def shown(number):
    if not math.isfinite(float(number)):
        raise ValueError("Nonfinite learned snapshot")
    return Decimal(str(number)).quantize(STEP, rounding=ROUND_HALF_UP)


def bias_number(text, group):
    if not re.fullmatch(r"[+-]?(?:\d+(?:\.\d{0,3})?|\.\d{1,3})", str(text).strip()):
        raise ValueError("Use a finite number with at most three decimal places.")
    try:
        value = Decimal(str(text).strip())
    except InvalidOperation as error:
        raise ValueError("Invalid Bias") from error
    low, high = (Decimal(0), Decimal(".10")) if group == "habits" else (Decimal("-.30"), Decimal(".30"))
    if not low <= value <= high:
        raise ValueError(f"{group}: Bias must be between {low} and {high}.")
    return value


class LearnedDraft:
    def __init__(self, row, session):
        data = row.get("learnedEditor")
        if not isinstance(data, dict) or data.get("itemSchemaVersion") != 1 or data.get("habitSchemaVersion") != 1 or not row.get("learnedRevision"):
            raise ValueError("Learned preferences unavailable or unsupported. Read from a compatible game.")
        rows = data.get("rows", [])
        if len(rows) != 17 or {(r.get("group"), r.get("id")) for r in rows} != set(KEYS):
            raise ValueError("Incomplete learned preferences snapshot")
        self.current = {(r["group"], r["id"]): dict(r) for r in rows}
        self.baseline = {key: shown(r["effectiveBias"]) for key, r in self.current.items()}
        self.values = {key: f"{v:.3f}" for key, v in self.baseline.items()}
        self.session = session
        self.revision = row["learnedRevision"]
        self.blocked = ""
        self.stale = False
        self.force_neutral = set()

    def changed(self, key):
        try:
            return key in self.force_neutral or bias_number(self.values[key], key[0]) != self.baseline[key]
        except ValueError:
            return True

    @property
    def dirty(self):
        return any(self.changed(key) for key in KEYS)

    def patch(self):
        if self.blocked:
            raise ValueError(self.blocked)
        result = {}
        for key in KEYS:
            value = bias_number(self.values[key], key[0])
            if self.changed(key):
                result.setdefault(key[0], {})[key[1]] = float(value)
        return result

    def reset(self, key=None):
        for item in KEYS if key is None else (key,):
            self.values[item] = f"{self.baseline[item]:.3f}"
            self.force_neutral.discard(item)

    def neutral(self, key):
        self.values[key] = "0.000"
        if self.current[key]["storedBias"] != 0:
            self.force_neutral.add(key)
        else:
            self.force_neutral.discard(key)

    def refresh(self, fresh):
        self.current = fresh.current
        self.stale = self.revision != fresh.revision
        self.blocked = "Game restarted. Read from game before applying learned changes." if self.session != fresh.session else ""
        if not self.stale and not self.blocked:
            for key in KEYS:
                if not self.changed(key):
                    self.baseline[key] = fresh.baseline[key]
                    self.values[key] = fresh.values[key]


class LearnedEditorMixin:
    def active_learned_draft(self):
        return getattr(self, "learned_drafts", {}).get(getattr(self, "selected", None))

    def learned_patch(self):
        draft = self.active_learned_draft()
        return draft.patch() if draft else {}

    def editor_dirty(self):
        return any(d and d.dirty for d in (self.active_personality_draft(), self.active_learned_draft()))

    def guard_editor_navigation(self):
        learned = self.active_learned_draft()
        if not learned or not learned.dirty:
            return self.guard_personality_navigation()
        personality = self.active_personality_draft()
        names = "Personality and learned preferences" if personality and personality.dirty else "Learned preferences"
        choice = messagebox.askyesnocancel("Unsaved editor drafts", names + " have unsaved changes.\nYes: apply these drafts and stay until confirmed.\nNo: discard these drafts.\nCancel: keep editing.", parent=self.window)
        if choice is True:
            self.apply(drafts_only=True)
            return False
        if choice is None:
            return False
        learned.reset()
        if personality and personality.dirty:
            personality.reset()
        self.refresh_learned_controls()
        self.refresh_personality_controls()
        return True

    def sync_learned_snapshot(self, snapshot, confirmed=False):
        if not hasattr(self, "learned_drafts"):
            self.learned_drafts = {}
        rows = {r["id"]: r for r in snapshot.get("chao", [])}
        for ident, old in self.learned_drafts.items():
            if ident not in rows:
                old.blocked = "This Chao is no longer in the garden. Draft retained."
        for ident, row in rows.items():
            old = self.learned_drafts.get(ident)
            try:
                fresh = LearnedDraft(row, snapshot["session"])
                if old and old.dirty and not (confirmed and ident == self.selected):
                    old.refresh(fresh)
                else:
                    self.learned_drafts[ident] = fresh
            except ValueError as error:
                if old:
                    old.blocked = str(error)
        self.refresh_learned_controls()

    def prepare_learned_widgets(self):
        self.learned_form_target = self.selected
        self.learned_vars, self.learned_sliders, self.learned_current_labels, self.learned_meta_labels = {}, {}, {}, {}
        self.learned_previews, self.learned_apply_buttons, self.learned_feedbacks = [], [], []

    def build_learned_page(self, parent, groups):
        draft = self.active_learned_draft()
        if draft is None:
            ttk.Label(parent, text="Unavailable: no supported learned-preferences snapshot.", wraplength=400).pack(anchor="w")
            return
        tabs = ttk.Notebook(parent)
        tabs.pack(fill="both", expand=True)
        tabs.enable_traversal()
        for group in groups:
            page = ttk.Frame(tabs, padding=5)
            tabs.add(page, text=group.title() if group != "habits" else "Edit biases")
            page.columnconfigure(0, weight=1)
            page.rowconfigure(1, weight=1)
            hint = "Manual adjustments do not add completed activities." if group == "habits" else "At -0.300, recovery through natural learning can be slow until decay."
            ttk.Label(page, text="Effective Bias only; real experience is read-only.\n" + hint, wraplength=410).grid(row=0, column=0, sticky="w")
            holder = ttk.Frame(page)
            holder.grid(row=1, column=0, sticky="nsew")
            canvas = tk.Canvas(holder, height=140, highlightthickness=0)
            scroll = ttk.Scrollbar(holder, command=canvas.yview)
            scroll.pack(side="right", fill="y")
            canvas.configure(yscrollcommand=scroll.set)
            canvas.pack(side="left", fill="both", expand=True)
            inner = ttk.Frame(canvas)
            embedded = canvas.create_window((0, 0), window=inner, anchor="nw")
            inner.columnconfigure(2, weight=1)
            inner.bind("<Configure>", lambda _e, c=canvas: c.configure(scrollregion=c.bbox("all")))
            labels = []
            def resize(event, c=canvas, item=embedded, descriptions=labels):
                c.itemconfigure(item, width=event.width)
                for label in descriptions:
                    label.configure(wraplength=max(220, event.width-10))
            canvas.bind("<Configure>", resize)
            for col, text in enumerate(("Item", "Current", "Adjust", "New", "Actions")):
                ttk.Label(inner, text=text).grid(row=0, column=col, sticky="w")
            for index, ident in enumerate(GROUPS[group]):
                key = (group, ident)
                row = index*2+1
                ttk.Label(inner, text=ident.replace("-", " ").title() if group == "toys" else ident, width=14, wraplength=105).grid(row=row, column=0, sticky="w", pady=4)
                current = tk.StringVar()
                meta = tk.StringVar()
                self.learned_current_labels[key], self.learned_meta_labels[key] = current, meta
                ttk.Label(inner, textvariable=current, width=8).grid(row=row, column=1)
                slider = tk.DoubleVar(value=float(bias_number(draft.values[key], group)))
                self.learned_sliders[key] = slider
                scale = ttk.Scale(inner, from_=0 if group == "habits" else -.30, to=.10 if group == "habits" else .30, length=55, variable=slider,
                    command=lambda raw, k=key: self.learned_slider_changed(k, raw))
                scale.grid(row=row, column=2, sticky="ew", padx=3)
                value = tk.StringVar(value=draft.values[key])
                self.learned_vars[key] = value
                entry = ttk.Entry(inner, width=7, textvariable=value)
                entry.grid(row=row, column=3, padx=2)
                entry.bind("<FocusIn>", lambda _e, w=entry, c=canvas, f=inner: c.yview_moveto(max(0, w.winfo_y()-30)/max(1, f.winfo_height())))
                buttons = ttk.Frame(inner)
                buttons.grid(row=row, column=4)
                ttk.Button(buttons, text="Reset", width=5, command=lambda k=key: self.reset_learned(k)).pack(side="left")
                ttk.Button(buttons, text="Neutral", width=7, command=lambda k=key: self.neutral_learned(k)).pack(side="left")
                label = ttk.Label(inner, textvariable=meta, wraplength=410, foreground="#555555")
                label.grid(row=row+1, column=0, columnspan=5, sticky="w", pady=(0, 6))
                labels.append(label)
                value.trace_add("write", lambda *_a, k=key: self.learned_entry_changed(k))
            preview_frame = ttk.Frame(page)
            preview_frame.grid(row=2, column=0, sticky="ew", pady=3)
            preview = tk.Text(preview_frame, height=3, state="disabled", wrap="word", takefocus=False)
            preview_scroll = ttk.Scrollbar(preview_frame, command=preview.yview)
            preview.configure(yscrollcommand=preview_scroll.set)
            preview_scroll.pack(side="right", fill="y")
            preview.pack(side="left", fill="x", expand=True)
            self.learned_previews.append(preview)
            feedback = tk.StringVar()
            self.learned_feedbacks.append(feedback)
            ttk.Label(page, textvariable=feedback, wraplength=410).grid(row=3, column=0, sticky="w")
            footer = ttk.Frame(page)
            footer.grid(row=4, column=0, sticky="ew", pady=4)
            ttk.Button(footer, text="Reset unsaved changes", command=self.reset_learned).pack(side="left")
            apply = ttk.Button(footer, text="Apply learned changes", command=lambda: self.apply(learned_only=True))
            apply.pack(side="right")
            self.learned_apply_buttons.append(apply)
        self.refresh_learned_controls()

    def learned_entry_changed(self, key):
        if self.learned_form_target != self.selected:
            return
        draft = self.active_learned_draft()
        draft.values[key] = self.learned_vars[key].get()
        draft.force_neutral.discard(key)
        try:
            self.learned_sliders[key].set(float(bias_number(draft.values[key], key[0])))
        except ValueError:
            pass
        self.update_learned_preview()

    def learned_slider_changed(self, key, raw):
        if self.learned_form_target == self.selected:
            self.learned_vars[key].set(f"{float(raw):.3f}")

    def refresh_learned_controls(self):
        draft = self.active_learned_draft()
        if not draft or getattr(self, "learned_form_target", None) != self.selected:
            return
        for key, var in self.learned_vars.items():
            if var.get() != draft.values[key]:
                forced = key in draft.force_neutral
                var.set(draft.values[key])
                if forced:
                    draft.force_neutral.add(key)
            row = draft.current[key]
            self.learned_current_labels[key].set(f"{shown(row['effectiveBias']):+.3f}")
            credit = f"{row['lastCreditSeconds']:.1f}s" if row['experienceCount'] else "—"
            self.learned_meta_labels[key].set(f"Stored {row['storedBias']:+.3f} · Experiences {row['experienceCount']} · Last real credit {credit}")
        self.update_learned_preview()

    def update_learned_preview(self):
        draft = self.active_learned_draft()
        try:
            changes = draft.patch()
            count = sum(len(v) for v in changes.values())
            text = "\n".join(f"{g}/{ident}: {draft.baseline[(g,ident)]:+.3f} → {n:+.3f}" for g, rows in changes.items() for ident, n in rows.items()) or "No learned changes."
            feedback = draft.blocked or ("Stored data changed. Read from game before applying." if draft.stale else f"{count} changed values · only learned Bias will be sent")
            enabled = count > 0 and not draft.blocked and self.pending is None
        except ValueError as error:
            text, feedback, enabled = str(error), str(error), False
        for widget in self.learned_previews:
            if widget.winfo_exists():
                widget.configure(state="normal")
                widget.delete("1.0", "end")
                widget.insert("end", text)
                widget.configure(state="disabled")
        for value in self.learned_feedbacks:
            value.set(feedback)
        for button in self.learned_apply_buttons:
            if button.winfo_exists():
                button.configure(state="normal" if enabled else "disabled")

    def reset_learned(self, key=None):
        draft = self.active_learned_draft()
        if draft:
            draft.reset(key)
            self.refresh_learned_controls()

    def neutral_learned(self, key):
        draft = self.active_learned_draft()
        draft.neutral(key)
        self.refresh_learned_controls()
