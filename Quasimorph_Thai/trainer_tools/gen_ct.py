"""Generate Quasimorph-v1.0.3-1.CT from the validated 1.0.3 offset map."""
import json
import os
from xml.sax.saxutils import escape

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, os.pardir, 'Quasimorph-v1.0.3-1.CT'))

_id = [0]


def nid():
    _id[0] += 1
    return _id[0]


def esc(s):
    return escape(s)


# ---------------------------------------------------------------- Lua runtime
LUA_RUNTIME = r'''
qmSaved = qmSaved or {}

function qmInsnSize(addr)
  if getInstructionSize ~= nil then
    local ok, s = pcall(getInstructionSize, addr)
    if ok and type(s) == 'number' and s > 0 then return s end
  end
  -- fallback: count the byte column of disassemble() output
  local ok, line = pcall(disassemble, addr)
  if not ok or line == nil then return nil end
  local bytes = line:match('^[^-]+%-%s*([0-9A-Fa-f ]+)%s*%-')
  if bytes == nil then return nil end
  local n = 0
  for _ in bytes:gmatch('%x%x') do n = n + 1 end
  if n < 1 then return nil end
  return n
end

function qmStolen(addr)
  local n = 0
  while n < 5 do
    local s = qmInsnSize(addr + n)
    if s == nil then return nil end
    n = n + s
    if n > 40 then return nil end
  end
  return n
end

function qmBytesToDb(t)
  local s = 'db'
  for i = 1, #t do s = s .. string.format(' %02X', t[i]) end
  return s
end

-- id      : unique name for this hook (used for the code cave symbol)
-- method  : mono symbol, e.g. 'MGSC.Perk:AddExp'
-- capture : assembler run at method entry; stores the object into a pointer
--           symbol. Always routed through RAX, because x86-64 encodes a store
--           to an absolute 64-bit address only for the accumulator.
function qmHook(id, method, capture)
  local addr = getAddress(method)
  if addr == nil or addr == 0 then
    error('Quasimorph: cannot resolve ' .. method ..
          '\nEnable "1) Activate me" first, and load a save so the method is JIT compiled.')
  end
  local first = readBytes(addr, 1, false)
  if first == 0xE8 or first == 0xE9 or first == 0xEB then
    error('Quasimorph: ' .. method .. ' starts with a relative branch - unsafe to hook.')
  end
  local len = qmStolen(addr)
  if len == nil then
    error('Quasimorph: cannot measure the prologue of ' .. method)
  end
  local orig = readBytes(addr, len, true)
  if orig == nil then
    error('Quasimorph: cannot read the prologue of ' .. method)
  end
  qmSaved[id] = { addr = addr, len = len, bytes = orig }
  local pad = ''
  if len > 5 then
    pad = 'db' .. string.rep(' 90', len - 5)
  end
  return string.format(
    'alloc(%s_code,2048,%X)\n' ..
    'label(%s_ret)\n' ..
    '%s_code:\n' ..
    '%s\n' ..
    '%s\n' ..
    'jmp %s_ret\n' ..
    '%X:\n' ..
    'jmp %s_code\n' ..
    '%s\n' ..
    '%s_ret:\n',
    id, addr, id, id, capture, qmBytesToDb(orig), id, addr, id, pad, id)
end

function qmUnhook(id)
  local s = qmSaved[id]
  if s == nil then return '' end
  qmSaved[id] = nil
  return string.format('%X:\n%s\ndealloc(%s_code)\n', s.addr, qmBytesToDb(s.bytes), id)
end
'''

POINTERS = ['pWeapon', 'pShipSlot', 'pItem', 'pInventory', 'pBreakable',
            'pStarve', 'pPerk', 'pQmorphos', 'pTravel', 'pFactions']

ACTIVATE = '[ENABLE]\n{$lua}\nif syntaxcheck then return end\nOpenProcess("Quasimorph.exe")\nmono_initialize()\nLaunchMonoDataCollector()\n' \
    + LUA_RUNTIME + '{$asm}\n' \
    + '\n'.join('alloc(%s,8)' % p for p in POINTERS) + '\n' \
    + '\n'.join('registersymbol(%s)' % p for p in POINTERS) + '\n\n' \
    + '\n'.join('%s:\ndq 0' % p for p in POINTERS) + '\n\n' \
    + '[DISABLE]\n' \
    + '\n'.join('unregistersymbol(%s)' % p for p in POINTERS) + '\n' \
    + '\n'.join('dealloc(%s)' % p for p in POINTERS) + '\n'


