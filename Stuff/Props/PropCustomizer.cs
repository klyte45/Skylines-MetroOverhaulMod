using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using ICities;
using NetworkSkins.Data;
using NetworkSkins.Detour;
using NetworkSkins.Net;
using UnityEngine;

namespace NetworkSkins.Props
{
    public class PropCustomizer : LoadingExtensionBase
    {
        public static PropCustomizer Instance;

        private readonly List<TreeInfo> _availableTrees = new List<TreeInfo>();
        private readonly List<PropInfo> _availablePillarProps = new List<PropInfo>();

        public int[] PillarPropPrefabDataIndices;

        public override void OnCreated(ILoading loading)
        {
            base.OnCreated(loading);
            Instance = this;

            RenderManagerDetour.EventUpdateData += OnUpdateData;

            NetLaneDetour.Deploy();
        }

        /// <summary>
        /// Like OnLevelLoaded, but executed earlier.
        /// </summary>
        /// <param name="mode"></param>
        public void OnUpdateData(SimulationManager.UpdateMode mode)
        {
            if (mode != SimulationManager.UpdateMode.LoadMap && mode != SimulationManager.UpdateMode.NewMap
                && mode != SimulationManager.UpdateMode.LoadGame && mode != SimulationManager.UpdateMode.NewGameFromMap && mode != SimulationManager.UpdateMode.NewGameFromScenario) return;

            // no street lights
            _availablePillarProps.Add(null);

            for (uint i = 0; i < PrefabCollection<PropInfo>.LoadedCount(); i++)
            {
                var prefab = PrefabCollection<PropInfo>.GetLoaded(i);

                if (prefab == null) continue;
                if (prefab.name.Contains("L Pillar ("))
                {
                    _availablePillarProps.Add(prefab);
                }
            }
            Next.Debug.Log($"{_availablePillarProps.Count()} PILLARS FOUND");
            // compile list of data indices for fast check if a prefab is a pillar prop:
            PillarPropPrefabDataIndices = _availablePillarProps.Where(prop => prop != null).Select(prop => prop.m_prefabDataIndex).ToArray();
        }

        public override void OnLevelUnloading()
        {
            _availableTrees.Clear();
            _availablePillarProps.Clear();
        }

        public override void OnReleased()
        {
            Instance = null;

            RenderManagerDetour.EventUpdateData -= OnUpdateData;

            NetLaneDetour.Revert();
        }

        public bool HasPillarProps(NetInfo prefab)
        {
            if (prefab.m_lanes == null) return false;

            foreach (var lane in prefab.m_lanes)
                if (lane?.m_laneProps?.m_props != null)
                    foreach (var laneProp in lane.m_laneProps.m_props)
                    {
                        if (laneProp?.m_finalProp != null && _availablePillarProps.Contains(laneProp.m_finalProp)) return true;
                    }

            return false;
        }

        public List<PropInfo> GetAvailablePillarProps(NetInfo prefab)
        {
            return _availablePillarProps;
        }

        public PropInfo GetActivePillarProp(NetInfo prefab)
        {
            var segmentData = SegmentDataManager.Instance.GetActiveOptions(prefab);

            if (segmentData == null || !segmentData.Features.IsFlagSet(SegmentData.FeatureFlags.PillarProp))
            {
                return GetDefaultPillarProp(prefab);
            }
            else
            {
                return segmentData.PillarPropPrefab;
            }
        }

        public PropInfo GetDefaultPillarProp(NetInfo prefab)
        {
            if (prefab.m_lanes == null) return null;

            foreach (var lane in prefab.m_lanes)
                if (lane?.m_laneProps?.m_props != null)
                    foreach (var laneProp in lane.m_laneProps.m_props)
                    {
                        if (laneProp?.m_finalProp != null && _availablePillarProps.Contains(laneProp.m_finalProp)) return laneProp.m_finalProp;
                    }

            return null;
        }

        public void SetPillarProp(NetInfo prefab, PropInfo prop)
        {
            var newSegmentData = new SegmentData(SegmentDataManager.Instance.GetActiveOptions(prefab));

            newSegmentData.SetPrefabFeature(SegmentData.FeatureFlags.PillarProp, prop);
            SegmentDataManager.Instance.SetActiveOptions(prefab, newSegmentData);
        }

        public void SetPillarPropDistance(NetInfo prefab, float val)
        {
            var newSegmentData = new SegmentData(SegmentDataManager.Instance.GetActiveOptions(prefab));

            var distanceVector = newSegmentData.RepeatDistances;
            distanceVector.w = val;
            newSegmentData.SetStructFeature(SegmentData.FeatureFlags.RepeatDistances, distanceVector);
            SegmentDataManager.Instance.SetActiveOptions(prefab, newSegmentData);
        }

        public float GetActivePillarPropDistance(NetInfo prefab)
        {
            var segmentData = SegmentDataManager.Instance.GetActiveOptions(prefab);
            return segmentData.RepeatDistances.w;
        }
    }
}
