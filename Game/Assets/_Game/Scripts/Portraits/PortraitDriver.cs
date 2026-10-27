using UnityEngine;

public enum PortraitMood { Neutral, Happy, Tired, Panic }

// Gives a portrait its idle life (blinking, breathing, glancing around) and turns the staff
// member's mood into an expression. Works with any IPortraitModel on the same object:
// the built-in PortraitPuppet or a Live2D Cubism model adapter.
//
// Gameplay sets Mood (from fatigue / morale / rush hour) and Talking; everything else is automatic.
public class PortraitDriver : MonoBehaviour
{
    public PortraitMood mood;
    public bool talking;
    [Tooltip("Random seed so a row of portraits doesn't blink in sync.")]
    public int seed;

    IPortraitModel model;
    float nextBlink, blinkStart = -1f;

    public PortraitMood Mood { get => mood; set => mood = value; }

    void OnEnable()
    {
        model = GetComponent<IPortraitModel>();
        if (seed == 0) seed = GetInstanceID();
        nextBlink = Time.unscaledTime + 1f + Hash(1) * 3f;
    }

    void Update()
    {
        if (model == null) return;
        Pose(model, mood, talking, Time.unscaledTime, seed, BlinkAmount(Time.unscaledTime));
    }

    float BlinkAmount(float t)
    {
        const float duration = 0.16f;
        if (blinkStart < 0f && t >= nextBlink) blinkStart = t;
        if (blinkStart < 0f) return 0f;
        float k = (t - blinkStart) / duration;
        if (k >= 1f)
        {
            blinkStart = -1f;
            // Tired staff blink slowly and often; panicked staff barely blink.
            float gap = mood == PortraitMood.Tired ? 1.6f : mood == PortraitMood.Panic ? 5f : 2.5f;
            nextBlink = t + gap + Random.value * 2.5f;
            return 0f;
        }
        return 1f - Mathf.Abs(k * 2f - 1f);   // 0 → 1 → 0
    }

    // Pure function of time, so previews and tests can pose a portrait at any moment.
    // blink: 0 = eyes at rest, 1 = fully shut.
    public static void Pose(IPortraitModel m, PortraitMood mood, bool talking, float t, int seed, float blink)
    {
        float s = (seed % 997) * 0.37f;
        float breathPeriod = mood == PortraitMood.Panic ? 1.1f : mood == PortraitMood.Tired ? 4.5f : 3.2f;
        float lookAmount = mood == PortraitMood.Tired ? 0.4f : 1f;

        float angleX = (Mathf.PerlinNoise(s, t * 0.25f) - 0.5f) * 16f * lookAmount;
        float angleY = (Mathf.PerlinNoise(s + 5f, t * 0.2f) - 0.5f) * 8f * lookAmount;
        float angleZ = (Mathf.PerlinNoise(s + 9f, t * 0.15f) - 0.5f) * 6f;
        float eyeX = Mathf.Clamp(angleX / 12f + (Mathf.PerlinNoise(s + 13f, t * 0.6f) - 0.5f) * 0.6f, -1f, 1f);
        float eyeY = (Mathf.PerlinNoise(s + 17f, t * 0.5f) - 0.5f) * 0.6f;
        float eyeOpen = m.GetDefault(PortraitParams.EyeLOpen);
        float brow = 0f, mouth = 0f, bodyX = (Mathf.PerlinNoise(s + 21f, t * 0.12f) - 0.5f) * 4f;

        switch (mood)
        {
            case PortraitMood.Happy:
                eyeOpen *= 0.85f; brow = 0.35f; mouth = 0.35f;
                angleY += Mathf.Abs(Mathf.Sin(t * 3f)) * 4f;   // a little bounce
                break;
            case PortraitMood.Tired:
                eyeOpen *= 0.5f; brow = -0.6f; angleY -= 10f; angleZ += 7f; bodyX -= 2f;
                break;
            case PortraitMood.Panic:
                eyeOpen = 1f; brow = 1f; mouth = 0.55f;
                angleX += Mathf.Sin(t * 37f) * 2.5f;           // trembling
                eyeX = Mathf.Sin(t * 5f + s) * 0.9f;           // darting eyes
                break;
        }
        if (talking) mouth = Mathf.Max(mouth, Mathf.Abs(Mathf.Sin(t * 13f)) * Mathf.PerlinNoise(s + 30f, t * 4f) * 1.2f);

        eyeOpen *= 1f - blink;
        m.SetParameter(PortraitParams.AngleX, angleX);
        m.SetParameter(PortraitParams.AngleY, angleY);
        m.SetParameter(PortraitParams.AngleZ, angleZ);
        m.SetParameter(PortraitParams.EyeLOpen, eyeOpen);
        m.SetParameter(PortraitParams.EyeROpen, eyeOpen);
        m.SetParameter(PortraitParams.EyeBallX, eyeX);
        m.SetParameter(PortraitParams.EyeBallY, eyeY);
        m.SetParameter(PortraitParams.BrowLY, brow);
        m.SetParameter(PortraitParams.BrowRY, brow);
        m.SetParameter(PortraitParams.MouthOpenY, Mathf.Clamp01(mouth));
        m.SetParameter(PortraitParams.BodyAngleX, bodyX);
        m.SetParameter(PortraitParams.Breath, (Mathf.Sin(t * 2f * Mathf.PI / breathPeriod) + 1f) * 0.5f);
    }

    float Hash(int k) => Mathf.Repeat(Mathf.Sin((seed + k) * 12.9898f) * 43758.5453f, 1f);
}
