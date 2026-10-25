using System;
using UnityEngine;

public struct DamageContext : IDisposable
{
    public static GameObject CurrentAttacker { get; private set; }

    public DamageContext(GameObject attacker)
    {
        CurrentAttacker = attacker;
    }

    public void Dispose()
    {
        CurrentAttacker = null;
    }
}