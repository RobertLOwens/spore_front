using System;

namespace Sporefront.Models
{
    /// <summary>
    /// Tracks active poison damage-over-time on an army.
    /// Applied by Amanita Muscaria's Toxic Strikes faction ability.
    /// </summary>
    [System.Serializable]
    public class PoisonState
    {
        public double damagePerTick;      // DPS applied each combat engine tick
        public double remainingDuration;  // seconds remaining
        public int stacks;                // for ToxinAccumulation research (max 3)
        public Guid sourcePlayerID;       // player who applied the poison

        public PoisonState(double damagePerTick, double duration, Guid sourcePlayerID, int stacks = 1)
        {
            this.damagePerTick = damagePerTick;
            this.remainingDuration = duration;
            this.sourcePlayerID = sourcePlayerID;
            this.stacks = stacks;
        }

        /// <summary>
        /// Total effective DPS accounting for stacks.
        /// </summary>
        public double EffectiveDamagePerTick
        {
            get { return damagePerTick * stacks; }
        }
    }
}
