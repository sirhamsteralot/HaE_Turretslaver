﻿using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class GatlingTurretGroup : ITurretGroup
        {
            private const double accuracyVal = 0.9999;

            public Vector3D defaultDir;
            public bool restMode = false;
            public bool inactive = false;
            public bool restAfterReset = true;

            private bool firing = false;
            Vector3D currentTargetDir = Vector3D.Zero;

            INISerializer turretConfig;
            public double azimuthMultiplier { get { return (double)turretConfig.GetValue("azimuthMultiplier"); } }
            public double elevationMultiplier { get { return (double)turretConfig.GetValue("elevationMultiplier"); } }

            DeadzoneProvider deadzoneProvider;
            RotorControl rotorControl;
            IngameTime ingameTime;
            IMyUserControllableGun[] gatlingGuns;


            public GatlingTurretGroup(List<IMyMotorStator> rotors, IngameTime ingameTime, DeadzoneProvider deadzoneProvider, string azimuthTag, string elevationTag)
            {
                Setup(rotors, ingameTime, deadzoneProvider, azimuthTag, elevationTag);
            }

            public GatlingTurretGroup(IMyBlockGroup turretGroup, IngameTime ingameTime, DeadzoneProvider deadzoneProvider, string azimuthTag, string elevationTag)
            {
                var rotors = new List<IMyMotorStator>();
                turretGroup.GetBlocksOfType(rotors);

                Setup(rotors, ingameTime, deadzoneProvider, azimuthTag, elevationTag);
            }

            public void Setup(List<IMyMotorStator> rotors, IngameTime ingameTime, DeadzoneProvider deadzoneProvider, string azimuthTag, string elevationTag)
            {
                this.deadzoneProvider = deadzoneProvider;
                this.ingameTime = ingameTime;

                List<IMyMotorStator> elevation = rotors.Where(x => x.CustomName.Contains(elevationTag)).ToList();
                IMyMotorStator azimuth = rotors.First(x => x.CustomName.Contains(azimuthTag));
                defaultDir = azimuth.WorldMatrix.Forward;

                List<IMyUserControllableGun> gatlingGunsList = new List<IMyUserControllableGun>();

                List<RotorControl.RotorReferencePair> elevationPairs = Select(elevation, gatlingGunsList);

                RotorControl.RotorReferencePair azimuthPair = new RotorControl.RotorReferencePair { rotor = azimuth, reference = gatlingGunsList[0] };

                gatlingGuns = new IMyUserControllableGun[gatlingGunsList.Count];
                for(int i = 0; i < gatlingGunsList.Count; i++)
                {
                    gatlingGuns[i] = gatlingGunsList[i];
                }
                gatlingGunsList = null;

                rotorControl = new RotorControl(azimuthPair, elevationPairs);
                rotorControl.onTarget = OnTarget;
                rotorControl.useForward = true;

                #region configuration
                turretConfig = new INISerializer("TurretConfig");

                turretConfig.AddValue("azimuthMultiplier", x => double.Parse(x), -1.0);
                turretConfig.AddValue("elevationMultiplier", x => double.Parse(x), -1.0);

                if (rotorControl.azimuth.rotor.CustomData == "")
                {
                    string temp = rotorControl.azimuth.rotor.CustomData;
                    turretConfig.FirstSerialization(ref temp);
                    rotorControl.azimuth.rotor.CustomData = temp;
                }
                else
                {
                    turretConfig.DeSerialize(rotorControl.azimuth.rotor.CustomData);
                }
                #endregion
            }

            public void Tick()
            {
                if (restMode)
                    return;

                if (inactive)
                    return;

                if (currentTargetDir == Vector3D.Zero)
                {
                    FireGuns(false);
                    rotorControl.AimAtTarget(ref defaultDir, azimuthMultiplier, elevationMultiplier);
                    restAfterReset = true;
                    return;
                }

                rotorControl.AimAtTarget(ref currentTargetDir, azimuthMultiplier, elevationMultiplier);
            }

            public void DisableTurret(bool value)
            {
                inactive = value;
                rotorControl.azimuth.rotor.TargetVelocityRad = 0;
                foreach (var rotor in rotorControl.elevationRotors)
                {
                    rotor.rotor.TargetVelocityRad = 0;
                }

                rotorControl.Lock(value);
            }

            public TurretGroupUtils.TurretGroupStatus CheckGroupStatus()
            {
                var damageAmount = TurretGroupUtils.TurretGroupStatus.Active;

                if (rotorControl.azimuth.rotor.IsClosed())
                    return TurretGroupUtils.TurretGroupStatus.MajorDMG;
                if (rotorControl.azimuth.reference.IsClosed())
                    return TurretGroupUtils.TurretGroupStatus.MajorDMG;

                foreach (var elevationRot in rotorControl.elevationRotors)
                {
                    if (elevationRot.rotor.IsClosed())
                        return TurretGroupUtils.TurretGroupStatus.MajorDMG;

                    if (elevationRot.reference.IsClosed())
                        damageAmount = TurretGroupUtils.TurretGroupStatus.MinorDMG;
                }

                foreach (var gun in gatlingGuns)
                {
                    if (gun.IsClosed())
                        damageAmount = TurretGroupUtils.TurretGroupStatus.MinorDMG;
                }

                return damageAmount;
            }

            public void TargetDirection(ref Vector3D targetDirection)
            {
                if (targetDirection == Vector3D.Zero)
                {
                    currentTargetDir = targetDirection;
                    restMode = false;
                    restAfterReset = false;
                    return;
                }

                if (currentTargetDir != Vector3D.Zero && targetDirection != Vector3D.Zero && currentTargetDir.Dot(targetDirection) < 1 - accuracyVal)
                    return;

                bool blockInTheWay = deadzoneProvider.IsBlockInTheWay(rotorControl.azimuth.reference.GetPosition() + targetDirection * 2.5, rotorControl.azimuth.reference.GetPosition() + targetDirection * 800);
                if (blockInTheWay)
                {
                    currentTargetDir = Vector3D.Zero;
                    restAfterReset = true;
                    return;
                }

                currentTargetDir = targetDirection;
                restMode = false;
                restAfterReset = false;
            }

            public void TargetPosition(ref Vector3D position)
            {
                Vector3D targetdir = Vector3D.Normalize(position - rotorControl.azimuth.rotor.GetPosition());
                TargetDirection(ref targetdir);
            }

            private void OnTarget(bool val, RotorControl.RotorReferencePair pair)
            {
                if (val)
                {
                    if (currentTargetDir != Vector3D.Zero)
                    {
                        bool blockInTheWay = false;//deadzoneProvider.IsBlockInTheWay(rotorControl.azimuth.reference.GetPosition() + currentTargetDir * 2.5, rotorControl.azimuth.reference.GetPosition() + currentTargetDir * 800);
                        FireGuns(!blockInTheWay);
                        rotorControl.Lock(false);
                        return;
                    }

                    if (rotorControl.currentAccuracy > accuracyVal && restAfterReset)
                    {
                        rotorControl.Lock(true);
                        restMode = true;
                    }

                }
                else if (currentTargetDir != Vector3D.Zero)
                {
                    FireGuns(false);
                    rotorControl.Lock(false);
                }
            }

            private void FireGuns(bool fire)
            {
                if (fire == firing)
                    return;

                firing = fire;

                foreach (var gun in gatlingGuns)
                {
                    if (!gun.IsClosed())
                        gun.SetValueBool("Shoot", fire);
                }
            }

            private List<RotorControl.RotorReferencePair> Select(List<IMyMotorStator> elevation, List<IMyUserControllableGun> guns)
            {
                List<RotorControl.RotorReferencePair> elevationPairs = new List<RotorControl.RotorReferencePair>();
                int amountToRemove = 0;

                for (int i = 0; i < elevation.Count; i++)
                {
                    int prevTempCount = guns.Count;
                    elevation[i].TopGrid.GetCubesOfType(guns);

                    if (prevTempCount == guns.Count)
                    {
                        elevation.Move(i, elevation.Count - 1);
                        amountToRemove++;
                    }
                    else
                        elevationPairs.Add(new RotorControl.RotorReferencePair { rotor = elevation[i], reference = guns[prevTempCount] });
                }
                elevation.RemoveRange(elevation.Count - amountToRemove, amountToRemove);
                return elevationPairs;
            }
        }
    }
}
