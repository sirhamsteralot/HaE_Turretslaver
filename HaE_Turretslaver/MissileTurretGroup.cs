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
        public class MissileTurretGroup : ITurretGroup
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
            public int salvoSize { get { return (int)turretConfig.GetValue("salvoSize"); } }
            public double salvoTimeout { get { return (double)turretConfig.GetValue("salvoTimeout"); } }

            DeadzoneProvider deadzoneProvider;
            RotorControl rotorControl;
            List<IMySmallMissileLauncher> launchers;
            IngameTime ingameTime;
            GridTerminalSystemUtils GTS;


            public MissileTurretGroup(GridTerminalSystemUtils GTS, List<IMyMotorStator> rotors, IngameTime ingameTime, DeadzoneProvider deadzoneProvider, string azimuthTag, string elevationTag)
            {
                Setup(GTS, rotors, ingameTime, deadzoneProvider, azimuthTag, elevationTag);
            }

            public MissileTurretGroup(GridTerminalSystemUtils GTS, IMyBlockGroup turretGroup, IngameTime ingameTime, DeadzoneProvider deadzoneProvider, string azimuthTag, string elevationTag)
            {
                var rotors = new List<IMyMotorStator>();
                turretGroup.GetBlocksOfType(rotors);

                Setup(GTS, rotors, ingameTime, deadzoneProvider, azimuthTag, elevationTag);
            }

            public void Setup(GridTerminalSystemUtils GTS, List<IMyMotorStator> rotors, IngameTime ingameTime, DeadzoneProvider deadzoneProvider, string azimuthTag, string elevationTag)
            {
                this.GTS = GTS;
                this.deadzoneProvider = deadzoneProvider;
                this.ingameTime = ingameTime;

                List<IMyMotorStator> elevation = rotors.Where(x => x.CustomName.Contains(elevationTag)).ToList();
                IMyMotorStator azimuth = rotors.First(x => x.CustomName.Contains(azimuthTag));
                defaultDir = azimuth.WorldMatrix.Forward;

                launchers = Select(elevation);

                List<RotorControl.RotorReferencePair> elevationPairs = new List<RotorControl.RotorReferencePair>();
                for (int i = 0; i < elevation.Count; i++)
                {
                    elevationPairs.Add(new RotorControl.RotorReferencePair { rotor = elevation[i], reference = launchers[i] });
                }

                RotorControl.RotorReferencePair azimuthPair = new RotorControl.RotorReferencePair { rotor = azimuth, reference = launchers[0] };

                rotorControl = new RotorControl(azimuthPair, elevationPairs);
                rotorControl.onTarget = OnTarget;

                #region configuration
                turretConfig = new INISerializer("TurretConfig");

                turretConfig.AddValue("azimuthMultiplier", x => double.Parse(x), -1.0);
                turretConfig.AddValue("elevationMultiplier", x => double.Parse(x), -1.0);
                turretConfig.AddValue("salvoSize", x => int.Parse(x), 3);
                turretConfig.AddValue("salvoTimeout", x => double.Parse(x), 2.5);

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
                    FireMissiles(false);
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

                return damageAmount;
            }

            public void TargetDirection(ref Vector3D targetDirection)
            {
                if (currentTargetDir != Vector3D.Zero && targetDirection != Vector3D.Zero && currentTargetDir.Dot(targetDirection) < 1 - accuracyVal)
                    return;

                currentTargetDir = targetDirection;
                restMode = false;
                restAfterReset = false;
            }

            public void TargetPosition(ref Vector3D position)
            {
                Vector3D targetdir = Vector3D.Normalize(position - rotorControl.azimuth.rotor.GetPosition());
                TargetDirection(ref targetdir);
            }

            private void FireMissiles(bool fire)
            {
                restMode = false;
                restAfterReset = false;

                if (fire == firing)
                    return;

                firing = fire;

                foreach (var launcher in launchers)
                    if (!launcher.IsClosed())
                        launcher.SetValueBool("Shoot", fire);
            }

            private void OnTarget(bool val, RotorControl.RotorReferencePair pair)
            {
                if (val)
                {
                    if (currentTargetDir != Vector3D.Zero)
                    {
                        bool blockInTheWay = deadzoneProvider.IsBlockInTheWay(rotorControl.azimuth.reference.GetPosition() + currentTargetDir * 15, rotorControl.azimuth.reference.GetPosition() + currentTargetDir * 800);
                        FireMissiles(!blockInTheWay);
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
                    FireMissiles(false);
                    rotorControl.Lock(false);
                }
            }

            private List<IMySmallMissileLauncher> Select(List<IMyMotorStator> elevation)
            {
                var tmplist = new List<IMySmallMissileLauncher>();

                for (int i = 0; i < elevation.Count; i++)
                {
                    elevation[i].TopGrid.GetCubesOfType(GTS.GridTerminalSystem, tmplist);
                }
                return tmplist;
            }
        }
    }
}

