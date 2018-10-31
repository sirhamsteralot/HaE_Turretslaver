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
        public class DeadzoneProvider
        {
            private Dictionary<long, GridDeadzone> grids = new Dictionary<long, GridDeadzone>();
            public bool Enabled = true;


            public DeadzoneProvider(GridTerminalSystemUtils gtsUtils)
            {
                var subgridEnablers = new List<IMyMechanicalConnectionBlock>();
                gtsUtils.GridTerminalSystem.GetBlocksOfType(subgridEnablers);

                var myCubeGrid = gtsUtils.Me.CubeGrid;
                grids[myCubeGrid.EntityId] = new GridDeadzone(myCubeGrid);
                foreach (var subgridEnabler in subgridEnablers)
                {
                    var topGrid = subgridEnabler.TopGrid;
                    if (topGrid == null)
                        continue;
                    if (!grids.ContainsKey(topGrid.EntityId))
                        grids[topGrid.EntityId] = new GridDeadzone(topGrid);
                }
            }

            public bool IsBlockInTheWay(Vector3D origin, Vector3D targetPos)
            {
                if (!Enabled)
                    return false;

                Vector3D targetDir = targetPos - origin;
                RayD line = new RayD(origin, targetDir);

                foreach (var grid in grids.Values)
                {
                    double? result;
                    if (grid.grid.WorldVolume.Contains(origin) != ContainmentType.Contains)
                    {
                        result = grid.grid.WorldVolume.Intersects(line);
                        if (!result.HasValue)
                            continue;

                        targetDir.Normalize();

                        if (grid.IsBlockInTheWay(origin + targetDir * result.Value, targetPos))
                            return true;
                    }

                    if (grid.IsBlockInTheWay(origin, targetPos))
                    {
                        return true;
                    }
                        
                }

                return false;
            }

            public class GridDeadzone
            {
                public IMyCubeGrid grid;
                private Dictionary<Vector3I, BlockDeadzone> blocksInTheWay = new Dictionary<Vector3I, BlockDeadzone>();
                private CachedFunction<CalculationValues, bool> cachedBlockSearch;
                private CachedFunction<Vector3I, bool> cubeExists;

                public GridDeadzone(IMyCubeGrid grid)
                {
                    this.grid = grid;
                    cachedBlockSearch = new CachedFunction<CalculationValues, bool>(IsBlockInTheWay);
                    cubeExists = new CachedFunction<Vector3I, bool>(grid.CubeExists);
                }

                public bool IsBlockInTheWay(Vector3D origin, Vector3D targetPos)
                {
                    Vector3I relativePos = grid.WorldToGridInteger(origin);
                    
                    Vector3I relativeTargetPos = grid.WorldToGridInteger(targetPos);

                    Vector3I direction = (Vector3I)(relativeTargetPos - relativePos);

                    CalculationValues calcPackage = new CalculationValues
                    {
                        relativePosRounded = relativePos,
                        direction = direction
                    };

                    return cachedBlockSearch.Execute(calcPackage);
                }

                public bool IsBlockInTheWay(CalculationValues calcValues)
                {
                    BoundingBox box = new BoundingBox(grid.Min, grid.Max);
                    var Ray = new Ray(calcValues.relativePosRounded, calcValues.direction);
                    float? intersects = box.Intersects(Ray);
                    if (!intersects.HasValue)
                        return false;

                    Vector3D searchDir = Vector3D.Normalize(calcValues.direction);
                    Vector3I startPos = (Vector3I)((Vector3D)calcValues.relativePosRounded + ((intersects.Value + 1) * searchDir));

                    for (Vector3D searchPos = startPos;
                        (searchPos.X <= grid.Max.X && searchPos.X >= grid.Min.X) &&
                        (searchPos.Y <= grid.Max.Y && searchPos.Y >= grid.Min.Y) &&
                        (searchPos.Z <= grid.Max.Z && searchPos.Z >= grid.Min.Z);
                        searchPos = (searchPos + searchDir))
                    {
                        if (cubeExists.Execute((Vector3I)searchPos))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                public struct CalculationValues
                {
                    public Vector3I relativePosRounded;
                    public Vector3I direction;

                    public override int GetHashCode()
                    {
                        return relativePosRounded.GetHashCode() ^ direction.GetHashCode();
                    }

                    public override bool Equals(object obj)
                    {
                        if (obj == null)
                            return false;

                        var values = (CalculationValues)obj;
                        return values.direction.Equals(direction) && values.relativePosRounded.Equals(relativePosRounded);
                    }
                }

                public class BlockDeadzone
                {
                    public IMyCubeGrid parent;

                    public Vector3I position;
                    public Vector3D worldRelativePosition => parent.GridIntegerToWorld(position);
                    public Vector3D volume;

                    public BlockDeadzone(IMyCubeGrid parent, Vector3I position)
                    {
                        this.parent = parent;
                        this.position = position;

                        volume = new Vector3D(parent.GridSize);
                    }
                }
            }
        }
    }
}
