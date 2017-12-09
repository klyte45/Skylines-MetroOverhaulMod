using System;
using ColossalFramework;
using ColossalFramework.IO;
using NetworkSkins.Net;
using UnityEngine;

// ReSharper disable NonReadonlyMemberInGetHashCode

namespace NetworkSkins.Data
{
    public class SegmentData : IDataContainer
    {
        [Flags]
        public enum FeatureFlags
        {
            None = 0,
            RepeatDistances = 1,
            PillarProp = 2,     
        }

        // After setting these fields once, they should only be read!
        public FeatureFlags Features = FeatureFlags.None;
        public string PillarProp;
        public Vector4 RepeatDistances; // x -> left tree, y -> middle tree, z -> right tree, w -> street light

        [NonSerialized]
        public PropInfo PillarPropPrefab;
        [NonSerialized]
        public int UsedCount = 0;

        public SegmentData() {}

        public SegmentData(SegmentData segmentData)
        {
            if (segmentData == null) return;

            Features = segmentData.Features;
            PillarProp = segmentData.PillarProp;
            RepeatDistances = segmentData.RepeatDistances;

            PillarPropPrefab = segmentData.PillarPropPrefab;
        }

        public void SetPrefabFeature<P>(FeatureFlags feature, P prefab = null) where P : PrefabInfo
        {
            Features = Features.SetFlags(feature);

            var flagName = feature.ToString();

            var nameField = GetType().GetField(flagName);
            var prefabField = GetType().GetField(flagName + "Prefab");

            nameField?.SetValue(this, prefab?.name);
            prefabField?.SetValue(this, prefab);
        }

        public void SetStructFeature<V>(FeatureFlags feature, V value) where V : struct
        {
            Features = Features.SetFlags(feature);

            var flagName = feature.ToString();

            var nameField = GetType().GetField(flagName);

            nameField?.SetValue(this, value); //TODO
        }

        public void UnsetFeature(FeatureFlags feature)
        {
            Features = Features.ClearFlags(feature);

            var flagName = feature.ToString();

            var nameField = GetType().GetField(flagName);
            var prefabField = GetType().GetField(flagName + "Prefab");

            nameField?.SetValue(this, null);
            prefabField?.SetValue(this, null);
        }

        public void Serialize(DataSerializer s)
        {
            s.WriteInt32((int)Features);

            if (Features.IsFlagSet(FeatureFlags.PillarProp))
                s.WriteSharedString(PillarProp);
            if (Features.IsFlagSet(FeatureFlags.RepeatDistances))
                s.WriteVector4(RepeatDistances);
        }

        public void Deserialize(DataSerializer s)
        {
            Features = (FeatureFlags)s.ReadInt32();

            if (Features.IsFlagSet(FeatureFlags.PillarProp))
                PillarProp = s.ReadSharedString();
            if (Features.IsFlagSet(FeatureFlags.RepeatDistances))
                RepeatDistances = s.ReadVector4();
        }

        public void AfterDeserialize(DataSerializer s) {}

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == this.GetType() && Equals((SegmentData) obj);
        }

        protected bool Equals(SegmentData other)
        {
            return Features == other.Features
                && string.Equals(PillarProp, other.PillarProp)
                && (Features.IsFlagSet(FeatureFlags.RepeatDistances) == Vector4.Equals(RepeatDistances, other.RepeatDistances));
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int) Features;
                hashCode = (hashCode*397) ^ (PillarProp?.GetHashCode() ?? 0);
                hashCode = (hashCode*397) ^ (Features.IsFlagSet(FeatureFlags.RepeatDistances) ? RepeatDistances.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString() 
        {
            return "[SegmentData] features: " + Features
                + ", PillarPropPrefab: " + (PillarPropPrefab == null ? "null" : PillarPropPrefab.name)
                + ", usedCount: " + UsedCount;
        }

        public void FindPrefabs()
        {
            FindPrefab(PillarProp, out PillarPropPrefab);
        }

        private static void FindPrefab<T>(string prefabName, out T prefab) where T : PrefabInfo
        {
            prefab = prefabName != null ? PrefabCollection<T>.FindLoaded(prefabName) : null;
        }
    }
}
