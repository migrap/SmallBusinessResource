using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace SmallBusinessResource {
    public class Program {
        public static void Main(string[] args) {
            var client = new SmallBusinessResourceClient();
            var cities = client.GetCitiesAsync().Result;

            using(FileStream fs = File.Open(@"cities.json", FileMode.OpenOrCreate))
            using(StreamWriter sw = new StreamWriter(fs))
            using(JsonWriter jw = new JsonTextWriter(sw)) {
                jw.Formatting = Formatting.Indented;

                var serializer = new JsonSerializer();
                serializer.Serialize(jw, cities);
            }
        }
    }

    public class SmallBusinessResourceClient {
        private HttpClient _client;

        public SmallBusinessResourceClient() {
            _client = new HttpClient(new HttpClientHandler()) {
                BaseAddress = new Uri("http://api.sba.gov")
            };
        }

        public async Task<JArray> GetCitiesAsync(params string[] states) {            
            return await GetCitiesAsync(states.AsEnumerable());
        }

        public async Task<JArray> GetCitiesAsync(IEnumerable<string> states, int count = 4) {
            states = (states.Any()) ? states : _states;

            using(var semaphore = new SemaphoreSlim(count)) {
                var tasks = states.Select(async (state) => {
                    await semaphore.WaitAsync();
                    try {
                        return await GetCitiesAsync(state);
                    }
                    finally {
                        semaphore.Release();
                    }
                }).ToArray();

                await Task.WhenAll(tasks);

                return tasks.Select(x => x.Result).Concat();
            }
        }

        public async Task<JArray> GetCitiesAsync(string state) {
            var response = await _client.GetAsync($"geodata/city_county_links_for_state_of/{state}.json");
            var content = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<JArray>(content);
        }

        private readonly string[] _states = new string[] {
            "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA",
            "HI", "ID", "IL", "IN", "IA", "KS", "KY", "LA", "ME", "MD",
            "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ",
            "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC",
            "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV", "WI", "WY" };
    }


    public class City {
        [JsonProperty("county_name")]
        public string County{get;set;} 
        
        [JsonProperty("description")]  
        public string Description { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("state_abbreviation")]
        public string StateAbbreviation { get; set; }

        [JsonProperty("state_name")]
        public string State { get; set; }
    }

    public static partial class Extensions {
        public static JArray Concat(this IEnumerable<JArray> source) {
            var settings = new JsonMergeSettings {
                MergeArrayHandling = MergeArrayHandling.Concat
            };

            var array = new JArray();

            foreach(var x in source) {
                array.Merge(x, settings);
            }

            return array;
        }
    }
}


