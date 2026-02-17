// ============================================================================
// FILE: Visual/CameraController.cs
// PURPOSE: Orthographic camera pan/zoom with map boundary clamping
//          Attach to the Main Camera GameObject
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using Sporefront.Models;

namespace Sporefront.Visual
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        // ================================================================
        // Configuration
        // ================================================================

        [Header("Zoom")]
        public float zoomLevel = 8f;
        public float minZoom = 3f;
        public float maxZoom = 20f;
        public float zoomSpeed = 2f;

        [Header("Pan")]
        public float panSpeed = 1f;

        // ================================================================
        // State
        // ================================================================

        private Camera cam;
        private Bounds mapBounds;
        private bool hasBounds;
        private bool isPanning;
        private Vector3 panOrigin;
        private Vector3 panStartScreenPos;
        private const float PanDragThreshold = 5f; // pixels before pan starts (#13)
        private bool panThresholdMet;

        // Smooth zoom
        private float targetZoom;

        // Smooth focus
        private bool isFocusing;
        private Vector3 focusTarget;
        private float focusZoom;
        private float focusSpeed = 5f;

        // ================================================================
        // Lifecycle
        // ================================================================

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = zoomLevel;
            targetZoom = zoomLevel;

            // Set background to parchment
            cam.backgroundColor = SporefrontColors.ParchmentLight;
        }

        private void Update()
        {
            HandleZoomInput();
            HandlePanInput();
            ApplySmoothZoom();
            ApplySmoothFocus();
            ClampPosition();
        }

        // ================================================================
        // Public API
        // ================================================================

        public void SetMapBounds(int width, int height)
        {
            mapBounds = HexMetrics.GetMapBounds(width, height);
            hasBounds = true;
        }

        /// <summary>
        /// Smoothly move the camera to focus on a hex coordinate.
        /// </summary>
        public void FocusOn(HexCoordinate coord, float zoom = -1f, bool animated = true)
        {
            Vector3 target = HexMetrics.HexToWorldPosition(coord);
            target.z = transform.position.z; // Preserve camera Z

            if (animated)
            {
                isFocusing = true;
                focusTarget = target;
                focusZoom = zoom > 0 ? zoom : targetZoom;
            }
            else
            {
                transform.position = target;
                if (zoom > 0)
                {
                    targetZoom = zoom;
                    zoomLevel = zoom;
                    cam.orthographicSize = zoom;
                }
                isFocusing = false;
            }
        }

        // ================================================================
        // Input Handling
        // ================================================================

        private void HandleZoomInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // Normalize scroll (scroll.y is in pixels, typically ±120)
                targetZoom -= Mathf.Sign(scroll) * zoomSpeed;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }
        }

        /// <summary>
        /// True when the camera is actively panning (drag threshold met). (#13)
        /// </summary>
        public bool IsPanning => isPanning && panThresholdMet;

        private void HandlePanInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector3 mousePos = mouse.position.ReadValue();

            // Right mouse button or middle mouse button to pan
            bool rightDown = mouse.rightButton.wasPressedThisFrame;
            bool middleDown = mouse.middleButton.wasPressedThisFrame;
            bool rightUp = mouse.rightButton.wasReleasedThisFrame;
            bool middleUp = mouse.middleButton.wasReleasedThisFrame;

            if (rightDown || middleDown)
            {
                isPanning = true;
                panThresholdMet = false;
                panStartScreenPos = mousePos;
                panOrigin = cam.ScreenToWorldPoint(mousePos);
            }

            if (rightUp || middleUp)
            {
                isPanning = false;
                panThresholdMet = false;
            }

            if (isPanning)
            {
                // Only start actual panning after threshold (#13)
                if (!panThresholdMet)
                {
                    float dist = Vector3.Distance(mousePos, panStartScreenPos);
                    if (dist >= PanDragThreshold)
                    {
                        panThresholdMet = true;
                        panOrigin = cam.ScreenToWorldPoint(mousePos);
                    }
                    return;
                }

                Vector3 currentMouse = cam.ScreenToWorldPoint(mousePos);
                Vector3 delta = panOrigin - currentMouse;
                transform.position += delta;
            }
        }

        // ================================================================
        // Smooth Movement
        // ================================================================

        private void ApplySmoothZoom()
        {
            float current = cam.orthographicSize;
            if (Mathf.Abs(current - targetZoom) > 0.01f)
            {
                cam.orthographicSize = Mathf.Lerp(current, targetZoom, Time.deltaTime * 10f);
                zoomLevel = cam.orthographicSize;
            }
        }

        private void ApplySmoothFocus()
        {
            if (!isFocusing) return;

            transform.position = Vector3.Lerp(
                transform.position, focusTarget, Time.deltaTime * focusSpeed
            );

            if (focusZoom > 0)
            {
                targetZoom = focusZoom;
            }

            if (Vector3.Distance(transform.position, focusTarget) < 0.05f)
            {
                transform.position = focusTarget;
                isFocusing = false;
            }
        }

        // ================================================================
        // Boundary Clamping
        // ================================================================

        private void ClampPosition()
        {
            if (!hasBounds) return;

            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, mapBounds.min.x, mapBounds.max.x);
            pos.y = Mathf.Clamp(pos.y, mapBounds.min.y, mapBounds.max.y);
            transform.position = pos;
        }
    }
}
