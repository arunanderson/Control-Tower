#!/usr/bin/env python3
"""Validate task-contract and build-doc frontmatter (no external deps; stdlib only).

Hard-fails on: missing required keys, invalid status. Warns on: blueprint_refs whose
file part does not exist. Runs over docs/build/tasks/*.md.
"""
import os
import re
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
TASKS_DIR = os.path.join(REPO, "docs", "build", "tasks")
REQUIRED = ["id", "title", "type", "status"]
VALID_STATUS = {"draft", "approved", "in-progress", "blocked", "failed", "complete"}


def frontmatter(text):
    m = re.match(r"^---\n(.*?)\n---\n", text, re.DOTALL)
    return m.group(1) if m else None


def top_level_scalar(fm, key):
    m = re.search(r"^%s:\s*(.+)$" % re.escape(key), fm, re.MULTILINE)
    if not m:
        return None
    val = m.group(1).strip()
    return val.split("#", 1)[0].strip() if key == "status" else val


def blueprint_refs(fm):
    # collect indented list items under blueprint_refs: until the next top-level key
    block = re.search(r"^blueprint_refs:\s*\n((?:\s+-\s.*\n?)*)", fm, re.MULTILINE)
    if not block:
        return []
    return re.findall(r"-\s*(\S+)", block.group(1))


def main():
    if not os.path.isdir(TASKS_DIR):
        print("no docs/build/tasks — nothing to validate")
        return 0
    errors, warnings, checked = [], [], 0
    for name in sorted(os.listdir(TASKS_DIR)):
        if not name.endswith(".md"):
            continue
        checked += 1
        path = os.path.join(TASKS_DIR, name)
        with open(path, encoding="utf-8") as f:
            fm = frontmatter(f.read())
        if fm is None:
            errors.append(f"{name}: missing YAML frontmatter")
            continue
        for key in REQUIRED:
            if top_level_scalar(fm, key) in (None, ""):
                errors.append(f"{name}: missing required key '{key}'")
        status = top_level_scalar(fm, "status")
        if status and status not in VALID_STATUS:
            errors.append(f"{name}: invalid status '{status}' (allowed: {sorted(VALID_STATUS)})")
        for ref in blueprint_refs(fm):
            filepart = ref.split("#", 1)[0]
            if filepart and not os.path.exists(os.path.join(REPO, filepart)):
                warnings.append(f"{name}: blueprint_ref not found: {filepart}")
    for w in warnings:
        print("WARN:", w)
    for e in errors:
        print("FAIL:", e)
    print(f"checked {checked} task contract(s); {len(errors)} error(s), {len(warnings)} warning(s)")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
