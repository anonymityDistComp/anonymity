using System;
using System.Collections.Generic;

namespace Anonymity
{
    static class HistoryTest
    {
        public static HistoryTree h;

        class RandomTest
        {
            public int n; // number of agents
            public int round = 0;
            public List<Entity> e = new List<Entity>(); // entity representative of each group
            public List<int> m = new List<int>(); // multiplicity of each group (less or equal to anonymity)

            private readonly Random rand;

            public RandomTest(int m = 10, int k = 10, bool differentInputs = false, int seed = -1) // create leader plus m groups of size k
            {
                rand = (seed == -1) ? new Random() : new Random(seed);
                NewGroup(1, 0);
                for (int i = 0; i < m; i++) NewGroup(k, differentInputs ? i + 1 : 1);
                CountEntities();
            }

            private int CountEntities()
            {
                n = 0;
                foreach (int x in m) n += x;
                return n;
            }

            private void NewGroup(int k, int input = 1)
            {
                e.Add(new Entity(input));
                m.Add(k);
            }

            public bool Split(int k, int a, bool adjacent = false) // split the kth group in two groups, one of which has size a
            {
                if (a <= 0 || a >= m[k]) return false;
                if (adjacent)
                {
                    e.Insert(k + 1, e[k].Copy());
                    m.Insert(k + 1, m[k] - a);
                }
                else
                {
                    e.Add(e[k].Copy());
                    m.Add(m[k] - a);
                }
                m[k] = a;
                return true;
            }

            public bool RandomSplit(int k, double r = 0.25, bool adjacent = false)
            {
                int s = m[k];
                int a = (int)(s * r);
                int b = (int)(s * (1 - r));
                return Split(k, rand.Next(a, b + 1), adjacent);
            }

            private bool ToBeSplitted1(int k, double splitChance) => m[k] > 1 && rand.NextDouble() < splitChance;

            private bool ToBeSplitted2(int k, double splitChance)
            {
                if (m[k] < 2) return false;
                double biasedChance = Math.Atan(m[k]) * 2.0 / Math.PI;
                return rand.NextDouble() < splitChance * biasedChance;
            }

            public void Interact(int g1, int g2, int multiplicity = 1)
            {
                int m1 = multiplicity;
                int m2 = m[g1] * m1 / m[g2];
                if (m[g1] * m1 != m[g2] * m2)
                {
                    m1 = m[g2] * multiplicity;
                    m2 = m[g1] * multiplicity;
                }
                e[g1].Observe(e[g2], m1);
                e[g2].Observe(e[g1], m2);
            }
            /*
                        public (int, int) EndRound() // returns minimum guess and maximum guess
                        {
                            round++;
                            int min = n + 1, max = -1;
                            foreach (Entity x in e)
                            {
                                int g = x.EndRound(true);
                                min = Math.Min(min, g);
                                max = Math.Max(max, g);
                            }
                            return (min, max);
                        }
            */

            public int EndRound2() // everyone matters
            {
                round++;
                int g = 0;
                foreach (Entity x in e) g = g == 0 ? x.EndRound(true) : Math.Min(g, x.EndRound(true));
                return g;
            }

            public int EndRound() // only leader matters
            {
                round++;
                int g = -1;
                foreach (Entity x in e)
                {
                    int t = x.EndRound(x.Leader());
                    if (g != -1 && t != -1) System.Diagnostics.Debug.Assert(g == t);
                    if (x.Leader()) g = t;
                }
                return g;
            }

            private void SwapGroups(int i, int j)
            {
                Entity te = e[i];
                int tm = m[i];
                e[i] = e[j]; m[i] = m[j];
                e[j] = te; m[j] = tm;
            }

            private void ShuffleGroups(bool includeLeader = true)
            {
                for (int i = includeLeader ? 0 : 1; i < e.Count - 1; i++)
                    SwapGroups(i, rand.Next(i, e.Count));
            }

