using Enums;

namespace EnumUtils
{
    public static class GameModesUtils
    {
        public static string GetDisplayName(this IOGameMode mode)
        {
            return mode switch
            {
                IOGameMode.FreeForAll => "Free For All",
                IOGameMode.TwoTeams => "Two Teams",
                _ => mode.ToString()
            };
        }

        public static string GetSessionSuffix(this IOGameMode mode)
        {
            return mode == IOGameMode.FreeForAll
                ? "ffa"
                : "teams";
        }
    }
}