def hook_script(hid, method, capture, note):
    return ('[ENABLE]\n{$lua}\nif syntaxcheck then return end\n'
            "return qmHook('%s', '%s', '%s')\n"
            '\n[DISABLE]\n{$lua}\nif syntaxcheck then return end\n'
            "return qmUnhook('%s')\n\n{%s}\n" % (hid, method, capture, hid, note))


def cap(sym, src):
    """Assembler that stores `src` into pointer symbol `sym` at method entry.

    x86-64 can only encode a store to an absolute 64-bit address from RAX
    (`mov [moffs64],rax`), and CE's assembler does not fall back to
    RIP-relative addressing, so every capture is routed through RAX.
    RAX is caller-scratch and holds no incoming argument, but it is saved
    and restored anyway so the displaced prologue sees untouched state.
    """
    return ('push rax\\r\\n'
            'mov rax,%s\\r\\n'
            'mov [%s],rax\\r\\n'
            'pop rax' % (src, sym))

# ------------------------------------------------------------- offset sources
# offsets.json is dumped from the live process with mono_class_enumFields, so
# it is what the JIT actually uses. monolayout's 'gc' model reproduces it
# exactly (486/486 fields over 24 classes) and is used to cross-check.
HERE = os.path.dirname(os.path.abspath(__file__))
with open(os.path.join(HERE, 'offsets.json'), encoding='utf-8') as _f:
    OFFSETS = json.load(_f)

# Mono runtime layout constants
LIST_ITEMS = '10'      # List<T>._items
ARRAY_DATA = 0x20      # MonoArray: header + bounds + max_length


def OFF(cls, field):
    """Runtime offset of a field, as an uppercase hex string."""
    try:
        return '%X' % OFFSETS[cls][field]
    except KeyError:
        raise SystemExit('gen_ct: %s::%s is not in offsets.json - re-run the '
                         'Mono dump against the current game build' % (cls, field))


def PROP(cls, name):
    """Offset of an auto-property's backing field."""
    return OFF(cls, '<%s>k__BackingField' % name)


def ELEM(i):
    """Offset of element i inside a Mono array's data area."""
    return '%X' % (ARRAY_DATA + 8 * i)


def cross_check():
    """Fail the build if the offline model disagrees with the runtime dump."""
    try:
        from monolayout import load_universe
    except ImportError:
        return 'skipped (monolayout unavailable)'
    game = os.environ.get('QM_GAME', DEFAULT_GAME)
    if not os.path.isdir(game):
        return 'skipped (game not found at %s)' % game
    U = load_universe(game)
    checked = bad = 0
    for cls, fields in OFFSETS.items():
        hit = U.find('MGSC', cls)
        if not hit:
            continue
        computed, _size, _notes = U.layout(hit[0], hit[1], 'gc')
        for fname, want in fields.items():
            got = computed.get(fname)
            if got is None:
                continue
            checked += 1
            if got != want:
                bad += 1
                print('  MISMATCH %s::%s dump=0x%X model=0x%X'
                      % (cls, fname, want, got))
    if bad:
        raise SystemExit('gen_ct: %d offset mismatches - refusing to build' % bad)
    return 'ok (%d fields agree with the gc model)' % checked


DEFAULT_GAME = r'C:\Users\Administrator\Desktop\Quasimorph.v1.0.3\game'


# ------------------------------------------------------------------ XML build
def entry(desc, script=None, vtype=None, address=None, applied=None,
          children=None, color=None, signed=None, dropdown=None,
          group_header=False):
    """applied = offsets in APPLICATION order; CE stores them reversed."""
    x = ['<CheatEntry>', '<ID>%d</ID>' % nid(),
         '<Description>"%s"</Description>' % esc(desc)]
    if children:
        x.append('<Options moHideChildren="1" moDeactivateChildrenAsWell="1" '
                 'moAllowManualCollapseAndExpand="1"/>')
    if color:
        x.append('<Color>%s</Color>' % color)
    if group_header:
        x.append('<GroupHeader>1</GroupHeader>')
    if script is not None:
        x.append('<VariableType>Auto Assembler Script</VariableType>')
        x.append('<AssemblerScript>%s</AssemblerScript>' % esc(script))
    elif vtype is not None:
        if dropdown:
            x.append('<DropDownList ReadOnly="1" DisplayValueAsItem="1">%s</DropDownList>'
                     % esc(dropdown))
        if signed is not None:
            x.append('<ShowAsSigned>%d</ShowAsSigned>' % (1 if signed else 0))
        x.append('<VariableType>%s</VariableType>' % vtype)
        x.append('<Address>%s</Address>' % address)
        if applied:
            x.append('<Offsets>')
            for o in reversed(applied):
                x.append('<Offset>%s</Offset>' % o)
            x.append('</Offsets>')
    if children:
        x.append('<CheatEntries>')
        x.extend(children)
        x.append('</CheatEntries>')
    x.append('</CheatEntry>')
    return '\n'.join(x)