            public int RandomRound(bool line = false, double splitChance = 0.1, double splitRatio = 0.25, bool simpleSplitModel = true, int multMin = 1, int multMax = 10)
            {
                // randomly split some groups
                for (int i = 0; i < e.Count; i++) if (simpleSplitModel ? ToBeSplitted1(i, splitChance) : ToBeSplitted2(i, splitChance)) RandomSplit(i, splitRatio);

                // generate random interaction graph
                ShuffleGroups(!line);
                if (line) for (int i = 0; i < e.Count - 1; i++) Interact(i, i + 1, rand.Next(multMin, multMax + 1));
                else for (int i = 1; i < e.Count; i++) Interact(i, rand.Next(i), rand.Next(multMin, multMax + 1));

                return EndRound();
            }

            public int RandomExecution(bool line = false, double splitChance = 0.1, double splitRatio = 0.25, bool simpleSplitModel = true, int multMin = 1, int multMax = 10)
            {
                int g;
                do g = RandomRound(line, splitChance, splitRatio, simpleSplitModel, multMin, multMax); while (g == -1);
                return g;
            }

            public int AdversarialExecution()
            {
                int g;
                do
                {
                    if (round % 2 == 0) Split(1, m[1] - 1, true);
                    for (int i = 0; i < e.Count - 1; i++) Interact(i, i + 1, 1);
                    g = EndRound();
                } while (g == -1);
                return g;
            }
        }

        static void Interact(Entity e1, Entity e2, int m = 1)
        {
            e1.Observe(e2, m);
            if (e1 != e2) e2.Observe(e1, m);
        }

        static void InteractM(List<Entity> e, int a, int b, int m) => Interact(e[a], e[b], m);

        static void InteractM(List<Entity> e, int a1, int a2, int b1, int b2, int m)
        {
            for (int i = a1; i <= a2; i++)
                for (int j = b1; j <= b2; j++)
                    Interact(e[i], e[j], m);
        }

        static void InteractM(List<Entity> e, int a, int b1, int b2, int m) => InteractM(e, a, a, b1, b2, m);

        static void Interact(List<Entity> e, int a, int b) => Interact(e[a], e[b]);

        static void Interact(List<Entity> e, int a1, int a2, int b1, int b2) => InteractM(e, a1, a2, b1, b2, 1);

        static void Interact(List<Entity> e, int a, int b1, int b2) => Interact(e, a, a, b1, b2);

        static void PathInteract(List<Entity> e, int a, int b, int m = 1)
        {
            for (int i = a; i < b; i++) Interact(e[i], e[i + 1], m);
        }

        static void CycleInteract(List<Entity> e, int a, int b, int m = 1)
        {
            PathInteract(e, a, b, m);
            InteractM(e, a, b, m);
        }

        static void RandomTests()
        {
            int batchSize = 1000;
            int numGroups = 1;
            int maxGroups = 4;
            int groupSize = 20;
            int maxGroupSize = 20;
            bool line = true;
            double splitChance = 0.15;
            double splitRatio = 0.25;
            bool simpleSplitModel = true;
            bool differentInputs = false;

            RandomTest t = new RandomTest();
            while (numGroups <= maxGroups)
            {
                int i = 400, mr = 0, seedMax = 0;
                while (i != batchSize)
                {
                    t = new RandomTest(numGroups, groupSize, differentInputs, i);
                    int g = t.RandomExecution(line, splitChance, splitRatio, simpleSplitModel);
                    int n = t.n;
                    int bound = 2 * n;
                    int r = t.round;
                    if (r > mr) { mr = r; seedMax = i; }
                    Console.WriteLine("(" + numGroups + " x " + groupSize + ") random seed: " + i + "; estimate: " + g + " = " + n + "; rounds: " + r + " <= " + bound + "; groups: " + t.e.Count + "; max rounds: " + mr + "; max seed: " + seedMax);
                    //System.Diagnostics.Debug.Assert(g == n);
                    //System.Diagnostics.Debug.Assert(r <= bound);
                    if (g != n) { h = new HistoryTree(); foreach (var en in t.e) h.Merge(en.history); return; }
                    //if (r >= 37) { h = t.e[0].history; return; }
                    if (r > bound) { h = new HistoryTree(); foreach (var en in t.e) h.Merge(en.history); return; }
                    i++;
                }
                groupSize++;
                if (groupSize > maxGroupSize)
                {
                    groupSize = 1;
                    numGroups++;
                }
            }
            h = new HistoryTree(); foreach (var en in t.e) h.Merge(en.history);
            //h = t.e[0].history;
        }

