using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.TimeFormatters;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.Web.Share;
using SpeedrunComSharp;

namespace LiveSplit.PersonalRecord.UI.Components
{
    public class PersonalRecordComponent : IComponent
    {
        protected InfoTextComponent InternalComponent { get; set; }
        protected SimpleLabel SimpleLabel { get; set; }
        protected SimpleLabel Place { get; set; }
        protected SimpleLabel User { get; set; }
        protected SimpleLabel Time { get; set; }

        protected PersonalRecordSettings Settings { get; set; }

        private GraphicsCache Cache { get; set; }
        private ITimeFormatter TimeFormatter { get; set; }
        private RegularTimeFormatter LocalTimeFormatter { get; set; }
        private LiveSplitState State { get; set; }
        private Image TrophyIcon { get; set; }
        private Image CountryFlagIcon { get; set; }
        private Image UserIcon { get; set; }
        private TimeStamp LastUpdate { get; set; }
        private TimeSpan RefreshInterval { get; set; }
        public Record PersonalRecord { get; protected set; }
        private bool IsLoading { get; set; }
        private SpeedrunComClient Client { get; set; }

        public string ComponentName => "Personal Record";

        public float PaddingTop => InternalComponent.PaddingTop;
        public float PaddingLeft => InternalComponent.PaddingLeft;
        public float PaddingBottom => InternalComponent.PaddingBottom;
        public float PaddingRight => InternalComponent.PaddingRight;

        public float VerticalHeight => InternalComponent.VerticalHeight;
        public float MinimumWidth => InternalComponent.MinimumWidth;
        public float HorizontalWidth => InternalComponent.HorizontalWidth;
        public float MinimumHeight => InternalComponent.MinimumHeight;

        public IDictionary<string, Action> ContextMenuControls => null;

        public PersonalRecordComponent(LiveSplitState state)
        {
            State = state;

            Client = new SpeedrunComClient(userAgent: Updates.UpdateHelper.UserAgent, maxCacheElements: 0);

            RefreshInterval = TimeSpan.FromMinutes(5);
            Cache = new GraphicsCache();
            TimeFormatter = new AutomaticPrecisionTimeFormatter();
            LocalTimeFormatter = new RegularTimeFormatter();
            InternalComponent = new InfoTextComponent("PersonalRecord", TimeFormatConstants.DASH);
            Settings = new PersonalRecordSettings()
            {
                CurrentState = state
            };
        }

        public void Dispose()
        {
        }

