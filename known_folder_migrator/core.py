from __future__ import annotations

import ctypes
import json
import os
import shutil
import string
import subprocess
import time
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Callable, Iterable

try:
    import winreg
except ImportError:  # enables tests on non-Windows hosts
    winreg = None


FOLDERS = {
    "Escritorio": ("Desktop", "Desktop"),
    "Documentos": ("Personal", "Documents"),
    "Descargas": ("{374DE290-123F-4565-9164-39C4925E467B}", "Downloads"),
    "Imágenes": ("My Pictures", "Pictures"),
    "Música": ("My Music", "Music"),
    "Vídeos": ("My Video", "Videos"),
}

CRITICAL_NAMES = {"windows", "program files", "program files (x86)", "programdata", "appdata"}
USER_SHELL = r"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"
SHELL = r"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders"


class SafetyError(RuntimeError):
    pass


@dataclass(frozen=True)
class FolderPlan:
    label: str
    registry_name: str
    source: str
    destination: str
    size: int
    files: int


def format_bytes(value: int) -> str:
    units = ("B", "KB", "MB", "GB", "TB")
    amount = float(max(0, value))
    for unit in units:
        if amount < 1024 or unit == units[-1]:
            return f"{amount:.0f} {unit}" if unit in ("B", "KB") else f"{amount:.1f} {unit}"
        amount /= 1024
    return f"{amount:.1f} TB"


def expand_registry_path(value: str) -> Path:
    return Path(os.path.expandvars(value)).resolve()


def path_is_within(child: Path, parent: Path) -> bool:
    try:
        child.resolve().relative_to(parent.resolve())
        return True
    except ValueError:
        return False


def validate_destination(base: Path, sources: Iterable[Path], windows_dir: Path | None = None) -> Path:
    raw = str(base).strip()
    if not raw:
        raise SafetyError("Selecciona una carpeta de destino.")
    base = Path(raw).resolve()
    if not base.is_absolute() or base.parent == base:
        raise SafetyError("No se permite usar la raíz de una unidad.")
    parts = {part.casefold() for part in base.parts}
    if parts & CRITICAL_NAMES:
        raise SafetyError("La ruta está dentro de una carpeta crítica de Windows.")
    win = (windows_dir or Path(os.environ.get("WINDIR", r"C:\Windows"))).resolve()
    critical = [win, win.parent / "Program Files", win.parent / "Program Files (x86)", win.parent / "ProgramData"]
    if any(path_is_within(base, item) for item in critical):
        raise SafetyError("La ruta está dentro de una carpeta protegida.")
    for source in sources:
        source = source.resolve()
        if path_is_within(base, source) or path_is_within(source, base):
            raise SafetyError("Origen y destino no pueden contenerse entre sí.")
    return base


def folder_stats(path: Path) -> tuple[int, int]:
    total = count = 0
    if not path.exists():
        return 0, 0
    for root, dirs, files in os.walk(path, followlinks=False):
        dirs[:] = [d for d in dirs if not Path(root, d).is_symlink()]
        for name in files:
            item = Path(root, name)
            try:
                if not item.is_symlink():
                    total += item.stat().st_size
                    count += 1
            except OSError:
                pass
    return total, count


def read_known_folders() -> dict[str, str]:
    if winreg is None:
        home = Path.home()
        return {label: str(home / fallback) for label, (_, fallback) in FOLDERS.items()}
    result: dict[str, str] = {}
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, USER_SHELL) as key:
        for label, (name, fallback) in FOLDERS.items():
            try:
                value, _ = winreg.QueryValueEx(key, name)
                result[label] = str(expand_registry_path(value))
            except OSError:
                result[label] = str(Path.home() / fallback)
    return result


def list_drives() -> list[dict[str, int | str]]:
    drives = []
    if os.name != "nt":
        usage = shutil.disk_usage(Path.home())
        return [{"root": str(Path.home().anchor), "label": "", "total": usage.total, "free": usage.free}]
    mask = ctypes.windll.kernel32.GetLogicalDrives()
    for index, letter in enumerate(string.ascii_uppercase):
        if not mask & (1 << index):
            continue
        root = f"{letter}:\\"
        try:
            usage = shutil.disk_usage(root)
            volume = ctypes.create_unicode_buffer(261)
            ctypes.windll.kernel32.GetVolumeInformationW(root, volume, 261, None, None, None, None, 0)
            drives.append({"root": root, "label": volume.value, "total": usage.total, "free": usage.free})
        except OSError:
            continue
    return drives


