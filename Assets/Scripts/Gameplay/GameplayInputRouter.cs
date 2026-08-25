using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace IdiotTape.Gameplay
{

    public sealed class GameplayInputRouter : MonoBehaviour
    {

        private const int MouseContactId = -1;

        public event Action<int, Vector2, double> ContactPressed;
        public event Action<int, Vector2> ContactMoved;
        public event Action<int> ContactReleased;
        public event Action<int, double> LanePressed;
        public event Action<int> LaneReleased;
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

        public void SubmitLaneRelease(int laneIndex)
        {

            if (laneIndex < 0 || laneIndex >= 8)
            {

                return;

            }

            LaneReleased?.Invoke(laneIndex);

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

            bool handledTouch = Touch.activeTouches.Count > 0;

            for (int index = 0; index < Touch.activeTouches.Count; index++)
            {

                Touch touch = Touch.activeTouches[index];

                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {

                    ContactPressed?.Invoke(touch.touchId, touch.screenPosition, touch.time);
                    continue;

                }

                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {

                    ContactMoved?.Invoke(touch.touchId, touch.screenPosition);
                    continue;

                }

                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                    touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {

                    ContactReleased?.Invoke(touch.touchId);

                }

            }

            if (!handledTouch && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {

                ContactPressed?.Invoke(
                    MouseContactId,
                    Mouse.current.position.ReadValue(),
                    Mouse.current.lastUpdateTime);

            }

            if (!handledTouch && Mouse.current != null && Mouse.current.leftButton.isPressed)
            {

                ContactMoved?.Invoke(MouseContactId, Mouse.current.position.ReadValue());

            }

            if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {

                ContactReleased?.Invoke(MouseContactId);

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

                else if (WasLaneKeyReleased(keyboard, laneIndex))
                {

                    SubmitLaneRelease(laneIndex);

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

        private static bool WasLaneKeyReleased(Keyboard keyboard, int laneIndex)
        {

            switch (laneIndex)
            {

                case 0:
                    return keyboard.digit1Key.wasReleasedThisFrame || keyboard.numpad1Key.wasReleasedThisFrame;
                case 1:
                    return keyboard.digit2Key.wasReleasedThisFrame || keyboard.numpad2Key.wasReleasedThisFrame;
                case 2:
                    return keyboard.digit3Key.wasReleasedThisFrame || keyboard.numpad3Key.wasReleasedThisFrame;
                case 3:
                    return keyboard.digit4Key.wasReleasedThisFrame || keyboard.numpad4Key.wasReleasedThisFrame;
                case 4:
                    return keyboard.digit5Key.wasReleasedThisFrame || keyboard.numpad5Key.wasReleasedThisFrame;
                case 5:
                    return keyboard.digit6Key.wasReleasedThisFrame || keyboard.numpad6Key.wasReleasedThisFrame;
                case 6:
                    return keyboard.digit7Key.wasReleasedThisFrame || keyboard.numpad7Key.wasReleasedThisFrame;
                case 7:
                    return keyboard.digit8Key.wasReleasedThisFrame || keyboard.numpad8Key.wasReleasedThisFrame;
                default:
                    return false;

            }

        }

    }

}
