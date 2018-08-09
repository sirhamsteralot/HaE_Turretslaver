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
        public class RotorTurret
        {
            public IMyTerminalBlock referenceDir;
            public IMyMotorStator azimuth;
            public List<IMyMotorStator> elevationRotors;


            public void AimAtTarget(Vector3D desiredDirection)
            {
                RotorUtils.PointRotorAtVector(azimuth, desiredDirection, referenceDir.WorldMatrix.Forward);

                foreach(var elevation in elevationRotors)
                {
                    RotorUtils.PointRotorAtVector(elevation, desiredDirection, referenceDir.WorldMatrix.Forward);
                }
            }
        }
    }
}
