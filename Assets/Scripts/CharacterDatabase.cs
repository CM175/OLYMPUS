using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Olympus/Character Database")]
public class CharacterDatabase : ScriptableObject
{
    [Header("Roster")]
    [SerializeField] private List<Hero> characterPrefabs = new List<Hero>();

    public int CharacterCount => characterPrefabs.Count;

    public Hero GetHeroAtIndex(int index)
    {
        if (index < 0 || index >= characterPrefabs.Count) return null;
        return characterPrefabs[index];
    }

    public Hero GetHeroByName(string name)
    {
        return characterPrefabs.Find(h => h.heroName.Equals(name, System.StringComparison.OrdinalIgnoreCase));
    }

    // Expose read-only list for the UI setup loop
    public IReadOnlyList<Hero> GetAllHeroes() => characterPrefabs;
}