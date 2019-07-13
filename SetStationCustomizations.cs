﻿using MetroOverhaul.NEXT.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using MetroOverhaul.Extensions;
using UnityEngine;

namespace MetroOverhaul {
    public static class SetStationCustomizations {
        public const float MAX_DEPTH = 36;
        public const float MIN_DEPTH = 12;
        public const float DEF_DEPTH = 15;
        public const float MAX_ANGLE = 180;
        public const float MIN_ANGLE = -180;
        public const float DEF_ANGLE = 0;
        public const float MAX_LENGTH = 192;
        public const float MIN_LENGTH = 88;
        public const float DEF_LENGTH = 144;
        public const float MAX_BEND_STRENGTH = 90;
        public const float MIN_BEND_STRENGTH = -90;
        public const float DEF_BEND_STRENGTH = 0;
        public static int m_PremierPath = -1;
        public static float m_PrevAngle = 0;
        //private static BuildingInfo m_SuperInfo;
        private static BuildingInfo m_Info;
        private static float m_TargetDepth;
        private static float m_TargetStationTrackLength;
        private static float m_Angle;
        private static float m_BendStrength;
        private static float StairCoeff { get { return 0.2f; } }// (11f / 64f); } }
        private static float AntiStairCoeff { get { return 1 - StairCoeff; } }
        private const float GENERATED_PATH_MARKER = 0.09999f; //regular paths have snapping distance of 0.1f. This way we differentiate
        private const float TOLERANCE = 0.000001f; //equals 1/10 of difference between 0.1f and GENERATED_PATH_MARKER
        private static Vector3 AdjForOffset(Vector3 node, Vector3 offset, bool subToSuper = true) {
            var multiplier = subToSuper ? 1 : -1;
            return new Vector3(node.x + (multiplier * offset.x), node.y, node.z - (multiplier * offset.z));
        }
        public static void ModifyStation(BuildingInfo info, float targetDepth, float targetStationTrackLength, float angle, float bendStrength) {
            m_Info = info;
            m_TargetDepth = targetDepth;
            m_TargetStationTrackLength = targetStationTrackLength;
            m_Angle = angle;
            m_BendStrength = bendStrength / 90;

            if (m_TargetDepth <= 0 || targetStationTrackLength <= 0 || (!info.HasUndergroundMetroStationTracks() && (info?.m_subBuildings == null || info?.m_subBuildings.Count() == 0))) {
                return;
            }
            HandleSubBuildings();
            CleanUpPaths();
            ResizeUndergroundStationTracks();
            ChangeStationDepthAndRotation();
            ReconfigureStationAccess();
            BendStationTrack();
            RecalculateSpawnPoints();
        }
        private static NetInfo m_SpecialNetInfo = null;
        private static NetInfo SpecialNetInfo {
            get
            {
                if (m_SpecialNetInfo == null) {
                    m_SpecialNetInfo = FindNetInfo("Pedestrian Connection Underground").ShallowClone();
                    m_SpecialNetInfo.m_maxSlope = 100;
                    m_SpecialNetInfo.m_maxTurnAngle = 180;
                    m_SpecialNetInfo.m_maxTurnAngleCos = -1;
                    var lanes = m_SpecialNetInfo.m_lanes.ToList();
                    lanes.First().m_speedLimit = 10;
                    m_SpecialNetInfo.m_lanes = lanes.ToArray();
                }
                return m_SpecialNetInfo;
            }
        }
        private static BuildingInfo.PathInfo TrackPath { get; set; }
        private static BuildingInfo.PathInfo NewPath { get; set; }
        private static Vector3 BendVector { get; set; }
        private static Vector3 Crossing { get; set; }
        private static float xCoeff { get; set; }
        private static float zCoeff { get; set; }
        private static float StairsLengthX { get; set; }
        private static float StairsLengthZ { get; set; }
        private static List<BuildingInfo.PathInfo> SetStationPaths(params float[] connectorHalfWidths) {
            var retval = new List<BuildingInfo.PathInfo>();
            if (connectorHalfWidths != null) {
                var newNodes = new List<Vector3>();
                var connectorHalfWidthList = new List<float>();
                connectorHalfWidthList.AddRange(connectorHalfWidths);
                connectorHalfWidthList.Sort();
                var offset = connectorHalfWidthList[0];
                var forbiddenList = new List<bool>();

                for (int i = 0; i < connectorHalfWidthList.Count(); i++) {
                    if (i == 0) {
                        newNodes.Add(new Vector3()
                        {
                            x = Crossing.x + (connectorHalfWidthList[0] * zCoeff) - (3 * xCoeff),
                            y = TrackPath.m_nodes.Last().y + 8,
                            z = Crossing.z + (connectorHalfWidthList[0] * xCoeff) + (3 * zCoeff)
                        });
                        newNodes[0] = new Vector3(newNodes[0].x - BendVector.x, newNodes[0].y, newNodes[0].z + BendVector.z);
                    }
                    else {
                        offset -= connectorHalfWidthList[i];
                        newNodes.Add(new Vector3()
                        {
                            x = newNodes[i - 1].x + (Math.Abs(offset) * zCoeff),
                            y = TrackPath.m_nodes.Last().y + 8,
                            z = newNodes[i - 1].z + (Math.Abs(offset) * xCoeff)
                        });
                        offset = connectorHalfWidthList[i];
                    }
                    forbiddenList.Add(true);

                }


                var averagedNodeList = new List<Vector3>();
                var stairNodes = new List<Vector3>();
                if (newNodes.Count() == 1) {
                    averagedNodeList.Add(newNodes.First());
                    stairNodes.Add(new Vector3()
                    {
                        x = newNodes[0].x + StairsLengthX,
                        y = TrackPath.m_nodes.Last().y,
                        z = newNodes[0].z + StairsLengthZ,
                    });
                }
                else {
                    var mergeOnLast = false;
                    for (int i = 0; i < newNodes.Count(); i++) {
                        if (i > 0) {
                            if (mergeOnLast) {
                                mergeOnLast = false;
                            }
                            else if (Vector3.Distance(newNodes[i], newNodes[i - 1]) < 4) {
                                averagedNodeList.Add(((newNodes[i] + newNodes[i - 1]) / 2));
                                mergeOnLast = true;
                            }
                            else {
                                averagedNodeList.Add(newNodes[i - 1]);
                                if (i == newNodes.Count() - 1) {
                                    averagedNodeList.Add(newNodes[i]);
                                }
                            }
                        }

                        stairNodes.Add(new Vector3()
                        {
                            x = newNodes[i].x + StairsLengthX,
                            y = TrackPath.m_nodes.Last().y,
                            z = newNodes[i].z + StairsLengthZ,
                        });
                    }
                }

                NewPath.m_nodes = averagedNodeList.ToArray();
                NewPath.m_forbidLaneConnection = forbiddenList.ToArray();
                NewPath.AssignNetInfo(m_SpecialNetInfo);

                retval.Add(NewPath);

                for (var i = 0; i < stairNodes.Count(); i++) {
                    var closestIndex = averagedNodeList.IndexOf(averagedNodeList.OrderBy(n => Vector3.Distance(n, stairNodes[i])).FirstOrDefault());
                    var branchPathStair = ChainPath(NewPath, stairNodes[i], closestIndex);
                    branchPathStair.m_forbidLaneConnection = new[] { true, false };
                    branchPathStair.AssignNetInfo(m_SpecialNetInfo);
                    retval.Add(branchPathStair);
                }
            }
            return retval;
        }
        private static void ReconfigureStationAccess() {
            var pathList = new List<BuildingInfo.PathInfo>();
            var connectList = new List<Vector3[]>();
            var singleGap = 16;
            var directionList = new List<string>();
            //for (int i = 0; i < m_Info.m_paths.Length; i++)
            //{
            //    var thePath = m_Info.m_paths[i];
            //    if (thePath.m_netInfo == null || thePath.m_nodes == null || thePath.m_nodes.Count() < 2)
            //        continue;
            //    if (thePath.m_netInfo.IsUndergroundMetroStationTrack())
            //    {
            //        var xDirection = (thePath.m_nodes.First().x - thePath.m_nodes.Last().x);
            //        var zDirection = (thePath.m_nodes.First().z - thePath.m_nodes.Last().z);
            //        var direction = $"{xDirection},{zDirection}";
            //        var antiDirection = $"{-xDirection},{-zDirection}";
            //        if (directionList.Contains(direction))
            //        {
            //            continue;
            //        }
            //        if (directionList.Contains(antiDirection))
            //        {
            //            thePath.m_nodes = thePath.m_nodes.Reverse().ToArray();
            //        }
            //        else
            //        {
            //            directionList.Add(direction);
            //        }
            //    }
            //}

            for (int i = 0; i < m_Info.m_paths.Length; i++) {
                var thePath = m_Info.m_paths[i];
                if (thePath.m_netInfo == null || thePath.m_nodes == null || thePath.m_nodes.Count() < 2)
                    continue;
                if (thePath.m_netInfo.IsUndergroundSmallStationTrack() && !IsPathGenerated(thePath)) {
                    var xCoeff = -(thePath.m_nodes.First().x - thePath.m_nodes.Last().x) / Vector3.Distance(thePath.m_nodes.First(), thePath.m_nodes.Last());
                    var zCoeff = (thePath.m_nodes.First().z - thePath.m_nodes.Last().z) / Vector3.Distance(thePath.m_nodes.First(), thePath.m_nodes.Last());

                    var multiplier = 1;
                    var nearestTrackNode = m_Info.m_paths.Where(p => p.m_netInfo.IsUndergroundMetroStationTrack()).SelectMany(p => p.m_nodes).Where(n => n.y == thePath.m_nodes.First().y).OrderBy(n => Vector3.Distance(n, thePath.m_nodes.First())).FirstOrDefault();
                    if (Vector3.Distance(nearestTrackNode, thePath.m_nodes.First()) <= 2 * singleGap) {
                        multiplier = -1;
                    }
                    var aNewPath = thePath.ShallowClone();
                    var nodeList = new List<Vector3>();
                    nodeList.Add(new Vector3()
                    {
                        x = thePath.m_nodes.First().x + multiplier * zCoeff * singleGap,
                        y = thePath.m_nodes.First().y,
                        z = thePath.m_nodes.First().z + multiplier * xCoeff * singleGap
                    });
                    nodeList.Add(new Vector3()
                    {
                        x = thePath.m_nodes.Last().x + multiplier * zCoeff * singleGap,
                        y = thePath.m_nodes.Last().y,
                        z = thePath.m_nodes.Last().z + multiplier * xCoeff * singleGap
                    });
                    aNewPath.m_nodes = nodeList.ToArray();
                    MarkPathGenerated(aNewPath);
                    pathList.Add(aNewPath);
                }
                pathList.Add(thePath);
            }

            var aPath = pathList.FirstOrDefault(p => IsPedestrianPath(p));
            if (aPath == null) {
                aPath = new BuildingInfo.PathInfo();
            }

            for (int i = 0; i < pathList.Count; i++) {
                if (pathList[i].m_netInfo != null && pathList[i].m_netInfo.IsUndergroundMetroStationTrack()) {
                    TrackPath = pathList[i];
                    var middleBend = GetMiddle(TrackPath, true);
                    var middle = GetMiddle(TrackPath, false);
                    BendVector = middleBend - middle;

                    //var trackNodes = new List<Vector3>();
                    //for (var j = 0; j < trackPath.m_nodes.Count(); j++)
                    //{
                    //    trackNodes.Add(new Vector3()
                    //    {
                    //        x = trackPath.m_nodes[j].x - bendVector.x,
                    //        y = trackPath.m_nodes[j].y,
                    //        z = trackPath.m_nodes[j].z - bendVector.z
                    //    });
                    //}
                    //trackPath.m_nodes = trackNodes.ToArray();
                    NewPath = aPath.ShallowClone();
                    NewPath.AssignNetInfo(SpecialNetInfo);
                    MarkPathGenerated(NewPath);

                    xCoeff = -(TrackPath.m_nodes[0].x - TrackPath.m_nodes.Last().x) / Vector3.Distance(TrackPath.m_nodes[0], TrackPath.m_nodes.Last());
                    zCoeff = (TrackPath.m_nodes[0].z - TrackPath.m_nodes.Last().z) / Vector3.Distance(TrackPath.m_nodes[0], TrackPath.m_nodes.Last());
                    var stationLength = Vector3.Distance(TrackPath.m_nodes[0], TrackPath.m_nodes.Last());
                    StairsLengthX = (((0.12f * m_BendStrength) + 1) * (stationLength * StairCoeff)) * -xCoeff;
                    StairsLengthZ = (((0.12f * m_BendStrength) + 1) * (stationLength * StairCoeff)) * zCoeff;
                    var interpolant = 0.6f;
                    Crossing = Vector3.Lerp(TrackPath.m_nodes.First(), TrackPath.m_nodes.Last(), interpolant);
                    if (TrackPath.m_netInfo.IsUndergroundIslandPlatformStationTrack()) {
                        pathList.AddRange(SetStationPaths(0));
                    }
                    //else if (TrackPath.m_netInfo.IsUndergroundSideIslandPlatformMetroStationTrack())
                    //{
                    //    pathList.AddRange(SetStationPaths(-14, 0, 14));
                    //}
                    else if (TrackPath.m_netInfo.IsUndergroundSmallStationTrack()) {
                        pathList.AddRange(SetStationPaths(5));
                    }
                    else if (TrackPath.m_netInfo.IsUndergroundPlatformLargeMetroStationTrack()) {
                        pathList.AddRange(SetStationPaths(-11, 11));
                    }
                    else if (TrackPath.m_netInfo.IsUndergroundDualIslandPlatformMetroStationTrack()) {
                        pathList.AddRange(SetStationPaths(-8.8f, -5.5f, 5.5f, 8.8f));
                    }
                    else if (TrackPath.m_netInfo.IsUndergroundSidePlatformMetroStationTrack()) {
                        pathList.AddRange(SetStationPaths(-7, 7));
                    }

                    if (!connectList.Contains(NewPath.m_nodes))
                        connectList.Add(NewPath.m_nodes);
                }
            }
            if (/*m_SuperInfo == null &&*/ m_Info.m_paths.Count() >= 2) {
                CheckPedestrianConnections();
            }
            pathList = CleanPaths(pathList);
            var lowestHighPaths = m_Info.m_paths.Where(p => IsPedestrianPath(p) && p.m_nodes.Any(n => n.y > -4) && p.m_nodes.Any(nd => nd.y <= -4)).ToList();
            if (lowestHighPaths.Count == 0)
            {
                lowestHighPaths.Add(m_Info.m_paths.Where(p => IsPedestrianPath(p))
                    .OrderByDescending(p => p.m_nodes[0].y)
                    .FirstOrDefault());
            }
            var connectPoints = new List<Vector3>();
            if (lowestHighPaths != null && lowestHighPaths.Count > 0)
            {
                foreach (BuildingInfo.PathInfo p in lowestHighPaths)
                {
                    if (p != null && p.m_nodes != null && p.m_nodes.Count() > 0)
                    {
                        connectPoints.Add(p.m_nodes.OrderByDescending(n => n.y).ThenBy(n => n.z).LastOrDefault());
                    }

                }
            }
            var currentVector = connectPoints.FirstOrDefault();
            if (connectPoints != null && connectPoints.Count > 0) {
                var pool = new List<Vector3[]>();
                var pivotPath = pathList.FirstOrDefault(p => p.m_nodes.Any(n => n == connectPoints.FirstOrDefault()));
                pool.AddRange(connectList);

                for (var i = 0; i < connectList.Count; i++) {
                    var closestVector = pool.SelectMany(n => n).OrderBy(n => (currentVector.x - n.x) + (100 * (connectPoints.FirstOrDefault().y - n.y)) + (currentVector.z - n.z)).LastOrDefault();

                    var closestPath = pathList.FirstOrDefault(p => p.m_nodes.Any(n => n == closestVector));
                    BuildingInfo.PathInfo branch = null;
                    if (currentVector == connectPoints.FirstOrDefault()) {
                        branch = ChainPath(pivotPath, closestVector, Array.IndexOf(pivotPath.m_nodes, connectPoints.FirstOrDefault()));
                    }
                    else {
                        branch = ChainPath(closestPath, currentVector, Array.IndexOf(closestPath.m_nodes, closestVector));
                    }
                    branch.AssignNetInfo(SpecialNetInfo);
                    branch.m_forbidLaneConnection = new[] { true, true };
                    pathList.Add(branch);
                    var nodeArrayToLose = pool.FirstOrDefault(na => na.Any(n => n == closestVector));
                    if (nodeArrayToLose != null) {
                        currentVector = nodeArrayToLose.OrderBy(n => Vector3.Distance(closestVector, n)).LastOrDefault();
                        pool.Remove(nodeArrayToLose);
                    }
                }

                if (connectPoints.Count > 1) {
                    for (var i = 1; i < connectPoints.Count(); i++) {
                        Vector3 node = connectPoints[i];
                        Vector3 closestVector = connectList.SelectMany(n => n).OrderBy(n => (node.x - n.x) + (100 * (node.y - n.y)) + (node.z - n.z)).FirstOrDefault();
                        var closestPath = pathList.FirstOrDefault(p => p.m_nodes.Any(n => n == closestVector));
                        var branch = ChainPath(closestPath, node, Array.IndexOf(closestPath.m_nodes, closestVector));
                        branch.AssignNetInfo(SpecialNetInfo);
                        branch.m_forbidLaneConnection = new[] { true, true };
                        pathList.Add(branch);
                    }
                }
            }

            m_Info.m_paths = CleanPaths(pathList).ToArray();
        }

