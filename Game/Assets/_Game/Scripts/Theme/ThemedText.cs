using TMPro;
using UnityEngine;

// Fills a TextMeshPro label from the theme. The template can mix plain text and {key} tokens,
// e.g. "{inn.name}" or "Hire a {role.porter}". Also applies the theme's font and a palette colour.
[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class ThemedText : MonoBehaviour
{
    [TextArea] [SerializeField] string template = "{inn.name}";
    [SerializeField] PaletteRole color = PaletteRole.Text;

    TMP_Text label;

    public string Template
    {
        get => template;
        set { template = value; Refresh(ThemeManager.Current); }
    }

    void OnEnable()
    {
        label = GetComponent<TMP_Text>();
        ThemeManager.ThemeChanged += Refresh;
        Refresh(ThemeManager.Current);
    }

    void OnDisable() => ThemeManager.ThemeChanged -= Refresh;

    void Refresh(ThemeProfile theme)
    {
        if (theme == null || label == null) return;
        label.text = theme.Format(template);
        label.color = theme.palette.Get(color);
        if (theme.uiFont != null) label.font = theme.uiFont;
    }
}
