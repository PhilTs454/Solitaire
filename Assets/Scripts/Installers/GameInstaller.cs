using DG.Tweening;
using Solitaire.Commands;
using Solitaire.Models;
using Solitaire.Presenters;
using Solitaire.Services;
using TMPro;
using UnityEngine;
using Zenject;

namespace Solitaire.Installers
{
    public class GameInstaller : MonoInstaller
    {
        [SerializeField]
        private GameObject _cardPrefab;

        [SerializeField]
        private GameObject _hintArrowPrefab;

        [SerializeField]
        private GameObject _particlePulsePrefab;

        [Tooltip("Tiny star burst played with the tableau confirm rings.")]
        [SerializeField]
        private GameObject _starsPrefab;

        [SerializeField]
        private Sprite _softRippleSprite;

        [Tooltip("Optional. Leave empty to use the built-in Solitaire/SpriteAdditive glow.")]
        [SerializeField]
        private Material _softRippleMaterial;

        [Tooltip("Material for the lonely-Ace home glow (e.g. FoilUnlit).")]
        [SerializeField]
        private Material _aceHomeGlowMaterial;

        [Tooltip("Baron Neue SDF. Score pops use this; empty falls back to TMP default.")]
        [SerializeField]
        private TMP_FontAsset _scorePopupFont;

        [Tooltip("Burst 45°, scale/speed lerp, then slow float. Next pop uses the latest values.")]
        [SerializeField]
        private ScorePopupTune _scorePopupTune = new ScorePopupTune();

        [SerializeField]
        private VictoryPalette _victoryPalette = new VictoryPalette();

        private void Awake()
        {
            // Initialize
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            DOTween.Init();
        }

        public override void InstallBindings()
        {
            InstallServices();
            InstallGame();
            InstallPiles();
            InstallCards();
            InstallHintArrow();
            InstallParticlePulse();
            InstallStars();
            InstallSoftRipple();
            InstallAceHomeGlow();
            InstallScorePopup();
            InstallCommands();
            InstallVictoryTester();
        }

        private void InstallServices()
        {
            Container.BindInterfacesAndSelfTo<CommandService>().AsSingle();
            Container.BindInterfacesAndSelfTo<DragAndDropHandler>().AsSingle();
            Container.BindInterfacesAndSelfTo<MovesService>().AsSingle();
            Container.BindInterfacesAndSelfTo<PointsService>().AsSingle();
            Container.BindInterfacesAndSelfTo<StreakService>().AsSingle();
            Container.BindInterfacesAndSelfTo<HintService>().AsSingle();
            Container.BindInterfacesAndSelfTo<OrientationService>().AsSingle();
            Container.BindInterfacesAndSelfTo<AudioService>().AsSingle();
            Container.BindInterfacesAndSelfTo<AdService>().AsSingle();
            Container.BindInterfacesAndSelfTo<StorageService>().AsSingle();
            Container.BindInterfacesAndSelfTo<ScorePopupService>().AsSingle();
            Container.BindInterfacesAndSelfTo<CardLandFxService>().AsSingle();
            Container.BindInterfacesAndSelfTo<HudTuckService>().AsSingle();
            Container.BindInterfacesAndSelfTo<VictoryGlyphService>().AsSingle();
            Container.Bind<CrestShieldPresenter>().FromComponentInHierarchy(true).AsSingle();
            Container.Bind<VictoryBubblePresenter>().FromComponentInHierarchy(true).AsSingle();
            if (_victoryPalette == null)
                _victoryPalette = new VictoryPalette();
            Container.Bind<VictoryPalette>().FromInstance(_victoryPalette);
            Container.BindInterfacesAndSelfTo<VictoryStageService>().AsSingle();
            Container.BindInterfacesAndSelfTo<VictorySequenceService>().AsSingle();
            Container.Bind<IScoreHud>().To<GameInfoPresenter>().FromComponentInHierarchy(true).AsSingle();
        }

        private void InstallGame()
        {
            Container.Bind<Game>().AsSingle();
            Container.Bind<GameState>().AsSingle();
            Container.Bind<GamePopup>().AsSingle();
            Container.Bind<GameControls>().AsSingle();
            Container.Bind<OrientationState>().AsSingle();
            Container.Bind<Options>().AsSingle();
            Container.Bind<Leaderboard>().AsSingle();
        }

        private void InstallPiles()
        {
            Container.Bind<Pile>().AsTransient();
        }

