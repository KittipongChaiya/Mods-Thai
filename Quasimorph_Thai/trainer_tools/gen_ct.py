"""Generate Quasimorph-v1.0.3-1.CT from the validated 1.0.3 offset map."""
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
-- capture : assembler line(s) run at method entry, e.g. 'mov [pPerk],rcx'
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
                       'mov [pWeapon],rcx',
                       'WeaponComponent entry hook; RCX = this.\n'
                       'CurrentAmmo is per-item; the record fields are shared by every\n'
                       'weapon of that type.'),
    color=ORANGE,
    children=[
        val('CurrentAmmo', '2 Bytes', 'pWeapon', '48', signed=True),
        val('MagazineCapacity', '4 Bytes', 'pWeapon', '28', 'BC'),
        val('ReloadDuration', '4 Bytes', 'pWeapon', '28', 'B8'),
        val('Range', '4 Bytes', 'pWeapon', '28', 'B0'),
        val('Falloff', 'Float', 'pWeapon', '28', 'B4'),
        val('ThrowRange', '4 Bytes', 'pWeapon', '28', 'C8'),
        val('BonusAccuracy', 'Float', 'pWeapon', '28', 'DC'),
    ]))

# --- Weapon (ship) via ItemSlot -> item -> records[0]
groups.append(entry(
    'Weapon - Ship (right click a weapon in cargo to populate)',
    script=hook_script('hkShipSlot',
                       'MGSC.ScreenWithShipCargo:DragControllerShowContextMenuCallback',
                       'mov [pShipSlot],rdx',
                       'RDX = ItemSlot. Chain: ItemSlot+128 = item, item+38 = _records,\n'
                       'list+10 = array, array+20 = records[0].\n'
                       'Only meaningful when the clicked item really is a weapon.'),
    color=ORANGE,
    children=[
        val('Item StackCount', '2 Bytes', 'pShipSlot', '128', '20', signed=True),
        val('Range', '4 Bytes', 'pShipSlot', '128', '38', '10', '20', 'B0'),
        val('Falloff', 'Float', 'pShipSlot', '128', '38', '10', '20', 'B4'),
        val('ReloadDuration', '4 Bytes', 'pShipSlot', '128', '38', '10', '20', 'B8'),
        val('MagazineCapacity', '4 Bytes', 'pShipSlot', '128', '38', '10', '20', 'BC'),
        val('Price', 'Float', 'pShipSlot', '128', '38', '10', '20', '2C'),
        val('Weight', 'Float', 'pShipSlot', '128', '38', '10', '20', '30'),
    ]))

# --- Items
groups.append(entry(
    'Items (right click an item to populate)',
    script=hook_script('hkItem', 'MGSC.PickupItem:get_IsStackable',
                       'mov [pItem],rcx',
                       'PickupItem entry hook; RCX = this.\n'
                       '+40 = _stackable component, +48 = _usable component.'),
    color=ORANGE,
    children=[
        val('StackCount', '2 Bytes', 'pItem', '40', '10', signed=True),
        val('MaxCount', '2 Bytes', 'pItem', '40', '12', signed=True),
        val('&lt;CurrentUsageValue&gt;', '4 Bytes', 'pItem', '48', '18'),
        val('&lt;MaxUsageValue&gt;', '4 Bytes', 'pItem', '48', '10'),
        val('&lt;UsageCost&gt;', '4 Bytes', 'pItem', '48', '14'),
        val('&lt;CurrentMaxUsageValue&gt;', '4 Bytes', 'pItem', '48', '1C'),
    ]))

# --- Backpack
groups.append(entry(
    'Backpack',
    script=hook_script('hkInv', 'MGSC.Inventory:ResizeBackpack',
                       'mov [pInventory],rcx',
                       'Inventory entry hook; RCX = this.\n'
                       'Original credit: Pekar of fearlessrevolution.com'),
    color=ORANGE,
    children=[
        val('BackpackMode', '4 Bytes', 'pInventory', 'B4',
            dropdown='0:Normal\n1:Endless\n'),
        val('ItemsWeight', 'Float', 'pInventory', '108'),
        val('AccessMask', '4 Bytes', 'pInventory', 'B0'),
    ]))

# --- Durability
groups.append(entry(
    'Durability (mouse over an item to populate)',
    script=hook_script('hkBreak', 'MGSC.BreakableItemComponent:get_Durability',
                       'mov [pBreakable],rcx',
                       'BreakableItemComponent entry hook; RCX = this.'),
    color=ORANGE,
    children=[
        val('Current %', 'Float', 'pBreakable', '10'),
        val('Max Penalty %', 'Float', 'pBreakable', '14'),
        val('Max Durability', '4 Bytes', 'pBreakable', '18'),
        val('Min Durability After Repair', '4 Bytes', 'pBreakable', '1C'),
        val('&lt;Unbreakable&gt; On=1', 'Byte', 'pBreakable', '20'),
    ]))

