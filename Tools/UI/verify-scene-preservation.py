import argparse
import re
from pathlib import Path


def gameplay_objects(scene_path, ignored_scripts):
    document = Path(scene_path).read_text(encoding="utf-8-sig")
    sections = re.split(r"(?=^--- !u!\d+ &)", document, flags=re.MULTILINE)
    objects = {}
    for section in sections:
        match = re.match(r"--- !u!(\d+) &(-?\d+)", section)
        if match:
            objects[match.group(2)] = (match.group(1), section.replace("\r\n", "\n"))
    ui_roots = set()
    for object_id, (kind, section) in objects.items():
        if kind == "1" and re.search(r"^  m_Name: (Canvas|MenuCanvas)$", section, re.MULTILINE):
            ui_roots.add(object_id)
    transforms = {}
    for object_id, (kind, section) in objects.items():
        if kind in {"4", "224"}:
            owner = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", section)
            parent = re.search(r"m_Father: \{fileID: (-?\d+)\}", section)
            if owner and parent:
                transforms[object_id] = (owner.group(1), parent.group(1))
    ui_transforms = {object_id for object_id, (owner, _) in transforms.items() if owner in ui_roots}
    while True:
        children = {object_id for object_id, (_, parent) in transforms.items() if parent in ui_transforms}
        if children.issubset(ui_transforms):
            break
        ui_transforms.update(children)
    ui_objects = ui_roots | {owner for object_id, (owner, _) in transforms.items() if object_id in ui_transforms}
    remaining = {}
    for object_id, (kind, section) in objects.items():
        owner = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", section)
        if object_id in ui_objects or owner and owner.group(1) in ui_objects:
            continue
        if any("guid: " + guid in section for guid in ignored_scripts):
            continue
        if kind in {"4", "224", "1660057539"}:
            section = "".join(line for line in section.splitlines(keepends=True)
                              if not any("fileID: " + transform_id + "}" in line for transform_id in ui_transforms))
        remaining[object_id] = section
    if not ui_roots:
        raise ValueError("No UI Canvas root found in " + str(scene_path))
    return remaining


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("source")
    parser.add_argument("updated")
    arguments = parser.parse_args()
    source = Path(arguments.source)
    updated = Path(arguments.updated)
    scripts = []
    for script in ["UIManager.cs.meta", "MainMenuController.cs.meta"]:
        meta = source / "Assets/_Game/Scripts/UI" / script
        scripts.append(re.search(r"guid: (\w+)", meta.read_text()).group(1))
    for scene in ["Kitchen", "MainMenu"]:
        relative = Path("Assets/_Game/Scenes") / (scene + ".unity")
        before = gameplay_objects(source / relative, scripts)
        after = gameplay_objects(updated / relative, scripts)
        changed = sorted(key for key in before.keys() | after.keys() if before.get(key) != after.get(key))
        if changed:
            raise ValueError(scene + " non-UI objects differ: " + ", ".join(changed))
        print(scene + ": " + str(len(before)) + " non-UI serialized objects unchanged")


if __name__ == "__main__":
    main()
