using System;
using UnityEngine;

// Holds the active ThemeProfile and applies its world settings (sky, sun, ambient light).
// Put one in the scene and assign a theme. Everything themed reads ThemeManager.Current
// and listens to ThemeChanged, so swapping the theme updates the whole game, in Play mode
// and in the editor.
[ExecuteAlways]
[DefaultExecutionOrder(-100)] // set Current before anything else reads it
public class ThemeManager : MonoBehaviour
{
    [SerializeField] ThemeProfile theme;
    [Tooltip("Directional light that the theme's sun settings apply to.")]
    [SerializeField] Light sun;

    public static ThemeProfile Current { get; private set; }
    public static event Action<ThemeProfile> ThemeChanged;

    void OnEnable() => SetTheme(theme);

    public void SetTheme(ThemeProfile newTheme)
    {
        if (newTheme == null)
        {
            Debug.LogWarning("ThemeManager has no theme assigned.", this);
            return;
        }
        theme = newTheme;
        Current = newTheme;
        ApplyWorld();
        ThemeChanged?.Invoke(newTheme);
    }

    void ApplyWorld()
    {
        var cam = Camera.main;
        if (cam != null) cam.backgroundColor = theme.backgroundColor;
        if (sun != null)
        {
            sun.color = theme.sunColor;
            sun.intensity = theme.sunIntensity;
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = theme.ambientColor;
    }

    void OnDisable()
    {
        if (Current == theme) Current = null;
    }

#if UNITY_EDITOR
    // Swapping the theme field in the Inspector applies it immediately.
    // Labels can't be changed inside OnValidate itself, so wait one editor tick.
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled && theme != null && theme != Current) SetTheme(theme);
        };
    }
#endif
}
