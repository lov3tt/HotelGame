using UnityEngine;
using UnityEngine.UI;

// Colours a UI Image (or any Graphic) from the theme palette.
// With usePanelSprite on, Images also take the theme's panel sprite.
[ExecuteAlways]
[RequireComponent(typeof(Graphic))]
public class ThemedGraphic : MonoBehaviour
{
    [SerializeField] PaletteRole color = PaletteRole.Panel;
    [SerializeField] bool usePanelSprite = true;

    Graphic graphic;

    void OnEnable()
    {
        graphic = GetComponent<Graphic>();
        ThemeManager.ThemeChanged += Refresh;
        Refresh(ThemeManager.Current);
    }

    void OnDisable() => ThemeManager.ThemeChanged -= Refresh;

    void Refresh(ThemeProfile theme)
    {
        if (theme == null || graphic == null) return;
        graphic.color = theme.palette.Get(color);
        if (usePanelSprite && graphic is Image image)
        {
            image.sprite = theme.panelSprite;
            image.type = theme.panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        }
    }
}
