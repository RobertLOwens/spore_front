// ============================================================================
// FILE: Visual/SporefrontPanel.cs
// PURPOSE: Base class for all popup/modal panels. Centralizes backdrop
//          creation, visibility checks, fade animations, and content clearing.
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Sporefront.Visual
{
    public abstract class SporefrontPanel : MonoBehaviour
    {
        // ================================================================
        // Core References
        // ================================================================

        protected GameObject backdrop;
        protected CanvasGroup backdropCG;
        protected RectTransform panelRT;
        protected RectTransform contentRT;
        protected Guid localPlayerID;

        // Fade state
        private Coroutine fadeCoroutine;

        // ================================================================
        // Visibility
        // ================================================================

        public bool IsVisible => backdrop != null && backdrop.activeSelf;

        // ================================================================
        // Lifecycle Helpers
        // ================================================================

        /// <summary>
        /// Creates the standard backdrop (full-screen semi-transparent overlay with click-to-close).
        /// Call from Initialize() or BuildContent() before creating panel content.
        /// </summary>
        protected GameObject CreateBackdrop(Transform parent, float alpha = 0.4f, bool withFade = false)
        {
            backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(parent, false);
            var rt = backdrop.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = backdrop.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, alpha);
            backdrop.SetActive(false);

            // Click-to-close
            var btn = backdrop.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(OnBackdropClicked);

            if (withFade)
            {
                backdropCG = backdrop.AddComponent<CanvasGroup>();
                backdropCG.alpha = 0f;
            }

            return backdrop;
        }

        /// <summary>
        /// Override to customize what happens when the backdrop is clicked.
        /// Default behavior calls Hide().
        /// </summary>
        protected virtual void OnBackdropClicked()
        {
            Hide();
        }

        // ================================================================
        // Show / Hide
        // ================================================================

        public virtual void Show()
        {
            if (backdrop == null) return;
            backdrop.SetActive(true);
            if (backdropCG != null)
                FadeIn();
        }

        public virtual void Hide()
        {
            if (backdrop == null) return;
            if (backdropCG != null)
                FadeOut();
            else
                backdrop.SetActive(false);
        }

        // ================================================================
        // Fade Animations
        // ================================================================

        protected void FadeIn()
        {
            if (backdropCG == null) return;
            backdrop.SetActive(true);
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(UIHelper.FadeIn(backdropCG));
        }

        protected void FadeOut(Action onComplete = null)
        {
            if (backdropCG == null)
            {
                if (backdrop != null) backdrop.SetActive(false);
                onComplete?.Invoke();
                return;
            }
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeOutCoroutine(onComplete));
        }

        private IEnumerator FadeOutCoroutine(Action onComplete)
        {
            yield return UIHelper.FadeOut(backdropCG);
            if (backdrop != null) backdrop.SetActive(false);
            onComplete?.Invoke();
        }

        // ================================================================
        // Content Management
        // ================================================================

        /// <summary>
        /// Destroys all children of contentRT. Safe to call when contentRT is null.
        /// </summary>
        protected void ClearContent()
        {
            if (contentRT == null) return;
            for (int i = contentRT.childCount - 1; i >= 0; i--)
                Destroy(contentRT.GetChild(i).gameObject);
        }

        // ================================================================
        // Player Tracking
        // ================================================================

        public void UpdateLocalPlayerID(Guid playerID)
        {
            localPlayerID = playerID;
        }
    }
}