        static void WorstCaseAlgorithm1()
        {
            int groupSize = 15;
            RandomTest t = new RandomTest(1, groupSize, false, 0);
            int g = t.AdversarialExecution();
            int n = t.n;
            System.Diagnostics.Debug.Assert(g == n);
            //System.Diagnostics.Debug.Assert(t.round <= 2 * n);
            h = new HistoryTree(); foreach (var en in t.e) h.Merge(en.history);
        }

        static void EndRound(List<Entity> e)
        {
            for (int i = 0; i < e.Count; i++) e[i].EndRound(false);
        }

        static void EndRound(List<Entity> e1, List<Entity> e2)
        {
            EndRound(e1);
            EndRound(e2);
        }

        static void LowerBoundTopology1(List<Entity> e, int m, int round)
        {
            int n = e.Count;
            for (int i = 0; i < m - 1; i++) Interact(e[i], e[i + 1]);
            for (int i = n - 1; i > m; i--) Interact(e[i], e[i - 1]);
            if (round < m)
            {
                Interact(e[m - 1], e[m]);
                Interact(e[n - 1], e[0]);
            }
            else Interact(e[m - 1], e[n - 1]);
        }

        static void LowerBound1(bool counting)
        {
            int m = 9;
            int n1 = 2 * m;
            int n2 = counting ? 2 * m + 1 : 4 * m;
            int rounds = 0;
            List<Entity> e1 = new List<Entity>();
            List<Entity> e2 = new List<Entity>();
            for (int i = 0; i < n1; i++) e1.Add(new Entity(i == 0 ? 0 : 1));
            for (int i = 0; i < n2; i++) e2.Add(new Entity(i == 0 ? 0 : counting || i != n2 / 2 ? 1 : 2));
            bool equal;
            do
            {
                rounds++;
                LowerBoundTopology1(e1, m, rounds);
                LowerBoundTopology1(e2, m, rounds);
                EndRound(e1, e2);
                equal = e1[0].history.Equals(e2[0].history);
            } while (equal);

            rounds++;
            LowerBoundTopology1(e1, m, rounds);
            LowerBoundTopology1(e2, m, rounds);
            EndRound(e1, e2);

            h = new HistoryTree();
            foreach (var en in e1) h.Merge(en.history);
        }

