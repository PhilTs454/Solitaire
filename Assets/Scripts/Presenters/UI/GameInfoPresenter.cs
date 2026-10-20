using DG.Tweening;
using Solitaire.Services;
using TMPro;
using UniRx;
using UnityEngine;
using Zenject;

namespace Solitaire.Presenters
{
    public class GameInfoPresenter : OrientationAwarePresenter, IScoreHud
    {
        [SerializeField] private TextMeshProUGUI _labelPoints;
        [SerializeField] private TextMeshProUGUI _labelMoves;
        [SerializeField] private RectTransform _rectPoints;
        [SerializeField] private RectTransform _rectTime;
        [SerializeField] private RectTransform _rectMoves;

        [Tooltip("Seconds for the score to roll up to its new value.")]
        [SerializeField]
        private float pointsRollDuration = 0.4f;

        [Tooltip("Gains this big or bigger also punch the label.")]
        [SerializeField]
        private int pointsPunchThreshold = 25;

        [SerializeField]
        private float pointsPunchScale = 1.18f;

        [Inject] private readonly IMovesService _movesService;

        [Inject] private readonly IPointsService _pointsService;

        [Inject] private readonly IScorePopup _scorePopup;

        [Inject(Optional = true)]
        private readonly ScorePopupTune _scorePopupTune;

        [Inject(Optional = true, Id = "ScorePopupFont")]
        private readonly TMP_FontAsset _scorePopupFont;

        public RectTransform PointsRect =>
            _labelPoints != null ? _labelPoints.rectTransform : _rectPoints;

        private int shownPoints;
        private Tweener pointsRoll;

        protected override void Start()
        {
            base.Start();

            ApplyHudLook(_labelPoints, isScore: true);
            ApplyHudLook(_labelMoves, isScore: false);

            shownPoints = _pointsService.Points.Value;
            _labelPoints.text = shownPoints.ToString();

            _pointsService.Points.Subscribe(OnPointsChanged).AddTo(this);
            _movesService.Moves.SubscribeToText(_labelMoves).AddTo(this);

            if (_scorePopup != null)
                _scorePopup.Committed += OnScoreCommitted;
        }

        private void OnDestroy()
        {
            if (_scorePopup != null)
                _scorePopup.Committed -= OnScoreCommitted;
        }

        /// <summary>
        /// Real score updates instantly. HUD only snaps for undo / new game.
        /// Gains wait for each +N pop to peak.
        /// </summary>
        private void OnPointsChanged(int points)
        {
            if (points <= shownPoints)
                SnapPoints(points);
        }

        /// <summary>One pop reached full size — roll that chunk into the counter.</summary>
        private void OnScoreCommitted(int delta)
        {
            if (delta <= 0)
                return;

            var truth = _pointsService.Points.Value;
            var target = Mathf.Min(shownPoints + delta, truth);
            if (target <= shownPoints)
                return;

            RollPoints(target);
        }

        private void SnapPoints(int points)
        {
            pointsRoll?.Kill();
            shownPoints = points;

            if (_labelPoints != null)
                _labelPoints.text = points.ToString();
        }

        /// <summary>Rolls the number up, and punches it on a big gain.</summary>
        private void RollPoints(int points)
        {
            pointsRoll?.Kill();

            var from = shownPoints;
            shownPoints = points;

            if (_labelPoints == null)
                return;

            if (from == points)
            {
                _labelPoints.text = points.ToString();
                return;
            }

            pointsRoll = DOTween
                .To(() => from, v => _labelPoints.text = v.ToString(), points, pointsRollDuration)
                .SetEase(Ease.OutCubic)
                .SetTarget(_labelPoints);

            if (points - from >= pointsPunchThreshold)
            {
                var t = _labelPoints.transform;
                t.DOKill();
                t.localScale = Vector3.one;
                t.DOPunchScale(Vector3.one * (pointsPunchScale - 1f), pointsRollDuration, 6, 0.6f);
            }
        }

