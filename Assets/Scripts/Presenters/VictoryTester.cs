#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Solitaire.Models;
using UnityEngine;
using Zenject;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Editor / Dev hotkeys to trigger the win cascade without finishing a deal.
    /// Spawned from GameInstaller — no scene wiring.
    /// V force win, B bubble only, Shift+V stage preview, L one legal move, Esc home.
    /// </summary>
    public sealed class VictoryTester : MonoBehaviour
    {
        [Inject]
        private readonly Game _game;

        [Inject]
        private readonly GameState _gameState;

        private void Update()
        {
            var shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (Input.GetKeyDown(KeyCode.V) && !shift)
                ForceVictory();

            if (Input.GetKeyDown(KeyCode.V) && shift)
                PreviewStage();

            if (Input.GetKeyDown(KeyCode.B) && !shift)
                PreviewBubble();

            if (Input.GetKeyDown(KeyCode.L) && !shift)
                SetupOneMove();

            if (Input.GetKeyDown(KeyCode.Escape))
                GoHome();
        }

        [ContextMenu("Last Move")]
        public void SetupOneMove()
        {
            if (_game == null)
                return;

            Debug.Log("[VictoryTester] One legal move, score 500");
            _game.SetupOneMoveFromVictory();
        }

        [ContextMenu("Force Victory")]
        public void ForceVictory()
        {
            if (_game == null)
                return;

            Debug.Log("[VictoryTester] Force Victory");
            _game.ForceVictory();
        }

        [ContextMenu("Preview Stage")]
        public void PreviewStage()
        {
            if (_game == null)
                return;

            Debug.Log("[VictoryTester] Preview Stage");
            _game.PreviewVictoryStage();
        }

        [ContextMenu("Preview Bubble")]
        public void PreviewBubble()
        {
            if (_game == null)
                return;

            Debug.Log("[VictoryTester] Preview Bubble");
            _game.PreviewVictoryBubble();
        }

        [ContextMenu("Go Home")]
        public void GoHome()
        {
            if (_game == null)
            {
                if (_gameState != null)
                    _gameState.State.Value = Game.State.Home;
                return;
            }

            Debug.Log("[VictoryTester] Home");
            _game.RequestHome();
        }
    }
}
#endif
