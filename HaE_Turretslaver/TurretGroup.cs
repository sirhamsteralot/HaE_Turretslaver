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
            RotorControl rotorControl;
            List<RotorLauncher> launchers = new List<RotorLauncher>();

            public TurretGroup(IMyBlockGroup turretGroup, string azimuthTag, string elevationTag, string cannonBaseTag)
            {
                var rotors = new List<IMyMotorStator>();
                turretGroup.GetBlocksOfType(rotors);

                List<IMyMotorStator> elevation = rotors.Where(x => x.CustomName.Contains(elevationTag)).ToList();
                IMyMotorStator azimuth = rotors.First(x => x.CustomName.Contains(azimuthTag));

                List<IMyMotorStator> cannonBases = rotors.Where(x => x.CustomName.Contains(cannonBaseTag)).ToList();


                rotorControl = new RotorControl(cannonBases[0], azimuth, elevation);

                foreach (var cannonbase in cannonBases)
                {
                    var launcher = new RotorLauncher(cannonbase);
                    launchers.Add(launcher);
                }
            }

            public void SetTarget(Vector3D targetDirection)
            {

            }
        }
    }
}
