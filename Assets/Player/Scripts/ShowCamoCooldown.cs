using UnityEngine;

public class ShowCamoCooldown : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CamoAbility camoAbility;

    [Header("Display")]
    [SerializeField] private bool showCamo = true;
    [SerializeField] private Vector2 offsetFromBottomLeft = new Vector2(500f, 18f); // Offset to the right of the bullet time box
    [SerializeField] private float boxWidth = 240f;
    [SerializeField] private float boxHeight = 64f;
    [SerializeField] private int fontSize = 22;
    [SerializeField] private string title = "Camouflage";

    private GUIStyle boxStyle;
    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle activeTextStyle;
    private GUIStyle cooldownTextStyle;
    private bool stylesReady;

    private void Reset()
    {
        if (camoAbility == null)
            camoAbility = GetComponent<CamoAbility>();
        if (camoAbility == null)
            camoAbility = GetComponentInParent<CamoAbility>();
    }

    private void Awake()
    {
        if (camoAbility == null)
            camoAbility = GetComponent<CamoAbility>();
        if (camoAbility == null)
            camoAbility = GetComponentInParent<CamoAbility>();
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
        
        activeTextStyle = new GUIStyle(textStyle);
        activeTextStyle.normal.textColor = Color.green;

        cooldownTextStyle = new GUIStyle(textStyle);
        cooldownTextStyle.normal.textColor = Color.red;

        stylesReady = true;
    }

    private void OnGUI()
    {
        if (!showCamo || camoAbility == null)
            return;

        EnsureStyles();

        float cooldown = camoAbility.CurrentCooldown;
        bool isActive = camoAbility.IsActive;

        Rect boxRect = new Rect(
            offsetFromBottomLeft.x,
            Screen.height - offsetFromBottomLeft.y - boxHeight,
            boxWidth,
            boxHeight
        );

        GUI.Box(boxRect, GUIContent.none, boxStyle);

        Rect titleRect = new Rect(boxRect.x + 8f, boxRect.y + 4f, boxRect.width - 16f, 24f);
        Rect resourceRect = new Rect(boxRect.x + 8f, boxRect.y + 30f, boxRect.width - 16f, 28f);

        GUI.Label(titleRect, title, titleStyle);

        GUIStyle currentTextStyle = textStyle;
        string displayText = "READY";

        if (isActive)
        {
            currentTextStyle = activeTextStyle;
            displayText = "ACTIVE";
        }
        else if (cooldown > 0f)
        {
            currentTextStyle = cooldownTextStyle;
            displayText = $"CD: {cooldown:F1}s";
        }

        GUI.Label(resourceRect, displayText, currentTextStyle);
    }
}
