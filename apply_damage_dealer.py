#!/usr/bin/env python3
"""Migrates OLYMPUS damage calls to DamageDealer. Run from the repo root:
    python apply_damage_dealer.py
Line-ending tolerant (CRLF/LF). Each file is edited all-or-nothing; files that
don't match are skipped and reported, never half-edited."""
import re, sys, pathlib

ROOT = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else '.')
if not (ROOT / 'Assets').is_dir():
    sys.exit("Run this from the repo root (the folder containing Assets/).")

applied, skipped, already = [], [], []

def edit(path, subs):
    p = ROOT / 'Assets' / path
    if not p.exists():
        skipped.append((path, "file not found")); return
    raw = p.read_bytes().decode('utf-8')
    crlf = '\r\n' in raw
    s = raw.replace('\r\n', '\n')
    if re.search(r'\bDeal(OverTime)?\(|DamageDealer\.|float duration, GameObject attacker|applied on impact by BaseProjectile', s):
        already.append(path); return
    for kind, a, b in subs:
        if kind == 's':
            n = s.count(a)
            if n != 1:
                skipped.append((path, f"expected 1 match, found {n}: {a.strip()[:70]!r}")); return
            s = s.replace(a, b)
        else:
            s, n = re.subn(a, b, s, flags=re.S)
            if n < 1:
                skipped.append((path, f"no regex match: {a[:70]!r}")); return
    if crlf: s = s.replace('\n', '\r\n')
    p.write_bytes(s.encode('utf-8'))
    applied.append(path)

# ---------- new helper ----------
dd = '''using UnityEngine;

/// <summary>
/// Single entry point for all outgoing damage. Always passes the attacker
/// explicitly and applies the attacker's PlayerStats.DamageMultiplier once,
/// so abilities never rely on ambient state or scale damage themselves.
/// </summary>
public static class DamageDealer
{
    public static int Scale(GameObject attacker, int baseDamage)
    {
        if (attacker != null && attacker.transform.root.TryGetComponent(out PlayerStats stats))
            return Mathf.RoundToInt(baseDamage * stats.DamageMultiplier);
        return baseDamage;
    }

    public static void Deal(GameObject attacker, IDamageable target, int baseDamage)
    {
        if (target == null) return;
        target.TakeDamage(Scale(attacker, baseDamage), attacker);
    }

    public static void DealOverTime(GameObject attacker, IDamageable target, int baseTotalDamage, float duration)
    {
        if (target == null) return;
        target.DamageOverTime(Scale(attacker, baseTotalDamage), duration, attacker);
    }
}
'''
ddp = ROOT / 'Assets/Scripts/DamageDealer.cs'
ab = ROOT / 'Assets/Scripts/Ability.cs'
use_crlf = ab.exists() and b'\r\n' in ab.read_bytes()
if ddp.exists() and b'class DamageDealer' in ddp.read_bytes():
    already.append('Scripts/DamageDealer.cs')
else:
    ddp.write_bytes((dd.replace('\n', '\r\n') if use_crlf else dd).encode('utf-8'))
    applied.append('Scripts/DamageDealer.cs')

# ---------- interface + health + proxy ----------
edit('Scripts/IDamageable.cs', [('s',
 '    void DamageOverTime(int totalDamage, float duration);',
 '    void DamageOverTime(int totalDamage, float duration);\n    void DamageOverTime(int totalDamage, float duration, GameObject attacker);')])

def health(path, default_attacker):
    edit(path, [
     ('re', r'public void DamageOverTime\(int totalDamage, float duration\)\s*\{\s*if \(IsDead\) return;\s*if \(duration <= 0f\)\s*\{\s*TakeDamage\(totalDamage\);\s*return;\s*\}\s*StartCoroutine\(SmoothDamageRoutine\(totalDamage, duration\)\);\s*\}',
      'public void DamageOverTime(int totalDamage, float duration) => DamageOverTime(totalDamage, duration, %s);\n\n'
      '    public void DamageOverTime(int totalDamage, float duration, GameObject attacker)\n    {\n        if (IsDead) return;\n        if (duration <= 0f)\n        {\n            TakeDamage(totalDamage, attacker);\n            return;\n        }\n        StartCoroutine(SmoothDamageRoutine(totalDamage, duration, attacker));\n    }' % default_attacker),
     ('s', 'private IEnumerator SmoothDamageRoutine(int totalDamage, float duration)',
           'private IEnumerator SmoothDamageRoutine(int totalDamage, float duration, GameObject attacker)'),
     ('s', 'TakeDamage(damageAmount);', 'TakeDamage(damageAmount, attacker);'),
     ('s', 'TakeDamage(Mathf.RoundToInt(accumulatedDamage));', 'TakeDamage(Mathf.RoundToInt(accumulatedDamage), attacker);'),
    ])
