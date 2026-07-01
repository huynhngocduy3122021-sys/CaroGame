using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;

public class UIAnimate : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public enum AnimationType
    {
        CardPopIn,
        ButtonInteractive,
        TextGlowPulse
    }

    public AnimationType animType = AnimationType.CardPopIn;
    
    private Vector3 originalScale;
    private CanvasGroup canvasGroup;
    private Coroutine activeCoroutine;

    private void Awake()
    {
        originalScale = transform.localScale;
        
        // Ensure CanvasGroup exists for alpha fade
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null && animType == AnimationType.CardPopIn)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        if (animType == AnimationType.CardPopIn)
        {
            StartCoroutine(AnimateCardPopIn());
        }
        else if (animType == AnimationType.TextGlowPulse)
        {
            StartCoroutine(AnimateTextPulse());
        }
    }

    private IEnumerator AnimateCardPopIn()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        transform.localScale = originalScale * 0.82f;

        float duration = 0.55f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // easeOutBack formula
            float c1 = 1.4f;
            float c3 = c1 + 1f;
            float ease = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            
            transform.localScale = originalScale * Mathf.Lerp(0.82f, 1f, ease);
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Clamp01(t * 1.6f);
            
            yield return null;
        }

        transform.localScale = originalScale;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    private IEnumerator AnimateTextPulse()
    {
        TextMeshProUGUI text = GetComponent<TextMeshProUGUI>();
        if (text == null) yield break;

        float duration = 2.2f;
        float elapsed = 0f;
        Color baseColor = text.color;

        while (true)
        {
            elapsed += Time.deltaTime;
            float t = (elapsed % duration) / duration;
            float pulse = (Mathf.Sin(t * 2f * Mathf.PI) + 1f) / 2f; // 0 to 1

            // Gently shift towards a bright white/glow color
            text.color = Color.Lerp(baseColor, new Color(1f, 0.9f, 0.6f, 1f), pulse * 0.25f);
            yield return null;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (animType != AnimationType.ButtonInteractive) return;
        ScaleTo(originalScale * 1.05f, 0.12f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (animType != AnimationType.ButtonInteractive) return;
        ScaleTo(originalScale, 0.12f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (animType != AnimationType.ButtonInteractive) return;
        ScaleTo(originalScale * 0.95f, 0.08f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (animType != AnimationType.ButtonInteractive) return;
        ScaleTo(originalScale * 1.05f, 0.08f);
    }

    private void ScaleTo(Vector3 targetScale, float duration)
    {
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(AnimateScale(targetScale, duration));
    }

    private IEnumerator AnimateScale(Vector3 targetScale, float duration)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float ease = t * (2f - t); // easeOutQuad
            transform.localScale = Vector3.Lerp(startScale, targetScale, ease);
            yield return null;
        }

        transform.localScale = targetScale;
    }
}
