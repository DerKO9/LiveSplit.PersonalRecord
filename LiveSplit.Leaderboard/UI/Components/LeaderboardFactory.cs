using LiveSplit.Model;
using LiveSplit.UI.Components;
using System;

[assembly:ComponentFactory(typeof(LiveSplit.Leaderboard.UI.Components.LeaderboardFactory))]

namespace LiveSplit.Leaderboard.UI.Components
{
    public class LeaderboardFactory : IComponentFactory
    {
        public string ComponentName => "Leaderboard";

        public string Description => "Shows the Leaderboard for the run";

        public ComponentCategory Category => ComponentCategory.Information;

        public IComponent Create(LiveSplitState state) => new LeaderboardComponent(state);

        public string UpdateName => ComponentName;

        public string XMLURL => "http://livesplit.org/update/Components/update.LiveSplit.WorldRecord.xml";

        public string UpdateURL => "http://livesplit.org/update/";

        public Version Version => Version.Parse("1.7.5");
    }
}