        private static List<BuildingInfo.PathInfo> CleanPaths(List<BuildingInfo.PathInfo> pathList) {
            var tinyPaths = pathList.Where(p => Vector3.Distance(p.m_nodes.First(), p.m_nodes.Last()) <= 4).ToList();
            var mergePathDict = new Dictionary<Vector3, Vector3>();
            if (tinyPaths.Count > 0) {
                foreach (var path in tinyPaths) {
                    var average = (path.m_nodes.First() + path.m_nodes.Last()) / 2;

                    if (!(mergePathDict.ContainsKey(path.m_nodes.First()))) {
                        mergePathDict.Add(path.m_nodes.First(), average);
                    }
                    else if (!mergePathDict.ContainsKey(path.m_nodes.Last())) {
                        mergePathDict.Add(path.m_nodes.Last(), mergePathDict[path.m_nodes.First()]);
                    }
                    if (!(mergePathDict.ContainsKey(path.m_nodes.Last()))) {
                        mergePathDict.Add(path.m_nodes.Last(), average);
                    }
                    else if (!mergePathDict.ContainsKey(path.m_nodes.First())) {
                        mergePathDict.Add(path.m_nodes.First(), mergePathDict[path.m_nodes.Last()]);
                    }
                }
            }

            var finalPathList = new List<BuildingInfo.PathInfo>();
            for (var i = 0; i < pathList.Count(); i++) {
                var path = pathList[i];
                if (tinyPaths.Contains(path)) {
                    continue;
                }
                var nodeList = new List<Vector3>();
                for (var j = 0; j < path.m_nodes.Count(); j++) {
                    var newNode = path.m_nodes[j];
                    if (mergePathDict.ContainsKey(path.m_nodes[j])) {
                        newNode = mergePathDict[path.m_nodes[j]];
                    }
                    nodeList.Add(newNode);
                }
                path.m_nodes = nodeList.ToArray();
                SetCurveTargets(path);
                finalPathList.Add(path);
            }
            return finalPathList;
        }
        private static NetInfo FindNetInfo(string prefabName) {
            return PrefabCollection<NetInfo>.FindLoaded(prefabName);
        }
        private static BuildingInfo.PathInfo ChainPath(BuildingInfo.PathInfo startPath, Vector3 endNode, int startNodeIndex = -1, NetInfo info = null) {

            var newNodes = new List<Vector3>();
            var newPath = startPath.ShallowClone();
            if (startNodeIndex == -1 || startNodeIndex >= startPath.m_nodes.Count()) {
                startNodeIndex = startPath.m_nodes.Count() - 1;
            }
            if (info != null) {
                newPath.AssignNetInfo(info);
            }
            var startNode = startPath.m_nodes[startNodeIndex];
            newNodes.Add(startNode);
            newNodes.Add(endNode);

            newPath.m_nodes = newNodes.ToArray();
            MarkPathGenerated(newPath);
            return newPath;
        }

