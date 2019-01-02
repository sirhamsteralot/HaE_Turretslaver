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
        public interface ITurretGroup
        {
            void Tick();
            void DisableTurret(bool value);
            TurretGroupUtils.TurretGroupStatus CheckGroupStatus();
            void TargetDirection(ref Vector3D targetDirection);
            void TargetPosition(ref Vector3D position);

        }
    }
}
