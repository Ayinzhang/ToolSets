import tkinter as tk
from tkinter import ttk
from Core import Core

class Application(tk.Tk):
    def __init__(self):
        super().__init__()
        self.core = Core()
        self.title("AutoClicker")
        self.geometry("360x64")
        self.resizable(False, False)
        self.option_add("*Font", ("Segoe UI", 8))
        self.toolTip = None
        self.toolTipTimer = None

        layout = ttk.Frame(self, padding=0)
        layout.pack(fill="both", expand=True)
        for column, width in enumerate((70, 90, 100, 90)):
            layout.columnconfigure(column, minsize=width)
        for row in range(2):
            layout.rowconfigure(row, weight=1)

        ttk.Label(layout, text="Click").grid(row=0, column=0, sticky="w")
        self.click = ttk.Combobox(layout, values=("Left", "Middle", "Right"), state="readonly", width=8)
        self.click.current(self.core.clickType)
        self.click.grid(row=0, column=1, sticky="ew", padx=1)
        self.click.bind("<<ComboboxSelected>>", lambda _: setattr(self.core, "clickType", self.click.current()))

        ttk.Label(layout, text="Gap(ms)").grid(row=0, column=2, sticky="w")
        self.gap = self.Number(layout, "gapMs", 0, 3)

        ttk.Label(layout, text="Hotkey").grid(row=1, column=0, sticky="w")
        self.hotKey = ttk.Button(layout, text=self.core.hotKeyName, command=self.ToggleHotKeyBinding)
        self.hotKey.grid(row=1, column=1, sticky="ew", padx=1)

        ttk.Label(layout, text="PowerOff(mins)").grid(row=1, column=2, sticky="w")
        self.powerOff = self.Number(layout, "powerOffMins", 1, 3)

        self.SetToolTip(self.click, "Simu the L/M/R mouse click.")
        self.SetToolTip(self.hotKey, "Click to bind a key. Esc cancels. Works in the background.\nEnter and KeypadEnter share the same hotkey.")
        self.SetToolTip(self.powerOff, "Normal PC shutdown after this many minutes of clicking. 0 or less disables it.\nStopping cancels; restarting resets the countdown.")

        self.bind_class("HotKey", "<KeyPress>", self.ProcessKey)
        self.bind_class("HotKey", "<KeyRelease>", self.ProcessKey)
        for widget in layout.winfo_children():
            widget.bindtags(("HotKey",) + widget.bindtags())
        self.bindtags(("HotKey",) + self.bindtags())

        self.protocol("WM_DELETE_WINDOW", self.Close)
        self.timer = self.after(10, self.Update)

    def Run(self):
        self.mainloop()

    def Update(self):
        self.core.Update()
        self.DrawUI()
        self.timer = self.after(10, self.Update)

    def DrawUI(self):
        self.hotKey.configure(text="Press a key" if self.core.isSettingHotKey else self.core.hotKeyName)

    def ToggleHotKeyBinding(self):
        self.core.ToggleHotKeyBinding()
        self.DrawUI()

    def ProcessKey(self, event):
        if self.core.isSettingHotKey or event.keycode == self.core.hotKey:
            return "break"

    def Number(self, parent, field, row, column):
        value = tk.StringVar(value=str(getattr(self.core, field)))

        def IsNumber(text):
            return text in ("", "-") or (text.removeprefix("-").isascii() and text.removeprefix("-").isdigit() and -(2**31) <= int(text) < 2**31)

        def Changed(*_):
            text = value.get()
            if text not in ("", "-"):
                setattr(self.core, field, int(text))

        number = ttk.Spinbox(parent, from_=-(2**31), to=2**31 - 1, textvariable=value, width=8, validate="key", validatecommand=(self.register(IsNumber), "%P"))
        value.trace_add("write", Changed)
        number.grid(row=row, column=column, sticky="ew", padx=1)
        return number

    def SetToolTip(self, widget, text):
        widget.bind("<Enter>", lambda _: setattr(self, "toolTipTimer", self.after(500, lambda: self.ShowToolTip(widget, text))))
        widget.bind("<Leave>", self.HideToolTip)
        widget.bind("<ButtonPress>", self.HideToolTip, add="+")

    def ShowToolTip(self, widget, text):
        self.toolTipTimer = None
        self.toolTip = tk.Toplevel(self)
        self.toolTip.overrideredirect(True)
        self.toolTip.geometry(f"+{widget.winfo_rootx()}+{widget.winfo_rooty() + widget.winfo_height() + 4}")
        ttk.Label(self.toolTip, text=text, padding=4, relief="solid").pack()

    def HideToolTip(self, _=None):
        if self.toolTipTimer is not None:
            self.after_cancel(self.toolTipTimer)
            self.toolTipTimer = None
        if self.toolTip is not None:
            self.toolTip.destroy()
            self.toolTip = None

    def Close(self):
        self.after_cancel(self.timer)
        self.HideToolTip()
        self.destroy()
