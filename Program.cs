using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Data;
using System.Xml;
using System.Collections.Specialized;
using System.IdentityModel.Tokens;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Web.Helpers;
using Newtonsoft.Json;



namespace Tvzon
{
    class Program
    {

        static void HandleException(Exception ex)
        {
            HandleException(ex.Message);
        }

        static void HandleException(string message)
        {
            WriteToLog(message);
            try
            {
                EventLog.WriteEntry(SSource, message, EventLogEntryType.Error);
            }
            catch (Exception)
            {
            }

            Thread.Sleep(2000);
            //Environment.Exit(1);
        }

        public static string Serialize<T>(T obj)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(obj.GetType());
            MemoryStream ms = new MemoryStream();
            serializer.WriteObject(ms, obj);
            string retVal = Encoding.UTF8.GetString(ms.ToArray());
            return retVal;
        }

        public static T Deserialize<T>(string json)
        {
            T obj = Activator.CreateInstance<T>();
            MemoryStream ms = new MemoryStream(Encoding.Unicode.GetBytes(json));
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(obj.GetType());
            obj = (T)serializer.ReadObject(ms);
            ms.Close();
            return obj;
        }

        private static Stream GetWebStream(String url, Encoding encoding = null, int tries = 0)
        {

            MemoryStream resultStream = new MemoryStream();

            var tempUrl = getApiKey(url);
            var request = (HttpWebRequest)WebRequest.Create(tempUrl);
            request.UserAgent = "tvzon";


            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            request.Method = "GET";
            request.Timeout = 20000;

            if (encoding == null)
                encoding = Encoding.UTF8;
            string result;
            try
            {
                using (var response = request.GetResponse())
                {
                    using (var stream = response.GetResponseStream())
                    {
                        var streamReader = new StreamReader(stream, encoding);
                        result = streamReader.ReadToEnd();
                        stream.CopyTo(resultStream);
                    }
                }
            }
            catch (Exception ex)
            {
                if (tries > 4)
                    throw;
                tries++;
                var wait = tries * 10;
                WriteToLog("Error retrieving resource '{0}' {1}. Try '{2}'. Wait '{3}'.", url, ex.Message, tries, wait);
                Thread.Sleep(wait * 1000);
                return GetWebStream(url, encoding, tries);
            }
            return resultStream;
        }

        private static XmlDocument GetWebResponseDom(String url, Encoding encoding = null)
        {
            var xml = GetWebResponse(url, encoding);
            var dom = new XmlDocument();
            dom.XmlResolver = null;
            try
            {
                dom.LoadXml(xml);
            }
            catch (Exception)
            {
            }
            var errorNode = dom.SelectSingleNode("/*/error");
            if (errorNode != null && !String.IsNullOrEmpty(errorNode.InnerText))
                throw new InvalidOperationException(errorNode.InnerText);

            return dom;
        }

