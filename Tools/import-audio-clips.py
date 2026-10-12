#!/usr/bin/env python3
"""Import only the picked audio clips from Asset Store .unitypackage files.

Contract: Docs/specs/audio-packs-contract.md (Part A).
Usage from the repo root:  python -I Tools/import-audio-clips.py [--dry-run]

Reads CoolSchool/Assets/CoolSchool/Audio/SoundPackPicks.txt, groups the unique clip
specs by pack, and for each package copies `asset` and `asset.meta` (keeping the
package GUID) to CoolSchool/Assets/Imported/Audio/<PackFolder>/<path in pack>.
Only writes below CoolSchool/Assets/Imported/Audio/. Never guesses another clip.
Standard library only. Package file names are treated as data.
"""
import argparse
import difflib
import os
import re
import sys
import tarfile

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PICKS = os.path.join(REPO_ROOT, "CoolSchool", "Assets", "CoolSchool", "Audio", "SoundPackPicks.txt")
ASSETS = os.path.join(REPO_ROOT, "CoolSchool", "Assets")
DEST_ROOT = os.path.join(ASSETS, "Imported", "Audio")
STORE = "C:/Users/Timo/AppData/Roaming/Unity/Asset Store-5.x/"

# key -> (package file, path prefix inside package, project folder under Assets/Imported/Audio/)
PACKS = {
    "PM": ("Epic Sounds and FX/AudioSound FX/Phantom Magic Sounds.unitypackage", "Assets/Phantom Magic/", "PhantomMagic/"),
    "AR": ("Audio Alchemy Room/AudioSound FX/Action RPG SFX Pack v2.unitypackage", "Assets/Action RPG SFX V2/", "ActionRPG/"),
    "ENV": ("PlaceHolder Inc/AudioSound FX/Environment Sounds Pack.unitypackage", "Assets/Environment Sounds/", "EnvironmentSounds/"),
    "BES": ("Khron Studio/AudioSound FXCreatures/Bestiary Monster Bundle Vol 1.unitypackage", "Assets/BestiaryMonsterBundle/", "Bestiary/"),
    "MON": ("Cafofo/AudioSound FXCreatures/Monster Sounds Pack.unitypackage", "Assets/Monster Sounds Pack/", "MonsterSounds/"),
    "FEEL": ("More Mountains/ScriptingEffects/Feel.unitypackage", "Assets/Feel/", "Feel/"),
    "GUI": ("gamevanilla/Textures MaterialsGUI Skins/Ultimate Clean GUI Pack.unitypackage", "Assets/UltimateCleanGUIPack/Common/Sounds/", "UltimateCleanGUI/"),
}
NOT_IMPORTED = {"POLY"}  # already in the project

GUID_RE = re.compile(r"^[0-9a-f]{32}$")
META_GUID_RE = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$")


def parse_picks():
    """Return ordered list of unique (key, rel_path) clip specs."""
    seen = set()
    specs = []
    with open(PICKS, "r", encoding="utf-8-sig") as f:
        for lineno, raw in enumerate(f, 1):
            line = raw.rstrip("\r\n")
            if not line.strip() or line.lstrip().startswith("#"):
                continue
            cols = line.split("\t")
            if len(cols) < 5:
                print("WARN picks line %d: expected 5 tab-separated columns, got %d; skipped" % (lineno, len(cols)))
                continue
            for clip in cols[4].split(" | "):
                clip = clip.strip()
                if not clip:
                    continue
                key, _, rel = clip.partition("/")
                if key in NOT_IMPORTED:
                    continue
                if key not in PACKS or not rel:
                    print("WARN picks line %d: unknown pack key in %r; skipped" % (lineno, clip))
                    continue
                if (key, rel) not in seen:
                    seen.add((key, rel))
                    specs.append((key, rel))
    return specs


