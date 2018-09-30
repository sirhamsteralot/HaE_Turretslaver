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
        public class TurretGroup
        {
            public Vector3D defaultDir;
            public bool restMode = false;

            Vector3D currentTargetDir;

            INISerializer turretConfig;
            public double azimuthMultiplier { get { return (double)turretConfig.GetValue("azimuthMultiplier"); } }
            public double elevationMultiplier { get { return (double)turretConfig.GetValue("elevationMultiplier"); } }
            public int salvoSize { get { return (int)turretConfig.GetValue("salvoSize"); } }
            public double salvoTimeout { get { return (double)turretConfig.GetValue("salvoTimeout"); } }
            
            RotorControl rotorControl;
            List<RotorLauncher> launchers = new List<RotorLauncher>();
            IngameTime ingameTime;

            

            public TurretGroup(List<IMyMotorStator> rotors, IngameTime ingameTime, string azimuthTag, string elevationTag)
            {
                Setup(rotors, ingameTime, azimuthTag, elevationTag);
            }

            public TurretGroup(IMyBlockGroup turretGroup, IngameTime ingameTime, string azimuthTag, string elevationTag)
            {
                var rotors = new List<IMyMotorStator>();
                turretGroup.GetBlocksOfType(rotors);

                Setup (rotors, ingameTime, azimuthTag, elevationTag);
            }

            public void Setup(List<IMyMotorStator> rotors, IngameTime ingameTime, string azimuthTag, string elevationTag)
            {
                this.ingameTime = ingameTime;

                List<IMyMotorStator> elevation = rotors.Where(x => x.CustomName.Contains(elevationTag)).ToList();
                IMyMotorStator azimuth = rotors.First(x => x.CustomName.Contains(azimuthTag));
                defaultDir = azimuth.WorldMatrix.Forward;

                List<IMyMotorStator> cannonBases = Select(elevation);

                List<RotorControl.RotorReferencePair> elevationPairs = new List<RotorControl.RotorReferencePair>();
                for (int i = 0; i < elevation.Count; i++)
                {
                    elevationPairs.Add(new RotorControl.RotorReferencePair { rotor = elevation[i], reference = cannonBases[i] });
                }

                RotorControl.RotorReferencePair azimuthPair = new RotorControl.RotorReferencePair { rotor = azimuth, reference = cannonBases[0] };

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

                foreach (var cannonbase in cannonBases)
                {
                    var launcher = new RotorLauncher(cannonbase, ingameTime, salvoTimeout);
                    launchers.Add(launcher);
                }
            }

            public void Tick()
            {
                if (restMode)
                    return;

                foreach (var gun in launchers)
                    gun.Tick();

                if (currentTargetDir == Vector3D.Zero)
                {
                    rotorControl.AimAtTarget(defaultDir, azimuthMultiplier, elevationMultiplier);
                    return;
                }

                rotorControl.AimAtTarget(currentTargetDir, azimuthMultiplier, elevationMultiplier);
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

            public void FireCannons()
            {
                restMode = false;

                foreach (var cannon in launchers)
                    cannon.Salvo(salvoSize);
            }

            private void OnTarget(bool val)
            {
                if (val)
                {
                    if (currentTargetDir != Vector3D.Zero)
                    {
                        FireCannons();
                        rotorControl.Lock(false);
                        return;
                    }
                    
                    if (rotorControl.currentAccuracy > 0.9999)
                    {
                        rotorControl.Lock(true);
                        restMode = true;
                    }
                        
                } else if (currentTargetDir != Vector3D.Zero)
                {
                    rotorControl.Lock(false);
                }
            }

            private List<IMyMotorStator> Select(List<IMyMotorStator> elevation)
            {
                var tmplist = new List<IMyMotorStator>();

                for(int i = 0; i < elevation.Count; i++)
                {
                    var tmpRot = RotorLauncher.Selector(elevation[i].TopGrid);
                    if (tmpRot != null)
                        tmplist.Add(tmpRot);
                    else
                        elevation.RemoveAt(i);
                }
                return tmplist;
            }
        }
    }
}