        private static String GetWebResponse(String url, Encoding encoding = null, int tries = 0)
        {

            var tempUrl = getApiKey(url);
            var request = (HttpWebRequest)WebRequest.Create(tempUrl);
            request.UserAgent = "tvzon";


            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            request.Method = "GET";
            request.Timeout = 20000;

            if (encoding == null)
                encoding = Encoding.UTF8;
            string result;
            try
            {
                using (var response = request.GetResponse())
                {
                    using (var stream = response.GetResponseStream())
                    {
                        var streamReader = new StreamReader(stream, encoding);
                        result = streamReader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                if (tries > 4)
                    throw;
                tries++;
                var wait = tries * 10;
                WriteToLog("Error retrieving resource '{0}' {1}. Try '{2}'. Wait '{3}'.", url, ex.Message, tries, wait);
                Thread.Sleep(wait * 1000);
                return GetWebResponse(url, encoding, tries);
            }
            return result;
        }

        private static bool ExsistImage(String url, int tries = 0)
        {
            var request = WebRequest.Create(url);
            request.Method = "HEAD";
            request.Timeout = 10000;
            try
            {
                using (request.GetResponse())
                {
                    return true;
                }
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError && ((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
                    return false;
                if (tries > 4)
                    return false;
                tries++;
                var wait = tries * 10;
                WriteToLog("Error retrieving resource '{0}' {1}. Try '{2}'. Wait '{3}'.", url, ex.Message, tries, wait);
                Thread.Sleep(wait * 1000);
                return ExsistImage(url, tries);

            }
            catch (Exception ex)
            {
                if (tries > 4)
                    return false;
                tries++;
                var wait = tries * 10;
                WriteToLog("Error retrieving resource '{0}' {1}. Try '{2}'. Wait '{3}'.", url, ex.Message, tries, wait);
                Thread.Sleep(wait * 1000);
                return ExsistImage(url, tries);
            }

        }

        private static string getApiKey(string url)
        {
            if (!url.Contains("epg.io") || url.Contains("api_key="))
                return url;
            if (url.Contains("?"))
                url += "&";
            else
                url += "?";

            url += "api_key=" + Properties.Settings.Default.apiKey;


            return url;
        }

        private static string CleanName(string name)
        {
            if (String.IsNullOrEmpty(name))
                return name;
            var re = new Regex(@"\W");
            name = re.Replace(name, @"_");
            re = new Regex(@"[åä]", RegexOptions.IgnoreCase);
            name = re.Replace(name, @"a");
            re = new Regex(@"[ö]", RegexOptions.IgnoreCase);
            name = re.Replace(name, @"o");
            return name;

        }

        private static string FixRating(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;
            var arr = value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (arr.Length < 2)
                return value;

            var rating = double.Parse(arr[0].Trim(), CultureInfo.InvariantCulture);
            var max = double.Parse(arr[1].Trim(), CultureInfo.InvariantCulture);

            if (max != 5)
            {
                var multiplier = 5d / max;
                max = 5d;
                rating = Math.Round((multiplier * rating) * 2, MidpointRounding.AwayFromZero) / 2;
            }
            return String.Format(CultureInfo.InvariantCulture, "{0:0.#}/{1:0}", rating, max);
        }

        private static readonly Dictionary<String, String> TvNuCache = new Dictionary<string, string>();

        private static readonly Dictionary<Int32, XmlDocument> ApiChannelCache = new Dictionary<Int32, XmlDocument>();

        private static readonly Dictionary<String, String> ApiChannelsCache = new Dictionary<string, string>();

        private static readonly DateTime TimeStamp = new DateTime(1970, 1, 1);

        private static string GetIconFromAPI(string channelId, int day, DateTime startDate)
        {
            String channelHandle;
            if (!ApiChannelsCache.TryGetValue(channelId, out channelHandle))
                return null;

            if ((startDate.TimeOfDay.Hours > 0 && startDate.TimeOfDay.Hours < 6))
                day--;

            XmlDocument dom;
            string jsonString;
            if (!ApiChannelCache.TryGetValue(day, out dom))
            {
                var apiChannelUrl = String.Format("http://api.epg.io/v1/schedule/listings/{0:yyyy-MM-dd}/{1}", Today.AddDays(day),
                                                  String.Join(",", ApiChannelsCache.Values));
                WriteToLog("Retrieving channels from API for day '{0:yyyy-MM-dd}'...", Today);
                try
                {
                    //           dom = GetWebResponseDom(apiChannelUrl);
                    jsonString = GetWebResponse(apiChannelUrl);
                    ListingsObject apiChannelsObject = JsonConvert.DeserializeObject<ListingsObject>(jsonString);
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                    return null;
                }
                ApiChannelCache.Add(day, dom);
            }
            var totalSeconds = (int)(startDate.ToUniversalTime() - TimeStamp).TotalSeconds;
            var channelHandleArr = channelHandle.Split(';');
            XmlNode itemNode = null;
            foreach (var handleId in channelHandleArr)
            {
                itemNode =
                    dom.SelectSingleNode(String.Format("/*/programmes/*/*[channel_handle={0} and start_time={1}]",
                                                       XPathEncode(handleId), XPathEncode((totalSeconds).ToString())));
                if (itemNode != null)
                    break;
            }
            if (itemNode == null)
                return null;
            var posterNode = itemNode.SelectSingleNode(String.Format("poster/small/text()", (int)(startDate.ToUniversalTime() - TimeStamp).TotalSeconds));
            if (posterNode != null)
                return posterNode.Value;
            posterNode = itemNode.SelectSingleNode(String.Format("fanart/small/text()", (int)(startDate.ToUniversalTime() - TimeStamp).TotalSeconds));
            if (posterNode != null)
                return posterNode.Value;
            return null;
        }

        private static string GetTvNuIcon(string channelId, int day, string programName, DateTime startDate)
        {


            var url = GetTvNuUrl(channelId, day, startDate);
            if (string.IsNullOrEmpty(url))
                return null;
            var html = "";
            if (TvNuCache.ContainsKey(url))
                html = TvNuCache[url];
            if (String.IsNullOrEmpty(html))
            {

                //try
                //{
                html = GetWebResponse(url, Encoding.GetEncoding(1252));
                //}
                //catch (Exception ex)
                //{
                //    HandleException(ex);
                //    return null;
                //}
            }
            if (!TvNuCache.ContainsKey(url))
                TvNuCache.Add(url, html);
            var re = new Regex(String.Format(@"<img src=""([^>]+?)""[^>]+?alt=""{0}""", Regex.Escape(programName)), RegexOptions.IgnoreCase);
            var match = re.Match(html);
            string imgUrl = null;
            if (!match.Success)
            {
                if (startDate != DateTime.MinValue)
                {
                    var reTime = new Regex(String.Format(@"<div[^>]+?id=""{0:HHmm}""[\s\S]+?</div>", startDate), RegexOptions.IgnoreCase);
                    match = reTime.Match(html);
                    if (match.Success)
                    {
                        var divHtml = match.Value;
                        re = new Regex(@"<img src=""([^>]+?)""", RegexOptions.IgnoreCase);
                        match = re.Match(divHtml);
                        if (match.Success)
                        {
                            var iconUrl = match.Groups[1].Value;
                            if (iconUrl.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
                                imgUrl = match.Groups[1].Value;

                        }
                    }
                }
            }
            else
                imgUrl = match.Groups[1].Value;
            if (!String.IsNullOrWhiteSpace(imgUrl))
            {
                if (imgUrl.StartsWith("//"))
                    imgUrl = "http:" + imgUrl;
                if (!imgUrl.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
                    imgUrl = null;

                var reQuality = new Regex(@"/sq/100/", RegexOptions.IgnoreCase);
                imgUrl = reQuality.Replace(imgUrl, @"/re/700/");

            }
            return imgUrl;
        }

        private static readonly List<String> EmptyImageCache = new List<string>();

        private static readonly DateTime Today = DateTime.Today;

        private static string GetProgramIcon(string channelId, int day, string programName, string programUrl, DateTime startDate)
        {
            //     var imgUrl = GetIconFromAPI(channelId, day, startDate);
            string imgUrl = "";
            if (String.IsNullOrEmpty(imgUrl) && !String.IsNullOrEmpty(programUrl))
            {



                if (programUrl.IndexOf("www.themoviedb.org", StringComparison.InvariantCultureIgnoreCase) > -1)
                {
                    try
                    {
                        var html = GetWebResponse(programUrl);
                        var re = new Regex(@"src=""(http://cf2.imgobject.com[\s\S]+?)""", RegexOptions.IgnoreCase);
                        var match = re.Match(html);
                        if (match.Success)
                        {
                            imgUrl = match.Groups[1].Value;
                        }

                    }
                    catch (Exception)
                    {

                    }
                }
                else if (programUrl.IndexOf("thetvdb.com", StringComparison.InvariantCultureIgnoreCase) > -1)
                {
                    var start = programUrl.IndexOf("?");
                    var query = HttpUtility.ParseQueryString(programUrl.Substring(start));
                    imgUrl =
                        String.Format("http://thetvdb.com/banners/_cache/episodes/{0}/{1}.jpg", query["seriesid"], query["id"]);
                    if (!ExsistImage(imgUrl))
                        imgUrl = "";
                }
                else if (programUrl.IndexOf("www.axess.se", StringComparison.InvariantCultureIgnoreCase) > -1)
                {
                    var start = programUrl.IndexOf("?");
                    var query = HttpUtility.ParseQueryString(programUrl.Substring(start));
                    imgUrl =
                        String.Format("http://www.axess.se/public/upload/images/tv_programs/{0}.jpg", query["id"]);
                    if (!ExsistImage(imgUrl))
                        imgUrl = "";
                }

            }
            if (String.IsNullOrEmpty(imgUrl))
                imgUrl = GetTvNuIcon(channelId, day, programName, startDate);

            return imgUrl;


        }

        private static string GetTvNuUrl(string channelId, int day, DateTime startDate)
        {

            if (startDate.Hour < 6)
                day--;

            if (day > 7)
                return null;

            var mappings = Properties.Settings.Default.TvNuMappings;
            if (!mappings.ContainsKey(channelId))
                return null;
            var urlPart = mappings[channelId];


            var weekDay = "";
            if (day == -1)
                weekDay = "igar";
            if (day == 1)
                weekDay = "imorgon";
            if (day > 1)
            {
                switch (Today.AddDays(day).DayOfWeek)
                {
                    case DayOfWeek.Monday:
                        weekDay = "mandag";
                        break;
                    case DayOfWeek.Tuesday:
                        weekDay = "tisdag";
                        break;
                    case DayOfWeek.Wednesday:
                        weekDay = "onsdag";
                        break;
                    case DayOfWeek.Thursday:
                        weekDay = "torsdag";
                        break;
                    case DayOfWeek.Friday:
                        weekDay = "fredag";
                        break;
                    case DayOfWeek.Saturday:
                        weekDay = "lordag";
                        break;
                    case DayOfWeek.Sunday:
                        weekDay = "sondag";
                        break;

                }
            }
            var url = String.Format("http://www.tv.nu/kanal/{0}/{1}", urlPart, weekDay);
            return url;
        }

        private const string SSource = "Application";

        private static StreamWriter _logwriter;



        private static void FixCategory(XmlElement node)
        {
            if (node.InnerText.Contains("/"))
            {
                var arr = node.InnerText.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                node.InnerText = arr[0];
                for (var i = 1; i < arr.Length; i++)
                {
                    var clone = node.CloneNode(true);
                    clone.InnerText = arr[i];
                    node.ParentNode.InsertBefore(clone, node);
                }
            }
        }

        private static bool IsProgrammeModified(string id, DateTime date, XmlDocument lastModifiedDom, XmlDocument cacheDom)
        {
            var historyLastModifiedNode = cacheDom.SelectSingleNode(
                                            String.Format("/*/lastmodified/channel[@id={0}]/datafor[text()='{1:yyyy-MM-dd}']/@lastmodified", XPathEncode(id), date));
            if (historyLastModifiedNode == null)
                return true;

            var currentLastModifiedNode = lastModifiedDom.SelectSingleNode(
                                            String.Format("/*/channel[@id={0}]/datafor[text()='{1:yyyy-MM-dd}']/@lastmodified", XPathEncode(id), date));
            if (currentLastModifiedNode == null)
                return true;

            return historyLastModifiedNode.Value != currentLastModifiedNode.Value;
        }

        private static void SaveHistory(XmlDocument cacheDom, XmlDocument lastModifiedDom, int days, StringCollection channels)
        {

            var lastDate = Today.AddDays(days - 1);
            WriteToLog("Save last modified to history...");

            var lastModifiedNode = (XmlElement)cacheDom.SelectSingleNode("/*/lastmodified");
            if (lastModifiedNode == null)
            {
                lastModifiedNode = cacheDom.CreateElement("lastmodified");
                cacheDom.DocumentElement.AppendChild(lastModifiedNode);
            }
            lastModifiedNode.RemoveAll();
            var nodes = lastModifiedDom.SelectNodes("/*/channel");
            foreach (XmlElement channelNode in nodes)
            {
                if (!channels.Contains(channelNode.GetAttribute("id")))
                    continue;

                var dataforNodes = channelNode.SelectNodes("datafor");

                foreach (XmlElement nodeToDelete in dataforNodes)
                {
                    var currentDate = DateTime.MinValue;
                    if (DateTime.TryParse(nodeToDelete.InnerText, out currentDate) && (currentDate - lastDate).TotalDays > 0)
                        channelNode.RemoveChild(nodeToDelete);
                }

                lastModifiedNode.AppendChild(cacheDom.ImportNode(channelNode, true));
            }
        }

        private static void PreviousShown(XmlDocument domOutput, XmlDocument cacheDom, int previousShownDuration)
        {

            WriteToLog("Start analyzing previously shown...");
            var nodes = domOutput.SelectNodes("tv/programme");
            foreach (XmlElement programme in nodes)
            {
                if (programme.GetAttribute("programmeday") != String.Empty)
                {
                    programme.RemoveAttribute("programmeday");
                    programme.RemoveAttribute("lastmodified");
                    continue;
                }
                try
                {
                    if (programme.SelectSingleNode("previously-shown") == null)
                        FixPreviouslyShown(programme, cacheDom, previousShownDuration);
                }
                catch (Exception)
                {
                }
            }
        }

        private static void FixEpisodeNum(XmlElement programmeNode, bool insertEpisodeInDesc)
        {

            foreach (XmlElement node in programmeNode.SelectNodes("episode-num"))
            {
                var episodeText = node.InnerText;
                if (string.IsNullOrEmpty(episodeText))
                    continue;
                var system = node.GetAttribute("system");


                if (system.Equals("xmltv_ns"))
                {
                    var arr = episodeText.Split('.');
                    var result = "";
                    foreach (var item in arr)
                    {
                        var num = item.Trim();
                        if (String.IsNullOrEmpty(num))
                            num = " ";
                        if (!String.IsNullOrEmpty(result))
                            result += ".";
                        result += num;
                    }

                    if (result.Contains('/'))
                    {
                        result = result.Substring(0, result.IndexOf('/')) + ".";
                    }
                    node.InnerText = result;
                }

                if (insertEpisodeInDesc && system.Equals("onscreen"))
                {
                    foreach (XmlElement descNode in programmeNode.SelectNodes("desc"))
                    {
                        var descText = descNode.InnerText;
                        if (!descText.Contains(episodeText))
                        {
                            descText = episodeText + ". " + descText;
                            descNode.InnerText = descText;
                        }
                    }
                }

                if (system.Equals("thetvdb.com"))
                {
                    programmeNode.RemoveChild(node);
                }
            }
        }

        private static string FixDateString(string dateString)
        {
            if (string.IsNullOrEmpty(dateString))
                return dateString;
            return dateString.Insert(12, ":").Insert(10, ":").Insert(8, " ").Insert(6, "-").Insert(4, "-");
        }

        private static string XPathEncode(string text)
        {
            if (text == null)
                text = String.Empty;
            if (text.Contains('\''))
                return "concat('" + text.Replace("'", "', \"'\", '") + "')";
            return "'" + text + "'";
        }

        private static void FixPreviouslyShown(XmlElement programmeNode, XmlDocument cacheDom, int previousShownDuration)
        {
            String title = null;
            String subTitle = null;
            String episode = null;
            var node = programmeNode.SelectSingleNode("title/text()");
            if (node == null || node.Value == null)
                return;
            title = node.Value.Trim();

            node = programmeNode.SelectSingleNode("sub-title/text()");
            if (node != null && node.Value != null)
                subTitle = node.Value.Trim();

            node = programmeNode.SelectSingleNode("episode-num[@system='xmltv_ns']/text()");
            if (node != null && node.Value != null)
                episode = node.Value.Trim();

            if (episode == null && subTitle == null)
                return;

            var xPath = String.Format("/*/programme[normalize-space(title) = {0}", XPathEncode(title));

            if (subTitle != null)
                xPath += String.Format(" and normalize-space(sub-title) = {0}", XPathEncode(subTitle));

            if (episode != null)
                xPath += String.Format(" and normalize-space(episode-num[@system='xmltv_ns']) = {0}", XPathEncode(episode));

            xPath += "]";
            var currentStart = DateTime.MinValue;
            var currentStartString = programmeNode.GetAttribute("start");
            if (!DateTime.TryParse(FixDateString(currentStartString), out currentStart))
                return;
            var currentStop = DateTime.MinValue;
            var currentStopString = programmeNode.GetAttribute("stop");
            if (!DateTime.TryParse(FixDateString(currentStopString), out currentStop))
                return;

            var programLengthMinutes = (int)(currentStop - currentStart).TotalMinutes;

            var currentChannel = programmeNode.GetAttribute("channel");

            var oColl = cacheDom.SelectNodes(xPath);


            foreach (XmlElement historyProgrammeNode in oColl)
            {

                if (subTitle == null && historyProgrammeNode.SelectSingleNode("sub-title") != null)
                    continue;
                if (episode == null && historyProgrammeNode.SelectSingleNode("episode-num") != null)
                    continue;
                var historyStart = DateTime.MinValue;
                var historyStartString = historyProgrammeNode.GetAttribute("start");
                if (String.IsNullOrEmpty(historyStartString) || historyStartString.Length < 14)
                    continue;

                if (!DateTime.TryParse(FixDateString(historyStartString), out historyStart))
                    continue;
                var historyChannel = historyProgrammeNode.GetAttribute("channel");

                var historyPreviouslyShownNode = (XmlElement)historyProgrammeNode.SelectSingleNode("previously-shown");
                if (((int)(currentStart - historyStart).TotalMinutes) == 0 && historyPreviouslyShownNode != null)
                {
                    historyStartString = historyPreviouslyShownNode.GetAttribute("start");
                    historyChannel = historyPreviouslyShownNode.GetAttribute("channel");
                    if (!DateTime.TryParse(FixDateString(historyStartString), out historyStart))
                        continue;
                }
                var totalMinutes = (int)(currentStart - historyStart).TotalMinutes - programLengthMinutes;
                if (totalMinutes < previousShownDuration)
                    continue;

                XmlElement findHistoryPreviouslyShownNode = null;
                var findHistoryProgrammeNode = cacheDom.SelectSingleNode(String.Format("/*/programme[@channel={0} and @start={1}]", XPathEncode(currentChannel), XPathEncode(currentStartString)));
                if (findHistoryProgrammeNode != null)
                {
                    findHistoryPreviouslyShownNode = (XmlElement)findHistoryProgrammeNode.SelectSingleNode("previously-shown");
                    if (findHistoryPreviouslyShownNode != null)
                    {
                        var historyTempStart = DateTime.MinValue;
                        var historyTempStartString = findHistoryPreviouslyShownNode.GetAttribute("start");
                        var historyTempChannel = findHistoryPreviouslyShownNode.GetAttribute("channel");
                        if (DateTime.TryParse(FixDateString(historyTempStartString), out historyTempStart))
                        {
                            var tempMinutes = (int)(historyTempStart - historyStart).TotalMinutes;
                            if (tempMinutes < 0 || (historyTempChannel == currentChannel && historyChannel != currentChannel && tempMinutes == 0))
                            {
                                historyChannel = historyTempChannel;
                                historyStartString = historyTempStartString;
                                historyStart = historyTempStart;
                            }
                        }
                    }
                }

                var currentPreviouslyShownNode = (XmlElement)programmeNode.SelectSingleNode("previously-shown");
                if (currentPreviouslyShownNode == null)
                {
                    WriteToLog("Adding previously shown for program '{0}' on channel '{1}'...", title, currentChannel);
                    currentPreviouslyShownNode = programmeNode.OwnerDocument.CreateElement("previously-shown");
                    programmeNode.AppendChild(currentPreviouslyShownNode);
                }



                currentPreviouslyShownNode.SetAttribute("channel", historyChannel);
                currentPreviouslyShownNode.SetAttribute("start", historyStartString);

                if (findHistoryProgrammeNode != null)
                {
                    if (findHistoryPreviouslyShownNode == null)
                    {
                        findHistoryPreviouslyShownNode =
                            (XmlElement)findHistoryProgrammeNode.OwnerDocument.ImportNode(currentPreviouslyShownNode, true);
                        findHistoryProgrammeNode.AppendChild(findHistoryPreviouslyShownNode);
                    }
                    else
                    {
                        findHistoryPreviouslyShownNode.SetAttribute("channel", historyChannel);
                        findHistoryPreviouslyShownNode.SetAttribute("start", historyStartString);
                    }
                }
            }
        }

        private static void WriteToLog(string message, params Object[] args)
        {

            Console.WriteLine(message, args);

            if (_logwriter != null)
            {
                var logMessage = string.Format("{0:G} {1}", DateTime.Now, String.Format(message, args));

                try
                {
                    _logwriter.WriteLine(logMessage);

                }
                catch (Exception)
                {
                    _logwriter.Dispose();
                    _logwriter = null;
                }

            }


        }

        static void Main(string[] args)
        {
            var saveToFile = false;

            try
            {
                EventLog.WriteEntry(SSource, "Starting Tvzon...", EventLogEntryType.Information);
            }
            catch (Exception)
            {
            }

            var path = Properties.Settings.Default.OutputPath;
            var days = Properties.Settings.Default.Days;
            var channels = Properties.Settings.Default.Channels;
            var daysInHistory = Properties.Settings.Default.DaysInHistory;
            var previousShownDuration = Properties.Settings.Default.PreviousShownDuration;
            var insertEpisodeInDesc = Properties.Settings.Default.InsertEpisodeInDesc;



            var dirName = Path.Combine(Environment.GetEnvironmentVariable("PROGRAMDATA"), "Tvzon");
            if (!Directory.Exists(dirName))
                Directory.CreateDirectory(dirName);

            var logPath = Path.Combine(dirName, "log.txt");

            var cacheLocation = Path.Combine(dirName, "temp.xml");


            using (_logwriter = (Properties.Settings.Default.WriteToLog ? new StreamWriter(logPath, false) : null))
            {

                if (String.IsNullOrEmpty(path))
                {
                    HandleException("Invalid path");
                    return;
                }
                if (channels.Count == 0)
                {
                    HandleException("No channels specified.");
                    return;
                }
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                {
                    HandleException(String.Format("Directory '{0}' does not exist.", dir));
                    return;
                }
                if (days < 0)
                    days = 0;

                if (daysInHistory < 1)
                    daysInHistory = 1;

                if (previousShownDuration < 0)
                    previousShownDuration = 0;

                const string url = "http://xmltv.xmltv.se/channels.xml.gz";
                const string ApiChannelsUrl = "http://api.epg.io/v1/schedule/channels/se.json";
                const string ChannelsLastModifiedUrl = "http://xmltv.xmltv.se/datalist.xml.gz";

                XmlDocument domInput;
                var domOutput = new XmlDocument();
                XmlDocument tempDom;
                var cacheDom = new XmlDocument();

                XmlDocument apiChannelsDom;
                XmlDocument lastModifiedDom;



                cacheDom.XmlResolver = domOutput.XmlResolver = null;

                var cachedIcons = Properties.Settings.Default.ImageCache;
                if (cachedIcons == null)
                    cachedIcons = new SerializableStringDictionary();
                //domOutput.PreserveWhitespace = true;
                WriteToLog("Loading Cache History...");
                if (File.Exists(cacheLocation) && daysInHistory > 0)
                    cacheDom.Load(cacheLocation);

                if (cacheDom.DocumentElement == null)
                    cacheDom.LoadXml("<tv generator-info-name=\"nonametv\" />");

                domOutput.LoadXml("<tv generator-info-name=\"nonametv\" />");
                WriteToLog("Retrieving channels from '{0}'...", url);
                try
                {
                    domInput = GetWebResponseDom(url);
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                    return;

                }

                WriteToLog("Retrieving channels last modified from '{0}'...", ChannelsLastModifiedUrl);
                try
                {
                    lastModifiedDom = GetWebResponseDom(ChannelsLastModifiedUrl);
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                    return;

                }

                //    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(Channel));
                //    ChannelObject channel
                //    JsonConvert.PopulateObject(jsonString, channel );

                WriteToLog("Retrieving channels from '{0}'...", ApiChannelsUrl);

                var jsonStream = GetWebResponse(ApiChannelsUrl);
                //  ConstructorHandling.
                dynamic apiChannelsJSON = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonStream);

                //    DataSet dataSet = new DataSet("Suppliers");

                ChannelsObject apiChannelsObject = JsonConvert.DeserializeObject<ChannelsObject>(jsonStream);

                //      foreach (var item in apiChannelsObject )
                //       {

                //       }
                //  Console.WriteLine(apiChannelsObject.channels[0].name );  


                //     var result = JsonConvert.DeserializeObject<RootObject>(jsonStream);

                apiChannelsDom = new XmlDocument();


                var nodes = cacheDom.SelectNodes("tv/channel");
                foreach (XmlElement element in nodes)
                {
                    element.ParentNode.RemoveChild(element);
                }

                nodes = cacheDom.SelectNodes("tv/programme");
                foreach (XmlElement programme in nodes)
                {
                    var startString = programme.GetAttribute("start");
                    var start = DateTime.MinValue;
                    if (!String.IsNullOrEmpty(startString) && startString.Length >= 14)
                    {
                        startString = FixDateString(startString);
                        DateTime.TryParse(startString, out start);
                    }
                    var daysCount = (int)(Today - start.Date).TotalDays;
                    if (daysCount < 1 || daysCount > daysInHistory)
                    {
                        var titleNode = programme.SelectSingleNode("title/text()");
                        if (daysCount < 1)
                        {
                            var lastModified = programme.GetAttribute("lastmodified");
                            var programmeDay = programme.GetAttribute("programmeday");
                            if (lastModified != String.Empty && programmeDay != String.Empty)
                            {
                                var lastModifiedNode =
                                    lastModifiedDom.SelectSingleNode(String.Format("/*/channel[@id={0}]/datafor[text()={1}]/@lastmodified",
                                                                                   XPathEncode(programme.GetAttribute("channel")),
                                                                                   XPathEncode(programmeDay)));
                                if (lastModifiedNode != null && lastModifiedNode.Value == lastModified)
                                {
                                    if (titleNode != null)
                                        WriteToLog("Program '{0}' on channel '{1}' found in cache...", titleNode.Value, programme.GetAttribute("channel"));
                                    continue;
                                }
                            }
                        }

                        if (titleNode != null)
                            WriteToLog("Deleting program '{0}' on channel '{1}' in cache...", titleNode.Value, programme.GetAttribute("channel"));
                        programme.ParentNode.RemoveChild(programme);
                    }
                }

                // Kod för att hantera att kanalen t.ex heter svtb-svt24.se
                nodes = domInput.SelectNodes("tv/channel");
                foreach (XmlElement element in nodes)
                {
                    var id = element.GetAttribute("id");
                    if (channels.Contains(id))
                    {
                        var channelName = id;
                        var titleNode = element.SelectSingleNode("display-name");
                        if (titleNode != null)
                            channelName = titleNode.InnerText;

                        cacheDom.DocumentElement.InsertBefore(cacheDom.ImportNode(element, true), cacheDom.DocumentElement.FirstChild);

                        domOutput.DocumentElement.AppendChild(domOutput.ImportNode(element, true));
                        WriteToLog("Added channel '{0}'.", channelName);

                        //       	var apiChannelNode =
                        //       		apiChannelsDom.SelectSingleNode(String.Format("/*/channels/channel[tvuri={0}]/handle/text()", XPathEncode(id)));
                        bool foundChannel = false;


                        foreach (var channel in apiChannelsJSON.channels)
                        {
                            //  Console.WriteLine(id+ " "  + channel.xmltv_id );
                            string xmltv_id = channel.xmltv_id;
                            if (id.Equals(xmltv_id))
                            {
                                string channelid = channel.id;
                                ApiChannelsCache.Add(id, channelid);
                                foundChannel = true;
                                //    Console.WriteLine("YES");
                            }
                        }
                        if (foundChannel == false && id.Contains('-') && id.Contains('.'))
                        {
                            var idStringEnd = id;
                            var idPart = idStringEnd.Substring(0, idStringEnd.IndexOf('.'));
                            idStringEnd = idStringEnd.Substring(idStringEnd.IndexOf('.'));
                            var newId = String.Empty;
                            foreach (var sId in idPart.Split('-'))
                            {
                                foreach (var xchannel in apiChannelsJSON.channels)
                                {
                                    //  Console.WriteLine(id+ " "  + channel.xmltv_id );
                                    string Xxmltv_id = xchannel.xmltv_id;
                                    string SearchString = sId + idStringEnd;
                                    if (SearchString.Equals(Xxmltv_id))
                                    //	apiChannelNode =
                                    //				apiChannelsDom.SelectSingleNode(String.Format("/*/channels/channel[tvuri={0}]/handle/text()",
                                    //										  XPathEncode(sId + idStringEnd)));
                                    //if (apiChannelNode != null)
                                    {
                                        if (!String.IsNullOrEmpty(newId))
                                            newId += ";";
                                        string channelid = xchannel.id;
                                        newId += channelid;
                                    }
                                }
                                if (!String.IsNullOrEmpty(newId))
                                    ApiChannelsCache.Add(id, newId);
                            }


                        }



                        //   ChannelObject currentChannelObject = new ChannelObject() ;

                        //      	var apiChannelNode =
                        //      		apiChannelsDom.SelectSingleNode(String.Format("/*/channels/channel[tvuri={0}]/handle/text()", XPathEncode(id)));
                        //              
                        //			if (apiChannelNode != null)
                        //					ApiChannelsCache.Add(id, apiChannelNode.Value);
                        //				if (apiChannelNode == null && id.Contains('-') && id.Contains('.'))
                        //					{
                        //						var idStringEnd = id;
                        //						var idPart = idStringEnd.Substring(0, idStringEnd.IndexOf('.'));
                        //						idStringEnd = idStringEnd.Substring(idStringEnd.IndexOf('.'));
                        //						var newId = String.Empty;
                        //						foreach (var sId in idPart.Split('-'))
                        //						{
                        //							apiChannelNode =
                        //								apiChannelsDom.SelectSingleNode(String.Format("/*/channels/channel[tvuri={0}]/handle/text()",
                        //																			  XPathEncode(sId + idStringEnd)));
                        //							if (apiChannelNode != null)
                        //							{
                        //								if (!String.IsNullOrEmpty(newId))
                        //									newId += ";";
                        //								newId += apiChannelNode.Value;
                        //							}
                        //						}
                        //						if (!String.IsNullOrEmpty(newId))
                        //							ApiChannelsCache.Add(id, newId);
                        //					}


                    }
                }


                nodes = domOutput.SelectNodes("tv/channel");
                foreach (XmlElement element in nodes)
                {
                    var id = element.GetAttribute("id");
                    var channelName = id;
                    var titleNode = element.SelectSingleNode("display-name");
                    if (titleNode != null)
                        channelName = titleNode.InnerText;

                    if (channels.Contains(id))
                    {
                        for (var i = 0; i < days; i++)
                        {
                            var date = Today.AddDays(i);
                            var cacheNodes =
                                cacheDom.SelectNodes(String.Format("tv/programme[@channel={0} and @programmeday='{1:yyyy-MM-dd}']",
                                                                   XPathEncode(id), date));
                            if (cacheNodes.Count > 0)
                            {
                                foreach (XmlElement programme in cacheNodes)
                                {
                                    var clone = (XmlElement)domOutput.ImportNode(programme, true);
                                    domOutput.DocumentElement.AppendChild(clone);
                                }
                                continue;
                            }

                            if (!IsProgrammeModified(id, date, lastModifiedDom, cacheDom))
                                continue;


                            var channelUrl = String.Format("http://xmltv.xmltv.se/{0}_{1:yyyy-MM-dd}.xml.gz", id, date);
                            WriteToLog("Retrieving xmltv from '{0}'...", channelUrl);
                            try
                            {
                                tempDom = GetWebResponseDom(channelUrl);
                                saveToFile = true;
                            }
                            catch (Exception ex)
                            {
                                HandleException(ex);
                                return;
                            }
                            var programmeNodes = tempDom.SelectNodes("tv/programme");
                            foreach (XmlElement programme in programmeNodes)
                            {

                                var programName = "";
                                titleNode = programme.SelectSingleNode("title");
                                if (titleNode != null)
                                    programName = titleNode.InnerText;
                                var start = DateTime.MinValue;
                                var startString = programme.GetAttribute("start");
                                if (!String.IsNullOrEmpty(startString) && startString.Length >= 14)
                                {
                                    startString = FixDateString(startString);
                                    DateTime.TryParse(startString, out start);
                                }

                                WriteToLog("Added program '{0}' on channel '{1}'.", programName, channelName);
                                var imgUrl = "";
                                var programUrl = "";
                                var urlNode = programme.SelectSingleNode("url");
                                if (urlNode != null)
                                {
                                    programUrl = urlNode.InnerText;

                                }
                                var cacheKey = programUrl;
                                if (String.IsNullOrEmpty(cacheKey))
                                    cacheKey = string.Format("{0}_{1}", id, programName);

                                if (cachedIcons.ContainsKey(cacheKey))
                                    imgUrl = cachedIcons[cacheKey];
                                try
                                {
                                    if (String.IsNullOrEmpty(imgUrl) && !EmptyImageCache.Contains(cacheKey))
                                    {
                                        /*        foreach (var channel in apiChannelsJSON.channels)
                                                   // den här är fel, ska leta urlicon till programmet istället...ska ändra
                                                   {
                                                       string xmltv_id = channel.xmltv_id;
                                                       if (id.Equals(xmltv_id))
                                                       {
                                                           imgUrl = channel.url;

                                                       }
                                                   }*/
                                    }
                                    imgUrl = GetProgramIcon(id, i, programName, programUrl, start);
                                }
                                catch (Exception ex)
                                {
                                    HandleException(ex);
                                    return;
                                }
                                if (!String.IsNullOrEmpty(imgUrl))
                                {
                                    var iconNode = tempDom.CreateElement("icon");
                                    iconNode.SetAttribute("src", imgUrl);
                                    programme.AppendChild(iconNode);
                                    if (!cachedIcons.ContainsKey(cacheKey))
                                        cachedIcons.Add(cacheKey, imgUrl);
                                    WriteToLog("Added program icon '{0}' for program '{1}' on channel '{2}'.", imgUrl, programName, channelName);
                                }
                                else
                                    EmptyImageCache.Add(cacheKey);


                                foreach (XmlElement node in programme.SelectNodes("star-rating/value"))
                                    node.InnerText = FixRating(node.InnerText);

                                FixEpisodeNum(programme, insertEpisodeInDesc);

                                foreach (XmlElement node in programme.SelectNodes("category"))
                                    FixCategory(node);

                                if (id.IndexOf("hd", StringComparison.InvariantCultureIgnoreCase) == -1)
                                {
                                    var qualityNode = programme.SelectSingleNode("video/quality");
                                    if (qualityNode != null)
                                        qualityNode.ParentNode.RemoveChild(qualityNode);
                                }
                                if (daysInHistory > 0)
                                {
                                    var lastModifiedNode =
                                        lastModifiedDom.SelectSingleNode(
                                            String.Format("/*/channel[@id={0}]/datafor[text()='{1:yyyy-MM-dd}']/@lastmodified", XPathEncode(id), date));
                                    var cacheClone = (XmlElement)cacheDom.ImportNode(programme, true);
                                    if (lastModifiedNode != null)
                                    {
                                        cacheClone.SetAttribute("programmeday", date.ToString("yyyy-MM-dd"));
                                        cacheClone.SetAttribute("lastmodified", lastModifiedNode.Value);
                                    }

                                    cacheDom.DocumentElement.AppendChild(cacheClone);

                                }
                                domOutput.DocumentElement.AppendChild(domOutput.ImportNode(programme, true));
                            }
                        }
                    }


                }
                if (daysInHistory > 0)
                {
                    PreviousShown(domOutput, cacheDom, previousShownDuration);
                    SaveHistory(cacheDom, lastModifiedDom, days, channels);
                }

                //#if DEBUG
                //                var list = new List<string>();
                //                var tempNodes = cacheDom.SelectNodes("//category/text()");
                //                foreach (XmlNode node in tempNodes)
                //                    if (!list.Contains(node.Value))
                //                        list.Add(node.Value);

                //                Debug.Print(String.Join(Environment.NewLine, list));
                //#endif

                try
                {
                    if (saveToFile || !File.Exists(path))
                    {
                        domOutput.Save(path);
                        if (cachedIcons.Count > 50000)
                            cachedIcons = null;
                        Properties.Settings.Default.ImageCache = cachedIcons;
                        Properties.Settings.Default.Save();


                        cacheDom.Save(cacheLocation);
                    }
                    else
                        WriteToLog("No changes found, ignoring write to file...");
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                    return;
                }

                WriteToLog("Done!");
                try
                {
                    EventLog.WriteEntry(SSource, "Tvzon has successfully ended.", EventLogEntryType.Information);
                }
                catch (Exception)
                {
                }
            }
            //Thread.Sleep(2000);
        }

    }
}
