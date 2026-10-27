using System;
using System.Collections.Generic;
using UnityEngine;

// A lightweight "pseudo Live2D" rig: layered sprites that fake a 3D head turn with parallax
// (near layers like the fringe slide further than the back hair), plus blink, look, brows,
// mouth, breathing and spring physics on hair, ears and tails.
//
// Built from layered art by InnKeeper → Rebuild Portraits (Editor/PortraitBuilder.cs).
// Drive it through IPortraitModel, usually from a PortraitDriver on the same object.
[ExecuteAlways]
public class PortraitPuppet : MonoBehaviour, IPortraitModel
{
    [Serializable]
    public class Part
    {
        public Transform transform;
        public Vector3 basePosition;
        [Tooltip("0 = face plane. Positive is nearer the viewer and slides further when the head turns.")]
        public float depth;
    }

    [Serializable]
    public class SwingGroup
    {
        public Transform transform;
        [Tooltip("How much this group sways with head movement (0 = rigid).")]
        public float swing;
        [NonSerialized] public float angle, velocity;
    }

    public float pixelsPerUnit = 512f;
    public List<Part> parts = new List<Part>();
    public List<SwingGroup> swingGroups = new List<SwingGroup>();

    [Header("Rig pieces (filled in by the builder)")]
    public Transform body;
    public Transform head;
    public Transform eyeL, eyeR, irisL, irisR, browL, browR;
    public SpriteRenderer eyeClosedL, eyeClosedR, mouthClosed, mouthOpen;
    [HideInInspector] public Vector3 headBase, irisLBase, irisRBase, browLBase, browRBase;

    [Header("Resting values")]
    [Range(0, 1)] public float defaultEyeOpen = 1f;

    [Header("Parameters")]
    [Range(-30, 30)] public float angleX;
    [Range(-30, 30)] public float angleY;
    [Range(-30, 30)] public float angleZ;
    [Range(0, 1)] public float eyeLOpen = 1f;
    [Range(0, 1)] public float eyeROpen = 1f;
    [Range(-1, 1)] public float eyeBallX;
    [Range(-1, 1)] public float eyeBallY;
    [Range(-1, 1)] public float browLY;
    [Range(-1, 1)] public float browRY;
    [Range(0, 1)] public float mouthOpenY;
    [Range(-10, 10)] public float bodyAngleX;
    [Range(0, 1)] public float breath;

    // Pixel distances at full parameter range, in canvas pixels.
    const float ParallaxX = 70f, ParallaxY = 40f, HeadShiftX = 16f, HeadShiftY = 12f;
    const float IrisRangeX = 9f, IrisRangeY = 6f, BrowRange = 10f;

    float lastAngleX, lastAngleY;

    public void SetParameter(string id, float value)
    {
        switch (id)
        {
            case PortraitParams.AngleX: angleX = Mathf.Clamp(value, -30, 30); break;
            case PortraitParams.AngleY: angleY = Mathf.Clamp(value, -30, 30); break;
            case PortraitParams.AngleZ: angleZ = Mathf.Clamp(value, -30, 30); break;
            case PortraitParams.EyeLOpen: eyeLOpen = Mathf.Clamp01(value); break;
            case PortraitParams.EyeROpen: eyeROpen = Mathf.Clamp01(value); break;
            case PortraitParams.EyeBallX: eyeBallX = Mathf.Clamp(value, -1, 1); break;
            case PortraitParams.EyeBallY: eyeBallY = Mathf.Clamp(value, -1, 1); break;
            case PortraitParams.BrowLY: browLY = Mathf.Clamp(value, -1, 1); break;
            case PortraitParams.BrowRY: browRY = Mathf.Clamp(value, -1, 1); break;
            case PortraitParams.MouthOpenY: mouthOpenY = Mathf.Clamp01(value); break;
            case PortraitParams.BodyAngleX: bodyAngleX = Mathf.Clamp(value, -10, 10); break;
            case PortraitParams.Breath: breath = Mathf.Clamp01(value); break;
        }
    }

    public float GetParameter(string id)
    {
        switch (id)
        {
            case PortraitParams.AngleX: return angleX;
            case PortraitParams.AngleY: return angleY;
            case PortraitParams.AngleZ: return angleZ;
            case PortraitParams.EyeLOpen: return eyeLOpen;
            case PortraitParams.EyeROpen: return eyeROpen;
            case PortraitParams.EyeBallX: return eyeBallX;
            case PortraitParams.EyeBallY: return eyeBallY;
            case PortraitParams.BrowLY: return browLY;
            case PortraitParams.BrowRY: return browRY;
            case PortraitParams.MouthOpenY: return mouthOpenY;
            case PortraitParams.BodyAngleX: return bodyAngleX;
            case PortraitParams.Breath: return breath;
        }
        return 0f;
    }

