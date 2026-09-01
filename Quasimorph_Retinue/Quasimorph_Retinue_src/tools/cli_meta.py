"""Minimal ECMA-335 (.NET metadata) reader.

Parses a managed PE well enough to recover TypeDef / Field / MethodDef rows and
field signatures, which is all that is needed to reason about Mono's runtime
object layout.
"""
import struct

# ---------------------------------------------------------------- PE plumbing


class PE:
    def __init__(self, data):
        self.d = data
        pe = struct.unpack_from('<I', data, 0x3c)[0]
        assert data[pe:pe + 4] == b'PE\0\0', 'not a PE file'
        nsec, = struct.unpack_from('<H', data, pe + 6)
        optsz, = struct.unpack_from('<H', data, pe + 20)
        opt = pe + 24
        magic, = struct.unpack_from('<H', data, opt)
        self.pe32plus = magic == 0x20b
        ddoff = opt + (112 if self.pe32plus else 96)
        self.datadirs = [struct.unpack_from('<II', data, ddoff + 8 * i) for i in range(16)]
        secoff = opt + optsz
        self.sections = []
        for i in range(nsec):
            s = secoff + 40 * i
            name = data[s:s + 8].rstrip(b'\0').decode('ascii', 'replace')
            vsize, vaddr, rsize, raddr = struct.unpack_from('<IIII', data, s + 8)
            self.sections.append((name, vaddr, vsize, raddr, rsize))

    def rva2off(self, rva):
        for _n, va, vs, ra, rs in self.sections:
            if va <= rva < va + max(vs, rs):
                return ra + (rva - va)
        raise ValueError('unmapped RVA 0x%x' % rva)


# ------------------------------------------------------------- table schema

CODED = {
    'TypeDefOrRef': [2, 1, 27],
    'HasConstant': [4, 8, 23],
    'HasCustomAttribute': [6, 4, 1, 2, 8, 9, 10, 0, 14, 23, 20, 17, 26, 27, 32,
                           35, 38, 39, 40, 42, 44, 43],
    'HasFieldMarshal': [4, 8],
    'HasDeclSecurity': [2, 6, 32],
    'MemberRefParent': [2, 1, 26, 6, 27],
    'HasSemantics': [20, 23],
    'MethodDefOrRef': [6, 10],
    'MemberForwarded': [4, 6],
    'Implementation': [38, 35, 39],
    'CustomAttributeType': [-1, -1, 6, 10, -1],
    'ResolutionScope': [0, 26, 35, 1],
    'TypeOrMethodDef': [2, 6],
}


def C(n):
    return ('coded', n)


def T(i):
    return ('tab', i)


