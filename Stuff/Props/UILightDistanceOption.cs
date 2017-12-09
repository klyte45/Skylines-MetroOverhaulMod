using System.Collections.Generic;
using NetworkSkins.UI;

namespace NetworkSkins.Props
{
    public class UILightDistanceOption : UISliderOption
    {
        protected override void Initialize() 
        {
            Description = "Street Light Dist.";
            base.Initialize();
        }
        
        protected override bool PopulateSlider()
        {
            if (SelectedPrefab != null && PropCustomizer.Instance.HasPillarProps(SelectedPrefab))
            {
                var defaultDistance = PropCustomizer.Instance.GetDefaultPillarPropDistance(SelectedPrefab);
                var activeDistance = PropCustomizer.Instance.GetActivePillarPropDistance(SelectedPrefab);

                if (defaultDistance < 0f) return false;

                Slider.minValue = defaultDistance*0.5f;
                Slider.maxValue = defaultDistance*2f;
                Slider.value = activeDistance;
                Slider.stepSize = defaultDistance*.15f;

                return true;
            }
            return false;
        }

        protected override void OnValueChanged(float val)
        {
            PropCustomizer.Instance.SetPillarPropDistance(SelectedPrefab, val);
        }
    }
}