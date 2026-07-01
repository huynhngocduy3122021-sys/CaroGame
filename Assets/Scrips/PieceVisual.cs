using UnityEngine;
using System.Collections;

public class PieceVisual : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(AnimateSpawn());
    }

    private IEnumerator AnimateSpawn()
    {
        Vector3 finalScale = transform.localScale;
        transform.localScale = Vector3.zero;

        float duration = 0.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // easeOutBack formula: springy pop-in
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            float ease = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);

            transform.localScale = finalScale * Mathf.Clamp(ease, 0f, 1.2f);
            yield return null;
        }

        transform.localScale = finalScale;
    }
}
