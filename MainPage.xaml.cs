using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace encounter_difficulty
{
    public sealed partial class MainPage : Page
    {

        private List<SimpleMonster> mainMonsterList;
        private List<string> monsterNameList;
        private ObservableCollection<FullMonsterDetails> displayMonsterList;
        ObservableCollection<int> partySizeCollection = new ObservableCollection<int>();
        ObservableCollection<int> partyMembersLevelCollection = new ObservableCollection<int>();
        ObservableCollection<int> pageSizeOptions = new ObservableCollection<int>();
        List<string> noResultFound = new List<string>(1) { "No such monster found" };
        int pageIndex = -1;
        int pageSize;

        public MainPage()
        {
            this.InitializeComponent();
            this.InitParty();

            Get_All_Monsters();
            displayMonsterList = new ObservableCollection<FullMonsterDetails>();
        }

        private void InitParty()
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

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var filtered = monsterNameList.Where(monsterName => monsterName.ToLower().Contains(this.SearchBox.Text.ToLower())).ToList();

                if (filtered.Count > 0)
                {
                    sender.ItemsSource = filtered;
                } else
                {
                    sender.ItemsSource = noResultFound; 
                }
            }
        }


        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            // Set sender.Text. You can use args.SelectedItem to build your text string.
        }


        private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion != null && !args.ChosenSuggestion.Equals(noResultFound.First()))
            {
                List<SimpleMonster> chosenMonster = mainMonsterList.Where(monster => monster.Name.Equals(args.ChosenSuggestion)).ToList();
                if (chosenMonster.Count > 0)
                {
                    Display_Monsters(chosenMonster);
                    pageIndex = -1;
                }
            }
            else
            {
                // Use args.QueryText to determine what to do.
            }
        }
     
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            pageIndex++;
         
            List<SimpleMonster> simpleMonsters = mainMonsterList.Skip(pageIndex * pageSize).Take(pageSize).ToList();

            Display_Monsters(simpleMonsters);
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            pageIndex--;

            List<SimpleMonster> simpleMonsters = mainMonsterList.Skip(pageIndex * pageSize).Take(pageSize).ToList();

            Display_Monsters(simpleMonsters);
        }

        private void PageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            pageSize = (int)e.AddedItems[0];
        }

        private void DisplayMonsterList_ItemClick(object sender, ItemClickEventArgs e)
        {
            MonsterEncounterList.Items.Add(e.ClickedItem);
        }

        private void MonsterEncounterList_ItemClick(object sender, ItemClickEventArgs e)
        {
            MonsterEncounterList.Items.Remove(e.ClickedItem);
        }

        private async void Display_Monsters(List<SimpleMonster> simpleMonsters)
        {
            displayMonsterList.Clear();
            string requestUrl = "https://www.dnd5eapi.co/api/monsters/";

            foreach (var monster in simpleMonsters)
            {
                var json = await FetchAsync(requestUrl + monster.Index);
                displayMonsterList.Add(JsonConvert.DeserializeObject<FullMonsterDetails>(json));
            }

            DisplayMonsterList.ItemsSource = displayMonsterList;
        }

        private async void Get_All_Monsters()
        {
            string requestUrl = "https://www.dnd5eapi.co/api/monsters";

            var json = await FetchAsync(requestUrl);

            RootSimpleMonsterResult rootObjectData = JsonConvert.DeserializeObject<RootSimpleMonsterResult>(json);
            mainMonsterList = new List<SimpleMonster>(rootObjectData.SimpleMonsters);

            monsterNameList = mainMonsterList.Select(monster => monster.Name).ToList();
            NextButton_Click(null, null);
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

        internal class FullMonsterDetails
        {
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

        internal class SimpleMonster
        {
            [JsonProperty("index")]
            public string Index { get; set; }
            
            [JsonProperty("name")]
            public string Name { get; set; }
        }
    }
}