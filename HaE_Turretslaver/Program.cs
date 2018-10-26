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
        public const bool DEBUG = false;

        #region iniSerializer
        INISerializer nameSerializer;

        public string rotorTurretGroupTag { get { return (string)nameSerializer.GetValue("rotorTurretGroupTag"); } }
        public string gatlingTurretGroupTag { get { return (string)nameSerializer.GetValue("gatlingTurretGroupTag"); } }
        public string azimuthTag { get { return (string)nameSerializer.GetValue("azimuthTag"); } }
        public string elevationTag { get { return (string)nameSerializer.GetValue("elevationTag"); } }
        public string controllerName { get { return (string)nameSerializer.GetValue("controllerName"); } }
        public string groupType { get { return (string)nameSerializer.GetValue("groupType"); } }
        public string lcdStatusTag { get { return (string)nameSerializer.GetValue("lcdStatusTag"); } }
        public double maxProjectileVel { get { return (double)nameSerializer.GetValue("maxProjectileVel"); } }
        public double maxGatlingBulletVel { get { return (double)nameSerializer.GetValue("maxGatlingBulletVel"); } }
        #endregion


        List<RotorTurretGroup> rotorTurretGroups;
        List<GatlingTurretGroup> gatlingTurretGroups;
        EntityTracking_Module targetTracker;
        GridCannonTargeting gridCannonTargeting;
        QuarticTargeting basicTargeting;
        StatusWriter statusWriter;

        IngameTime ingameTime;
        GridTerminalSystemUtils GTSUtils;

        Scheduler mainScheduler;

        IMyShipController control;


        public Program() {

            GTSUtils = new GridTerminalSystemUtils(Me, GridTerminalSystem);
            mainScheduler = new Scheduler();
            ingameTime = new IngameTime();
            rotorTurretGroups = new List<RotorTurretGroup>();
            gatlingTurretGroups = new List<GatlingTurretGroup>();

            #region serializer
            nameSerializer = new INISerializer("Config");

            nameSerializer.AddValue("rotorTurretGroupTag", x => x, "[HaE RotorTurret]");
            nameSerializer.AddValue("gatlingTurretGroupTag", x => x, "[HaE GatlingTurret]");
            nameSerializer.AddValue("azimuthTag", x => x, "[Azimuth]");
            nameSerializer.AddValue("elevationTag", x => x, "[Elevation]");
            nameSerializer.AddValue("controllerName", x => x, "Controller");
            nameSerializer.AddValue("groupType", x => x, "Any");
            nameSerializer.AddValue("lcdStatusTag", x => x, "[GridcannonStatus]");
            nameSerializer.AddValue("maxProjectileVel", x => double.Parse(x), 100);
            nameSerializer.AddValue("maxGatlingBulletVel", x => double.Parse(x), 400);

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

            mainScheduler.AddTask(Init());

            Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10 | UpdateFrequency.Update100;
        }

        public bool initialized = false;
        public IEnumerator<bool> Init()
        {
            control = GridTerminalSystem.GetBlockWithName(controllerName) as IMyShipController;
            if (control == null)
            {
                Echo($"no shipcontroller found with name {controllerName}!");
                yield return false;
            }

            List<IMyTextPanel> lcds = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(lcds, x => x.CustomName.Contains(lcdStatusTag));
            statusWriter = new StatusWriter(lcds);


            targetTracker = new EntityTracking_Module(GTSUtils, control, null);
            targetTracker.onEntityDetected += OnEntityDetected;

            gridCannonTargeting = new GridCannonTargeting(control, ingameTime, maxProjectileVel);
            gridCannonTargeting.onRoutineFinish += OnTargetSolved;
            gridCannonTargeting.onRoutineFail += OnTargetingFail;
            gridCannonTargeting.onTargetTimeout += OnTargetTimeout;

            basicTargeting = new QuarticTargeting(Vector3D.Zero, Vector3D.Zero, maxGatlingBulletVel);

            switch (groupType)
            {
                case "BlockGroup":
                    List<IMyBlockGroup> groups = new List<IMyBlockGroup>();
                    GridTerminalSystem.GetBlockGroups(groups, x => x.Name.Contains(rotorTurretGroupTag));
                    foreach (var group in groups)
                    {
                        AddRotorTurret(group);
                        yield return true;
                    }
                    groups.Clear();
                    GridTerminalSystem.GetBlockGroups(groups, x => x.Name.Contains(gatlingTurretGroupTag));
                    foreach (var group in groups)
                    {
                        AddGatlingTurret(group);
                        yield return true;
                    }
                    break;

                case "NameTag":
                    List<IMyMotorStator> rotors = new List<IMyMotorStator>();
                    GridTerminalSystem.GetBlocksOfType(rotors, x => x.CustomName.Contains(rotorTurretGroupTag));
                    foreach (var stator in rotors)
                    {
                        AddRotorTurret(stator);
                        yield return true;
                    }
                    rotors.Clear();
                    GridTerminalSystem.GetBlocksOfType(rotors, x => x.CustomName.Contains(gatlingTurretGroupTag));
                    foreach (var stator in rotors)
                    {
                        AddRotorTurret(stator);
                        yield return true;
                    }
                    break;

                case "Any":
                    List<IMyBlockGroup> groupsA = new List<IMyBlockGroup>();
                    GridTerminalSystem.GetBlockGroups(groupsA, x => x.Name.Contains(rotorTurretGroupTag));
                    foreach (var group in groupsA)
                    {
                        AddRotorTurret(group);
                        yield return true;
                    }
                    groupsA.Clear();
                    GridTerminalSystem.GetBlockGroups(groupsA, x => x.Name.Contains(gatlingTurretGroupTag));
                    foreach (var group in groupsA)
                    {
                        AddGatlingTurret(group);
                        yield return true;
                    }

                    List<IMyMotorStator> rotorsA = new List<IMyMotorStator>();
                    GridTerminalSystem.GetBlocksOfType(rotorsA, x => x.CustomName.Contains(rotorTurretGroupTag));
                    foreach (var stator in rotorsA)
                    {
                        AddRotorTurret(stator);
                        yield return true;
                    }
                    rotorsA.Clear();
                    GridTerminalSystem.GetBlocksOfType(rotorsA, x => x.CustomName.Contains(gatlingTurretGroupTag));
                    foreach (var stator in rotorsA)
                    {
                        AddRotorTurret(stator);
                        yield return true;
                    }
                    break;
            }

            statusWriter.UpdateCannonCount(rotorTurretGroups.Count, 0, 0);
            initialized = true;
        }


        double averageTotalInstructionCount;
        double averageTargetTrackerInstructionCount;
        double averageGridTargetingInstructionCount;
        double averageTurretGroupInstructionCount;
        public void Main(string argument, UpdateType updateSource)
        {
            mainScheduler.Main();

            if (!initialized)
                return;

            statusWriter.Main();

            #pragma warning disable CS0162
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

                Echo($"turretCount: {rotorTurretGroups.Count}");
                Echo($"target restMode:  {gridCannonTargeting.restMode}");

                averageTotalInstructionCount = Runtime.CurrentInstructionCount * 0.01 + averageTotalInstructionCount * 0.99;
                Echo($"\nComplexity:\nTotal: {averageTotalInstructionCount: #.##}\nTargetTracker: {averageTargetTrackerInstructionCount: #.##}\nGridTargeting: {averageGridTargetingInstructionCount: #.##}\nTurretGroup: {averageTurretGroupInstructionCount: #.##}");
                Echo($"\nlastRuntime: {Runtime.LastRunTimeMs: #.##}");
                return;
            }

            
            if ((updateSource & UpdateType.Update10) != 0)
                targetTracker.Poll();
            if ((updateSource & UpdateType.Update100) != 0)
                CheckTurrets();


            gridCannonTargeting.Tick();
            TickTurrets();

            ingameTime.Tick(Runtime.TimeSinceLastRun);
            #pragma warning restore
        }

        public void CheckTurrets()
        {
            int normal = 0;
            int minor = 0;
            int major = 0;

            foreach (var cannon in rotorTurretGroups)
            {
                TurretGroupUtils.TurretGroupStatus status = cannon.CheckGroupStatus();

                switch(status)
                {
                    case TurretGroupUtils.TurretGroupStatus.Active:
                        normal++;
                        break;
                    case TurretGroupUtils.TurretGroupStatus.MinorDMG:
                        minor++;
                        break;
                    case TurretGroupUtils.TurretGroupStatus.MajorDMG:
                        major++;
                        cannon.DisableTurret(true);
                        break;
                }
            }

            statusWriter.UpdateCannonCount(normal, minor, major);
        }

        public void AddRotorTurret(IMyBlockGroup group)
        {
            var turretGroup = new RotorTurretGroup(group, ingameTime, azimuthTag, elevationTag);
            turretGroup.TargetDirection(Vector3D.Zero);
            turretGroup.defaultDir = control.WorldMatrix.Forward;

            if (turretGroup.CheckGroupStatus() != TurretGroupUtils.TurretGroupStatus.MajorDMG)
                rotorTurretGroups.Add(turretGroup);
        }

        public void AddGatlingTurret(IMyBlockGroup group)
        {
            var turretGroup = new RotorTurretGroup(group, ingameTime, azimuthTag, elevationTag);
            turretGroup.TargetDirection(Vector3D.Zero);
            turretGroup.defaultDir = control.WorldMatrix.Forward;

            if (turretGroup.CheckGroupStatus() != TurretGroupUtils.TurretGroupStatus.MajorDMG)
                rotorTurretGroups.Add(turretGroup);
        }

        List<IMyMotorStator> rotors = new List<IMyMotorStator>();
        List<IMyMotorStator> cache = new List<IMyMotorStator>();
        List<IMyMotorStator> prevTop = new List<IMyMotorStator>();
        List<IMyMotorStator> currentTop = new List<IMyMotorStator>();
        public void AddRotorTurret(IMyMotorStator sourceRotor)
        {
            rotors.Clear();
            cache.Clear();
            prevTop.Clear();
            currentTop.Clear();

            rotors.Add(sourceRotor);
            prevTop.AddRange(rotors);

            while (prevTop.Count > 0)
            {
                foreach (var rotor in prevTop)
                {
                    cache.Clear();
                    rotor.TopGrid?.GetCubesOfType(GridTerminalSystem, cache);
                    currentTop.AddRange(cache);
                }

                rotors.AddRange(currentTop);

                prevTop.Clear();
                prevTop.AddRange(currentTop);
                currentTop.Clear();
            }

            var turretGroup = new RotorTurretGroup(rotors, ingameTime, azimuthTag, elevationTag);
            turretGroup.TargetDirection(Vector3D.Zero);
            turretGroup.defaultDir = control.WorldMatrix.Forward;

            if (turretGroup.CheckGroupStatus() != TurretGroupUtils.TurretGroupStatus.MajorDMG)
                rotorTurretGroups.Add(turretGroup);
        }

        public void AddGatlingTurret(IMyMotorStator sourceRotor)
        {
            rotors.Clear();
            cache.Clear();
            prevTop.Clear();
            currentTop.Clear();

            rotors.Add(sourceRotor);
            prevTop.AddRange(rotors);

            while (prevTop.Count > 0)
            {
                foreach (var rotor in prevTop)
                {
                    cache.Clear();
                    rotor.TopGrid?.GetCubesOfType(GridTerminalSystem, cache);
                    currentTop.AddRange(cache);
                }

                rotors.AddRange(currentTop);

                prevTop.Clear();
                prevTop.AddRange(currentTop);
                currentTop.Clear();
            }

            var turretGroup = new GatlingTurretGroup(rotors, ingameTime, azimuthTag, elevationTag);
            turretGroup.TargetDirection(Vector3D.Zero);
            turretGroup.defaultDir = control.WorldMatrix.Forward;

            if (turretGroup.CheckGroupStatus() != TurretGroupUtils.TurretGroupStatus.MajorDMG)
                gatlingTurretGroups.Add(turretGroup);
        }

        public void TickTurrets()
        {
            foreach (RotorTurretGroup turret in rotorTurretGroups)
                turret.Tick();

            foreach (GatlingTurretGroup turret in gatlingTurretGroups)
                turret.Tick();
        }

        public void OnEntityDetected(HaE_Entity entity)
        {
            gridCannonTargeting.NewTarget(entity.entityInfo);
            statusWriter.UpdateStatus(StatusWriter.TargetingStatus.Targeting);

            basicTargeting.UpdateValues(control.GetVelocityVector(), control.GetPosition());
            var result = basicTargeting.CalculateTrajectory(entity.entityInfo);
            if (result.HasValue)
                TargetSolvedGatling(result.Value);
        }

        public void TargetSolvedGatling(Vector3D targetPos)
        {
            foreach(var gatlingTurret in gatlingTurretGroups)
            {
                gatlingTurret.TargetPosition(targetPos);
            }
        }

        public void OnTargetSolved(Vector3D targetPos)
        {
            foreach (RotorTurretGroup turretGroup in rotorTurretGroups)
            {
                turretGroup.TargetPosition(targetPos);
            }

            statusWriter.UpdateStatus(StatusWriter.TargetingStatus.Ontarget);
        }

        public void OnTargetingFail()
        {
            foreach (RotorTurretGroup turretGroup in rotorTurretGroups)
            {
                turretGroup.TargetDirection(Vector3D.Zero);

                if (gridCannonTargeting.simTargeting != null)
                {
                    Vector3D currentSimDir = gridCannonTargeting.simTargeting.firingDirection;

                    turretGroup.defaultDir = currentSimDir;
                }
            }

            statusWriter.UpdateStatus(StatusWriter.TargetingStatus.Targeting);
        }

        public void OnTargetTimeout()
        {
            foreach (RotorTurretGroup turretGroup in rotorTurretGroups)
            {
                turretGroup.TargetDirection(Vector3D.Zero);
                turretGroup.defaultDir = control.WorldMatrix.Forward;
            }

            statusWriter.UpdateStatus(StatusWriter.TargetingStatus.Idle);
        }
    }
}