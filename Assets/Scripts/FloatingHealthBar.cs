using UnityEngine;
using UnityEngine.UI;

public class FloatingHealthBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image fillImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.2f, 0f);
    [SerializeField] private bool hideWhenFull = false;

    private Camera mainCamera;
    private Transform target;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    public void Initialize(Transform followTarget, Color barColor)
    {
        target = followTarget;
        if (fillImage != null)
        {
            fillImage.color = barColor;
        }
    }

    public void SetHealthRatio(float ratio, bool isDead)
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = Mathf.Clamp01(ratio);
        }

        if (canvasGroup != null)
        {
            bool shouldShow = !isDead && (!hideWhenFull || ratio < 0.999f);
            canvasGroup.alpha = shouldShow ? 1f : 0f;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        offset.y = GetAnchorPoint();
        // Follow character with vertical offset
        transform.position = target.position + offset;

        // Face the active camera cleanly
        if (mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }
    }


    private float GetAnchorPoint()
    {
        // Fallback: calculate top of the model's renderers if headPoint wasn't assigned
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            
            float anchor = bounds.max.y + 0.2f; // place slightly above the top of the model
            return anchor;
        }
        Debug.Log("FloatingHealthBar: No renderers found to determine anchor point. Using default offset.", this);
        return 2.2f;
    }
}