        private static void BendStationTrack(BuildingInfo.PathInfo stationPath, float aBendStrength) {
            var middle = GetMiddle(stationPath);
            var newX = (stationPath.m_nodes.First().z - stationPath.m_nodes.Last().z) / 2;
            var newY = (stationPath.m_nodes.First().y + stationPath.m_nodes.Last().y) / 2;
            var newZ = -(stationPath.m_nodes.First().x - stationPath.m_nodes.Last().x) / 2;
            var newCurve = middle + (aBendStrength * new Vector3(newX, newY, newZ));
            stationPath.m_curveTargets[0] = newCurve;
        }
        private static void BendStationTrack() {
            var stationPaths = m_Info.m_paths.Where(p => p?.m_netInfo != null && p.m_netInfo.IsUndergroundMetroStationTrack()).ToList();
            for (var i = 0; i < stationPaths.Count(); i++) {
                var stationPath = stationPaths[i];
                BendStationTrack(stationPath, m_BendStrength);
                var stairPaths = m_Info.m_paths.Where(p => p != null && stationPaths.Contains(p) == false && p.m_nodes.Any(n => n.y == stationPath.m_nodes.First().y)).ToList();
                for (var j = 0; j < stairPaths.Count(); j++) {
                    BendStationTrack(stairPaths[j], -m_BendStrength * StairCoeff);
                }
            }
        }

