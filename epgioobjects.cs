using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Tvzon
{
    public class Poster
    {
        public string original { get; set; }
        public string large { get; set; }
        public string medium { get; set; }
        public string small { get; set; }
    }

    public class Fanart
    {
        public string original { get; set; }
        public string large { get; set; }
        public string medium { get; set; }
        public string small { get; set; }
    }

    public class Banner
    {
        public string original { get; set; }
    }

    public class ExternalIds
    {
        public int? tmdb_id { get; set; }
        public int? tvdb_id { get; set; }
        public int? simkl_id { get; set; }
        public object sfi_id { get; set; }
        public object dfi_id { get; set; }
        public int? tvrage_id { get; set; }
        public string imdb_id { get; set; }
        public string facebook_handle { get; set; }
        public string twitter_hashtag { get; set; }
    }

    public class Series
    {
        public string id { get; set; }
        public string title { get; set; }
        public string slug { get; set; }
        public string url { get; set; }
        public int? runtime { get; set; }
        public Poster poster { get; set; }
        public Fanart fanart { get; set; }
        public Banner banner { get; set; }
        public string category { get; set; }
        public List<object> genres { get; set; }
        public ExternalIds external_ids { get; set; }
    }

    public class Image
    {
        public string original { get; set; }
        public string large { get; set; }
        public string medium { get; set; }
        public string small { get; set; }
    }

    public class Episode
    {
        public string id { get; set; }
        public string name { get; set; }
        public int number { get; set; }
        public int season_number { get; set; }
        public string season_id { get; set; }
        public Image image { get; set; }
    }

    public class Credits
    {
        public List<string> actor { get; set; }
        public List<string> writer { get; set; }
        public List<string> producer { get; set; }
        public List<string> director { get; set; }
        public List<string> presenter { get; set; }
        public List<string> guest { get; set; }
    }

    public class Programme2
    {
        public string id { get; set; }
        public string start { get; set; }
        public string stop { get; set; }
        public string description { get; set; }
        public Credits credits { get; set; }
        public int? live { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
    }

    public class Name
    {
        public string title { get; set; }
        public object country { get; set; }
        public string type { get; set; }
        public object channels_id { get; set; }
    }

    public class Programme
    {
        public Series series { get; set; }
        public Episode episode { get; set; }
        public Programme2 programme { get; set; }
        public Name name { get; set; }
    }

    public class Channel2
    {
        public string id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }
        public string url { get; set; }
        public string xmltv_id { get; set; }
        public Logo logo { get; set; }
        public List<Programme> programmes { get; set; }
        public string updated_at { get; set; }
    }

    public class ListingsObject
    {
        public List<Channel2> channels { get; set; }
    }

    public class Country
    {
        public string alpha2 { get; set; }
        public string alpha3 { get; set; }
        public string name { get; set; }
        public string region { get; set; }
        public List<string> timezones { get; set; }
    }

    public class Logo
    {
        public string original { get; set; }
        public string large { get; set; }
        public string medium { get; set; }
        public string small { get; set; }
        public string thumb { get; set; }
    }

    public class Country2
    {
        public string country { get; set; }
        public int? default_order { get; set; }
        public string updated_at { get; set; }
    }

    public class Channel
    {
        public string id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }
        public string url { get; set; }
        public string xmltv_id { get; set; }
        public Logo logo { get; set; }
        public Country2 country { get; set; }
        public string updated_at { get; set; }
    }

    public class ChannelsObject
    {
        public Country country { get; set; }
        public List<Channel> channels { get; set; }
    }

}
