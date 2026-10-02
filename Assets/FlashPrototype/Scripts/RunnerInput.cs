using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FlashGame
{
    // One owner for input; device bindings work without PlayerInput scene wiring.
    public sealed class RunnerInput : IDisposable
    {
        readonly InputActionMap map = new InputActionMap("Flash Prototype");
        readonly InputAction move, mouseLook, stickLook;
        public readonly InputAction Faster, Slower, Jump, Brake, Perception, Reset, Race, Pause;
        public RunnerInput()
        {
            move = map.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");
            mouseLook = map.AddAction("Mouse look", InputActionType.Value, "<Mouse>/delta");
            stickLook = map.AddAction("Stick look", InputActionType.Value, "<Gamepad>/rightStick");
            Faster = Button("Faster", "<Keyboard>/e", "<Gamepad>/rightShoulder");
            Slower = Button("Slower", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            Jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Brake = Button("Brake", "<Keyboard>/leftShift", "<Gamepad>/leftTrigger");
            Perception = Button("Speed perception", "<Keyboard>/f", "<Gamepad>/rightTrigger");
            Reset = Button("Return to lab", "<Keyboard>/r", "<Gamepad>/select");
            Race = Button("Restart race", "<Keyboard>/t", "<Gamepad>/buttonNorth");
            Pause = Button("Pause", "<Keyboard>/escape", "<Gamepad>/start");
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
        public void Dispose() { map.Disable(); map.Dispose(); }
    }
}