def val(desc, vtype, base, *applied, **kw):
    return entry(desc, vtype=vtype, address=base, applied=list(applied), **kw)


ORANGE, BLUE, LBLUE = 'FF8000', '0000FF', '79BCFF'

# ------------------------------------------------------------------- the tree
groups = []

# --- Weapon (mission)
groups.append(entry(
    'Weapon - Mission (shoot a weapon to populate)',
    script=hook_script('hkWeapon', 'MGSC.WeaponComponent:SpendAmmo',
                       cap('pWeapon', 'rcx'),
                       'WeaponComponent entry hook; RCX = this.\n'
                       'CurrentAmmo is per-item; the record fields are shared by every\n'
                       'weapon of that type.'),
    color=ORANGE,
    children=[
        val('CurrentAmmo', '2 Bytes', 'pWeapon',
            PROP('WeaponComponent', 'CurrentAmmo'), signed=True),
        val('MagazineCapacity', '4 Bytes', 'pWeapon',
            OFF('WeaponComponent', '_weaponRecord'), PROP('WeaponRecord', 'MagazineCapacity')),
        val('ReloadDuration', '4 Bytes', 'pWeapon',
            OFF('WeaponComponent', '_weaponRecord'), PROP('WeaponRecord', 'ReloadDuration')),
        val('Range', '4 Bytes', 'pWeapon',
            OFF('WeaponComponent', '_weaponRecord'), PROP('WeaponRecord', 'Range')),
        val('Falloff', 'Float', 'pWeapon',
            OFF('WeaponComponent', '_weaponRecord'), PROP('WeaponRecord', 'Falloff')),
        val('ThrowRange', '4 Bytes', 'pWeapon',
            OFF('WeaponComponent', '_weaponRecord'), PROP('WeaponRecord', 'ThrowRange')),
        val('BonusAccuracy', 'Float', 'pWeapon',
            OFF('WeaponComponent', '_weaponRecord'), PROP('WeaponRecord', 'BonusAccuracy')),
    ]))

# --- Weapon (ship): ItemSlot -> item -> _records[0]
REC = [PROP('ItemSlot', 'Item'), OFF('PickupItem', '_records'), LIST_ITEMS, ELEM(0)]
groups.append(entry(
    'Weapon - Ship (right click a weapon in cargo to populate)',
    script=hook_script('hkShipSlot',
                       'MGSC.ScreenWithShipCargo:DragControllerShowContextMenuCallback',
                       cap('pShipSlot', 'rdx'),
                       'RDX = ItemSlot. Chain: ItemSlot -> Item -> _records -> [0].\n'
                       'Only meaningful when the clicked item really is a weapon.'),
    color=ORANGE,
    children=[
        val('Item StackCount', '2 Bytes', 'pShipSlot',
            PROP('ItemSlot', 'Item'), PROP('PickupItem', 'StackCount'), signed=True),
        val('Range', '4 Bytes', 'pShipSlot', *(REC + [PROP('WeaponRecord', 'Range')])),
        val('Falloff', 'Float', 'pShipSlot', *(REC + [PROP('WeaponRecord', 'Falloff')])),
        val('ReloadDuration', '4 Bytes', 'pShipSlot',
            *(REC + [PROP('WeaponRecord', 'ReloadDuration')])),
        val('MagazineCapacity', '4 Bytes', 'pShipSlot',
            *(REC + [PROP('WeaponRecord', 'MagazineCapacity')])),
        val('Price', 'Float', 'pShipSlot', *(REC + [PROP('WeaponRecord', 'Price')])),
        val('Weight', 'Float', 'pShipSlot', *(REC + [PROP('WeaponRecord', 'Weight')])),
    ]))

