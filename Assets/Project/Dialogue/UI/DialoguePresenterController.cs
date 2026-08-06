using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;
using JRPG.Services;

namespace JRPG.Dialogue.UI
{
    /// <summary>
    /// Renders the active dialogue: speaker bust + name plate + token-resolved body, with either an
    /// advance prompt (linear nodes) or a choice stack (choice nodes).
    ///
    /// <para><b>A MenuController subclass.</b> Choice rows are menu rows, so navigation, disabled-row
    /// skipping, focus, scroll-into-view and the prompt bar all come from the shared substrate rather
    /// than being reimplemented here — this class previously carried its own copy of all of it.</para>
    ///
    /// <para><b>Presentation is data, not a switch.</b> Everything that differs between importance
    /// levels — which of the three routes a line takes, framing, portrait, typewriter speed,
    /// auto-advance, whether skipping is allowed — comes from
    /// <see cref="DialoguePresentationProfile"/>. The three routes are the passive banner (handled
    /// elsewhere, by <see cref="PassiveDialoguePresenter"/>), the notice card, and the dialogue box.</para>
    /// </summary>
    public sealed class DialoguePresenterController : MenuController
    {
        [Header("Dialogue box")]
        [Tooltip("Everything that shifts left in combat so the box does not cover the party HUD corner.")]
        [SerializeField] private RectTransform boxRoot;

        [Tooltip("Name plate container. Hidden for lines with no speaker (system notices).")]
        [SerializeField] private GameObject namePlateRoot;
        [SerializeField] private TMP_Text speakerName;
        [SerializeField] private TMP_Text body;
        [SerializeField] private GameObject continuePrompt;

        [Tooltip("Heavier border shown only for a framed importance (Critical).")]
        [SerializeField] private Image frameImage;

        [Header("Speaker bust")]
        [SerializeField] private GameObject portraitRoot;
        [SerializeField] private Image portraitImage;

        [Header("Choices")]
        [Tooltip("The choice stack, shown only on choice nodes. Separate from the body box.")]
        [SerializeField] private GameObject choiceStackRoot;

        [Header("Notice card (Tutorial / System)")]
        [SerializeField] private NoticeCardController noticeCard;

        [Tooltip("Hint line on the notice card.")]
        [SerializeField] private string noticeCardHint = "Confirm to continue";

        [Header("Presentation")]
        [Tooltip("Importance → presentation mapping.")]
        [SerializeField] private DialoguePresentationProfile presentationProfile;

        [Tooltip("Pixels to pull the box's right edge in while in combat, clearing the party HUD corner.")]
        [SerializeField] private float combatRightInset = 420f;

        private IEventBus _bus;

        /// Right edge of boxRoot as authored, so the combat inset is applied against a stable baseline
        /// rather than compounding every time a node is entered.
        private float _authoredBoxRightOffset;
        private bool _capturedBoxOffset;

        private Color _defaultSpeakerColor = Color.white;
        private bool _capturedDefaultColor;

        private DialoguePresentationProfile.PresentationEntry _entry;

        private Coroutine _reveal;
        private Coroutine _autoAdvance;

        /// True while the typewriter is still revealing. Submit completes the reveal instead of advancing.
        private bool _revealing;

        private DialogueService Dialogue
            => AppContext.Services != null && AppContext.Services.TryResolve<IDialogueService>(out var d)
                ? d as DialogueService
                : null;

        // ---- Lifecycle ----------------------------------------------------------------------------

        protected override void OnEnable()
        {
            _bus = AppContext.Bus;
            _bus?.Subscribe<DialogueNodeEntered>(OnNodeEntered);

            // Base builds the choice rows and hooks input; the non-row visuals follow.
            base.OnEnable();
            RefreshPresentation();
        }

        protected override void OnDisable()
        {
            _bus?.Unsubscribe<DialogueNodeEntered>(OnNodeEntered);
            StopReveal();
            StopAutoAdvance();
            base.OnDisable();
        }

