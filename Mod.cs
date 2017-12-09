using ColossalFramework.UI;
using ICities;
using MetroOverhaul.OptionsFramework.Extensions;
using MetroOverhaul.UI;
using NetworkSkins.Data;
using NetworkSkins.Detour;
using NetworkSkins.Net;

namespace MetroOverhaul
{
    public class Mod : LoadingExtensionBase, IUserMod
    {
#if IS_PATCH
        public const bool isPatch = true;
#else
        public const bool isPatch = false;
#endif
        private UIPanel UITrackPanel = null;
        public string Name => "Metro Overhaul" + (isPatch ? " [Patch 1.6.2]" : "");
        public string Description => "Brings metro depots, ground and elevated metro tracks";

        public void OnSettingsUI(UIHelperBase helper)
        {
            helper.AddOptionsGroup<Options>();
        }
        public override void OnCreated(ILoading loading)
        {
            RenderManagerDetour.Deploy();
            NetManagerDetour.Deploy();
        }
        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);
            SegmentDataManager.Instance.OnLevelLoaded();
            if (UITrackPanel == null)
            {
                UITrackPanel = UIView.GetAView().AddUIComponent(typeof(MetroTrackCustomizerUI)) as MetroTrackCustomizerUI;
            }
            UITrackPanel.isVisible = true;
        }
        public override void OnReleased()
        {
            base.OnReleased();
            RenderManagerDetour.Revert();
            NetManagerDetour.Revert();
        }

    }
}