        static void BadExample()
        {
            int a = 20;
            //int n = 2 * a + 3;
            int n = a + 3;
            int l = 0;
            int a1 = 1;
            int a2 = a;
            int b1 = a + 1;
            int b2 = a + 2;
            //int c1 = b2 + 1;
            //int c2 = n - 1;
            int g = -1;
            int rounds = 0;
            List<Entity> e = new List<Entity>();
            e.Add(new Entity(0));
            for (int i = a1; i <= a2; i++) e.Add(new Entity(1));
            for (int i = b1; i <= b2; i++) e.Add(new Entity(2));
            //for (int i = c1; i <= c2; i++) e.Add(new Entity(3));
            do
            {
                rounds++;
                switch (rounds)
                {
                    case 1:
                        Interact(e, l, b1, b2);
                        Interact(e, b1, a1);
                        Interact(e, b2, a1 + 1, a2);
                        break;
                    case 2:
                        Interact(e, l, b1);
                        Interact(e, b1, a1);
                        PathInteract(e, a1, a2);
                        Interact(e, a2, b2);
                        break;
                    case int r when (r >= 3 && r <= 15):
                        Interact(e, l, b1);
                        Interact(e, b1, a1);
                        Interact(e, a1, a1 + r - 1);
                        PathInteract(e, a1 + r - 1, a2);
                        Interact(e, a2, b2);
                        Interact(e, b2, a1 + 1, a1 + r - 2);
                        break;
                    default:
                        Interact(e, l, b1);
                        Interact(e, b1, a1);
                        PathInteract(e, a1, a2);
                        Interact(e, a2, b2);
                        break;
                }
                g = e[0].EndRound(true);
                for (int i = 1; i < e.Count; i++) e[i].EndRound(true);
            } while (g == -1);
            //h = e[0].history;
            h = new HistoryTree();
            foreach (var en in e) h.Merge(en.history);
        }

        static void Counterexample()
        {
            int n = 8 + 1;
            int l = 0;
            int g = -1;
            int rounds = 0;
            List<Entity> e = new List<Entity>();
            e.Add(new Entity(0));
            for (int i = 1; i < n; i++) e.Add(new Entity(1));
            while (++rounds <= 12) //while (g == -1);
            {
                switch (rounds)
                {
                    case int r when (r <= 4): Interact(e, l, 1, 8); break;
                    case int r when (r >= 5 && r <= 6): Interact(e, l, 1, 4); break;
                    case int r when (r >= 7 && r <= 8): Interact(e, l, 5, 8); break;
                    case int r when (r >= 9 && r <= 12): Interact(e, l, (r - 9) * 2 + 1, (r - 9) * 2 + 2); break;
                    default: break;
                }
                g = e[l].EndRound(false);
                for (int i = 1; i < e.Count; i++) e[i].EndRound(true);
            }
            //h = e[l].history;
            h = new HistoryTree();
            foreach (var en in e) h.Merge(en.history);
        }

        static void LowerBoundTopology2(List<Entity> e, int round, bool selfLoop)
        {
            int n = e.Count;
            PathInteract(e, 0, n - 1);
            if (round < n - 1) Interact(e, round, n - 1);
            else if (selfLoop) { Interact(e, n - 1, n - 1); Interact(e, n - 1, n - 1); }
        }

        static void LowerBound2(bool selfLoop)
        {
            int n1 = 6;
            int n2 = n1 + 1;
            int rounds = 0;
            List<Entity> e1 = new List<Entity>();
            List<Entity> e2 = new List<Entity>();
            for (int i = 0; i < n1; i++) e1.Add(new Entity(i == 0 ? 0 : 1));
            for (int i = 0; i < n2; i++) e2.Add(new Entity(i == 0 ? 0 : 1));
            bool equal;
            do
            {
                rounds++;
                LowerBoundTopology2(e1, rounds, selfLoop);
                LowerBoundTopology2(e2, rounds, selfLoop);
                EndRound(e1, e2);
                equal = e1[0].history.Equals(e2[0].history);
            } while (equal);
            h = new HistoryTree();
            foreach (var en in e1) h.Merge(en.history);
        }

        static void LowerBound3(bool all)
        {
            int m = 5;
            int n1 = (int)Math.Pow(2, m) + 1;
            List<Entity> e1 = new List<Entity>();
            for (int i = 0; i < n1; i++) e1.Add(new Entity(i == 0 ? 0 : 1));

            for (int size = n1 - 1; size >= 2; size /= 2)
            {
                if (!all && size == n1 - 1) continue;
                for (int i = 1; i < n1; i += size)
                {
                    if (!all && i >= n1/2) break;
                    for (int j = 0; j < size / 2; j++)
                    {
                        Interact(e1, 0, i, i + size - 1); EndRound(e1);
                    }
                }
            }
            h = new HistoryTree();
            foreach (var x in e1) h.Merge(x.history);
        }

