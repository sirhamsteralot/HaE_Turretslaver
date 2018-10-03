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

        #region iniSerializer
        INISerializer nameSerializer;

        public string turretGroupTag { get { return (string)nameSerializer.GetValue("turretGroupTag"); } }
        public string azimuthTag { get { return (string)nameSerializer.GetValue("azimuthTag"); } }
        public string elevationTag { get { return (string)nameSerializer.GetValue("elevationTag"); } }
        public string controllerName { get { return (string)nameSerializer.GetValue("controllerName"); } }
        public string groupType { get { return (string)nameSerializer.GetValue("groupType"); } }

        #endregion


        List<TurretGroup> turretGroups;
        EntityTracking_Module targetTracker;
        GridCannonTargeting gridCannonTargeting;

        IngameTime ingameTime;
        GridTerminalSystemUtils GTSUtils;

        Scheduler mainScheduler;

        IMyShipController control;


        public Program() {

            GTSUtils = new GridTerminalSystemUtils(Me, GridTerminalSystem);
            mainScheduler = new Scheduler();
            ingameTime = new IngameTime();
            turretGroups = new List<TurretGroup>();

            #region serializer
            nameSerializer = new INISerializer("Config");

            nameSerializer.AddValue("turretGroupTag", x => x, "[HaE Turret]");
            nameSerializer.AddValue("azimuthTag", x => x, "[Azimuth]");
            nameSerializer.AddValue("elevationTag", x => x, "[Elevation]");
            nameSerializer.AddValue("controllerName", x => x, "Controller");
            nameSerializer.AddValue("groupType", x => x, "BlockGroup");

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

            Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10;
        }

        public bool initialized = false;
        public IEnumerator<bool> Init()
        {
            control = GridTerminalSystem.GetBlockWithName(controllerName) as IMyShipController;

            targetTracker = new EntityTracking_Module(GTSUtils, control, null);
            targetTracker.onEntityDetected += OnEntityDetected;

            gridCannonTargeting = new GridCannonTargeting(control, ingameTime, 100);
            gridCannonTargeting.onRoutineFinish += OnTargetSolved;
            gridCannonTargeting.onRoutineFail += OnTargetingFail;
            gridCannonTargeting.onTargetTimeout += OnTargetTimeout;

            switch (groupType)
            {
                case "BlockGroup":
                    List<IMyBlockGroup> groups = new List<IMyBlockGroup>();
                    GridTerminalSystem.GetBlockGroups(groups, x => x.Name.Contains(turretGroupTag));
                    foreach (var group in groups)
                    {
                        AddTurret(group);
                        yield return true;
                    }
                    break;

                case "NameTag":
                    List<IMyMotorStator> rotors = new List<IMyMotorStator>();
                    GridTerminalSystem.GetBlocksOfType(rotors, x => x.CustomName.Contains(turretGroupTag));
                    foreach (var stator in rotors)
                    {
                        AddTurret(stator);
                        yield return true;
                    }
                    break;
            }


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

        List<IMyMotorStator> rotors = new List<IMyMotorStator>();
        List<IMyMotorStator> cache = new List<IMyMotorStator>();
        List<IMyMotorStator> prevTop = new List<IMyMotorStator>();
        List<IMyMotorStator> currentTop = new List<IMyMotorStator>();
        public void AddTurret(IMyMotorStator sourceRotor)
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
                    rotor.TopGrid.GetCubesOfType(GridTerminalSystem, cache);
                    currentTop.AddRange(cache);
                }

                rotors.AddRange(currentTop);

                prevTop.Clear();
                prevTop.AddRange(currentTop);
            }

            var turretGroup = new TurretGroup(rotors, ingameTime, azimuthTag, elevationTag);
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