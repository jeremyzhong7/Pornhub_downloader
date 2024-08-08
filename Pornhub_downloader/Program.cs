using System.Net;
using System.Text.RegularExpressions;
using System;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Pornhub_downloader
{
    internal class Program
    {
        static HttpClient client = new HttpClient();
        static async Task Main(string[] args)
        {
#if DEBUG
            var baselink = @"https://cn.pornhub.com/view_video.php?viewkey=66855d107f8e3";
            Console.WriteLine($"please enter Pornhub link: {baselink}");
            
#else
            Console.Write("please enter Pornhub link: ");
            var baselink = Console.ReadLine();
#endif

            Dictionary<string, JObject> videoDict = new Dictionary<string, JObject>();

            if (!baselink.Contains(@"pornhub.com"))
            {
                Console.WriteLine("error link...");
                return;
            }

            try
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36");

                string html = await client.GetStringAsync(baselink);

                /*
                 * get m3u8 link
                 */
                string pattern = @"var flashvars_(.*?) = (.*?);";
                MatchCollection matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);

                if (matches.Count == 1 && matches[0].Groups.Count == 3)
                {
                    var jdata = (matches[0].Groups[2].Value);

                    if (!string.IsNullOrEmpty(jdata))
                    {
                        var array = JObject.Parse(jdata);
                        
                        if (array.ContainsKey("mediaDefinitions"))
                        {
                            JArray? defines = array["mediaDefinitions"] as JArray;

                            if (defines != null && defines.Count > 0)
                            {
                                foreach (JObject define in defines)
                                {
                                    if (define != null)
                                    {
                                        var format = define.ContainsKey("format") ? define["format"] : null;
                                        var quality = define.ContainsKey("quality") ? define["quality"] : null;

                                        if (format != null && format.ToString().Equals("hls") && quality != null)
                                            videoDict.Add(quality.ToString(), define);
                                    }
                                }
                            }
                        }
                    }
                }

                if (videoDict.Count == 0) throw new Exception("resp infomation has changed.");

                #region
                int tmp = 0;
                foreach (var kvp in videoDict)
                    Console.WriteLine($"[{tmp++}] quality: {kvp.Key}");

                string key;
                do
                {
                    Console.Write("please input quality: ");
                    var index = Console.ReadLine();

                    if (string.IsNullOrEmpty(index) || !videoDict.ContainsKey(index))
                    {
                        Console.WriteLine("error quality, please enter again!");
                    }
                    else
                    {
                        key = index;
                        break;
                    }
                }
                while (true);
                #endregion

                string m3u8Url;
                if (videoDict[key].ContainsKey("videoUrl") && !string.IsNullOrEmpty(videoDict?[key]?["videoUrl"]?.ToString()))
                    m3u8Url = videoDict[key]["videoUrl"].ToString();
                else
                    throw new Exception();
                /*
                 * spider m3u8 infomation
                 */
                var m3u8doc = await client.GetStringAsync(m3u8Url);

                pattern = @"^(.*?).m3u8(.*?)$";
                matches = Regex.Matches(m3u8doc, pattern, RegexOptions.Multiline);

                if (matches.Count == 0) throw new ApplicationException();

                int repIndex = m3u8Url.LastIndexOf('/');
                m3u8Url = m3u8Url.Remove(repIndex + 1);

                
                var links = await getAllts(matches[0].Value, m3u8Url);

                if (links == null ||  links.Count == 0) throw new ApplicationException();

                Stopwatch sw = Stopwatch.StartNew();

                // single thread
                // await DownloadModel.downloadTs(client, links, m3u8Url);

                // multithreading
                DownloadModel downloadModel = new DownloadModel();
                await downloadModel.downloadTsAsync(client, links, m3u8Url, thread_count: 8);

                sw.Stop();
                Console.WriteLine();
                Console.WriteLine($"Duration: {(double)(sw.ElapsedMilliseconds) / 1000}s");
                Console.WriteLine(@"Pls star repo https://github.com/jeremyzhong7/Pornhub_downloader");
                Console.WriteLine($"Done! Pls check [{DownloadModel.guid}.mp4] and Press to quit..");
                Console.ReadKey();
            }
            catch (ApplicationException ex)
            {
                Console.WriteLine($"Pornhub maybe has updated and this program has expired.");
                Console.WriteLine($"cause by: {ex.Message}");
            }

        }

        /// <summary>
        /// download & merge TS
        /// </summary>
        /// <param name="k"></param>
        /// <param name="baseUrl"></param>
        /// <returns></returns>
        private static async Task<List<string>> getAllts(string k, string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(k)) return null;

            var resp = await client.GetStringAsync(baseUrl + k);
            if (string.IsNullOrEmpty(resp) || resp.Length == 0) return null;

            var matches = Regex.Matches(resp, @"^(?!#).*", RegexOptions.Multiline);
            if (matches.Count == 0) return null;

            List<string> ts = new List<string>();
            foreach (Match match in matches)
                if (!string.IsNullOrEmpty(match.Value)) ts.Add(match.Value);

            return ts;
        }
    }
}
