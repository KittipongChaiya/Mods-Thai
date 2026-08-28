#!/usr/bin/env python3
"""Inspect Quasimorph's managed types: field layout, signatures, IL.

Everything the cheat table depends on can be derived from here, so a future
game update is re-checked rather than guessed.

    python inspect_types.py layout Faction WeaponRecord
    python inspect_types.py method TradeSystem:BuyStationItems
    python inspect_types.py il BreakableItemComponent:get_Durability
    python inspect_types.py grep Evacuation
"""
import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import il as ILM  # noqa: E402
from monolayout import Sig, load_universe  # noqa: E402

DEFAULT_GAME = r'C:\Users\Administrator\Desktop\Quasimorph.v1.0.3\game'

# Windows x64: the first four integer/pointer arguments live in these registers,
# everything after that is on the stack. `this` counts as the first argument.
WIN64_REGS = ['RCX', 'RDX', 'R8', 'R9']
SHADOW_SPACE = 0x20


def describe(U, md, s, depth=0):
    """Render a type from a signature blob."""
    et = s.u8()
    while et in (0x1f, 0x20, 0x45):        # CMOD_REQD / CMOD_OPT / PINNED
        if et != 0x45:
            s.typedeforref()
        et = s.u8()
    prim = {0x01: 'void', 0x02: 'bool', 0x03: 'char', 0x04: 'sbyte', 0x05: 'byte',
            0x06: 'short', 0x07: 'ushort', 0x08: 'int', 0x09: 'uint', 0x0a: 'long',
            0x0b: 'ulong', 0x0c: 'float', 0x0d: 'double', 0x0e: 'string',
            0x18: 'IntPtr', 0x19: 'UIntPtr', 0x1c: 'object'}
    if et in prim:
        return prim[et]
    if et in (0x11, 0x12):                 # VALUETYPE / CLASS
        tab, rid = s.typedeforref()
        try:
            md2, rid2 = U.resolve_ref(md, tab, rid)
            return U.typedef_name(md2, rid2).rsplit('.', 1)[-1]
        except Exception:
            return '?ref'
    if et == 0x1d:                         # SZARRAY
        return describe(U, md, s, depth + 1) + '[]'
    if et == 0x15:                         # GENERICINST
        s.u8()
        tab, rid = s.typedeforref()
        args = [describe(U, md, s, depth + 1) for _ in range(s.compressed())]
        try:
            md2, rid2 = U.resolve_ref(md, tab, rid)
            base = U.typedef_name(md2, rid2).rsplit('.', 1)[-1]
        except Exception:
            base = '?gen'
        return '%s<%s>' % (base.split('`')[0], ','.join(args))
    if et in (0x13, 0x1e):                 # VAR / MVAR
        s.compressed()
        return 'T'
    if et == 0x14:                         # ARRAY
        inner = describe(U, md, s, depth + 1)
        s.compressed()
        for _ in range(s.compressed()):
            s.compressed()
        for _ in range(s.compressed()):
            s.compressed()
        return inner + '[,]'
    if et == 0x10:                         # BYREF
        return describe(U, md, s, depth + 1) + '&'
    return 'et_%02x' % et


def field_types(U, cls, ns='MGSC'):
    hit = U.find(ns, cls)
    if not hit:
        return None
    md, rid = hit
    offs, size, notes = U.layout(md, rid, 'decl')
    types, chain, cur = {}, [], (md, rid)
    for _ in range(10):
        chain.append(cur)
        nxt = U.base_of(*cur)
        if nxt is None or U.typedef_name(*nxt) == 'System.Object':
            break
        cur = nxt
    for cmd, crid in chain:
        cn = U.typedef_name(cmd, crid).rsplit('.', 1)[-1]
        for fn, sb in U.fields_of(cmd, crid):
            s = Sig(sb)
            s.u8()
            types['%s::%s' % (cn, fn)] = describe(U, cmd, s)
    rows = sorted([(v, k) for k, v in offs.items() if '::' in k])
    return [(v, k, types.get(k, '?')) for v, k in rows], size, notes


