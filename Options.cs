﻿
using System.Xml.Serialization;
using MetroOverhaul.Detours;
using MetroOverhaul.OptionsFramework.Attibutes;
using MetroOverhaul.UI;

namespace MetroOverhaul
{
    [Options("MetroOverhaul")]
    public class Options
    {
        private const string UNSUBPREP = "Unsubscribe Prep";
        private const string GENERAL = "General Settings";
        private const string AI = "AI Settings";
        private const string CONVERTER = "In-Game Converter";
        private const string SINGLETRACKAI = "Single Track AI";
        public Options()
        {
            ingameTrainMetroConverter = true;
            improvedPassengerTrainAi = true;
            improvedMetroTrainAi = true;
            metroUi = true;
            ghostMode = false;
            depotsNotRequiredMode = false;
        }
        [Checkbox("In-Game Train <-> Metro Station Converter", "Interchangably convert whole stations or individual tracks between MOM and Passenger Rail", CONVERTER)]
        public bool ingameTrainMetroConverter { get; set; }

        [Checkbox("Metro track customization", "UI requires reloading from main menu", GENERAL)]
        public bool metroUi { set; get; }

        //TODO(bloodypengin): currently disabled because causes issues with trains not spawning at some types of stations       
        //        [Checkbox("No depot required mode (requires reloading from main menu)", GENERAL)]
        [XmlIgnore]
        public bool depotsNotRequiredMode { set; get; }


        [Checkbox("Improved PassengerTrainAI", "Allows trains to return to depots", AI, typeof(PassengerTrainAIDetour), nameof(PassengerTrainAIDetour.ChangeDeployState))]
        public bool improvedPassengerTrainAi { set; get; }

        [Checkbox("Improved MetroTrainAI", "Allows trains to properly spawn at surface", AI, typeof(MetroTrainAIDetour), nameof(MetroTrainAIDetour.ChangeDeployState))]
        public bool improvedMetroTrainAi { set; get; }

        [Checkbox("GHOST MODE", "Load your MOM city with this ON and save before unsubscribing", UNSUBPREP)]
        public bool ghostMode { set; get; }


    }
}