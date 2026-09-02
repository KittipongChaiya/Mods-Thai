"""Resolve every reference a mod makes into the game assemblies.

A mod compiles fine against one game build and then throws MissingMethodException
at runtime on another, because the C# compiler only checks the assemblies it was
handed. This walks the built DLL's TypeRef/MemberRef tables and confirms each one
still exists in the game you are shipping against.

The mod this project replaces shipped a call to a five-argument
ItemOnFloorSystem.SpawnItem overload that 1.0.3 had already dropped. That is
exactly what this catches.

Usage:
    python tools/apicheck.py <mod.dll> <path to Quasimorph_Data\\Managed>

Exit code 0 = everything resolves, 1 = something is missing.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cli_meta import Metadata

GAME_ASSEMBLIES = ("Assembly-CSharp", "Assembly-CSharp-firstpass")


def _compressed_uint(blob, pos):
    first = blob[pos]
    if first & 0x80 == 0:
        return first, pos + 1
    if first & 0xC0 == 0x80:
        return ((first & 0x3F) << 8) | blob[pos + 1], pos + 2
    return (((first & 0x1F) << 24) | (blob[pos + 1] << 16)
            | (blob[pos + 2] << 8) | blob[pos + 3]), pos + 4


def _compressed_size(blob, pos):
    first = blob[pos]
    return 1 if first & 0x80 == 0 else (2 if first & 0xC0 == 0x80 else 4)


def param_count(blob):
    """Parameter count for a method signature; the string 'field' for a field."""
    if not blob:
        return None
    pos = 0
    calling_convention = blob[pos]
    pos += 1
    if calling_convention & 0x0F == 0x06:
        return "field"
    if calling_convention & 0x10:                       # generic
        pos += _compressed_size(blob, pos)
    count, _ = _compressed_uint(blob, pos)
    return count


def type_full_names(md):
    """TypeDef row id -> namespace-qualified name, nested types joined with '/'."""
    enclosing = {}
    for rid in range(1, md.count(41) + 1):
        row = md.read(41, rid)
        enclosing[row["NestedClass"]] = row["EnclosingClass"]

    simple = {}
    for rid in range(1, md.count(2) + 1):
        row = md.read(2, rid)
        simple[rid] = (md.string(row["Namespace"]), md.string(row["Name"]))

    names = {}
    for rid in simple:
        trail, cursor, guard = [], rid, 0
        while cursor in enclosing and guard < 12:
            trail.append(simple[cursor][1])
            cursor = enclosing[cursor]
            guard += 1
        namespace, name = simple[cursor]
        base = (namespace + "." + name) if namespace else name
        names[rid] = base + "".join("/" + part for part in reversed(trail))
    return names


def build_index(managed_dir):
    """(assembly, type) -> {(member name, param count)} for the shipped game."""
    index = {}
    for assembly in GAME_ASSEMBLIES:
        path = os.path.join(managed_dir, assembly + ".dll")
        if not os.path.isfile(path):
            continue
        md = Metadata(path)
        names = type_full_names(md)
        for rid in range(1, md.count(2) + 1):
            row, following = md.read(2, rid), md.read(2, rid + 1)
            members = index.setdefault((assembly, names[rid]), set())

            start = row["MethodList"]
            end = following["MethodList"] if following else md.count(6) + 1
            for m in range(start, end):
                method = md.read(6, m)
                if method:
                    members.add((md.string(method["Name"]),
                                 param_count(md.blob(method["Signature"]))))

            start = row["FieldList"]
            end = following["FieldList"] if following else md.count(4) + 1
            for f in range(start, end):
                field = md.read(4, f)
                if field:
                    members.add((md.string(field["Name"]), "field"))
    return index


def check(mod_path, managed_dir):
    index = build_index(managed_dir)
    if not index:
        print("ERROR: no game assemblies found in " + managed_dir)
        return 1

    md = Metadata(mod_path)
    assembly_refs = {rid: md.string(md.read(35, rid)["Name"])
                     for rid in range(1, md.count(35) + 1)}

    type_names, type_assembly = {}, {}
    for rid in range(1, md.count(1) + 1):
        row = md.read(1, rid)
        namespace, name = md.string(row["Namespace"]), md.string(row["Name"])
        tag, scope = row["ResolutionScope"]
        if tag == 1:                                    # nested inside another TypeRef
            type_names[rid] = type_names.get(scope, "?") + "/" + name
            type_assembly[rid] = type_assembly.get(scope)
        else:
            type_names[rid] = (namespace + "." + name) if namespace else name
            type_assembly[rid] = assembly_refs.get(scope) if tag == 35 else None

    missing_types, missing_members, resolved = [], [], 0

    for rid, name in type_names.items():
        assembly = type_assembly.get(rid)
        if assembly in GAME_ASSEMBLIES and (assembly, name) not in index:
            missing_types.append((assembly, name))

    for rid in range(1, md.count(10) + 1):
        row = md.read(10, rid)
        tag, parent = row["Class"]
        if tag != 1:
            continue
        assembly = type_assembly.get(parent)
        if assembly not in GAME_ASSEMBLIES:
            continue
        type_name = type_names.get(parent)
        if (assembly, type_name) not in index:
            continue                                    # already reported as a missing type
        member = md.string(row["Name"])
        count = param_count(md.blob(row["Signature"]))
        if (member, count) in index[(assembly, type_name)]:
            resolved += 1
        else:
            available = sorted(str(c) for n, c in index[(assembly, type_name)] if n == member)
            missing_members.append((type_name, member, count, available))

    print("resolved OK: %d member references" % resolved)
    for assembly, name in missing_types:
        print("  MISSING TYPE   [%s] %s" % (assembly, name))
    for type_name, member, count, available in missing_members:
        print("  MISSING MEMBER %s::%s wants %s params; game has %s"
              % (type_name, member, count, available or "NO SUCH MEMBER"))

    if missing_types or missing_members:
        print("FAIL: %d unresolved reference(s)" % (len(missing_types) + len(missing_members)))
        return 1
    print("OK: every game reference resolves")
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(2)
    sys.exit(check(sys.argv[1], sys.argv[2]))
