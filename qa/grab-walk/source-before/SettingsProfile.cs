using System;

namespace ThoraxLab.Learning
{
    public enum GraphicsPreset { Low, Medium, High, VeryHigh, Extreme }
    public enum MoveStyle { Teleport, Smooth }
    public enum TurnStyle { Snap, Smooth }

    [Serializable] public sealed class SettingsProfile
    {
        public int schemaVersion = 1;
        public string id = Guid.NewGuid().ToString("N"), name = "My settings", platform = "Windows", updatedUtc;
        public GraphicsPreset preset = GraphicsPreset.Medium;
        public float renderScale = 1, brightness = 1, shadowDistance = 3;
        public int textureLimit, msaa = 4, width = 1440, height = 1000, frameCap = 60, refreshRate = 72;
        public bool fullscreen, vsync, shadows, ambientOcclusion, bloom, showFps = true;
        public float masterVolume = .8f, narrationVolume = 1, effectsVolume = .6f, ambienceVolume = .2f, narrationSpeed = 1;
        public bool captions = true, invertLook, leftHanded, toggleGrip, vibration = true;
        public float sensitivity = .12f, moveSpeed = .8f, turnSpeed = 60, snapAngle = 30;
        public MoveStyle movement = MoveStyle.Teleport;
        public TurnStyle turning = TurnStyle.Snap;
        public bool seated = true, vignette = true;
        public bool autoHeight = true;
        public float heightBoost;
        public float tableHeight, textScale = 1, captionScale = 1;
        public bool hints = true, labels = true, highContrast, reducedMotion;
        public string forward = "W", backward = "S", left = "A", right = "D", up = "E", down = "Q", reset = "R", undo = "Z", tool = "B";

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 48 || (platform != "Windows" && platform != "Quest")) throw new ArgumentException("Invalid settings profile.");
            if (!Enum.IsDefined(typeof(GraphicsPreset), preset) || !Enum.IsDefined(typeof(MoveStyle), movement) || !Enum.IsDefined(typeof(TurnStyle), turning)) throw new ArgumentException("Unknown setting option.");
            renderScale = Clamp(renderScale, .6f, 1.4f); brightness = Clamp(brightness, .5f, 1.5f);
            textScale = Clamp(textScale, .85f, 1.5f); captionScale = Clamp(captionScale, .85f, 1.7f);
            sensitivity = Clamp(sensitivity, .03f, .4f); moveSpeed = Clamp(moveSpeed, .2f, 1.5f);
            turnSpeed = Clamp(turnSpeed, 15, 120); snapAngle = Clamp(snapAngle, 15, 60); tableHeight = Clamp(tableHeight, -.2f, .25f);
            heightBoost = Clamp(heightBoost, 0, .8f);
            masterVolume = Clamp(masterVolume, 0, 1); narrationVolume = Clamp(narrationVolume, 0, 1);
            effectsVolume = Clamp(effectsVolume, 0, 1); ambienceVolume = Clamp(ambienceVolume, 0, 1); narrationSpeed = Clamp(narrationSpeed, .75f, 1.5f);
            shadowDistance = Clamp(shadowDistance, 0, 12); textureLimit = Math.Max(0, Math.Min(3, textureLimit));
            if (msaa != 1 && msaa != 2 && msaa != 4 && msaa != 8) msaa = 2;
            width = Math.Max(960, Math.Min(3840, width)); height = Math.Max(540, Math.Min(2160, height));
            frameCap = Math.Max(30, Math.Min(144, frameCap));
            if (platform == "Quest") { msaa = Math.Min(msaa, 4); bloom = false; ambientOcclusion = false; }
        }

        public void SetPreset(GraphicsPreset value)
        {
            preset = value;
            int i = (int)value;
            renderScale = (platform == "Quest" ? new[] { .72f, .85f, 1f, 1.1f, 1.2f } : new[] { .8f, 1f, 1f, 1.1f, 1.25f })[i];
            textureLimit = new[] { 2, 1, 0, 0, 0 }[i]; msaa = i < 2 ? 2 : 4;
            shadows = i >= 2; shadowDistance = new[] { 0f, 0f, 3f, 5f, 8f }[i];
            // These effects are not offered until a matching render feature exists.
            bloom = false; ambientOcclusion = false;
            Validate();
        }
        static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("A numeric setting is invalid.");
            return Math.Max(min, Math.Min(max, value));
        }
    }

    [Serializable] public sealed class SettingsCollection
    {
        public int schemaVersion = 1;
        public string selectedId;
        public System.Collections.Generic.List<SettingsProfile> profiles = new System.Collections.Generic.List<SettingsProfile>();
    }
}
