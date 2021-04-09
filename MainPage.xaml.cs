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
        int pageIndex = -1;
        int pageSize = 10;

        public MainPage()
        {
            this.InitializeComponent();

            Get_All_Monsters();
            displayMonsterList = new ObservableCollection<FullMonsterDetails>();
        }


        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var filtered = monsterNameList.Where(monsterName => monsterName.ToLower().Contains(this.SearchBox.Text.ToLower())).ToList();
                sender.ItemsSource = filtered;
            }
        }


        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            // Set sender.Text. You can use args.SelectedItem to build your text string.
        }


        private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion != null)
            {
                // User selected an item from the suggestion list, take an action on it here.
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
            mainMonsterList = new List<SimpleMonster>(rootObjectData.simpleMonsters);

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
            public List<SimpleMonster> simpleMonsters { get; set; }
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