# --- Items
groups.append(entry(
    'Items (right click an item to populate)',
    script=hook_script('hkItem', 'MGSC.PickupItem:get_IsStackable',
                       cap('pItem', 'rcx'),
                       'PickupItem entry hook; RCX = this.'),
    color=ORANGE,
    children=[
        val('StackCount', '2 Bytes', 'pItem', OFF('PickupItem', '_stackable'),
            PROP('StackableItemComponent', 'Count'), signed=True),
        val('MaxCount', '2 Bytes', 'pItem', OFF('PickupItem', '_stackable'),
            PROP('StackableItemComponent', 'Max'), signed=True),
        val('&lt;CurrentUsageValue&gt;', '4 Bytes', 'pItem', OFF('PickupItem', '_usable'),
            PROP('UsableItemComponent', 'CurrentUsageValue')),
        val('&lt;MaxUsageValue&gt;', '4 Bytes', 'pItem', OFF('PickupItem', '_usable'),
            PROP('UsableItemComponent', 'MaxUsageValue')),
        val('&lt;UsageCost&gt;', '4 Bytes', 'pItem', OFF('PickupItem', '_usable'),
            PROP('UsableItemComponent', 'UsageCost')),
        val('&lt;CurrentMaxUsageValue&gt;', '4 Bytes', 'pItem', OFF('PickupItem', '_usable'),
            PROP('UsableItemComponent', 'CurrentMaxUsageValue')),
    ]))

# --- Backpack
groups.append(entry(
    'Backpack',
    script=hook_script('hkInv', 'MGSC.Inventory:ResizeBackpack',
                       cap('pInventory', 'rcx'),
                       'Inventory entry hook; RCX = this.\n'
                       'Original credit: Pekar of fearlessrevolution.com'),
    color=ORANGE,
    children=[
        val('BackpackMode', '4 Bytes', 'pInventory', OFF('Inventory', '_backpackMode'),
            dropdown='0:Normal\n1:Endless\n'),
        val('ItemsWeight', 'Float', 'pInventory', PROP('Inventory', 'ItemsWeight')),
        val('AccessMask', '4 Bytes', 'pInventory', OFF('Inventory', 'AccessMask')),
    ]))

# --- Durability
groups.append(entry(
    'Durability (mouse over an item to populate)',
    script=hook_script('hkBreak', 'MGSC.BreakableItemComponent:get_Durability',
                       cap('pBreakable', 'rcx'),
                       'BreakableItemComponent entry hook; RCX = this.'),
    color=ORANGE,
    children=[
        val('Current %', 'Float', 'pBreakable', PROP('BreakableItemComponent', 'CurrentPercent')),
        val('Max Penalty %', 'Float', 'pBreakable',
            PROP('BreakableItemComponent', 'MaxPenaltyPercent')),
        val('Max Durability', '4 Bytes', 'pBreakable',
            PROP('BreakableItemComponent', 'MaxDurability')),
        val('Min Durability After Repair', '4 Bytes', 'pBreakable',
            PROP('BreakableItemComponent', 'MinDurabilityAfterRepair')),
        val('&lt;Unbreakable&gt; On=1', 'Byte', 'pBreakable',
            PROP('BreakableItemComponent', 'Unbreakable')),
    ]))

# --- Player stats
CRE = OFF('StarvationEffect', '_creature')
CD = [CRE, OFF('Creature', 'CreatureData')]
HP = CD + [OFF('CreatureData', 'Health')]
groups.append(entry(
    'Player Stats (move around to populate)',
    script=hook_script('hkStarve', 'MGSC.StarvationEffect:set_CurrentLevel',
                       cap('pStarve', 'rcx'),
                       'StarvationEffect entry hook; RCX = this.\n'
                       'Chain: _creature -> CreatureData -> Health / Inventory.'),
    color=ORANGE,
    children=[
        val('Health', '4 Bytes', 'pStarve', *(HP + [OFF('HealthInfo', '_value')])),
        val('Health Max', '4 Bytes', 'pStarve', *(HP + [OFF('HealthInfo', 'MaxValue')])),
        val('Health Min', '4 Bytes', 'pStarve', *(HP + [OFF('HealthInfo', 'MinValue')])),
        val('Invulnerability On=1', 'Byte', 'pStarve',
            *(HP + [OFF('HealthInfo', '_invulnerability')])),
        val('Hunger', '4 Bytes', 'pStarve', OFF('StarvationEffect', '_currentLevel')),
        val('Hunger Max', '4 Bytes', 'pStarve', PROP('StarvationEffect', 'MaxLevel')),
        val('Hunger Regen', 'Float', 'pStarve', PROP('StarvationEffect', 'Regen')),
        val('Weight', 'Float', 'pStarve',
            *(CD + [OFF('CreatureData', 'Inventory'), PROP('Inventory', 'ItemsWeight')])),
        val('BaseHealth', '4 Bytes', 'pStarve', *(CD + [OFF('CreatureData', 'BaseHealth')])),
        val('BaseActionPoints', '4 Bytes', 'pStarve',
            *(CD + [OFF('CreatureData', 'BaseActionPoints')])),
        val('BaseLosLevel', '4 Bytes', 'pStarve', *(CD + [OFF('CreatureData', 'BaseLosLevel')])),
        val('BaseMeleeAccuracy', 'Float', 'pStarve',
            *(CD + [OFF('CreatureData', 'BaseMeleeAccuracy')])),
        val('BaseRangeAccuracy', 'Float', 'pStarve',
            *(CD + [OFF('CreatureData', 'BaseRangeAccuracy')])),
        val('BaseDodge', 'Float', 'pStarve', *(CD + [OFF('CreatureData', 'BaseDodge')])),
        val('IsInfiniteAmmo On=1', 'Byte', 'pStarve', CRE, OFF('Creature', 'IsInfiniteAmmo')),
    ]))

