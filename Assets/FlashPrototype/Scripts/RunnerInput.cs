using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FlashGame
{
    // One owner for input; device bindings work without PlayerInput scene wiring.
    public sealed class RunnerInput : IDisposable
    {
        readonly InputActionMap map = new InputActionMap("Flash Prototype");
        readonly InputAction move, mouseLook, stickLook, scroll;
        public readonly InputAction Faster, Slower, Jump, Brake, Perception, Reset, Race, Pause,
            Punch, Lightning, Boost, Afterimage, Special, NextPower, PreviousPower;
        public RunnerInput()
        {
            move = map.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");
            mouseLook = map.AddAction("Mouse look", InputActionType.Value, "<Mouse>/delta");
            stickLook = map.AddAction("Stick look", InputActionType.Value, "<Gamepad>/rightStick");
            scroll = map.AddAction("Power scroll", InputActionType.Value, "<Mouse>/scroll/y");
            Faster = Button("Faster", "<Keyboard>/e", "<Gamepad>/rightShoulder");
            Slower = Button("Slower", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            Jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Brake = Button("Brake", "<Keyboard>/leftShift", "<Gamepad>/leftTrigger");
            Perception = Button("Speed perception", "<Keyboard>/f", "<Gamepad>/rightTrigger");
            Reset = Button("Return to lab", "<Keyboard>/r", "<Gamepad>/select");
            Race = Button("Restart race", "<Keyboard>/t", "<Gamepad>/dpad/up");
            Pause = Button("Pause", "<Keyboard>/escape", "<Gamepad>/start");
            Punch = Button("Punch", "<Mouse>/leftButton", "<Gamepad>/buttonWest");
            Lightning = Button("Lightning", "<Mouse>/rightButton", "<Gamepad>/buttonEast");
            Boost = Button("Speed boost", "<Keyboard>/leftCtrl", "<Gamepad>/leftStickPress");
            Afterimage = Button("Afterimage", "<Keyboard>/v", "<Gamepad>/rightStickPress");
            Special = Button("Special power", "<Keyboard>/g", "<Gamepad>/buttonNorth");
            Special.AddBinding("<Mouse>/middleButton");
            NextPower = Button("Next power", "<Keyboard>/x", "<Gamepad>/dpad/right");
            PreviousPower = Button("Previous power", "<Keyboard>/z", "<Gamepad>/dpad/left");
            map.Enable();
        }
        InputAction Button(string name, string keyboard, string gamepad)
        {
            var action = map.AddAction(name, InputActionType.Button, keyboard);
            action.AddBinding(gamepad);
            return action;
        }
        public Vector2 Move => Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
        public Vector2 Look(float dt) => mouseLook.ReadValue<Vector2>() * 0.11f
            + stickLook.ReadValue<Vector2>() * (145f * dt);
        // +1 / -1 on a mouse-wheel notch, otherwise 0.
        public int Scroll { get { float y = scroll.ReadValue<float>(); return y > 0.01f ? 1 : y < -0.01f ? -1 : 0; } }
        public void Dispose() { map.Disable(); map.Dispose(); }
    }
}
