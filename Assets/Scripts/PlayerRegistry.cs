using UnityEngine;

public static class PlayerRegistry
{
    public static Transform Player { get; private set; }

    // Called once when the player object initialises
    public static void Register(Transform player) => Player = player;
    public static void Unregister()               => Player = null;
}