using LiveSplit.Model;
using LiveSplit.UI.Components;
using System;

[assembly:ComponentFactory(typeof(LiveSplit.PersonalRecord.UI.Components.PersonalRecordFactory))]

namespace LiveSplit.PersonalRecord.UI.Components
{
    public class PersonalRecordFactory : IComponentFactory
    {
        public string ComponentName => "Personal Record";

        public string Description => "Shows your personal record for the run";

        public ComponentCategory Category => ComponentCategory.Information;

        public IComponent Create(LiveSplitState state) => new PersonalRecordComponent(state);

        public string UpdateName => ComponentName;

        public string XMLURL => "http://livesplit.org/update/Components/update.LiveSplit.WorldRecord.xml";

        public string UpdateURL => "http://livesplit.org/update/";

        public Version Version => Version.Parse("1.7.5");
    }
}