        private void OnNodeEntered(DialogueNodeEntered e)
        {
            RebuildAndFocus();
            RefreshPresentation();
        }

        // ---- Choice rows --------------------------------------------------------------------------

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();

            var d = Dialogue;
            if (d == null || !d.IsDialogueActive) return rows;

            var snap = d.GetCurrentRenderSnapshot();
            if (snap == null || !snap.hasChoices) return rows;

            foreach (var c in d.GetCurrentChoices())
            {
                rows.Add(new RowModel
                {
                    id = c.choiceId,
                    label = c.text,
                    enabled = c.available,
                    // The domain already knows why a choice is closed off; an unexplained greyed-out
                    // line reads as a bug rather than as a locked path.
                    disabledReason = c.available ? null : c.unavailableReason,
                    action = new ChooseDialogueAction(c.choiceId),
                    context = Context,
                });
            }

            return rows;
        }

        // ---- Presentation -------------------------------------------------------------------------

        private void RefreshPresentation()
        {
            var d = Dialogue;
            if (d == null || !d.IsDialogueActive) return;

            var snap = d.GetCurrentRenderSnapshot();
            if (snap == null) return;

            _entry = presentationProfile != null
                ? presentationProfile.Resolve(snap.importance)
                : new DialoguePresentationProfile.PresentationEntry { importance = snap.importance, showsPortrait = true, allowsSkip = true };

            bool hasChoices = snap.hasChoices;
            string speaker = snap.speaker.displayName ?? string.Empty;
            string text = snap.body ?? string.Empty;

            // Route. Tutorial/System are not conversation, so they take the card instead of the box —
            // the choice stack still rides on top when such a node asks something.
            bool card = _entry.usesNoticeCard && noticeCard != null;

            ApplyBoxInset();
            ApplyEmphasis(snap.importance, speaker, card);
            ApplyPortrait(snap.speaker.portraitId);

            // The name plate is a sibling of the box, not a child, so that it can straddle the box's
            // top edge — which means hiding the box does not hide it. Both go together, or the plate
            // is left floating over the notice card's dimmed backdrop with nothing under it.
            SetActive(boxRoot != null ? boxRoot.gameObject : null, !card);
            if (frameImage != null) frameImage.enabled = !card && _entry.framed;

            if (card) noticeCard.Show(string.IsNullOrEmpty(speaker) ? "NOTICE" : speaker, text,
                                      hasChoices ? null : noticeCardHint);
            else noticeCard?.Hide();

            SetActive(choiceStackRoot, hasChoices);
            SetActive(continuePrompt, !hasChoices && !card);

            // The card renders its own body; the box's typewriter would fight it.
            if (card) BeginBody(text, instant: true);
            else BeginBody(text, instant: _entry.typewriterCharsPerSecond <= 0f);

            RefreshPrompts();
        }

        /// <summary>
        /// Name plate + emphasis colour. An empty speaker hides the plate entirely rather than leaving
        /// a floating empty bar over the box.
        /// </summary>
        private void ApplyEmphasis(DialogueImportance importance, string speaker, bool routedToCard)
        {
            // Hidden on the card route (the card carries its own heading) and whenever there is no
            // speaker at all, rather than leaving an empty bar over the box.
            SetActive(namePlateRoot, !routedToCard && !string.IsNullOrEmpty(speaker));

            if (speakerName == null) return;

            if (!_capturedDefaultColor) { _defaultSpeakerColor = speakerName.color; _capturedDefaultColor = true; }
            speakerName.text = speaker;
            speakerName.color = Emphasize(_entry.emphasisKey, importance, _defaultSpeakerColor);
        }