        private static void CleanUpPaths(BuildingInfo info) {
            info.m_paths = info.m_paths.Where(p => !IsPathGenerated(p)).ToArray();
        }

        private static void CleanUpPaths() {
            m_Info.m_paths = m_Info.m_paths.Where(p => !IsPathGenerated(p)).ToArray();
        }

        private static void RecalculateSpawnPoints() {
            var buildingAI = m_Info?.GetComponent<DepotAI>();
            var paths = m_Info?.m_paths;
            if (buildingAI == null || paths == null) {
                return;
            }
            var spawnPoints = (from path in paths
                               where IsVehicleStop(path)
                               select GetMiddle(path, true)).Distinct().ToArray();

            switch (spawnPoints.Length) {
                case 0:
                    buildingAI.m_spawnPosition = Vector3.zero;
                    buildingAI.m_spawnTarget = Vector3.zero;
                    buildingAI.m_spawnPoints = new DepotAI.SpawnPoint[] { };
                    break;
                case 1:
                    buildingAI.m_spawnPosition = spawnPoints[0];
                    buildingAI.m_spawnTarget = spawnPoints[0];
                    buildingAI.m_spawnPoints = new[]
                    {
                        new DepotAI.SpawnPoint
                        {
                            m_position =  spawnPoints[0],
                            m_target =  spawnPoints[0]
                        }
                    };
                    break;
                default:
                    buildingAI.m_spawnPosition = Vector3.zero;
                    buildingAI.m_spawnTarget = Vector3.zero;
                    var spawnPointList = new List<DepotAI.SpawnPoint>();
                    foreach (var msp in buildingAI.m_spawnPoints) {
                        spawnPointList.Add(msp);
                    }
                    foreach (var sp in spawnPoints) {
                        spawnPointList.Add(new DepotAI.SpawnPoint() { m_position = sp, m_target = sp });
                    }
                    buildingAI.m_spawnPoints = spawnPointList.ToArray();
                    break;
            }
        }

