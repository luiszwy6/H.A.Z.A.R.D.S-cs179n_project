using UnityEngine;

public class ShowBulletTime : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BulletTimeAbility bulletTimeAbility;

    [Header("Display")]
    [SerializeField] private bool showBulletTime = true;
    [SerializeField] private Vector2 offsetFromBottomLeft = new Vector2(250f, 18f); // Offset to the right of the ammo box
    [SerializeField] private float boxWidth = 240f;
    [SerializeField] private float boxHeight = 64f;
    [SerializeField] private int fontSize = 22;
    [SerializeField] private string title = "Bullet Time";

    private GUIStyle boxStyle;
    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle activeTextStyle;
    private bool stylesReady;

    private void Reset()
    {
        if (bulletTimeAbility == null)
            bulletTimeAbility = GetComponent<BulletTimeAbility>();
        if (bulletTimeAbility == null)
            bulletTimeAbility = GetComponentInParent<BulletTimeAbility>();
    }

    private void Awake()
    {
        if (bulletTimeAbility == null)
            bulletTimeAbility = GetComponent<BulletTimeAbility>();
        if (bulletTimeAbility == null)
            bulletTimeAbility = GetComponentInParent<BulletTimeAbility>();
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
        activeTextStyle.normal.textColor = Color.cyan;

        stylesReady = true;
    }

    private void OnGUI()
    {
        if (!showBulletTime || bulletTimeAbility == null)
            return;

        EnsureStyles();

        float current = bulletTimeAbility.CurrentResource;
        float max = bulletTimeAbility.maxResource;
        bool isActive = bulletTimeAbility.IsActive;

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

        GUIStyle currentTextStyle = isActive ? activeTextStyle : textStyle;
        
        GUI.Label(resourceRect, $"{current:F1} / {max:F1}{(isActive ? " [ACTIVE]" : "")}", currentTextStyle);
    }
}
