# Copyright (c) 2026, i2kiselev
# BSD 3-Clause License: see LICENSE in the repository root.
"""Writes config/pfp4-1.3.json, what a Project Forza Plus 4 install takes from the mod.

    make-pfp4-manifest.py "D:/Project Forza Plus 4" .local/state/user/0000000000000000/4D530910/00000002 \
        -o config/pfp4-1.3.json

The first argument is the mod's extracted root folder (the one holding `Main Game Changes`), the
second our own installed content (Disc 2 and every DLC pack), read only for the packages'
headers: the mod's package folders have none, and the game cannot open content without them.
Every file the launcher takes from the mod is listed with its size and SHA-256; the launcher
refuses a mod whose files differ. Paths are `\\`-separated, as the launcher compares them.
"""
import argparse
import hashlib
import json
import pathlib
import struct

GAME_FILES = ["default.xex"] + [f"Media\\{p}" for p in (
    "physics\\PhysicsSettings.ini", "physics\\PI.xml", "GameTunableSettings\\GameTunableSettings.ini",
    "Effects\\TireSmoke.xml", "Effects\\Backfire.xml", "audio\\AudioEngineConfig.xml",
    "audio\\BackfireTimerConfig.xml")]
GAME_DB = "Media\\db\\gamedb.slt"
TUNABLES = "Media\\GameTunableSettings\\GameTunableSettings.ini"
MODES = {"normal": "Normal Mode", "hard": "Hard Mode", "sandbox": "Sandbox Mode", "vanilla": "Vanilla Mode"}
CONTENT = "Disc 2 and DLCs\\00000002"
HEADER_SIZE = 0x148
TITLE_ID = 0x4D530910
# Pack 15 (offer 21) ships its track archive extracted; its PhysicsTrack TrackSettings.xml carries
# the mod's Xenia workarounds (shadows, specular, detail blur), which our renderer does not need.
# The installer puts vanilla's three values back and checks the result against vanilla's file.
TRACK_SETTINGS = "Media\\DLCZips\\0015_pri_65\\Media\\Tracks\\PhysicsTrack\\TrackSettings.xml"
TRACK_SETTINGS_OFFER = 21
TRACK_SETTINGS_VALUES = [
    ('<ShadowColor r="1.000000" g="1.000000" b="1.000000" a="1.000000"/>',
     '<ShadowColor r="0.207200" g="0.259100" b="0.357600" a="1.000000"/>'),
    ('<GlobalSpecularLevel Version="2" value="0.000000"/>', '<GlobalSpecularLevel Version="2" value="3.150000"/>'),
    ('<DetailBlurMPHForMaxBlur value="9999999960.000000"/>', '<DetailBlurMPHForMaxBlur value="60.000000"/>'),
]
TRACK_SETTINGS_SIZE = 8763
TRACK_SETTINGS_SHA256 = "3C34AE9D350A36CE27D1FD9C8FC590F930B16872D10811EA90294BA43A5EA03F"


def digest(path: pathlib.Path) -> str:
    with path.open("rb") as handle:
        return hashlib.file_digest(handle, "sha256").hexdigest().upper()


def windows(path: pathlib.PurePath) -> str:
    return str(path).replace("/", "\\")


def entry(root: pathlib.Path, source: pathlib.Path, target: str) -> dict:
    if not source.is_file():
        raise SystemExit(f"{source} is missing")
    return {"source": windows(source.relative_to(root)), "target": target,
            "size": source.stat().st_size, "sha256": digest(source)}


def files_under(folder: pathlib.Path) -> list[pathlib.Path]:
    return sorted((p for p in folder.rglob("*") if p.is_file()), key=lambda p: windows(p.relative_to(folder)))


def offer(package: pathlib.Path) -> int:
    offers = [int(p.stem) for p in (package / "Media").glob("*.puboffer")]
    if len(offers) != 1:
        raise SystemExit(f"{package} has {len(offers)} offer files")
    return offers[0]


def header_fields(header: pathlib.Path) -> tuple[str, int | None]:
    raw = header.read_bytes()
    device, content_type = struct.unpack(">II", raw[:8])
    title = struct.unpack(">I", raw[0x140:0x144])[0]
    if (device, content_type, title) != (1, 2, TITLE_ID) or len(raw) not in (HEADER_SIZE, HEADER_SIZE + 4):
        raise SystemExit(f"{header} is not a content header this tool knows")
    name = raw[8:0x108].decode("utf-16-be").split("\0", 1)[0]
    mask = struct.unpack("<I", raw[HEADER_SIZE:])[0] if len(raw) > HEADER_SIZE else None
    return name, mask


