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
            public Action<bool> onTarget;

            public RotorReferencePair azimuth;
            public List<RotorReferencePair> elevationRotors;

            public RotorControl(RotorReferencePair azimuth, List<RotorReferencePair> elevationRotors)
            {
                this.azimuth = azimuth;
                this.elevationRotors = elevationRotors;
            }

            public void AimAtTarget(Vector3D desiredDirection)
            {
                Vector3D refDirAz = azimuth.reference.WorldMatrix.Up;
                RotorUtils.PointRotorAtVector(azimuth.rotor, desiredDirection, refDirAz);

                foreach (var elevation in elevationRotors)
                {
                    var refDirEl = elevation.reference.WorldMatrix.Up;
                    RotorUtils.PointRotorAtVector(elevation.rotor, -desiredDirection, refDirEl);

                    CheckSetTarget(desiredDirection, refDirEl, elevation.rotor);
                }
            }

            private void CheckSetTarget(Vector3D desiredDir, Vector3D currentDir, IMyMotorStator rotor)
            {
                double dot = Vector3D.Dot(currentDir, desiredDir);
                if (dot > 0.999)
                {
                    onTarget?.Invoke(true);
                }
                else
                {
                    onTarget?.Invoke(false);
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
