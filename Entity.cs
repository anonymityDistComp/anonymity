using System;
using System.Collections.Generic;

namespace Anonimity
{
    public class Entity
    {
        public readonly int input;

        public HistoryTree history = new HistoryTree();
        private HistoryTree current; // current node in history tree
        private List<Tuple<HistoryTree, int>> newObservations = new List<Tuple<HistoryTree, int>>();

        public Entity(int input)
        {
            this.input = input;
            current = history;
            ExtendHistory();
        }

        public bool Leader() => current.input == 0;

        public Entity Copy() // assumes newObservations to be empty
        {
            Entity e = new Entity(input);
            (e.history, e.current) = history.Copy();
            return e;
        }

        private void ExtendHistory() => current = current.AddChild(input);

        public int EndRound(bool count) // returns (correct) guess on n
        {
            ExtendHistory();
            foreach (Tuple<HistoryTree, int> x in newObservations)
                current.AddObservation(history.Merge(x.Item1).Item1, x.Item2);
            newObservations = new List<Tuple<HistoryTree, int>>();
            return count ? history.CountAgents(true) : -1;
        }

        public void Observe(Entity e, int multiplicity = 1) => newObservations.Add(new Tuple<HistoryTree, int>(e.history.Copy().Item1, multiplicity));
    }
}