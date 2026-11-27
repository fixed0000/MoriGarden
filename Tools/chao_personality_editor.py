"""Editor-only drafts and widgets. Canonical personality stays in the game."""
import re
import tkinter as tk
from tkinter import ttk, messagebox

TRAITS = {
    "Curious": "Garden exploration choices, interest expression, Idle looks and curious voice intonation.",
    "Kindness": "Animal assistance, expedition social weighting and watering-can eligibility.",
    "Energetic": "Active choices, expression, paw gestures and positive voice pitch. Negative side also backs Crybaby.",
    "Naive": "Negative values mean more naive; the numeric axis is not inverted. Not yet connected to gameplay.",
    "Solitude": "Social-choice weighting and a small inward resting-paw posture.",
    "Vitality": "Small Play and Leisure choice adjustments; does not change Energy drain.",
    "BigEater": "Eating choices and the existing Glutton eating/snoring rules; fullness safety still applies.",
    "Regain": "Not yet connected to gameplay.",
    "Skillful": "Not yet connected to gameplay. Separate from the existing Dexterous flag.",
    "Charm": "Not yet connected to gameplay.",
    "Chatty": "Social-choice weighting and the interval of ordinary voice reactions; separate from Sociability.",
    "Carefree": "Play/Leisure weighting and relaxed Idle sway.",
    "Fickle": "Small variation among close activity choices; never overrides critical needs.",
}
LEGACY = {"Curiosity", "Kindness", "Glutton", "Crybaby"}


def trait_integer(value):
    if not re.fullmatch(r"[+-]?\d+", str(value).strip()):
        raise ValueError("Enter a whole number from -100 to +100; decimals are not rounded.")
    number = int(str(value).strip())
    if not -100 <= number <= 100:
        raise ValueError("Personality values must be between -100 and +100.")
    return number


class PersonalityDraft:
    def __init__(self, row, session):
        values = row.get("personality")
        if row.get("personalitySchemaVersion") != 1 or not isinstance(values, dict) or set(values) != set(TRAITS) or not row.get("personalityRevision"):
            raise ValueError("Personality unavailable or unsupported. Read from a compatible game.")
        if any(type(v) is not int or not -100 <= v <= 100 for v in values.values()):
            raise ValueError("Invalid personality snapshot.")
        self.session = session
        self.revision = row["personalityRevision"]
        self.baseline = dict(values)
        self.values = {k: str(v) for k, v in values.items()}
        self.blocked = ""

    @property
    def dirty(self):
        try:
            return bool(self.patch())
        except ValueError:
            return True

    def patch(self):
        values = {k: trait_integer(self.values[k]) for k in TRAITS}
        return {k: v for k, v in values.items() if v != self.baseline[k]}

    def reset(self, name=None):
        for key in (TRAITS if name is None else (name,)):
            self.values[key] = str(self.baseline[key])


