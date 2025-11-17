using System.Collections.Generic;

namespace MarketScanner.Core.Classification
{
    public static class SicSectorMap
    {
        public static readonly Dictionary<string, string> Map = new()
        {
            // --- Agriculture, Forestry, Fishing (01–09)
            {"01", "Agriculture"},
            {"02", "Agriculture"},
            {"07", "Agriculture Services"},
            {"08", "Forestry"},
            {"09", "Fishing"},

            // --- Mining (10–14)
            {"10", "Metal Mining"},
            {"12", "Coal Mining"},
            {"13", "Oil & Gas"},
            {"14", "Nonmetallic Mining"},

            // --- Construction (15–17)
            {"15", "Construction"},
            {"16", "Construction"},
            {"17", "Construction"},

            // --- Manufacturing (20–39)
            {"20", "Food Manufacturing"},
            {"21", "Tobacco"},
            {"22", "Textiles"},
            {"23", "Apparel"},
            {"24", "Lumber & Wood"},
            {"25", "Furniture"},
            {"26", "Paper Products"},
            {"27", "Printing & Publishing"},
            {"28", "Chemicals"},
            {"29", "Petroleum Refining"},
            {"30", "Rubber & Plastics"},
            {"31", "Leather"},
            {"32", "Stone, Clay & Glass"},
            {"33", "Primary Metals"},
            {"34", "Fabricated Metals"},
            {"35", "Industrial Machinery"},
            {"36", "Electronics"},
            {"37", "Transportation Equipment"},
            {"38", "Instruments & Measuring"},
            {"39", "Misc Manufacturing"},

            // --- Transportation & Communications (40–49)
            {"40", "Railroads"},
            {"41", "Local Transit"},
            {"42", "Trucking & Warehousing"},
            {"43", "Postal Services"},
            {"44", "Water Transport"},
            {"45", "Air Transport"},
            {"46", "Pipelines"},
            {"47", "Freight Forwarding"},
            {"48", "Communications"},
            {"49", "Electric, Gas & Water"},

            // --- Wholesale Trade (50–51)
            {"50", "Wholesale Trade"},
            {"51", "Wholesale Trade"},

            // --- Retail Trade (52–59)
            {"52", "Retail"},
            {"53", "Retail"},
            {"54", "Food Stores"},
            {"55", "Automotive Dealers"},
            {"56", "Apparel Stores"},
            {"57", "Furniture Stores"},
            {"58", "Eating & Drinking"},
            {"59", "Misc Retail"},

            // --- Finance, Insurance, Real Estate (60–67)
            {"60", "Banking"},
            {"61", "Nondepository Finance"},
            {"62", "Security Brokers"},
            {"63", "Insurance"},
            {"64", "Insurance Agents"},
            {"65", "Real Estate"},
            {"67", "Holding Companies"},

            // --- Services (70–89)
            {"70", "Hotels"},
            {"72", "Personal Services"},
            {"73", "Business Services"},     // Many tech companies fall here
            {"75", "Auto Repair"},
            {"76", "Misc Repair"},
            {"78", "Motion Pictures"},
            {"79", "Entertainment"},
            {"80", "Health Services"},
            {"81", "Legal Services"},
            {"82", "Education"},
            {"83", "Social Services"},
            {"84", "Museums"},
            {"86", "Membership Orgs"},
            {"87", "Engineering & Management"},
            {"88", "Private Households"},
            {"89", "Misc Services"},

            // --- Public Administration (91–99)
            {"91", "Executive Offices"},
            {"92", "Justice & Public Order"},
            {"93", "Finance & Taxation"},
            {"94", "Administration"},
            {"95", "Environmental Quality"},
            {"96", "Housing & Urban Dev"},
            {"97", "International Affairs"},
            {"99", "Nonclassifiable"},
        };

        /// <summary>
        /// Helper to map a full SIC code like "3571" to major group "35" then sector.
        /// </summary>
        public static string GetSector(string? sic)
        {
            if (string.IsNullOrWhiteSpace(sic))
                return "Unknown";

            // Take first 2 digits: major SIC group
            var major = sic.Length >= 2 ? sic.Substring(0, 2) : sic;

            return Map.TryGetValue(major, out var sector) ? sector : "Unknown";
        }
    }
}
