using System.Diagnostics;
using Enums;

namespace EnumUtils
{
    public static class GameModesUtils
    {
        public static string GetDisplayName(this IOGameMode mode) => 
            mode switch
            {
                IOGameMode.Any => "All Game Modes",
                IOGameMode.FreeForAll => "Fun Mode",
                IOGameMode.TwoTeams => "Boring Mode"
            };
    }
}