        /// <summary>
        /// Binds the speaker bust. <c>portraitId</c> is a character id resolved by
        /// <see cref="SpeakerResolver"/>; the sprite itself lives on <see cref="CharacterData"/>, so a
        /// character with no authored art simply shows no bust and the name plate carries the identity.
        /// </summary>
        private void ApplyPortrait(string portraitId)
        {
            Sprite sprite = null;

            if (_entry.showsPortrait && !string.IsNullOrEmpty(portraitId)
                && AppContext.Data is DataRegistry data
                && data.TryGet<CharacterData>(portraitId, out var charData) && charData != null)
                sprite = charData.portraitSprite;

            if (portraitImage != null)
            {
                portraitImage.sprite = sprite;
                portraitImage.enabled = sprite != null;
                portraitImage.preserveAspect = true;
            }

            SetActive(portraitRoot, sprite != null);
        }

        /// <summary>
        /// Pulls the box in from the right during combat so it cannot sit under the bottom-right party
        /// HUD. Both registry entries share this prefab, so the layout difference has to be decided
        /// here rather than by having two prefabs drift apart.
        /// </summary>
        private void ApplyBoxInset()
        {
            if (boxRoot == null) return;

            if (!_capturedBoxOffset) { _authoredBoxRightOffset = boxRoot.offsetMax.x; _capturedBoxOffset = true; }

            bool inCombat = AppContext.State != null && AppContext.State.Current.Mode == GameMode.Combat;

            var offset = boxRoot.offsetMax;
            offset.x = inCombat ? _authoredBoxRightOffset - Mathf.Max(0f, combatRightInset) : _authoredBoxRightOffset;
            boxRoot.offsetMax = offset;
        }

        private static Color Emphasize(string emphasisKey, DialogueImportance importance, Color fallback)
        {
            string key = string.IsNullOrEmpty(emphasisKey) ? importance.ToString().ToLowerInvariant() : emphasisKey.ToLowerInvariant();

            switch (key)
            {
                case "critical": return new Color(1f, 0.55f, 0.45f);
                case "system":   return new Color(0.72f, 0.74f, 0.8f);
                case "tutorial": return new Color(0.55f, 0.85f, 1f);
                default:         return fallback;
            }
        }

        // ---- Typewriter ---------------------------------------------------------------------------

        /// <summary>
        /// Sets the body and starts the reveal. The full string is assigned up front and
        /// <c>maxVisibleCharacters</c> does the revealing, so the text never reflows mid-line — building
        /// the string a character at a time makes wrapping jump as words complete.
        /// </summary>
        private void BeginBody(string text, bool instant)
        {
            StopReveal();
            StopAutoAdvance();

            if (body == null) return;

            body.text = text ?? string.Empty;
            body.ForceMeshUpdate();

            int total = body.textInfo != null ? body.textInfo.characterCount : (text?.Length ?? 0);

            if (instant || total <= 0 || !isActiveAndEnabled || !Application.isPlaying)
            {
                body.maxVisibleCharacters = int.MaxValue;
                _revealing = false;
                ScheduleAutoAdvance();
                return;
            }

            body.maxVisibleCharacters = 0;
            _revealing = true;
            _reveal = StartCoroutine(RevealRoutine(total, _entry.typewriterCharsPerSecond));
        }

        private IEnumerator RevealRoutine(int total, float charsPerSecond)
        {
            float shown = 0f;
            float rate = Mathf.Max(1f, charsPerSecond);

            while (shown < total)
            {
                // Unscaled: dialogue routinely runs while the rest of the game is paused.
                shown += Time.unscaledDeltaTime * rate;
                body.maxVisibleCharacters = Mathf.Clamp(Mathf.FloorToInt(shown), 0, total);
                yield return null;
            }

            CompleteReveal();
        }

        /// <summary>Jump to the fully-revealed line. Safe to call when nothing is revealing.</summary>
        private void CompleteReveal()
        {
            StopReveal();

            if (body != null) body.maxVisibleCharacters = int.MaxValue;
            _revealing = false;

            ScheduleAutoAdvance();
            RefreshPrompts();
        }

        private void StopReveal()
        {
            if (_reveal != null) { StopCoroutine(_reveal); _reveal = null; }
            _revealing = false;
        }

