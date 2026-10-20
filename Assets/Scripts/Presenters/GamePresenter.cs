using System.Linq;
using Solitaire.Models;
using Solitaire.Services;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Solitaire.Presenters
{
    public class GamePresenter : MonoBehaviour
    {
        private const float CamSizeLandscape = 4.25f;
        private const float CamSizePortrait = 8.25f;

        [SerializeField]
        private Physics2DRaycaster _cardRaycaster;

        [SerializeField]
        private PilePresenter _pileStock;

        [SerializeField]
        private PilePresenter _pileWaste;

        [SerializeField]
        private PilePresenter[] _pileFoundations;

        [SerializeField]
        private PilePresenter[] _pileTableaus;

        [Inject]
        private readonly IAudioService _audioService;

        [Inject]
        private readonly Game _game;

        [Inject]
        private readonly GameState _gameState;

        [Inject]
        private readonly OrientationState _orientation;

        private Camera _camera;
        private int _layerInteractable;

        private void Awake()
        {
            _camera = Camera.main;
            _layerInteractable = LayerMask.NameToLayer("Interactable");
        }

        private void Start()
        {
            ResolvePileRefs();

            if (!CanInitGame())
            {
                Debug.LogError(
                    "[GamePresenter] Missing pile references — cannot Init. "
                        + "Check Stock/Waste/Foundation/Tableau prefab instances in the Game scene."
                );
                return;
            }

            // Update camera on orientation change
            _orientation.State.Subscribe(AdjustCamera).AddTo(this);

            // Handle game state change
            _gameState.State.Pairwise().Subscribe(HandleGameStateChanges).AddTo(this);

            // Initialize game
            _game.Init(
                _pileStock.Pile,
                _pileWaste.Pile,
                _pileFoundations.Select(p => p.Pile).ToList(),
                _pileTableaus.Select(p => p.Pile).ToList()
            );

            SetCameraLayers(true);
        }

        private void Update()
        {
            // Detect win condition
            if (_gameState.State.Value == Game.State.Playing)
                _game.DetectWinCondition();
        }

        private bool CanInitGame()
        {
            return _game != null
                && _pileStock != null
                && _pileWaste != null
                && _pileFoundations != null
                && _pileFoundations.Length == 4
                && _pileFoundations.All(p => p != null)
                && _pileTableaus != null
                && _pileTableaus.Length == 7
                && _pileTableaus.All(p => p != null);
        }

        /// <summary>
        /// After a wiped Pile.prefab, Unity can leave GamePresenter refs as Missing.
        /// Re-bind from live scene instances when needed.
        /// </summary>
        private void ResolvePileRefs()
        {
            if (CanInitGame())
                return;

            var piles = FindObjectsOfType<PilePresenter>(true);
            if (piles == null || piles.Length == 0)
                return;

            if (_pileStock == null)
                _pileStock = piles.FirstOrDefault(p => p.PileType == Pile.PileType.Stock);

            if (_pileWaste == null)
                _pileWaste = piles.FirstOrDefault(p => p.PileType == Pile.PileType.Waste);

            if (
                _pileFoundations == null
                || _pileFoundations.Length != 4
                || _pileFoundations.Any(p => p == null)
            )
            {
                _pileFoundations = piles
                    .Where(p => p.PileType == Pile.PileType.Foundation)
                    .OrderBy(p => p.name)
                    .ToArray();
            }

            if (
                _pileTableaus == null
                || _pileTableaus.Length != 7
                || _pileTableaus.Any(p => p == null)
            )
            {
                _pileTableaus = piles
                    .Where(p => p.PileType == Pile.PileType.Tableau)
                    .OrderBy(p => p.name)
                    .ToArray();
            }
        }

        private void AdjustCamera(Orientation orientation)
        {
            _camera.orthographicSize =
                orientation == Orientation.Landscape ? CamSizeLandscape : CamSizePortrait;
        }

        private void HandleGameStateChanges(Pair<Game.State> state)
        {
            if (state.Previous == Game.State.Home)
            {
                // Render everything and play music
                SetCameraLayers(false);
                _audioService.PlayMusic(Audio.Music, 0.3333f);
            }
            else if (state.Current == Game.State.Home)
            {
                // Cull game elements and stop music
                SetCameraLayers(true);
                _audioService.StopMusic();
            }

            // Enable card interactions only while playing
            _cardRaycaster.enabled = state.Current == Game.State.Playing;
        }

#if UNITY_EDITOR
        [ContextMenu("Victory/One Legal Move")]
        private void ContextOneLegalMove()
        {
            _game.SetupOneMoveFromVictory();
        }

        [ContextMenu("Victory/Force Victory")]
        private void ContextForceVictory()
        {
            _game.ForceVictory();
        }

        [ContextMenu("Victory/Preview Stage")]
        private void ContextPreviewStage()
        {
            _game.PreviewVictoryStage();
        }

        [ContextMenu("Victory/Go Home")]
        private void ContextGoHome()
        {
            _game.RequestHome();
        }
#endif

        private void SetCameraLayers(bool cullGame)
        {
            if (cullGame)
                // Every layer except Interactable
                _camera.cullingMask = ~(1 << _layerInteractable);
            else
                // Everything
                _camera.cullingMask = ~0;
        }
    }
}
