using Sandbox.Game.EntityComponents;
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
            public Vector3D defaultDir;
            public bool restMode = false;
            public bool inactive = false;

            Vector3D currentTargetDir;

            INISerializer turretConfig;
            public double azimuthMultiplier { get { return (double)turretConfig.GetValue("azimuthMultiplier"); } }
            public double elevationMultiplier { get { return (double)turretConfig.GetValue("elevationMultiplier"); } }

            RotorControl rotorControl;
            IngameTime ingameTime;
            List<IMyUserControllableGun> gatlingGuns;


            public GatlingTurretGroup(List<IMyMotorStator> rotors, IngameTime ingameTime, string azimuthTag, string elevationTag)
            {
                Setup(rotors, ingameTime, azimuthTag, elevationTag);
            }

            public GatlingTurretGroup(IMyBlockGroup turretGroup, IngameTime ingameTime, string azimuthTag, string elevationTag)
            {
                var rotors = new List<IMyMotorStator>();
                turretGroup.GetBlocksOfType(rotors);

                Setup(rotors, ingameTime, azimuthTag, elevationTag);
            }

            public void Setup(List<IMyMotorStator> rotors, IngameTime ingameTime, string azimuthTag, string elevationTag)
            {
                this.ingameTime = ingameTime;

                List<IMyMotorStator> elevation = rotors.Where(x => x.CustomName.Contains(elevationTag)).ToList();
                IMyMotorStator azimuth = rotors.First(x => x.CustomName.Contains(azimuthTag));
                defaultDir = azimuth.WorldMatrix.Forward;

                gatlingGuns = new List<IMyUserControllableGun>();

                List<RotorControl.RotorReferencePair> elevationPairs = Select(elevation, gatlingGuns);

                RotorControl.RotorReferencePair azimuthPair = new RotorControl.RotorReferencePair { rotor = azimuth, reference = gatlingGuns[0] };

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
                    rotorControl.AimAtTarget(defaultDir, azimuthMultiplier, elevationMultiplier);
                    return;
                }

                rotorControl.AimAtTarget(currentTargetDir, azimuthMultiplier, elevationMultiplier);
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

            public void TargetDirection(Vector3D targetDirection)
            {
                currentTargetDir = targetDirection;
                restMode = false;
            }

            public void TargetPosition(Vector3D position)
            {
                Vector3D targetdir = Vector3D.Normalize(position - rotorControl.azimuth.rotor.GetPosition());
                TargetDirection(targetdir);
            }

            private void OnTarget(bool val)
            {
                if (val)
                {
                    if (currentTargetDir != Vector3D.Zero)
                    {
                        FireGuns(true);
                        rotorControl.Lock(false);
                        return;
                    }

                    if (rotorControl.currentAccuracy > 0.9999)
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
