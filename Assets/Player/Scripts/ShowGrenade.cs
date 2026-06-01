using UnityEngine;

public class ShowGrenade : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerGrenadeSlots grenadeSlots;

    [Header("Display")]
    [SerializeField] private bool show = true;
    [SerializeField] private Vector2 offsetFromBottomLeft = new Vector2(18f, 100f);
    [SerializeField] private float boxWidth = 220f;
    [SerializeField] private float boxHeight = 64f;
    [SerializeField] private int fontSize = 22;
    [SerializeField] private string title = "Grenade";

    private GUIStyle boxStyle;
    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle emptyStyle;
    private bool stylesReady;

    private void Awake()
    {
        if (grenadeSlots == null)
            grenadeSlots = GetComponent<PlayerGrenadeSlots>();
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

        emptyStyle = new GUIStyle(textStyle);
        emptyStyle.normal.textColor = Color.gray;

        stylesReady = true;
    }

    private void OnGUI()
    {
        if (!show || grenadeSlots == null)
            return;

        EnsureStyles();

        PlayerGrenadeSlots.GrenadeSlot slot = grenadeSlots.CurrentSlot;

        Rect boxRect = new Rect(
            offsetFromBottomLeft.x,
            Screen.height - offsetFromBottomLeft.y - boxHeight,
            boxWidth,
            boxHeight
        );

        GUI.Box(boxRect, GUIContent.none, boxStyle);

        Rect titleRect = new Rect(boxRect.x + 8f, boxRect.y + 4f,  boxRect.width - 16f, 24f);
        Rect valueRect = new Rect(boxRect.x + 8f, boxRect.y + 30f, boxRect.width - 16f, 28f);

        GUI.Label(titleRect, title, titleStyle);

        if (slot == null || !grenadeSlots.HasUsableCurrentGrenade)
        {
            GUI.Label(valueRect, "None", emptyStyle);
            return;
        }

        string typeName = slot.grenadeType.ToString();
        string countText = slot.count < 0 ? "∞" : slot.count.ToString();
        GUI.Label(valueRect, $"{typeName}  x{countText}", textStyle);
    }
}
