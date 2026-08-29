#!/usr/bin/env python3
"""Drive Cheat Engine to test the shipped table against the running game.

Static analysis is not enough for this table -- it shipped once with ten hooks
that could not assemble and 38 wrong field offsets, and both were only caught
here. Run this after any change.

    python ce_selftest.py hooks   --run   # install/restore every hook
    python ce_selftest.py dump    --run   # dump real Mono field offsets
    python ce_selftest.py monitor --run   # leave hooks in, watch live values

Each mode writes a temporary script into Cheat Engine's autorun folder, which
executes on CE start and closes CE when finished. `--run` launches CE, waits,
prints the report and removes the temporary script again.
"""
import argparse
import os
import re
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
CT = os.path.normpath(os.path.join(HERE, os.pardir, 'Quasimorph-v1.0.3-1.CT'))
CE_DIR = r'C:\Program Files\Cheat Engine'
CE_EXE = os.path.join(CE_DIR, 'cheatengine-x86_64.exe')
AUTORUN = os.path.join(CE_DIR, 'autorun', '_qm_selftest.lua')
PROCESS = 'Quasimorph.exe'

# classes whose layout the table depends on
DUMP_CLASSES = [
    'WeaponComponent', 'WeaponRecord', 'ItemSlot', 'PickupItem',
    'BasePickupItem', 'StackableItemComponent', 'UsableItemComponent',
    'Inventory', 'BreakableItemComponent', 'StarvationEffect', 'BaseEffect',
    'Creature', 'CreatureData', 'HealthInfo', 'Perk', 'QmorphosController',
    'RaidMetadata', 'MissionWinCondition', 'TravelMetadata', 'Factions',
    'Faction', 'ItemStorage', 'BasePickupItemRecord', 'ItemRecord',
]


# --------------------------------------------------------------- table pieces
def read_table():
    """Pull the runtime, the pointer block, the hooks and the value entries
    straight out of the .CT so the test exercises the real artifact."""
    root = ET.parse(CT).getroot()
    scripts, values = [], []

    def walk(e, grp=''):
        for c in e.findall('./CheatEntries/CheatEntry'):
            d = (c.findtext('Description') or '').strip('"')
            vt = c.findtext('VariableType')
            g = d if vt == 'Auto Assembler Script' else grp
            if vt == 'Auto Assembler Script':
                scripts.append((d, c.findtext('AssemblerScript')))
            if c.findtext('Address'):
                offs = [o.text for o in c.findall('./Offsets/Offset')]
                values.append((g, d, vt, c.findtext('Address'), list(reversed(offs))))
            walk(c, g)

    walk(root)
    activate = [s for d, s in scripts if 'Activate' in d][0]
    runtime = activate.split('{$lua}')[1].split('{$asm}')[0]
    runtime = '\n'.join(
        l for l in runtime.splitlines()
        if not re.match(r'\s*(if syntaxcheck|OpenProcess|mono_initialize|'
                        r'LaunchMonoDataCollector)', l))
    ptr_block = activate.split('{$asm}')[1].split('[DISABLE]')[0].strip()
    hooks = []
    for _desc, s in scripts:
        m = re.search(r"return (qmHook\('(\w+)', '([^']+)', '.*?'\))", s, re.S)
        if m:
            hooks.append((m.group(2), m.group(3), m.group(1)))
    return runtime, ptr_block, hooks, values


PRELUDE = '''-- TEMPORARY Quasimorph trainer test. Safe to delete.
local REPORT = [[%(report)s]]
local out, pass, fail = {}, 0, 0
local function log(fmt, ...) out[#out+1] = string.format(fmt, ...) end
local function ck(cond, fmt, ...)
  if cond then pass = pass + 1 else fail = fail + 1 end
  log('%%s %%s', cond and 'PASS' or 'FAIL', string.format(fmt, ...))
  return cond
end
local function flush()
  local f = io.open(REPORT, 'w')
  if f then
    f:write(table.concat(out, '\\n'))
    f:write(string.format('\\n\\nPASS=%%d FAIL=%%d\\n', pass, fail))
    f:close()
  end
end
local function attach()
  if not ck(openProcess('%(proc)s'), 'attach to %(proc)s') then return false end
  mono_initialize()
  LaunchMonoDataCollector()
  return true
end
'''

EPILOGUE = '''
local t = createTimer(nil)
t.Interval = 4000
t.OnTimer = function(timer)
  timer.Enabled = false
  local ok, err = pcall(run)
  if not ok then log('FATAL %s', tostring(err)) end
  flush()
  if not DEFER_CLOSE then closeCE() end
end
t.Enabled = true
'''


