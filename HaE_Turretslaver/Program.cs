﻿using Sandbox.Game.EntityComponents;
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

        #region iniSerializer
        INISerializer nameSerializer;

        public string turretGroupTag { get { return (string)nameSerializer.GetValue("turretGroupTag"); } }
        public string azimuthTag { get { return (string)nameSerializer.GetValue("azimuthTag"); } }
        public string elevationTag { get { return (string)nameSerializer.GetValue("elevationTag"); } }
        public string controllerName { get { return (string)nameSerializer.GetValue("controllerName"); } }


        #endregion


        List<TurretGroup> turretGroups;
        EntityTracking_Module targetTracker;
        GridCannonTargeting gridCannonTargeting;

        IngameTime ingameTime;
        GridTerminalSystemUtils GTSUtils;

        IMyShipController control;


        public Program() {

            GTSUtils = new GridTerminalSystemUtils(Me, GridTerminalSystem);
            ingameTime = new IngameTime();
            turretGroups = new List<TurretGroup>();

            #region serializer
            nameSerializer = new INISerializer("NameConfig");

            nameSerializer.AddValue("turretGroupTag", x => x, "[HaE Turret]");
            nameSerializer.AddValue("azimuthTag", x => x, "[Azimuth]");
            nameSerializer.AddValue("elevationTag", x => x, "[Elevation]");
            nameSerializer.AddValue("controllerName", x => x, "Controller");

            if (Me.CustomData == "")
            {
                string temp = Me.CustomData;
                nameSerializer.FirstSerialization(ref temp);
                Me.CustomData = temp;
            }
            else
            {
                nameSerializer.DeSerialize(Me.CustomData);
            }
            #endregion
            
            control = GridTerminalSystem.GetBlockWithName(controllerName) as IMyShipController;
            Echo($"controllerName: |{controllerName}|\ncontrol: {control != null}\n\n");

            targetTracker = new EntityTracking_Module(GTSUtils, control, null);
            targetTracker.onEntityDetected += OnEntityDetected;

            gridCannonTargeting = new GridCannonTargeting(control, ingameTime, 100);
            gridCannonTargeting.onRoutineFinish += OnTargetSolved;
            gridCannonTargeting.onRoutineFail += OnTargetingFail;
            gridCannonTargeting.onTargetTimeout += OnTargetTimeout;

            List<IMyBlockGroup> groups = new List<IMyBlockGroup>();
            GridTerminalSystem.GetBlockGroups(groups, x => x.Name.Contains(turretGroupTag));
            foreach(var group in groups)
            {
                AddTurret(group);
            }


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
                TickTurrets();
                tempCount = Runtime.CurrentInstructionCount - tempCount;
                averageTurretGroupInstructionCount = tempCount * 0.01 + averageTurretGroupInstructionCount * 0.99;

                ingameTime.Tick(Runtime.TimeSinceLastRun);

                Echo($"turretCount: {turretGroups.Count}");
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
            TickTurrets();

            ingameTime.Tick(Runtime.TimeSinceLastRun);
            #pragma warning restore
        }

        public void AddTurret(IMyBlockGroup group)
        {
            var turretGroup = new TurretGroup(group, ingameTime, azimuthTag, elevationTag);
            turretGroup.TargetDirection(Vector3D.Zero);
            turretGroup.defaultDir = control.WorldMatrix.Forward;

            turretGroups.Add(turretGroup);
        }

        public void TickTurrets()
        {
            foreach (TurretGroup turret in turretGroups)
                turret.Tick();
        }

        public void OnEntityDetected(HaE_Entity entity)
        {
            gridCannonTargeting.NewTarget(entity.entityInfo);
        }

        public void OnTargetSolved(Vector3D targetPos)
        {
            foreach (TurretGroup turretGroup in turretGroups)
            {
                turretGroup.TargetPosition(targetPos);
            }
        }

        public void OnTargetingFail()
        {
            foreach (TurretGroup turretGroup in turretGroups)
            {
                turretGroup.TargetDirection(Vector3D.Zero);

                if (gridCannonTargeting.simTargeting != null)
                {
                    Vector3D currentSimDir = gridCannonTargeting.simTargeting.firingDirection;

                    turretGroup.defaultDir = currentSimDir;
                }
            }
        }

        public void OnTargetTimeout()
        {
            foreach (TurretGroup turretGroup in turretGroups)
            {
                turretGroup.TargetDirection(Vector3D.Zero);
                turretGroup.defaultDir = control.WorldMatrix.Forward;
            }
        }
    }
}