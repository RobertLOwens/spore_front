// ============================================================================
// FILE: Data/EnumMappings.cs
// PURPOSE: Pre-cached enum ToString() lookups and cross-enum mappings to avoid
//          repeated allocations in hot paths (engine update loops)
// ============================================================================

using System.Collections.Generic;
using Sporefront.Models;

namespace Sporefront.Data
{
    /// <summary>
    /// Static lookup tables for enum string conversions and cross-enum mappings.
    /// Eliminates per-frame ToString() allocations in engine update loops.
    /// </summary>
    public static class EnumMappings
    {
        /// <summary>
        /// Maps ResourcePointType to the corresponding ResearchBonusType for gathering rate bonuses.
        /// Only resource types that have a research bonus are included.
        /// </summary>
        public static readonly Dictionary<ResourcePointType, ResearchBonusType> ResourceToResearchBonus =
            new Dictionary<ResourcePointType, ResearchBonusType>
            {
                { ResourcePointType.Farmland, ResearchBonusType.FarmGatheringRate },
                { ResourcePointType.Trees, ResearchBonusType.LumberCampGatheringRate },
                { ResourcePointType.OreMine, ResearchBonusType.MiningCampGatheringRate },
                { ResourcePointType.StoneQuarry, ResearchBonusType.MiningCampGatheringRate }
            };
    }
}