SCHEMA = {
    0: ('Module', [('Generation', 'u2'), ('Name', 'str'), ('Mvid', 'guid'),
                   ('EncId', 'guid'), ('EncBaseId', 'guid')]),
    1: ('TypeRef', [('ResolutionScope', C('ResolutionScope')), ('Name', 'str'),
                    ('Namespace', 'str')]),
    2: ('TypeDef', [('Flags', 'u4'), ('Name', 'str'), ('Namespace', 'str'),
                    ('Extends', C('TypeDefOrRef')), ('FieldList', T(4)),
                    ('MethodList', T(6))]),
    3: ('FieldPtr', [('Field', T(4))]),
    4: ('Field', [('Flags', 'u2'), ('Name', 'str'), ('Signature', 'blob')]),
    5: ('MethodPtr', [('Method', T(6))]),
    6: ('MethodDef', [('RVA', 'u4'), ('ImplFlags', 'u2'), ('Flags', 'u2'),
                      ('Name', 'str'), ('Signature', 'blob'), ('ParamList', T(8))]),
    7: ('ParamPtr', [('Param', T(8))]),
    8: ('Param', [('Flags', 'u2'), ('Sequence', 'u2'), ('Name', 'str')]),
    9: ('InterfaceImpl', [('Class', T(2)), ('Interface', C('TypeDefOrRef'))]),
    10: ('MemberRef', [('Class', C('MemberRefParent')), ('Name', 'str'),
                       ('Signature', 'blob')]),
    11: ('Constant', [('Type', 'u1'), ('Pad', 'u1'), ('Parent', C('HasConstant')),
                      ('Value', 'blob')]),
    12: ('CustomAttribute', [('Parent', C('HasCustomAttribute')),
                             ('Type', C('CustomAttributeType')), ('Value', 'blob')]),
    13: ('FieldMarshal', [('Parent', C('HasFieldMarshal')), ('NativeType', 'blob')]),
    14: ('DeclSecurity', [('Action', 'u2'), ('Parent', C('HasDeclSecurity')),
                          ('PermissionSet', 'blob')]),
    15: ('ClassLayout', [('PackingSize', 'u2'), ('ClassSize', 'u4'), ('Parent', T(2))]),
    16: ('FieldLayout', [('Offset', 'u4'), ('Field', T(4))]),
    17: ('StandAloneSig', [('Signature', 'blob')]),
    18: ('EventMap', [('Parent', T(2)), ('EventList', T(20))]),
    19: ('EventPtr', [('Event', T(20))]),
    20: ('Event', [('EventFlags', 'u2'), ('Name', 'str'),
                   ('EventType', C('TypeDefOrRef'))]),
    21: ('PropertyMap', [('Parent', T(2)), ('PropertyList', T(23))]),
    22: ('PropertyPtr', [('Property', T(23))]),
    23: ('Property', [('Flags', 'u2'), ('Name', 'str'), ('Type', 'blob')]),
    24: ('MethodSemantics', [('Semantics', 'u2'), ('Method', T(6)),
                             ('Association', C('HasSemantics'))]),
    25: ('MethodImpl', [('Class', T(2)), ('MethodBody', C('MethodDefOrRef')),
                        ('MethodDeclaration', C('MethodDefOrRef'))]),
    26: ('ModuleRef', [('Name', 'str')]),
    27: ('TypeSpec', [('Signature', 'blob')]),
    28: ('ImplMap', [('MappingFlags', 'u2'), ('MemberForwarded', C('MemberForwarded')),
                     ('ImportName', 'str'), ('ImportScope', T(26))]),
    29: ('FieldRVA', [('RVA', 'u4'), ('Field', T(4))]),
    30: ('ENCLog', [('Token', 'u4'), ('FuncCode', 'u4')]),
    31: ('ENCMap', [('Token', 'u4')]),
    32: ('Assembly', [('HashAlgId', 'u4'), ('Major', 'u2'), ('Minor', 'u2'),
                      ('Build', 'u2'), ('Rev', 'u2'), ('Flags', 'u4'),
                      ('PublicKey', 'blob'), ('Name', 'str'), ('Culture', 'str')]),
    33: ('AssemblyProcessor', [('Processor', 'u4')]),
    34: ('AssemblyOS', [('OSPlatformID', 'u4'), ('OSMajor', 'u4'), ('OSMinor', 'u4')]),
    35: ('AssemblyRef', [('Major', 'u2'), ('Minor', 'u2'), ('Build', 'u2'), ('Rev', 'u2'),
                         ('Flags', 'u4'), ('PublicKeyOrToken', 'blob'), ('Name', 'str'),
                         ('Culture', 'str'), ('HashValue', 'blob')]),
    36: ('AssemblyRefProcessor', [('Processor', 'u4'), ('AssemblyRef', T(35))]),
    37: ('AssemblyRefOS', [('OSPlatformID', 'u4'), ('OSMajor', 'u4'), ('OSMinor', 'u4'),
                           ('AssemblyRef', T(35))]),
    38: ('File', [('Flags', 'u4'), ('Name', 'str'), ('HashValue', 'blob')]),
    39: ('ExportedType', [('Flags', 'u4'), ('TypeDefId', 'u4'), ('Name', 'str'),
                          ('Namespace', 'str'), ('Implementation', C('Implementation'))]),
    40: ('ManifestResource', [('Offset', 'u4'), ('Flags', 'u4'), ('Name', 'str'),
                              ('Implementation', C('Implementation'))]),
    41: ('NestedClass', [('NestedClass', T(2)), ('EnclosingClass', T(2))]),
    42: ('GenericParam', [('Number', 'u2'), ('Flags', 'u2'),
                          ('Owner', C('TypeOrMethodDef')), ('Name', 'str')]),
    43: ('MethodSpec', [('Method', C('MethodDefOrRef')), ('Instantiation', 'blob')]),
    44: ('GenericParamConstraint', [('Owner', T(42)), ('Constraint', C('TypeDefOrRef'))]),
}