        static void WorstCaseAlgorithm2()
        {
            int n = 10;
            List<Entity> e = new List<Entity>();
            for (int i = 0; i < n; i++) e.Add(new Entity(i == 0 ? 0 : 1));
            int g = -1;
            int rounds = 0;
            while (g == -1)
            {
                rounds++;
                Console.WriteLine("Round " + rounds);
                switch (rounds)
                {
                    case int r when (r <= n - 2):
                        Interact(e, 0, 1, n - r - 1);
                        InteractM(e, 0, n - r, 2);
                        if (r > 1) PathInteract(e, n - r, n - 1);
                        break;
                    default:
                        PathInteract(e, 0, n - 1);
                        break;
                }
                g = e[0].EndRound(true);
                for (int i = 1; i < e.Count; i++) e[i].EndRound(true);
            }

            h = new HistoryTree();
            foreach (var x in e) h.Merge(x.history);
        }

        static void WorstCaseAlgorithm3()
        {
            int n = 11;
            List<Entity> e = new List<Entity>();
            for (int i = 0; i < n; i++) e.Add(new Entity(i == 0 ? 0 : 1));
            int g = -1;
            int rounds = 0;
            while (g == -1)
            {
                rounds++;
                Console.WriteLine("Round " + rounds);
                switch (rounds)
                {
                    case 1:
                        Interact(e, 0, 1, n - 1);
                        break;
                    case int r when (r > 1 && r <= n - 3):
                        Interact(e, 0, 1, n - r - 2);
                        InteractM(e, 0, n - r - 1, 2);
                        PathInteract(e, n - r - 1, n - 2);
                        Interact(e, n - 3, n - 1);
                        break;
                    default:
                        PathInteract(e, 0, n - 2);
                        Interact(e, n - 3, n - 1);
                        break;
                }
                g = e[0].EndRound(true);
                for (int i = 1; i < e.Count; i++) e[i].EndRound(true);
            }

            h = new HistoryTree();
            foreach (var x in e) h.Merge(x.history);
        }

        static void CounterexampleNaiveAlgorithm()
        {
            int n = 10;
            int l = 0;
            int a1 = 1, a2 = 2;
            int b1 = a2 + 1, b2 = n - 1;
            List<Entity> e = new List<Entity>();
            e.Add(new Entity(0));
            for (int i = a1; i <= a2; i++) e.Add(new Entity(1));
            for (int i = b1; i <= b2; i++) e.Add(new Entity(2));
            int g = -1;
            int rounds = 0;
            while (g == -1)
            {
                rounds++;
                Console.WriteLine("Round " + rounds);
                switch (rounds)
                {
                    case int r when (r <= 2):
                        Interact(e, 0, a1);
                        Interact(e, a1, b1);
                        Interact(e, a2, b1 + 1, b2);
                        CycleInteract(e, b1, b2);
                        break;
                    case 3:
                        Interact(e, 0, b1);
                        Interact(e, b1, a1, a2);
                        CycleInteract(e, b1, b2);
                        break;
                    default:
                        Interact(e, 0, b2);
                        PathInteract(e, a1, b2);
                        break;
                }
                g = e[0].EndRound(true);
                for (int i = 1; i < e.Count; i++) e[i].EndRound(true);
            }

            h = new HistoryTree();
            foreach (var x in e) h.Merge(x.history);
        }

        public static void Test()
        {
            //RandomTests();
            BadExample(); // naive algorithm counterexample!
            //Counterexample();
            //CounterexampleNaiveAlgorithm();
            //WorstCaseAlgorithm1();
            //WorstCaseAlgorithm2(); // 3n
            //WorstCaseAlgorithm3();
            //LowerBound1(true);
            //LowerBound2(true); // 2n
            //LowerBound3(true);
        }
    }
}