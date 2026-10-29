#!/usr/bin/env python3
"""Fixes the agent's primary-attack handling (attacks fired once per episode, bows never fired), adds per-hero TensorBoard stats,
and strips per-attack Debug.Log spam. Run from the repo root AFTER the earlier apply_*.py scripts:
    python apply_attack_fix.py
CRLF/LF tolerant, idempotent, all-or-nothing per file."""
import re, sys, pathlib

ROOT = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else '.')
if not (ROOT / 'Assets').is_dir():
    sys.exit("Run this from the repo root (the folder containing Assets/).")
applied, skipped, already, notes = [], [], [], []

def edit(path, subs, marker):
    p = ROOT / path
    if not p.exists():
        skipped.append((path, "file not found")); return
    raw = p.read_bytes().decode('utf-8'); crlf = '\r\n' in raw; s = raw.replace('\r\n', '\n')
    if re.search(marker, s):
        already.append(path); return
    for kind, a, b in subs:
        if kind == 's':
            if s.count(a) != 1:
                skipped.append((path, f"expected 1 match, found {s.count(a)}: {a.strip()[:70]!r}")); return
            s = s.replace(a, b)
        else:
            s, n = re.subn(a, lambda m: b, s, count=1, flags=re.S)
            if n < 1:
                skipped.append((path, f"no regex match: {a[:70]!r}")); return
    if crlf: s = s.replace('\n', '\r\n')
    p.write_bytes(s.encode('utf-8')); applied.append(path)

NEW_COMBAT = '''private void ExecuteCombatSlot(int actionIndex)
    {
        Ability targetAbility = null;
        switch (actionIndex)
        {
            case 0:
                ReleasePrimary();
                return;
            case 1:
                HoldPrimary();
                return;
            case 2: targetAbility = _abilityManager.primaryAbility; break;
            case 3: targetAbility = _abilityManager.secondaryAbility; break;
            case 4: targetAbility = _abilityManager.mobilityAbility; break;
            case 5: targetAbility = _abilityManager.ultimateAbility; break;
        }

        if (targetAbility == null) return;
        if (targetAbility.IsOnCooldown() || _abilityManager.HasActiveExclusiveAbility()) return;

        _epAbilityUses++;
        targetAbility.Use();
    }

    // The primary attack is driven exactly like a player's mouse button: press starts it (Attack.Update then fires it
    // every cooldown), release stops it. Calling Use() directly left isExecuting stuck on, so plain attacks only fired
    // once per episode and the bows (which fire on release) never fired at all.
    private void HoldPrimary()
    {
        Attack atk = _abilityManager.primaryAttack;
        if (atk == null || _abilityManager.HasActiveExclusiveAbility()) return;
        if (_primaryHeld) return;                                    // keep holding; re-pressing would reset a bow's charge timer
        if (GetChargeTime(atk) > 0f && atk.IsOnCooldown()) return;   // charge weapons ignore presses while cooling down

        atk.OnPress();
        _primaryHeld = true;
        _primaryHeldSince = Time.time;
        _epAttackUses++;
    }

    private void ReleasePrimary()
    {
        if (!_primaryHeld) return;
        _primaryHeld = false;
        if (_abilityManager.primaryAttack != null) _abilityManager.primaryAttack.OnRelease();
    }

    private void UpdatePrimaryHold()
    {
        if (!_primaryHeld) return;

        Attack atk = _abilityManager.primaryAttack;
        if (atk == null)
        {
            _primaryHeld = false;
            return;
        }

        float chargeTime = GetChargeTime(atk);
        if (chargeTime > 0f && Time.time - _primaryHeldSince >= chargeTime)
            ReleasePrimary();   // fully charged: loose the arrow
    }

    private static float GetChargeTime(Attack atk)
    {
        if (atk is GoldenBow golden) return golden.maxChargeTime;
        if (atk is CrescentBow crescent) return crescent.maxChargeTime;
        return 0f;
    }
'''

edit('Assets/Scripts/RLEnemyCombatAgent.cs', [
 ('s', 'private TrainingArena _arena;', 'private TrainingArena _arena;\n    private bool _primaryHeld;\n    private float _primaryHeldSince;'),
 ('re', r'private void ExecuteCombatSlot\(int actionIndex\)\s*\{.*?\n    \}\n', NEW_COMBAT),
 ('re', r'private void Update\(\)\s*\{', 'private void Update()\n    {\n        UpdatePrimaryHold();'),
 ('s', '_abilityManager.InterruptAllAbilities();',
       '_abilityManager.InterruptAllAbilities();\n            if (_abilityManager.primaryAttack != null) _abilityManager.primaryAttack.isHolding = false;\n            _primaryHeld = false;'),
 ('s', 'bool unavailable = ability == null || ability.IsOnCooldown() || exclusiveLocked;',
       'bool unavailable = ability == null || exclusiveLocked || (actionIndex != 1 && ability.IsOnCooldown());   // slot 1 = "hold attack": stays selectable while cooling down'),
], marker=r'private void HoldPrimary')

edit('Assets/Scripts/RLEnemyCombatAgent.cs', [
 ('s', 'stats.Add("Combat/EpisodeDecisions", d);',
       'stats.Add("Combat/EpisodeDecisions", d);\n            string hero = GetHeroName();\n            stats.Add($"Hero/{hero}/DamageDealt", _epDamageDealt);\n            stats.Add($"Hero/{hero}/Killed", _epKilled);\n            stats.Add($"Hero/{hero}/AttackUses", _epAttackUses);'),
 ('s', 'private void TrackDecision()',
       '''private string GetHeroName()
    {
        if (TryGetComponent<HeroManager>(out var heroManager) && heroManager.currentHero != null)
            return heroManager.currentHero.name.Replace("(Clone)", "").Trim();
        return "NoHero";
    }

    private void TrackDecision()'''),
], marker=r'GetHeroName')

# ---- strip single-line Debug.Log statements (not Warning/Error) from abilities + the agent ----
def strip_logs(path):
    raw = path.read_bytes().decode('utf-8'); crlf = '\r\n' in raw
    lines = raw.replace('\r\n', '\n').split('\n'); out = []; removed = 0
    for ln in lines:
        if re.match(r'^\s*Debug\.Log\(.*\);\s*$', ln):
            prev = next((x.strip() for x in reversed(out) if x.strip()), '{')
            if prev.endswith(('{', '}', ';', ':')) or prev.startswith('//'):
                removed += 1; continue
        out.append(ln)
    if removed:
        s = '\n'.join(out)
        path.write_bytes((s.replace('\n', '\r\n') if crlf else s).encode('utf-8'))
    return removed

total = 0; files = 0
targets = list((ROOT / 'Assets/Scripts/Abilities').rglob('*.cs')) + [ROOT / 'Assets/Scripts/RLEnemyCombatAgent.cs']
for f in targets:
    if f.exists():
        n = strip_logs(f)
        if n: total += n; files += 1

print(f"\nApplied: {len(applied)}, already done: {len(already)}")
for a in applied: print("  +", a)
print(f"Removed {total} Debug.Log lines from {files} files (warnings/errors kept).")
if skipped:
    print(f"SKIPPED: {len(skipped)} (left untouched):")
    for p, why in skipped: print(f"  - {p}: {why}")