    public float GetDefault(string id) =>
        id == PortraitParams.EyeLOpen || id == PortraitParams.EyeROpen ? defaultEyeOpen : 0f;

    void LateUpdate()
    {
        float dt = Application.isPlaying ? Time.unscaledDeltaTime : 0f;
        Apply(dt);
    }

    // Poses every layer from the current parameters. dt > 0 also steps the hair/tail springs.
    public void Apply(float dt)
    {
        float u = 1f / pixelsPerUnit;
        float tx = angleX / 30f, ty = angleY / 30f;

        if (body != null)
        {
            body.localRotation = Quaternion.Euler(0, 0, -bodyAngleX);
            body.localScale = new Vector3(1f, 1f + breath * 0.012f, 1f);
        }
        if (head != null)
        {
            head.localPosition = headBase + new Vector3(tx * HeadShiftX, ty * HeadShiftY + breath * 3f, 0) * u;
            head.localRotation = Quaternion.Euler(0, 0, -angleZ * 0.5f);
            // Turning the head narrows it slightly, which sells the rotation.
            head.localScale = new Vector3(1f - Mathf.Abs(tx) * 0.04f, 1f - Mathf.Abs(ty) * 0.02f, 1f);
        }

        foreach (var p in parts)
        {
            if (p.transform == null) continue;
            p.transform.localPosition = p.basePosition + new Vector3(tx * ParallaxX * p.depth, ty * ParallaxY * p.depth, 0) * u;
        }

        PoseEye(eyeL, eyeClosedL, eyeLOpen);
        PoseEye(eyeR, eyeClosedR, eyeROpen);
        var look = new Vector3(eyeBallX * IrisRangeX, eyeBallY * IrisRangeY, 0) * u;
        if (irisL != null) irisL.localPosition = irisLBase + look;
        if (irisR != null) irisR.localPosition = irisRBase + look;
        if (browL != null) browL.localPosition = browLBase + new Vector3(0, browLY * BrowRange, 0) * u;
        if (browR != null) browR.localPosition = browRBase + new Vector3(0, browRY * BrowRange, 0) * u;

        if (mouthOpen != null)
        {
            bool open = mouthOpenY > 0.05f;
            mouthOpen.enabled = open;
            mouthOpen.transform.localScale = new Vector3(0.75f + 0.25f * mouthOpenY, Mathf.Lerp(0.25f, 1f, mouthOpenY), 1f);
        }
        if (mouthClosed != null) mouthClosed.enabled = mouthOpenY < 0.3f;

        StepSprings(dt);
    }

    static void PoseEye(Transform eye, SpriteRenderer closed, float open)
    {
        bool shut = open < 0.2f;
        if (eye != null) eye.localScale = new Vector3(1f, shut ? 0f : Mathf.Lerp(0.35f, 1f, (open - 0.2f) / 0.8f), 1f);
        if (closed != null) closed.enabled = shut;
    }

    void StepSprings(float dt)
    {
        if (dt <= 0f)
        {
            foreach (var g in swingGroups)
                if (g.transform != null) g.transform.localRotation = Quaternion.Euler(0, 0, g.angle);
            return;
        }
        dt = Mathf.Min(dt, 0.05f);
        // Head speed pushes the hair the other way; gravity of the tilt pulls it back down.
        float headSpeed = (angleX - lastAngleX) / dt + (angleY - lastAngleY) / dt * 0.3f;
        lastAngleX = angleX;
        lastAngleY = angleY;
        float t = Time.unscaledTime;
        for (int i = 0; i < swingGroups.Count; i++)
        {
            var g = swingGroups[i];
            if (g.transform == null) continue;
            float idle = Mathf.Sin(t * 1.4f + i * 1.7f) * 2.5f;
            float target = g.swing * (-headSpeed * 0.08f + angleZ * 0.4f + bodyAngleX * 0.6f + idle);
            const float stiffness = 60f, damping = 7f;
            g.velocity += ((target - g.angle) * stiffness - g.velocity * damping) * dt;
            g.angle = Mathf.Clamp(g.angle + g.velocity * dt, -25f, 25f);
            g.transform.localRotation = Quaternion.Euler(0, 0, g.angle);
        }
    }

    void OnValidate() => Apply(0f);
}
