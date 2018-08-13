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
    partial class Program : MyGridProgram
    {
        public static Program P;

        TurretGroup turretGroup;
        public Program() {
            P = this;

            IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName("Test");

            turretGroup = new TurretGroup(group, "[Azimuth]", "[Elevation]");
            turretGroup.SetTarget(Vector3D.Normalize(Vector3D.Zero - Me.GetPosition()));

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            turretGroup.Tick();
        }
    }
}