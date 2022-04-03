using System;
using System.Collections.Generic;

namespace Anonymity
{
    public class HistoryTree
    {
        private static int SERIAL = 0;
        private int serial = SERIAL++;

        public readonly int level = -1; // level in the history tree (equal to round number)
        public readonly int input = 0; // 0: leader
        public bool selected = true; // used to exclude some nodes from the tree
        public int guess = -1; // estimated anonymity
        public int deepest = -1; // level of deepest node used to obtain guess (only used in aggressive mode)
        public bool correct = false; // is guess known to be correct? (only used in careful mode)
        public int weight = 0; // number of guesses in subtree (only used in careful mode)
        public int cumulativeAnonymity = 0; // anonymity coming from children (only used in careful mode)
        public bool cut = false; // subtree hanging from node has a cut of correct nodes (only used in careful mode)
        public bool guesser = false; // the anonymities of the node and all its children are known (only used in careful mode)

        public readonly HistoryTree parent;
        public readonly int siblingIndex; // index in parent's children list (needed only for drawing the tree)
        public readonly List<HistoryTree> children = new List<HistoryTree>();
        public readonly List<Tuple<HistoryTree, int>> observations = new List<Tuple<HistoryTree, int>>(); // agents observed by this state (upward links)
        public readonly List<Tuple<HistoryTree, int>> observed = new List<Tuple<HistoryTree, int>>(); // agents that observed this state (downward links)

        private HistoryTree reference = null; // used when merging trees

        public HistoryTree(HistoryTree parent = null, int siblingIndex = -1, int input = 1)
        {
            this.parent = parent;
            this.siblingIndex = siblingIndex;
            this.input = input;
            if (parent != null) level = parent.level + 1;
        }

        public void ResetReferences()
        {
            reference = null;
            foreach (var h in children) h.ResetReferences();
        }