class Metadata:
    def __init__(self, path):
        self.raw = open(path, 'rb').read()
        self.pe = PE(self.raw)
        cli_rva, _ = self.pe.datadirs[14]
        cli = self.pe.rva2off(cli_rva)
        md_rva, _md_size = struct.unpack_from('<II', self.raw, cli + 8)
        self.md = self.pe.rva2off(md_rva)
        self._parse_root()
        self._parse_tables()

    # -- streams -----------------------------------------------------------
    def _parse_root(self):
        d, md = self.raw, self.md
        assert d[md:md + 4] == b'BSJB'
        vlen, = struct.unpack_from('<I', d, md + 12)
        p = md + 16 + vlen + 2  # skip version string + 2-byte flags
        nstreams, = struct.unpack_from('<H', d, p)
        p += 2
        self.streams = {}
        for _ in range(nstreams):
            off, size = struct.unpack_from('<II', d, p)
            p += 8
            end = d.index(b'\0', p)
            name = d[p:end].decode('ascii')
            p = end + 1
            p = md + (((p - md) + 3) & ~3)  # header names pad to 4 rel. to root
            self.streams[name] = (md + off, size)
        self.strings = self.streams.get('#Strings')
        self.blobs = self.streams.get('#Blob')

    def string(self, idx):
        base, _ = self.strings
        end = self.raw.index(b'\0', base + idx)
        return self.raw[base + idx:end].decode('utf-8', 'replace')

    def blob(self, idx):
        base, _ = self.blobs
        p = base + idx
        b0 = self.raw[p]
        if b0 & 0x80 == 0:
            n, p = b0 & 0x7f, p + 1
        elif b0 & 0xc0 == 0x80:
            n, p = ((b0 & 0x3f) << 8) | self.raw[p + 1], p + 2
        else:
            n = ((b0 & 0x1f) << 24) | (self.raw[p + 1] << 16) | \
                (self.raw[p + 2] << 8) | self.raw[p + 3]
            p += 4
        return self.raw[p:p + n]

    # -- #~ table stream ---------------------------------------------------
    def _parse_tables(self):
        d = self.raw
        base, _ = self.streams['#~']
        heapsizes = d[base + 6]
        self.s_str = 4 if heapsizes & 1 else 2
        self.s_guid = 4 if heapsizes & 2 else 2
        self.s_blob = 4 if heapsizes & 4 else 2
        valid, _sorted = struct.unpack_from('<QQ', d, base + 8)
        p = base + 24
        self.rows = {}
        for i in range(64):
            if valid >> i & 1:
                self.rows[i], = struct.unpack_from('<I', d, p)
                p += 4
        self.tab_idx = {i: (4 if self.rows.get(i, 0) >= 0x10000 else 2) for i in range(64)}
        self.coded_idx = {}
        for name, tags in CODED.items():
            bits = max(1, (len(tags) - 1).bit_length())
            biggest = max((self.rows.get(t, 0) for t in tags if t >= 0), default=0)
            self.coded_idx[name] = 4 if biggest >= (1 << (16 - bits)) else 2
        self.row_size, self.col_off = {}, {}
        for i in self.rows:
            offs, sz = [], 0
            for _fn, kind in SCHEMA[i][1]:
                offs.append(sz)
                sz += self._ksize(kind)
            self.row_size[i], self.col_off[i] = sz, offs
        self.tab_base = {}
        for i in sorted(self.rows):
            self.tab_base[i] = p
            p += self.row_size[i] * self.rows[i]

    def _ksize(self, kind):
        if kind == 'u1':
            return 1
        if kind == 'u2':
            return 2
        if kind == 'u4':
            return 4
        if kind == 'str':
            return self.s_str
        if kind == 'guid':
            return self.s_guid
        if kind == 'blob':
            return self.s_blob
        if kind[0] == 'tab':
            return self.tab_idx[kind[1]]
        return self.coded_idx[kind[1]]

    def read(self, tab, rid):
        """rid is 1-based, as in metadata tokens."""
        if tab not in self.rows or not (1 <= rid <= self.rows[tab]):
            return None
        cols = SCHEMA[tab][1]
        p = self.tab_base[tab] + (rid - 1) * self.row_size[tab]
        out = {}
        for (fn, kind), o in zip(cols, self.col_off[tab]):
            q, sz = p + o, self._ksize(kind)
            v = int.from_bytes(self.raw[q:q + sz], 'little')
            if isinstance(kind, tuple) and kind[0] == 'coded':
                tags = CODED[kind[1]]
                bits = max(1, (len(tags) - 1).bit_length())
                v = (tags[v & ((1 << bits) - 1)], v >> bits)
            out[fn] = v
        return out

    def count(self, tab):
        return self.rows.get(tab, 0)
