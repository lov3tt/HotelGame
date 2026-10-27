// Parameter ids shared by every portrait model. They match Live2D Cubism's standard ids,
// so the same driver code moves the built-in puppet and a real Cubism model alike.
public static class PortraitParams
{
    public const string AngleX = "ParamAngleX";          // head turn, -30 (their right) .. 30
    public const string AngleY = "ParamAngleY";          // head nod, -30 (down) .. 30 (up)
    public const string AngleZ = "ParamAngleZ";          // head tilt, -30 .. 30
    public const string EyeLOpen = "ParamEyeLOpen";      // 0 closed .. 1 open
    public const string EyeROpen = "ParamEyeROpen";
    public const string EyeBallX = "ParamEyeBallX";      // look direction, -1 .. 1
    public const string EyeBallY = "ParamEyeBallY";
    public const string BrowLY = "ParamBrowLY";          // -1 lowered (tired, cross) .. 1 raised (surprised)
    public const string BrowRY = "ParamBrowRY";
    public const string MouthOpenY = "ParamMouthOpenY";  // 0 closed .. 1 open
    public const string BodyAngleX = "ParamBodyAngleX";  // body lean, -10 .. 10
    public const string Breath = "ParamBreath";          // 0 .. 1

    public static readonly string[] All =
        { AngleX, AngleY, AngleZ, EyeLOpen, EyeROpen, EyeBallX, EyeBallY, BrowLY, BrowRY, MouthOpenY, BodyAngleX, Breath };
}

// Anything that can show a staff portrait: the built-in PortraitPuppet, or a Live2D model adapter.
public interface IPortraitModel
{
    void SetParameter(string id, float value);
    float GetParameter(string id);
    // The model's resting value, e.g. Ember's half-open sleepy eyes.
    float GetDefault(string id);
}
