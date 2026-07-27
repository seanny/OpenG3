using System;
using UnityEngine;
using GTA3Unity.Core;

namespace GTA3Unity.Vehicles
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
            bool isBus)
        {
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
                    definition.DamagedName);
                doorAnchor.transform.SetPositionAndRotation(
                    dummy.transform.position,
                    dummy.transform.rotation);
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
            switch (doorIndex)
            {
                case EVehicleDoorIndex.FrontLeft:
                    door.OnStart(
                        isBus ? -(Mathf.PI / 2.0f) : -(Mathf.PI / 0.4f),
                        0.0f,
                        0,
                        2);
                    break;

                case EVehicleDoorIndex.FrontRight:
                    door.OnStart(
                        0.0f,
                        isBus ? Mathf.PI / 2.0f : Mathf.PI / 0.4f,
                        0,
                        2);
                    break;

                case EVehicleDoorIndex.RearLeft:
                    door.OnStart(
                        isVan ? -(Mathf.PI / 2.0f) : -(Mathf.PI * 0.4f),
                        0.0f,
                        isVan ? 1 : 0,
                        2);
                    break;

                case EVehicleDoorIndex.RearRight:
                    door.OnStart(
                        0.0f,
                        isVan ? Mathf.PI / 2.0f : Mathf.PI * 0.4f,
                        isVan ? 0 : 1,
                        2);
                    break;

                case EVehicleDoorIndex.Bonnet:
                    if (handlingFlags.HasFlag(EHandlingFlags.RevBonnet))
                    {
                        door.OnStart(-(Mathf.PI * 0.3f), 0.0f, 1, 0);
                    }
                    else
                    {
                        door.OnStart(0.0f, Mathf.PI * 0.3f, 1, 0);
                    }
                    break;

                case EVehicleDoorIndex.Boot:
                    if (handlingFlags.HasFlag(EHandlingFlags.HangingBoot))
                    {
                        door.OnStart(-(Mathf.PI * 0.4f), 0.0f, 0, 0);
                    }
                    else if (handlingFlags.HasFlag(EHandlingFlags.TailGateBoot))
                    {
                        door.OnStart(0.0f, Mathf.PI / 2.0f, 1, 0);
                    }
                    else
                    {
                        door.OnStart(-(Mathf.PI * 0.3f), 0.0f, 1, 0);
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(doorIndex), doorIndex, null);
            }
        }

        private void PlaceDoorMeshes(
            Transform modelRoot,
            Transform doorFrame,
            string intactName,
            string damagedName)
        {
            for (int childIndex = 0; childIndex < modelRoot.childCount; childIndex++)
            {
                Transform child = modelRoot.GetChild(childIndex);
                if (child.name.Equals(intactName, StringComparison.Ordinal))
                {
                    child.SetPositionAndRotation(doorFrame.position, doorFrame.rotation);
                }

                if (child.name.Equals(damagedName, StringComparison.Ordinal))
                {
                    child.SetPositionAndRotation(doorFrame.position, doorFrame.rotation);
                    child.gameObject.SetActive(false);
                }
            }
        }
    }
}