# --- Perks
groups.append(entry(
    'Perks / XP (gain XP to populate)',
    script=hook_script('hkPerk', 'MGSC.Perk:AddExp', cap('pPerk', 'rcx'),
                       'Perk entry hook; RCX = this.'),
    color=ORANGE,
    children=[
        val('CurrentExp', '4 Bytes', 'pPerk', OFF('Perk', 'CurrentExp')),
        val('ExpPerAction', '4 Bytes', 'pPerk', OFF('Perk', 'ExpPerAction')),
        val('MaxExp', '4 Bytes', 'pPerk', OFF('Perk', 'MaxExp')),
    ]))

# --- Quasimorphosis
RAID = OFF('QmorphosController', '_raidMetadata')
WIN = [RAID, OFF('RaidMetadata', 'WinCondition')]
groups.append(entry(
    'QuasiLvL (move around to populate)',
    script=hook_script('hkQmorph', 'MGSC.QmorphosController:ProcessActionPoint',
                       cap('pQmorphos', 'rcx'),
                       'QmorphosController entry hook; RCX = this.\n'
                       'Chain: _raidMetadata -> WinCondition (MissionWinCondition).'),
    color=ORANGE,
    children=[
        val('QMorphosLevel', '4 Bytes', 'pQmorphos', RAID, OFF('RaidMetadata', 'QMorphosLevel')),
        val('QMorphosMinLevel', '4 Bytes', 'pQmorphos', RAID,
            OFF('RaidMetadata', 'QMorphosMinLevel')),
        val('TurnNumber', '4 Bytes', 'pQmorphos', RAID, OFF('RaidMetadata', 'TurnNumber')),
        val('IsBaronAllowed', 'Byte', 'pQmorphos', RAID, OFF('RaidMetadata', 'IsBaronAllowed')),
        val('IsGlobalJammed', 'Byte', 'pQmorphos', RAID, OFF('RaidMetadata', 'IsGlobalJammed')),
        val('EvacuationInProgress', 'Byte', 'pQmorphos',
            *(WIN + [OFF('MissionWinCondition', 'EvacuationInProgress')])),
        val('EvacuationBlocked', 'Byte', 'pQmorphos',
            *(WIN + [OFF('MissionWinCondition', 'EvacuationBlocked')])),
        val('EvacuationByItem', 'Byte', 'pQmorphos',
            *(WIN + [OFF('MissionWinCondition', 'EvacuationByItem')])),
        val('EvacuationFlee', 'Byte', 'pQmorphos',
            *(WIN + [OFF('MissionWinCondition', 'EvacuationFlee')])),
        val('EvacuationCompleted', 'Byte', 'pQmorphos', RAID,
            OFF('RaidMetadata', 'EvacuationCompleted')),
    ]))

