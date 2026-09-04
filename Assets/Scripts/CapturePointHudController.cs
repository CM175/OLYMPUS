using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CapturePointHudController : MonoBehaviour
{
    [Header("Source Reference")]
    [SerializeField] private CapturePoint point;

    [Header("Score Displays")]
    [SerializeField] private TextMeshProUGUI teamAScoreText;
    [SerializeField] private TextMeshProUGUI teamBScoreText;

    [Header("Center Capture Dial")]
    [SerializeField] private Image captureFillImage;      // Image set to 'Filled' -> 'Radial 360'
    [SerializeField] private Image pointCenterIcon;       // Core icon showing current point owner
    [SerializeField] private GameObject contestedIndicator; // "CONTESTED" badge

    [Header("Overtime Elements")]
    [SerializeField] private GameObject overtimeContainer;
    [SerializeField] private Slider overtimeSlider;       // Drains 1 to 0 during overtime

    [Header("Team Palettes")]
    [SerializeField] private Color teamAColor = new(0.2f, 0.6f, 1f);
    [SerializeField] private Color teamBColor = new(0.9f, 0.2f, 0.2f);
    [SerializeField] private Color neutralColor = Color.white;

    private void Update()
    {
        if (point == null) return;

        UpdateScores();
        UpdateCaptureMeter();
        UpdateOvertimeDisplay();
    }

    private void UpdateScores()
    {
        teamAScoreText.text = $"{Mathf.FloorToInt(point.teamAScore)}%";
        teamBScoreText.text = $"{Mathf.FloorToInt(point.teamBScore)}%";
    }

    private void UpdateCaptureMeter()
    {
        // Contested toggle
        if (contestedIndicator != null)
        {
            contestedIndicator.SetActive(point.isContested);
        }

        //Center ownership icon color
        pointCenterIcon.color = point.controllingTeam switch
        {
            PointOwner.TeamA => teamAColor,
            PointOwner.TeamB => teamBColor,
            _ => neutralColor
        };

        // Radial fill progress
        captureFillImage.fillAmount = point.captureProgress / 100f;
        captureFillImage.color = (point.currentChallenger == Team.TeamA) ? teamAColor : teamBColor;
    }

    private void UpdateOvertimeDisplay()
    {
        if (overtimeContainer == null) return;

        overtimeContainer.SetActive(point.isOvertime);

        if (point.isOvertime && overtimeSlider != null)
        {
            // You can expose overtimeFuseTimer / overtimeFuseDuration as public floats on CapturePoint
            // overtimeSlider.value = point.overtimeFuseTimer / point.overtimeFuseDuration;
        }
    }
}