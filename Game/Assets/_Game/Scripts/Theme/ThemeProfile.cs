using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Everything that gives the game its flavour: names, colours, fonts, props, sounds, lighting
// and character appearances. Gameplay code never hard-codes these; it asks ThemeManager.Current.
//
// To retheme: duplicate Theme_Medieval (Ctrl+D), edit it, and assign it on the ThemeManager.
[CreateAssetMenu(menuName = "InnKeeper/Theme Profile", fileName = "Theme_New")]
public class ThemeProfile : ScriptableObject
{
    [Serializable]
    public struct NameEntry
    {
        [Tooltip("Lookup key, e.g. role.porter. See ThemeKeys for the full list.")]
        public string key;
        public string displayName;
    }

    [Serializable]
    public struct PropEntry
    {
        [Tooltip("Prop key, e.g. prop.bed. See ThemeKeys.")]
        public string key;
        [Tooltip("Replaces the graybox block. Leave empty to keep the block.")]
        public GameObject prefab;
    }

    [Serializable]
    public struct AppearancePool
    {
        [Tooltip("Guest type key, e.g. guest.merchant.")]
        public string guestType;
        public CharacterAppearance[] appearances;
    }

    [Header("Identity")]
    public string innName = "The Wayside";
    public string currencyName = "Coin";
    public string currencySymbol = "c";

    [Header("Display names")]
    [Tooltip("Roles, guest types, room types, services, tiers... Missing keys fall back to the key itself.")]
    public List<NameEntry> names = new List<NameEntry>();
    [Tooltip("First names for hiring-board candidates.")]
    public string[] firstNames = Array.Empty<string>();

    [Header("UI")]
    public ThemePalette palette = ThemePalette.Default;
    public TMP_FontAsset uiFont;
    [Tooltip("9-sliced sprite for panels. Empty = plain coloured panels.")]
    public Sprite panelSprite;

    [Header("World")]
    public List<PropEntry> props = new List<PropEntry>();
    public Color backgroundColor = new Color(0.55f, 0.70f, 0.85f);
    public Color sunColor = new Color(1f, 0.95f, 0.85f);
    public float sunIntensity = 1.2f;
    public Color ambientColor = new Color(0.45f, 0.45f, 0.5f);

    [Header("Audio")]
    public AudioClip serviceBell;
    public AudioClip music;

    [Header("Characters")]
    [Tooltip("Looks handed out to newly hired staff.")]
    public CharacterAppearance[] staffAppearances = Array.Empty<CharacterAppearance>();
    public List<AppearancePool> guestAppearances = new List<AppearancePool>();

    Dictionary<string, string> nameLookup;
    Dictionary<string, GameObject> propLookup;

    // Display name for a key, e.g. GetName(ThemeKeys.RolePorter) → "Porter".
    public string GetName(string key)
    {
        switch (key)
        {
            case ThemeKeys.InnName: return innName;
            case ThemeKeys.CurrencyName: return currencyName;
            case ThemeKeys.CurrencySymbol: return currencySymbol;
        }
        if (nameLookup == null)
        {
            nameLookup = new Dictionary<string, string>();
            foreach (var n in names) nameLookup[n.key] = n.displayName;
        }
        return nameLookup.TryGetValue(key, out var value) ? value : key;
    }

    // Replaces {key} tokens, e.g. "Welcome to {inn.name}" → "Welcome to The Wayside".
    public string Format(string template)
    {
        if (string.IsNullOrEmpty(template) || template.IndexOf('{') < 0) return template;
        var sb = new System.Text.StringBuilder(template.Length);
        int i = 0;
        while (i < template.Length)
        {
            int open = template.IndexOf('{', i);
            int close = open < 0 ? -1 : template.IndexOf('}', open + 1);
            if (open < 0 || close < 0)
            {
                sb.Append(template, i, template.Length - i);
                break;
            }
            sb.Append(template, i, open - i);
            sb.Append(GetName(template.Substring(open + 1, close - open - 1)));
            i = close + 1;
        }
        return sb.ToString();
    }

    public string FormatMoney(int amount) => amount + currencySymbol;

    public GameObject GetProp(string key)
    {
        if (propLookup == null)
        {
            propLookup = new Dictionary<string, GameObject>();
            foreach (var p in props) if (p.prefab != null) propLookup[p.key] = p.prefab;
        }
        return propLookup.TryGetValue(key, out var prefab) ? prefab : null;
    }

    public CharacterAppearance[] GetGuestAppearances(string guestType)
    {
        foreach (var pool in guestAppearances)
            if (pool.guestType == guestType) return pool.appearances;
        return staffAppearances;
    }

    // Inspector edits change the lists, so rebuild the lookups next time they're used.
    void OnValidate()
    {
        nameLookup = null;
        propLookup = null;
    }
}

[Serializable]
public struct ThemePalette
{
    public Color panel;
    public Color panelAccent;
    public Color text;
    public Color textMuted;
    public Color highlight;
    public Color good;
    public Color warning;
    public Color bad;

    public static ThemePalette Default => new ThemePalette
    {
        panel = new Color(0.22f, 0.16f, 0.11f, 0.92f),
        panelAccent = new Color(0.45f, 0.30f, 0.16f, 1f),
        text = new Color(0.96f, 0.91f, 0.80f),
        textMuted = new Color(0.75f, 0.68f, 0.56f),
        highlight = new Color(0.95f, 0.75f, 0.30f),
        good = new Color(0.45f, 0.80f, 0.40f),
        warning = new Color(0.95f, 0.80f, 0.25f),
        bad = new Color(0.85f, 0.30f, 0.25f),
    };

    public Color Get(PaletteRole role) => role switch
    {
        PaletteRole.Panel => panel,
        PaletteRole.PanelAccent => panelAccent,
        PaletteRole.Text => text,
        PaletteRole.TextMuted => textMuted,
        PaletteRole.Highlight => highlight,
        PaletteRole.Good => good,
        PaletteRole.Warning => warning,
        PaletteRole.Bad => bad,
        _ => Color.magenta,
    };
}

public enum PaletteRole { Panel, PanelAccent, Text, TextMuted, Highlight, Good, Warning, Bad }
