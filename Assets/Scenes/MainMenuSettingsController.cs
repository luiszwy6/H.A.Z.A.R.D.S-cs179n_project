using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MainMenuSettingsController : MonoBehaviour
{
    private const string VolumeKey = "Settings.Volume";
    private const string MusicEnabledKey = "Settings.MusicEnabled";

    [Header("Settings Art")]
    [SerializeField] private Sprite settingsBackgroundSprite;
    [SerializeField] private Sprite musicOffSprite;
    [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite volume0Sprite;
    [SerializeField] private Sprite volume33Sprite;
    [SerializeField] private Sprite volume67Sprite;
    [SerializeField] private Sprite volume100Sprite;

    private Canvas canvas;
    private GameObject settingsPanel;
    private Image musicStateImage;
    private Image volumeStateImage;
    private int volumeStep;
    private bool musicEnabled;

    private static bool SavedMusicEnabled => PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;
    private static float SavedVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));

    private void Awake()
    {
        ApplyAudioSettings(SavedMusicEnabled, SavedVolume);
    }

    private void Start()
    {
        canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
            return;

        musicEnabled = SavedMusicEnabled;
        volumeStep = VolumeToStep(SavedVolume);

        CreateSettingsPanel();
        WireSettingsButtons();
        RefreshStateImages();
        SaveAndApply();
    }

    private void WireSettingsButtons()
    {
        bool foundSettingsButton = false;
        Button[] buttons = canvas.GetComponentsInChildren<Button>(true);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];

            if (button == null || !LooksLikeSettingsButton(button))
                continue;

            button.onClick.RemoveListener(ShowSettings);
            button.onClick.AddListener(ShowSettings);
            foundSettingsButton = true;
        }

        if (!foundSettingsButton)
            CreateFallbackSettingsClickArea();
    }

    private bool LooksLikeSettingsButton(Button button)
    {
        if (button.name.ToLowerInvariant().Contains("setting"))
            return true;

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        return text != null && text.text.ToLowerInvariant().Contains("setting");
    }

    private void CreateFallbackSettingsClickArea()
    {
        GameObject buttonObject = CreateTransparentHitArea(
            "SettingsClickArea",
            canvas.transform,
            new Vector2(-1f, -83f),
            new Vector2(800f, 80f)
        );

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.onClick.AddListener(ShowSettings);
    }

    private void CreateSettingsPanel()
    {
        settingsPanel = CreateUiObject("SettingsPanel", canvas.transform);
        settingsPanel.SetActive(false);

        RectTransform panelRect = settingsPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image background = settingsPanel.AddComponent<Image>();
        background.sprite = settingsBackgroundSprite;
        background.color = Color.white;
        background.preserveAspect = true;
        background.raycastTarget = true;

        volumeStateImage = CreateStateImage(
            "VolumeStateImage",
            settingsPanel.transform,
            new Vector2(0f, -58f),
            new Vector2(1060f, 354f)
        );

        musicStateImage = CreateStateImage(
            "MusicStateImage",
            settingsPanel.transform,
            new Vector2(0f, -276f),
            new Vector2(1060f, 354f)
        );

        SettingsVolumeHitArea volumeHitArea = CreateTransparentHitArea(
            "VolumeHitArea",
            settingsPanel.transform,
            new Vector2(0f, -82f),
            new Vector2(900f, 150f)
        ).AddComponent<SettingsVolumeHitArea>();
        volumeHitArea.Initialize(this);

        Button musicButton = CreateTransparentHitArea(
            "MusicHitArea",
            settingsPanel.transform,
            new Vector2(260f, -276f),
            new Vector2(360f, 150f)
        ).AddComponent<Button>();
        musicButton.targetGraphic = musicButton.GetComponent<Image>();
        musicButton.onClick.AddListener(ToggleMusic);

        Button backButton = CreateTransparentHitArea(
            "BackHitArea",
            settingsPanel.transform,
            new Vector2(-515f, 315f),
            new Vector2(260f, 140f)
        ).AddComponent<Button>();
        backButton.targetGraphic = backButton.GetComponent<Image>();
        backButton.onClick.AddListener(HideSettings);
    }

    private Image CreateStateImage(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject imageObject = CreateUiObject(name, parent);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = imageObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private GameObject CreateTransparentHitArea(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject hitArea = CreateUiObject(name, parent);
        RectTransform rect = hitArea.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = hitArea.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
        return hitArea;
    }

    public void SetVolumeFromNormalizedPosition(float normalizedPosition)
    {
        volumeStep = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(normalizedPosition) * 3f), 0, 3);
        RefreshStateImages();
        SaveAndApply();
    }

    private void ToggleMusic()
    {
        musicEnabled = !musicEnabled;
        RefreshStateImages();
        SaveAndApply();
    }

    private void ShowSettings()
    {
        settingsPanel.SetActive(true);
        settingsPanel.transform.SetAsLastSibling();
    }

    public void HideSettings()
    {
        settingsPanel.SetActive(false);
    }

    private void RefreshStateImages()
    {
        if (musicStateImage != null)
            musicStateImage.sprite = musicEnabled ? musicOnSprite : musicOffSprite;

        if (volumeStateImage == null)
            return;

        switch (volumeStep)
        {
            case 0:
                volumeStateImage.sprite = volume0Sprite;
                break;
            case 1:
                volumeStateImage.sprite = volume33Sprite;
                break;
            case 2:
                volumeStateImage.sprite = volume67Sprite;
                break;
            default:
                volumeStateImage.sprite = volume100Sprite;
                break;
        }
    }

    private void SaveAndApply()
    {
        float volume = StepToVolume(volumeStep);
        PlayerPrefs.SetInt(MusicEnabledKey, musicEnabled ? 1 : 0);
        PlayerPrefs.SetFloat(VolumeKey, volume);
        PlayerPrefs.Save();
        ApplyAudioSettings(musicEnabled, volume);
    }

    private static void ApplyAudioSettings(bool enabled, float volume)
    {
        AudioListener.volume = enabled ? Mathf.Clamp01(volume) : 0f;
    }

    private static int VolumeToStep(float volume)
    {
        return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(volume) * 3f), 0, 3);
    }

    private static float StepToVolume(int step)
    {
        return Mathf.Clamp01(Mathf.Clamp(step, 0, 3) / 3f);
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }
}

public class SettingsVolumeHitArea : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private MainMenuSettingsController controller;
    private RectTransform rectTransform;

    public void Initialize(MainMenuSettingsController owner)
    {
        controller = owner;
        rectTransform = GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        SetVolumeFromPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        SetVolumeFromPointer(eventData);
    }

    private void SetVolumeFromPointer(PointerEventData eventData)
    {
        if (controller == null || rectTransform == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        Rect rect = rectTransform.rect;
        float normalized = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        controller.SetVolumeFromNormalizedPosition(normalized);
    }
}
