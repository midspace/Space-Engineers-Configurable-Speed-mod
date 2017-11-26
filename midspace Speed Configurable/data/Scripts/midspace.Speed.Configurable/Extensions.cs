namespace midspace.Speed.ConfigurableScript
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Sandbox.ModAPI;
    using VRage.Game;
    using VRage.Game.ModAPI;

    public static class Extensions
    {
        /// <summary>
        /// Determines if the player is an Administrator of the active game session.
        /// </summary>
        /// <param name="player"></param>
        /// <returns>True if is specified player is an Administrator in the active game.</returns>
        public static bool IsAdmin(this IMyPlayer player)
        {
            // Offline mode. You are the only player.
            if (MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE)
                return true;

            // Hosted game, and the player is hosting the server.
            if (MyAPIGateway.Multiplayer.IsServerPlayer(player.Client))
                return true;

            return player.PromoteLevel == MyPromoteLevel.Owner ||  // 5 star
                player.PromoteLevel == MyPromoteLevel.Admin ||     // 4 star
                player.PromoteLevel == MyPromoteLevel.SpaceMaster; // 3 star
        }

        public static IMyPlayer FindPlayerBySteamId(this IMyPlayerCollection collection, ulong steamId)
        {
            var listplayers = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(listplayers, p => p.SteamUserId == steamId);
            return listplayers.FirstOrDefault();
        }

        public static void ShowMessage(this IMyUtilities utilities, string sender, string messageText, params object[] args)
        {
            utilities.ShowMessage(sender, string.Format(messageText, args));
        }

        public static bool TryWordParseBool(this string value, out bool result)
        {
            bool boolTest;
            if (bool.TryParse(value, out boolTest))
            {
                result = boolTest;
                return true;
            }

            if (value.Equals("on", StringComparison.InvariantCultureIgnoreCase) || value.Equals("yes", StringComparison.InvariantCultureIgnoreCase) || value.Equals("1", StringComparison.InvariantCultureIgnoreCase))
            {
                result = true;
                return true;
            }

            if (value.Equals("off", StringComparison.InvariantCultureIgnoreCase) || value.Equals("no", StringComparison.InvariantCultureIgnoreCase) || value.Equals("0", StringComparison.InvariantCultureIgnoreCase))
            {
                result = false;
                return true;
            }

            result = false;
            return false;
        }
    }
}