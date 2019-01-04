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
        public double maxActiveRotorGunVel { get { return (double)nameSerializer.GetValue("maxActiveRotorGunVel"); } }

        public double maxGatlingBulletVel { get { return (double)nameSerializer.GetValue("maxGatlingBulletVel"); } }

        public bool enableAutoDeadzoning { get { return (bool)nameSerializer.GetValue("enableAutoDeadzoning"); } }
        #endregion

        DeadzoneProvider deadzoneProvider;

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

        bool IgnoreEventAfterOnce;

        public Program()
        {
            #region serializer
            nameSerializer = new INISerializer("Config");

            nameSerializer.AddValue("rotorTurretGroupTag", x => x, "[HaE RotorTurret]");
            nameSerializer.AddValue("gatlingTurretGroupTag", x => x, "[HaE GatlingTurret]");
            nameSerializer.AddValue("azimuthTag", x => x, "[Azimuth]");
            nameSerializer.AddValue("elevationTag", x => x, "[Elevation]");
            nameSerializer.AddValue("controllerName", x => x, "Controller");
            nameSerializer.AddValue("groupType", x => x, "Any");
            nameSerializer.AddValue("lcdStatusTag", x => x, "[GridcannonStatus]");
            nameSerializer.AddValue("maxProjectileVel", x => double.Parse(x), 100.0);
            nameSerializer.AddValue("maxActiveRotorGunVel", x => double.Parse(x), 30.0);
            nameSerializer.AddValue("maxGatlingBulletVel", x => double.Parse(x), 400.0);
            nameSerializer.AddValue("enableAutoDeadzoning", x => bool.Parse(x), true);

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

            GTSUtils = new GridTerminalSystemUtils(Me, GridTerminalSystem);
            mainScheduler = new Scheduler();
            ingameTime = new IngameTime();
            rotorTurretGroups = new List<RotorTurretGroup>();
            gatlingTurretGroups = new List<GatlingTurretGroup>();
            deadzoneProvider = new DeadzoneProvider(GTSUtils);
            deadzoneProvider.Enabled = enableAutoDeadzoning;

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
            foreach (var tracker in targetTracker.ObjectTrackers)
            {
                var turretTracker = tracker as TurretTracking;
                if (turretTracker != null)
                    turretTracker.earlyReturn = true;
            }
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
                        AddGatlingTurret(stator);
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
                        AddGatlingTurret(stator);
                        yield return true;
                    }
                    break;
            }

            statusWriter.UpdateCannonCount(rotorTurretGroups.Count, 0, 0);
            initialized = true;
        }


        double averageTotalInstructionCount;
        double averageCheckTurretsInstructionCount;
        double averageTargetTrackerInstructionCount;
        double averageGridTargetingInstructionCount;
        double averageTurretGroupInstructionCount;
        double averageRuntime;
        public void Main(string argument, UpdateType updateSource)
        {
            mainScheduler.Main();

            if (!initialized)
                return;

            IgnoreEventAfterOnce = false;

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
                if ((updateSource & UpdateType.Update100) != 0)
                    if (mainScheduler.QueueCount < 1)
                        mainScheduler.AddTask(CheckTurrets());
                tempCount = Runtime.CurrentInstructionCount - tempCount;
                averageCheckTurretsInstructionCount = tempCount * 0.01 + averageCheckTurretsInstructionCount * 0.99;

                tempCount = Runtime.CurrentInstructionCount;
                gridCannonTargeting.Tick();
                tempCount = Runtime.CurrentInstructionCount - tempCount;
                averageGridTargetingInstructionCount = tempCount * 0.01 + averageGridTargetingInstructionCount * 0.99;

                tempCount = Runtime.CurrentInstructionCount;
                TickTurrets();
                tempCount = Runtime.CurrentInstructionCount - tempCount;
                averageTurretGroupInstructionCount = tempCount * 0.01 + averageTurretGroupInstructionCount * 0.99;

                ingameTime.Tick(Runtime.TimeSinceLastRun);

                Echo($"turretCount: {rotorTurretGroups.Count + gatlingTurretGroups.Count}");

                averageTotalInstructionCount = Runtime.CurrentInstructionCount * 0.01 + averageTotalInstructionCount * 0.99;
                Echo($"\nComplexity:\nTotal: {averageTotalInstructionCount: #.##}\n" +
                    $"TargetTracker: {averageTargetTrackerInstructionCount: #.##}\n" +
                    $"CheckTurrets: {averageCheckTurretsInstructionCount: #.##}\n" +
                    $"GridTargeting: {averageGridTargetingInstructionCount: #.##}\n" +
                    $"TurretGroup: {averageTurretGroupInstructionCount: #.##}");

                averageRuntime = averageRuntime * 0.99 + Runtime.LastRunTimeMs * 0.01;
                Echo($"\nlastRuntime: {Runtime.LastRunTimeMs: #.##}\n" +
                     $"\averageRunTime: {averageRuntime: #.##}\n");
                return;
            }

            
            if ((updateSource & UpdateType.Update10) != 0)
                targetTracker.Poll();
            if ((updateSource & UpdateType.Update100) != 0)
                if (mainScheduler.QueueCount < 1)
                    mainScheduler.AddTask(CheckTurrets());


            gridCannonTargeting.Tick();
            TickTurrets();

            ingameTime.Tick(Runtime.TimeSinceLastRun);
            #pragma warning restore
        }

        public IEnumerator<bool> CheckTurrets()
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
                yield return true;
            }

            foreach (var cannon in gatlingTurretGroups)
            {
                TurretGroupUtils.TurretGroupStatus status = cannon.CheckGroupStatus();

                switch (status)
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
                yield return true;
            }

            statusWriter.UpdateCannonCount(normal, minor, major);
        }

        public void AddRotorTurret(IMyBlockGroup group)
        {
            RotorTurretGroup turretGroup;
            try
            {
                turretGroup = new RotorTurretGroup(group, ingameTime, deadzoneProvider, azimuthTag, elevationTag);
                turretGroup.TargetDirection(ref Vector3D.Zero);
                turretGroup.defaultDir = control.WorldMatrix.Forward;

                if (turretGroup.CheckGroupStatus() != TurretGroupUtils.TurretGroupStatus.MajorDMG)
                    rotorTurretGroups.Add(turretGroup);
            } catch
            {
                statusWriter.AddUninitializable(1);
            }
        }

        public void AddGatlingTurret(IMyBlockGroup group)
        {
            try
            {
                var turretGroup = new RotorTurretGroup(group, ingameTime, deadzoneProvider, azimuthTag, elevationTag);
                turretGroup.TargetDirection(ref Vector3D.Zero);
                turretGroup.defaultDir = control.WorldMatrix.Forward;

                if (turretGroup.CheckGroupStatus() != TurretGroupUtils.TurretGroupStatus.MajorDMG)
                    rotorTurretGroups.Add(turretGroup);
            }
            catch
            {
                statusWriter.AddUninitializable(1);
            }
        }

        List<IMyMotorStator> rotors = new List<IMyMotorStator>();
        List<IMyMotorStator> cache = new List<IMyMotorStator>();
        List<IMyMotorStator> prevTop = new List<IMyMotorStator>();
        List<IMyMotorStator> currentTop = new List<IMyMotorStator>();
        public void AddRotorTurret(IMyMotorStator sourceRotor)
        {
            try
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

                var turretGroup = new RotorTurretGroup(rotors, ingameTime, deadzoneProvider, azimuthTag, elevationTag);
                turretGroup.TargetDirection(ref Vector3D.Zero);
                turretGroup.defaultDir = control.WorldMatrix.Forward;

                if (turretGroup.CheckGroupStatus() != TurretGroupUtils.TurretGroupStatus.MajorDMG)
                    rotorTurretGroups.Add(turretGroup);
            }
            catch
            {
                statusWriter.AddUninitializable(1);
            }
        }

        public void AddGatlingTurret(IMyMotorStator sourceRotor)
        {
            try
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

                var turretGroup = new GatlingTurretGroup(rotors, ingameTime, deadzoneProvider, azimuthTag, elevationTag);
                turretGroup.TargetDirection(ref Vector3D.Zero);
                turretGroup.defaultDir = control.WorldMatrix.Forward;

                if (turretGroup.CheckGroupStatus() != TurretGroupUtils.TurretGroupStatus.MajorDMG)
                    gatlingTurretGroups.Add(turretGroup);
            }
            catch
            {
                statusWriter.AddUninitializable(1);
            }
        }

        public void TickTurrets()
        {
            foreach (RotorTurretGroup turret in rotorTurretGroups)
            {
                turret.Tick();
            }
            foreach (GatlingTurretGroup turret in gatlingTurretGroups)
            {
                turret.Tick();
            }  
        }

        
        public void OnEntityDetected(HaE_Entity entity)
        {
            if (Vector3D.DistanceSquared(entity.entityInfo.Position, control.GetPosition()) > 1000 * 1000)
                return;

            if (IgnoreEventAfterOnce)
                return;

            if (control.GetShipSpeed() < maxActiveRotorGunVel)
            {
                gridCannonTargeting.NewTarget(entity.entityInfo);
            } else
            {
                foreach (var cannon in rotorTurretGroups)
                {
                    cannon.TargetDirection(ref Vector3D.Zero);
                }
            }
            
            statusWriter.UpdateStatus(StatusWriter.TargetingStatus.Targeting);

            basicTargeting.UpdateValues(control.GetVelocityVector(), control.GetPosition(), maxGatlingBulletVel);
            var result = basicTargeting.CalculateTrajectory(entity.entityInfo);
            if (result.HasValue)
                TargetSolvedGatling(result.Value);

            IgnoreEventAfterOnce = true;
        }

        public void TargetSolvedGatling(Vector3D targetPos)
        {
            foreach(var gatlingTurret in gatlingTurretGroups)
            {
                gatlingTurret.TargetPosition(ref targetPos);
            }
        }

        public void OnTargetSolved(Vector3D targetPos)
        {
            foreach (RotorTurretGroup turretGroup in rotorTurretGroups)
            {
                turretGroup.TargetPosition(ref targetPos);
            }

            statusWriter.UpdateStatus(StatusWriter.TargetingStatus.Ontarget);
        }

        public void OnTargetingFail()
        {
            foreach (RotorTurretGroup turretGroup in rotorTurretGroups)
            {
                turretGroup.TargetDirection(ref Vector3D.Zero);

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
                turretGroup.TargetDirection(ref Vector3D.Zero);
                turretGroup.defaultDir = control.WorldMatrix.Forward;
                turretGroup.restAfterReset = true;
            }

            foreach (GatlingTurretGroup group in gatlingTurretGroups)
            {
                group.TargetDirection(ref Vector3D.Zero);
                group.defaultDir = control.WorldMatrix.Forward;
                group.restAfterReset = true;
            }

            statusWriter.UpdateStatus(StatusWriter.TargetingStatus.Idle);
        }
    }
}