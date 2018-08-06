﻿using LunaCommon;
using UnityEngine;

namespace LunaClient.Systems.VesselPositionSys.ExtensionMethods
{
    public static class VeselPositioner
    {
        public static void SetVesselPosition(this Vessel vessel, VesselPositionUpdate update, VesselPositionUpdate target, float percentage)
        {
            if (vessel == null) return;

            var lerpedBody = percentage < 0.5 ? update.Body : target.Body;

            if (vessel.isEVA && vessel.loaded)
            {
                ApplyPositionsToEva(vessel, update, target, lerpedBody, percentage);
                return;
            }

            ApplyOrbitInterpolation(vessel, update, target, lerpedBody, percentage);

            //Do not use CoM. It's not needed and it generate issues when you patch the protovessel with it as it generate weird commnet lines
            //It's important to set the static pressure as otherwise the vessel situation is not updated correctly when
            //Vessel.updateSituation() is called in the Vessel.LateUpdate(). Same applies for landed and splashed
            vessel.staticPressurekPa = FlightGlobals.getStaticPressure(target.LatLonAlt[2], lerpedBody);
            vessel.heightFromTerrain = target.HeightFromTerrain;

            if (!vessel.loaded)
            {
                //DO NOT lerp the latlonalt as otherwise if you are in orbit you will see landed vessels in the map view with weird jittering
                vessel.latitude = target.LatLonAlt[0];
                vessel.longitude = target.LatLonAlt[1];
                vessel.altitude = target.LatLonAlt[2];

                if (vessel.LandedOrSplashed)
                    vessel.SetPosition(lerpedBody.GetWorldSurfacePosition(vessel.latitude, vessel.longitude, vessel.altitude));
            }
            else
            {
                ApplyInterpolationsToLoadedVessel(vessel, update, target, lerpedBody, percentage);
            }
        }

        private static void ApplyPositionsToEva(Vessel vessel, VesselPositionUpdate update, VesselPositionUpdate target, CelestialBody lerpedBody, float percentage)
        {
            vessel.latitude = LunaMath.Lerp(update.LatLonAlt[0], target.LatLonAlt[0], percentage);
            vessel.longitude = LunaMath.Lerp(update.LatLonAlt[1], target.LatLonAlt[1], percentage);
            vessel.altitude = LunaMath.Lerp(update.LatLonAlt[2], target.LatLonAlt[2], percentage);

            vessel.Landed = percentage < 0.5 ? update.Landed : target.Landed;
            vessel.Splashed = percentage < 0.5 ? update.Splashed : target.Splashed;

            var currentSurfaceRelRotation = Quaternion.Slerp(update.SurfaceRelRotation, target.SurfaceRelRotation, percentage);
            vessel.SetRotation((Quaternion)lerpedBody.rotation * currentSurfaceRelRotation, true);
            vessel.srfRelRotation = currentSurfaceRelRotation;

            ApplyOrbitInterpolation(vessel, update, target, lerpedBody, percentage);

            //We don't do the surface positioning as with vessels because kerbals don't walk at high speeds and with this code it will be enough ;)
            if (vessel.LandedOrSplashed || vessel.situation <= Vessel.Situations.FLYING)
                vessel.SetPosition(lerpedBody.GetWorldSurfacePosition(vessel.latitude, vessel.longitude, vessel.altitude));
        }

        private static void ApplyOrbitInterpolation(Vessel vessel, VesselPositionUpdate update, VesselPositionUpdate target, CelestialBody lerpedBody, float percentage)
        {
            var startTime = update.KspOrbit.epoch;
            var targetTime = target.KspOrbit.epoch;

            var currentPos = update.KspOrbit.getRelativePositionAtUT(startTime);
            var targetPos = target.KspOrbit.getRelativePositionAtUT(targetTime);

            var currentVel = update.KspOrbit.getOrbitalVelocityAtUT(startTime) + update.KspOrbit.referenceBody.GetFrameVelAtUT(startTime) - update.Body.GetFrameVelAtUT(startTime);
            var targetVel = target.KspOrbit.getOrbitalVelocityAtUT(targetTime) + target.KspOrbit.referenceBody.GetFrameVelAtUT(targetTime) - target.Body.GetFrameVelAtUT(targetTime);

            var lerpedPos = Vector3d.Lerp(currentPos, targetPos, percentage);
            var lerpedVel = Vector3d.Lerp(currentVel, targetVel, percentage);

            var lerpTime = LunaMath.Lerp(startTime, targetTime, percentage);

            vessel.orbit.UpdateFromStateVectors(lerpedPos, lerpedVel, lerpedBody, lerpTime);
        }

        private static void ApplyInterpolationsToLoadedVessel(Vessel vessel, VesselPositionUpdate update, VesselPositionUpdate target, CelestialBody lerpedBody, float percentage)
        {
            var currentSurfaceRelRotation = Quaternion.Slerp(update.SurfaceRelRotation, target.SurfaceRelRotation, percentage);

            //If you don't set srfRelRotation and vessel is packed it won't change it's rotation
            vessel.srfRelRotation = currentSurfaceRelRotation;
            vessel.SetRotation((Quaternion)lerpedBody.rotation * currentSurfaceRelRotation, true);
            
            if (vessel.situation <= Vessel.Situations.FLYING)
            {
                vessel.Landed = percentage < 0.5 ? update.Landed : target.Landed;
                vessel.Splashed = percentage < 0.5 ? update.Splashed : target.Splashed;

                var curPos = Vector3d.Lerp(update.LatLonAltPos, target.LatLonAltPos, percentage);
                vessel.SetPosition(curPos);
            }
            else
            {
                //Do not use vessel.orbit.epoch as otherwise the vessel drifts away when unpacked
                var posVec = vessel.orbit.getPositionAtUT(Planetarium.GetUniversalTime());
                vessel.SetPosition(posVec);
            }
        }
    }
}