# --- Travel
groups.append(entry(
    'Travel (start a flight to populate)',
    script=hook_script('hkTravel', 'MGSC.TravelSystem:ProcessSpaceshipTravel',
                       cap('pTravel', '[rsp+40]'),
                       'Static method: travelData is parameter 7, so at the entry it\n'
                       'lives at [rsp+38] (return address + 20h shadow space + 2 slots).\n'
                       'RAX is pushed first, hence [rsp+40] inside the hook.'),
    color=ORANGE,
    children=[
        val('TravelHoursDuration', 'Double', 'pTravel',
            OFF('TravelMetadata', 'TravelHoursDuration')),
        val('FlightTime', 'Float', 'pTravel', OFF('TravelMetadata', 'FlightTime')),
        val('InitialTravelDistance', 'Float', 'pTravel',
            OFF('TravelMetadata', 'InitialTravelDistance')),
        val('TravelFinalOrbitT', 'Double', 'pTravel',
            OFF('TravelMetadata', 'TravelFinalOrbitT')),
        val('BramfaturaCounter', '4 Bytes', 'pTravel',
            OFF('TravelMetadata', 'BramfaturaCounter')),
        val('CanTravel', 'Byte', 'pTravel', OFF('TravelMetadata', 'CanTravel')),
    ]))

# --- Factions: Factions.Values -> List<Faction> -> array -> [i]
FACTION_FIELDS = [
    ('Power', '4 Bytes', OFF('Faction', 'Power')),
    ('CurrentTechLevel', '4 Bytes', OFF('Faction', 'CurrentTechLevel')),
    ('TechExp', 'Float', OFF('Faction', 'TechExp')),
    ('BasePower', '4 Bytes', OFF('Faction', 'BasePower')),
    ('PlayerReputation', 'Float', OFF('Faction', 'PlayerReputation')),
    ('PlayerTradePoints', '4 Bytes', OFF('Faction', 'PlayerTradePoints')),
    ('AllTimeTradingPoints', '4 Bytes', OFF('Faction', 'AllTimeTradingPoints')),
]
VALUES = OFF('Factions', 'Values')
faction_children = [val('Faction count', '4 Bytes', 'pFactions', VALUES, '18')]
for i in range(8):
    faction_children.append(entry(
        'Faction [%d]' % i, group_header=True,
        children=[val(fn, ft, 'pFactions', VALUES, LIST_ITEMS, ELEM(i), off)
                  for fn, ft, off in FACTION_FIELDS]))

groups.append(entry(
    'Faction Stats (buy or sell anything to populate)',
    script=hook_script('hkBuy', 'MGSC.TradeSystem:BuyStationItems',
                       cap('pFactions', 'rdx'),
                       'Static method; RDX = Factions.\n'
                       'Chain: Values -> List._items -> array[i].'),
    color=ORANGE, children=faction_children))

groups.append(entry(
    'Faction Stats - also capture on Sell',
    script=hook_script('hkSell', 'MGSC.TradeSystem:SellItems',
                       cap('pFactions', 'rdx'),
                       'Second capture point for the same pFactions symbol,\n'
                       'so selling populates the Faction Stats tree as well.'),
    color=ORANGE))

# --- compact-view helper, kept from the 0.9.87 table
COMPACT = """[ENABLE]
LuaCall(function cycleFullCompact(sender,force) local state = not(compactmenuitem.Caption == 'Compact View Mode'); if force~=nil then state = not force end; compactmenuitem.Caption = state and 'Compact View Mode' or 'Full View Mode'; getMainForm().Splitter1.Visible = state; getMainForm().Panel4.Visible    = state; getMainForm().Panel5.Visible    = state; end; function addCompactMenu() if compactmenualreadyexists then return end; local parent = getMainForm().Menu.Items; compactmenuitem = createMenuItem(parent); parent.add(compactmenuitem); compactmenuitem.Caption = 'Compact View Mode'; compactmenuitem.OnClick = cycleFullCompact; compactmenualreadyexists = 'yes'; end; addCompactMenu(); cycleFullCompact(nil,true))

[DISABLE]
LuaCall(cycleFullCompact(nil,false))
"""

root = [
    entry('Full / Compact - Mode', script=COMPACT, color=LBLUE),
    entry('1)  Activate me ! Quasimorph 1.0.3', script=ACTIVATE, color=BLUE,
          children=groups),
]

doc = ('<?xml version="1.0" encoding="utf-8"?>\n'
       '<CheatTable CheatEngineTableVersion="46">\n'
       '  <CheatEntries>\n' + '\n'.join(root) + '\n  </CheatEntries>\n'
       '  <UserdefinedSymbols/>\n'
       '</CheatTable>\n')

os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
    f.write(doc)
print('offset cross-check: %s' % cross_check())
print('wrote %s (%d bytes, %d entries)' % (OUT, len(doc), _id[0]))