        // ---- Auto-advance -------------------------------------------------------------------------

        /// <summary>
        /// Starts the hands-off advance timer for a fully-revealed linear line. Never runs on a choice
        /// node: advancing past a question the player has not answered would decide it for them.
        /// </summary>
        private void ScheduleAutoAdvance()
        {
            StopAutoAdvance();

            if (_entry.autoAdvanceSeconds <= 0f) return;
            if (!isActiveAndEnabled || !Application.isPlaying) return;

            var d = Dialogue;
            var snap = d?.GetCurrentRenderSnapshot();
            if (snap == null || snap.hasChoices) return;

            _autoAdvance = StartCoroutine(AutoAdvanceRoutine(_entry.autoAdvanceSeconds));
        }

        private IEnumerator AutoAdvanceRoutine(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            _autoAdvance = null;
            Dialogue?.Advance();
        }

        private void StopAutoAdvance()
        {
            if (_autoAdvance == null) return;
            StopCoroutine(_autoAdvance);
            _autoAdvance = null;
        }

        // ---- Input --------------------------------------------------------------------------------

        protected override void OnSubmit(InputAction.CallbackContext ctx)
        {
            // The press that opened this conversation is not an answer to it — gamepad buttonSouth is
            // both Exploration/Interact and Menu/Submit.
            if (MenuInputMap.IsStalePress(ctx, ListeningSince)) return;

            var d = Dialogue;
            if (d == null || !d.IsDialogueActive) return;

            // First press finishes the line, second acts on it. A line that forbids skipping ignores
            // the first press entirely and simply waits.
            if (_revealing)
            {
                if (_entry.allowsSkip && !_entry.blocksInput) CompleteReveal();
                return;
            }

            var snap = d.GetCurrentRenderSnapshot();

            if (snap != null && snap.hasChoices) ConfirmChoice();
            else d.Advance();
        }

        /// <summary>
        /// Runs the focused choice.
        ///
        /// <para>Deliberately not <c>MenuController.ExecuteRow</c>: that rebuilds the row list after
        /// executing, and a choice can end the conversation, which closes this menu and destroys the
        /// canvas mid-call. The refresh instead arrives through <c>DialogueNodeEntered</c>, which only
        /// fires when there is a next node to show.</para>
        /// </summary>
        private void ConfirmChoice()
        {
            if (populator == null) return;

            int index = HighlightedIndex;
            if (index < 0 || index >= populator.ActiveRows.Count) return;

            var model = populator.ActiveRows[index].Model;
            if (!model.enabled) return;

            model.action?.Execute(model.context ?? Context);
        }

        /// <summary>
        /// Dialogue cannot be cancelled — a conversation ends by being played out, not by backing out
        /// of it. Overridden so the base does not close the presenter behind the service's back, which
        /// would leave <c>DialogueService</c> holding a live session with no UI.
        /// </summary>
        protected override void OnCancel(InputAction.CallbackContext ctx) { }

        // ---- Prompts ------------------------------------------------------------------------------

        public override IReadOnlyList<InputPrompt> Prompts
        {
            get
            {
                var d = Dialogue;
                var snap = d?.GetCurrentRenderSnapshot();

                if (_revealing && _entry.allowsSkip && !_entry.blocksInput) return FastForwardPrompts;
                if (snap != null && snap.hasChoices) return ChoicePrompts;

                return AdvancePrompts;
            }
        }

        private static readonly InputPrompt[] AdvancePrompts =
        {
            new InputPrompt("Submit", "Continue"),
        };

        private static readonly InputPrompt[] FastForwardPrompts =
        {
            new InputPrompt("Submit", "Skip"),
        };

        private static readonly InputPrompt[] ChoicePrompts =
        {
            new InputPrompt("Navigate", "Select"),
            new InputPrompt("Submit", "Choose"),
        };

        // ---- Helpers ------------------------------------------------------------------------------

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }
    }
}
