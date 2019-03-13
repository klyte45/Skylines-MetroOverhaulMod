using System;
using MetroOverhaul.NEXT;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using MetroOverhaul.NEXT.Extensions;

namespace MetroOverhaul.InitializationSteps
{
    public static class LateBuildUpSteel
    {
        private static NetTool m_NetTool = null;
        public static void BuildUpExtraPillars(NetInfo prefab, NetInfoVersion version)
        {
            if (version == NetInfoVersion.Elevated)
            {
                var laneCount = prefab.m_lanes.Where(l => l.m_vehicleType == VehicleInfo.VehicleType.Metro).GroupBy(g => g.m_position).Count();
                var lowPillarProp = PrefabCollection<PropInfo>.FindLoaded($"{Util.PackageName($"Classic {laneCount}L Pillar (Low) Prop")}.Classic {laneCount}L Pillar (Low) Prop_Data");
                var highPillarProp = PrefabCollection<PropInfo>.FindLoaded($"{Util.PackageName($"Classic {laneCount}L Pillar (High) Prop")}.Classic {laneCount}L Pillar (High) Prop_Data");
                var pillarPropList = new List<BridgePillarPropItem>();
                pillarPropList.Add(new BridgePillarPropItem() { HeightLimit = 12, RepeatDistance = 60, Position = new Vector3(0, -15.75f, 0), Prop = lowPillarProp });
                pillarPropList.Add(new BridgePillarPropItem() { HeightLimit = 60, RepeatDistance = 60, Position = new Vector3(0, -24.9f, 0), Prop = highPillarProp });

                var propLane = prefab.m_lanes.FirstOrDefault(l => l.m_laneType == NetInfo.LaneType.None && l.m_laneProps != null && l.m_laneProps.name == "CenterLaneProps");

                var propsList = new List<NetLaneProps.Prop>();
                var elevation = m_NetTool.GetElevation();
                var theList = pillarPropList.Where(d => d.HeightLimit == 0 || d.HeightLimit >= elevation).OrderBy(x => x.HeightLimit).ToList();
                for (var i = 0; i < pillarPropList.Count; i++)
                {
                    var thePillarPropInfo = pillarPropList[i];
                    if (thePillarPropInfo != null)
                    {
                        var prop = new NetLaneProps.Prop();
                        prop.m_prop = thePillarPropInfo.Prop;
                        prop.m_position = thePillarPropInfo.Position;
                        prop.m_finalProp = thePillarPropInfo.Prop;
                        prop.m_probability = 100;
                        prop.m_repeatDistance = thePillarPropInfo.RepeatDistance;
                        prop.m_segmentOffset = thePillarPropInfo.SegmentOffset;
                        propsList.Add(prop);
                    }
                }
                propLane.m_laneProps.m_props = propsList.ToArray();
            }
        }
        public static void BuildUp(NetInfo prefab, NetInfoVersion version)
        {
            var laneCount = prefab.m_lanes.Where(l => l.m_vehicleType == VehicleInfo.VehicleType.Metro).GroupBy(g => g.m_position).Count();
            switch (version)
            {
                case NetInfoVersion.Elevated:
                    {

                        var lowPillar = PrefabCollection<BuildingInfo>.FindLoaded($"{Util.PackageName($"Classic {laneCount}L Pillar (Low)")}.Classic {laneCount}L Pillar (Low)_Data");
                        var lowPillarNoCol = PrefabCollection<BuildingInfo>.FindLoaded($"{Util.PackageName($"Classic {laneCount}L Pillar NoCol (Low)")}.Classic {laneCount}L Pillar NoCol (Low)_Data");

                        var highPillar = PrefabCollection<BuildingInfo>.FindLoaded($"{Util.PackageName($"Classic {laneCount}L Pillar (High)")}.Classic {laneCount}L Pillar (High)_Data");
                        var highPillarNoCol = PrefabCollection<BuildingInfo>.FindLoaded($"{Util.PackageName($"Classic {laneCount}L Pillar NoCol (High)")}.Classic {laneCount}L Pillar NoCol (High)_Data");

                        if (lowPillar == null)
                        {
                            throw new Exception($"{prefab.name}: SteelMetroElevatedPillar not found!");
                        }
                        var bridgeAI = prefab.GetComponent<TrainTrackBridgeAIMetro>();
                        if (bridgeAI != null)
                        {
                            bridgeAI.m_bridgePillarInfo = lowPillar;
                            if (highPillar != null)
                            {
                                bridgeAI.pillarList = new List<BridgePillarItem>();
                                bridgeAI.pillarList.Add(new BridgePillarItem() { HeightLimit = 18, HeightOffset = 0, info = lowPillar, noCollisionInfo = lowPillarNoCol });
                                bridgeAI.pillarList.Add(new BridgePillarItem() { HeightLimit = 60, HeightOffset = 0, info = highPillar, noCollisionInfo = highPillarNoCol });
                            }
                            bridgeAI.m_bridgePillarOffset = 0.75f;
                        }
                        //var lowPillarProp = PrefabCollection<PropInfo>.FindLoaded($"{Util.PackageName($"Classic {laneCount}L Pillar (Low) Prop")}.Classic {laneCount}L Pillar (Low) Prop_Data");
                        //var highPillarProp = PrefabCollection<PropInfo>.FindLoaded($"{Util.PackageName($"Classic {laneCount}L Pillar (High) Prop")}.Classic {laneCount}L Pillar (High) Prop_Data");
                        //var pillarPropList = new List<BridgePillarPropItem>();
                        //pillarPropList.Add(new BridgePillarPropItem() { HeightLimit = 12, RepeatDistance = 60, Position = new Vector3(0, -15.75f, 0), Prop = lowPillarProp });
                        //pillarPropList.Add(new BridgePillarPropItem() { HeightLimit = 60, RepeatDistance = 60, Position = new Vector3(0, -24.9f, 0), Prop = highPillarProp });
                        //var lanes = prefab.m_lanes.ToList();
                        //var propLane = lanes.FirstOrDefault(l => l.m_laneType == NetInfo.LaneType.None && l.m_position == 0);
                        //var propsList = new List<NetLaneProps.Prop>();
                        //for (var i = 0; i < pillarPropList.Count; i++)
                        //{
                        //    var thePillarPropInfo = pillarPropList[i];
                        //    if (thePillarPropInfo != null)
                        //    {
                        //        var prop = new NetLaneProps.Prop();
                        //        prop.m_prop = thePillarPropInfo.Prop;
                        //        prop.m_position = thePillarPropInfo.Position;
                        //        prop.m_finalProp = thePillarPropInfo.Prop;
                        //        prop.m_probability = 100;
                        //        prop.m_repeatDistance = thePillarPropInfo.RepeatDistance;
                        //        prop.m_segmentOffset = thePillarPropInfo.SegmentOffset;
                        //        propsList.Add(prop);
                        //    }
                        //}
                        //propLane.m_laneProps.m_props = propsList.ToArray();
                        //var laneList = new List<NetInfo.Lane>();
                        //laneList.AddRange(prefab.m_lanes);
                        //laneList.Add(propLane);
                        //prefab.m_lanes = laneList.ToArray();
                        //if (m_NetTool == null)
                        //{
                        //    m_NetTool = UnityEngine.Object.FindObjectOfType<NetTool>();
                        //}
                        break;
                    }
                case NetInfoVersion.Bridge:
                    {
                        var steelBridgePillarInfo = PrefabCollection<BuildingInfo>.FindLoaded($"{Util.PackageName($"Classic {laneCount}L Pillar (Bridge)")}.Classic {laneCount}L Pillar (Bridge)_Data");
                        var steelNoColBridgePillarInfo = PrefabCollection<BuildingInfo>.FindLoaded($"{Util.PackageName($"Classic {laneCount}L Pillar NoCol (Bridge)")}.Classic {laneCount}L Pillar NoCol (Bridge)_Data");
                        if (steelBridgePillarInfo == null)
                        {
                            throw new Exception($"{prefab.name}: MetroBridgePillar not found!");
                        }
                        var bridgeAI = prefab.GetComponent<TrainTrackBridgeAIMetro>();
                        if (bridgeAI != null)
                        {
                            bridgeAI.m_bridgePillarInfo = steelBridgePillarInfo;
                            bridgeAI.m_middlePillarInfo = steelBridgePillarInfo;
                            bridgeAI.pillarList = new List<BridgePillarItem>();
                            bridgeAI.pillarList.Add(new BridgePillarItem() { HeightLimit = 0, HeightOffset = 0, info = steelBridgePillarInfo, noCollisionInfo = steelNoColBridgePillarInfo });
                            bridgeAI.m_bridgePillarOffset = 0.55f;
                        }
                        break;
                    }
            }
        }

    }
}
