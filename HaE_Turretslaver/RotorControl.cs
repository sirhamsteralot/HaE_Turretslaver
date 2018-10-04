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
            public double currentAccuracy = double.MaxValue;

            public RotorReferencePair azimuth;
            public List<RotorReferencePair> elevationRotors;

            public RotorControl(RotorReferencePair azimuth, List<RotorReferencePair> elevationRotors)
            {
                this.azimuth = azimuth;
                this.elevationRotors = elevationRotors;
            }

            public void AimAtTarget(Vector3D desiredDirection, double azimuthMultiplier = -1, double elevationMultiplier = -1)
            {
                Vector3D refDirAz = azimuth.reference.WorldMatrix.Up;
                RotorUtils.PointRotorAtVector(azimuth.rotor, azimuthMultiplier * desiredDirection, refDirAz, 2);

                foreach (var elevation in elevationRotors)
                {
                    var refDirEl = elevation.reference.WorldMatrix.Up;
                    RotorUtils.PointRotorAtVector(elevation.rotor, elevationMultiplier * desiredDirection, refDirEl, 2);

                    CheckSetTarget(desiredDirection, refDirEl, elevation.rotor);
                }
            }

            public void Lock(bool val)
            {
                azimuth.rotor.RotorLock = val;

                foreach (var elevation in elevationRotors)
                {
                    elevation.rotor.RotorLock = val;
                }
            }

            private void CheckSetTarget(Vector3D desiredDir, Vector3D currentDir, IMyMotorStator rotor)
            {
                currentAccuracy = Vector3D.Dot(currentDir, desiredDir);
                if (currentAccuracy > 0.999)
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