        public bool NodeEquals(HistoryTree h)
        {
            if (h == null) return false;
            if (parent == null) { if (h.parent != null) return false; }
            else if (h.parent == null || parent.reference != h.parent) return false;
            if (level != h.level) return false;
            if (input != h.input) return false;
            if (observations.Count != h.observations.Count) return false;
            foreach (var x in observations)
            {
                bool found = false;
                foreach (var y in h.observations)
                    if (x.Item1.reference == y.Item1 && x.Item2 == y.Item2) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        public (HistoryTree, bool) Merge(HistoryTree h) // returns endpoint in new tree and whether any nodes were added
        {
            var deepest = h;
            bool added = false;
            var q = new Queue<HistoryTree>();
            reference = h;
            h.reference = this;
            q.Enqueue(h);
            while (q.Count > 0)
            {
                var a = q.Dequeue();
                if (a.level > deepest.level) deepest = a;
                var b = a.reference;
                foreach (var x in a.children)
                {
                    q.Enqueue(x);
                    HistoryTree z = null;
                    foreach (var y in b.children)
                        if (y.NodeEquals(x)) { z = y; break; }
                    if (z == null)
                    {
                        added = true;
                        z = b.AddChild(x.input);
                        foreach (var c in x.observations)
                        {
                            var d = c.Item1.reference;
                            int m = c.Item2;
                            z.observations.Add(new Tuple<HistoryTree, int>(d, m));
                            d.observed.Add(new Tuple<HistoryTree, int>(z, m));
                        }
                    }
                    x.reference = z;
                    z.reference = x;
                }
            }
            deepest = deepest.reference;
            ResetReferences();
            h.ResetReferences();
            return (deepest, added);
        }

        public (HistoryTree, HistoryTree) Copy() // returns copied tree and deepest node
        {
            var h = new HistoryTree();
            var c = h.Merge(this).Item1;
            return (h, c);
        }

        public bool Contains(HistoryTree h) => !Copy().Item1.Merge(h).Item2;

        public bool Equals(HistoryTree h) => Contains(h) && h.Contains(this);

        public HistoryTree AddChild(int input)
        {
            var t = new HistoryTree(this, children.Count, input);
            children.Add(t);
            return t;
        }

        public HistoryTree AddChild() => AddChild(input);

        public void AddObservation(HistoryTree h, int multiplicity)
        {
            for (int i = 0; i < observations.Count; i++)
            {
                var x = observations[i];
                if (x.Item1 == h)
                {
                    observations[i] = new Tuple<HistoryTree, int>(h, x.Item2 + multiplicity);
                    for (int j = 0; j < h.observed.Count; j++)
                    {
                        var y = h.observed[j];
                        if (y.Item1 == this)
                        {
                            h.observed[j] = new Tuple<HistoryTree, int>(this, y.Item2 + multiplicity);
                            break;
                        }
                    }
                    return;
                }
            }
            observations.Add(new Tuple<HistoryTree, int>(h, multiplicity));
            h.observed.Add(new Tuple<HistoryTree, int>(this, multiplicity));
        }

        public int CountAgents(bool reset = true, bool careful = true)
        {
            int round = ResetCounts(reset);
            if (careful)
            {
                int deep = GuessCarefully(false);
                if (deep == -1) return -1;
                return round >= deep + cumulativeAnonymity ? cumulativeAnonymity : -1;
            }
            else
            {
                //int n = GuessAggressively();
                int n = GuessNaively();
                return round >= 2 * n ? n : -1;
            }
        }

        private (int, int) AllChildrenGuessed() // returns sum of guesses and deepest
        {
            int g = 0;
            int d = -1;
            foreach (var x in children)
            {
                if (!x.selected) continue;
                if (x.guess == -1) return (-1, -1);
                g += x.guess;
                d = Math.Max(d, x.deepest);
            }
            return (g == 0 ? -1 : g, d);
        }

        private (HistoryTree, int, int) UniqueChildNotGuessed() // returns sum of guesses and deepest
        {
            if (guess == -1) return (null, -1, -1);
            int g = guess;
            int d = deepest;
            HistoryTree h = null;
            foreach (var x in children)
            {
                if (!x.selected) continue;
                if (x.guess == -1) { if (h == null) { h = x; continue; } else return (null, -1, -1); }
                g -= x.guess;
                d = Math.Max(d, x.deepest);
            }
            return (h, g, d);
        }

        private static void MakeGuess(SortedSet<HistoryTree> s, HistoryTree h, int guess, int deepest)
        {
            if (h == null || !h.selected || h.guess != -1) return;
            h.guess = guess;
            h.deepest = Math.Max(h.level, deepest);
            s.Add(h);
        }

        private static void MakeGuess(SortedSet<HistoryTree> s, HistoryTree h, int guess) => MakeGuess(s, h, guess, -1);

        private int GuessAggressively()
        {
            var s = new SortedSet<HistoryTree>(new NodeComparer());
            foreach (var x in children) if (x.input == 0) MakeGuess(s, x, 1);
            while (s.Count > 0)
            {
                var h = s.Min;
                s.Remove(h);
                int a = h.guess;

                var c1 = h.UniqueChildNotGuessed(); // if exactly one of my children is not guessed, guess it
                if (c1.Item1 != null) MakeGuess(s, c1.Item1, c1.Item2, c1.Item3);

                var p = h.parent;
                if (p == null) continue;

                var b1 = p.AllChildrenGuessed(); // if all my siblings are guessed, guess my parent
                if (b1.Item1 != -1) MakeGuess(s, p, b1.Item1, b1.Item2);
                var b2 = p.UniqueChildNotGuessed(); // if my parent is guessed and exactly one of my siblings is not guessed, guess it
                if (b2.Item1 != null) MakeGuess(s, b2.Item1, b2.Item2, b2.Item3);

                foreach (var t1 in h.observations)
                {
                    var p2 = t1.Item1; // i observed p2
                    if (!p2.selected) continue;
                    int found = 0;
                    HistoryTree h2 = null;
                    int m = -1;
                    foreach (var t2 in p2.children)
                    {
                        if (!t2.selected) continue;
                        foreach (var t3 in t2.observations)
                            if (t3.Item1 == p) { found++; h2 = t2; m = t3.Item2; break; }
                        if (found > 1) break;
                    }
                    if (found != 1 || h2.guess != -1) continue; // h2 is the unique child of p2 that observed p (with multiplicity m)
                    bool fail = false;
                    int g = 0; // number of links from children of p to p2
                    int d = -1; // deepest level used to guess the children of p that observed p2
                    foreach (var t2 in p.children)
                    {
                        if (!t2.selected) continue;
                        foreach (var t3 in t2.observations)
                            if (t3.Item1 == p2)
                            {
                                if (t2.guess == -1) fail = true; // if an unguessed child of p observed p2, leave
                                else { g += t2.guess * t3.Item2; d = Math.Max(d, t2.deepest); }
                                break;
                            }
                        if (fail) break;
                    }
                    if (fail) continue;
                    MakeGuess(s, h2, (g - 1) / m + 1, d); // guess h2 as ceiling(g / m)
                }
            }
            return guess;
        }

        private class NodeComparer : IComparer<HistoryTree>
        {
            public int Compare(HistoryTree x, HistoryTree y)
            {
                int a = x.level; // ToDo: try with x.deepest
                int b = y.level; // ToDo: try with y.deepest
                if (a == b)
                {
                    a = x.serial;
                    b = y.serial;
                }
                if (a < b) return -1;
                if (a > b) return 1;
                return 0;
            }
        }

        private class AuxData
        {
            public readonly int n;
            public List<List<HistoryTree>> l; // level-by-level list of relevant nodes
            private bool[] g; // does a level have a guessed node?
            private bool[] c; // does a level have new guessers since the last guess?
            private SortedSet<int> s; // priority queue of levels with guessers but no guessed nodes (c[i] && !g[i+1])

            public AuxData(HistoryTree h)
            {
                l = new List<List<HistoryTree>>();
                foreach (var x in h.children) // create one level for each leader node
                    if (x.input == 0)
                    {
                        var y = x;
                        while (y.selected)
                        {
                            var nl = new List<HistoryTree>();
                            nl.Add(y);
                            l.Add(nl);
                            if (y.children.Count == 0) break;
                            y = y.children[0];
                        }
                        break;
                    }
                n = l.Count;
                AddLevelList(h); // add the non-leader nodes
                g = new bool[n];
                c = new bool[n];
                s = new SortedSet<int>();
            }

            private void AddLevelList(HistoryTree h)
            {
                if (!h.selected || h.input == 0 || h.level >= n) return;
                if (h.level >= 0) l[h.level].Add(h);
                foreach (var x in h.children) AddLevelList(x);
            }

            public void SetGuess(int i, bool b)
            {
                if (g[i]) System.Diagnostics.Debug.Assert(!b);
                else System.Diagnostics.Debug.Assert(b);
                g[i] = b;
                if (b) s.Remove(i - 1);
                else if (c[i - 1]) s.Add(i - 1);
            }

            public void SetGuesser(int i, bool b)
            {
                c[i] = b;
                if (!b) s.Remove(i);
                else if (!g[i + 1]) s.Add(i);
            }

            public bool QueueEmpty() => s.Count == 0;

            public int GetNextLevel()
            {
                var i = s.Min;
                s.Remove(i);
                System.Diagnostics.Debug.Assert(c[i] && !g[i + 1]);
                return i;
            }
        }

        private bool Heavy() => guess != -1 && weight >= guess;

        private bool AddWeight(AuxData data, int w) // returns whether the root has been cut
        {
            System.Diagnostics.Debug.Assert(selected);
            weight += w;
            System.Diagnostics.Debug.Assert(weight >= 0);
            if (Heavy())
            {
                weight -= w;
                return SetCorrect(data);
            }
            if (parent == null) return false;
            return parent.AddWeight(data, w);
        }

        private bool Guess(AuxData data, int g) // returns whether the root has been cut
        {
            System.Diagnostics.Debug.Assert(selected && guess == -1);
            guess = g;
            data.SetGuess(level, true);
            return AddWeight(data, 1);
        }

        private void CoverIsland(AuxData data)
        {
            System.Diagnostics.Debug.Assert(selected);
            System.Diagnostics.Debug.Assert(!guesser);
            if (guess != -1 && !correct)
            {
                data.SetGuess(level, false);
                AddWeight(data, -1);
            }
            guess = cumulativeAnonymity;
            correct = true;
            cut = true;
            guesser = true;
            data.SetGuesser(level, true);
            foreach (var x in children)
                if (!x.correct) x.CoverIsland(data); // no need to check if selected == true, because it is an island
        }

        private bool UpdateCuts() // returns whether the root has been cut
        {
            System.Diagnostics.Debug.Assert(selected);
            if (correct) return false;
            foreach (var x in children) if (x.selected && !x.correct && !x.cut) return false;
            cut = true;
            if (parent == null) return true;
            return parent.UpdateCuts();
        }

        private bool SetCorrect(AuxData data, int a) // returns whether the root has been cut
        {
            System.Diagnostics.Debug.Assert(selected && !correct);
            guess = a;
            correct = true;
            cut = true;
            HistoryTree h = parent;
            while (h != null)
            {
                h.cumulativeAnonymity += guess - cumulativeAnonymity;
                if (h.correct)
                {
                    if (h.guess == h.cumulativeAnonymity) h.CoverIsland(data);
                    break;
                }
                h = h.parent;
            }
            if (guess == cumulativeAnonymity) CoverIsland(data);
            return parent == null || parent.UpdateCuts();
        }

        private bool SetCorrect(AuxData data)
        {
            System.Diagnostics.Debug.Assert(selected && guess != -1);
            data.SetGuess(level, false);
            return SetCorrect(data, guess);
        }

        private int CumulativeMultiplicity(HistoryTree a, HistoryTree b) // computes cumulative multiplicity from children of a to parent of b
        {
            System.Diagnostics.Debug.Assert(a.selected && b.selected && a.level == b.level - 1);
            var p = b.parent;
            int m = 0;
            foreach (var x in a.children)
            {
                System.Diagnostics.Debug.Assert(x.selected && x.guess != -1 && x.correct);
                foreach (var y in x.observations)
                {
                    if (y.Item1 != p) continue;
                    m += x.guess * y.Item2;
                    break;
                }
            }
            System.Diagnostics.Debug.Assert(m > 0);
            return m;
        }

        private int CutDepth(bool optimize)
        {
            System.Diagnostics.Debug.Assert(selected);
            if (optimize && guess == cumulativeAnonymity) correct = true;
            if (correct) return level;
            System.Diagnostics.Debug.Assert(cumulativeAnonymity != 0);
            int m = 0;
            foreach (var x in children)
                if (x.selected) m = Math.Max(m, x.CutDepth(optimize));
            if (level > 0) System.Diagnostics.Debug.Assert(m > 0);
            return m;
        }

        private int GuessCarefully(bool optimize) // returns deepest level in root's cut
        {
            bool cutFound = false;
            AuxData data = new AuxData(this);
            for (int i = 0; i < data.n; i++)
                if (data.l[i][0].SetCorrect(data, 1)) cutFound = true; // set leader nodes' anonymities
            while (!data.QueueEmpty())
            {
                int j = data.GetNextLevel();
                bool found = false;
                foreach (var x in data.l[j])
                {
                    System.Diagnostics.Debug.Assert(x.selected);
                    if (!x.guesser) continue;
                    foreach (var y in x.observed)
                    {
                        var h = y.Item1;
                        if (!h.selected || h.guess != -1) continue;
                        found = true;
                        if (h.Guess(data, (CumulativeMultiplicity(x, h) - 1) / y.Item2 + 1)) cutFound = true; // ceiling(m1 / m2)
                        break;
                    }
                    if (found) break;
                }
            }
            return cutFound ? CutDepth(optimize) : -1;
        }

        private int GuessNaively()
        {
            AuxData data = new AuxData(this);
            List<HistoryTree> l = null;
            HistoryTree[] c = null;
            int m = 0;
            for (int i = 0; i < data.n - 1; i++)
            {
                l = data.l[i];
                m = l.Count;
                if (m > data.l[i + 1].Count) return -1;
                if (m < data.l[i + 1].Count) continue;
                c = new HistoryTree[m];
                for (int j = 0; j < m; j++)
                {
                    int cn = 0;
                    foreach (var x in l[j].children)
                        if (x.selected) { c[j] = x; cn++; }
                    if (cn != 1) return -1;
                }
                break;
            }
            if (c == null) return -1;
            for (int i = 0; i < m; i++) System.Diagnostics.Debug.Assert(c[i].selected);
            int n = 0, t = 0;
            l[0].guess = 1;
            c[0].guess = 1;
            var s = new SortedSet<int>();
            s.Add(0);
            while (s.Count > 0)
            {
                var i = s.Min;
                s.Remove(i);
                n += l[i].guess;
                t++;
                System.Diagnostics.Debug.Assert(t <= m);
                foreach (var x in l[i].observed)
                {
                    var h = x.Item1;
                    if (!h.selected || h.guess != -1) continue;
                    var p = h.parent;
                    System.Diagnostics.Debug.Assert(p.selected && p.guess == -1);
                    for (int j = 0; j < m; j++)
                        if (l[j] == p) { s.Add(j); break; }
                        else System.Diagnostics.Debug.Assert(j < m - 1); // make sure j is found
                    bool found = false;
                    foreach (var y in c[i].observations)
                    {
                        if (y.Item1 != p) continue;
                        int g = (l[i].guess * y.Item2 - 1) / x.Item2 + 1;
                        p.guess = g;
                        h.guess = g;
                        found = true;
                        break;
                    }
                    if (!found) return -1;
                }
            }
            return m == t ? n : -1;
        }

        public int ResetCounts(bool resetSelected = false) // returns deepest level
        {
            if (resetSelected) selected = true;
            guess = -1;
            correct = false;
            cut = false;
            guesser = false;
            deepest = -1;
            weight = 0;
            cumulativeAnonymity = 0;
            int m = level;
            foreach (var h in children) m = Math.Max(m, h.ResetCounts(resetSelected));
            return selected ? m : 0;
        }
    }
}