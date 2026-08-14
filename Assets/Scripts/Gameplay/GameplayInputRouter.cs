using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace IdiotTape.Gameplay
{

    public sealed class GameplayInputRouter : MonoBehaviour
    {

        public event Action<Vector2, double> Pressed;
        public event Action<int, double> LanePressed;
        public event Action RestartRequested;
        public event Action PauseRequested;

        public void SubmitLaneInput(int laneIndex, double eventTimestamp)
        {

            if (laneIndex < 0 || laneIndex >= 8)
            {

                return;

            }

            LanePressed?.Invoke(laneIndex, eventTimestamp);

        }

        public void RequestPause()
        {

            PauseRequested?.Invoke();

        }

        private void OnEnable()
        {

            EnhancedTouchSupport.Enable();

        }

        private void OnDisable()
        {

            EnhancedTouchSupport.Disable();

        }

        private void Update()
        {

            HandleKeyboard();

            bool handledTouch = false;

            for (int index = 0; index < Touch.activeTouches.Count; index++)
            {

                Touch touch = Touch.activeTouches[index];

                if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began)
                {

                    continue;

                }

                handledTouch = true;
                Pressed?.Invoke(touch.screenPosition, touch.time);

            }

            if (!handledTouch && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {

                Pressed?.Invoke(Mouse.current.position.ReadValue(), Mouse.current.lastUpdateTime);

            }

        }

        private void HandleKeyboard()
        {

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {

                return;

            }

            for (int laneIndex = 0; laneIndex < 8; laneIndex++)
            {

                if (WasLaneKeyPressed(keyboard, laneIndex))
                {

                    SubmitLaneInput(laneIndex, keyboard.lastUpdateTime);

                }

            }

            if (keyboard.rKey.wasPressedThisFrame)
            {

                RestartRequested?.Invoke();

            }

            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame)
            {

                RequestPause();

            }

        }

        private static bool WasLaneKeyPressed(Keyboard keyboard, int laneIndex)
        {

            switch (laneIndex)
            {

                case 0:
                    return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
                case 1:
                    return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
                case 2:
                    return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
                case 3:
                    return keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame;
                case 4:
                    return keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame;
                case 5:
                    return keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame;
                case 6:
                    return keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame;
                case 7:
                    return keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame;
                default:
                    return false;

            }

        }

    }

}
