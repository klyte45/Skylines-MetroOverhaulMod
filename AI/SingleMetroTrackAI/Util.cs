using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MetroOverhaul.AI.SingleMetroTrackAI
{
    class Util
    {
        ///////// settings ////////////

        public static bool allowPriorityQueue = true;
        public static bool allowFollowing = true;
        public static bool allowGoAsFarAsPossible = false;
        public static bool allowSpawnSignals = false;
        public static bool noCheckOverlapOnLastSegment = false;

        //not in xml
        public static bool fixReverseTrainSingleTrackStation = true;
        public static bool extendReservationAfterStopStation = true;
        public static bool includeDoubleTrackStationAfterSingleTrackSection = true;
        public static bool slowSpeedTrains = false;
    }
}