        protected override void OnOrientationChanged(bool isLandscape)
        {
            // Portrait: Moves top-left, Points top-right (matches CanvasGameInfo layout).
            // Landscape: Moves top-left, Points bottom-left.
            if (isLandscape)
            {
                PlaceHudCorner(
                    _rectMoves,
                    RectTransform.Edge.Left,
                    RectTransform.Edge.Top,
                    50f,
                    50f
                );
                PlaceHudCorner(
                    _rectPoints,
                    RectTransform.Edge.Left,
                    RectTransform.Edge.Bottom,
                    50f,
                    120f
                );
            }
            else
            {
                PlaceHudCorner(
                    _rectMoves,
                    RectTransform.Edge.Left,
                    RectTransform.Edge.Top,
                    0f,
                    0f
                );
                PlaceHudCorner(
                    _rectPoints,
                    RectTransform.Edge.Right,
                    RectTransform.Edge.Top,
                    0f,
                    0f
                );
            }
        }

        private static void PlaceHudCorner(
            RectTransform rect,
            RectTransform.Edge horizontal,
            RectTransform.Edge vertical,
            float horizontalInset,
            float verticalInset
        )
        {
            if (rect == null)
                return;

            var pivot = new Vector2(
                horizontal == RectTransform.Edge.Left ? 0f : 1f,
                vertical == RectTransform.Edge.Bottom ? 0f : 1f
            );
            rect.anchorMin = pivot;
            rect.anchorMax = pivot;
            rect.pivot = pivot;
            rect.SetInsetAndSizeFromParentEdge(horizontal, horizontalInset, rect.sizeDelta.x);
            rect.SetInsetAndSizeFromParentEdge(vertical, verticalInset, rect.sizeDelta.y);
        }

        /// <summary>
        /// Same candy font as the +N pops. Score is quiet gold; moves are cooler cream.
        /// Uses a per-label material so we don't change Baron Neue for the rest of the game.
        /// </summary>
        private void ApplyHudLook(TextMeshProUGUI label, bool isScore)
        {
            if (label == null)
                return;

            var tune = _scorePopupTune != null ? _scorePopupTune : new ScorePopupTune();

            if (_scorePopupFont != null)
                label.font = _scorePopupFont;

            Color top;
            Color bottom;
            Color shade;
            if (isScore)
            {
                // Card-pop gold, not the hot streak shout.
                top = tune.goldTop;
                bottom = tune.goldBottom;
                shade = tune.goldShade;
            }
            else
            {
                var cream = new Color(1f, 0.95f, 0.88f, 1f);
                var tan = new Color(0.86f, 0.7f, 0.46f, 1f);
                top = Color.Lerp(tune.goldTop, cream, 0.5f);
                bottom = Color.Lerp(tune.goldBottom, tan, 0.4f);
                shade = new Color(0.28f, 0.12f, 0.05f, 0.55f);
            }

            label.color = Color.white;
            label.enableVertexGradient = true;
            label.colorGradient = new VertexGradient(top, top, bottom, bottom);
            label.outlineWidth = 0f;
            label.extraPadding = true;

            var mat = label.fontMaterial;
            if (mat == null)
                return;

            var dilate = isScore
                ? Mathf.Clamp(tune.faceDilate * 0.75f, 0f, 0.45f)
                : Mathf.Clamp(tune.faceDilate * 0.55f, 0f, 0.35f);

            mat.DisableKeyword("OUTLINE_ON");
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetFloat("_OutlineWidth", 0f);
            mat.SetFloat("_FaceDilate", dilate);
            mat.SetColor("_UnderlayColor", shade);
            mat.SetFloat("_UnderlayOffsetX", Mathf.Clamp(tune.shadeOffsetX, -1f, 1f));
            mat.SetFloat("_UnderlayOffsetY", Mathf.Clamp(tune.shadeOffsetY, -1f, 1f));
            mat.SetFloat("_UnderlayDilate", Mathf.Clamp01(tune.shadeDilate));
            mat.SetFloat("_UnderlaySoftness", Mathf.Clamp01(tune.shadeSoftness));
            label.UpdateMeshPadding();
        }
    }
}