        public static void HandleSubBuildings()
        {
            Debug.Log("Investigating " + m_Info.name);
            if (m_Info?.m_subBuildings != null && m_Info.m_subBuildings.Count() > 0)
            {
                for (int i = 0; i < m_Info.m_subBuildings.Count(); i++)
                {
                    var subBuilding = m_Info.m_subBuildings[i];
                    if (subBuilding?.m_buildingInfo?.m_paths != null && subBuilding.m_buildingInfo.HasUndergroundMetroStationTracks())
                    {
                        CleanUpPaths(subBuilding.m_buildingInfo);
                        var theAngle = m_Angle;
                        if (m_Info.name == "Large Airport")
                        {
                            if (subBuilding.m_buildingInfo.name == "Integrated Metro Station")
                            {
                                theAngle += 45;
                                subBuilding.m_angle = 0;
                                var offset = new Vector3(14, 0, 20);
                                subBuilding.m_position = offset;
                                var sbInsidePath = subBuilding.m_buildingInfo.m_paths.FirstOrDefault(p => p.m_nodes.Any(n => n == new Vector3(0, -4, 6)));
                                var airportEntranceLeft = new Vector3(-40, 0, -27);
                                var airportEntranceRight = new Vector3(10, 0, 40);
                                var midpoint = Vector3.Lerp(airportEntranceLeft, airportEntranceRight, 0.5f);
                                midpoint = new Vector3(midpoint.x, -4, midpoint.z);
                                var keyNodeIndex = 0;
                                var superKeyNode = AdjForOffset(sbInsidePath.m_nodes[keyNodeIndex], offset);
                                var adjOtherNode = AdjForOffset(Vector3.Lerp(superKeyNode, midpoint, 0.5f), offset, false);
                                var pathList = new List<BuildingInfo.PathInfo>();
                                BuildingInfo.PathInfo thePath = null;
                                for (int j = 0; j < subBuilding.m_buildingInfo.m_paths.Count(); j++)
                                {
                                    var path = subBuilding.m_buildingInfo.m_paths[j];
                                    if (path.m_nodes.Contains(new Vector3(0, -4, 6)))
                                    {
                                        var nodeList = new List<Vector3>() { new Vector3(0, -4, 6), adjOtherNode };
                                        path.m_nodes = nodeList.ToArray();
                                        thePath = path;
                                    }
                                    pathList.Add(path);
                                }
                                var airportEntranceLeftToSub = AdjForOffset(airportEntranceLeft, offset, false);
                                var airportEntranceRightToSub = AdjForOffset(airportEntranceRight, offset, false);
                                var pathBranchLeft = ChainPath(thePath, airportEntranceLeftToSub, 1);
                                var pathBranchRight = ChainPath(thePath, airportEntranceRightToSub, 1);
                                pathList.Add(pathBranchLeft);
                                pathList.Add(pathBranchRight);
                                subBuilding.m_buildingInfo.m_paths = pathList.ToArray();
                            }
                        }
                        ModifyStation(subBuilding.m_buildingInfo, m_TargetDepth, m_TargetStationTrackLength, theAngle, m_BendStrength);
                    }
                }
            }
        }
        private static bool IsVehicleStop(BuildingInfo.PathInfo path) {
            return (path?.m_nodes?.Length ?? 0) > 1 && (path?.m_netInfo?.IsUndergroundMetroStationTrack() ?? false);
        }

