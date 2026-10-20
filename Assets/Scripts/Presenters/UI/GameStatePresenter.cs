using Solitaire.Helpers;
using Solitaire.Models;
using UnityEngine;
using Zenject;

namespace Solitaire.Presenters
{
    [RequireComponent(typeof(Canvas))]
    public class GameStatePresenter : StateReactor<Game.State>
    {
        [Inject] private readonly GameState _gameState;

        private Canvas _canvas;

        protected override StateModel<Game.State> Model => _gameState;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();

            // Old "You won!" title used to appear as soon as State.Win started.
            // The new sequence owns the result frame; keep this canvas retired.
            if (name == "CanvasWin")
            {
                if (_canvas != null)
                    _canvas.enabled = false;
                gameObject.SetActive(false);
            }
        }

        protected override void SetVisibility(bool isVisible)
        {
            if (name == "CanvasWin")
                isVisible = false;

            _canvas.enabled = isVisible;
        }
    }
}