import json
import os
import time
import tkinter as tk
from pathlib import Path
from tkinter import ttk
from uuid import uuid4
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from chao_personality_editor import PersonalityEditorMixin, LEGACY
from chao_learned_editor import LearnedEditorMixin
from chao_memory_view import MemoryEditorMixin


class ChaoEditor(MemoryEditorMixin, LearnedEditorMixin, PersonalityEditorMixin):
    def __init__(self, window):
        self.window = window
        personal = Path(os.environ["APPDATA"]) / "Godot" / "app_userdata" / "Mori · маленький сад"
        selected = os.environ.get("MORI_SAVE_DIRECTORY") or os.environ.get("MORI_EDITOR_DIRECTORY")
        self.directory = Path(selected).resolve() if selected else personal
        profile = "Personal garden" if self.directory == personal else self.directory.name
        self.snapshot = None
        self.variables = {}
        self.stat_variables = {}
        self.statistics_rows = {}
        self.roster = {}
        self.relationship_rows = {}
        self.relationship_peer = None
        self.selected = None
        self.personality_drafts = {}
        self.pending = None
        self.status = tk.StringVar(value="Start Mori, then click Read from game.")
        window.title("Mori — Chao Editor · " + profile)
        window.geometry("960x690")
        window.minsize(710, 540)
        ttk.Label(window, text="Profile: " + profile + " · " + str(self.directory), padding=(12, 6), wraplength=900).pack(fill="x")
        toolbar = ttk.Frame(window, padding=12)
        toolbar.pack(fill="x")
        ttk.Button(toolbar, text="Read from game", command=self.reload).pack(side="left")
        ttk.Button(toolbar, text="Apply changes", command=self.apply).pack(side="left", padx=8)
        self.subject = tk.StringVar(value="Current Chao")
        ttk.Label(toolbar, textvariable=self.subject).pack(side="right")
        content = ttk.Frame(window, padding=(12, 0, 12, 0))
        content.pack(fill="both", expand=True)
        sidebar = ttk.LabelFrame(content, text="Chao in your garden", padding=6)
        sidebar.pack(side="left", fill="y", padx=(0, 12))
        self.list = tk.Listbox(sidebar, width=22, exportselection=False)
        scroll = ttk.Scrollbar(sidebar, orient="vertical", command=self.list.yview)
        self.list.configure(yscrollcommand=scroll.set)
        scroll.pack(side="right", fill="y")
        self.list.pack(side="left", fill="both", expand=True)
        self.list.bind("<<ListboxSelect>>", self.select_chao)
        self.tabs = ttk.Notebook(content)
        self.tabs.pack(fill="both", expand=True)
        self.tabs.enable_traversal()
        ttk.Label(window, textvariable=self.status, padding=12, wraplength=550).pack(fill="x")
        self.reload()
        self.window.after(1000, self.refresh_roster)

    def read(self):
        for attempt in range(4):
            try:
                snapshot = json.loads((self.directory / "chao-editor-state.json").read_text(encoding="utf-8"))
                break
            except PermissionError:
                if attempt == 3:
                    raise
                time.sleep(.01)  # Windows/OneDrive can briefly hold the atomic snapshot replacement.
        if snapshot.get("profileDirectory") and Path(snapshot["profileDirectory"]).resolve() != self.directory.resolve():
            raise ValueError("This snapshot belongs to a different garden profile.")
        return snapshot

    def update_roster(self, snapshot):
        roster = {chao["id"]: chao["parameters"] for chao in snapshot.get("chao", [])}
        names = [(key, values["Name"]) for key, values in roster.items()]
        if names != [(key, values["Name"]) for key, values in self.roster.items()]:
            self.list.delete(0, "end")
            for _, name in names:
                self.list.insert("end", name)
        # Canonical personality is carried by the game; the editor sends only patches.
        self.place_rows = {chao["id"]: chao.get("placePreferences", {}) for chao in snapshot.get("chao", [])}
        self.social_rows = {chao["id"]: chao.get("socialPreferences", {}) for chao in snapshot.get("chao", [])}
        self.voice_rows = {chao["id"]: chao.get("voice") for chao in snapshot.get("chao", [])}
        self.gesture_rows = {chao["id"]: chao.get("idleGestures") for chao in snapshot.get("chao", [])}
        self.expression_rows = {chao["id"]: chao.get("expression") for chao in snapshot.get("chao", [])}
        self.rhythm_rows = {chao["id"]: chao.get("dailyRhythm", {}) for chao in snapshot.get("chao", [])}
        self.habit_rows = {chao["id"]: chao.get("activityHabits", {}) for chao in snapshot.get("chao", [])}
        self.learned_rows = {chao["id"]: chao.get("learnedPreferences", {}) for chao in snapshot.get("chao", [])}
        self.personality_rows = {chao["id"]: chao.get("personality") for chao in snapshot.get("chao", [])}
        self.statistics_rows = {chao["id"]: chao.get("statistics", {}) for chao in snapshot.get("chao", [])}
        self.roster = roster
        self.relationship_rows = {chao["id"]: chao.get("relationships", []) for chao in snapshot.get("chao", [])}
        draft = self.active_personality_draft()
        if self.selected not in roster and not self.editor_dirty():
            self.selected = next(iter(roster), None)
        if self.selected in roster:
            index = list(roster).index(self.selected)
            if self.list.curselection() != (index,):
                self.list.selection_clear(0, "end")
                self.list.selection_set(index)
        self.sync_personality_snapshot(snapshot)
        self.sync_learned_snapshot(snapshot)
        self.sync_memory_snapshot(snapshot)

    def refresh_roster(self):
        try:
            snapshot = self.read()
            previous = self.selected
            self.update_roster(snapshot)
            self.refresh_daily_rhythm()
            self.refresh_expression()
            self.refresh_idle_gestures()
            self.refresh_voice()
            if self.snapshot is None or previous != self.selected or snapshot["session"] != self.snapshot["session"]:
                draft = self.active_personality_draft()
                if not self.editor_dirty():
                    self.reload(force=True)
                else:
                    self.status.set("Game restarted. Unsaved personality draft retained; Read from game to continue.")
        except (OSError, ValueError):
            self.memory_unavailable()
        self.window.after(1000, self.refresh_roster)

    def select_chao(self, _event):
        selection = self.list.curselection()
        if self.pending is not None:
            self.list.selection_clear(0, "end")
            if self.selected in self.roster:
                self.list.selection_set(list(self.roster).index(self.selected))
            self.status.set("Wait for the current request before switching Chao.")
            return
        if selection and self.pending is None:
            target = list(self.roster)[selection[0]]
            if target == self.selected:
                return
            if target != self.selected and not self.guard_editor_navigation():
                self.list.selection_clear(0, "end")
                self.list.selection_set(list(self.roster).index(self.selected))
                return
            self.selected = target
            self.reload(force=True)

    def reload(self, force=False):
        if not force and not self.guard_editor_navigation():
            return
        try:
            snapshot = self.read()
            self.update_roster(snapshot)
        except (OSError, ValueError):
            self.status.set("No game snapshot yet. Open Mori and click Read from game.")
            return
        if self.selected is None:
            self.status.set("Waiting for the Chao list from Mori.")
            return
        self.snapshot = dict(session=snapshot["session"], target=self.selected,
                             parameters=self.roster[self.selected], statistics=self.statistics_rows.get(self.selected, {}),
                             personality=self.personality_rows.get(self.selected),
                             learnedPreferences=self.learned_rows.get(self.selected, {}),
                             placePreferences=self.place_rows.get(self.selected, {}),
                             socialPreferences=self.social_rows.get(self.selected, {}),
                             activityHabits=self.habit_rows.get(self.selected, {}),
                             dailyRhythm=self.rhythm_rows.get(self.selected, {}),
                             expression=self.expression_rows.get(self.selected),
                             relationships=self.relationship_rows.get(self.selected, []))
        tab_index = self.tabs.index("current") if self.tabs.select() else 1
        for widget in self.tabs.winfo_children():
            widget.destroy()
        self.variables.clear()
        self.stat_variables.clear()
        self.subject.set("Current Chao: " + self.snapshot["parameters"].get("Name", "Mori"))
        groups = {
            "Stats": set(),
            "Favorites": {"FavoriteToy", "FavoriteFruit", "FavoriteInstrument"},
            "Hidden": {"Initiative", "Agility", "Intelligence", "Luck", "Kindness", "Manners", "CourtshipReadiness", "BloomRemaining", "Sociability", "ActivityPreference", "Clumsy", "Dexterous", "Aggressiveness", "Curiosity"},
            "Care": {"Fullness", "Joy", "Energy", "Fun", "SocialNeed", "Bond", "Care", "LikesToyCars", "Glutton", "Crybaby"},
            "Life": {"IsEgg", "InCocoon", "AgeSeconds", "EggSeconds", "ChildSeconds", "AdultSeconds", "EvolutionFoodSeconds", "CocoonSeconds", "Form", "RecentAnimalSkill"},
        }
        pages = {}
        counts = {}
        for tab in ("General", *groups):
            pages[tab] = ttk.Frame(self.tabs, padding=12)
            self.tabs.add(pages[tab], text=tab)
            counts[tab] = 0
        for name, value in self.snapshot["parameters"].items():
            tab = next((tab for tab, fields in groups.items() if name in fields), "General")
            fields = pages[tab]
            index = counts[tab]
            counts[tab] += 1
            row, pair = divmod(index, 2)
            column = pair * 2
            ttk.Label(fields, text=name).grid(row=row, column=column, sticky="w", pady=4)
            if name in LEGACY:
                var = tk.StringVar(value=str(value))
                control = ttk.Label(fields, text=f"{value} (Personality — read-only adapter)", wraplength=180)
            elif isinstance(value, bool):
                var = tk.BooleanVar(value=value)
                control = ttk.Checkbutton(fields, variable=var)
            else:
                var = tk.StringVar(value="" if value is None else str(value))
                if name == "Form":
                    control = ttk.Combobox(fields, textvariable=var, state="readonly",
                                           values=("seed", "bloom", "comet", "tide", "swift", "stone"), width=13)
                elif name in groups["Favorites"]:
                    choices = {
                        "FavoriteToy": ("", "Toy Car", "Rattle", "Beach Ball"),
                        "FavoriteFruit": ("Blue Square Fruit", "Yellow Triangle Fruit", "Red Round Fruit", "Green Diamond Fruit"),
                        "FavoriteInstrument": ("Flute", "Electric Guitar", "Synthesizer", "Drums", "None"),
                    }
                    control = ttk.Combobox(fields, textvariable=var, state="readonly",
                                           values=choices[name], width=22 if name == "FavoriteFruit" else 14)
                elif name == "RecentAnimalSkill":
                    control = ttk.Combobox(fields, textvariable=var, state="readonly",
                                           values=("", "Swim", "Fly", "Run", "Power"), width=13)
                else:
                    control = ttk.Entry(fields, textvariable=var, width=15)
            control.grid(row=row, column=column + 1, sticky="w", padx=(10, 24), pady=4)
            self.variables[name] = var
        self.build_statistics(pages["Stats"])
        self.build_personality()
        self.build_relationships()
        self.build_learned_preferences()
        self.build_memory()
        self.tabs.select(tab_index)
        self.status.set("Edit the values you want to change. Applies to the running game.")

    def build_learned_preferences(self):
        page = ttk.Frame(self.tabs, padding=12)
        self.tabs.add(page, text="Experience")
        section_choice = ttk.Combobox(page, state="readonly")
        section_choice.pack(fill="x", pady=(0, 4))
        sections = ttk.Notebook(page)
        sections.pack(fill="both", expand=True)
        sections.enable_traversal()
        self.experience_sections = sections
        self.prepare_learned_widgets()
        item_editor = ttk.Frame(sections)
        sections.add(item_editor, text="Item preferences")
        self.build_learned_page(item_editor, ("toys", "fruits"))
        panel = ttk.Frame(sections)
        sections.add(panel, text="Personality and items")
        places = ttk.Frame(sections, padding=6)
        sections.add(places, text="Garden places")
        self.build_place_preferences(places)
        partners = ttk.Frame(sections, padding=6)
        sections.add(partners, text="Social partners")
        self.build_social_preferences(partners)
        habits = ttk.Frame(sections, padding=6)
        sections.add(habits, text="Activity habits")
        habit_sections = ttk.Notebook(habits)
        habit_sections.pack(fill="both", expand=True)
        habit_history = ttk.Frame(habit_sections)
        habit_edit = ttk.Frame(habit_sections)
        habit_sections.add(habit_history, text="Real experience")
        habit_sections.add(habit_edit, text="Adjust Bias")
        self.build_activity_habits(habit_history)
        self.build_learned_page(habit_edit, ("habits",))
        rhythm = ttk.Frame(sections, padding=6)
        sections.add(rhythm, text="Daily rhythm")
        self.build_daily_rhythm(rhythm)
        expression = ttk.Frame(sections, padding=6)
        sections.add(expression, text="Expression")
        self.expression_text = tk.StringVar()
        ttk.Label(expression, textvariable=self.expression_text, wraplength=660, justify="left").pack(anchor="w", pady=8)
        self.refresh_expression()
        voice_panel = ttk.Frame(self.tabs, padding=12)
        self.tabs.add(voice_panel, text="Voice and vocal reactions")
        self.voice_text = tk.StringVar()
        ttk.Label(voice_panel, textvariable=self.voice_text, wraplength=650, justify="left").pack(anchor="w")
        self.refresh_voice()
        gestures = ttk.Frame(sections, padding=6)
        sections.add(gestures, text="Idle gestures")
        self.gesture_text = tk.StringVar()
        ttk.Label(gestures, textvariable=self.gesture_text, wraplength=660, justify="left").pack(anchor="w", pady=8)
        self.refresh_idle_gestures()
        self.refresh_voice()
        section_choice.configure(values=[sections.tab(t, "text") for t in sections.tabs()])
        section_choice.current(0)
        section_choice.bind("<<ComboboxSelected>>", lambda _e: sections.select(section_choice.current()))
        sections.bind("<<NotebookTabChanged>>", lambda _e: section_choice.current(sections.index(sections.select())))
        ttk.Label(panel, text="Innate personality — read-only").pack(anchor="w")
        traits = self.snapshot.get("personality") or {}
        ttk.Label(panel, text=", ".join(f"{name}: {value}" for name, value in traits.items()), wraplength=690).pack(anchor="w", pady=(3, 10))
        data = self.snapshot.get("learnedPreferences") or {}
        favorites = data.get("fixedFavorites") or {}
        ttk.Label(panel, text="Fixed favorites").pack(anchor="w")
        ttk.Label(panel, text=" · ".join(f"{key}: {value}" for key, value in favorites.items()), wraplength=690).pack(anchor="w", pady=(3, 10))
        ttk.Label(panel, text="Learned preferences — read-only · active Chao seconds").pack(anchor="w")
        table_frame = ttk.Frame(panel)
        table_frame.pack(fill="both", expand=True, pady=5)
        columns = ("group", "id", "bias", "count", "last", "fixed")
        table = ttk.Treeview(table_frame, columns=columns, show="headings", height=10)
        for key, title, width in zip(columns, ("Group", "Item type", "Effective bias", "Experiences", "Last credit (s)", "Fixed favorite"), (55, 175, 88, 78, 95, 80)):
            table.heading(key, text=title)
            table.column(key, width=width, minwidth=45)
        scroll = ttk.Scrollbar(table_frame, orient="vertical", command=table.yview)
        table.configure(yscrollcommand=scroll.set)
        scroll.pack(side="right", fill="y")
        table.pack(side="left", fill="both", expand=True)
        for entry in data.get("entries", []):
            last = entry.get("lastCreditSeconds")
            table.insert("", "end", values=(entry["group"], entry["id"], f"{entry['effectiveBias']:+.3f}", entry["experienceCount"], "—" if last is None else f"{last:.1f}", "Yes" if entry["fixedFavorite"] else ""))

    def refresh_voice(self):
        if not hasattr(self, "voice_text"):
            return
        data = self.voice_rows.get(self.selected)
        if not data:
            self.voice_text.set("Voice and vocal reactions — read-only\nUnavailable: no active vocal executor.")
            return
        self.voice_text.set("Voice and vocal reactions — read-only · actual ChaoVocal\n\n" + "\n".join(f"{key}: {value}" for key, value in data.items()))

    def refresh_idle_gestures(self):
        if not hasattr(self, "gesture_text"):
            return
        data = self.gesture_rows.get(self.selected)
        if not data:
            self.gesture_text.set("Idle gestures — read-only\nUnavailable: no active visual controller.")
            return
        self.gesture_text.set("Idle gestures — read-only · actual visual model\n\n" +
            f"Gesture: {data['CurrentIdleGesture']}\nElapsed: {data['GestureElapsed']:.2f}s / {data['GestureDuration']:.2f}s\n" +
            f"Next eligible attempt: {data['TimeUntilNextGesture']:.2f}s\nAmplitude: {data['CurrentGestureAmplitude']:.4f} rad\n\n" +
            f"Curious chance contribution: {data['CuriousContribution']:+.3f}\n" +
            f"Energetic chance contribution: {data['EnergeticContribution']:+.3f}\n" +
            f"Carefree chance contribution: {data['CarefreeContribution']:+.3f}\n" +
            f"Solitude pose adjustment: {data['SolitudePoseAdjustment']:.4f} rad\n\n" +
            f"Pose: {data['CurrentPose']}\nSuppressed: {data['Suppressed']}\nReason: {data['SuppressionReason'] or 'None'}\n" +
            f"Started: {data['Started']} · Completed: {data['Completed']}\n\n" +
            "Decorative only. No activity, needs, emotion or memory credit.")

    def refresh_expression(self):
        if not hasattr(self, "expression_text"):
            return
        data = self.expression_rows.get(self.selected)
        if not data:
            self.expression_text.set("Expression — read-only\nNo active visual model for this Chao.")
            return
        self.expression_text.set("Expression — read-only · current live model\n\n" +
            f"Mood: {data['DominantEmotion']} · intensity: {data['EmotionIntensity']:.3f}\n" +
            f"Valence: {data['Valence']:.3f} · Arousal: {data['Arousal']:.3f}\n\n" +
            f"Expression: {data['CurrentExpression']}\nPose: {data['CurrentPose']}\nSource: {data['Source']}\n" +
            f"Personality multiplier: {data['AppliedPersonalityMultiplier']:.3f}\n" +
            f"Target visual intensity: {data['FinalVisualIntensity']:.3f}\nSmoothed intensity: {data['SmoothedVisualIntensity']:.3f}\n" +
            f"Blink: {data['BlinkAmount']:.3f}\nProtected animation: {data['ProtectedAnimation']}\n" +
            f"Mood suppression: {data['SuppressionReason'] or 'None'}\n\n" +
            "Visual state belongs to the active model; it is not saved or editable.")

    def build_daily_rhythm(self, panel):
        self.rhythm_text = tk.StringVar()
        ttk.Label(panel, textvariable=self.rhythm_text, wraplength=660, justify="left").pack(anchor="w", pady=8)
        self.refresh_daily_rhythm()

    def refresh_daily_rhythm(self):
        if not hasattr(self, "rhythm_text"):
            return
        data = self.rhythm_rows.get(self.selected, {})
        cycle = data.get("cycle") or {}
        phase = cycle.get("CurrentPhase", "Inactive")
        if isinstance(phase, int):
            phase = ("Dawn", "Day", "Dusk", "Night")[phase]
        lines = ["Daily rhythm — read-only · shared garden time", ""]
        if data.get("active"):
            lines += [f"Garden: {data['gardenSeconds']:.1f}s · cycle: {cycle['CyclePosition']:.2f}/600 · {phase}",
                      f"Daylight: {cycle['DaylightIntensity']:.3f} · Night: {cycle['NightIntensity']:.3f}"]
        else:
            lines += ["Inactive outside permanent gardens"]
        lines += [f"Personal phase offset: {data.get('personalPhaseOffset', 0):+.2f}s", f"Energy: {data.get('energy', 0):.1f}",
                  (data.get("restEligibility") or {}).get("Reason", "Waiting for game context"),
                  "Last safe-place probe: " + data.get("safePlace", "Not available"), "", "Activity       Rhythm adjustment"]
        for row in data.get("adjustments", []):
            lines.append(f"{row['Activity']:<14} {row['Adjustment']:+.4f}")
        lines += ["", "Current learned habits (unchanged)"]
        for row in (data.get("habits") or {}).get("entries", []):
            lines.append(f"{row['activity']}: {row['effectiveBias']:+.4f}")
        lines += ["", "Night rest also requires a safe, free dry place.", "No required schedule. Needs remain authoritative."]
        self.rhythm_text.set("\n".join(lines))

    def build_activity_habits(self, panel):
        data = self.snapshot.get("activityHabits") or {}
        ttk.Label(panel, text="Learned activity habits — read-only · active Chao seconds").pack(anchor="w")
        credit = data.get("LastCreditTime")
        ttk.Label(panel, text=f"Last credit: {data.get('LastCreditedCategory') or 'None'} · {'—' if credit is None else f'{credit:.1f}s'}\nLast rejected credit: {data.get('LastRejectedReason', 'None')}", wraplength=670).pack(anchor="w", pady=8)
        columns = ("activity", "bias", "count", "credit", "update")
        table = ttk.Treeview(panel, columns=columns, show="headings", height=5)
        for key, label, width in zip(columns, ("Activity", "Effective bias", "Experiences", "Last credit (s)", "Last update (s)"), (125, 125, 100, 125, 125)):
            table.heading(key, text=label)
            table.column(key, width=width, minwidth=70)
        table.pack(fill="both", expand=True)
        for row in data.get("entries", []):
            table.insert("", "end", values=(row["activity"], f"{row['effectiveBias']:+.4f}", row["experienceCount"], "—" if row["lastCreditSeconds"] is None else f"{row['lastCreditSeconds']:.1f}", "—" if row["lastUpdateSeconds"] is None else f"{row['lastUpdateSeconds']:.1f}"))
        ttk.Label(panel, text="Only completed Play, Socialize and Leisure can teach habits. Needs and availability remain authoritative.", wraplength=670).pack(anchor="w", pady=6)

    def build_social_preferences(self, panel):
        data = self.snapshot.get("socialPreferences") or {}
        ttk.Label(panel, text="Directional relationships + personal memories · read-only", wraplength=670).pack(anchor="w")
        ttk.Label(panel, text=f"Last activity: {data.get('lastInteraction', 'None')}\nLast choice: {data.get('lastChosenPartner') or 'None'}\nLast completed partner: {data.get('lastCompletedPartner') or 'None'}", wraplength=670).pack(anchor="w", pady=8)
        columns = ("id", "name", "score", "status", "memory", "relationship", "experience", "weight", "last", "available")
        table = ttk.Treeview(panel, columns=columns, show="headings", height=9)
        for key, label, width in zip(columns, ("Partner ID", "Name", "Relation", "Status", "PartnerBias", "Rel. part", "Memory part", "Partner weight", "Last final weight", "Eligible at last scan"), (210, 105, 60, 100, 85, 75, 85, 100, 110, 130)):
            table.heading(key, text=label)
            table.column(key, width=width, minwidth=50, stretch=False)
        scroll = ttk.Scrollbar(panel, orient="horizontal", command=table.xview)
        table.configure(xscrollcommand=scroll.set)
        table.pack(fill="both", expand=True)
        scroll.pack(fill="x")
        for row in data.get("partners", []):
            score, last = row["score"], row.get("lastSelection")
            table.insert("", "end", values=(row["id"], row["name"], score["Relationship"], score["Status"], f"{score['PartnerBias']:+.3f}", f"{score['RelationshipContribution']:+.3f}", f"{score['MemoryContribution']:+.3f}", f"{score['PartnerWeight']:.3f}", "—" if last is None else f"{last['FinalWeight']:.3f}", "Unknown" if last is None else "Yes" if last["Available"] else "No"))
        ttk.Label(panel, text="Partner weight excludes distance and temporary repetition. Eligibility is an observation, not a reservation.", wraplength=670).pack(anchor="w", pady=6)

    def build_place_preferences(self, panel):
        data = self.snapshot.get("placePreferences") or {}
        current = data.get("current") or {}
        ttk.Label(panel, text="Existing Mori Garden leisure destinations · memory-derived · read-only").pack(anchor="w")
        ttk.Label(panel, text=f"Current: {current.get('CurrentId') or 'None'}\nLast outcome: {current.get('Outcome', 'None')}\nRecent route: {current.get('RecentRoute') or 'None'} · Recent sky: {current.get('RecentSky') or 'None'}", wraplength=680).pack(anchor="w", pady=8)
        columns = ("id", "type", "available", "learned", "spatial", "total", "weight")
        table = ttk.Treeview(panel, columns=columns, show="headings", height=9)
        for key, label, width in zip(columns, ("Destination", "Type", "Available", "Learned", "Spatial", "Total", "Weight"), (210, 45, 65, 65, 65, 65, 60)):
            table.heading(key, text=label)
            table.column(key, width=width, minwidth=40)
        table.pack(fill="both", expand=True)
        for item in data.get("destinations", []):
            table.insert("", "end", values=(item["Id"], str(item["Type"]), "Yes" if item["Available"] else "No", f"{item['LearnedBias']:.3f}", f"{item['SpatialBias']:+.3f}", f"{item['TotalBias']:+.3f}", f"{item['SelectionWeight']:.3f}"))
        ttk.Label(panel, text="Availability comes from the active leisure executor; no targets are unlocked here.", wraplength=680).pack(anchor="w", pady=6)

    def build_statistics(self, panel):
        for child in panel.winfo_children():
            child.destroy()
        for column, text in enumerate(("Ability", "Grade", "Level / 99", "Points", "Progress / 99")):
            ttk.Label(panel, text=text).grid(row=0, column=column, padx=7, pady=8)
        for row, (name, values) in enumerate(self.snapshot["statistics"].items(), 1):
            hidden = name in ("Luck", "Intelligence")
            limit = 4000 if hidden else 3266
            ttk.Label(panel, text=name + (" (hidden)" if hidden else "")).grid(row=row, column=0, sticky="w")
            variables = {}
            for column, key in enumerate(("Grade", "Level", "Points", "Progress"), 1):
                value = values[key]
                var = tk.StringVar(value="EDCBAS"[value] if key == "Grade" and not hidden else str(value))
                variables[key] = var
                if key == "Grade" and not hidden:
                    control = ttk.Combobox(panel, textvariable=var, values=tuple("EDCBAS"), state="readonly", width=5)
                else:
                    maximum = (255 if hidden else 5) if key == "Grade" else limit if key == "Points" else 99
                    control = ttk.Spinbox(panel, textvariable=var, from_=0, to=maximum, width=9)
                control.grid(row=row, column=column, padx=7, pady=5)
            ttk.Label(panel, text=f"max {limit}").grid(row=row, column=5)
            self.stat_variables[name] = variables
        row = len(self.stat_variables) + 1
        ttk.Label(panel, text="Points determine ability. Level and grade do not directly set strength.").grid(row=row, column=0, columnspan=6, sticky="w", pady=14)
        preset = ttk.Frame(panel)
        preset.grid(row=row+1, column=0, columnspan=6, sticky="w")
        for grade in ("E", "C", "S"):
            ttk.Button(preset, text=f"Run Lv99 / {grade}", command=lambda g=grade: self.stat_preset(g)).pack(side="left", padx=4)
        ttk.Label(panel, text="Presets change fields only. Click Apply changes to send them.").grid(row=row+2, column=0, columnspan=6, sticky="w", pady=8)

    def stat_preset(self, grade):
        fields = self.stat_variables.get("Run")
        if fields:
            for key, value in dict(Grade=grade, Level=99, Points=99*(13+3*"EDCBAS".index(grade)), Progress=0).items():
                fields[key].set(str(value))

    def statistics_patch(self):
        changes = {}
        for name, fields in self.stat_variables.items():
            hidden = name in ("Luck", "Intelligence")
            values = {key: ("EDCBAS".index(var.get()) if key == "Grade" and not hidden else int(var.get())) for key, var in fields.items()}
            maximum = 4000 if hidden else 3266
            if not (0 <= values["Grade"] <= (255 if hidden else 5) and 0 <= values["Level"] <= 99 and 0 <= values["Points"] <= maximum and 0 <= values["Progress"] <= 99):
                raise ValueError(f"{name}: value outside the displayed limits.")
            if values["Level"] == 0 and values["Points"] != 0:
                raise ValueError(f"{name}: level zero has zero points.")
            old = self.snapshot["statistics"][name]
            unchanged_baseline = all(values[key] == old[key] for key in ("Grade", "Level", "Points"))
            lower = max(0, values["Level"]-1)*11
            upper = min(maximum, (400 if hidden else 326) + values["Level"]*(15+3*values["Grade"]))
            if not unchanged_baseline and not lower <= values["Points"] <= upper:
                raise ValueError(f"{name}: points do not fit this level/grade. Use a preset.")
            if values != self.snapshot["statistics"][name]:
                changes[name] = values
        return changes

    def build_relationships(self):
        panel = ttk.Frame(self.tabs, padding=12)
        self.tabs.add(panel, text="Relationships")
        rows = self.snapshot["relationships"]
        if not rows:
            self.relationship_peer = None
            ttk.Label(panel, text="No other hatched Chao.").pack(anchor="w")
            return
        ttk.Label(panel, text="Choose a Chao to inspect").pack(anchor="w")
        picker = ttk.Combobox(panel, values=tuple(row["name"] for row in rows), state="readonly", width=32)
        picker.pack(anchor="w", pady=(4, 12))
        self.relation_status = tk.StringVar()
        ttk.Label(panel, textvariable=self.relation_status).pack(anchor="w")
        self.relation_meter = tk.DoubleVar(value=100)
        ttk.Progressbar(panel, maximum=200, length=350, variable=self.relation_meter).pack(anchor="w", pady=(8, 0))
        ttk.Label(panel, text="Hate                         Neutral                         Love").pack(anchor="w", pady=(0, 8))
        controls = ttk.LabelFrame(panel, text="Details", padding=10)
        controls.pack(anchor="w", fill="x", pady=6)
        self.relation_met = tk.BooleanVar()
        ttk.Checkbutton(controls, text="Introduced", variable=self.relation_met).pack(side="left", padx=(0, 15))
        self.relation_score = tk.StringVar()
        ttk.Label(controls, text="Affinity").pack(side="left")
        ttk.Spinbox(controls, from_=-100, to=100, textvariable=self.relation_score, width=6).pack(side="left", padx=7)
        self.relation_score.trace_add("write", self.update_relation_meter)

        def select_peer(_event=None):
            index = picker.current()
            if index < 0:
                return
            row = rows[index]
            self.relationship_peer = row["id"]
            self.relation_status.set(row["status"])
            self.relation_met.set(row["met"])
            self.relation_score.set(str(row["score"]))

        picker.bind("<<ComboboxSelected>>", select_peer)
        picker.current(next((i for i, row in enumerate(rows) if row["id"] == self.relationship_peer), 0))
        select_peer()

    def update_relation_meter(self, *_args):
        try:
            self.relation_meter.set(max(-100, min(100, int(self.relation_score.get()))) + 100)
        except ValueError:
            pass

    def unrelated_draft(self):
        values = {k: var.get() for k, var in self.variables.items() if k not in LEGACY and var.get() != (self.snapshot["parameters"][k] if isinstance(self.snapshot["parameters"][k], bool) else str(self.snapshot["parameters"][k] if self.snapshot["parameters"][k] is not None else ""))}
        stats = {}
        for name, fields in self.stat_variables.items():
            for key, var in fields.items():
                old = self.snapshot["statistics"][name][key]
                expected = "EDCBAS"[old] if key == "Grade" and name not in ("Luck", "Intelligence") else str(old)
                if var.get() != expected:
                    stats[(name, key)] = var.get()
        relation = None
        old = next((r for r in self.snapshot["relationships"] if r["id"] == self.relationship_peer), None)
        if old and (self.relation_met.get() != old["met"] or self.relation_score.get() != str(old["score"])):
            relation = (self.relation_met.get(), self.relation_score.get())
        return values, stats, relation

    def restore_unrelated_draft(self, draft):
        values, stats, relation = draft
        for key, value in values.items():
            if key in self.variables:
                self.variables[key].set(value)
        for (name, key), value in stats.items():
            if name in self.stat_variables:
                self.stat_variables[name][key].set(value)
        if relation:
            self.relation_met.set(relation[0])
            self.relation_score.set(relation[1])

    def apply(self, personality_only=False, learned_only=False, drafts_only=False):
        if self.snapshot is None or self.pending is not None:
            return
        if self.snapshot["target"] != getattr(self, "selected", self.snapshot["target"]):
            self.status.set("Selected Chao changed. Read its values before applying.")
            return
        try:
            current = self.read()
            if current["session"] != self.snapshot["session"]:
                self.status.set("The game restarted. Read the new values first.")
                return
            if not any(chao["id"] == self.snapshot["target"] for chao in current["chao"]):
                self.status.set("This Chao is no longer in the garden.")
                return
            scoped = personality_only or learned_only or drafts_only
            patch = {}
            for key, old in ({} if scoped else self.snapshot["parameters"]).items():
                if key in LEGACY:
                    continue
                value = self.variables[key].get()
                if isinstance(old, bool):
                    value = bool(value)
                elif key in {"Fullness", "Joy", "Energy", "AgeSeconds", "EggSeconds", "ChildSeconds", "CocoonSeconds"}:
                    value = float(value)
                elif isinstance(old, int):
                    value = int(value)
                elif isinstance(old, float):
                    value = float(value)
                elif key == "RecentAnimalSkill":
                    value = value or None
                if value != old:
                    patch[key] = value
            relationship = {}
            old_relation = next((row for row in self.snapshot["relationships"]
                                 if row["id"] == self.relationship_peer), None)
            if old_relation is not None and not scoped:
                met = self.relation_met.get()
                score = int(self.relation_score.get())
                if not -100 <= score <= 100:
                    raise ValueError("Affinity must be between -100 and 100.")
                if met != old_relation["met"]:
                    relationship["met"] = met
                if score != old_relation["score"]:
                    relationship["score"] = score
            statistics = {} if scoped else self.statistics_patch()
            personality = {} if learned_only else self.personality_patch()
            learned = {} if personality_only else self.learned_patch()
            if not patch and not relationship and not statistics and not personality and not learned:
                self.status.set("No changes to apply.")
                return
            request_id = uuid4().hex
            command = self.directory / "chao-editor-request.json"
            temporary = command.with_suffix(".tmp")
            request = {"session": current["session"], "id": request_id,
                       "target": self.snapshot["target"], "patch": patch, "statistics": statistics}
            if personality:
                request["personality"] = personality
                request["personalityRevision"] = self.active_personality_draft().revision
            if learned:
                request["learnedAdjustments"] = learned
                request["learnedRevision"] = self.active_learned_draft().revision
            if relationship:
                request["relationship"] = {"other": self.relationship_peer, **relationship}
            temporary.write_text(json.dumps(request, allow_nan=False), encoding="utf-8")
            temporary.replace(command)
            self.pending_other_draft = self.unrelated_draft() if scoped else None
            self.pending_sections = {key for key in ("personality", "learnedAdjustments") if key in request}
            self.pending = request_id
            self.refresh_personality_controls()
            self.refresh_learned_controls()
            self.status.set("Waiting for the game…")
            self.window.after(400, lambda: self.check(16))
        except (OSError, ValueError) as error:
            self.status.set(f"Could not send changes: {error}")

    def check(self, attempts):
        try:
            snapshot = self.read()
            if snapshot["session"] != self.snapshot["session"]:
                self.pending = None
                self.sync_personality_snapshot(snapshot)
                self.sync_learned_snapshot(snapshot)
                self.status.set("The game restarted. Draft retained. Read from game before applying.")
                return
            if snapshot["lastRequest"] == self.pending:
                self.pending = None
                if snapshot["status"] == "Applied":
                    sent = getattr(self, "pending_sections", {"personality"})
                    self.sync_personality_snapshot(snapshot, confirmed="personality" in sent)
                    self.sync_learned_snapshot(snapshot, confirmed="learnedAdjustments" in sent)
                    self.reload(force=True)
                    if getattr(self, "pending_other_draft", None) is not None:
                        self.restore_unrelated_draft(self.pending_other_draft)
                else:
                    self.refresh_personality_controls()
                    self.refresh_learned_controls()
                self.status.set(snapshot["status"])
                return
        except (OSError, ValueError):
            pass
        if attempts > 0:
            self.window.after(400, lambda: self.check(attempts - 1))
        else:
            self.pending = None
            self.refresh_personality_controls()
            self.refresh_learned_controls()
            self.status.set("No reply. Check that Mori is still open.")


if __name__ == "__main__":
    app = tk.Tk()
    ChaoEditor(app)
    app.mainloop()