        private static void ResizeUndergroundStationTracks() {
            var linkedStationTracks = GetInterlinkedStationTracks();
            var processedConnectedPaths = new List<int>();
            for (var index = 0; index < m_Info.m_paths.Length; index++) {
                var path = m_Info.m_paths[index];
                if (!path.m_netInfo.IsUndergroundMetroStationTrack())
                    continue;

                if (!linkedStationTracks.Contains(path)) 
                    ChangeStationTrackLength(m_Info.m_paths, index, processedConnectedPaths); 
            }
        }
        private static void ChangeStationDepthAndRotation() {
            var pathList = new List<BuildingInfo.PathInfo>();
            var highestStation = float.MinValue;
            var totalNode = Vector3.zero;
            foreach (var path in m_Info.m_paths) {

                if (path.m_netInfo?.m_netAI == null || !path.m_netInfo.IsUndergroundMetroStationTrack())
                    continue;
                
                var highest = path.m_nodes.OrderByDescending(n => n.y).FirstOrDefault().y;
                highestStation = Math.Max(highest, highestStation);
            }
            var offsetDepthDist = m_TargetDepth + highestStation;

            var lowestHighPaths = m_Info.m_paths.Where(p => IsPedestrianPath(p) && p.m_nodes.Any(n => n.y > -4) && p.m_nodes.Any(nd => nd.y <= -4)).ToList();
            if (lowestHighPaths.Count == 0) {
                lowestHighPaths.Add(m_Info.m_paths.Where(p => IsPedestrianPath(p))
                    .OrderByDescending(p => p.m_nodes[0].y)
                    .FirstOrDefault());
            }
            if (lowestHighPaths.Count > 0) {
                var angleDelta = CalculateAngleDelta();
                for (var i = 0; i < m_Info.m_paths.Count(); i++) {
                    var path = m_Info.m_paths[i];
                    if (AllNodesUnderGround(path)) {
                        if (lowestHighPaths.Contains(path)) {
                            path.AssignNetInfo("Pedestrian Connection Underground");
                            path.m_forbidLaneConnection = new[] { false, true };
                        }
                        else {
                            if (IsPedestrianPath(path)) {
                                pathList.Add(path);
                                continue;
                            }
                            ChangePathRotation(path, angleDelta);
                            DipPath(path, offsetDepthDist);
                        }
                    }
                    pathList.Add(path);
                }
                m_Info.m_paths = pathList.ToArray();
            }
        }

        private static Vector3 GetMeanVector(IEnumerable<BuildingInfo.PathInfo> pathList) {
            List<Vector3> vectorList = pathList.SelectMany(p => p.m_nodes).ToList();
            return GetMeanVector(vectorList);
        }
        private static Vector3 GetMeanVector(List<Vector3> vectorList) {
            if (vectorList.Count > 0) {
                float x = 0f;
                float y = 0f;
                float z = 0f;
                foreach (Vector3 v in vectorList) {
                    x += v.x;
                    y += v.y;
                    z += v.z;
                }
                return new Vector3(x / vectorList.Count, y / vectorList.Count, z / vectorList.Count);
            }
            return Vector3.zero;


        }
        private static void DipPath(BuildingInfo.PathInfo path, float depthOffsetDist) {
            ShiftPath(path, new Vector3(0, -depthOffsetDist, 0));
        }

        private static void ShiftPath(BuildingInfo.PathInfo path, Vector3 offset) {
            for (var i = 0; i < path.m_nodes.Length; i++) {
                path.m_nodes[i] = path.m_nodes[i] + offset;
            }
            for (var i = 0; i < path.m_curveTargets.Length; i++) {
                path.m_curveTargets[i] = path.m_curveTargets[i] + offset;
            }
        }

        private static bool IsPedestrianPath(BuildingInfo.PathInfo path) {
            return path.m_netInfo.IsPedestrianNetwork();
        }
        private static void SetCurveTargets(BuildingInfo.PathInfo path) {
            if (path.m_nodes.Length < 2) {
                return;
            }
            var newCurveTargets = new List<Vector3>();
            if (path.m_curveTargets != null && path.m_curveTargets.Length > 0)
                newCurveTargets.AddRange(path.m_curveTargets);
            else
                newCurveTargets.Add(new Vector3());
            newCurveTargets[0] = GetMiddle(path);
            path.m_curveTargets = newCurveTargets.ToArray();
        }

        //private static IEnumerable<BuildingInfo.PathInfo> GenerateSteps(BuildingInfo.PathInfo path, float depth, double angle, bool isBound = true)
        //{
        //    var newPath = path.ShallowClone();
        //    var pathList = new List<BuildingInfo.PathInfo>();
        //    Vector3 lastCoords;
        //    Vector3 currCoords;
        //    if (AllNodesUnderGround(newPath))
        //    {
        //        lastCoords = newPath.m_nodes.OrderBy(n => n.z).FirstOrDefault();
        //        currCoords = newPath.m_nodes.OrderBy(n => n.z).LastOrDefault();
        //    }
        //    else
        //    {
        //        lastCoords = newPath.m_nodes.OrderBy(n => n.y).LastOrDefault();
        //        currCoords = newPath.m_nodes.OrderBy(n => n.y).FirstOrDefault();
        //    }
        //    var dir = new Vector3();

        //    MarkPathGenerated(newPath);
        //    newPath.AssignNetInfo("Pedestrian Connection Underground");
        //    var steps = (float)Math.Floor((depth + 4) / 12) * 4;
        //    if (steps == 0)
        //    {
        //        currCoords.y -= depth;
        //        newPath.m_nodes = new[] { lastCoords, currCoords };
        //        SetCurveTargets(newPath);
        //        ChangePathRotation(newPath, angle);
        //        pathList.Add(newPath);
        //    }
        //    else
        //    {
        //        float binder = 8;
        //        float stepDepth = binder / 2;
        //        var depthLeft = depth;
        //        if (isBound)
        //        {
        //            dir.x = Math.Min(currCoords.x - lastCoords.x, binder);
        //            dir.z = Math.Min(currCoords.z - lastCoords.z, binder);
        //        }
        //        else
        //        {
        //            dir.x = currCoords.x - lastCoords.x;
        //            dir.z = currCoords.z - lastCoords.z;
        //        }
        //        dir.y = currCoords.y - lastCoords.y;
        //        for (var j = 0; j < steps; j++)
        //        {
        //            var newZ = currCoords.z + dir.x;
        //            var newX = currCoords.x - dir.z;
        //            lastCoords = currCoords;
        //            currCoords = new Vector3() { x = newX, y = currCoords.y - Math.Max(0, Math.Min(stepDepth, depthLeft)), z = newZ };
        //            depthLeft -= stepDepth;
        //            var newNodes = new[] { lastCoords, currCoords };
        //            newPath = newPath.ShallowClone();
        //            newPath.m_nodes = newNodes;
        //            SetCurveTargets(newPath);
        //            ChangePathRotation(newPath, angle);
        //            pathList.Add(newPath);
        //            if (isBound)
        //            {
        //                dir.x = Math.Min(currCoords.x - lastCoords.x, binder);
        //                dir.z = Math.Min(currCoords.z - lastCoords.z, binder);
        //            }
        //            else
        //            {
        //                dir.x = currCoords.x - lastCoords.x;
        //                dir.z = currCoords.z - lastCoords.z;
        //            }
        //            dir.y = currCoords.y - lastCoords.y;
        //        }
        //    }
        //    return pathList;
        //}

