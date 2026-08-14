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
        public event Action RestartRequested;
        public event Action PauseRequested;

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

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {

                RestartRequested?.Invoke();

            }

            if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            {

                PauseRequested?.Invoke();

            }

        }

    }

}
