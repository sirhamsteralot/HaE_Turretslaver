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
        public class RotorControl
        {
            public RotorReferencePair azimuth;
            public List<RotorReferencePair> elevationRotors;

            public RotorControl(RotorReferencePair azimuth, List<RotorReferencePair> elevationRotors)
            {
                this.azimuth = azimuth;
                this.elevationRotors = elevationRotors;
            }

            public void AimAtTarget(Vector3D desiredDirection)
            {
                RotorUtils.PointRotorAtVector(azimuth.rotor, desiredDirection, azimuth.reference.WorldMatrix.Up);

                foreach(var elevation in elevationRotors)
                {
                    RotorUtils.PointRotorAtVector(elevation.rotor, -desiredDirection, elevation.reference.WorldMatrix.Up);
                }
            }

            public struct RotorReferencePair
            {
                public IMyMotorStator rotor;
                public IMyTerminalBlock reference;
            }
        }
    }
}
