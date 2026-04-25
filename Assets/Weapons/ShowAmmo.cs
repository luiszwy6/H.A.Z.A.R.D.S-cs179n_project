using UnityEngine;

public class ShowAmmo : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private AssaultRifleAmmoSettings assaultRifleAmmoSettings;

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
        if (assaultRifleAmmoSettings == null)
            assaultRifleAmmoSettings = FindFirstObjectByType<AssaultRifleAmmoSettings>();
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
        if (!showAmmo || assaultRifleAmmoSettings == null)
            return;

        EnsureStyles();

        int currentMag = assaultRifleAmmoSettings.CurrentAmmoInMagazine;
        int magSize = assaultRifleAmmoSettings.MagazineSize;
        int reserveAmmo = assaultRifleAmmoSettings.CurrentReserveAmmo;

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
}