health('Scripts/PlayerHealth.cs', 'DamageContext.CurrentAttacker')
health('Scripts/EnemyHealth.cs', 'null')

edit('Scripts/DamageProxy.cs', [('s',
 'public void DamageOverTime(int totalDamage, float duration) => _parentDamageable?.DamageOverTime(totalDamage, duration);',
 'public void DamageOverTime(int totalDamage, float duration) => _parentDamageable?.DamageOverTime(totalDamage, duration);\n    public void DamageOverTime(int totalDamage, float duration, GameObject attacker) => _parentDamageable?.DamageOverTime(totalDamage, duration, attacker);')])

edit('Scripts/Ability.cs', [('s',
 '    public virtual void OnPress() {}',
 '''    protected GameObject Owner => transform.root.gameObject;

    // Outgoing damage always goes through these so the attacker is explicit
    // and PlayerStats.DamageMultiplier is applied exactly once.
    protected void Deal(IDamageable target, int baseDamage) => DamageDealer.Deal(Owner, target, baseDamage);
    protected void DealOverTime(IDamageable target, int baseTotalDamage, float duration) => DamageDealer.DealOverTime(Owner, target, baseTotalDamage, duration);

    public virtual void OnPress() {}''')])

# ---------- projectiles ----------
edit('Scripts/BaseProjectile.cs', [('s', 'hit?.TakeDamage(damage, attackerRoot);', 'DamageDealer.Deal(attackerRoot, hit, damage);')])
edit('Abilities/Apollo/SolarArrow.cs', [('s', 'health?.DamageOverTime(damage, damageOverTimeDuration);', 'DamageDealer.DealOverTime(attackerRoot, health, damage, damageOverTimeDuration);')])
edit('Abilities/Achillies/SpearThrow.cs', [
 ('re', r'\s*PlayerStats stats = root\.GetComponent<PlayerStats>\(\);\s*int finalDamage = [^;]*;\s*projectile\.damage = finalDamage;', '\n            projectile.damage = damage; // multiplier applied on impact by BaseProjectile')])

# ---------- simple one-line swaps ----------
simple = {
 'Abilities/Apollo/HymnOfRadiance.cs': [('dmg?.DamageOverTime(damageAmount, healOrDamageDuration);', 'DealOverTime(dmg, damageAmount, healOrDamageDuration);')],
 'Abilities/Apollo/ChariotOfTheSun.cs': [('targetHealth.TakeDamage(damagePerTick);', 'Deal(targetHealth, damagePerTick);')],
 'Abilities/Apollo/SolarBlind.cs': [('victimRoot.GetComponent<IDamageable>()?.TakeDamage(minorBurnDamage);', 'Deal(victimRoot.GetComponent<IDamageable>(), minorBurnDamage);')],
 'Abilities/Minotaur/GroundSlam.cs': [('damageable.TakeDamage(slamDamage);', 'Deal(damageable, slamDamage);')],
 'Abilities/Minotaur/AxeSwing.cs': [('targetDamage?.TakeDamage(damage, _rootTransform.gameObject);', 'Deal(targetDamage, damage);')],
 'Abilities/Minotaur/Charge.cs': [
    ('pinnedDamageable?.TakeDamage(impactDamage);', 'Deal(pinnedDamageable, impactDamage);'),
    ('damageable.TakeDamage((int)(impactDamage * 0.5f));', 'Deal(damageable, (int)(impactDamage * 0.5f));'),
    ('target?.TakeDamage(pinWallCrushDamage);', 'Deal(target, pinWallCrushDamage);')],
 'Abilities/Poseidon/UndertowSurge.cs': [('dmg?.TakeDamage(emergeDamage);', 'Deal(dmg, emergeDamage);')],
 'Abilities/Zeus/SkyCharge.cs': [('target.TakeDamage(dashDamage);', 'Deal(target, dashDamage);')],
 'Abilities/Hermes/BlinkDash.cs': [('target.TakeDamage(dashDamage);', 'Deal(target, dashDamage);')],
 'Abilities/Zeus/LightningBolt.cs': [
    ('target.DamageOverTime(damage, damageOverTimeDuration);', 'DealOverTime(target, damage, damageOverTimeDuration);'),
    ('chainDamageable?.DamageOverTime((int)(damage * 0.5f), damageOverTimeDuration);', 'DealOverTime(chainDamageable, (int)(damage * 0.5f), damageOverTimeDuration);')],
 'Abilities/Zeus/OlympianWrath.cs': [('target.TakeDamage(damagePerStrike, attackerRoot);', 'Deal(target, damagePerStrike);')],
 'Abilities/Zeus/BarrierDamage.cs': [('target?.TakeDamage(damage, attackerTransform.gameObject);', 'DamageDealer.Deal(attackerTransform.gameObject, target, damage);')],
 'Scripts/EnemyAttack.cs': [('_playerDamageable.TakeDamage(damage);', 'DamageDealer.Deal(gameObject, _playerDamageable, damage);')],
}
for f, subs in simple.items():
    edit(f, [('s', a, b) for a, b in subs])