        private void RefreshPersonalRecord()
        {
            LastUpdate = TimeStamp.Now;

            PersonalRecord = null;

            try
            {
                if (State != null && State.Run != null
                    && State.Run.Metadata.Game != null && State.Run.Metadata.Category != null)
                {
                    var variableFilter = Settings.FilterVariables ? State.Run.Metadata.VariableValues.Values : null;
                    var regionFilter = Settings.FilterRegion && State.Run.Metadata.Region != null ? State.Run.Metadata.Region.ID : null;
                    var platformFilter = Settings.FilterPlatform && State.Run.Metadata.Platform != null ? State.Run.Metadata.Platform.ID : null;
                    EmulatorsFilter emulatorFilter = EmulatorsFilter.NotSet;
                    if (Settings.FilterPlatform)
                    {
                        if (State.Run.Metadata.UsesEmulator)
                            emulatorFilter = EmulatorsFilter.OnlyEmulators;
                        else
                            emulatorFilter = EmulatorsFilter.NoEmulators;
                    }

                    var leaderboard = Client.Leaderboards.GetLeaderboardForFullGameCategory(State.Run.Metadata.Game.ID, State.Run.Metadata.Category.ID, 
                        platformId: platformFilter, regionId: regionFilter, 
                        emulatorsFilter: emulatorFilter, variableFilters: variableFilter);

                    if (leaderboard != null)
                    {
                        PersonalRecord = leaderboard.Records.Where(r => r.Player.Name.ToLower() == "derko".ToLower()).FirstOrDefault(); 
                        TrophyIcon = GetTrophyIcon();
                        CountryFlagIcon = GetCountryFlagIcon();
                        UserIcon = GetUserIcon();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            IsLoading = false;
            ShowPersonalRecord(State.Layout.Mode);
        }

        private Image GetTrophyIcon()
        {
            if (PersonalRecord != null)
            {
                Uri uri;
                switch (PersonalRecord.Rank)
                {
                    case 1:
                        uri = PersonalRecord.Game.Assets.TrophyFirstPlace.Uri;
                        break;
                    case 2:
                        uri = PersonalRecord.Game.Assets.TrophySecondPlace.Uri;
                        break;
                    case 3:
                        uri = PersonalRecord.Game.Assets.TrophyThirdPlace.Uri;
                        break;
                    case 4:
                        uri = PersonalRecord.Game.Assets.TrophyFourthPlace.Uri;
                        break;
                    default:
                        return null;
                }

                return GetImage(uri);
            }
            else
            {
                return null;
            }
        }

        private Image GetCountryFlagIcon()
        {
            if (PersonalRecord != null)
            {
                var countryCode = PersonalRecord.Player.User.Location.Country.Code;
                var uri = new Uri($"https://www.speedrun.com/images/flags/{countryCode}.png");
                return GetImage(uri);
            }
            else
            {
                return null;
            }
        }

        private Image GetUserIcon()
        {
            if (PersonalRecord != null)
            {
                var userName = PersonalRecord.Player.User.Name;
                var uri = new Uri($"https://www.speedrun.com/themes/user/{userName}/icon.png");
                return GetImage(uri);
            }
            else
            {
                return null;
            }
        }

        private Image GetImage(Uri uri)
        {
            using (var wc = new WebClient())
            {
                try
                {
                    return Image.FromStream(wc.OpenRead(uri));
                }
                catch (Exception e)
                {
                    return null;
                }
            };
        }

        private void ShowPersonalRecord(LayoutMode mode)
        {
            var centeredText = Settings.CenteredText && !Settings.Display2Rows && mode == LayoutMode.Vertical;
            if (PersonalRecord != null)
            {
                var time = PersonalRecord.Times.Primary;
                var timingMethod = State.CurrentTimingMethod;
                var game = State.Run.Metadata.Game;
                if (game != null)
                {
                    timingMethod = game.Ruleset.DefaultTimingMethod.ToLiveSplitTimingMethod();
                    LocalTimeFormatter.Accuracy = game.Ruleset.ShowMilliseconds ? TimeAccuracy.Hundredths : TimeAccuracy.Seconds;
                }

                var formatted = TimeFormatter.Format(time);
                var isLoggedIn = SpeedrunCom.Client.IsAccessTokenValid;
                var userName = string.Empty;
                if (isLoggedIn)
                    userName = SpeedrunCom.Client.Profile.Name;

                var runners = string.Join(", ", PersonalRecord.Players.Select(p => p.Name));

                var finalTime = GetPBTime(timingMethod);
                if (IsPBTimeLower(finalTime, time, game != null ? game.Ruleset.ShowMilliseconds : false))
                {
                    formatted = LocalTimeFormatter.Format(finalTime);
                    runners = State.Run.Metadata.Category.Players.Value > 1 ? "us" : "me";
                }

                if (centeredText)
                {
                    var textList = new List<string>();

                    textList.Add(string.Format("Personal Record is {0} by {1}", formatted, runners));
                    textList.Add(string.Format("Personal Record: {0} by {1}", formatted, runners));
                    textList.Add(string.Format("PR: {0} by {1}", formatted, runners));
                    textList.Add(string.Format("PR is {0} by {1}", formatted, runners));

                    InternalComponent.InformationName = textList.First();
                    InternalComponent.AlternateNameText = textList;
                }
                else
                {
                    InternalComponent.InformationValue = string.Format("{0} by {1}", formatted, runners);
                }
            }
            else if (IsLoading)
            {
                if (centeredText)
                {
                    InternalComponent.InformationName = "Loading Personal Record...";
                    InternalComponent.AlternateNameText = new[] { "Loading PR..." };
                }
                else
                {
                    InternalComponent.InformationValue = "Loading...";
                }
            }
            else
            {
                if (centeredText)
                {
                    InternalComponent.InformationName = "Unknown Personal Record";
                    InternalComponent.AlternateNameText = new[] { "Unknown PR" };
                }
                else
                {
                    InternalComponent.InformationValue = TimeFormatConstants.DASH;
                }
            }
        }

        private bool IsPBTimeLower(TimeSpan? pbTime, TimeSpan? recordTime, bool showMillis)
        {
            if (pbTime == null || recordTime == null)
                return false;
            if (showMillis)
                return (int)pbTime.Value.TotalMilliseconds <= (int)recordTime.Value.TotalMilliseconds;
            return (int)pbTime.Value.TotalSeconds <= (int)recordTime.Value.TotalSeconds;
        }

        private TimeSpan? GetPBTime(Model.TimingMethod method)
        {
            var lastSplit = State.Run.Last();
            var pbTime = lastSplit.PersonalBestSplitTime[method];
            var splitTime = lastSplit.SplitTime[method];

            if (State.CurrentPhase == TimerPhase.Ended && splitTime < pbTime)
                return splitTime;
            return pbTime;
        }

        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
        {
            Cache.Restart();
            Cache["Game"] = state.Run.GameName;
            Cache["Category"] = state.Run.CategoryName;
            Cache["PlatformID"] = Settings.FilterPlatform ? state.Run.Metadata.PlatformName : null;
            Cache["RegionID"] = Settings.FilterRegion ? state.Run.Metadata.RegionName : null;
            Cache["UsesEmulator"] = Settings.FilterPlatform ? (bool?)state.Run.Metadata.UsesEmulator : null;
            Cache["Variables"] = Settings.FilterVariables ? string.Join(",", state.Run.Metadata.VariableValueNames.Values) : null;

            if (Cache.HasChanged)
            {
                IsLoading = true;
                PersonalRecord = null;
                ShowPersonalRecord(mode);
                Task.Factory.StartNew(RefreshPersonalRecord);
            }
            else if (LastUpdate != null && TimeStamp.Now - LastUpdate >= RefreshInterval)
            {
                Task.Factory.StartNew(RefreshPersonalRecord);
            }
            else
            {
                Cache["CenteredText"] = Settings.CenteredText && !Settings.Display2Rows && mode == LayoutMode.Vertical;
                Cache["RealPBTime"] = GetPBTime(Model.TimingMethod.RealTime);
                Cache["GamePBTime"] = GetPBTime(Model.TimingMethod.GameTime);

                if (Cache.HasChanged)
                {
                    ShowPersonalRecord(mode);
                }
            }

            InternalComponent.Update(invalidator, state, width, height, mode);
        }

        private void DrawBackground(Graphics g, LiveSplitState state, float width, float height)
        {
            if (Settings.BackgroundColor.A > 0
                || Settings.BackgroundGradient != GradientType.Plain
                && Settings.BackgroundColor2.A > 0)
            {
                var gradientBrush = new LinearGradientBrush(
                            new PointF(0, 0),
                            Settings.BackgroundGradient == GradientType.Horizontal
                            ? new PointF(width, 0)
                            : new PointF(0, height),
                            Settings.BackgroundColor,
                            Settings.BackgroundGradient == GradientType.Plain
                            ? Settings.BackgroundColor
                            : Settings.BackgroundColor2);
                g.FillRectangle(gradientBrush, 0, 0, width, height);
            }
        }

        private void PrepareDraw(LiveSplitState state, LayoutMode mode)
        {
            InternalComponent.DisplayTwoRows = Settings.Display2Rows;

            InternalComponent.NameLabel.HasShadow
                = InternalComponent.ValueLabel.HasShadow
                = state.LayoutSettings.DropShadows;

            if (Settings.CenteredText && !Settings.Display2Rows && mode == LayoutMode.Vertical)
            {
                InternalComponent.NameLabel.HorizontalAlignment = StringAlignment.Center;
                InternalComponent.ValueLabel.HorizontalAlignment = StringAlignment.Center;
                InternalComponent.NameLabel.VerticalAlignment = StringAlignment.Center;
                InternalComponent.ValueLabel.VerticalAlignment = StringAlignment.Center;
                InternalComponent.InformationValue = "";
            }
            else
            {
                InternalComponent.InformationName = "Personal Record";
                InternalComponent.AlternateNameText = new[]
                {
                    "PR"
                };
                InternalComponent.NameLabel.HorizontalAlignment = StringAlignment.Near;
                InternalComponent.ValueLabel.HorizontalAlignment = StringAlignment.Far;
                InternalComponent.NameLabel.VerticalAlignment =
                    mode == LayoutMode.Horizontal || Settings.Display2Rows ? StringAlignment.Near : StringAlignment.Center;
                InternalComponent.ValueLabel.VerticalAlignment =
                    mode == LayoutMode.Horizontal || Settings.Display2Rows ? StringAlignment.Far : StringAlignment.Center;
            }

            InternalComponent.NameLabel.ForeColor = Settings.OverrideTextColor ? Settings.TextColor : state.LayoutSettings.TextColor;
            InternalComponent.ValueLabel.ForeColor = Settings.OverrideTimeColor ? Settings.TimeColor : state.LayoutSettings.TextColor;
        }

        public void DrawHorizontal(Graphics g, LiveSplitState state, float height, System.Drawing.Region clipRegion)
        {
            DrawBackground(g, state, HorizontalWidth, height);
            PrepareDraw(state, LayoutMode.Horizontal);
            InternalComponent.DrawHorizontal(g, state, height, clipRegion);
            g.DrawEllipse(new Pen(Color.Aqua), new Rectangle(new Point(10,0), new Size((int)HorizontalWidth - 10, (int)height)));
            if (UserIcon != null) g.DrawImage(UserIcon, new Point(10, (int)height/2 - 50));
        }

        public void DrawVertical(Graphics g, LiveSplitState state, float width, System.Drawing.Region clipRegion)
        {
            DrawBackground(g, state, width, VerticalHeight);
            PrepareDraw(state, LayoutMode.Vertical);
            //InternalComponent.DrawVertical(g, state, width, clipRegion);

            if (TrophyIcon != null) g.DrawImage(TrophyIcon, 10, VerticalHeight / 2 - 10, 20, 20);

            SimpleLabel = new SimpleLabel();
            SimpleLabel.Text = "1st";
            SimpleLabel.HorizontalAlignment = StringAlignment.Near;
            SimpleLabel.VerticalAlignment = StringAlignment.Center;
            SimpleLabel.Font = state.LayoutSettings.TextFont;
            SimpleLabel.ForeColor = state.LayoutSettings.TextColor;
            SimpleLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
            SimpleLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
            SimpleLabel.Width = width - 10;
            SimpleLabel.Height = VerticalHeight;
            SimpleLabel.X = 30;
            SimpleLabel.Y = 0;
            SimpleLabel.Draw(g);

            if (CountryFlagIcon != null) g.DrawImage(CountryFlagIcon, width/2 - 50, VerticalHeight/2 - (0.33f * CountryFlagIcon.Height)/2, 0.33f * CountryFlagIcon.Width, 0.33f * CountryFlagIcon.Height);

            SimpleLabel = new SimpleLabel();
            SimpleLabel.Text = "hedweg";
            SimpleLabel.HorizontalAlignment = StringAlignment.Center;
            SimpleLabel.VerticalAlignment = StringAlignment.Center;
            SimpleLabel.Font = state.LayoutSettings.TextFont;
            SimpleLabel.ForeColor = state.LayoutSettings.TextColor;
            SimpleLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
            SimpleLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
            SimpleLabel.Width = width - 10;
            SimpleLabel.Height = VerticalHeight;
            SimpleLabel.X = 10;
            SimpleLabel.Y = 0;
            SimpleLabel.Draw(g);

            SimpleLabel = new SimpleLabel();
            SimpleLabel.Text = "42:01";
            SimpleLabel.HorizontalAlignment = StringAlignment.Far;
            SimpleLabel.VerticalAlignment = StringAlignment.Center;
            SimpleLabel.Font = state.LayoutSettings.TextFont;
            SimpleLabel.ForeColor = state.LayoutSettings.TextColor;
            SimpleLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
            SimpleLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
            SimpleLabel.Width = width - 10;
            SimpleLabel.Height = VerticalHeight;
            SimpleLabel.X = -10;
            SimpleLabel.Y = 0;
            SimpleLabel.Draw(g);
        }

        public Control GetSettingsControl(LayoutMode mode)
        {
            Settings.Mode = mode;
            return Settings;
        }

        public XmlNode GetSettings(XmlDocument document)
        {
            return Settings.GetSettings(document);
        }

        public void SetSettings(XmlNode settings)
        {
            Settings.SetSettings(settings);
        }

        public int GetSettingsHashCode() => Settings.GetSettingsHashCode();
    }
}
