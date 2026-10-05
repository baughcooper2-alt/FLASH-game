using UnityEngine;
using UnityEngine.InputSystem;

namespace FlashGame
{
    public static class ControllerConnection
    {
        static string checkMessage = "USB and Bluetooth gamepads are detected automatically.";
        public static string DeviceStatus => ConnectedPad != null
            ? "Ready: " + ConnectedPad.displayName + " • " + ConnectedPad.description.interfaceName
            : checkMessage;
        public static void CheckWired()
        {
            var pad=ConnectedPad;
            if(pad!=null)
            {
                InputSystem.TrySyncDevice(pad);
                checkMessage="Controller ready. Move a stick below.";
                return;
            }
            bool other=false;
            foreach(var device in InputSystem.devices)
                if(device is Joystick)other=true;
            checkMessage=other ? "Joystick detected, but no supported gamepad mapping. Check macOS support for this controller."
                : "No gamepad reported by the computer. Try another USB data cable or port. Some Xbox models need Bluetooth on macOS.";
        }
        public static Gamepad ConnectedPad
        {
            get
            {
                if (Gamepad.current != null && Gamepad.current.added && Gamepad.current.enabled)
                    return Gamepad.current;
                foreach (var pad in Gamepad.all)
                    if (pad.added && pad.enabled) return pad;
                return null;
            }
        }

        public static void OpenBluetoothSettings()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            Application.OpenURL("x-apple.systempreferences:com.apple.BluetoothSettings");
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            Application.OpenURL("ms-settings:bluetooth");
#endif
        }

        public static string InputSummary(Gamepad pad)
        {
            var left = pad.leftStick.ReadValue();
            var right = pad.rightStick.ReadValue();
            string buttons = "";
            foreach (var button in new[] { pad.buttonSouth, pad.buttonEast, pad.buttonWest,
                pad.buttonNorth, pad.leftShoulder, pad.rightShoulder, pad.selectButton })
                if (button.isPressed) buttons += button.displayName + "  ";
            return $"Left stick: {left.x:F2}, {left.y:F2}    Right stick: {right.x:F2}, {right.y:F2}\n"
                + $"LT: {pad.leftTrigger.ReadValue():F2}    RT: {pad.rightTrigger.ReadValue():F2}\n"
                + "Buttons: " + (buttons.Length == 0 ? "press A to check" : buttons);
        }
    }
}
