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

            public bool IsBlockInTheWay(Vector3D origin, Vector3D targetPos, IMyCubeBlock constructBlock = null)
            {
                if (!Enabled)
                    return false;



                Vector3D targetDir = targetPos - origin;
                RayD line = new RayD(origin, targetDir);

                foreach (var grid in grids.Values)
                {
                    if (constructBlock != null)
                        if (!constructBlock.CubeGrid.IsSameConstructAs(grid.grid))
                            continue;

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

                    int x1 = startPos.X;
                    int y1 = startPos.Y;
                    int z1 = startPos.Z;

                    int x2 = startPos.X + calcValues.direction.X;
                    int y2 = startPos.Y + calcValues.direction.Y;
                    int z2 = startPos.Z + calcValues.direction.Z;

                    // Bresenham algorythm, implementation borrowed from https://gist.github.com/yamamushi/5823518
                    int i, dx, dy, dz, l, m, n, x_inc, y_inc, z_inc, err_1, err_2, dx2, dy2, dz2;
                    int[] point = new int[3];

                    point[0] = x1;
                    point[1] = y1;
                    point[2] = z1;
                    dx = x2 - x1;
                    dy = y2 - y1;
                    dz = z2 - z1;
                    x_inc = (dx < 0) ? -1 : 1;
                    l = Math.Abs(dx);
                    y_inc = (dy < 0) ? -1 : 1;
                    m = Math.Abs(dy);
                    z_inc = (dz < 0) ? -1 : 1;
                    n = Math.Abs(dz);
                    dx2 = l << 1;
                    dy2 = m << 1;
                    dz2 = n << 1;

                    if ((l >= m) && (l >= n))
                    {
                        err_1 = dy2 - l;
                        err_2 = dz2 - l;
                        for (i = 0; i < l; i++)
                        {

                            if (!((point[0] <= grid.Max.X && point[0] >= grid.Min.X) &&
                                (point[1] <= grid.Max.Y && point[1] >= grid.Min.Y) &&
                                (point[2] <= grid.Max.Z && point[2] >= grid.Min.Z)))
                            {
                                return false;
                            }

                            if (cubeExists.Execute(new Vector3I(point[0], point[1], point[2])))
                            {
                                return true;
                            }

                            if (err_1 > 0)
                            {
                                point[1] += y_inc;
                                err_1 -= dx2;
                            }
                            if (err_2 > 0)
                            {
                                point[2] += z_inc;
                                err_2 -= dx2;
                            }
                            err_1 += dy2;
                            err_2 += dz2;
                            point[0] += x_inc;
                        }
                    }
                    else if ((m >= l) && (m >= n))
                    {
                        err_1 = dx2 - m;
                        err_2 = dz2 - m;
                        for (i = 0; i < m; i++)
                        {
                            if (!((point[0] <= grid.Max.X && point[0] >= grid.Min.X) &&
                                (point[1] <= grid.Max.Y && point[1] >= grid.Min.Y) &&
                                (point[2] <= grid.Max.Z && point[2] >= grid.Min.Z)))
                            {
                                return false;
                            }

                            if (cubeExists.Execute(new Vector3I(point[0], point[1], point[2])))
                            {
                                return true;
                            }

                            if (err_1 > 0)
                            {
                                point[0] += x_inc;
                                err_1 -= dy2;
                            }
                            if (err_2 > 0)
                            {
                                point[2] += z_inc;
                                err_2 -= dy2;
                            }
                            err_1 += dx2;
                            err_2 += dz2;
                            point[1] += y_inc;
                        }
                    }
                    else
                    {
                        err_1 = dy2 - n;
                        err_2 = dx2 - n;
                        for (i = 0; i < n; i++)
                        {

                            if (!((point[0] <= grid.Max.X && point[0] >= grid.Min.X) &&
                                (point[1] <= grid.Max.Y && point[1] >= grid.Min.Y) &&
                                (point[2] <= grid.Max.Z && point[2] >= grid.Min.Z)))
                            {
                                return false;
                            }

                            if (cubeExists.Execute(new Vector3I(point[0], point[1], point[2])))
                            {
                                return true;
                            }

                            if (err_1 > 0)
                            {
                                point[1] += y_inc;
                                err_1 -= dz2;
                            }
                            if (err_2 > 0)
                            {
                                point[0] += x_inc;
                                err_2 -= dz2;
                            }
                            err_1 += dy2;
                            err_2 += dx2;
                            point[2] += z_inc;
                        }
                    }

                    if (cubeExists.Execute(new Vector3I(point[0], point[1], point[2])))
                    {
                        return true;
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