# ---------- sites that scaled locally: drop local scaling ----------
edit('Abilities/Achillies/SpearThrust.cs', [
 ('re', r'\n\s*PlayerStats stats = root\.GetComponent<PlayerStats>\(\);', ''),
 ('re', r'int finalDamage = stats != null\s*\?[^;]*;\s*target\.TakeDamage\(finalDamage\);', 'Deal(target, damage);')])
edit('Abilities/Achillies/ShieldBash.cs', [
 ('re', r'\n\s*PlayerStats stats = transform\.root\.GetComponent<PlayerStats>\(\);', ''),
 ('re', r'\s*// DamageMultiplier still applied here[^\n]*\n\s*// Charge awarding[^\n]*\n\s*int finalDamage = stats != null\s*\?[^;]*;\s*target\.TakeDamage\(finalDamage\);', '\n            Deal(target, damage);')])
edit('Abilities/Minotaur/LabyrinthDomain.cs', [
 ('re', r'int finalDamage = playerStats != null\s*\?[^;]*;\s*', ''),
 ('s', 'dmg?.TakeDamage(finalDamage);', 'Deal(dmg, slamDamage);')])
edit('Abilities/Poseidon/ReleaseTheKraken.cs', [
 ('re', r'int finalDmg = playerStats != null\s*\?[^;]*;\s*', ''),
 ('s', 'dmg?.TakeDamage(finalDmg);', 'Deal(dmg, eruptionDamage);')])
edit('Abilities/Poseidon/TridentStrike.cs', [
 ('re', r'PlayerStats stats = _rootTransform\.GetComponent<PlayerStats>\(\);\s*int finalDamage = [^;]*;\s*target\.TakeDamage\(finalDamage, _rootTransform\.gameObject\);', 'Deal(target, damage);')])
edit('Abilities/Hermes/WingedDaggers.cs', [
 ('re', r'\n\s*PlayerStats stats = root\.GetComponent<PlayerStats>\(\);', ''),
 ('re', r'int finalDamage = damage;\s*if \(stats != null\)\s*\{[^}]*\}', 'int finalDamage = damage;'),
 ('s', 'target.TakeDamage(finalDamage, root.gameObject);', 'Deal(target, finalDamage);')])
edit('Abilities/Hermes/SonicBoom.cs', [
 ('re', r'int finalDmg = playerStats != null\s*\?[^;]*;\s*', ''),
 ('s', 'dmg?.TakeDamage(finalDmg);', 'Deal(dmg, baseDamage);')])

print(f"\nApplied: {len(applied)} files, already migrated: {len(already)}")
if skipped:
    print(f"SKIPPED: {len(skipped)} files (left untouched):")
    for p, why in skipped: print(f"  - {p}: {why}")
    print("\nSend me this list and I'll adapt the edits to your versions.")
else:
    print("No files skipped.")
