using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Io;
using System;
using System.Collections.Generic;
using System.IO;
using static MBT.DalamudAPI;

namespace MBT
{
    internal class NavMeshDetour
    {
        const int MAX_POLYS = 256;
        private readonly RcVec3f m_polyPickExt = new RcVec3f(2, 4, 2);
        private readonly DtQueryDefaultFilter m_filter;

        public NavMeshDetour()
        {
            m_filter = new DtQueryDefaultFilter(
                0xffff,
                0x10,
                [1f, 1f, 1f, 1f, 2f, 1.5f]
            );
        }
        public DtNavMesh? LoadNavMesh(FileStream file)
        {
            try
            {
                DtNavMesh? mesh = null;
                using var br = new BinaryReader(file);
                DtMeshSetReader reader = new DtMeshSetReader();
                mesh = reader.Read(br, 6);
                return mesh;
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "");
                return null;
            }
        }
        public RcVec3f FindNearestPolyPoint(RcVec3f startPos, RcVec3f halfExtents, FileStream file)
        {
            var navQuery = new DtNavMeshQuery(LoadNavMesh(file));
            navQuery.FindNearestPoly(startPos, halfExtents, m_filter, out var refdfd, out var polyPoint, out var _);
            DalamudAPI.PluginLog.Info(refdfd.ToString());
            return polyPoint;
        }
        public List<DtStraightPath> QueryPath(RcVec3f startPos, RcVec3f endPos, FileStream file)
        {
            var navQuery = new DtNavMeshQuery(LoadNavMesh(file));
            navQuery.FindNearestPoly(startPos, m_polyPickExt, m_filter, out long startNearestPoly, out var _, out var _);
            navQuery.FindNearestPoly(endPos, m_polyPickExt, m_filter, out long endNearestPoly, out var _, out var _);

            var sPathPolys = new List<long>();
            var sPath = new List<DtStraightPath>();

            FindStraightPath(navQuery, startNearestPoly, endNearestPoly, startPos, endPos, m_filter, true, ref sPathPolys, ref sPath, 0);

            return sPath;

        }
        public DtStatus FindStraightPath(DtNavMeshQuery navQuery, long startRef, long endRef, RcVec3f startPt, RcVec3f endPt, IDtQueryFilter filter, bool enableRaycast,
            ref List<long> polys, ref List<DtStraightPath> straightPath, int straightPathOptions)
        {
            if (startRef == 0 || endRef == 0)
            {
                return DtStatus.DT_FAILURE;
            }

            polys ??= new List<long>();
            straightPath ??= new List<DtStraightPath>();

            polys.Clear();
            straightPath.Clear();

            var opt = new DtFindPathOption(enableRaycast ? DtFindPathOptions.DT_FINDPATH_ANY_ANGLE : 0, float.MaxValue);
            navQuery.FindPath(startRef, endRef, startPt, endPt, filter, ref polys, opt);

            if (0 >= polys.Count)
                return DtStatus.DT_FAILURE;

            // In case of partial path, make sure the end point is clamped to the last polygon.
            var epos = new RcVec3f(endPt.X, endPt.Y, endPt.Z);
            if (polys[polys.Count - 1] != endRef)
            {
                var result = navQuery.ClosestPointOnPoly(polys[polys.Count - 1], endPt, out var closest, out var _);
                if (result.Succeeded())
                {
                    epos = closest;
                }
            }

            navQuery.FindStraightPath(startPt, epos, polys, ref straightPath, MAX_POLYS, straightPathOptions);

            return DtStatus.DT_SUCCESS;
        }
    }
}
