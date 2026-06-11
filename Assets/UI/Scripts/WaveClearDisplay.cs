using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows a "WAVE CLEAR" message at the center of the screen for a short time
/// after each wave is cleared. Attach alongside SquadWaveHUD or anywhere in the scene.
/// </summary>
public class WaveClearDisplay : MonoBehaviour
{
    [Header("Generator")]
    [SerializeField] private EnemySquadGenerator generator;

    [Header("UI")]
    [Tooltip("TMP_Text placed in the center of the screen (screen-space canvas).")]
    [SerializeField] private TMP_Text waveClearText;

    [Header("Settings")]
    [SerializeField] private string displayText  = "WAVE CLEAR";
    [Min(0.1f)] [SerializeField] private float visibleDuration = 2f;
    [Min(0f)]   [SerializeField] private float fadeInDuration  = 0.3f;
    [Min(0f)]   [SerializeField] private float fadeOutDuration = 0.5f;

    private Coroutine displayRoutine;

    private void Awake()
    {
        if (generator == null)
            generator = FindFirstObjectByType<EnemySquadGenerator>();

        if (waveClearText != null)
        {
            SetAlpha(0f);
            waveClearText.text = displayText;
        }
    }

    private void OnEnable()
    {
        if (generator != null)
            generator.OnWaveCleared += HandleWaveCleared;
    }

    private void OnDisable()
    {
        if (generator != null)
            generator.OnWaveCleared -= HandleWaveCleared;
    }

    private void HandleWaveCleared(int _, float __)
    {
        ShowWithText(displayText);
    }

    public void ShowWithText(string text, System.Action onComplete = null)
    {
        if (waveClearText == null)
            return;

        if (displayRoutine != null)
            StopCoroutine(displayRoutine);

        displayRoutine = StartCoroutine(DisplayRoutine(text, onComplete));
    }

    private IEnumerator DisplayRoutine(string text, System.Action onComplete = null)
    {
        waveClearText.text = text;

        yield return FadeTo(1f, fadeInDuration);

        float held = 0f;
        while (held < visibleDuration)
        {
            held += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return FadeTo(0f, fadeOutDuration);

        displayRoutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(targetAlpha);
            yield break;
        }

        float startAlpha = waveClearText.color.a;
        float elapsed    = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void SetAlpha(float a)
    {
        if (waveClearText == null) return;
        Color c = waveClearText.color;
        c.a = a;
        waveClearText.color = c;
    }
}
