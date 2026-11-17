using System.Diagnostics;
using System.Text;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace KnotAndSpool.EditorTools
{
    /// Generates the first levels and logs their difficulty settings, to check that generation succeeds.
    /// Command line: Unity -batchmode -quit -projectPath . -executeMethod KnotAndSpool.EditorTools.LevelReport.Run
    public static class LevelReport
    {
        [MenuItem("Knot & Spool/Level Report")]
        public static void Run()
        {
            var sb = new StringBuilder("[Level Report]\n level  fabric  colors  spools  board  slots  buried  hidden   ms\n");
            for (int level = 1; level <= 30; level++)
            {
                var watch = Stopwatch.StartNew();
                var data = LevelGenerator.Generate(level);
                watch.Stop();

                int spools = 0, hidden = 0;
                var colors = new System.Collections.Generic.HashSet<int>();
                foreach (int c in data.Cells) colors.Add(c);
                foreach (var s in data.Board)
                {
                    if (s == null) continue;
                    spools++;
                    if (s.Hidden) hidden++;
                }
                sb.AppendLine($" {level,5}  {data.Width}x{data.Height,-3}  {colors.Count,6}  {spools,6}  {data.BoardWidth}x{data.BoardHeight}    {data.Slots,5}  {data.Jitter,6}  {hidden,6}  {watch.ElapsedMilliseconds,4}");
            }
            Debug.Log(sb.ToString());
        }
    }
}
