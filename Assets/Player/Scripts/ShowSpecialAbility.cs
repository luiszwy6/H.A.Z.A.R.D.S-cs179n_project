using UnityEngine;

public class ShowSpecialAbility : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private KillCount killCount;
    [SerializeField] private AR_SpecialAbility arAbility;
    [SerializeField] private SG_SpecialAbility sgAbility;

    [Header("Display")]
    [SerializeField] private bool show = true;
    [SerializeField] private Vector2 offsetFromBottomLeft = new Vector2(510f, 18f);
    [SerializeField] private float boxWidth = 220f;
    [SerializeField] private float boxHeight = 64f;
    [SerializeField] private int fontSize = 22;
    [SerializeField] private string title = "Special";

    private GUIStyle boxStyle;
    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle chargedStyle;
    private GUIStyle activeStyle;
    private bool stylesReady;

    private void Awake()
    {
        if (killCount == null)
            killCount = GetComponent<KillCount>();

        if (arAbility == null)
            arAbility = GetComponent<AR_SpecialAbility>();

        if (sgAbility == null)
            sgAbility = GetComponent<SG_SpecialAbility>();
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

        textStyle = new GUIStyle(GUI.skin.label);
        textStyle.fontSize = fontSize;

        chargedStyle = new GUIStyle(textStyle);
        chargedStyle.normal.textColor = Color.yellow;

        activeStyle = new GUIStyle(textStyle);
        activeStyle.normal.textColor = Color.cyan;

        stylesReady = true;
    }

    private void OnGUI()
    {
        if (!show || killCount == null)
            return;

        EnsureStyles();

        bool isActive = (arAbility != null && arAbility.IsActive) ||
                        (sgAbility != null && sgAbility.IsActive);

        float remainingTime = 0f;

        if (arAbility != null && arAbility.IsActive)
            remainingTime = arAbility.RemainingTime;
        else if (sgAbility != null && sgAbility.IsActive)
            remainingTime = sgAbility.RemainingTime;

        Rect boxRect = new Rect(
            offsetFromBottomLeft.x,
            Screen.height - offsetFromBottomLeft.y - boxHeight,
            boxWidth,
            boxHeight
        );

        GUI.Box(boxRect, GUIContent.none, boxStyle);

        Rect titleRect = new Rect(boxRect.x + 8f, boxRect.y + 4f, boxRect.width - 16f, 24f);
        Rect valueRect = new Rect(boxRect.x + 8f, boxRect.y + 30f, boxRect.width - 16f, 28f);

        GUI.Label(titleRect, title, titleStyle);

        if (isActive)
        {
            GUI.Label(valueRect, $"ACTIVE  {remainingTime:F1}s", activeStyle);
        }
        else if (killCount.IsCharged)
        {
            GUI.Label(valueRect, $"READY  {killCount.RequiredKills}/{killCount.RequiredKills}", chargedStyle);
        }
        else
        {
            GUI.Label(valueRect, $"{killCount.CurrentKills}/{killCount.RequiredKills}", textStyle);
        }
    }
}
