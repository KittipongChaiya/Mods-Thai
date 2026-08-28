"""Mono x64 object-layout model built on top of cli_meta.

Reproduces `mono_class_layout_fields` closely enough to predict the runtime
field offsets that Cheat Engine pointer chains rely on.

Two candidate models are provided because Mono's behaviour for AUTO layout
classes depends on whether "GC aware layout" is compiled in:

  decl : single pass, fields in declaration order
  gc   : two passes, reference-bearing fields first (GC aware)

The correct model is chosen empirically by replaying known-good offsets from
the 0.9.87 cheat table (see validate.py).
"""
import os

from cli_meta import Metadata

PTR = 8
OBJ_HEADER = 16  # MonoObject: vtable ptr + sync block, x64

# ---- ECMA-335 II.23.1.16 element types
ET_VOID, ET_BOOLEAN, ET_CHAR = 0x01, 0x02, 0x03
ET_I1, ET_U1, ET_I2, ET_U2, ET_I4, ET_U4, ET_I8, ET_U8 = range(0x04, 0x0c)
ET_R4, ET_R8, ET_STRING, ET_PTR, ET_BYREF = 0x0c, 0x0d, 0x0e, 0x0f, 0x10
ET_VALUETYPE, ET_CLASS, ET_VAR, ET_ARRAY, ET_GENERICINST = 0x11, 0x12, 0x13, 0x14, 0x15
ET_TYPEDBYREF, ET_I, ET_U, ET_FNPTR = 0x16, 0x18, 0x19, 0x1b
ET_OBJECT, ET_SZARRAY, ET_MVAR = 0x1c, 0x1d, 0x1e
ET_CMOD_REQD, ET_CMOD_OPT, ET_PINNED = 0x1f, 0x20, 0x45

PRIMITIVE = {
    ET_BOOLEAN: (1, 1), ET_CHAR: (2, 2),
    ET_I1: (1, 1), ET_U1: (1, 1), ET_I2: (2, 2), ET_U2: (2, 2),
    ET_I4: (4, 4), ET_U4: (4, 4), ET_I8: (8, 8), ET_U8: (8, 8),
    ET_R4: (4, 4), ET_R8: (8, 8),
    ET_I: (PTR, PTR), ET_U: (PTR, PTR), ET_PTR: (PTR, PTR), ET_FNPTR: (PTR, PTR),
}
REFTYPES = {ET_STRING, ET_CLASS, ET_OBJECT, ET_SZARRAY, ET_ARRAY, ET_VAR, ET_MVAR}

FIELD_STATIC = 0x0010
FIELD_LITERAL = 0x0040
FIELD_HASRVA = 0x0100

TA_LAYOUT_MASK = 0x18
TA_AUTO, TA_SEQUENTIAL, TA_EXPLICIT = 0x00, 0x08, 0x10


class Sig:
    """Byte cursor over a signature blob."""

    def __init__(self, b):
        self.b, self.i = b, 0

    def u8(self):
        v = self.b[self.i]
        self.i += 1
        return v

    def compressed(self):
        b0 = self.u8()
        if b0 & 0x80 == 0:
            return b0
        if b0 & 0xc0 == 0x80:
            return ((b0 & 0x3f) << 8) | self.u8()
        return ((b0 & 0x1f) << 24) | (self.u8() << 16) | (self.u8() << 8) | self.u8()

    def typedeforref(self):
        v = self.compressed()
        return {0: 2, 1: 1, 2: 27}[v & 3], v >> 2


class TypeRefUnresolved(Exception):
    pass


