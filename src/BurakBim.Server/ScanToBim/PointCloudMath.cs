namespace BurakBim.Server.ScanToBim;

/// <summary>
/// Scan-to-BIM feature extraction (all units mm). Deterministic (fixed RNG seed) so the same scan
/// always gives the same model - important for inspectors re-checking results.
/// </summary>
public static class PointCloudMath
{
    public readonly record struct P3(double X, double Y, double Z);
    public readonly record struct P2(double X, double Y);

    public sealed record Plane(double Z, int Points, string Role);
    public sealed record Segment(P2 A, P2 B, int Support, double Rms)
    {
        public double Length => Dist(A, B);
        public double Angle => Math.Atan2(B.Y - A.Y, B.X - A.X);
    }
    public sealed record WallCandidate(P2 Start, P2 End, double ThicknessMm, string Kind, double Confidence, int Support)
    {
        public double Length => Dist(Start, End);
    }
    public sealed record ColumnCandidate(P2 Center, double SizeX, double SizeY, string Shape, int Support);

    public static double Dist(P2 a, P2 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    public static List<P3> FromFlat(IList<object> flat)
    {
        var r = new List<P3>(flat.Count / 3);
        for (int i = 0; i + 2 < flat.Count; i += 3)
            r.Add(new P3(Shared.Json.ToDouble(flat[i]), Shared.Json.ToDouble(flat[i + 1]), Shared.Json.ToDouble(flat[i + 2])));
        return r;
    }

    // ------------------------------------------------------------------ levels
    /// <summary>
    /// Horizontal surfaces from the Z histogram. Slabs show up as a ceiling plane (underside)
    /// followed by a floor plane (top) about one slab thickness higher.
    /// </summary>
    public static (List<Plane> planes, List<double> levels) DetectLevels(List<P3> pts, double binMm = 20, double minShare = 0.015,
        double minStoreyMm = 2200, double maxSlabMm = 600)
    {
        if (pts.Count < 100) return (new(), new());
        double zmin = pts.Min(p => p.Z), zmax = pts.Max(p => p.Z);
        int bins = Math.Max(1, (int)Math.Ceiling((zmax - zmin) / binMm) + 1);
        var hist = new int[bins];
        foreach (var p in pts) hist[(int)((p.Z - zmin) / binMm)]++;
        int threshold = Math.Max(30, (int)(pts.Count * minShare));
        var peaks = new List<(double z, int n)>();
        for (int i = 0; i < bins; i++)
        {
            int left = i > 0 ? hist[i - 1] : 0, right = i < bins - 1 ? hist[i + 1] : 0;
            if (hist[i] >= threshold && hist[i] >= left && hist[i] >= right)
            {
                // weighted centre of the peak (3 bins)
                double w = hist[i] + left + right;
                double z = zmin + binMm * (i + 0.5) + binMm * (right - left) / Math.Max(1, w);
                if (peaks.Count > 0 && z - peaks[^1].z < binMm * 3) { if (hist[i] > peaks[^1].n) peaks[^1] = (z, hist[i]); }
                else peaks.Add((z, hist[i]));
            }
        }
        // classify: lowest = floor; a plane followed by another within maxSlab = ceiling(underside) + floor(top)
        var planes = new List<Plane>();
        var levels = new List<double>();
        for (int i = 0; i < peaks.Count; i++)
        {
            var (z, n) = peaks[i];
            string role;
            if (i == 0) role = "floor";
            else if (i + 1 < peaks.Count && peaks[i + 1].z - z <= maxSlabMm) role = "ceiling/slab underside";
            else if (i > 0 && z - peaks[i - 1].z <= maxSlabMm) role = "floor";
            else if (i == peaks.Count - 1) role = "roof/ceiling";
            else role = "floor";
            planes.Add(new Plane(Math.Round(z), n, role));
            if (role == "floor" && (levels.Count == 0 || z - levels[^1] >= minStoreyMm)) levels.Add(Math.Round(z));
        }
        return (planes, levels);
    }

    // ------------------------------------------------------------------ walls
    public static List<P2> Slice(List<P3> pts, double zFrom, double zTo, double gridMm = 10)
    {
        var seen = new HashSet<(long, long)>();
        var r = new List<P2>();
        foreach (var p in pts)
        {
            if (p.Z < zFrom || p.Z > zTo) continue;
            var key = ((long)Math.Floor(p.X / gridMm), (long)Math.Floor(p.Y / gridMm));
            if (seen.Add(key)) r.Add(new P2(p.X, p.Y));
        }
        return r;
    }

    /// <summary>Sequential RANSAC line extraction, split into continuous segments.</summary>
    public static List<Segment> ExtractSegments(List<P2> pts, double tolMm = 15, int minSupport = 60, double minLengthMm = 400,
        double maxGapMm = 250, int trials = 400, int maxLines = 150, int seed = 7)
    {
        var rng = new Random(seed);
        var remaining = new List<P2>(pts);
        var segments = new List<Segment>();
        int failures = 0;
        while (remaining.Count >= minSupport && segments.Count < maxLines && failures < 8)
        {
            // score hypotheses on a random subset (fast), then take full inliers of the winner
            var sample = remaining.Count <= 2500 ? remaining : Enumerable.Range(0, 2500).Select(_ => remaining[rng.Next(remaining.Count)]).ToList();
            int bestScore = -1;
            P2 bestA = default, bestB = default;
            for (int t = 0; t < trials; t++)
            {
                var a = remaining[rng.Next(remaining.Count)];
                var b = remaining[rng.Next(remaining.Count)];
                var len = Dist(a, b);
                if (len < 100) continue;
                double nx = -(b.Y - a.Y) / len, ny = (b.X - a.X) / len;
                int score = 0;
                foreach (var q in sample)
                    if (Math.Abs((q.X - a.X) * nx + (q.Y - a.Y) * ny) <= tolMm) score++;
                if (score > bestScore) { bestScore = score; bestA = a; bestB = b; }
            }
            List<int>? best = null;
            if (bestScore > 0)
            {
                var blen = Dist(bestA, bestB);
                double bnx = -(bestB.Y - bestA.Y) / blen, bny = (bestB.X - bestA.X) / blen;
                best = new List<int>();
                for (int i = 0; i < remaining.Count; i++)
                {
                    var q = remaining[i];
                    if (Math.Abs((q.X - bestA.X) * bnx + (q.Y - bestA.Y) * bny) <= tolMm) best.Add(i);
                }
            }
            if (best == null || best.Count < minSupport) { failures++; continue; }

            // least-squares refit (PCA) on inliers
            var inPts = best.Select(i => remaining[i]).ToList();
            var (c, dir) = FitLine(inPts);
            // project, sort and split by gaps
            var proj = inPts.Select(q => (t: (q.X - c.X) * dir.X + (q.Y - c.Y) * dir.Y, q)).OrderBy(x => x.t).ToList();
            int start = 0;
            bool any = false;
            for (int i = 1; i <= proj.Count; i++)
            {
                if (i == proj.Count || proj[i].t - proj[i - 1].t > maxGapMm)
                {
                    int n = i - start;
                    double t0 = proj[start].t, t1 = proj[i - 1].t;
                    if (n >= minSupport / 2 && t1 - t0 >= minLengthMm)
                    {
                        var part = proj.Skip(start).Take(n).Select(x => x.q).ToList();
                        var (pc, pd) = FitLine(part);
                        double a0 = part.Min(q => (q.X - pc.X) * pd.X + (q.Y - pc.Y) * pd.Y);
                        double a1 = part.Max(q => (q.X - pc.X) * pd.X + (q.Y - pc.Y) * pd.Y);
                        double rms = Math.Sqrt(part.Average(q => Sq((q.X - pc.X) * -pd.Y + (q.Y - pc.Y) * pd.X)));
                        segments.Add(new Segment(new P2(pc.X + pd.X * a0, pc.Y + pd.Y * a0), new P2(pc.X + pd.X * a1, pc.Y + pd.Y * a1), n, rms));
                        any = true;
                    }
                    start = i;
                }
            }
            var drop = new HashSet<int>(best);
            remaining = remaining.Where((_, i) => !drop.Contains(i)).ToList();
            if (!any) failures++; else failures = 0;
        }
        return segments;
    }

    static double Sq(double v) => v * v;

    public static (P2 centre, P2 dir) FitLine(List<P2> pts)
    {
        double mx = pts.Average(p => p.X), my = pts.Average(p => p.Y);
        double sxx = 0, syy = 0, sxy = 0;
        foreach (var p in pts) { var dx = p.X - mx; var dy = p.Y - my; sxx += dx * dx; syy += dy * dy; sxy += dx * dy; }
        double ang = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
        return (new P2(mx, my), new P2(Math.Cos(ang), Math.Sin(ang)));
    }

    /// <summary>Dominant orientation (mod 90 deg) for Manhattan snapping.</summary>
    public static double DominantAngle(List<Segment> segs)
    {
        if (segs.Count == 0) return 0;
        var bins = new double[90];
        foreach (var s in segs)
        {
            var deg = (s.Angle * 180 / Math.PI % 90 + 90) % 90;
            bins[(int)deg % 90] += s.Length;
        }
        int best = Array.IndexOf(bins, bins.Max());
        // refine with weighted mean around the best bin
        double sum = 0, w = 0;
        foreach (var s in segs)
        {
            var deg = (s.Angle * 180 / Math.PI % 90 + 90) % 90;
            var d = deg - best; if (d > 45) d -= 90; if (d < -45) d += 90;
            if (Math.Abs(d) <= 3) { sum += (best + d) * s.Length; w += s.Length; }
        }
        return w > 0 ? sum / w : best;
    }

    public static Segment Snap(Segment s, double dominantDeg, double maxSnapDeg = 3)
    {
        var deg = s.Angle * 180 / Math.PI;
        double target = double.NaN;
        for (int k = -4; k <= 4; k++)
        {
            var cand = dominantDeg + 90 * k;
            if (Math.Abs(deg - cand) <= maxSnapDeg) { target = cand; break; }
        }
        if (double.IsNaN(target)) return s;
        var mid = new P2((s.A.X + s.B.X) / 2, (s.A.Y + s.B.Y) / 2);
        var half = s.Length / 2;
        var r = target * Math.PI / 180;
        return s with { A = new P2(mid.X - Math.Cos(r) * half, mid.Y - Math.Sin(r) * half), B = new P2(mid.X + Math.Cos(r) * half, mid.Y + Math.Sin(r) * half) };
    }

    /// <summary>
    /// Pair parallel faces into walls (centreline + thickness). Unpaired long faces become
    /// "single-face" walls (only one side scanned) using defaultThickness, flagged for inspection.
    /// </summary>
    public static List<WallCandidate> PairWalls(List<Segment> segs, double minThk = 60, double maxThk = 600,
        double defaultThickness = 200, double minSingleFaceLength = 1000)
    {
        var used = new bool[segs.Count];
        var walls = new List<WallCandidate>();
        var order = Enumerable.Range(0, segs.Count).OrderByDescending(i => segs[i].Length).ToList();
        foreach (var i in order)
        {
            if (used[i]) continue;
            var a = segs[i];
            int bestJ = -1; double bestScore = 0, bestD = 0;
            foreach (var j in order)
            {
                if (j == i || used[j]) continue;
                var b = segs[j];
                var dAng = Math.Abs(Math.Sin(a.Angle - b.Angle));
                if (dAng > Math.Sin(4 * Math.PI / 180)) continue;
                var (dist, overlap) = DistanceAndOverlap(a, b);
                if (dist < minThk || dist > maxThk) continue;
                var ovShare = overlap / Math.Min(a.Length, b.Length);
                if (ovShare < 0.5) continue;
                var score = ovShare * Math.Min(a.Support, b.Support) / dist;
                if (score > bestScore) { bestScore = score; bestJ = j; bestD = dist; }
            }
            if (bestJ >= 0)
            {
                var b = segs[bestJ];
                used[i] = used[bestJ] = true;
                // centreline = mid-line over the union of both faces' extents
                var dir = new P2(Math.Cos(a.Angle), Math.Sin(a.Angle));
                var mid = new P2((a.A.X + a.B.X + b.A.X + b.B.X) / 4, (a.A.Y + a.B.Y + b.A.Y + b.B.Y) / 4);
                var ts = new[] { a.A, a.B, b.A, b.B }.Select(p => (p.X - mid.X) * dir.X + (p.Y - mid.Y) * dir.Y).ToList();
                var s = new P2(mid.X + dir.X * ts.Min(), mid.Y + dir.Y * ts.Min());
                var e = new P2(mid.X + dir.X * ts.Max(), mid.Y + dir.Y * ts.Max());
                var conf = Math.Min(1.0, 0.5 + 0.5 * Math.Min(1, (a.Support + b.Support) / 600.0)) * (1 - Math.Min(0.5, (a.Rms + b.Rms) / 40));
                walls.Add(new WallCandidate(s, e, Math.Round(bestD / 5) * 5, "two-face", Math.Round(conf, 2), a.Support + b.Support));
            }
        }
        foreach (var i in order)
        {
            if (used[i] || segs[i].Length < minSingleFaceLength) continue;
            var a = segs[i];
            walls.Add(new WallCandidate(a.A, a.B, defaultThickness, "single-face (offset needs review)", Math.Round(0.35 * (1 - Math.Min(0.5, a.Rms / 40)), 2), a.Support));
        }
        return walls;
    }

    static (double dist, double overlap) DistanceAndOverlap(Segment a, Segment b)
    {
        var dir = new P2(Math.Cos(a.Angle), Math.Sin(a.Angle));
        var n = new P2(-dir.Y, dir.X);
        var bm = new P2((b.A.X + b.B.X) / 2, (b.A.Y + b.B.Y) / 2);
        var dist = Math.Abs((bm.X - a.A.X) * n.X + (bm.Y - a.A.Y) * n.Y);
        double a0 = 0, a1 = a.Length;
        double b0 = (b.A.X - a.A.X) * dir.X + (b.A.Y - a.A.Y) * dir.Y;
        double b1 = (b.B.X - a.A.X) * dir.X + (b.B.Y - a.A.Y) * dir.Y;
        if (b0 > b1) (b0, b1) = (b1, b0);
        return (dist, Math.Max(0, Math.Min(a1, b1) - Math.Max(a0, b0)));
    }

    /// <summary>Wall end snapping: extend/trim wall ends to meet neighbours (corners, T-junctions).</summary>
    public static List<WallCandidate> CleanJoins(List<WallCandidate> walls, double snapMm = 350)
    {
        var w = walls.ToList();
        for (int i = 0; i < w.Count; i++)
        {
            for (int j = 0; j < w.Count; j++)
            {
                if (i == j) continue;
                var x = Intersect(w[i].Start, w[i].End, w[j].Start, w[j].End);
                if (x == null) continue;
                var p = x.Value;
                if (Dist(p, w[i].Start) < snapMm && DistToSeg(p, w[j]) < snapMm) w[i] = w[i] with { Start = p };
                else if (Dist(p, w[i].End) < snapMm && DistToSeg(p, w[j]) < snapMm) w[i] = w[i] with { End = p };
            }
        }
        return w;
    }

    static double DistToSeg(P2 p, WallCandidate s)
    {
        double dx = s.End.X - s.Start.X, dy = s.End.Y - s.Start.Y;
        double l2 = dx * dx + dy * dy;
        double t = l2 == 0 ? 0 : Math.Clamp(((p.X - s.Start.X) * dx + (p.Y - s.Start.Y) * dy) / l2, 0, 1);
        return Dist(p, new P2(s.Start.X + t * dx, s.Start.Y + t * dy));
    }

    static P2? Intersect(P2 a, P2 b, P2 c, P2 d)
    {
        double d1x = b.X - a.X, d1y = b.Y - a.Y, d2x = d.X - c.X, d2y = d.Y - c.Y;
        double den = d1x * d2y - d1y * d2x;
        if (Math.Abs(den) < 1e-6 * Math.Sqrt((d1x * d1x + d1y * d1y) * (d2x * d2x + d2y * d2y))) return null;
        double t = ((c.X - a.X) * d2y - (c.Y - a.Y) * d2x) / den;
        return new P2(a.X + t * d1x, a.Y + t * d1y);
    }

    // ------------------------------------------------------------------ columns
    /// <summary>Compact clusters (after removing wall points) of column size -> rectangular/round columns.</summary>
    public static List<ColumnCandidate> DetectColumns(List<P2> pts, List<Segment> wallFaces, double cell = 50,
        double minSize = 150, double maxSize = 1200, int minPoints = 25, double wallTol = 40)
    {
        var free = pts.Where(p => wallFaces.All(s => DistToSegment(p, s) > wallTol)).ToList();
        var grid = new Dictionary<(long, long), List<P2>>();
        foreach (var p in free)
        {
            var k = ((long)Math.Floor(p.X / cell), (long)Math.Floor(p.Y / cell));
            if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<P2>();
            l.Add(p);
        }
        var seen = new HashSet<(long, long)>();
        var result = new List<ColumnCandidate>();
        foreach (var start in grid.Keys)
        {
            if (!seen.Add(start)) continue;
            var cluster = new List<P2>();
            var q = new Queue<(long, long)>();
            q.Enqueue(start);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                cluster.AddRange(grid[c]);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var nb = (c.Item1 + dx, c.Item2 + dy);
                        if (grid.ContainsKey(nb) && seen.Add(nb)) q.Enqueue(nb);
                    }
                if (cluster.Count > 20000) break;
            }
            if (cluster.Count < minPoints) continue;
            double x0 = cluster.Min(p => p.X), x1 = cluster.Max(p => p.X), y0 = cluster.Min(p => p.Y), y1 = cluster.Max(p => p.Y);
            double sx = x1 - x0, sy = y1 - y0;
            if (sx < minSize || sy < minSize || sx > maxSize || sy > maxSize) continue;
            if (Math.Max(sx, sy) / Math.Min(sx, sy) > 3) continue;
            var (cx, cy, r, rms) = FitCircle(cluster);
            bool round = rms < 12 && Math.Abs(2 * r - (sx + sy) / 2) < 60;
            result.Add(round
                ? new ColumnCandidate(new P2(cx, cy), Math.Round(2 * r / 10) * 10, Math.Round(2 * r / 10) * 10, "round", cluster.Count)
                : new ColumnCandidate(new P2((x0 + x1) / 2, (y0 + y1) / 2), Math.Round(sx / 10) * 10, Math.Round(sy / 10) * 10, "rectangular", cluster.Count));
        }
        return result;
    }

    static double DistToSegment(P2 p, Segment s)
    {
        double dx = s.B.X - s.A.X, dy = s.B.Y - s.A.Y;
        double l2 = dx * dx + dy * dy;
        double t = l2 == 0 ? 0 : Math.Clamp(((p.X - s.A.X) * dx + (p.Y - s.A.Y) * dy) / l2, 0, 1);
        return Dist(p, new P2(s.A.X + t * dx, s.A.Y + t * dy));
    }

    /// <summary>Algebraic (Kasa) circle fit.</summary>
    public static (double cx, double cy, double r, double rms) FitCircle(List<P2> pts)
    {
        double mx = pts.Average(p => p.X), my = pts.Average(p => p.Y);
        double suu = 0, svv = 0, suv = 0, suuu = 0, svvv = 0, suvv = 0, svuu = 0;
        foreach (var p in pts)
        {
            double u = p.X - mx, v = p.Y - my;
            suu += u * u; svv += v * v; suv += u * v;
            suuu += u * u * u; svvv += v * v * v; suvv += u * v * v; svuu += v * u * u;
        }
        double det = suu * svv - suv * suv;
        if (Math.Abs(det) < 1e-9) return (mx, my, 0, double.MaxValue);
        double a = 0.5 * (suuu + suvv), b = 0.5 * (svvv + svuu);
        double uc = (a * svv - b * suv) / det, vc = (b * suu - a * suv) / det;
        double cx = uc + mx, cy = vc + my;
        double r = Math.Sqrt(uc * uc + vc * vc + (suu + svv) / pts.Count);
        double rms = Math.Sqrt(pts.Average(p => Sq(Math.Sqrt(Sq(p.X - cx) + Sq(p.Y - cy)) - r)));
        return (cx, cy, r, rms);
    }

    // ------------------------------------------------------------------ outline
    /// <summary>Convex hull (Andrew monotone chain) - floor/slab outline from slab points.</summary>
    public static List<P2> ConvexHull(List<P2> pts)
    {
        var p = pts.Distinct().OrderBy(q => q.X).ThenBy(q => q.Y).ToList();
        if (p.Count < 3) return p;
        static double Cross(P2 o, P2 a, P2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var h = new P2[2 * p.Count];
        int k = 0;
        for (int i = 0; i < p.Count; i++) { while (k >= 2 && Cross(h[k - 2], h[k - 1], p[i]) <= 0) k--; h[k++] = p[i]; }
        for (int i = p.Count - 2, t = k + 1; i >= 0; i--) { while (k >= t && Cross(h[k - 2], h[k - 1], p[i]) <= 0) k--; h[k++] = p[i]; }
        return h.Take(k - 1).ToList();
    }
}
