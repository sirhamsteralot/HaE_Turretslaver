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
        public const bool DEBUG = true;

        TurretGroup turretGroup;
        EntityTracking_Module targetTracker;
        GridCannonTargeting gridCannonTargeting;
        IngameTime ingameTime;


        IMyShipController control;


        public Program() {
            var GTSUtils = new GridTerminalSystemUtils(Me, GridTerminalSystem);
            ingameTime = new IngameTime();

            IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName("Test");
            control = GridTerminalSystem.GetBlockWithName("RC") as IMyShipController;

            turretGroup = new TurretGroup(group, ingameTime, "[Azimuth]", "[Elevation]");
            targetTracker = new EntityTracking_Module(GTSUtils, control, null);
            targetTracker.onEntityDetected += OnEntityDetected;
            gridCannonTargeting = new GridCannonTargeting(control, ingameTime, 100);
            gridCannonTargeting.onRoutineFinish += OnTargetSolved;
            gridCannonTargeting.onRoutineFail += OnTargetingFail;
            gridCannonTargeting.onTargetTimeout += OnTargetTimeout;

            turretGroup.TargetDirection(Vector3D.Zero);
            turretGroup.defaultDir = control.WorldMatrix.Forward;

            Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10;
        }


        double averageTotalInstructionCount;
        double averageTargetTrackerInstructionCount;
        double averageGridTargetingInstructionCount;
        double averageTurretGroupInstructionCount;
        public void Main(string argument, UpdateType updateSource)
        {
            if (DEBUG)
            {
                int tempCount = Runtime.CurrentInstructionCount;
                if ((updateSource & UpdateType.Update10) != 0)
                    targetTracker.Poll();
                tempCount = Runtime.CurrentInstructionCount - tempCount;
                averageTargetTrackerInstructionCount = tempCount * 0.01 + averageTargetTrackerInstructionCount * 0.99;

                tempCount = Runtime.CurrentInstructionCount;
                gridCannonTargeting.Tick();
                tempCount = Runtime.CurrentInstructionCount - tempCount;
                averageGridTargetingInstructionCount = tempCount * 0.01 + averageGridTargetingInstructionCount * 0.99;

                tempCount = Runtime.CurrentInstructionCount;
                turretGroup.Tick();
                tempCount = Runtime.CurrentInstructionCount - tempCount;
                averageTurretGroupInstructionCount = tempCount * 0.01 + averageTurretGroupInstructionCount * 0.99;

                ingameTime.Tick(Runtime.TimeSinceLastRun);

                Echo($"turret restMode:  {turretGroup.restMode}");
                Echo($"target restMode:  {gridCannonTargeting.restMode}");

                averageTotalInstructionCount = Runtime.CurrentInstructionCount * 0.01 + averageTotalInstructionCount * 0.99;
                Echo($"\nComplexity:\nTotal: {averageTotalInstructionCount: #.##}\nTargetTracker: {averageTargetTrackerInstructionCount: #.##}\nGridTargeting: {averageGridTargetingInstructionCount: #.##}\nTurretGroup: {averageTurretGroupInstructionCount: #.##}");
                Echo($"\nlastRuntime: {Runtime.LastRunTimeMs: #.##}");
                return;
            }

            #pragma warning disable
            if ((updateSource & UpdateType.Update10) != 0)
                targetTracker.Poll();

            gridCannonTargeting.Tick();
            turretGroup.Tick();

            ingameTime.Tick(Runtime.TimeSinceLastRun);
            #pragma warning restore
        }

        public void OnEntityDetected(HaE_Entity entity)
        {
            gridCannonTargeting.NewTarget(entity.entityInfo);
        }

        public void OnTargetSolved(Vector3D targetPos)
        {
            turretGroup.TargetPosition(targetPos);
        }

        public void OnTargetingFail()
        {
            turretGroup.TargetDirection(Vector3D.Zero);

            if (gridCannonTargeting.simTargeting != null)
            {
                Vector3D currentSimDir = gridCannonTargeting.simTargeting.firingDirection;

                turretGroup.defaultDir = currentSimDir;
            }

        }

        public void OnTargetTimeout()
        {
            turretGroup.TargetDirection(Vector3D.Zero);
            turretGroup.defaultDir = control.WorldMatrix.Forward;
        }
    }
}