class Universe:
    """A set of assemblies that can resolve TypeRefs between each other."""

    def __init__(self, paths):
        self.asms = []
        self.by_name = {}
        for p in paths:
            try:
                md = Metadata(p)
            except Exception:
                continue
            md.path = p
            self.asms.append(md)
            self._index(md)
        self._struct_cache = {}

    def _index(self, md):
        for rid in range(1, md.count(2) + 1):
            r = md.read(2, rid)
            key = (md.string(r['Namespace']), md.string(r['Name']))
            self.by_name.setdefault(key, (md, rid))

    # -- type access -------------------------------------------------------
    def find(self, ns, name):
        return self.by_name.get((ns, name))

    def find_by_name(self, name):
        return [(ns, md, rid) for (ns, n), (md, rid) in self.by_name.items() if n == name]

    def typedef_name(self, md, rid):
        r = md.read(2, rid)
        ns, nm = md.string(r['Namespace']), md.string(r['Name'])
        return f'{ns}.{nm}' if ns else nm

    def resolve_ref(self, md, tab, rid):
        """Map a TypeDef/TypeRef reference in `md` to (md2, rid2)."""
        if tab == 2:
            return (md, rid)
        if tab == 1:
            r = md.read(1, rid)
            key = (md.string(r['Namespace']), md.string(r['Name']))
            hit = self.by_name.get(key)
            if hit is None:
                raise TypeRefUnresolved('.'.join(x for x in key if x))
            return hit
        raise TypeRefUnresolved('TypeSpec')

    def fields_of(self, md, rid):
        """Instance fields of a TypeDef, in declaration order."""
        r = md.read(2, rid)
        start = r['FieldList']
        nxt = md.read(2, rid + 1)
        end = nxt['FieldList'] if nxt else md.count(4) + 1
        out = []
        for f in range(start, end):
            fr = md.read(4, f)
            if fr is None:
                continue
            if fr['Flags'] & (FIELD_STATIC | FIELD_LITERAL | FIELD_HASRVA):
                continue
            out.append((md.string(fr['Name']), md.blob(fr['Signature'])))
        return out

    def base_of(self, md, rid):
        r = md.read(2, rid)
        tab, ridx = r['Extends']
        if ridx == 0:
            return None
        try:
            return self.resolve_ref(md, tab, ridx)
        except TypeRefUnresolved:
            return None

    def base_name(self, md, rid):
        r = md.read(2, rid)
        tab, ridx = r['Extends']
        if ridx == 0:
            return None
        if tab == 2:
            return self.typedef_name(md, ridx)
        if tab == 1:
            rr = md.read(1, ridx)
            ns, nm = md.string(rr['Namespace']), md.string(rr['Name'])
            return f'{ns}.{nm}' if ns else nm
        return 'TypeSpec'

    # -- signature -> (size, align, is_ref) --------------------------------
    def type_info(self, md, sig, depth=0):
        et = sig.u8()
        while et in (ET_CMOD_REQD, ET_CMOD_OPT, ET_PINNED):
            if et != ET_PINNED:
                sig.typedeforref()
            et = sig.u8()
        return self._type_info_et(md, sig, et, depth)

    def _type_info_et(self, md, sig, et, depth):
        if et in PRIMITIVE:
            return PRIMITIVE[et] + (False,)
        if et in (ET_STRING, ET_OBJECT):
            return (PTR, PTR, True)
        if et in (ET_VAR, ET_MVAR):
            sig.compressed()
            return (PTR, PTR, True)
        if et == ET_SZARRAY:
            self.type_info(md, sig, depth + 1)
            return (PTR, PTR, True)
        if et == ET_ARRAY:
            self.type_info(md, sig, depth + 1)
            sig.compressed()                       # rank
            for _ in range(sig.compressed()):      # sizes
                sig.compressed()
            for _ in range(sig.compressed()):      # lo bounds
                sig.compressed()
            return (PTR, PTR, True)
        if et == ET_PTR:
            self.type_info(md, sig, depth + 1)
            return (PTR, PTR, False)
        if et == ET_CLASS:
            sig.typedeforref()
            return (PTR, PTR, True)
        if et == ET_TYPEDBYREF:
            return (16, PTR, True)
        if et == ET_VALUETYPE:
            tab, rid = sig.typedeforref()
            return self.valuetype_info(md, tab, rid, depth)
        if et == ET_GENERICINST:
            inner = sig.u8()
            tab, rid = sig.typedeforref()
            for _ in range(sig.compressed()):
                self.type_info(md, sig, depth + 1)
            if inner == ET_CLASS:
                return (PTR, PTR, True)
            return self.valuetype_info(md, tab, rid, depth, generic=True)
        raise TypeRefUnresolved('element type 0x%02x' % et)

    def valuetype_info(self, md, tab, rid, depth, generic=False):
        try:
            md2, rid2 = self.resolve_ref(md, tab, rid)
        except TypeRefUnresolved as e:
            raise TypeRefUnresolved('valuetype %s' % e)
        key = (id(md2), rid2, generic)
        if key in self._struct_cache:
            return self._struct_cache[key]
        if depth > 12:
            raise TypeRefUnresolved('recursion')
        self._struct_cache[key] = (PTR, PTR, False)  # break cycles
        base = self.base_name(md2, rid2)
        if base == 'System.Enum':
            for _n, sb in self.fields_of(md2, rid2):
                s = Sig(sb)
                s.u8()  # FIELD sentinel 0x06
                info = self.type_info(md2, s, depth + 1)
                self._struct_cache[key] = info
                return info
            raise TypeRefUnresolved('enum without value__')
        # plain struct: pack its fields from 0
        size, align, hasref = 0, 1, False
        for _n, sb in self.fields_of(md2, rid2):
            s = Sig(sb)
            s.u8()
            fs, fa, fr = self.type_info(md2, s, depth + 1)
            hasref |= fr
            size = align_to(size, fa) + fs
            align = max(align, fa)
        size = align_to(size, align) if align else 0
        info = (max(size, 1), max(align, 1), hasref)
        self._struct_cache[key] = info
        return info

    # -- class layout ------------------------------------------------------
    def layout(self, md, rid, model='decl'):
        """Return (offsets, instance_size, notes) for a reference type."""
        chain, notes = [], []
        cur = (md, rid)
        seen = set()
        while cur is not None:
            if (id(cur[0]), cur[1]) in seen:
                break
            seen.add((id(cur[0]), cur[1]))
            chain.append(cur)
            nm = self.typedef_name(*cur)
            if nm in ('System.Object', 'System.ValueType', 'System.Enum'):
                break
            cur = self.base_of(*cur)
            if cur is None:
                bn = self.base_name(*chain[-1])
                if bn not in (None, 'System.Object'):
                    notes.append('unresolved base: %s' % bn)
                break
        chain.reverse()

        offsets, real = {}, OBJ_HEADER
        for cmd, crid in chain:
            nm = self.typedef_name(cmd, crid)
            if nm in ('System.Object', 'System.ValueType', 'System.Enum'):
                continue
            flags = cmd.read(2, crid)['Flags']
            auto = (flags & TA_LAYOUT_MASK) == TA_AUTO
            gc_aware = auto and model == 'gc'
            fields = []
            for fname, sb in self.fields_of(cmd, crid):
                s = Sig(sb)
                s.u8()
                try:
                    fs, fa, fr = self.type_info(cmd, s)
                except TypeRefUnresolved as e:
                    notes.append('%s.%s: %s' % (nm, fname, e))
                    fs, fa, fr = PTR, PTR, True
                fields.append((fname, fs, fa, fr))
            passes = (True, False) if gc_aware else (None,)
            for want_ref in passes:
                for fname, fs, fa, fr in fields:
                    if want_ref is not None and fr != want_ref:
                        continue
                    real = align_to(real, fa)
                    offsets['%s::%s' % (nm.rsplit('.', 1)[-1], fname)] = real
                    offsets.setdefault(fname, real)
                    real += fs
        return offsets, align_to(real, PTR), notes


def align_to(v, a):
    return (v + a - 1) & ~(a - 1) if a > 1 else v


def managed_dir(game):
    return os.path.join(game, 'Quasimorph_Data', 'Managed')


def load_universe(game):
    md = managed_dir(game)
    prefer = ['Assembly-CSharp.dll', 'mscorlib.dll', 'netstandard.dll',
              'System.dll', 'System.Core.dll']
    files = [os.path.join(md, f) for f in prefer if os.path.exists(os.path.join(md, f))]
    for f in sorted(os.listdir(md)):
        if f.endswith('.dll') and f not in prefer:
            files.append(os.path.join(md, f))
    return Universe(files)
