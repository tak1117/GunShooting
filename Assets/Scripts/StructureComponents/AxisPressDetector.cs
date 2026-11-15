using Valve.VR;

namespace StructureComponents
{
    // Turns a continuous SteamVR axis (e.g. an analog trigger) into a debounced press/release
    // edge, so callers don't each reimplement the same hysteresis threshold.
    public class AxisPressDetector
    {
        private readonly SteamVR_Action_Single _axis;
        private readonly float _pressAt;
        private readonly float _releaseAt;

        private bool _isPressed;

        public AxisPressDetector(SteamVR_Action_Single axis, float pressAt = 0.9f, float releaseAt = 0.5f)
        {
            _axis = axis;
            _pressAt = pressAt;
            _releaseAt = releaseAt;
        }

        // Call once per frame. Returns true exactly on the frame the axis crosses the press threshold.
        public bool UpdateAndCheckPressed(SteamVR_Input_Sources source)
        {
            var value = _axis[source].axis;

            if (value > _pressAt && !_isPressed)
            {
                _isPressed = true;
                return true;
            }

            if (value < _releaseAt && _isPressed)
            {
                _isPressed = false;
            }

            return false;
        }

        // Forces the next press to be treated as a fresh one, e.g. when the controller
        // holding this axis has been let go of and re-grabbed.
        public void Reset()
        {
            _isPressed = false;
        }
    }
}
