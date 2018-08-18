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
            Vector3D currentTargetDir;
            Vector3D defaultDir;
            bool cannonsOnTarget;
            int salvoSize = 5;

            RotorControl rotorControl;
            List<RotorLauncher> launchers = new List<RotorLauncher>();

            public TurretGroup(IMyBlockGroup turretGroup, IngameTime ingameTime, string azimuthTag, string elevationTag)
            {
                var rotors = new List<IMyMotorStator>();
                turretGroup.GetBlocksOfType(rotors);

                List<IMyMotorStator> elevation = rotors.Where(x => x.CustomName.Contains(elevationTag)).ToList();
                IMyMotorStator azimuth = rotors.First(x => x.CustomName.Contains(azimuthTag));
                defaultDir = azimuth.WorldMatrix.Forward;

                List<IMyMotorStator> cannonBases = Select(elevation);

                List<RotorControl.RotorReferencePair> elevationPairs = new List<RotorControl.RotorReferencePair>();
                for(int i = 0; i < elevation.Count; i++)
                {
                    elevationPairs.Add(new RotorControl.RotorReferencePair { rotor = elevation[i], reference = cannonBases[i] });
                }

                RotorControl.RotorReferencePair azimuthPair = new RotorControl.RotorReferencePair { rotor = azimuth, reference = cannonBases[0]};

                rotorControl = new RotorControl(azimuthPair, elevationPairs);
                rotorControl.onTarget = OnTarget;

                foreach (var cannonbase in cannonBases)
                {
                    var launcher = new RotorLauncher(cannonbase, ingameTime, 1);
                    launchers.Add(launcher);
                }
            }

            public void Tick()
            {
                if (currentTargetDir == Vector3D.Zero)
                {
                    rotorControl.AimAtTarget(defaultDir);
                    return;
                }

                rotorControl.AimAtTarget(currentTargetDir);
                foreach (var gun in launchers)
                    gun.Tick();
            }

            public void TargetDirection(Vector3D targetDirection)
            {
                currentTargetDir = targetDirection;
            }

            public void TargetPosition(Vector3D position)
            {
                Vector3D targetdir = Vector3D.Normalize(position - rotorControl.azimuth.rotor.GetPosition());
                TargetDirection(targetdir);
            }

            public void FireCannons()
            {
                foreach (var cannon in launchers)
                    cannon.Salvo(salvoSize);
            }

            private void OnTarget(bool val)
            {
                if (val == cannonsOnTarget)
                    return;

                cannonsOnTarget = val;

                if (val)
                    FireCannons();
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