        private static void ChangeStationTrackLength(IList<BuildingInfo.PathInfo> assetPaths, int pathIndex, ICollection<int> processedConnectedPaths) {
            var path = assetPaths[pathIndex];
            if (path.m_netInfo == null || !path.m_netInfo.IsUndergroundMetroStationTrack()) {
                return;
            }
            if (path.m_nodes.Length < 2) {
                return;
            }
            var beginning = path.m_nodes.First();
            var end = path.m_nodes.Last();
            var middle = GetMiddle(path);
            var length = 0.0f;
            for (var i = 1; i < path.m_nodes.Length; i++) {
                length += Vector3.Distance(path.m_nodes[i], path.m_nodes[i - 1]);
            }
            if (Math.Abs(length) < TOLERANCE) {
                return;
            }
            var adjTrackLength = m_TargetStationTrackLength * (((2 * Math.Abs(m_BendStrength) * Math.Sqrt(2)) / Math.PI) - Math.Abs(m_BendStrength) + 1);
            var coefficient = (float)Math.Abs(adjTrackLength / length);
            for (var i = 0; i < path.m_nodes.Length; i++) {
                path.m_nodes[i] = new Vector3
                {
                    x = middle.x + ((path.m_nodes[i].x - middle.x) * coefficient),
                    y = middle.y + ((path.m_nodes[i].y - middle.y) * coefficient),
                    z = middle.z + ((path.m_nodes[i].z - middle.z) * coefficient),
                };
            }
            for (var i = 0; i < path.m_curveTargets.Length; i++) {
                path.m_curveTargets[i] = new Vector3
                {
                    x = middle.x + (path.m_curveTargets[i].x - middle.x) * coefficient,
                    y = middle.y + (path.m_curveTargets[i].y - middle.y) * coefficient,
                    z = middle.z + (path.m_curveTargets[i].z - middle.z) * coefficient,
                };
            }
            var newBeginning = path.m_nodes.First();
            var newEnd = path.m_nodes.Last();

            ChangeConnectedPaths(assetPaths, beginning, newBeginning - beginning, processedConnectedPaths);
            ChangeConnectedPaths(assetPaths, end, newEnd - end, processedConnectedPaths);

        }
        private static double CalculateAngleDelta() {
            var stationTracks = m_Info.m_paths.Where(p => p.m_netInfo != null && p.m_netInfo.IsUndergroundMetroStationTrack() && p.m_nodes.Count() > 1).ToList();
            BuildingInfo.PathInfo mainTrack = null;
            if (m_PremierPath > -1 && stationTracks.Count > m_PremierPath) {
                mainTrack = stationTracks[m_PremierPath];
            }
            else {
                mainTrack = stationTracks.OrderBy(s => Vector3.Magnitude(GetMiddle(s))).ThenBy(s2 => GetMiddle(s2).y).FirstOrDefault();
                m_PremierPath = stationTracks.IndexOf(mainTrack);
            }
            float delta = m_Angle - m_PrevAngle;
            m_PrevAngle = m_Angle;
            delta *= (float)(Math.PI / 180);
            return delta;
        }
        private static Vector3 ChangePathRotation(BuildingInfo.PathInfo path, double angle) {

            Vector3 newNode = Vector3.zero;
            if (path.m_nodes != null && path.m_nodes.Length > 0) {
                for (var nodeIndex = 0; nodeIndex < path.m_nodes.Count(); nodeIndex++) {
                    var oldNode = path.m_nodes[nodeIndex];
                    newNode = new Vector3
                    {
                        x = (float)((oldNode.x * Math.Cos(angle)) - (oldNode.z * Math.Sin(angle))),
                        y = oldNode.y,
                        z = (float)((oldNode.x * Math.Sin(angle)) + (oldNode.z * Math.Cos(angle)))
                    };
                    path.m_nodes[nodeIndex] = newNode;
                }
            }
            if (path.m_curveTargets != null && path.m_curveTargets.Length > 0) {
                for (var curveIndex = 0; curveIndex < path.m_curveTargets.Count(); curveIndex++) {
                    var oldCurve = path.m_curveTargets[curveIndex];
                    var newCurve = new Vector3
                    {
                        x = (float)((oldCurve.x * Math.Cos(angle)) - (oldCurve.z * Math.Sin(angle))),
                        y = oldCurve.y,
                        z = (float)((oldCurve.x * Math.Sin(angle)) + (oldCurve.z * Math.Cos(angle))),
                    };
                    path.m_curveTargets[curveIndex] = newCurve;
                }
            }
            return newNode;
        }
        private static void ChangeConnectedPaths(IList<BuildingInfo.PathInfo> assetPaths, Vector3 nodePoint, Vector3 delta, ICollection<int> processedConnectedPaths) {
            for (var pathIndex = 0; pathIndex < assetPaths.Count; pathIndex++) {
                var path = assetPaths[pathIndex];
                if (path?.m_netInfo == null || path.m_netInfo.IsUndergroundMetroStationTrack()) {
                    continue;
                }
                if (processedConnectedPaths.Contains(pathIndex) || !path.m_nodes.Where(n => n == nodePoint).Any()) {
                    continue;
                }
                var beginning = path.m_nodes.First();
                var end = path.m_nodes.Last();
                if (path.m_netInfo.m_netAI.IsUnderground()) {
                    for (var i = 0; i < path.m_nodes.Length; i++) {
                        if (path.m_nodes[i].y <= -12f || path.m_nodes[i] == nodePoint) {
                            continue;
                        }
                        path.m_nodes[i] = new Vector3
                        {
                            x = path.m_nodes[i].x,
                            y = -DEF_DEPTH,
                            z = path.m_nodes[i].z
                        };
                        SetCurveTargets(path);
                    }
                }
                ShiftPath(path, delta);
                processedConnectedPaths.Add(pathIndex);
                var newBeginning = path.m_nodes.First();
                var newEnd = path.m_nodes.Last();

                ChangeConnectedPaths(assetPaths, beginning, newBeginning - beginning, processedConnectedPaths);
                ChangeConnectedPaths(assetPaths, end, newEnd - end, processedConnectedPaths);
            }
        }

