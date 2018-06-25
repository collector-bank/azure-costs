using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace azurecosts
{
    class CostGroup
    {
        public string subscription { get; set; }
        public string type { get; set; }
        public double cost { get; set; }
    }

    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("azurecosts 1.0 - Shows resource costs based on downloaded csv files.\n\nUsage: azurecosts <file pattern>");
                return 1;
            }

            ParseCostFiles(args[0]);

            return 0;
        }

        static void ParseCostFiles(string filepattern)
        {
            string path, pattern;
            if (filepattern.Contains(Path.DirectorySeparatorChar) || filepattern.Contains(Path.AltDirectorySeparatorChar))
            {
                path = Path.GetDirectoryName(filepattern);
                pattern = Path.GetFileName(filepattern);
            }
            else
            {
                path = ".";
                pattern = filepattern;
            }

            string prefix1 = "." + Path.DirectorySeparatorChar;
            string prefix2 = "." + Path.AltDirectorySeparatorChar;

            string[] files = Directory.GetFiles(path, pattern)
                .Select(f => f.StartsWith(prefix1) ? f.Substring(prefix1.Length) : f.StartsWith(prefix2) ? f.Substring(prefix2.Length) : f)
                .OrderBy(f => f)
                .ToArray();

            List<CostGroup> allcostgroups = new List<CostGroup>();

            foreach (string filename in files)
            {
                string[] rows = File.ReadAllLines(filename);

                string subscription = Path.GetFileNameWithoutExtension(filename);

                if (rows.Length < 9)
                {
                    Console.WriteLine($"Ignoring malformed file, it has too few rows: '{filename}'");
                    continue;
                }

                var costgroups = rows
                    .Skip(8)
                    .Select(r => r.Substring(1, r.Length - 2).Split("\",\""))
                    .Where(v => (v[2] != "--" || v[0] == "Other classic resources") && v[3] != "0.00")
                    .Select(v => new { id = v[0], type = v[1] == "--" ? "Other classic resources" : v[1], cost = double.Parse(v[3].Replace(",", string.Empty), CultureInfo.InvariantCulture) })
                    .GroupBy(c => c.type)
                    .Select(cg => new CostGroup { subscription = subscription, type = cg.Key, cost = cg.Sum(c => c.cost) });

                allcostgroups.AddRange(costgroups);
            }

            string[] subscriptions = files
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToArray();

            var uniquecostgroups = allcostgroups
                .GroupBy(cg => cg.type)
                .Select(cg => cg.Key);

            var outputs = " "
                .Select(o => new { name = string.Empty, costs = subscriptions.Select(s => new { subscription = s, cost_d = double.MaxValue, cost = s }) })
                .Concat(allcostgroups
                .GroupBy(cg => cg.type)
                .OrderBy(o => o.Key)
                .Select(o => new { name = o.Key, costs = o.Select(cg => new { subscription = cg.subscription, cost_d = cg.cost, cost = cg.cost.ToString("0.00") }) }))
                .Append(new
                {
                    name = "Total",
                    costs = subscriptions.Select(s => new
                    {
                        subscription = s,
                        cost_d = double.MinValue,
                        cost = allcostgroups.Where(cg => cg.subscription == s).Sum(cg => cg.cost).ToString("0.00")
                    })
                });

            int coltype = uniquecostgroups
                .Max(cg => cg.Length);
            int[] colsubscriptions = outputs
                .SelectMany(ocg => ocg.costs)
                .GroupBy(ocg => ocg.subscription)
                .Select(ocg => ocg.Max(cg => cg.cost.Length))
                .ToArray();


            foreach (var cost in outputs.OrderBy(o => o.costs.Sum(ocg => -ocg.cost_d)))
            {
                Console.Write(string.Format("{0," + -coltype + "}", cost.name));
                for (int i = 0; i < colsubscriptions.Length; i++)
                {
                    if (cost.costs.Any(c => c.subscription == subscriptions[i]))
                    {
                        Console.Write(string.Format("  {0," + colsubscriptions[i] + "}", cost.costs.Single(c => c.subscription == subscriptions[i]).cost));
                    }
                    else
                    {
                        Console.Write(new string(' ', colsubscriptions[i] + 2));
                    }
                }
                Console.WriteLine();
            }

            return;
        }
    }
}
