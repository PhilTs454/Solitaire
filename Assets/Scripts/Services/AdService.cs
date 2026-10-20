using UnityEngine;
using UnityEngine.Advertisements;
using Zenject;

namespace Solitaire.Services
{
    public class AdService : IInitializable, IUnityAdsInitializationListener, IAdService
    {
#if UNITY_ANDROID
        private const string GameId = "4848282";
        private const string PlacementId = "Banner_Android";
#elif UNITY_IOS
        private const string GameId = "4848283";
        private const string PlacementId = "Banner_iOS";
#else
        // Editor / Standalone: Unity Ads is not supported — do not initialize.
        private const string GameId = "";
        private const string PlacementId = "";
#endif

        private static bool IsMobileAdsPlatform
        {
            get
            {
#if UNITY_ANDROID || UNITY_IOS
                return true;
#else
                return false;
#endif
            }
        }

        public void Initialize()
        {
            if (!IsMobileAdsPlatform || !Advertisement.isSupported)
                return;

            Advertisement.Initialize(GameId, true, this);
        }

        public void OnInitializationComplete()
        {
            ShowBanner();
        }

        public void OnInitializationFailed(UnityAdsInitializationError error, string message)
        {
            Debug.LogWarning($"[AdService] Init failed ({error}): {message}");
        }

        public void ShowBanner()
        {
            if (!IsMobileAdsPlatform || !Advertisement.isSupported)
                return;

            Advertisement.Banner.SetPosition(BannerPosition.BOTTOM_CENTER);
            Advertisement.Banner.Show(PlacementId);
        }

        public void HideBanner()
        {
            if (!IsMobileAdsPlatform || !Advertisement.isSupported)
                return;

            Advertisement.Banner.Hide();
        }
    }
}
