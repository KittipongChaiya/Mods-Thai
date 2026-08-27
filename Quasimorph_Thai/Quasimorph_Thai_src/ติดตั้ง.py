#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ตัวติดตั้งม็อดภาษาไทย Quasimorph

ม็อดนี้ไม่แก้ไขไฟล์เกมแม้แต่ไฟล์เดียว มันติดตั้งเป็นม็อดตามระบบม็อดของเกมเอง
โดยคัดลอกโฟลเดอร์เดียวไปไว้ในโฟลเดอร์ม็อดของเกม ถอนการติดตั้งได้ด้วยการลบโฟลเดอร์นั้น
"""
from __future__ import annotations

import os
import shutil
import sys
import traceback
from pathlib import Path

MIN_PYTHON = (3, 8)

MOD_NAME = "QuasimorphThai"
MOD_VERSION = "1.3"
GAME_VERSION = "1.0.3.578s.024ad60"
COMPANY = "Magnum Scriptum LTD"
PRODUCT = "Quasimorph"

# ไฟล์ที่ต้องมีครบในโฟลเดอร์ mod/ ข้างๆ สคริปต์นี้
REQUIRED = ["modmanifest.json", "QuasimorphThai.dll", "quasimorph_tahoma_tmp.bundle"]
# ต้องมีไฟล์คำแปลอย่างน้อยหนึ่งแบบ (บีบอัดหรือไม่บีบอัดก็ได้)
TABLE_ANY = ["thai_overrides.tsv.gz", "thai_overrides.tsv"]


def out(message: str) -> None:
    print(message, flush=True)


def bootstrap_console() -> None:
    for stream in (sys.stdout, sys.stderr):
        reconfigure = getattr(stream, "reconfigure", None)
        if reconfigure is not None:
            reconfigure(encoding="utf-8", errors="replace")


def mods_root() -> Path:
    """โฟลเดอร์ม็อดของเกม = Application.persistentDataPath\\LocalUserPresets"""
    if os.name == "nt":
        base = Path(os.environ["USERPROFILE"]) / "AppData" / "LocalLow"
    elif sys.platform == "darwin":
        base = Path.home() / "Library" / "Application Support"
    else:
        base = Path(os.environ.get("XDG_CONFIG_HOME", Path.home() / ".config"))
    return base / COMPANY / PRODUCT / "LocalUserPresets"


def check_python() -> None:
    if sys.version_info < MIN_PYTHON:
        raise SystemExit("ต้องใช้ Python 3.8 ขึ้นไป")


def check_source(source: Path) -> None:
    if not source.is_dir():
        raise SystemExit(f"ไม่พบโฟลเดอร์ mod ข้างๆ ไฟล์นี้: {source}")
    missing = [name for name in REQUIRED if not (source / name).is_file()]
    if missing:
        raise SystemExit("ไฟล์ม็อดไม่ครบ: " + ", ".join(missing))
    if not any((source / name).is_file() for name in TABLE_ANY):
        raise SystemExit("ไม่พบไฟล์คำแปล (thai_overrides.tsv หรือ .tsv.gz)")


def copy_tree(source: Path, destination: Path) -> int:
    destination.mkdir(parents=True, exist_ok=True)
    copied = 0
    for item in sorted(source.rglob("*")):
        if not item.is_file():
            continue
        # ไฟล์วินิจฉัยและ log จากการรันครั้งก่อน ไม่ต้องคัดลอกไปด้วย
        if item.name in ("QuasimorphThai.log", "screenshot.png", "diagnostics.on"):
            continue
        target = destination / item.relative_to(source)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(item, target)
        copied += 1
    return copied


def main() -> int:
    here = Path(__file__).resolve().parent
    source = here / "mod"
    destination = mods_root() / MOD_NAME

    out("=" * 64)
    out(f"ม็อดภาษาไทย Quasimorph v{MOD_VERSION}")
    out(f"ทดสอบกับเกมเวอร์ชัน: {GAME_VERSION}")
    out("ม็อดนี้ไม่แก้ไขไฟล์เกม จึงไม่ต้องสำรองไฟล์เกมไว้ก่อน")
    out("=" * 64)

    out("[1/4] กำลังตรวจ Python")
    check_python()

    out("[2/4] กำลังตรวจไฟล์ม็อด")
    check_source(source)

    out(f"[3/4] กำลังคัดลอกไปที่: {destination}")
    if destination.exists():
        out("      พบเวอร์ชันเดิมอยู่แล้ว จะเขียนทับ")
    copied = copy_tree(source, destination)
    out(f"      คัดลอกแล้ว {copied} ไฟล์")

    out("[4/4] กำลังตรวจผลหลังติดตั้ง")
    for name in REQUIRED:
        if not (destination / name).is_file():
            raise SystemExit(f"ติดตั้งไม่ครบ ขาดไฟล์: {name}")

    out("")
    out("ติดตั้งเสร็จแล้ว เปิดเกมได้ตามปกติ")
    out("ในเกมให้เลือกภาษา \"ไทย\" ใน Options (จะอยู่ตรงที่เคยเป็น English)")
    out("")
    out("ถ้าต้องการถอนการติดตั้ง ให้ลบโฟลเดอร์นี้ทิ้ง:")
    out(f"  {destination}")
    return 0


def entrypoint() -> int:
    bootstrap_console()
    try:
        return main()
    except SystemExit as exc:
        if exc.code not in (0, None):
            out("")
            out(f"[ผิดพลาด] {exc}")
        code = exc.code if isinstance(exc.code, int) else 1
    except BaseException:
        out("")
        out("[ผิดพลาด] ตัวติดตั้งมีปัญหาและหยุดทำงาน")
        out(traceback.format_exc())
        code = 1
    else:
        code = 0
    try:
        input("\nกด Enter เพื่อปิดหน้าต่าง...")
    except EOFError:
        pass
    return code


if __name__ == "__main__":
    raise SystemExit(entrypoint())
