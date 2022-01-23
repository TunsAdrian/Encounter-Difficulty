using Newtonsoft.Json;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace encounter_difficulty
{
    public sealed partial class MainPage : Page
    {
        private int pageSize = 10;
        private int pageIndex = -1;
        private int partySize = 1;
        private int partyLevel = 1;
        private List<string> monsterNameList;
        private List<SimpleMonster> mainMonsterList;
        private Dictionary<int, double> multipleMonstersXPModifier;
        private Dictionary<int, Dictionary<string, int>> experienceThresholdByCharacterLevel;
        private readonly List<string> noResultFound = new List<string>(1) { "No such monster found" };
        private readonly ObservableCollection<int> pageSizeOptions = new ObservableCollection<int>();
        private readonly ObservableCollection<int> partySizeCollection = new ObservableCollection<int>();
        private readonly ObservableCollection<int> partyMembersLevelCollection = new ObservableCollection<int>();
        private readonly ObservableCollection<FullMonsterDetails> displayMonsterList = new ObservableCollection<FullMonsterDetails>();

        public MainPage()
        {
            InitializeComponent();
            InitApplication();
        }

        // Initialization section
        private void InitApplication()
        {
            InitExpTresholdDictionary();
            InitMultipleMonstersXPModifier();
            InitComboBox();
            AsyncContext.Run(LoadAppState);
            AsyncContext.Run(Get_All_Monsters);
        }

        // Save/Load application state section
        private async Task LoadAppState()
        {
            ApplicationDataContainer roamingSettings = ApplicationData.Current.RoamingSettings;
            ApplicationDataCompositeValue composite = (ApplicationDataCompositeValue)roamingSettings.Values["RoamingAppState"];

            if (composite != null)
            {
                partySize = (int)composite["PartySize"];
                partyLevel = (int)composite["PartyLevel"];
                pageSize = (int)composite["PageSize"];
            }

            // Preselect the ComboBox selected items
            PartySize.SelectedItem = partySize;
            PartyMembersLevel.SelectedItem = partyLevel;
            PageSize.SelectedItem = pageSize;

            try
            {
                StorageFolder applicationFolder = ApplicationData.Current.LocalFolder;
                StorageFile lastEncounterFile = await applicationFolder.GetFileAsync("last_encounter.txt");
                var lastEncounterMonsters = await FileIO.ReadTextAsync(lastEncounterFile);
                dynamic deserializedMonsterList = JsonConvert.DeserializeObject(lastEncounterMonsters);

                foreach (var monster in deserializedMonsterList)
                {
                    MonsterEncounterList.Items.Add(new FullMonsterDetails(monster));
                }

                if (MonsterEncounterList.Items.Count > 0)
                {
                    ApplicationHelpText.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                // if the file does not exist just skip this step
            }

            ComputeEncounterDifficulty();
        }

        private async Task SaveAppState()
        {
            ApplicationDataContainer roamingSettings = ApplicationData.Current.RoamingSettings;
            ApplicationDataCompositeValue composite = new ApplicationDataCompositeValue
            {
                ["PartySize"] = partySize,
                ["PartyLevel"] = partyLevel,
                ["PageSize"] = pageSize
            };

            roamingSettings.Values["RoamingAppState"] = composite;

            if (MonsterEncounterList.Items.Count >= 0)
            {
                StorageFile lastEncounterFile = await ApplicationData.Current.LocalFolder.CreateFileAsync("last_encounter.txt", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(lastEncounterFile, JsonConvert.SerializeObject(MonsterEncounterList.Items));
            }
        }

        // Text update section
        private void UpdatePartyExpTextblocks(Dictionary<string, int> partyExpThreshold)
        {
            EasyEncounterExp.Text = "Easy: " + partyExpThreshold["Easy"].ToString("N0") + " exp";
            MediumEncounterExp.Text = "Medium: " + partyExpThreshold["Medium"].ToString("N0") + " exp";
            HardEncounterExp.Text = "Hard: " + partyExpThreshold["Hard"].ToString("N0") + " exp";
            DeadlyEncounterExp.Text = "Deadly: " + partyExpThreshold["Deadly"].ToString("N0") + " exp";
        }

        private void UpdateEncounterExpTextblocks(int totalExperience, int totalExperienceAdjusted, string difficulty)
        {
            TotalXP.Text = "Total XP: " + totalExperience.ToString("N0") + " exp";
            PlayerXP.Text = "(" + (totalExperience / partySize).ToString("N0") + " per player)";
            TotalAdjustedXP.Text = "Adjusted XP: " + totalExperienceAdjusted.ToString("N0") + " exp";
            PlayerAdjustedXP.Text = "(" + (totalExperienceAdjusted / partySize).ToString("N0") + " per player)";
            EncounterDifficulty.Text = "Difficulty: " + difficulty;
        }

        // Encounter difficulty computing section
        // Rules for computing the encounter difficulty were taken from the official source: https://www.dndbeyond.com/sources/basic-rules/building-combat-encounters
        private void ComputeEncounterDifficulty()
        {
            int totalExperience = 0;
            int totalExperienceAdjusted = 0;
            int monsterEncounterNumber = MonsterEncounterList.Items.Count;
            int multipleMonstersXPModifierKey = monsterEncounterNumber > 15 ? 15 : monsterEncounterNumber; // ensure that the dictionary key won't be above the max
            string encounterDifficuly = "";

            Dictionary<string, int> partyExpThreshold = new Dictionary<string, int> {
                { "Easy", partySize * experienceThresholdByCharacterLevel[partyLevel]["easy"] },
                { "Medium", partySize * experienceThresholdByCharacterLevel[partyLevel]["medium"] },
                { "Hard", partySize * experienceThresholdByCharacterLevel[partyLevel]["hard"] },
                { "Deadly", partySize * experienceThresholdByCharacterLevel[partyLevel]["deadly"] },
            };

            if (monsterEncounterNumber > 0)
            {
                ApplicationHelpText.Visibility = Visibility.Collapsed;

                foreach (FullMonsterDetails monster in MonsterEncounterList.Items)
                {
                    totalExperience += int.Parse(monster.XP);
                }

                totalExperienceAdjusted = (int)(totalExperience * multipleMonstersXPModifier[multipleMonstersXPModifierKey]);

                int closestPartyExpThreshold = partyExpThreshold.Values.OrderBy(item => Math.Abs(totalExperienceAdjusted - item)).First();
                encounterDifficuly = partyExpThreshold.FirstOrDefault(x => x.Value == closestPartyExpThreshold).Key;
            }
            else
            {
                ApplicationHelpText.Visibility = Visibility.Visible;
            }

            UpdatePartyExpTextblocks(partyExpThreshold);
            UpdateEncounterExpTextblocks(totalExperience, totalExperienceAdjusted, encounterDifficuly);
            AsyncContext.Run(SaveAppState);
        }

        // List navigation section
        // When awaiting for the displayed monsters list, disable the options that could start this event again
        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            pageIndex++;

            List<SimpleMonster> simpleMonsters = mainMonsterList.Skip(pageIndex * pageSize).Take(pageSize).ToList();

            NextButton.IsEnabled = PreviousButton.IsEnabled = PageSize.IsEnabled = false;

            await Display_Monsters(simpleMonsters);

            NextButton.IsEnabled = PreviousButton.IsEnabled = PageSize.IsEnabled = true;
        }

        private async void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            pageIndex--;

            List<SimpleMonster> simpleMonsters = mainMonsterList.Skip(pageIndex * pageSize).Take(pageSize).ToList();

            NextButton.IsEnabled = PreviousButton.IsEnabled = PageSize.IsEnabled = false;

            await Display_Monsters(simpleMonsters);

            NextButton.IsEnabled = PreviousButton.IsEnabled = PageSize.IsEnabled = true;
        }

        // Combobox selection events section
        private void PartySize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            partySize = (int)e.AddedItems[0];

            ComputeEncounterDifficulty();
        }

        private void PartyMembersLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            partyLevel = (int)e.AddedItems[0];

            ComputeEncounterDifficulty();
        }

        private async void PageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            pageSize = (int)e.AddedItems[0];
            pageIndex = -1;

            // Reload the list and show it from the begining, when changing the displayed monsters number
            if (mainMonsterList != null)
            {
                NextButton_Click(null, null);
            }

            await SaveAppState();
        }

        // Lists item click events section
        private void DisplayMonsterList_ItemClick(object sender, ItemClickEventArgs e)
        {
            MonsterEncounterList.Items.Add(e.ClickedItem);

            ComputeEncounterDifficulty();
        }

        private void MonsterEncounterList_ItemClick(object sender, ItemClickEventArgs e)
        {
            MonsterEncounterList.Items.Remove(e.ClickedItem);

            ComputeEncounterDifficulty();
        }

        // AutoSuggestBox component section
        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var filtered = monsterNameList.Where(monsterName => monsterName.ToLower().Contains(SearchBox.Text.ToLower())).ToList();

                if (filtered.Count > 0)
                {
                    sender.ItemsSource = filtered;
                }
                else
                {
                    sender.ItemsSource = noResultFound;
                }
            }
        }

        private async void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion != null && !args.ChosenSuggestion.Equals(noResultFound.First()))
            {
                List<SimpleMonster> chosenMonster = mainMonsterList.Where(monster => monster.Name.Equals(args.ChosenSuggestion)).ToList();
                if (chosenMonster.Count > 0)
                {
                    await Display_Monsters(chosenMonster);
                    pageIndex = -1;
                }
            }
            else
            {
                if (args.QueryText == "")
                {
                    NextButton_Click(null, null);
                }
            }
        }

        // API call section
        private async Task Display_Monsters(List<SimpleMonster> simpleMonsters)
        {
            displayMonsterList.Clear();
            string requestUrl = "https://www.dnd5eapi.co/api/monsters/";

            try
            {
                foreach (var monster in simpleMonsters)
                {
                    var json = await FetchAsync(requestUrl + monster.Index);
                    displayMonsterList.Add(JsonConvert.DeserializeObject<FullMonsterDetails>(json));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                NoInternetError.Visibility = Visibility.Visible;
                DisplayMonsterList.Visibility = Visibility.Collapsed;
            }
        }

        private async Task Get_All_Monsters()
        {
            string requestUrl = "https://www.dnd5eapi.co/api/monsters";

            try
            {
                var json = await FetchAsync(requestUrl);

                RootSimpleMonsterResult rootObjectData = JsonConvert.DeserializeObject<RootSimpleMonsterResult>(json);
                mainMonsterList = new List<SimpleMonster>(rootObjectData.SimpleMonsters);

                monsterNameList = mainMonsterList.Select(monster => monster.Name).ToList();
                NextButton_Click(null, null);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                NoInternetError.Visibility = Visibility.Visible;
                DisplayMonsterList.Visibility = Visibility.Collapsed;
            }
        }

        private async Task<string> FetchAsync(string url)
        {
            string jsonString;

            using (var httpClient = new System.Net.Http.HttpClient())
            {
                var stream = await httpClient.GetStreamAsync(url);
                StreamReader reader = new StreamReader(stream);
                jsonString = reader.ReadToEnd();
            }

            return jsonString;
        }

        internal class RootSimpleMonsterResult
        {
            [JsonProperty("results")]
            public List<SimpleMonster> SimpleMonsters { get; set; }
        }

        internal class SimpleMonster
        {
            [JsonProperty("index")]
            public string Index { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }
        }

        internal class FullMonsterDetails
        {
            public FullMonsterDetails()
            {
                // empty constructor needed when creating objects directly from JSON
            }

            public FullMonsterDetails(dynamic deserializedMonster)
            {
                Index = (string)deserializedMonster.index;
                Name = (string)deserializedMonster.name;
                CR = (string)deserializedMonster.challenge_rating;
                Size = (string)deserializedMonster.size;
                Type = (string)deserializedMonster.type;
                Alignment = (string)deserializedMonster.alignment;
                XP = (string)deserializedMonster.xp;
            }

            [JsonProperty("index")]
            public string Index { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("challenge_rating")]
            public string CR { get; set; }

            [JsonProperty("size")]
            public string Size { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("alignment")]
            public string Alignment { get; set; }

            [JsonProperty("xp")]
            public string XP { get; set; }
        }

        private void InitComboBox()
        {
            for (int i = 1; i <= 12; i++)
            {
                partySizeCollection.Add(i);
            }

            for (int i = 1; i <= 20; i++)
            {
                partyMembersLevelCollection.Add(i);
            }

            pageSizeOptions.Add(10);
            pageSizeOptions.Add(25);
            pageSizeOptions.Add(50);
            pageSizeOptions.Add(100);
        }

        private void InitMultipleMonstersXPModifier()
        {
            multipleMonstersXPModifier = new Dictionary<int, double>
            {
                {1, 1}, {2, 1.5}, {3, 2}, {4, 2}, {5, 2}, {6, 2}, {7, 2.5}, {8, 2.5},
                {9, 2.5}, {10, 2.5}, {11, 3}, {12, 3}, {13, 3}, {14, 3}, {15, 4}
            };
        }

        private void InitExpTresholdDictionary()
        {
            experienceThresholdByCharacterLevel = new Dictionary<int, Dictionary<string, int>>
            {
                { 1, new Dictionary<string, int> {
                        { "easy", 25 }, { "medium", 50 } , { "hard", 75 }, { "deadly", 100 }
                    }
                },
               { 2, new Dictionary<string, int> {
                        { "easy", 50 }, { "medium", 100 }, { "hard", 150 }, { "deadly", 200 }
                    }
                },
                { 3, new Dictionary<string, int> {
                        { "easy", 75 }, { "medium", 150 }, { "hard", 225 }, { "deadly", 400 }
                    }
                },
                { 4, new Dictionary<string, int> {
                        { "easy", 125 }, { "medium", 250 }, { "hard", 375 }, { "deadly", 500 }
                    }
                },
                { 5, new Dictionary<string, int> {
                        { "easy", 250 }, { "medium", 500 }, { "hard", 750 }, { "deadly", 1100 }
                    }
                },
                { 6, new Dictionary<string, int> {
                        { "easy", 300 }, { "medium", 600 }, { "hard", 900 }, { "deadly", 1400 }
                    }
                },
                { 7, new Dictionary<string, int> {
                        { "easy", 350 }, { "medium", 750 }, { "hard", 1100 }, { "deadly", 1700 }
                    }
                },
                { 8, new Dictionary<string, int> {
                        { "easy", 450 }, { "medium", 900 }, { "hard", 1400 }, { "deadly", 2100 }
                    }
                },
                { 9, new Dictionary<string, int> {
                        { "easy", 550 }, { "medium", 1100 }, { "hard", 1600 }, { "deadly", 2400 }
                    }
                },
                { 10, new Dictionary<string, int> {
                        { "easy", 600 }, { "medium", 1200 }, { "hard", 1900 }, { "deadly", 2800 }
                    }
                },
                { 11, new Dictionary<string, int> {
                        { "easy", 800 }, { "medium", 1600 }, { "hard", 2400 }, { "deadly", 3600 }
                    }
                },
                { 12, new Dictionary<string, int> {
                        { "easy", 1000 }, { "medium", 2000 }, { "hard", 3000 }, { "deadly", 4500 }
                    }
                },
                { 13, new Dictionary<string, int> {
                        { "easy", 1100 }, { "medium", 2200 }, { "hard", 3400 }, { "deadly", 5100 }
                    }
                },
                { 14, new Dictionary<string, int> {
                        { "easy", 1250 }, { "medium", 2500 }, { "hard", 3800 }, { "deadly", 5700 }
                    }
                },
                { 15, new Dictionary<string, int> {
                        { "easy", 1400 }, { "medium", 2800 }, { "hard", 4300 }, { "deadly", 6400 }
                    }
                },
                { 16, new Dictionary<string, int> {
                        { "easy", 1600 }, { "medium", 3200 }, { "hard", 4800 }, { "deadly", 7200 }
                    }
                },
                { 17, new Dictionary<string, int> {
                        { "easy", 2000 }, { "medium", 3900 }, { "hard", 5900 }, { "deadly", 8800 }
                    }
                },
                { 18, new Dictionary<string, int> {
                        { "easy", 2100 }, { "medium", 4200 }, { "hard", 6300 }, { "deadly", 9500 }
                    }
                },
                { 19, new Dictionary<string, int> {
                        { "easy", 2400 }, { "medium", 4900 }, { "hard", 7300 }, { "deadly", 10900 }
                    }
                },
                { 20, new Dictionary<string, int> {
                        { "easy", 2800 }, { "medium", 5700 }, { "hard", 8500 }, { "deadly", 12700 }
                    }
                }
            };
        }
    }
}