def cmd_layout(U, args):
    for cls in args.names:
        ns, _, name = cls.rpartition('.')
        r = field_types(U, name, ns or 'MGSC')
        print('\n' + '=' * 78)
        if r is None:
            print('%s NOT FOUND' % cls)
            continue
        rows, size, notes = r
        md, rid = U.find(ns or 'MGSC', name)
        print('%s   instance_size=0x%X   base=%s'
              % (U.typedef_name(md, rid), size, U.base_name(md, rid)))
        for off, fname, ty in rows:
            print('   0x%03X  %-52s %s' % (off, fname, ty))
        if notes:
            print('   NOTES: %s' % '; '.join(notes[:6]))


def method_sig(U, md, mrow):
    s = Sig(md.blob(mrow['Signature']))
    flags = s.u8()
    if flags & 0x10:
        s.compressed()
    n = s.compressed()
    ret = describe(U, md, s)
    params = []
    for _ in range(n):
        try:
            params.append(describe(U, md, s))
        except Exception:
            params.append('?')
    return bool(flags & 0x20), ret, params


def param_names(md, mrow, rid):
    start = mrow['ParamList']
    nxt = md.read(6, rid + 1)
    end = nxt['ParamList'] if nxt else md.count(8) + 1
    out = {}
    for i in range(start, end):
        pr = md.read(8, i)
        if pr:
            out[pr['Sequence']] = md.string(pr['Name'])
    return out


def cmd_method(U, args):
    for spec in args.names:
        cls, _, meth = spec.partition(':')
        ns, _, cls = cls.rpartition('.')
        res = ILM.find_method(U, ns or 'MGSC', cls, meth)
        print('\n### %s' % spec)
        if not res:
            print('   NOT FOUND')
            continue
        for md, rid, mrow in res:
            has_this, ret, params = method_sig(U, md, mrow)
            pn = param_names(md, mrow, rid)
            print('   %s %s(%s)   [%s]  rva=0x%X' % (
                ret, meth,
                ', '.join('%s %s' % (t, pn.get(i + 1, 'p%d' % (i + 1)))
                          for i, t in enumerate(params)),
                'instance' if has_this else 'STATIC', mrow['RVA']))
            slots = ([('this', cls)] if has_this else []) + \
                    [(pn.get(i + 1, 'p%d' % (i + 1)), t) for i, t in enumerate(params)]
            print('   entry-point register map (Windows x64):')
            for i, (nm, t) in enumerate(slots):
                if i < 4:
                    where = WIN64_REGS[i]
                else:
                    where = '[rsp+%X]' % (8 + SHADOW_SPACE + 8 * (i - 4))
                print('        %-10s = %-26s : %s' % (where, nm, t))


def cmd_il(U, args):
    for spec in args.names:
        cls, _, meth = spec.partition(':')
        ns, _, cls = cls.rpartition('.')
        res = ILM.find_method(U, ns or 'MGSC', cls, meth)
        print('\n### %s' % spec)
        if not res:
            print('   NOT FOUND')
            continue
        for md, rid, mrow in res:
            b = ILM.body(md, mrow['RVA'])
            print('   IL size = %s' % (b[1] if b else '<none>'))
            for line in ILM.dump(U, md, mrow, limit=args.limit):
                print(line)


def cmd_grep(U, args):
    for (ns, nm), (md, rid) in sorted(U.by_name.items()):
        if args.ns and ns != args.ns:
            continue
        try:
            for fn, _sb in U.fields_of(md, rid):
                if any(w.lower() in fn.lower() for w in args.names):
                    print('   %-34s %s' % (nm, fn))
        except Exception:
            pass


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('command', choices=['layout', 'method', 'il', 'grep'])
    ap.add_argument('names', nargs='+')
    ap.add_argument('--game', default=DEFAULT_GAME, help='game root folder')
    ap.add_argument('--ns', default='MGSC', help='namespace filter for grep')
    ap.add_argument('--limit', type=int, default=80, help='max IL lines')
    args = ap.parse_args()

    U = load_universe(args.game)
    print('# %d assemblies, %d types indexed from %s'
          % (len(U.asms), len(U.by_name), args.game))
    {'layout': cmd_layout, 'method': cmd_method,
     'il': cmd_il, 'grep': cmd_grep}[args.command](U, args)


if __name__ == '__main__':
    main()
