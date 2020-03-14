using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using OxyPlot.Axes;

namespace Bot
{
    public class GraphableVector
    {
        public List<(DateTime x, decimal y, decimal z)> Data = new List<(DateTime x, decimal y, decimal z)>();

        public int Resolution { get; set; } = 1000;

        public void AddValue(DateTime x, List<(decimal y, decimal z)> data)
        {
            foreach (var (y, z) in data)
            {
                Data.Add((x, y, z));
            }
        }

        public (double Average, (double X, double Y) Min, (double X, double Y) Max, double[,] Rendered) Raster()
        {
            var numX = Data.Select(x => x.x).Distinct().Count();

            var rendered = new double[numX + 1, Resolution + 1];
            var min = (X: DateTimeAxis.ToDouble(Data.Min(x => x.x)), Y: (double) Data.Min(x => x.y * 100000000m));
            var max = (X: DateTimeAxis.ToDouble(Data.Max(x => x.x)), Y: (double) Data.Max(x => x.y * 100000000m));

            var sizeX = max.X - min.X;
            var sizeY = max.Y - min.Y;

            foreach (var (x, y, z) in Data)
            {
                var realX = DateTimeAxis.ToDouble(x);
                var realY = y * 100000000m;
                var partX = Math.Round((realX - min.X) / sizeX * numX);
                var partY = Math.Round(((double)realY - min.Y) / sizeY * Resolution);
                rendered[ (int) partX, (int) partY] += (double) z;
            }

            return ((double) Data.Average(x => x.z), min, max, rendered);
        }
    }
}