def scan_guids():
    """guid -> project-relative path (forward slashes, without .meta) for every .meta under Assets."""
    guids = {}
    for dirpath, _dirs, files in os.walk(ASSETS):
        for name in files:
            if not name.endswith(".meta"):
                continue
            p = os.path.join(dirpath, name)
            try:
                with open(p, "r", encoding="utf-8", errors="replace") as f:
                    for _ in range(8):
                        line = f.readline()
                        if not line:
                            break
                        m = META_GUID_RE.match(line.strip())
                        if m:
                            guids[m.group(1).lower()] = os.path.relpath(p[:-5], os.path.dirname(ASSETS)).replace(os.sep, "/")
                            break
            except OSError:
                pass
    return guids


def index_package(path):
    """Pass 1. Returns (pathname -> guid, guid -> asset size, guid -> meta size)."""
    paths, asize, msize = {}, {}, {}
    with tarfile.open(path, "r|gz") as tf:
        for m in tf:
            if not m.isfile():
                continue
            parts = m.name.replace("\\", "/").split("/")
            if len(parts) != 2:
                continue
            guid, leaf = parts[0].lower(), parts[1]
            if not GUID_RE.match(guid):
                continue
            if leaf == "pathname":
                data = tf.extractfile(m).read().decode("utf-8", errors="replace")
                first = data.split("\n", 1)[0].rstrip()
                paths[first] = guid
            elif leaf == "asset":
                asize[guid] = m.size
            elif leaf == "asset.meta":
                msize[guid] = m.size
    return paths, asize, msize


def resolve(full, paths, norm):
    if full in paths:
        return paths[full], "exact"
    n = full.rstrip().casefold()
    if n in norm:
        return norm[n], "case-insensitive"
    return None, None


def safe_dest(rel_folder, rel):
    """Destination path guaranteed to stay under DEST_ROOT, or None."""
    parts = rel.replace("\\", "/").split("/")
    if any(p in ("", ".", "..") or ":" in p for p in parts):
        return None
    dest = os.path.normpath(os.path.join(DEST_ROOT, *rel_folder.strip("/").split("/"), *parts))
    root = os.path.normpath(DEST_ROOT) + os.sep
    return dest if dest.startswith(root) else None


