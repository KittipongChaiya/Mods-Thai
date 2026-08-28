"""Execute the table's Lua runtime against a stubbed Cheat Engine API."""
import os
import re
import sys
import xml.etree.ElementTree as ET

import lupa

HERE = os.path.dirname(os.path.abspath(__file__))
CT = os.path.normpath(os.path.join(HERE, os.pardir, 'Quasimorph-v1.0.3-1.CT'))

fails = []


def check(cond, msg, extra=''):
    print(('  PASS  ' if cond else '  FAIL  ') + msg)
    if extra and not cond:
        print(extra)
    if not cond:
        fails.append(msg)


# ---- pull the Lua runtime out of the Activate script
root = ET.parse(CT).getroot()
scripts = []


def walk(e):
    for c in e.findall('./CheatEntries/CheatEntry'):
        if c.findtext('VariableType') == 'Auto Assembler Script':
            scripts.append((c.findtext('Description'), c.findtext('AssemblerScript')))
        walk(c)


walk(root)
activate = [s for d, s in scripts if 'Activate' in d][0]
lua_src = activate.split('{$lua}')[1].split('{$asm}')[0]
# drop the CE-only calls; keep the function definitions
lua_src = '\n'.join(l for l in lua_src.splitlines()
                    if not re.match(r'\s*(if syntaxcheck|OpenProcess|mono_initialize|'
                                    r'LaunchMonoDataCollector)', l))

L = lupa.LuaRuntime(unpack_returned_tuples=True)

# ---- stubbed target: a realistic Mono x64 prologue
#      48 83 EC 28        sub rsp,28        (4 bytes)
#      48 89 4C 24 30     mov [rsp+30],rcx  (5 bytes)  -> stolen length 9
BASE = 0x7FF801234000
CODE = [0x48, 0x83, 0xEC, 0x28, 0x48, 0x89, 0x4C, 0x24, 0x30, 0x90, 0x90]
SIZES = {0: 4, 4: 5, 9: 1, 10: 1}

L.globals()['getAddress'] = lambda name: BASE if 'MGSC' in name else 0
L.globals()['getInstructionSize'] = lambda a: SIZES.get(a - BASE, 1)


def make_read_bytes(rt):
    def read_bytes(addr, count, as_table):
        off = addr - BASE
        vals = CODE[off:off + count]
        if as_table:
            return rt.table_from(vals)
        return vals[0] if vals else None
    return read_bytes


L.globals()['readBytes'] = make_read_bytes(L)
L.globals()['disassemble'] = lambda a: '%X - 48 83 EC 28 - sub rsp,28' % a

L.execute(lua_src)
qmHook = L.globals()['qmHook']
qmUnhook = L.globals()['qmUnhook']

print('== Lua runtime ==')
check(True, 'runtime parses and loads under Lua %s' % lupa.LuaRuntime().lua_implementation)

print('\n== qmHook (RCX capture) ==')
out = qmHook('hkTest', 'MGSC.Perk:AddExp', 'mov [pPerk],rcx')
print(out)
check('alloc(hkTest_code,2048,7FF801234000)' in out, 'cave allocated near the method')
check('mov [pPerk],rcx' in out, 'capture line emitted')
check('db 48 83 EC 28 48 89 4C 24 30' in out, 'all 9 stolen bytes copied verbatim')
check('7FF801234000:\njmp hkTest_code' in out, 'jmp written at the method entry')
check(out.count('db 90') == 1 and 'db 90 90 90 90' in out,
      'entry padded with exactly 9-5=4 nops')
check(out.rstrip().endswith('hkTest_ret:'), 'return label is last')

order = [out.index('mov [pPerk],rcx'), out.index('db 48 83'), out.index('jmp hkTest_ret')]
check(order == sorted(order), 'cave order is capture -> stolen bytes -> jmp back')

print('\n== qmUnhook ==')
out2 = qmUnhook('hkTest')
print(out2)
check('7FF801234000:' in out2, 'restores at the right address')
check('db 48 83 EC 28 48 89 4C 24 30' in out2, 'restores the original 9 bytes')
check('dealloc(hkTest_code)' in out2, 'frees the cave')
check(qmUnhook('hkTest') == '', 'second unhook is a no-op (state cleared)')

print('\n== disassemble fallback (getInstructionSize unavailable) ==')
L2 = lupa.LuaRuntime(unpack_returned_tuples=True)
L2.globals()['getAddress'] = lambda name: BASE
L2.globals()['readBytes'] = make_read_bytes(L2)
L2.globals()['disassemble'] = lambda a: '%X - 48 83 EC 28 - sub rsp,28' % a
L2.execute(lua_src)
out3 = L2.globals()['qmHook']('hkFb', 'MGSC.X:Y', 'mov [pX],rcx')
check('db 48 83 EC 28 48' in out3,
      'fallback measures 4-byte insns from disassemble() and reaches 8 bytes')
check('db 90 90 90' in out3, 'fallback pads correctly')

print('\n== error paths ==')
try:
    qmHook('hkBad', 'NoSuchThing', 'mov [pX],rcx')
    check(False, 'unresolvable method raises')
except Exception as e:
    check('cannot resolve' in str(e), 'unresolvable method raises a clear error')

L3 = lupa.LuaRuntime(unpack_returned_tuples=True)
L3.globals()['getAddress'] = lambda name: BASE
L3.globals()['getInstructionSize'] = lambda a: 5
L3.globals()['readBytes'] = lambda addr, count, t: (
    L3.table_from([0xE9, 0x00, 0x00, 0x00, 0x00][:count]) if t else 0xE9)
L3.globals()['disassemble'] = lambda a: ''
L3.execute(lua_src)
try:
    L3.globals()['qmHook']('hkJmp', 'MGSC.X:Y', 'mov [pX],rcx')
    check(False, 'relative-branch prologue is rejected')
except Exception as e:
    check('relative branch' in str(e), 'relative-branch prologue is rejected')

print('\n== result ==')
print('FAILURES: %d' % len(fails))
for f in fails:
    print('  - ' + f)
sys.exit(1 if fails else 0)
