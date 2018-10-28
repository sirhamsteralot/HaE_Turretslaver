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
                foreach (var grid in grids.Values)
                {
                    RayD line = new RayD(origin, targetPos);
                    double? result = grid.grid.WorldVolume.Intersects(line);
                    if (!result.HasValue)
                        continue;

                    Vector3D targetDir = targetPos - origin;
                    targetDir.Normalize();


                    if (grid.IsBlockInTheWay(origin + targetDir * result.Value, targetPos))
                        return true;
                }

                return false;
            }

            public class GridDeadzone
            {
                public IMyCubeGrid grid;
                private Dictionary<Vector3I, BlockDeadzone> blocksInTheWay = new Dictionary<Vector3I, BlockDeadzone>();
                private CachedFunction<CalculationValues, bool> cachedBlockSearch;

                public GridDeadzone(IMyCubeGrid grid)
                {
                    this.grid = grid;
                    cachedBlockSearch = new CachedFunction<CalculationValues, bool>(IsBlockInTheWay);
                }

                public bool IsBlockInTheWay(Vector3D origin, Vector3D targetPos)
                {
                    Vector3D relativePos = VectorUtils.TransformPosWorldToLocal(grid.WorldMatrix, origin);
                    Vector3I relativePosRounded = new Vector3I((int)Math.Round(relativePos.X), (int)Math.Round(relativePos.Y), (int)Math.Round(relativePos.Z));
                    Vector3D relativeTargetPos = VectorUtils.TransformPosWorldToLocal(grid.WorldMatrix, targetPos);

                    Vector3D direction = Vector3D.Normalize(relativeTargetPos - relativePos);
                    Vector3I directionInteger = new Vector3I((int)Math.Round(direction.X), (int)Math.Round(direction.Y), (int)Math.Round(direction.Z));

                    CalculationValues calcPackage = new CalculationValues
                    {
                        relativePosRounded = relativePosRounded,
                        directionInteger = directionInteger
                    };

                    return cachedBlockSearch.Execute(calcPackage);
                }

                public bool IsBlockInTheWay(CalculationValues calcValues)
                {
                    BoundingBox box = new BoundingBox(grid.Min, grid.Max);
                    var Ray = new Ray(calcValues.relativePosRounded, calcValues.directionInteger);
                    float? intersects = box.Intersects(Ray);
                    if (!intersects.HasValue)
                        return false;

                    Vector3I startPos = calcValues.relativePosRounded + calcValues.directionInteger * (int)Math.Round(intersects.Value);

                    for (Vector3I searchPos = startPos;
                        (searchPos.X <= grid.Max.X && searchPos.X >= grid.Min.X) &&
                        (searchPos.Y <= grid.Max.Y && searchPos.Y >= grid.Min.Y) &&
                        (searchPos.Z <= grid.Max.Z && searchPos.Z >= grid.Min.Z);
                        searchPos += calcValues.directionInteger)
                    {
                        if (grid.CubeExists(searchPos))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                public struct CalculationValues
                {
                    public Vector3I relativePosRounded;
                    public Vector3I directionInteger;

                    public override int GetHashCode()
                    {
                        return relativePosRounded.GetHashCode() ^ directionInteger.GetHashCode();
                    }

                    public override bool Equals(object obj)
                    {
                        if (obj == null)
                            return false;

                        var values = (CalculationValues)obj;
                        return values.directionInteger.Equals(directionInteger) && values.relativePosRounded.Equals(relativePosRounded);
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
