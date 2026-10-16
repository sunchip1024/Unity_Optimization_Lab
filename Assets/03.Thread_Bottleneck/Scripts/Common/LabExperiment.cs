using System.Diagnostics;
using UnityEngine;
namespace OptimizationLab
{
    public abstract class LabExperiment : MonoBehaviour
    {
        [Min(0)] public float activationTime = 3f;
        public abstract string Description { get; }
        public virtual bool DrivesOrbit => false;
        public double LastWorkMs { get; private set; }
        public virtual void Initialize(SurvivorGame game) { }
        public void Step(SurvivorGame game)
        {
            if (game.Elapsed < activationTime) return;
            long start = Stopwatch.GetTimestamp();
            Tick(game);
            LastWorkMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }
        protected abstract void Tick(SurvivorGame game);
        public virtual void ResetExperiment() { }
    }
}
