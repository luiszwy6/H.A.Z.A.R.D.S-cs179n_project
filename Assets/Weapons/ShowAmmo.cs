using UnityEngine;

public class ShowAmmo : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerWeaponSlots playerWeaponSlots;
    [SerializeField] private WeaponAmmoSettings fallbackAmmoSettings;

    [Header("Display")]
    [SerializeField] private bool showAmmo = true;
    [SerializeField] private Vector2 offsetFromBottomLeft = new Vector2(18f, 18f);
    [SerializeField] private float boxWidth = 220f;
    [SerializeField] private float boxHeight = 64f;
    [SerializeField] private int fontSize = 22;
    [SerializeField] private string title = "Ammo";

    private GUIStyle boxStyle;
    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private bool stylesReady;

    private void Reset()
    {
        if (playerWeaponSlots == null)
            playerWeaponSlots = GetComponent<PlayerWeaponSlots>();

        if (fallbackAmmoSettings == null)
            fallbackAmmoSettings = GetComponentInChildren<WeaponAmmoSettings>(true);
    }

    private void Awake()
    {
        if (playerWeaponSlots == null)
            playerWeaponSlots = GetComponent<PlayerWeaponSlots>();
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.alignment = TextAnchor.UpperLeft;
        boxStyle.padding = new RectOffset(10, 10, 8, 8);

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = fontSize;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.UpperLeft;

        textStyle = new GUIStyle(GUI.skin.label);
        textStyle.fontSize = fontSize;
        textStyle.alignment = TextAnchor.UpperLeft;

        stylesReady = true;
    }

    private void OnGUI()
    {
        if (!showAmmo)
            return;

        WeaponAmmoSettings ammoSettings = GetCurrentAmmoSettings();

        if (ammoSettings == null)
            return;

        EnsureStyles();

        int currentMag = ammoSettings.CurrentAmmoInMagazine;
        int magSize = ammoSettings.MagazineSize;
        int reserveAmmo = ammoSettings.CurrentReserveAmmo;

        Rect boxRect = new Rect(
            offsetFromBottomLeft.x,
            Screen.height - offsetFromBottomLeft.y - boxHeight,
            boxWidth,
            boxHeight
        );

        GUI.Box(boxRect, GUIContent.none, boxStyle);

        Rect titleRect = new Rect(boxRect.x + 8f, boxRect.y + 4f, boxRect.width - 16f, 24f);
        Rect ammoRect = new Rect(boxRect.x + 8f, boxRect.y + 30f, boxRect.width - 16f, 28f);

        GUI.Label(titleRect, title, titleStyle);
        GUI.Label(ammoRect, $"{currentMag}/{magSize}   |   {reserveAmmo}", textStyle);
    }

    private WeaponAmmoSettings GetCurrentAmmoSettings()
    {
        if (playerWeaponSlots != null && playerWeaponSlots.CurrentAmmoSettings != null)
            return playerWeaponSlots.CurrentAmmoSettings;

        return fallbackAmmoSettings;
    }
}