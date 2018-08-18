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
        EntityTracking_Module targetTracker;
        GridCannonTargeting gridCannonTargeting;


        IMyShipController control;


        public Program() {
            P = this;

            var GTSUtils = new GridTerminalSystemUtils(Me, GridTerminalSystem);

            IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName("Test");
            control = GridTerminalSystem.GetBlockWithName("RC") as IMyShipController;

            turretGroup = new TurretGroup(group, "[Azimuth]", "[Elevation]");
            targetTracker = new EntityTracking_Module(GTSUtils, control, null);
            targetTracker.onEntityDetected += OnEntityDetected;
            gridCannonTargeting = new GridCannonTargeting(control, 100, true);
            gridCannonTargeting.onRoutineFinish += OnTargetSolved;
            gridCannonTargeting.onRoutineFail += OnTargetingFail;

            Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10;
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Update10) != 0)
                targetTracker.Poll();

            gridCannonTargeting.Tick();
            
            turretGroup.Tick();
        }

        public void OnEntityDetected(HaE_Entity entity)
        {
            //Echo($"Targeting {entity.entityInfo.EntityId}");
            gridCannonTargeting.NewTarget(entity.entityInfo);
        }

        public void OnTargetSolved(Vector3D targetPos)
        {
            Echo($"targeting position: {targetPos}");
            turretGroup.TargetPosition(targetPos);
        }

        public void OnTargetingFail()
        {
            turretGroup.TargetDirection(Vector3D.Zero);
        }
    }
}