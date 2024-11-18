using System.Collections.Generic;
using System.Threading;
using UnityEngine;

#if USE_TMP
    using TMPro;
#else
    using TextMeshProUGUI = Yarn.Unity.TMPShim;
#endif

#if USE_UNITASK
    using Cysharp.Threading.Tasks;
    using YarnTask = Cysharp.Threading.Tasks.UniTask;
    using YarnOptionTask = Cysharp.Threading.Tasks.UniTask<Yarn.Unity.DialogueOption>;
    using YarnOptionCompletionSource = Cysharp.Threading.Tasks.UniTaskCompletionSource<Yarn.Unity.DialogueOption>;
#elif UNITY_2023_1_OR_NEWER
    using YarnTask = UnityEngine.Awaitable;
    using YarnOptionTask = UnityEngine.Awaitable<Yarn.Unity.DialogueOption>;
    using YarnOptionCompletionSource = UnityEngine.AwaitableCompletionSource<Yarn.Unity.DialogueOption>;
#else
    using YarnTask = System.Threading.Tasks.Task;
    using YarnOptionTask = System.Threading.Tasks.Task<Yarn.Unity.DialogueOption>;
    using YarnOptionCompletionSource = System.Threading.Tasks.TaskCompletionSource<Yarn.Unity.DialogueOption>;
#endif

namespace Yarn.Unity
{
    public class AsyncOptionsView : AsyncDialogueViewBase
    {
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] AsyncOptionItem optionViewPrefab;

        // A cached pool of OptionView objects so that we can reuse them
        List<AsyncOptionItem> optionViews = new List<AsyncOptionItem>();

        [SerializeField] TextMeshProUGUI lastLineText;
        [SerializeField] GameObject lastLineContainer;

        [SerializeField] TextMeshProUGUI lastLineCharacterNameText;
        [SerializeField] GameObject lastLineCharacterNameContainer;
        LocalizedLine lastSeenLine;
        [SerializeField] bool showsLastLine;

        public bool showUnavailableOptions = false;

        public override YarnTask OnDialogueCompleteAsync()
        {
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            return YarnAsync.CompletedTask;
        }

        void Start()
        {
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (lastLineContainer == null && lastLineText != null)
            {
                lastLineContainer = lastLineText.gameObject;
            }
            if (lastLineCharacterNameContainer == null && lastLineCharacterNameText != null)
            {
                lastLineCharacterNameContainer = lastLineCharacterNameText.gameObject;
            }
        }
        public override YarnTask OnDialogueStartedAsync()
        {
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            return YarnAsync.CompletedTask;
        }

        public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            if (showsLastLine)
            {
                lastSeenLine = line;
            }
            return YarnAsync.CompletedTask;
        }

        public override async YarnOptionTask RunOptionsAsync(DialogueOption[] dialogueOptions, CancellationToken cancellationToken)
        {
            // If we don't already have enough option views, create more
            while (dialogueOptions.Length > optionViews.Count)
            {
                var optionView = CreateNewOptionView();
                optionViews.Add(optionView);
            }

            YarnOptionCompletionSource tcs = new YarnOptionCompletionSource();
            var completionCancelSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // tracks the options views created so we can use it to configure the interaction correctly
            int optionViewsCreated = 0;
            for (int i = 0; i < dialogueOptions.Length; i++)
            {
                var optionView = optionViews[i];
                var option = dialogueOptions[i];

                if (option.IsAvailable == false && showUnavailableOptions == false)
                {
                    Debug.Log("option is unavailable, skipping it");
                    continue;
                }
                optionView.gameObject.SetActive(true);
                optionView.Option = option;

                optionView.OnOptionSelected = tcs;
                optionView.completionToken = completionCancelSource.Token;

                // The first available option is selected by default
                if (optionViewsCreated == 0)
                {
                    optionView.Select();
                }
                optionViewsCreated += 1;
            }

            // Update the last line, if one is configured
            if (lastLineContainer != null)
            {
                if (lastSeenLine != null && showsLastLine)
                {
                    // if we have a last line character name container
                    // and the last line has a character then we show the nameplate
                    // otherwise we turn off the nameplate
                    var line = lastSeenLine.Text;
                    if (lastLineCharacterNameContainer != null)
                    {
                        if (string.IsNullOrWhiteSpace(lastSeenLine.CharacterName))
                        {
                            lastLineCharacterNameContainer.SetActive(false);
                        }
                        else
                        {
                            line = lastSeenLine.TextWithoutCharacterName;
                            lastLineCharacterNameContainer.SetActive(true);
                            lastLineCharacterNameText.text = lastSeenLine.CharacterName;
                        }
                    }
                    else
                    {
                        line = lastSeenLine.TextWithoutCharacterName;
                    }

                    lastLineText.text = line.Text;
                    lastLineContainer.SetActive(true);
                }
                else
                {
                    lastLineContainer.SetActive(false);
                }
            }

            // fade up the UI now
            await Effects.FadeAlpha(canvasGroup, 0, 1, 1, cancellationToken);

            // allow interactivity and wait for an option to be selected
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            var completedTask = await tcs.AsTask();
            completionCancelSource.Cancel();

            // now one of the option items has been selected so we do cleanup
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // fade down
            await Effects.FadeAlpha(canvasGroup, 1, 0, 1, cancellationToken);

            // disabling ALL the options views now
            foreach (var optionView in optionViews)
            {
                optionView.gameObject.SetActive(false);
            }
            await YarnAsync.Yield();

            // if we are cancelled we still need to return but we don't want to have a selection, so we return no selected option
            if (cancellationToken.IsCancellationRequested)
            {
                return await YarnAsync.NoOptionSelected;
            }

            // finally we return the selected option
            return completedTask;
        }

        private AsyncOptionItem CreateNewOptionView()
        {
            var optionView = Instantiate(optionViewPrefab);
            optionView.transform.SetParent(canvasGroup.transform, false);
            optionView.transform.SetAsLastSibling();
            optionView.gameObject.SetActive(false);

            return optionView;
        }
    }
}
