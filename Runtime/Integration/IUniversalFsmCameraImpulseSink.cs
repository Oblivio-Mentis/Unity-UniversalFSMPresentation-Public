#nullable enable


namespace OblivioMentis.Presentation
{
    /// <summary>
    /// Optional bridge for camera shake/impulse integration.
    /// Implement this on a scene component and assign it to a character presentation controller.
    /// </summary>
    public interface IUniversalFsmCameraImpulseSink
    {
        void EmitImpulse(float amplitude, float durationSeconds);
    }
}


