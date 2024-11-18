/*
Yarn Spinner is licensed to you under the terms found in the file LICENSE.md.
*/

using UnityEngine;
using UnityEngine.EventSystems;

#if USE_TMP
    using TMPro;
#else
    using TextMeshProUGUI = Yarn.Unity.TMPShim;
#endif

#if USE_UNITASK
    using YarnOptionCompletionSource = Cysharp.Threading.Tasks.UniTaskCompletionSource<Yarn.Unity.DialogueOption>;
#elif UNITY_2023_1_OR_NEWER
    using YarnOptionCompletionSource = UnityEngine.AwaitableCompletionSource<Yarn.Unity.DialogueOption>;
#else
    using YarnOptionCompletionSource = System.Threading.Tasks.TaskCompletionSource<Yarn.Unity.DialogueOption>;
#endif

namespace Yarn.Unity
{
    public class AsyncOptionItem : UnityEngine.UI.Selectable, ISubmitHandler, IPointerClickHandler, IPointerEnterHandler
    {
        [SerializeField] TextMeshProUGUI text;

        public YarnOptionCompletionSource OnOptionSelected;
        public System.Threading.CancellationToken completionToken;

        private bool hasSubmittedOptionSelection = false;

        private DialogueOption _option;
        public DialogueOption Option
        {
            get => _option;

            set
            {
                _option = value;

                hasSubmittedOptionSelection = false;

                // When we're given an Option, use its text and update our
                // interactibility.
                text.text = value.Line.TextWithoutCharacterName.Text;
                interactable = value.IsAvailable;
            }
        }

        // If we receive a submit or click event, invoke our "we just selected this option" handler.
        public void OnSubmit(BaseEventData eventData)
        {
            InvokeOptionSelected();
        }

        public void InvokeOptionSelected()
        {
            Debug.Log($"{name}:{_option.DialogueOptionID} has been selected!");
            // turns out that Selectable subclasses aren't intrinsically interactive/non-interactive
            // based on their canvasgroup, you still need to check at the moment of interaction
            if (!IsInteractable())
            {
                return;
            }

            // We only want to invoke this once, because it's an error to
            // submit an option when the Dialogue Runner isn't expecting it. To
            // prevent this, we'll only invoke this if the flag hasn't been cleared already.
            if (hasSubmittedOptionSelection == false && !completionToken.IsCancellationRequested)
            {
                hasSubmittedOptionSelection = true;
                OnOptionSelected.SetResult(this.Option);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            InvokeOptionSelected();
        }

        // If we mouse-over, we're telling the UI system that this element is
        // the currently 'selected' (i.e. focused) element. 
        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.Select();
        }
    }
}