def build_plan(base: Path, selected: Iterable[str], known: dict[str, str]) -> list[FolderPlan]:
    labels = list(selected)
    sources = [Path(known[label]) for label in labels]
    base = validate_destination(base, sources)
    plans = []
    for label in labels:
        registry_name, fallback = FOLDERS[label]
        source = Path(known[label]).resolve()
        destination = (base / fallback).resolve()
        validate_destination(destination, [source])
        if destination.exists() and any(destination.iterdir()):
            raise SafetyError(f"{destination} ya contiene archivos. No se sobrescribirá.")
        size, files = folder_stats(source)
        plans.append(FolderPlan(label, registry_name, str(source), str(destination), size, files))
    return plans


def backup_registry(path: Path) -> dict:
    known = read_known_folders()
    data = {"version": 1, "created": time.strftime("%Y-%m-%dT%H:%M:%S"), "folders": known}
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    return data


def _set_registry(name: str, value: str) -> None:
    if winreg is None:
        raise SafetyError("El registro de Windows no está disponible.")
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, USER_SHELL, 0, winreg.KEY_SET_VALUE) as key:
        winreg.SetValueEx(key, name, 0, winreg.REG_EXPAND_SZ, value)
    with winreg.CreateKey(winreg.HKEY_CURRENT_USER, SHELL) as key:
        winreg.SetValueEx(key, name, 0, winreg.REG_SZ, os.path.expandvars(value))


def _copy_verified(source: Path, destination: Path, progress: Callable[[str], None]) -> None:
    destination.mkdir(parents=True, exist_ok=True)
    for root, dirs, files in os.walk(source, followlinks=False):
        rel = Path(root).relative_to(source)
        target_dir = destination / rel
        target_dir.mkdir(parents=True, exist_ok=True)
        dirs[:] = [d for d in dirs if not Path(root, d).is_symlink()]
        for name in files:
            src = Path(root, name)
            dst = target_dir / name
            if src.is_symlink():
                continue
            if dst.exists():
                raise SafetyError(f"Conflicto inesperado: {dst}")
            shutil.copy2(src, dst)
            if src.stat().st_size != dst.stat().st_size:
                raise SafetyError(f"Falló la verificación de {src.name}")
        progress(f"Copiando {source.name}: {rel}")
    src_size, src_count = folder_stats(source)
    dst_size, dst_count = folder_stats(destination)
    if (src_size, src_count) != (dst_size, dst_count):
        raise SafetyError("La copia no coincide con el origen; no se cambió Windows.")


def execute_plan(plans: list[FolderPlan], backup_path: Path, progress: Callable[[str], None]) -> None:
    if not plans:
        raise SafetyError("No hay carpetas seleccionadas.")
    required = sum(item.size for item in plans)
    free = shutil.disk_usage(Path(plans[0].destination).anchor).free
    if free < required + 100 * 1024 * 1024:
        raise SafetyError(f"Espacio insuficiente. Faltan {format_bytes(required + 100 * 1024 * 1024 - free)}.")
    backup_registry(backup_path)
    progress(f"Respaldo guardado en {backup_path}")
    # Copy and verify every folder before touching the registry.
    for item in plans:
        _copy_verified(Path(item.source), Path(item.destination), progress)
    changed: list[FolderPlan] = []
    try:
        for item in plans:
            _set_registry(item.registry_name, item.destination)
            changed.append(item)
    except Exception:
        backup = json.loads(backup_path.read_text(encoding="utf-8"))
        for item in changed:
            old = backup["folders"][item.label]
            _set_registry(item.registry_name, old)
        raise
    progress("Migración terminada. Los originales se conservaron.")


def restore_registry(backup_path: Path) -> None:
    data = json.loads(backup_path.read_text(encoding="utf-8"))
    if data.get("version") != 1 or not isinstance(data.get("folders"), dict):
        raise SafetyError("El archivo de respaldo no es válido.")
    for label, value in data["folders"].items():
        if label in FOLDERS:
            _set_registry(FOLDERS[label][0], value)


def restart_explorer() -> None:
    if os.name != "nt":
        return
    subprocess.run(["taskkill", "/f", "/im", "explorer.exe"], check=False, capture_output=True)
    subprocess.Popen(["explorer.exe"])