def write_file(dest, data):
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    tmp = dest + ".importing"
    with open(tmp, "wb") as f:
        f.write(data)
    os.replace(tmp, dest)


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--dry-run", action="store_true", help="report only, write nothing")
    args = ap.parse_args()

    specs = parse_picks()
    print("Picks: %d unique clips from %s" % (len(specs), os.path.relpath(PICKS, REPO_ROOT)))
    guid_map = scan_guids()
    print("Scanned %d existing .meta GUIDs under CoolSchool/Assets" % len(guid_map))

    by_pack = {}
    for key, rel in specs:
        by_pack.setdefault(key, []).append(rel)

    imported = skipped = 0
    bytes_written = 0
    missing = []      # (key, rel, [alternatives])
    collisions = []   # (key, rel, guid, existing path)
    ci_matches = []
    errors = []

    for key in PACKS:
        if key not in by_pack:
            continue
        pkg_file, prefix, folder = PACKS[key]
        pkg_path = STORE + pkg_file
        print("\n== %s: %s (%d picks)" % (key, pkg_file, len(by_pack[key])))
        if not os.path.isfile(pkg_path):
            for rel in by_pack[key]:
                missing.append((key, rel, []))
            errors.append("package not found: " + pkg_path)
            print("  PACKAGE NOT FOUND")
            continue

        paths, asize, msize = index_package(pkg_path)
        norm = {}
        for p, g in paths.items():
            norm.setdefault(p.rstrip().casefold(), g)
        print("  indexed %d pathnames" % len(paths))

        to_extract = {}  # guid -> (rel, dest)
        for rel in by_pack[key]:
            full = prefix + rel
            guid, how = resolve(full, paths, norm)
            if guid is None:
                folder_in_pkg = full.rsplit("/", 1)[0] + "/" if "/" in full else ""
                siblings = [p for p in paths if p.startswith(folder_in_pkg) and "/" not in p[len(folder_in_pkg):]]
                base = full[len(folder_in_pkg):]
                names = {p[len(folder_in_pkg):]: p for p in siblings}
                close = difflib.get_close_matches(base, list(names), n=3, cutoff=0.0)
                missing.append((key, rel, [names[c][len(prefix):] for c in close]))
                print("  MISSING: %s" % rel)
                continue
            if how != "exact":
                ci_matches.append((key, rel, how))
                print("  note: %s matched %s" % (rel, how))
            if guid not in asize or guid not in msize:
                errors.append("%s/%s: package entry lacks asset or asset.meta" % (key, rel))
                print("  ERROR: no asset/asset.meta for %s" % rel)
                continue
            dest = safe_dest(folder, rel)
            if dest is None:
                errors.append("%s/%s: unsafe destination path" % (key, rel))
                print("  ERROR: unsafe path %s" % rel)
                continue
            proj_rel = os.path.relpath(dest, os.path.dirname(ASSETS)).replace(os.sep, "/")
            other = guid_map.get(guid)
            if other is not None and other != proj_rel:
                collisions.append((key, rel, guid, other))
                print("  GUID COLLISION: %s (%s) already used by %s; skipped" % (rel, guid, other))
                continue
            meta_dest = dest + ".meta"
            if (os.path.isfile(dest) and os.path.getsize(dest) == asize[guid]
                    and os.path.isfile(meta_dest) and os.path.getsize(meta_dest) == msize[guid]):
                skipped += 1
                continue
            to_extract[guid] = (rel, dest)

        if not to_extract:
            continue
        if args.dry_run:
            for guid, (rel, dest) in to_extract.items():
                imported += 1
                bytes_written += asize[guid] + msize[guid]
                print("  would import %s -> %s" % (rel, os.path.relpath(dest, REPO_ROOT)))
            continue

        # Pass 2: extract only the needed members.
        pending = {}
        with tarfile.open(pkg_path, "r|gz") as tf:
            for m in tf:
                if not m.isfile():
                    continue
                parts = m.name.replace("\\", "/").split("/")
                if len(parts) != 2:
                    continue
                guid, leaf = parts[0].lower(), parts[1]
                if guid not in to_extract or leaf not in ("asset", "asset.meta"):
                    continue
                pending.setdefault(guid, {})[leaf] = tf.extractfile(m).read()
                got = pending[guid]
                if "asset" in got and "asset.meta" in got:
                    rel, dest = to_extract[guid]
                    write_file(dest, got["asset"])
                    write_file(dest + ".meta", got["asset.meta"])
                    imported += 1
                    bytes_written += len(got["asset"]) + len(got["asset.meta"])
                    print("  imported %s" % rel)
                    del pending[guid]
        for guid in pending:
            errors.append("%s/%s: incomplete extraction" % (key, to_extract[guid][0]))

    print("\n===== REPORT%s =====" % (" (dry run)" if args.dry_run else ""))
    print("clips %s: %d, skipped (already present): %d, written: %.2f MB"
          % ("that would be imported" if args.dry_run else "imported", imported, skipped, bytes_written / 1048576.0))
    if ci_matches:
        print("case-insensitive matches:")
        for key, rel, how in ci_matches:
            print("  %s/%s (%s)" % (key, rel, how))
    if collisions:
        print("GUID collisions (skipped):")
        for key, rel, guid, other in collisions:
            print("  %s/%s guid %s used by %s" % (key, rel, guid, other))
    if missing:
        print("MISSING picks (%d):" % len(missing))
        for key, rel, alts in missing:
            print("  %s/%s" % (key, rel))
            for a in alts:
                print("      nearest: %s/%s" % (key, a))
    if errors:
        print("ERRORS:")
        for e in errors:
            print("  " + e)
    return 1 if (missing or errors) else 0


if __name__ == "__main__":
    sys.exit(main())
