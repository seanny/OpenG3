using System;
using GTA3Unity;
using GTA3Unity.Core;
using OpenG3.Vehicles;
using UnityEngine;

namespace OpenG3.Core
{
    public class WorldManager : MonoBehaviour
    {
        void Update()
        {
            if(FileLoader.Instance == null
                || FileLoader.Instance.IsDone == false
                || PlayerController.Instance == null)
            {
                return;
            }

            VehicleSpawning.RemoveNonMissionDistantOrWreckedSpawnedVehicles(PlayerController.Instance, Time.deltaTime);
        }

    }
}