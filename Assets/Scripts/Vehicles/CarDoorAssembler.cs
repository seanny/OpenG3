using System;
using UnityEngine;
using GTA3Unity.Core;
using System.Collections.Generic;

namespace OpenG3.Vehicles
{
    /// <summary>
    /// Creates animated door anchors and places their intact and damaged meshes.
    /// </summary>
    internal sealed class CarDoorAssembler
    {
        private readonly struct DoorDefinition
        {
            public DoorDefinition(
                EVehicleDoorIndex index,
                string dummyName,
                string intactName,
                string damagedName)
            {
                Index = index;
                DummyName = dummyName;
                IntactName = intactName;
                DamagedName = damagedName;
            }

            public EVehicleDoorIndex Index { get; }
            public string DummyName { get; }
            public string IntactName { get; }
            public string DamagedName { get; }
        }

        private static readonly DoorDefinition[] s_DoorDefinitions =
        {
            new(EVehicleDoorIndex.Bonnet, "bonnet_dummy", "bonnet_hi_ok", "bonnet_hi_dam"),
            new(EVehicleDoorIndex.FrontLeft, "door_lf_dummy", "door_lf_hi_ok", "door_lf_hi_dam"),
            new(EVehicleDoorIndex.FrontRight, "door_rf_dummy", "door_rf_hi_ok", "door_rf_hi_dam"),
            new(EVehicleDoorIndex.RearLeft, "door_lr_dummy", "door_lr_hi_ok", "door_lr_hi_dam"),
            new(EVehicleDoorIndex.RearRight, "door_rr_dummy", "door_rr_hi_ok", "door_rr_hi_dam"),
            new(EVehicleDoorIndex.Boot, "boot_dummy", "boot_hi_ok", "boot_hi_dam")
        };

        internal void CreateDoors(
            GameObject model,
            HandlingData handlingData,
            bool isVan,
            bool isBus,
            out Dictionary<EVehicleDoorIndex, GameObject> intact,
            out Dictionary<EVehicleDoorIndex, GameObject> damaged)
        {
            intact = new();
            damaged = new();
            if (model == null)
            {
                return;
            }

            DummyObject[] dummies = model.GetComponentsInChildren<DummyObject>();
            for (int dummyIndex = 0; dummyIndex < dummies.Length; dummyIndex++)
            {
                DummyObject dummy = dummies[dummyIndex];
                if (!TryGetDefinition(dummy.name, out DoorDefinition definition))
                {
                    continue;
                }

                VehicleDoor doorAnchor = dummy.GetComponent<VehicleDoor>();
                if (doorAnchor == null)
                {
                    doorAnchor = dummy.gameObject.AddComponent<VehicleDoor>();
                }

                ConfigureDoor(doorAnchor, definition.Index, handlingData.Flags, isVan, isBus);
                PlaceDoorMeshes(
                    model.transform,
                    dummy.transform,
                    definition.IntactName,
                    definition.DamagedName,
                    out GameObject intactDoor,
                    out GameObject damagedDoor);
                doorAnchor.transform.SetPositionAndRotation(
                    dummy.transform.position,
                    dummy.transform.rotation);
                intactDoor.transform.SetParent(doorAnchor.transform, true);
                damagedDoor.transform.SetParent(doorAnchor.transform, true);
                intact.Add(definition.Index, intactDoor);
                damaged.Add(definition.Index, damagedDoor);
            }
        }

        private static bool TryGetDefinition(string dummyName, out DoorDefinition definition)
        {
            for (int definitionIndex = 0; definitionIndex < s_DoorDefinitions.Length; definitionIndex++)
            {
                DoorDefinition candidate = s_DoorDefinitions[definitionIndex];
                if (dummyName.Equals(candidate.DummyName, StringComparison.OrdinalIgnoreCase))
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = default;
            return false;
        }

        private static void ConfigureDoor(
            VehicleDoor door,
            EVehicleDoorIndex doorIndex,
            EHandlingFlags handlingFlags,
            bool isVan,
            bool isBus)
        {
            door.OnStart(doorIndex);
        }

        private void PlaceDoorMeshes(
            Transform modelRoot,
            Transform doorFrame,
            string intactName,
            string damagedName,
            out GameObject intact,
            out GameObject damaged)
        {
            intact = null;
            damaged = null;
            for (int childIndex = 0; childIndex < modelRoot.childCount; childIndex++)
            {
                Transform child = modelRoot.GetChild(childIndex);
                if (child.name.Equals(intactName, StringComparison.Ordinal))
                {
                    child.SetPositionAndRotation(doorFrame.position, doorFrame.rotation);
                    intact = child.gameObject;
                }

                if (child.name.Equals(damagedName, StringComparison.Ordinal))
                {
                    child.SetPositionAndRotation(doorFrame.position, doorFrame.rotation);
                    child.gameObject.SetActive(false);
                    damaged = child.gameObject;
                }
            }
        }
    }
}