class PersonalityEditorMixin:
    def active_personality_draft(self):
        return getattr(self, "personality_drafts", {}).get(getattr(self, "selected", None))

    def personality_patch(self):
        draft = self.active_personality_draft()
        if draft is None:
            return {}
        if draft.blocked:
            raise ValueError(draft.blocked)
        return draft.patch()

    def sync_personality_snapshot(self, snapshot, confirmed=False):
        if not hasattr(self, "personality_drafts"):
            self.personality_drafts = {}
        self.personality_metadata = {r["id"]: r for r in snapshot.get("chao", [])}
        for key, draft in self.personality_drafts.items():
            if key not in self.personality_metadata:
                draft.blocked = "This Chao is no longer in the garden. Draft retained."
        for row in snapshot.get("chao", []):
            old = self.personality_drafts.get(row["id"])
            try:
                fresh = PersonalityDraft(row, snapshot["session"])
                if old is not None and old.dirty and not (confirmed and row["id"] == self.selected):
                    old.blocked = "Game restarted. Read from game before applying." if old.session != snapshot["session"] else ""
                    continue
                self.personality_drafts[row["id"]] = fresh
            except ValueError as error:
                if old is not None:
                    old.blocked = str(error)
        self.refresh_personality_controls()

    def guard_personality_navigation(self):
        draft = self.active_personality_draft()
        if draft is None or not draft.dirty:
            return True
        choice = messagebox.askyesnocancel("Unsaved personality changes",
            "Apply personality changes before leaving?\nYes: apply and stay until confirmed.\nNo: discard this draft.\nCancel: keep editing.", parent=self.window)
        if choice is True:
            self.apply(personality_only=True)
            return False
        if choice is None:
            return False
        draft.reset()
        self.refresh_personality_controls()
        return True

    def build_personality(self):
        self.personality_form_target = self.selected
        page = ttk.Frame(self.tabs, padding=6)
        self.tabs.insert(2, page, text="Personality")  # General/Stats indices remain compatible.
        page.columnconfigure(0, weight=1)
        page.rowconfigure(2, weight=1)
        draft = self.active_personality_draft()
        name = self.snapshot["parameters"].get("Name", "")
        ttk.Label(page, text=f"{name} · ID: {self.selected}", wraplength=420).grid(row=0, column=0, sticky="w")
        row = getattr(self, "personality_metadata", {}).get(self.selected, {})
        ttk.Label(page, text=f"Manual innate-personality intervention · Model version: {row.get('personalitySchemaVersion', 'Unavailable')}", wraplength=420).grid(row=1, column=0, sticky="w", pady=(2, 5))
        self.personality_controls = []
        self.personality_vars = {}
        self.personality_sliders = {}
        self.personality_original = {}
        self.personality_feedback = tk.StringVar()
        if draft is None or draft.blocked:
            ttk.Label(page, text=draft.blocked if draft else "Unavailable: no supported personality snapshot.", wraplength=400).grid(row=2, column=0, sticky="nw")
            self.personality_apply = ttk.Button(page, text="Apply personality changes", state="disabled")
            self.personality_apply.grid(row=3, column=0, sticky="w")
            return
        body = ttk.Frame(page)
        body.grid(row=2, column=0, sticky="nsew")
        canvas = tk.Canvas(body, highlightthickness=0, height=210)
        bar = ttk.Scrollbar(body, orient="vertical", command=canvas.yview)
        bar.pack(side="right", fill="y")
        canvas.configure(yscrollcommand=bar.set)
        canvas.pack(side="left", fill="both", expand=True)
        inner = ttk.Frame(canvas)
        embedded = canvas.create_window((0, 0), window=inner, anchor="nw")
        inner.columnconfigure(2, weight=1)
        inner.bind("<Configure>", lambda _e: canvas.configure(scrollregion=canvas.bbox("all")))
        descriptions = []
        def resize(event):
            canvas.itemconfigure(embedded, width=event.width)
            for label in descriptions:
                label.configure(wraplength=max(240, event.width-14))
        canvas.bind("<Configure>", resize)
        for col, label in enumerate(("Trait", "Original", "Adjust", "New", "")):
            ttk.Label(inner, text=label).grid(row=0, column=col, sticky="w")
        for index, (trait, description) in enumerate(TRAITS.items()):
            r = index*2+1
            ttk.Label(inner, text=trait, width=10).grid(row=r, column=0, sticky="w", pady=(5, 0))
            original = tk.StringVar(value=f"{draft.baseline[trait]:+d}")
            self.personality_original[trait] = original
            ttk.Label(inner, textvariable=original, width=6).grid(row=r, column=1)
            slider = tk.DoubleVar(value=trait_integer(draft.values[trait]))
            self.personality_sliders[trait] = slider
            scale = ttk.Scale(inner, from_=-100, to=100, variable=slider, length=85,
                command=lambda raw, key=trait: self.personality_slider_changed(key, raw))
            scale.grid(row=r, column=2, sticky="ew", padx=5)
            value = tk.StringVar(value=draft.values[trait])
            self.personality_vars[trait] = value
            entry = ttk.Entry(inner, textvariable=value, width=6)
            entry.grid(row=r, column=3, padx=3)
            self.personality_controls.extend((scale, entry))
            entry.bind("<FocusIn>", lambda _e, widget=entry: canvas.yview_moveto(max(0, widget.winfo_y()-40)/max(1, inner.winfo_height())))
            ttk.Button(inner, text="Reset", width=5, command=lambda key=trait: self.reset_personality(key)).grid(row=r, column=4, padx=2)
            help_label = ttk.Label(inner, text=description, wraplength=410, foreground="#555555")
            help_label.grid(row=r+1, column=0, columnspan=5, sticky="w", pady=(0, 5))
            descriptions.append(help_label)
            value.trace_add("write", lambda *_args, key=trait: self.personality_entry_changed(key))
        preview_frame = ttk.Frame(page)
        preview_frame.grid(row=3, column=0, sticky="ew", pady=(5, 2))
        self.personality_preview = tk.Text(preview_frame, height=4, wrap="word", state="disabled", takefocus=False)
        preview_scroll = ttk.Scrollbar(preview_frame, command=self.personality_preview.yview)
        preview_scroll.pack(side="right", fill="y")
        self.personality_preview.configure(yscrollcommand=preview_scroll.set)
        self.personality_preview.pack(side="left", fill="x", expand=True)
        # Text has its own keyboard/mouse scrolling even for thirteen changed rows.
        ttk.Label(page, textvariable=self.personality_feedback, wraplength=420).grid(row=4, column=0, sticky="w")
        footer = ttk.Frame(page)
        footer.grid(row=5, column=0, sticky="ew", pady=4)
        ttk.Button(footer, text="Reset unsaved personality changes", command=self.reset_personality).pack(side="left")
        self.personality_apply = ttk.Button(footer, text="Apply personality changes", command=lambda: self.apply(personality_only=True))
        self.personality_apply.pack(side="right")
        self.refresh_personality_controls()

    def personality_slider_changed(self, trait, raw):
        if self.personality_form_target != self.selected:
            return
        # A slider gesture intentionally snaps to its advertised integer step; typed decimals are rejected.
        value = int(round(float(raw)))
        self.personality_sliders[trait].set(value)
        self.personality_vars[trait].set(str(value))

    def personality_entry_changed(self, trait):
        if self.personality_form_target != self.selected:
            return
        draft = self.active_personality_draft()
        draft.values[trait] = self.personality_vars[trait].get()
        try:
            self.personality_sliders[trait].set(trait_integer(draft.values[trait]))
        except ValueError:
            pass
        self.update_personality_preview()

    def refresh_personality_controls(self):
        draft = self.active_personality_draft()
        if draft is None or not hasattr(self, "personality_vars"):
            return
        if getattr(self, "personality_form_target", self.selected) != self.selected:
            return
        for key, var in self.personality_vars.items():
            self.personality_original[key].set(f"{draft.baseline[key]:+d}")
            if var.get() != draft.values[key]:
                var.set(draft.values[key])
        self.update_personality_preview()

    def update_personality_preview(self):
        if not hasattr(self, "personality_preview") or not self.personality_preview.winfo_exists():
            return
        draft = self.active_personality_draft()
        for control in self.personality_controls:
            control.configure(state="disabled" if self.pending is not None else "normal")
        try:
            changes = draft.patch()
            text = "\n".join(f"{k}: {draft.baseline[k]:+d} → {v:+d}" for k, v in changes.items()) or "No personality changes."
            self.personality_feedback.set(draft.blocked or f"{len(changes)} changed parameters")
            self.personality_apply.configure(state="disabled" if draft.blocked or self.pending is not None or not changes else "normal")
        except ValueError as error:
            text = str(error)
            self.personality_feedback.set(text)
            self.personality_apply.configure(state="disabled")
        self.personality_preview.configure(state="normal")
        self.personality_preview.delete("1.0", "end")
        self.personality_preview.insert("end", text)
        self.personality_preview.configure(state="disabled")

    def reset_personality(self, trait=None):
        draft = self.active_personality_draft()
        if draft is not None:
            draft.reset(trait)
            self.refresh_personality_controls()
