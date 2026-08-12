from __future__ import annotations

import json
import threading
import tkinter as tk
from datetime import datetime
from pathlib import Path
from tkinter import filedialog, messagebox, ttk

from .core import (FOLDERS, SafetyError, build_plan, execute_plan, folder_stats, format_bytes,
                   list_drives, read_known_folders, restart_explorer, restore_registry)


class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Migrador seguro de carpetas de Windows")
        self.geometry("1050x720")
        self.minsize(900, 620)
        self.configure(bg="#f4f6f8")
        self.known = read_known_folders()
        self.drives = []
        self.stats = {}
        self.vars = {label: tk.BooleanVar(value=True) for label in FOLDERS}
        self.base = tk.StringVar()
        self.status = tk.StringVar(value="Analizando carpetas…")
        self._build()
        self.refresh()

    def _build(self):
        style = ttk.Style(self)
        style.theme_use("vista")
        style.configure("Title.TLabel", font=("Segoe UI", 19, "bold"), background="#f4f6f8")
        style.configure("Card.TFrame", background="white")
        top = ttk.Frame(self, padding=20)
        top.pack(fill="x")
        ttk.Label(top, text="Migrador seguro de carpetas", style="Title.TLabel").pack(anchor="w")
        ttk.Label(top, text="Copia, verifica y redirige tus carpetas personales sin borrar los originales.").pack(anchor="w", pady=(4, 0))

        body = ttk.Panedwindow(self, orient="horizontal")
        body.pack(fill="both", expand=True, padx=20, pady=(0, 12))
        left = ttk.Frame(body, padding=16, style="Card.TFrame")
        right = ttk.Frame(body, padding=16, style="Card.TFrame")
        body.add(left, weight=3); body.add(right, weight=2)

        ttk.Label(left, text="1. Destino", font=("Segoe UI", 12, "bold"), background="white").pack(anchor="w")
        destination = ttk.Frame(left, style="Card.TFrame")
        destination.pack(fill="x", pady=8)
        ttk.Entry(destination, textvariable=self.base).pack(side="left", fill="x", expand=True)
        ttk.Button(destination, text="Examinar…", command=self.choose_base).pack(side="left", padx=(8, 0))
        self.drive_box = ttk.Combobox(left, state="readonly")
        self.drive_box.pack(fill="x", pady=(0, 14)); self.drive_box.bind("<<ComboboxSelected>>", self.choose_drive)

        ttk.Label(left, text="2. Carpetas", font=("Segoe UI", 12, "bold"), background="white").pack(anchor="w")
        self.folder_frame = ttk.Frame(left, style="Card.TFrame")
        self.folder_frame.pack(fill="x", pady=8)
        for row, label in enumerate(FOLDERS):
            ttk.Checkbutton(self.folder_frame, text=label, variable=self.vars[label], command=self.update_preview).grid(row=row, column=0, sticky="w", pady=4)
            size_label = ttk.Label(self.folder_frame, text="Calculando…", background="white")
            size_label.grid(row=row, column=1, sticky="e", padx=12)
            path_label = ttk.Label(self.folder_frame, text=self.known[label], foreground="#65717c", background="white")
            path_label.grid(row=row, column=2, sticky="w")
            setattr(self, f"size_{row}", size_label)
        self.folder_frame.columnconfigure(2, weight=1)

        ttk.Label(left, text="3. Vista previa origen → destino", font=("Segoe UI", 12, "bold"), background="white").pack(anchor="w", pady=(10, 5))
        self.preview = tk.Text(left, height=8, wrap="none", font=("Consolas", 9), relief="flat", bg="#f7f8fa")
        self.preview.pack(fill="both", expand=True)

        ttk.Label(right, text="Capacidad del disco", font=("Segoe UI", 12, "bold"), background="white").pack(anchor="w")
        self.canvas = tk.Canvas(right, width=250, height=250, bg="white", highlightthickness=0)
        self.canvas.pack(pady=10)
        self.capacity = ttk.Label(right, text="", justify="center", background="white", font=("Segoe UI", 10))
        self.capacity.pack()
        self.required = ttk.Label(right, text="", justify="left", background="white", font=("Segoe UI", 11, "bold"))
        self.required.pack(fill="x", pady=18)
        ttk.Separator(right).pack(fill="x", pady=8)
        ttk.Label(right, text="Protecciones activas", font=("Segoe UI", 11, "bold"), background="white").pack(anchor="w")
        ttk.Label(right, text="• Nunca sobrescribe archivos\n• Nunca borra los originales\n• Verifica la copia antes de cambiar Windows\n• Bloquea rutas críticas y falta de espacio\n• Crea respaldo restaurable del registro", justify="left", background="white").pack(anchor="w", pady=7)
        self.apply_btn = ttk.Button(right, text="Revisar y aplicar migración", command=self.confirm)
        self.apply_btn.pack(fill="x", pady=(15, 6))
        ttk.Button(right, text="Restaurar desde respaldo…", command=self.restore).pack(fill="x")

        ttk.Label(self, textvariable=self.status, anchor="w", padding=(20, 8)).pack(fill="x")

    def refresh(self):
        self.drives = list_drives()
        self.drive_box["values"] = [f"{d['root']}  {d['label']} — {format_bytes(d['free'])} libres de {format_bytes(d['total'])}" for d in self.drives]
        if self.drives:
            self.drive_box.current(0)
        def scan():
            for index, label in enumerate(FOLDERS):
                self.stats[label] = folder_stats(Path(self.known[label]))
                self.after(0, lambda i=index, l=label: getattr(self, f"size_{i}").configure(text=format_bytes(self.stats[l][0])))
            self.after(0, self.update_preview)
            self.after(0, lambda: self.status.set("Listo. Selecciona una carpeta base segura."))
        threading.Thread(target=scan, daemon=True).start()
        self.draw_disk()

    def choose_drive(self, _event=None):
        self.draw_disk()

    def draw_disk(self):
        self.canvas.delete("all")
        if not self.drives: return
        d = self.drives[max(0, self.drive_box.current())]
        used = int(d["total"]) - int(d["free"]); ratio = used / max(1, int(d["total"]))
        self.canvas.create_oval(25, 25, 225, 225, fill="#dce7ef", outline="")
        self.canvas.create_arc(25, 25, 225, 225, start=90, extent=-359.9 * ratio, fill="#277da1", outline="")
        self.canvas.create_oval(72, 72, 178, 178, fill="white", outline="")
        self.canvas.create_text(125, 113, text=d["root"], font=("Segoe UI", 16, "bold"))
        self.canvas.create_text(125, 139, text=format_bytes(int(d["total"])), font=("Segoe UI", 10))
        self.capacity.configure(text=f"Usado: {format_bytes(used)} ({ratio:.1%})\nLibre: {format_bytes(int(d['free']))}")
        self.update_preview()

    def choose_base(self):
        path = filedialog.askdirectory(title="Selecciona una carpeta base (no la raíz del disco)")
        if path:
            self.base.set(path); self.update_preview()

    def selected(self): return [name for name, var in self.vars.items() if var.get()]

    def update_preview(self):
        total = sum(self.stats.get(label, (0, 0))[0] for label in self.selected())
        self.preview.delete("1.0", "end")
        base = Path(self.base.get()) if self.base.get() else None
        for label in self.selected():
            dest = base / FOLDERS[label][1] if base else "(elige destino)"
            self.preview.insert("end", f"{self.known[label]}\n  → {dest}\n")
        free = 0
        if self.drives:
            free = int(self.drives[max(0, self.drive_box.current())]["free"])
        reserve = 100 * 1024 * 1024
        ok = bool(base and self.selected() and free >= total + reserve)
        self.required.configure(text=f"Datos a copiar: {format_bytes(total)}\nLibre estimado después: {format_bytes(max(0, free-total))}" + ("\n✓ Espacio suficiente" if ok else "\n⚠ Revisa destino y espacio"))
        self.apply_btn.configure(state="normal" if ok else "disabled")

    def confirm(self):
        try:
            plan = build_plan(Path(self.base.get()), self.selected(), self.known)
        except Exception as exc:
            messagebox.showerror("Operación bloqueada", str(exc)); return
        summary = "\n".join(f"• {p.label}: {format_bytes(p.size)}\n  {p.source}\n  → {p.destination}" for p in plan)
        if not messagebox.askyesno("Confirmación final", f"Se copiarán y verificarán estas carpetas:\n\n{summary}\n\nLos originales NO se borrarán. ¿Continuar?"):
            return
        self.apply_btn.configure(state="disabled"); self.status.set("Iniciando migración…")
        backup = Path.home() / "Documents" / "Respaldos Migrador Seguro" / f"known-folders-{datetime.now():%Y%m%d-%H%M%S}.json"
        def run():
            try:
                execute_plan(plan, backup, lambda msg: self.after(0, lambda m=msg: self.status.set(m)))
                self.after(0, lambda: self.done(backup))
            except Exception as exc:
                self.after(0, lambda e=exc: self.failed(e))
        threading.Thread(target=run, daemon=True).start()

    def done(self, backup):
        self.status.set("Migración completada correctamente.")
        if messagebox.askyesno("Migración completada", f"Windows ya apunta al nuevo destino.\nRespaldo: {backup}\n\nLos originales siguen intactos. ¿Reiniciar el Explorador ahora?"):
            restart_explorer()
        self.update_preview()

    def failed(self, exc):
        self.status.set("La migración fue detenida de forma segura.")
        messagebox.showerror("Migración detenida", f"{exc}\n\nNo se borraron archivos. Si alcanzó a copiar datos, permanecerán en el destino para revisión.")
        self.update_preview()

    def restore(self):
        path = filedialog.askopenfilename(title="Selecciona el respaldo JSON", filetypes=[("Respaldo JSON", "*.json")])
        if not path: return
        if not messagebox.askyesno("Restaurar rutas", "Esto restaurará las rutas registradas en el respaldo. No moverá ni borrará archivos. ¿Continuar?"):
            return
        try:
            restore_registry(Path(path))
            if messagebox.askyesno("Restaurado", "Las rutas fueron restauradas. ¿Reiniciar el Explorador?"):
                restart_explorer()
        except Exception as exc:
            messagebox.showerror("No se pudo restaurar", str(exc))


def main():
    App().mainloop()


if __name__ == "__main__":
    main()