        private void InstallCards()
        {
            Container.Bind<Card>().AsTransient();
            Container.BindInterfacesAndSelfTo<CardSpawner>().AsSingle();

            if (_cardPrefab == null)
            {
                Debug.LogError(
                    "[GameInstaller] Card prefab is null — assign Card.prefab on SceneContext/GameInstaller. "
                        + "Zenject install aborted early would leave Game.Init piles unset."
                );
                return;
            }

            Container
                .BindFactory<Card.Suits, Card.Types, CardPresenter, CardPresenter.Factory>()
                .FromMonoPoolableMemoryPool(x =>
                    x.WithInitialSize(52)
                        .FromComponentInNewPrefab(_cardPrefab)
                        .UnderTransformGroup("CardPool")
                );
        }

        private void InstallHintArrow()
        {
            if (_hintArrowPrefab == null)
            {
                Debug.LogWarning(
                    "[GameInstaller] Hint arrow prefab is not assigned — hints will run without an arrow."
                );
                return;
            }

            Container
                .Bind<HintArrowPresenter>()
                .FromComponentInNewPrefab(_hintArrowPrefab)
                .AsSingle()
                .NonLazy();
        }

        private void InstallParticlePulse()
        {
            if (_particlePulsePrefab == null)
            {
                Debug.LogWarning(
                    "[GameInstaller] ParticlePulse prefab is not assigned — foundation land FX will be missing."
                );
                return;
            }

            Container
                .Bind<GameObject>()
                .WithId("ParticlePulse")
                .FromInstance(_particlePulsePrefab);
        }

        private void InstallStars()
        {
            if (_starsPrefab == null)
            {
                Debug.LogWarning(
                    "[GameInstaller] Stars prefab is not assigned — tableau confirm will play without sparkles."
                );
                return;
            }

            Container.Bind<GameObject>().WithId("Stars").FromInstance(_starsPrefab);
        }

        private void InstallSoftRipple()
        {
            if (_softRippleSprite == null)
            {
                Debug.LogWarning(
                    "[GameInstaller] Soft ripple sprite is not assigned — tableau confirm uses procedural ring."
                );
            }
            else
            {
                Container
                    .Bind<Sprite>()
                    .WithId("SoftRipple")
                    .FromInstance(_softRippleSprite);
            }

            if (_softRippleMaterial == null)
                return;

            Container
                .Bind<Material>()
                .WithId("SoftRippleMaterial")
                .FromInstance(_softRippleMaterial);
        }

        private void InstallAceHomeGlow()
        {
            if (_aceHomeGlowMaterial == null)
            {
                Debug.LogWarning(
                    "[GameInstaller] Ace home glow material is not assigned — using SpriteAdditive."
                );
                return;
            }

            Container
                .Bind<Material>()
                .WithId("AceHomeGlow")
                .FromInstance(_aceHomeGlowMaterial);
        }

        private void InstallScorePopup()
        {
            var font = ResolveScorePopupFont();
            if (font == null)
            {
                Debug.LogWarning(
                    "[GameInstaller] Score popup font is not assigned — pops use TMP default."
                );
            }
            else
            {
                Container.Bind<TMP_FontAsset>().WithId("ScorePopupFont").FromInstance(font);
            }

            if (_scorePopupTune == null)
                _scorePopupTune = new ScorePopupTune();

            Container.Bind<ScorePopupTune>().FromInstance(_scorePopupTune);
        }

        private TMP_FontAsset ResolveScorePopupFont()
        {
            if (_scorePopupFont != null)
                return _scorePopupFont;

#if UNITY_EDITOR
            var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Art/Fonts/baron_neue/Baron Neue SDF.asset"
            );
            if (loaded != null)
                return loaded;
#endif
            return TMP_Settings.defaultFontAsset;
        }

        private void InstallVictoryTester()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Container
                .Bind<VictoryTester>()
                .FromNewComponentOnNewGameObject()
                .WithGameObjectName("VictoryTester")
                .AsSingle()
                .NonLazy();
#endif
        }

        private void InstallCommands()
        {
            Container
                .BindFactory<Pile, Pile, DrawCardCommand, DrawCardCommand.Factory>()
                .FromPoolableMemoryPool(x => x.WithInitialSize(256).ExpandByDoubling());
            Container
                .BindFactory<Card, Pile, Pile, MoveCardCommand, MoveCardCommand.Factory>()
                .FromPoolableMemoryPool(x => x.WithInitialSize(256).ExpandByDoubling());
            Container
                .BindFactory<Pile, Pile, RefillStockCommand, RefillStockCommand.Factory>()
                .FromPoolableMemoryPool(x => x.WithInitialSize(16).ExpandByDoubling());
        }
    }
}
