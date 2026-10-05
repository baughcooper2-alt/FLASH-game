using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FlashGame
{
    // The GameSir G7 Pro shows up on macOS as a plain HID game pad (USB 3537:1022) sending a 9-byte report.
    // Without a layout the Input System makes it a generic Joystick and no Gamepad binding fires.
    // Report: 15 buttons, 4-bit hat, X/Y left stick, Z/Rz right stick, Brake/Accelerator triggers.
    // Button order matches SDL's GameControllerDB entry for the G7 Pro on macOS.
    [StructLayout(LayoutKind.Explicit, Size = 9)]
    public struct GameSirHIDInputReport : IInputStateTypeInfo
    {
        public FourCC format => new FourCC('H', 'I', 'D');

        [InputControl(name = "buttonSouth", displayName = "A", bit = 0)]
        [InputControl(name = "buttonEast", displayName = "B", bit = 1)]
        [InputControl(name = "buttonWest", displayName = "X", bit = 3)]
        [InputControl(name = "buttonNorth", displayName = "Y", bit = 4)]
        [InputControl(name = "leftShoulder", displayName = "LB", bit = 6)]
        [InputControl(name = "rightShoulder", displayName = "RB", bit = 7)]
        [InputControl(name = "select", displayName = "View", bit = 10)]
        [InputControl(name = "start", displayName = "Menu", bit = 11)]
        [InputControl(name = "homeButton", layout = "Button", displayName = "Home", bit = 12)]
        [InputControl(name = "leftStickPress", displayName = "Left Stick Press", bit = 13)]
        [InputControl(name = "rightStickPress", displayName = "Right Stick Press", bit = 14)]
        [FieldOffset(0)] public ushort buttons;

        [InputControl(name = "dpad", format = "BIT", layout = "Dpad", sizeInBits = 4, defaultState = 8)]
        [InputControl(name = "dpad/up", format = "BIT", layout = "DiscreteButton", parameters = "minValue=7,maxValue=1,nullValue=8,wrapAtValue=7", bit = 0, sizeInBits = 4)]
        [InputControl(name = "dpad/right", format = "BIT", layout = "DiscreteButton", parameters = "minValue=1,maxValue=3", bit = 0, sizeInBits = 4)]
        [InputControl(name = "dpad/down", format = "BIT", layout = "DiscreteButton", parameters = "minValue=3,maxValue=5", bit = 0, sizeInBits = 4)]
        [InputControl(name = "dpad/left", format = "BIT", layout = "DiscreteButton", parameters = "minValue=5,maxValue=7", bit = 0, sizeInBits = 4)]
        [FieldOffset(2)] public byte hat;

        [InputControl(name = "leftStick", layout = "Stick", format = "VC2B")]
        [InputControl(name = "leftStick/x", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
        [InputControl(name = "leftStick/left", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0,clampMax=0.5,invert")]
        [InputControl(name = "leftStick/right", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0.5,clampMax=1")]
        [InputControl(name = "leftStick/y", offset = 1, format = "BYTE", parameters = "invert,normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
        [InputControl(name = "leftStick/up", offset = 1, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0,clampMax=0.5,invert")]
        [InputControl(name = "leftStick/down", offset = 1, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0.5,clampMax=1,invert=false")]
        [FieldOffset(3)] public byte leftStickX;
        [FieldOffset(4)] public byte leftStickY;

        [InputControl(name = "rightStick", layout = "Stick", format = "VC2B")]
        [InputControl(name = "rightStick/x", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
        [InputControl(name = "rightStick/left", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0,clampMax=0.5,invert")]
        [InputControl(name = "rightStick/right", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0.5,clampMax=1")]
        [InputControl(name = "rightStick/y", offset = 1, format = "BYTE", parameters = "invert,normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
        [InputControl(name = "rightStick/up", offset = 1, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0,clampMax=0.5,invert")]
        [InputControl(name = "rightStick/down", offset = 1, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0.5,clampMax=1,invert=false")]
        [FieldOffset(5)] public byte rightStickX;
        [FieldOffset(6)] public byte rightStickY;

        [InputControl(name = "leftTrigger", displayName = "LT", format = "BYTE")]
        [FieldOffset(7)] public byte leftTrigger;
        [InputControl(name = "rightTrigger", displayName = "RT", format = "BYTE")]
        [FieldOffset(8)] public byte rightTrigger;
    }

    [InputControlLayout(stateType = typeof(GameSirHIDInputReport), displayName = "GameSir Controller")]
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public class GameSirGamepad : Gamepad
    {
        public const int VendorId = 0x3537;
        public const int G7ProProductId = 0x1022;
        public ButtonControl homeButton { get; private set; }

        static GameSirGamepad()
        {
            InputSystem.RegisterLayout<GameSirGamepad>(matches: new InputDeviceMatcher()
                .WithInterface("HID")
                .WithCapability("vendorId", VendorId)
                .WithCapability("productId", G7ProProductId));
        }

        // Runs the static constructor in players; the editor does it through InitializeOnLoad.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { }

        protected override void FinishSetup()
        {
            base.FinishSetup();
            homeButton = GetChildControl<ButtonControl>("homeButton");
        }
    }
}