def script_hooks(report):
    runtime, ptr, hooks, _values = read_table()
    tbl = ',\n'.join("  { id='%s', method='%s', build=function() return %s end }"
                     % (h, m, c) for h, m, c in hooks)
    return (PRELUDE % {'report': report, 'proc': PROCESS} + '''
local PTR_BLOCK = [==[
''' + ptr + ''']==]
local DEFER_CLOSE = false
''' + runtime + '''
local HOOKS = {
''' + tbl + '''
}
local function hex(t, n)
  local s = {}
  for i = 1, math.min(n or #t, #t) do s[#s+1] = string.format('%02X', t[i]) end
  return table.concat(s, ' ')
end
local function same(a, b, n)
  for i = 1, n do if a[i] ~= b[i] then return false end end
  return true
end
function run()
  log('=== hook install / restore ===')
  if not attach() then return end
  if not ck(autoAssemble(PTR_BLOCK), 'pointer symbols allocated') then return end
  for _, h in ipairs(HOOKS) do
    local okA, addr = pcall(getAddress, h.method)
    if not ck(okA and addr and addr ~= 0, '%-56s resolves', h.method) then
      log('     %s', tostring(addr))
    else
      local before = readBytes(addr, 24, true)
      log('     %s addr=%X prologue=%s', h.id, addr, hex(before, 12))
      local okB, script = pcall(h.build)
      if not ck(okB, '%-56s qmHook builds', h.method) then
        log('     %s', tostring(script))
      else
        local okC, errC = autoAssemble(script)
        if ck(okC, '%-56s installs%s', h.method,
              okC and '' or (' -- ' .. tostring(errC))) then
          ck(readBytes(addr, 1, false) == 0xE9, '%-56s entry is jmp', h.method)
          ck(autoAssemble(qmUnhook(h.id)), '%-56s unhook assembles', h.method)
          ck(same(before, readBytes(addr, 24, true), 24),
             '%-56s restores exactly', h.method)
        end
      end
    end
  end
end
''' + EPILOGUE)


def script_dump(report):
    cls = ',\n'.join("  '%s'" % c for c in DUMP_CLASSES)
    return (PRELUDE % {'report': report, 'proc': PROCESS} + '''
local DEFER_CLOSE = false
local CLASSES = {
''' + cls + '''
}
function run()
  if not attach() then return end
  log('# FIELD, requested class, declaring class, field, offset, flags, type')
  for _, c in ipairs(CLASSES) do
    local k = mono_findClass('MGSC', c)
    if k == nil or k == 0 then
      log('CLASS\\t%s\\tNOTFOUND', c)
    else
      log('CLASS\\t%s\\tOK', c)
      local cur, depth = k, 0
      while cur ~= nil and cur ~= 0 and depth < 12 do
        local nm = mono_class_getName(cur)
        local ok, list = pcall(mono_class_enumFields, cur)
        if ok and list then
          for _, f in ipairs(list) do
            if f.name ~= nil and f.offset ~= nil then
              log('FIELD\\t%s\\t%s\\t%s\\t%d\\t%d\\t%s', c, tostring(nm),
                  f.name, f.offset, f.flags or 0, tostring(f.typename or '?'))
            end
          end
        end
        cur = mono_class_getParent(cur)
        depth = depth + 1
      end
    end
  end
  pass = pass + 1
end
''' + EPILOGUE)


