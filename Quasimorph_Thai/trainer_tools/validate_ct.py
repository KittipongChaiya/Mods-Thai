import os
import re
import sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
CT = os.path.normpath(os.path.join(HERE, os.pardir, 'Quasimorph-v1.0.3-1.CT'))

fails = []


def check(cond, msg):
    print(('  PASS  ' if cond else '  FAIL  ') + msg)
    if not cond:
        fails.append(msg)


print('== XML ==')
try:
    tree = ET.parse(CT)
    root = tree.getroot()
    check(True, 'file parses as XML')
except Exception as e:
    check(False, 'file parses as XML: %s' % e)
    sys.exit(1)

check(root.tag == 'CheatTable', 'root element is <CheatTable>')
check(root.get('CheatEngineTableVersion') == '46', 'table version 46')

scripts, addrs, ids = [], [], []


def walk(e):
    for c in e.findall('./CheatEntries/CheatEntry'):
        ids.append(c.findtext('ID'))
        vt = c.findtext('VariableType')
        if vt == 'Auto Assembler Script':
            scripts.append((c.findtext('Description'), c.findtext('AssemblerScript')))
        a = c.findtext('Address')
        if a:
            offs = [o.text for o in c.findall('./Offsets/Offset')]
            addrs.append((c.findtext('Description'), a, offs, vt))
        walk(c)


walk(root)
print('\n== structure ==')
check(len(ids) == len(set(ids)), 'all %d entry IDs unique' % len(ids))
check(len(scripts) == 13, 'expected 13 AA scripts, found %d' % len(scripts))
check(len(addrs) == 119, 'found %d value entries (expected 119)' % len(addrs))

print('\n== symbols ==')
POINTERS = {'pWeapon', 'pShipSlot', 'pItem', 'pInventory', 'pBreakable',
            'pStarve', 'pPerk', 'pQmorphos', 'pTravel', 'pFactions'}
activate = [s for d, s in scripts if 'Activate me' in (d or '')][0]
reg = set(re.findall(r'registersymbol\((\w+)\)', activate))
unreg = set(re.findall(r'unregistersymbol\((\w+)\)', activate))
alloc = set(re.findall(r'alloc\((\w+),', activate))
dealloc = set(re.findall(r'dealloc\((\w+)\)', activate))
check(reg == POINTERS, 'Activate me registers exactly the %d pointers' % len(POINTERS))
check(reg == unreg, 'every registersymbol has a matching unregistersymbol')
check(alloc == POINTERS and alloc == dealloc, 'every alloc has a matching dealloc')

used = {a for _d, a, _o, _v in addrs}
check(used <= POINTERS, 'all value entries use a registered pointer (%s)'
      % ', '.join(sorted(used - POINTERS)) if used - POINTERS else
      'all value entries use a registered pointer')
check(used == POINTERS, 'every pointer is actually used by some entry; unused=%s'
      % (sorted(POINTERS - used) or 'none'))

print('\n== offsets ==')
bad = [(d, o) for d, _a, offs, _v in addrs for o in offs
       if not re.fullmatch(r'[0-9A-Fa-f]{1,3}', o or '')]
check(not bad, 'all offsets are 1-3 hex digits%s' % ('' if not bad else ': %s' % bad[:5]))

vtypes = {v for _d, _a, _o, v in addrs}
ok_types = {'4 Bytes', '2 Bytes', 'Byte', 'Float', 'Double', '8 Bytes'}
check(vtypes <= ok_types, 'value types are all known CE types: %s' % sorted(vtypes))

print('\n== hook scripts ==')
hooks = [(d, s) for d, s in scripts if 'return qmHook(' in (s or '')]
check(len(hooks) == 11, 'found %d qmHook scripts' % len(hooks))
hids = re.findall(r"return qmHook\('(\w+)'", ''.join(s for _d, s in scripts))
check(len(hids) == len(set(hids)), 'hook ids unique: %s' % sorted(set(hids)))
for d, s in hooks:
    hid = re.search(r"return qmHook\('(\w+)'", s).group(1)
    ok = ("qmUnhook('%s')" % hid) in s
    check(ok, "%-52s ENABLE/DISABLE ids match (%s)" % (d[:52], hid))
    check('[ENABLE]' in s and '[DISABLE]' in s, '%-52s has both sections' % d[:52])
    check(s.count('if syntaxcheck then return end') == 2,
          '%-52s guards both blocks against syntax check' % d[:52])

meths = re.findall(r"return qmHook\('\w+', '([^']+)'", ''.join(s for _d, s in scripts))
print('\n  hooked methods:')
for m in meths:
    print('     ' + m)

print('\n== result ==')
print('FAILURES: %d' % len(fails))
for f in fails:
    print('  - ' + f)
sys.exit(1 if fails else 0)
