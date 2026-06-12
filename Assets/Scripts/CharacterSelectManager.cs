using UnityEngine;
using System;

public class CharacterSelectManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private CharacterDatabase characterDatabase;
    [SerializeField] private HeroManager heroManager;
    [SerializeField] private MatchManager matchManager;

    [Header("Settings")]
    [SerializeField] private bool loadDefaultOnStart = false;
    [SerializeField] private int defaultHeroIndex = 0;

    // Triggered when a player highlights or switch clicks an option in the UI menu
    public event Action<Hero> OnPreviewHeroChanged;
    // Triggered when the choice is locked in
    public event Action<Hero> OnHeroConfirmed;

    private int _currentSelectedIndex = -1;

    private void Start()
    {
        if (heroManager == null)
            heroManager = FindFirstObjectByType<HeroManager>();

        if (loadDefaultOnStart && characterDatabase != null && characterDatabase.CharacterCount > 0)
        {
            SelectCharacter(defaultHeroIndex);
            ConfirmSelection();
        }
    }

    public void SelectCharacter(int index)
    {
        if (characterDatabase == null) return;
        
        Hero selectedPrefab = characterDatabase.GetHeroAtIndex(index);
        if (selectedPrefab == null) return;

        _currentSelectedIndex = index;
        OnPreviewHeroChanged?.Invoke(selectedPrefab);
        Debug.Log($"[CharSelect] Highlighted: {selectedPrefab.heroName}");
    }

    public void ConfirmSelection()
    {
        if (_currentSelectedIndex == -1 || characterDatabase == null)
        {
            Debug.LogWarning("[CharSelect] Cannot confirm selection: No hero highlighted.");
            return;
        }

        Hero confirmedPrefab = characterDatabase.GetHeroAtIndex(_currentSelectedIndex);
        if (confirmedPrefab != null && heroManager != null)
        {
            // Inject selection dynamically into your runtime gameplay framework
            matchManager.SpawnHumanPlayer(confirmedPrefab);
            OnHeroConfirmed?.Invoke(confirmedPrefab);
            
            Debug.Log($"[CharSelect] Successfully confirmed and loaded: {confirmedPrefab.heroName}");
        }
    }
    
    public CharacterDatabase GetDatabase() => characterDatabase;
}