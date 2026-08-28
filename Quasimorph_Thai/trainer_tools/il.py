"""Tiny CIL reader: method bodies + opcode walk, enough to see field access."""
import struct

# operand size per one-byte opcode; anything absent is 0
OP1 = {}
for _o in (0x0e, 0x0f, 0x10, 0x11, 0x12, 0x13, 0x1f, 0x2b, 0x2c, 0x2d, 0xde):
    OP1[_o] = 1
for _o in range(0x2e, 0x38):
    OP1[_o] = 1
for _o in (0x20, 0x22, 0x27, 0x28, 0x29, 0x38, 0x39, 0x3a, 0x6f, 0x70, 0x71, 0x72,
           0x73, 0x74, 0x75, 0x79, 0x7b, 0x7c, 0x7d, 0x7e, 0x7f, 0x80, 0x81, 0x8c,
           0x8d, 0x8f, 0xa3, 0xa4, 0xa5, 0xc2, 0xc6, 0xd0, 0xdd):
    OP1[_o] = 4
for _o in range(0x3b, 0x45):
    OP1[_o] = 4
for _o in (0x21, 0x23):
    OP1[_o] = 8
OP2 = {0x06: 4, 0x07: 4, 0x09: 2, 0x0a: 2, 0x0b: 2, 0x0c: 2, 0x0d: 2, 0x0e: 2,
       0x12: 1, 0x15: 4, 0x16: 4, 0x19: 1, 0x1c: 4}

NAMES1 = {0x02: 'ldarg.0', 0x03: 'ldarg.1', 0x04: 'ldarg.2', 0x05: 'ldarg.3',
          0x25: 'dup', 0x26: 'pop', 0x28: 'call', 0x2a: 'ret', 0x6f: 'callvirt',
          0x72: 'ldstr', 0x73: 'newobj', 0x7b: 'ldfld', 0x7c: 'ldflda',
          0x7d: 'stfld', 0x7e: 'ldsfld', 0x80: 'stsfld', 0x8c: 'box',
          0x0e: 'ldarg.s', 0x1f: 'ldc.i4.s', 0x20: 'ldc.i4', 0x22: 'ldc.r4',
          0x58: 'add', 0x59: 'sub', 0x5a: 'mul', 0x5b: 'div', 0x6b: 'conv.r4',
          0x69: 'conv.i4', 0x6a: 'conv.i8', 0x8e: 'ldlen', 0xa5: 'unbox.any',
          0x14: 'ldnull', 0x2b: 'br.s', 0x2c: 'brfalse.s', 0x2d: 'brtrue.s',
          0x39: 'brfalse', 0x3a: 'brtrue', 0x0a: 'stloc.0', 0x06: 'ldloc.0',
          0xd0: 'ldtoken', 0x75: 'isinst', 0x74: 'castclass'}
for _i in range(0x15, 0x1f):
    NAMES1.setdefault(_i, 'ldc.i4.%d' % (_i - 0x16))


def body(md, rva):
    """Return (il_bytes, code_size) for a MethodDef RVA, or None."""
    if rva == 0:
        return None
    off = md.pe.rva2off(rva)
    b0 = md.raw[off]
    if b0 & 3 == 2:
        size = b0 >> 2
        return md.raw[off + 1:off + 1 + size], size
    if b0 & 3 == 3:
        flags_hdr, = struct.unpack_from('<H', md.raw, off)
        hdr = (flags_hdr >> 12) * 4
        size, = struct.unpack_from('<I', md.raw, off + 4)
        return md.raw[off + hdr:off + hdr + size], size
    return None


def walk(il):
    """Yield (offset, name, opcode, operand_bytes)."""
    i = 0
    n = len(il)
    while i < n:
        start = i
        op = il[i]
        i += 1
        if op == 0xfe:
            op2 = il[i]
            i += 1
            sz = OP2.get(op2, 0)
            yield start, 'fe%02x' % op2, 0xfe00 | op2, il[i:i + sz]
            i += sz
            continue
        if op == 0x45:  # switch
            cnt, = struct.unpack_from('<I', il, i)
            i += 4 + 4 * cnt
            yield start, 'switch', op, b''
            continue
        sz = OP1.get(op, 0)
        yield start, NAMES1.get(op, 'op_%02x' % op), op, il[i:i + sz]
        i += sz


def token(operand):
    if len(operand) != 4:
        return None
    v, = struct.unpack_from('<I', operand, 0)
    return v >> 24, v & 0xffffff  # (table, rid)


def field_name(U, md, tok):
    """Resolve a Field / MemberRef token to 'Type::field'."""
    if tok is None:
        return None
    tab, rid = tok
    if tab == 0x04:  # Field
        fr = md.read(4, rid)
        owner = owner_of_field(md, rid)
        return '%s::%s' % (owner, md.string(fr['Name']))
    if tab == 0x0a:  # MemberRef
        mr = md.read(10, rid)
        ctab, crid = mr['Class']
        try:
            nm = md.string(md.read(1, crid)['Name']) if ctab == 1 else '?'
        except Exception:
            nm = '?'
        return '%s::%s' % (nm, md.string(mr['Name']))
    return 'tok(%02x,%d)' % (tab, rid)


_owner_cache = {}


def owner_of_field(md, frid):
    key = id(md)
    if key not in _owner_cache:
        starts = []
        for rid in range(1, md.count(2) + 1):
            starts.append((md.read(2, rid)['FieldList'], rid))
        starts.sort()
        _owner_cache[key] = starts
    starts = _owner_cache[key]
    lo, hi, best = 0, len(starts) - 1, None
    while lo <= hi:
        mid = (lo + hi) // 2
        if starts[mid][0] <= frid:
            best = starts[mid][1]
            lo = mid + 1
        else:
            hi = mid - 1
    if best is None:
        return '?'
    r = md.read(2, best)
    ns, nm = md.string(r['Namespace']), md.string(r['Name'])
    return nm


def method_name(md, tok):
    if tok is None:
        return None
    tab, rid = tok
    if tab == 0x06:
        return md.string(md.read(6, rid)['Name'])
    if tab == 0x0a:
        return md.string(md.read(10, rid)['Name'])
    if tab == 0x2b:
        return 'methodspec#%d' % rid
    return 'tok(%02x,%d)' % (tab, rid)


def find_method(U, ns, cls, name):
    """Return (md, rid, methoddef_row) for MGSC.Cls::name."""
    hit = U.find(ns, cls)
    if hit is None:
        return None
    md, rid = hit
    r = md.read(2, rid)
    start = r['MethodList']
    nxt = md.read(2, rid + 1)
    end = nxt['MethodList'] if nxt else md.count(6) + 1
    out = []
    for i in range(start, end):
        mr = md.read(6, i)
        if md.string(mr['Name']) == name:
            out.append((md, i, mr))
    return out


def dump(U, md, mrow, limit=400):
    b = body(md, mrow['RVA'])
    if b is None:
        return ['<no body>']
    il, _ = b
    lines = []
    for off, nm, op, operand in walk(il):
        extra = ''
        if op in (0x7b, 0x7c, 0x7d, 0x7e, 0x80):
            extra = ' ' + str(field_name(U, md, token(operand)))
        elif op in (0x28, 0x6f, 0x73):
            extra = ' ' + str(method_name(md, token(operand)))
        lines.append('  IL_%04X: %-12s%s' % (off, nm, extra))
        if len(lines) >= limit:
            lines.append('  ...')
            break
    return lines
