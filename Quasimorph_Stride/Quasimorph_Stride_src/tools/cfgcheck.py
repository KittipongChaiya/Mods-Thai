"""Confirm the config file's four descriptions of itself still agree.

A key like `run_open_doors` is written down four times in this project: in the
self-documenting template ModConfig writes on first run, in the Load() call that
reads it back, in the field default it falls back to, and in the README table the
player actually reads. Nothing in the compiler connects those four. A key renamed
in the template but not in Load() produces a config file whose settings are
silently ignored - the worst kind of bug, because the game looks like it is
working.

This is the same idea as tools/apicheck.py, one level up: that one stops the mod
shipping a call to a method the game no longer has, this one stops it shipping a
setting the mod no longer reads.

Usage:
    python tools/cfgcheck.py [path to the mod source root]

Exit code 0 = the four agree, 1 = they have drifted.
"""
import os
import re
import sys

# The template lines are C# string literals ending in an escaped newline:
#     "run_open_doors=true\n" +
# so the pattern needs a literal backslash, built here rather than escaped
# inline to keep it readable.
BACKSLASH = chr(92)
TEMPLATE_KEY = '"([a-z_]+)=(true|false)' + BACKSLASH + BACKSLASH + 'n"'
LOAD_KEY = r'=\s*Bool\(values,\s*"([a-z_]+)",\s*(\w+)\)'
FIELD_DEFAULT = r'internal static bool (\w+)\s*=\s*(true|false);'
README_ROW = r'\| `([a-z_]+)` \| `(true|false)` \|'


def read(*parts):
    with open(os.path.join(*parts), encoding="utf-8") as handle:
        return handle.read()


def main():
    base = (sys.argv[1] if len(sys.argv) > 1
            else os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

    config = read(base, "mod_src", "QuasimorphStride", "ModConfig.cs")
    readme = read(base, "README.md")

    template = dict(re.findall(TEMPLATE_KEY, config))
    load = dict(re.findall(LOAD_KEY, config))
    fields = dict(re.findall(FIELD_DEFAULT, config))
    documented = dict(re.findall(README_ROW, readme))

    problems = []

    def complain(label, keys):
        if keys:
            problems.append(label + ": " + ", ".join(sorted(keys)))

    complain("written to config.txt but never read back", set(template) - set(load))
    complain("read by Load() but never written to config.txt", set(load) - set(template))
    complain("documented in the README but not in the config", set(documented) - set(template))
    complain("in the config but undocumented", set(template) - set(documented))

    for key, value in sorted(template.items()):
        field = fields.get(load.get(key))
        if key in load and field != value:
            problems.append("config.txt says %s=%s but the field default is %s"
                            % (key, value, field))

    for key, value in sorted(documented.items()):
        if template.get(key) != value:
            problems.append("README says %s=%s but config.txt writes %s"
                            % (key, value, template.get(key)))

    if not template:
        problems.append("no template keys matched - the template's shape has changed "
                        "and this check is no longer reading it")

    if problems:
        print("config drift:")
        for problem in problems:
            print("  - " + problem)
        return 1

    print("config OK: %d keys agree across the template, Load(), the field defaults "
          "and the README" % len(template))
    return 0


if __name__ == "__main__":
    sys.exit(main())
