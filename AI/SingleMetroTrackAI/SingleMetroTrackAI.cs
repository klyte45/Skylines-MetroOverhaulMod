using ColossalFramework;
using System;

namespace MetroOverhaul.AI.SingleMetroTrackAI
{
    class SingleMetroTrackAI
    {
        private bool CheckSingleTrack2Ways(ushort vehicleID, Vehicle vehicleData, ref float maxSpeed, uint laneID, uint prevLaneID, ref bool mayNeedSingleTrackStationFix)
        {
            NetManager instance = Singleton<NetManager>.instance;
            ushort next_segment_id = instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_segment;
            ushort crt_segment_id = instance.m_lanes.m_buffer[(int)((UIntPtr)prevLaneID)].m_segment;

            ReservationManager instance2 = ReservationManager.instance;

            ushort leadingVehicleID = vehicleData.GetFirstVehicle(vehicleID);

            ReservationInfo ri = null;
            bool preventCheckNextLane = false;
            bool notifyFutureTrack = false;

            if (ReservationManager.RequireReservation(next_segment_id))
                ri = instance2.GetReservationOnSegment(next_segment_id);


            CreateReservation:
            if (ReservationManager.IsSingleTrack2WSegment(next_segment_id)) //train carriage will enter a one lane section
            {
                if (ri == null) //reserve track if it is not reserved by any train
                {
                    ushort blocking_segmentID = 0;

                    ri = ReservationManager.instance.CheckCachedReservation(next_segment_id, leadingVehicleID, ref blocking_segmentID);

                    if (ri == null) //no cached reservation found, create one
                    {
                        SingleTrack2WSection section = instance2.CreateSingleTrack2WSectionFromTrainPath(leadingVehicleID, next_segment_id);
                        if (section != null)
                        {
                            ri = ReservationManager.instance.RegisterNewReservation(section, leadingVehicleID, ref blocking_segmentID);
                        }
                    }

                    if (blocking_segmentID != 0) //reservation blocked by a further segment already reserved, get this reservation
                    {
                        ri = instance2.GetReservationOnSegment(blocking_segmentID);
                        /*ReservationManager.instance.EnqueueReservation(ri, leadingVehicleID);
                        maxSpeed = 0f;
                        return true;*/
                    }
                    else
                    {
                        mayNeedSingleTrackStationFix = true; //track reserved by this train
                    }
                }
            }

            if (ri != null)
            {

                if (ReservationManager.IsReservationForTrain(ri, leadingVehicleID)) //track is reserved for this vehicle
                {
                    notifyFutureTrack = true;

                    mayNeedSingleTrackStationFix = true; //track reserved by this train

                    //reset wait counter
                    /* if((vehicleData.m_flags2 & Vehicle.Flags2.Yielding) != (Vehicle.Flags2) 0)
                     {
                         vehicleData.m_flags2 &= ~Vehicle.Flags2.Yielding;
                         vehicleData.m_waitCounter = 0;
                     }*/


                    //return true so that CheckNextLane does not interfere (it causes train to stop when going from one track to double track with a train waiting in the opposite direction)
                    if (Util.noCheckOverlapOnLastSegment && next_segment_id == ri.section.segment_ids[ri.section.segment_ids.Count - 1])
                        preventCheckNextLane = true;
                }
                else //section reserved by another train
                {
                    //train has spawned on a station reserved to another train, though case...
                    //attempt destroy reservation and give priority to this train
                    if (ReservationManager.IsSingleTrackStation(crt_segment_id))
                    {
                        ReservationManager.instance.m_data.RemoveReservation(ri.ID, true);
                        ri = null;
                        goto CreateReservation;
                    }

                    SingleTrack2WSection section = instance2.CreateSingleTrack2WSectionFromTrainPath(leadingVehicleID, next_segment_id);
                    if (!(section != null && ReservationManager.instance.AttemptJoinReservation(ri, section, leadingVehicleID))) //can train follow the previous one?
                    {
                        if (!(section != null && ReservationManager.instance.AttemptReservationForNextPendingTrain(ri, section, leadingVehicleID))) //has section been cleared?
                        {
                            //not allowed on this track, stop
                            ReservationManager.instance.EnqueueReservation(ri, leadingVehicleID);
                            maxSpeed = 0f;
                            preventCheckNextLane = true;
                        }
                    }
                }

                //asses if single track station fix is necessary, before Notify which can cancel TrainAtStation status (if new front carriage is out of station for example)
                if (mayNeedSingleTrackStationFix && ri.status != ReservationStatus.TrainAtStation)
                    mayNeedSingleTrackStationFix = false;
            }

            if (ReservationManager.RequireReservation(crt_segment_id)) //train carriage is on a one lane section (or double track station which may belong to a single track section)
            {
                instance2.NotifyReservation(leadingVehicleID, crt_segment_id, true, vehicleData.m_flags);
            }

            if (notifyFutureTrack)
                instance2.NotifyReservation(leadingVehicleID, next_segment_id, false);

            return preventCheckNextLane;
        }
    }
}