        private static bool IsPathGenerated(BuildingInfo.PathInfo path) {
            return Math.Abs(path.m_maxSnapDistance - GENERATED_PATH_MARKER) < TOLERANCE;
        }

        private static void MarkPathGenerated(BuildingInfo.PathInfo newPath) {
            newPath.m_maxSnapDistance = GENERATED_PATH_MARKER;
        }

        private static BuildingInfo.PathInfo[] GetInterlinkedStationTracks() {
            var pairs =
                m_Info.m_paths.Where(p => p.m_netInfo.IsUndergroundMetroStationTrack()).SelectMany(p => p.m_nodes)
                    .GroupBy(n => n)
                    .Where(grp => grp.Count() > 1)
                    .Select(grp => grp.Key)
                    .ToArray();
            return m_Info.m_paths.Where(p => p.m_netInfo.IsUndergroundMetroStationTrack() && p.m_nodes.Any(n => pairs.Contains(n))).ToArray();
        }

        private static Vector3 GetMiddle(BuildingInfo.PathInfo path, bool useBendStrength = false) {
            var aBendStrength = useBendStrength ? m_BendStrength : 0;
            var midPoint = (path.m_nodes.First() + path.m_nodes.Last()) / 2;
            var distance = Vector3.Distance(midPoint, path.m_nodes.First());
            var xCoeff = -(path.m_nodes.First().x - path.m_nodes.Last().x) / Vector3.Distance(path.m_nodes.First(), path.m_nodes.Last());
            var zCoeff = (path.m_nodes.First().z - path.m_nodes.Last().z) / Vector3.Distance(path.m_nodes.First(), path.m_nodes.Last());
            var bendCoeff = aBendStrength * (Math.Sqrt(2) - 1);
            var adjMidPoint = new Vector3()
            {
                x = midPoint.x + (float)(-zCoeff * distance * bendCoeff),
                y = midPoint.y,
                z = midPoint.z + (float)(xCoeff * distance * bendCoeff)
            };
            return adjMidPoint;
        }

        private static bool AllNodesUnderGround(BuildingInfo.PathInfo path) {
            return path.m_nodes.All(n => n.y <= -4);
        }
        private static bool BuildingHasPedestrianConnectionSurface() {
            return m_Info.m_paths.Count(p => IsPedestrianPath(p)) >= 1;
        }
        private static List<Vector3> GetIsoltedNodes() {
            return m_Info.m_paths
                .Where(p => IsPedestrianPath(p))
                .SelectMany(p => p.m_nodes).GroupBy(x => x)
                .Where(g => g.Count() == 1)
                .Select(y => y.Key)
                .ToList();
        }
        private static List<BuildingInfo.PathInfo> GetIsolatedPaths() {
            List<BuildingInfo.PathInfo> isolatedPaths = null;
            var query = GetIsoltedNodes();
            if (query.Count() > 0) {
                isolatedPaths = m_Info.m_paths.Where(p => p.m_nodes.All(n => query.Contains(n))).ToList();
            }
            return isolatedPaths;
        }
        private static void CheckPedestrianConnections() {
            var isolatedPaths = GetIsolatedPaths();
            if (isolatedPaths != null) {
                var straddlePaths = isolatedPaths.Where(p => p.m_nodes.Any(n => n.y >= 0));
                if (isolatedPaths.Count > 0 && straddlePaths.Count() == 0) {
                    var pathList = m_Info.m_paths.ToList();
                    foreach (var path in isolatedPaths) {
                        var newStub = AddStub(path);
                        if (newStub != null) {
                            pathList.Add(newStub);
                        }
                    }
                    m_Info.m_paths = pathList.ToArray();
                }
            }
            //return m_Info.m_paths.Count(p => p.m_netInfo != null && p.m_netInfo.name == "Pedestrian Connection Surface" || p.m_netInfo.name == "Pedestrian Connection Inside");
        }
        private static BuildingInfo.PathInfo AddStub(BuildingInfo.PathInfo path) {
            var pathLastIndex = path?.m_nodes?.Count() - 1 ?? 0;
            if (pathLastIndex < 1 || path.m_nodes.Any(n => n.y >= 0)) {
                return null;
            }
            var newPath = path.ShallowClone();
            var newPathNodes = new List<Vector3>();
            var pathNodeDiff = path.m_nodes.Last() - path.m_nodes.First();
            //var pathLengthCoeff = Vector3.Distance(path.m_nodes.First(), path.m_nodes.Last()) / 3;
            newPathNodes.Add(new Vector3() { x = path.m_nodes.First().x - pathNodeDiff.x, y = 0, z = path.m_nodes.First().z - pathNodeDiff.z });
            newPathNodes.Add(path.m_nodes.First());
            newPath.m_nodes = newPathNodes.ToArray();
            //MarkPathGenerated(newPath);
            return newPath;
        }
    }
}
