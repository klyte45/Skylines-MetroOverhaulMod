using System;
using System.Collections.Generic;
using System.Globalization;
using NetworkSkins.UI;
using UnityEngine;

namespace NetworkSkins.Props
{
    public class UILightOption : UIDropDownTextFieldOption
    {
        private List<PropInfo> _availablePillarProps;

        private bool ignoreTextFieldChanged = false;

        protected override void Initialize() 
        {
            Description = "Street Light";
            base.Initialize();
        }
        
        protected override bool PopulateDropDown()
        {
            _availablePillarProps = PropCustomizer.Instance.GetAvailablePillarProps(SelectedPrefab);

            if (SelectedPrefab != null && _availablePillarProps != null && PropCustomizer.Instance.HasPillarProps(SelectedPrefab))
            {
                var defaultProp = PropCustomizer.Instance.GetDefaultPillarProp(SelectedPrefab);
                var activeProp = PropCustomizer.Instance.GetActivePillarProp(SelectedPrefab);

                DropDown.items = new string[0];

                foreach (var prop in _availablePillarProps)
                {
                    var itemName = UIUtil.GenerateBeautifiedPrefabName(prop);
                    itemName = BeautifyNameEvenMore(itemName);
                    if (prop == defaultProp) itemName += " (Default)";

                    DropDown.AddItem(itemName);

                    if (prop == activeProp) DropDown.selectedIndex = DropDown.items.Length - 1;
                }

                var defaultDistance = PropCustomizer.Instance.GetDefaultPillarPropDistance(SelectedPrefab);
                var activeDistance = PropCustomizer.Instance.GetActivePillarPropDistance(SelectedPrefab);
                TextField.text = activeDistance.ToString(CultureInfo.InvariantCulture);
                TextField.tooltip = $"Distance between street lights in m (default {defaultDistance})\nValue must be between 1 and 100!";

                if (_availablePillarProps.Count >= 2)
                {
                    DropDown.Enable();
                }
                else
                {
                    DropDown.Disable();
                }
                return true;
            }
            return false;
        }

        private string BeautifyNameEvenMore(string itemName)
        {
            switch (itemName)
            {
                case "New Street Light": return "Cold White Street Light";
                case "New Street Light Highway": return "Warm White Street Light";
                case "New Street Light Small Road": return "Neutral White Street Light";
                case "Avenue Light": return "Sodium-vapor Double Street Light";
                case "Street Lamp #1": return "Modern Street Lamp";
                case "Street Lamp #2": return "Old Street Lamp";
                default: return itemName;
            }
        }

        protected override void OnSelectionChanged(int index)
        {
            PropCustomizer.Instance.SetPillarProp(SelectedPrefab, _availablePillarProps[index]);
        }


        protected override void OnTextChanged(string value)
        {
            if (ignoreTextFieldChanged) return;

            float distance;

            try
            {
                distance = Mathf.Clamp(Convert.ToSingle(value), 1f, 100f);
                PropCustomizer.Instance.SetPillarPropDistance(SelectedPrefab, distance);
            }
            catch
            {
                distance = PropCustomizer.Instance.GetActivePillarPropDistance(SelectedPrefab);
            }

            ignoreTextFieldChanged = true;
            TextField.text = distance.ToString(CultureInfo.InvariantCulture);
            ignoreTextFieldChanged = false;
        }
    }
}
