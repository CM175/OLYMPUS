using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UICharacterSelectMenu : MonoBehaviour
{
    [Header("Management Bindings")]
    [SerializeField] private CharacterSelectManager selectManager;

    [Header("UI Element Prefabs & Layouts")]
    [SerializeField] private GameObject menuRootPanel;       // The Selection Menu Root Panel
    [SerializeField] private GameObject gameplayHUDRoot;     // ADD THIS: Drag your HUD Canvas/Panel here
    [SerializeField] private Transform GridButtonContainer;
    [SerializeField] private Button characterButtonPrefab;

    [Header("Preview Details Screen")]
    [SerializeField] private TextMeshProUGUI previewNameText;
    [SerializeField] private TextMeshProUGUI previewRoleText;
    [SerializeField] private Button lockInButton;

    private void Start()
    {
        if (selectManager == null) return;

        selectManager.OnPreviewHeroChanged += UpdatePreviewDisplay;
        selectManager.OnHeroConfirmed += HandleSelectionLockedIn;

        if (lockInButton != null)
            lockInButton.onClick.AddListener(selectManager.ConfirmSelection);

        // Frame 1 Setup: Make sure the character selector is open and gameplay HUD is hidden
        if (menuRootPanel != null) menuRootPanel.SetActive(true);
        if (gameplayHUDRoot != null) gameplayHUDRoot.SetActive(false);

        PopulateSelectionGrid();
    }

    private void OnDestroy()
    {
        if (selectManager != null)
        {
            selectManager.OnPreviewHeroChanged -= UpdatePreviewDisplay;
            selectManager.OnHeroConfirmed -= HandleSelectionLockedIn;
        }
    }

    private void PopulateSelectionGrid()
    {
        foreach (Transform child in GridButtonContainer)
        {
            Destroy(child.gameObject);
        }

        var roster = selectManager.GetDatabase().GetAllHeroes();
        for (int i = 0; i < roster.Count; i++)
        {
            int index = i;
            Hero hero = roster[i];

            Button btnInstance = Instantiate(characterButtonPrefab, GridButtonContainer);
            TMP_Text buttonText = btnInstance.GetComponentInChildren<TMP_Text>();
            if (buttonText != null) buttonText.text = hero.heroName;

            btnInstance.onClick.AddListener(() => selectManager.SelectCharacter(index));
        }
    }

    private void UpdatePreviewDisplay(Hero highlightedHero)
    {
        if (highlightedHero == null) return;
        if (previewNameText != null) previewNameText.text = highlightedHero.heroName;
        if (previewRoleText != null) previewRoleText.text = $"Role: {highlightedHero.role}";
        if (lockInButton != null) lockInButton.interactable = true;
    }

    private void HandleSelectionLockedIn(Hero finalHero)
    {
        // 1. Hide the Selection Menu Panel
        if (menuRootPanel != null) menuRootPanel.SetActive(false);

        // 2. Show the real gameplay HUD (Crosshair, Health, etc.)
        if (gameplayHUDRoot != null) gameplayHUDRoot.SetActive(true);
        
    }
}