def mode(root: pathlib.Path, folder: pathlib.Path, package_files: set[str]) -> dict:
    dlc = folder / "DLC DBs"
    dlc_dbs = [entry(root, p, windows(p.relative_to(dlc))) for p in files_under(dlc)]
    for db in dlc_dbs:
        if db["target"].casefold() not in package_files:
            raise SystemExit(f"{db['source']} replaces no file of the mod's packages")
    saves = folder / "Skip Intro Saves"
    save_folders = sorted(saves.iterdir()) if saves.is_dir() else [folder / "Skip Intro Save"]
    return {
        "game_db": entry(root, folder / "gamedb.slt", GAME_DB),
        "harder_ai": {"game_db": entry(root, folder / "Harder AI/gamedb.slt", GAME_DB),
                      "tunables": entry(root, folder / "Harder AI/GameTunableSettings.ini", TUNABLES)},
        "dlc_dbs": dlc_dbs,
        "skip_intro": [{"name": s.name.removeprefix("Skip Intro "),
                        "files": [entry(root, p, windows(p.relative_to(s))) for p in files_under(s)]}
                       for s in save_folders],
    }


def track_settings_patch(root: pathlib.Path, folder: str) -> dict:
    target = f"{folder}\\{TRACK_SETTINGS}"
    text = (root / CONTENT / target).read_bytes()
    for mod, vanilla in TRACK_SETTINGS_VALUES:
        if text.count(mod.encode()) != 1:
            raise SystemExit(f"{target} does not hold {mod} once")
        text = text.replace(mod.encode(), vanilla.encode())
    if (len(text), hashlib.sha256(text).hexdigest().upper()) != (TRACK_SETTINGS_SIZE, TRACK_SETTINGS_SHA256):
        raise SystemExit(f"{target} with vanilla's values is not vanilla's file")
    return {"target": target, "replace": [{"from": m, "to": v} for m, v in TRACK_SETTINGS_VALUES],
            "size": TRACK_SETTINGS_SIZE, "sha256": TRACK_SETTINGS_SHA256}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("mod_root", type=pathlib.Path)
    parser.add_argument("our_content", type=pathlib.Path)
    parser.add_argument("-o", "--output", type=pathlib.Path, required=True)
    args = parser.parse_args()
    root = args.mod_root
    ours = {offer(p): p.name for p in sorted(args.our_content.iterdir()) if p.is_dir()}
    headers = args.our_content.parent / "Headers" / args.our_content.name
    packages = []
    for folder in sorted((root / CONTENT).iterdir(), key=lambda p: p.name):
        number = offer(folder)
        if number not in ours:
            raise SystemExit(f"our content has no package of offer {number} ({folder.name})")
        name, mask = header_fields(headers / f"{ours[number]}.header")
        packages.append({"folder": folder.name, "offer": number, "display_name": name, "license_mask": mask,
                         "files": [entry(root, p, windows(p.relative_to(folder))) for p in files_under(folder)]})
    if len(packages) != len(ours):
        raise SystemExit(f"the mod has {len(packages)} packages, our content {len(ours)}")
    package_files = {f"{p['folder']}\\{f['target']}".casefold() for p in packages for f in p["files"]}
    pack15 = next(p["folder"] for p in packages if p["offer"] == TRACK_SETTINGS_OFFER)
    manifest = {
        "schema_version": 1,
        "mod_version": "1.3",
        "game_files": [entry(root, root / "Main Game Changes" / f, f) for f in GAME_FILES],
        "modes": {key: mode(root, root / "Game Mode DBs" / folder, package_files) for key, folder in MODES.items()},
        "packages": packages,
        "patches": [track_settings_patch(root, pack15)],
    }
    args.output.write_text(json.dumps(manifest, indent=1) + "\n", encoding="utf-8", newline="\n")
    count = sum(len(p["files"]) for p in packages)
    print(f"{len(manifest['game_files'])} game files, {len(MODES)} modes, {len(packages)} packages ({count} files)")


if __name__ == "__main__":
    main()