def script_monitor(report, seconds):
    runtime, ptr, hooks, values = read_table()
    values = [v for v in values if not re.search(r'Faction \[[1-7]\]', v[0])]
    htbl = ',\n'.join("  { id='%s', method='%s', build=function() return %s end }"
                      % (h, m, c) for h, m, c in hooks)
    vtbl = ',\n'.join(
        "  { grp=[[%s]], name=[[%s]], vt=[[%s]], base='%s', offs={%s} }"
        % (g, d, vt, a, ','.join('0x' + o for o in offs))
        for g, d, vt, a, offs in values)
    return (PRELUDE % {'report': report, 'proc': PROCESS} + '''
local PTR_BLOCK = [==[
''' + ptr + ''']==]
local DEFER_CLOSE = true
local WATCH = ''' + str(seconds) + '''
''' + runtime + '''
local HOOKS = {
''' + htbl + '''
}
local VALUES = {
''' + vtbl + '''
}
local seen, elapsed = {}, 0
local function deref(base, offs)
  local p = readQword(base)
  if p == nil or p == 0 then return nil end
  for i = 1, #offs do
    if i == #offs then return p + offs[i] end
    p = readQword(p + offs[i])
    if p == nil or p == 0 then return nil end
  end
  return p
end
local function readTyped(a, vt)
  if vt == '4 Bytes' then return readInteger(a) end
  if vt == '2 Bytes' then return readSmallInteger(a) end
  if vt == 'Byte' then return readBytes(a, 1, false) end
  if vt == 'Float' then return readFloat(a) end
  if vt == 'Double' then return readDouble(a) end
end
local function countSeen()
  local c = 0
  for _ in pairs(seen) do c = c + 1 end
  return c
end
local function sample()
  local newly = {}
  for _, v in ipairs(VALUES) do
    local key = v.grp .. '|' .. v.name
    if not seen[key] then
      local pb = getAddressSafe(v.base)
      if pb then
        local a = deref(pb, v.offs)
        if a then
          local ok, val = pcall(readTyped, a, v.vt)
          if ok and val ~= nil then
            seen[key] = true
            newly[#newly+1] = string.format('  %-46s %-26s = %s',
                                            v.grp, v.name, tostring(val))
          end
        end
      end
    end
  end
  if #newly > 0 then
    log('')
    log('[t+%ds] newly populated:', elapsed)
    for _, l in ipairs(newly) do log('%s', l) end
    flush()
  end
end
local function finish()
  ck(countSeen() > 0, 'at least one value populated (%d of %d)',
     countSeen(), #VALUES)
  for _, v in ipairs(VALUES) do
    if not seen[v.grp .. '|' .. v.name] then
      log('  not seen: %-46s %s', v.grp, v.name)
    end
  end
  for _, h in ipairs(HOOKS) do pcall(function() autoAssemble(qmUnhook(h.id)) end) end
  log('')
  log('all hooks removed')
  flush()
  closeCE()
end
function run()
  if not attach() then return end
  if not ck(autoAssemble(PTR_BLOCK), 'pointer symbols allocated') then return end
  local n = 0
  for _, h in ipairs(HOOKS) do
    local okB, script = pcall(h.build)
    if okB and autoAssemble(script) then n = n + 1 end
  end
  ck(n == #HOOKS, 'all %d hooks installed (got %d)', #HOOKS, n)
  log('')
  log('PLAY NOW: load a save, move, shoot, right-click an item, hover a')
  log('damaged item, trade at a station, start a flight. Watching %ds.', WATCH)
  flush()
  local poll = createTimer(nil)
  poll.Interval = 1000
  poll.OnTimer = function(pt)
    elapsed = elapsed + 1
    pcall(sample)
    if countSeen() >= #VALUES or elapsed >= WATCH then
      pt.Enabled = false
      pcall(finish)
    end
  end
  poll.Enabled = true
end
''' + EPILOGUE)


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('mode', choices=['hooks', 'dump', 'monitor'])
    ap.add_argument('--run', action='store_true',
                    help='launch CE, wait for it to finish, print the report')
    ap.add_argument('--seconds', type=int, default=900,
                    help='monitor watch window (default 900)')
    ap.add_argument('--out', default=None, help='report path')
    args = ap.parse_args()

    report = args.out or os.path.join(HERE, 'ce_%s_report.txt' % args.mode)
    body = {'hooks': lambda: script_hooks(report),
            'dump': lambda: script_dump(report),
            'monitor': lambda: script_monitor(report, args.seconds)}[args.mode]()

    os.makedirs(os.path.dirname(AUTORUN), exist_ok=True)
    with open(AUTORUN, 'w', encoding='utf-8', newline='\n') as f:
        f.write(body)
    print('wrote %s' % AUTORUN)
    print('report -> %s' % report)

    if not args.run:
        print('\nStart Cheat Engine to run it, then delete the autorun script.')
        return 0

    if os.path.exists(report):
        os.remove(report)
    if not os.path.exists(CE_EXE):
        print('Cheat Engine not found at %s' % CE_EXE)
        return 1
    print('launching Cheat Engine...')
    proc = subprocess.Popen([CE_EXE])
    limit = args.seconds + 120 if args.mode == 'monitor' else 180
    deadline = time.time() + limit
    while proc.poll() is None and time.time() < deadline:
        time.sleep(2)
    if proc.poll() is None:
        proc.terminate()
        print('CE did not exit within %ds; terminated' % limit)
    try:
        os.remove(AUTORUN)
        print('removed the temporary autorun script')
    except OSError:
        pass
    if not os.path.exists(report):
        print('no report was written')
        return 1
    with open(report, encoding='utf-8') as f:
        text = f.read()
    print('\n' + text)
    return 0 if '\nFAIL' not in text and 'FAIL=0' in text else 1


if __name__ == '__main__':
    sys.exit(main())
