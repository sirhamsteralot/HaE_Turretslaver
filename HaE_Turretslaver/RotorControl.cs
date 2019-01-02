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
            public Action<bool, RotorReferencePair> onTarget;
            public double currentAccuracy = double.MaxValue;

            public RotorReferencePair azimuth;
            public List<RotorReferencePair> elevationRotors;

            public bool useForward = false;

            public RotorControl(RotorReferencePair azimuth, List<RotorReferencePair> elevationRotors)
            {
                this.azimuth = azimuth;
                this.elevationRotors = elevationRotors;
            }

            public void AimAtTarget(ref Vector3D desiredDirection, double azimuthMultiplier = -1, double elevationMultiplier = -1)
            {
                Vector3D refDirAz = !useForward ? azimuth.reference.WorldMatrix.Up : azimuth.reference.WorldMatrix.Forward;
                RotorUtils.PointRotorAtVector(azimuth.rotor, azimuthMultiplier * desiredDirection, refDirAz, 2);

                foreach (var elevation in elevationRotors)
                {
                    var refDirEl = !useForward ? elevation.reference.WorldMatrix.Up : elevation.reference.WorldMatrix.Forward;
                    RotorUtils.PointRotorAtVector(elevation.rotor, elevationMultiplier * desiredDirection, refDirEl, 2);

                    CheckSetTarget(ref desiredDirection, ref refDirEl, elevation);
                }
            }

            public void Lock(bool val)
            {
                if (azimuth.rotor.RotorLock != val)
                    azimuth.rotor.RotorLock = val;

                foreach (var elevation in elevationRotors)
                {
                    if (elevation.rotor.RotorLock != val)
                        elevation.rotor.RotorLock = val;
                }
            }

            int delayTicks;
            private void CheckSetTarget(ref Vector3D desiredDir, ref Vector3D currentDir, RotorReferencePair rotor)
            {
                if (delayTicks++ > 100)
                {
                    delayTicks = 0;
                    return;
                }

                currentAccuracy = Vector3D.Dot(currentDir, desiredDir);
                if (currentAccuracy > 0.995)
                {
                    onTarget?.Invoke(true, rotor);
                }
                else
                {
                    onTarget?.Invoke(false, rotor);
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
