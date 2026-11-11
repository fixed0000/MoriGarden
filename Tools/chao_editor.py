import json
import os
import tkinter as tk
from pathlib import Path
from tkinter import ttk
from uuid import uuid4


class ChaoEditor:
    def __init__(self, window):
        self.window = window
        self.directory = Path(os.environ["APPDATA"]) / "Godot" / "app_userdata" / "Mori · маленький сад"
        self.snapshot = None
        self.variables = {}
        self.roster = {}
        self.relationship_rows = {}
        self.relationship_peer = None
        self.selected = None
        self.pending = None
        self.status = tk.StringVar(value="Start Mori, then click Read from game.")
        window.title("Mori — Chao Editor")
        window.geometry("790x635")
        window.minsize(710, 540)
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
        ttk.Label(window, textvariable=self.status, padding=12, wraplength=550).pack(fill="x")
        self.reload()
        self.window.after(1000, self.refresh_roster)

    def read(self):
        return json.loads((self.directory / "chao-editor-state.json").read_text(encoding="utf-8"))

    def update_roster(self, snapshot):
        roster = {chao["id"]: chao["parameters"] for chao in snapshot.get("chao", [])}
        names = [(key, values["Name"]) for key, values in roster.items()]
        if names != [(key, values["Name"]) for key, values in self.roster.items()]:
            self.list.delete(0, "end")
            for _, name in names:
                self.list.insert("end", name)
        self.roster = roster
        self.relationship_rows = {chao["id"]: chao.get("relationships", []) for chao in snapshot.get("chao", [])}
        if self.selected not in roster:
            self.selected = next(iter(roster), None)
        if self.selected is not None:
            self.list.selection_set(list(roster).index(self.selected))

    def refresh_roster(self):
        try:
            snapshot = self.read()
            previous = self.selected
            self.update_roster(snapshot)
            if self.snapshot is None or previous != self.selected or snapshot["session"] != self.snapshot["session"]:
                self.reload()
        except (OSError, ValueError):
            pass
        self.window.after(1000, self.refresh_roster)

    def select_chao(self, _event):
        selection = self.list.curselection()
        if selection and self.pending is None:
            self.selected = list(self.roster)[selection[0]]
            self.reload()

    def reload(self):
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
                             parameters=self.roster[self.selected],
                             relationships=self.relationship_rows.get(self.selected, []))
        tab_index = self.tabs.index("current") if self.tabs.select() else 0
        for widget in self.tabs.winfo_children():
            widget.destroy()
        self.variables.clear()
        self.subject.set("Current Chao: " + self.snapshot["parameters"].get("Name", "Mori"))
        groups = {
            "Stats": {"Swim", "Fly", "Run", "Power", "Stamina", "Initiative", "Agility"},
            "Favorites": {"FavoriteToy", "FavoriteFruit", "FavoriteInstrument"},
            "Hidden": {"Intelligence", "Luck", "Kindness", "Manners", "CourtshipReadiness", "BloomRemaining", "Sociability", "ActivityPreference", "Clumsy", "Dexterous", "Aggressiveness", "Curiosity"},
            "Care": {"Fullness", "Joy", "Energy", "Fun", "Bond", "Care", "LikesToyCars", "Glutton", "Crybaby"},
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
            if isinstance(value, bool):
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
                        "FavoriteInstrument": ("Flute", "Electric Guitar", "Synthesizer", "None"),
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
        self.build_relationships()
        self.tabs.select(tab_index)
        self.status.set("Edit the values you want to change. Applies to the running game.")

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

    def apply(self):
        if self.snapshot is None or self.pending is not None:
            return
        try:
            current = self.read()
            if current["session"] != self.snapshot["session"]:
                self.status.set("The game restarted. Read the new values first.")
                return
            if not any(chao["id"] == self.snapshot["target"] for chao in current["chao"]):
                self.status.set("This Chao is no longer in the garden.")
                return
            patch = {}
            for key, old in self.snapshot["parameters"].items():
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
            if old_relation is not None:
                met = self.relation_met.get()
                score = int(self.relation_score.get())
                if not -100 <= score <= 100:
                    raise ValueError("Affinity must be between -100 and 100.")
                if met != old_relation["met"]:
                    relationship["met"] = met
                if score != old_relation["score"]:
                    relationship["score"] = score
            if not patch and not relationship:
                self.status.set("No changes to apply.")
                return
            request_id = uuid4().hex
            command = self.directory / "chao-editor-request.json"
            temporary = command.with_suffix(".tmp")
            request = {"session": current["session"], "id": request_id,
                       "target": self.snapshot["target"], "patch": patch}
            if relationship:
                request["relationship"] = {"other": self.relationship_peer, **relationship}
            temporary.write_text(json.dumps(request, allow_nan=False), encoding="utf-8")
            temporary.replace(command)
            self.pending = request_id
            self.status.set("Waiting for the game…")
            self.window.after(400, lambda: self.check(16))
        except (OSError, ValueError) as error:
            self.status.set(f"Could not send changes: {error}")

    def check(self, attempts):
        try:
            snapshot = self.read()
            if snapshot["lastRequest"] == self.pending:
                self.pending = None
                if snapshot["status"] == "Applied":
                    self.reload()
                self.status.set(snapshot["status"])
                return
        except (OSError, ValueError):
            pass
        if attempts > 0:
            self.window.after(400, lambda: self.check(attempts - 1))
        else:
            self.pending = None
            self.status.set("No reply. Check that Mori is still open.")


if __name__ == "__main__":
    app = tk.Tk()
    ChaoEditor(app)
    app.mainloop()