# --- Player stats
groups.append(entry(
    'Player Stats (move around to populate)',
    script=hook_script('hkStarve', 'MGSC.StarvationEffect:set_CurrentLevel',
                       'mov [pStarve],rcx',
                       'StarvationEffect entry hook; RCX = this.\n'
                       'Chain: +18 = Creature, +40 = CreatureData,\n'
                       '       CreatureData+88 = HealthInfo, +98 = Inventory.'),
    color=ORANGE,
    children=[
        val('Health', '4 Bytes', 'pStarve', '18', '40', '88', '2C'),
        val('Health Max', '4 Bytes', 'pStarve', '18', '40', '88', '24'),
        val('Health Min', '4 Bytes', 'pStarve', '18', '40', '88', '20'),
        val('Invulnerability On=1', 'Byte', 'pStarve', '18', '40', '88', '30'),
        val('Hunger', '4 Bytes', 'pStarve', '48'),
        val('Hunger Max', '4 Bytes', 'pStarve', '4C'),
        val('Hunger Regen', 'Float', 'pStarve', '58'),
        val('Weight', 'Float', 'pStarve', '18', '40', '98', '108'),
        val('BaseHealth', '4 Bytes', 'pStarve', '18', '40', '68'),
        val('BaseActionPoints', '4 Bytes', 'pStarve', '18', '40', '6C'),
        val('BaseLosLevel', '4 Bytes', 'pStarve', '18', '40', '70'),
        val('BaseMeleeAccuracy', 'Float', 'pStarve', '18', '40', '74'),
        val('BaseRangeAccuracy', 'Float', 'pStarve', '18', '40', '78'),
        val('BaseDodge', 'Float', 'pStarve', '18', '40', '7C'),
        val('IsInfiniteAmmo On=1', 'Byte', 'pStarve', '18', '14C'),
    ]))

# --- Perks
groups.append(entry(
    'Perks / XP (gain XP to populate)',
    script=hook_script('hkPerk', 'MGSC.Perk:AddExp', 'mov [pPerk],rcx',
                       'Perk entry hook; RCX = this.'),
    color=ORANGE,
    children=[
        val('CurrentExp', '4 Bytes', 'pPerk', '34'),
        val('ExpPerAction', '4 Bytes', 'pPerk', '38'),
        val('MaxExp', '4 Bytes', 'pPerk', '3C'),
    ]))

# --- Quasimorphosis
groups.append(entry(
    'QuasiLvL (move around to populate)',
    script=hook_script('hkQmorph', 'MGSC.QmorphosController:ProcessActionPoint',
                       'mov [pQmorphos],rcx',
                       'QmorphosController entry hook; RCX = this.\n'
                       'Chain: +30 = RaidMetadata, RaidMetadata+40 = MissionWinCondition.'),
    color=ORANGE,
    children=[
        val('QMorphosLevel', '4 Bytes', 'pQmorphos', '30', '2C'),
        val('QMorphosMinLevel', '4 Bytes', 'pQmorphos', '30', '34'),
        val('TurnNumber', '4 Bytes', 'pQmorphos', '30', '30'),
        val('IsBaronAllowed', 'Byte', 'pQmorphos', '30', '50'),
        val('IsGlobalJammed', 'Byte', 'pQmorphos', '30', '51'),
        val('EvacuationInProgress', 'Byte', 'pQmorphos', '30', '40', '31'),
        val('EvacuationBlocked', 'Byte', 'pQmorphos', '30', '40', '32'),
        val('EvacuationByItem', 'Byte', 'pQmorphos', '30', '40', '33'),
        val('EvacuationFlee', 'Byte', 'pQmorphos', '30', '40', '34'),
        val('EvacuationCompleted', 'Byte', 'pQmorphos', '30', '98'),
    ]))

# --- Travel
groups.append(entry(
    'Travel (start a flight to populate)',
    script=hook_script('hkTravel', 'MGSC.TravelSystem:ProcessSpaceshipTravel',
                       'push rax\\r\\nmov rax,[rsp+40]\\r\\nmov [pTravel],rax\\r\\npop rax',
                       'Static method: travelData is parameter 7, so at the entry it\n'
                       'lives at [rsp+38] (return address + 20h shadow space + 2 slots).\n'
                       'RAX is saved/restored, hence [rsp+40] inside the hook.'),
    color=ORANGE,
    children=[
        val('TravelHoursDuration', 'Double', 'pTravel', '90'),
        val('FlightTime', 'Float', 'pTravel', '48'),
        val('InitialTravelDistance', 'Float', 'pTravel', '78'),
        val('TravelFinalOrbitT', 'Double', 'pTravel', '30'),
        val('BramfaturaCounter', '4 Bytes', 'pTravel', '1C'),
        val('CanTravel', 'Byte', 'pTravel', '2C'),
    ]))

# --- Factions
FACTION_FIELDS = [
    ('Power', '4 Bytes', '18'),
    ('CurrentTechLevel', '4 Bytes', '1C'),
    ('TechExp', 'Float', '20'),
    ('BasePower', '4 Bytes', '24'),
    ('PlayerReputation', 'Float', '28'),
    ('PlayerTradePoints', '4 Bytes', '2C'),
    ('AllTimeTradingPoints', '4 Bytes', '30'),
]
faction_children = [val('Faction count', '4 Bytes', 'pFactions', '18', '18')]
for i in range(8):
    slot = '%X' % (0x20 + 8 * i)
    faction_children.append(entry(
        'Faction [%d]' % i, group_header=True,
        children=[val(fn, ft, 'pFactions', '18', '10', slot, off)
                  for fn, ft, off in FACTION_FIELDS]))

groups.append(entry(
    'Faction Stats (buy or sell anything to populate)',
    script=hook_script('hkBuy', 'MGSC.TradeSystem:BuyStationItems',
                       'mov [pFactions],rdx',
                       'Static method; RDX = Factions.\n'
                       'Chain: Factions+18 = List<Faction>, list+10 = array,\n'
                       '       array+20+8*i = Faction[i].'),
    color=ORANGE, children=faction_children))

groups.append(entry(
    'Faction Stats - also capture on Sell',
    script=hook_script('hkSell', 'MGSC.TradeSystem:SellItems',
                       'mov [pFactions],rdx',
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
print('wrote %s (%d bytes, %d entries)' % (OUT, len(doc), _id[0]))
