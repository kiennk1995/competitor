using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.IO;
using System.Net.Http;
using System.Configuration;
using RestSharp;
using log4net;

namespace Competitor_sUpdates
{
    class Program
    {
        protected static readonly ILog log = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            StringBuilder logsb = new StringBuilder();

            try
            {
                log4net.Config.BasicConfigurator.Configure();
                logsb.AppendLine(String.Format("[{0}] Start...", DateTime.Now.ToString()));
                log.Info(String.Format("[{0}] Start...", DateTime.Now.ToString()));

                //Check File Exist
                if (!File.Exists(@"..\..\Competitor\ListCompetitor.txt"))
                {
                    logsb.AppendLine("Competitor List is not exist...");
                    logsb.AppendLine(String.Format("[{0}] End...", DateTime.Now.ToString()));
                    log.Info("Competitor List is not exist...");
                    log.Info(String.Format("[{0}] End...", DateTime.Now.ToString()));
                }

                //Get all Competitor 
                List<String> AllCompetitor = GetListCompetitor();

                foreach (string Competitor in AllCompetitor)
                {
                    //Get Name Competitor
                    string nameCompetitor = GetNameCompetitor(Competitor);
                    string urlFileOld = String.Format(@"..\..\Compare\Old\{0}.txt", nameCompetitor);
                    string urlFileNew = String.Format(@"..\..\Compare\New\{0}.txt", nameCompetitor);
                    string urlResult = String.Format(@"..\..\Compare\Result\{0}.txt", nameCompetitor);
                    logsb.AppendLine("Checking : " + nameCompetitor);
                    log.Debug("Checking : " + nameCompetitor);

                    // Get HTML
                    string content = GetHTMLfromCompetitor(Competitor);
                    if (String.IsNullOrEmpty(content))
                    {
                        logsb.AppendLine("Error Read HTML : " + nameCompetitor);
                        logsb.AppendLine("-------------------------------");
                        log.Error("Error Read HTML : " + nameCompetitor);
                        log.Info("-------------------------------");
                        continue;
                    }
                    //content = UnicodeToUTF8(content);
                    //Check file exist
                    if (!File.Exists(urlFileOld))
                    {
                        // if not exist -> save file
                        StreamWriter file = new System.IO.StreamWriter(urlFileOld);
                        file.WriteLine(content);
                        file.Close();
                    }
                    else
                    {
                        // if file exist -> compare old vs new 
                        StreamWriter file = new System.IO.StreamWriter(urlFileNew);
                        file.WriteLine(content, true);
                        file.Close();

                        //Read 2 file 
                        String[] oldCompetitor = File.ReadAllLines(urlFileOld);
                        String[] newCompetitor = File.ReadAllLines(urlFileNew);
                        logsb.AppendLine("Save : " + nameCompetitor);
                        log.Debug("Save : " + nameCompetitor);

                        //Compare 2 file
                        IEnumerable<String> CompareResult = newCompetitor.Except(oldCompetitor);
                        //Write Result File
                        File.WriteAllLines(urlResult, CompareResult);

                        File.Delete(Path.Combine(urlFileOld));
                        File.Move(urlFileNew, urlFileOld);

                        //Get Result Compare
                        String[] ResultCompare = File.ReadAllLines(urlResult);
                        if (ResultCompare.Length > 0)
                        {
                            logsb.AppendLine("Push Notification to ChatWork : " + nameCompetitor);
                            log.Info("Push Notification to ChatWork : " + nameCompetitor);
                            Task<string> response = PostChatwork(
                                ConfigurationManager.AppSettings["TokenAPI"],
                                ConfigurationManager.AppSettings["ChatRoomID"],
                                string.Join("\n", ResultCompare),
                                Competitor);
                            string resAPI = response.Result.ToString();
                            if (resAPI.Contains("errors"))
                            {
                                logsb.AppendLine("Push Notification FALSE");
                                log.Error("Push Notification FALSE");
                            }
                        }
                        else
                        {
                            logsb.AppendLine("Nothing Changed : " + nameCompetitor);
                            log.Info("Nothing Changed :" + nameCompetitor);
                        }
                    }
                    logsb.AppendLine("-------------------------------");
                    log.Info("-------------------------------");
                }
            }
            catch (Exception ex)
            {
                logsb.AppendLine("Error Checking");
                logsb.AppendLine(ex.ToString());
                logsb.AppendLine("-------------------------------");

                log.Error("Error Checking");
                log.Error(ex.ToString());
                log.Error("-------------------------------");
                return;
            }
            finally
            {
                logsb.AppendLine(String.Format("[{0}] END...", DateTime.Now.ToString()));
                logsb.AppendLine("===================================");
                log.Info(String.Format("[{0}] END...", DateTime.Now.ToString()));

                StreamWriter file = new System.IO.StreamWriter(@"..\..\Compare\logHistory.txt", true);
                file.WriteLine(logsb.ToString());
                file.Close();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strFrom"></param>
        /// <returns></returns>
        private static string UnicodeToUTF8(string strFrom)
        {
            byte[] bytSrc;
            byte[] bytDestination;
            string strTo = String.Empty;

            bytSrc = Encoding.Unicode.GetBytes(strFrom);
            bytDestination = Encoding.Convert(Encoding.Unicode, Encoding.ASCII, bytSrc);
            strTo = Encoding.ASCII.GetString(bytDestination);

            return strTo;

        }

        /// <summary>
        /// Get HTML from Competitor
        /// </summary>
        /// <returns></returns>
        private static string GetHTMLfromCompetitor(string CompetitorURL)
        {
            try
            {
                string htmlOutput;
                string content = "";
                HtmlWeb web = new HtmlWeb();
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                HtmlAgilityPack.HtmlDocument doc = web.Load(CompetitorURL);

                switch (CompetitorURL)
                {
                    //Get NameCheap HTML (Namecheap can't get html by normal way)
                    case "https://www.namecheap.com/":
                        using (var client = new System.Net.WebClient())
                        {
                            var filename = System.IO.Path.GetTempFileName();
                            client.DownloadFile("https://www.namecheap.com/", @"..\..\Compare\New\namecheap.com.txt");
                        }
                        break;
                    //Remove tag comment from canhme.com
                    case "https://canhme.com/":
                        while (doc.DocumentNode.SelectNodes("//div[contains(@class, 'entry-meta')]") != null)
                        {
                            doc.DocumentNode.SelectNodes("//div[contains(@class, 'entry-meta')]")[0].Remove();
                        }
                        while (doc.DocumentNode.SelectNodes("//aside[contains(@class, 'widget widget-canhme-recent-comment posts-thumbnail-widget')]") != null)
                        {
                            doc.DocumentNode.SelectNodes("//aside[contains(@class, 'widget widget-canhme-recent-comment posts-thumbnail-widget')]")[0].Remove();
                        }
                        break;
                    //Remove tag twitter from ranmode.com
                    case "http://www.ramnode.com/":
                        while (doc.DocumentNode.SelectNodes("//div[contains(@id, 'twitterdiv')]") != null)
                        {
                            doc.DocumentNode.SelectNodes("//div[contains(@id, 'twitterdiv')]")[0].Remove();
                        }
                        break;
                }
                //Remove tag from digistar.vn -> tag allway change
                if (CompetitorURL.Contains("www.digistar.vn"))
                {
                    while (doc.DocumentNode.SelectNodes("//div[contains(@class, 'g1-grid')]") != null)
                    {
                        doc.DocumentNode.SelectNodes("//div[contains(@class, 'g1-grid')]")[0].Remove();
                    }
                    while (doc.DocumentNode.SelectNodes("//div[contains(@class, 'g1-layout-inner g1-layout-inner-custom')]") != null)
                    {
                        doc.DocumentNode.SelectNodes("//div[contains(@class, 'g1-layout-inner g1-layout-inner-custom')]")[0].Remove();
                    }
                    while (doc.DocumentNode.SelectNodes("//footer[contains(@id, 'g1-footer')]") != null)
                    {
                        doc.DocumentNode.SelectNodes("//footer[contains(@id, 'g1-footer')]")[0].Remove();
                    }
                    while (doc.DocumentNode.SelectNodes("//div[contains(@class, 'checkout-wrap')]") != null)
                    {
                        doc.DocumentNode.SelectNodes("//div[contains(@class, 'checkout-wrap')]")[0].Remove();
                    }
                }

                //Remove tag blog from digistar.vn -> tag allway change
                if (CompetitorURL.Contains("hostingviet.vn"))
                {
                    while (doc.DocumentNode.SelectNodes("//div[contains(@class, 'support-reviews')]") != null)
                    {
                        doc.DocumentNode.SelectNodes("//div[contains(@class, 'support-reviews')]")[0].Remove();
                    }
                    while (doc.DocumentNode.SelectNodes("//div[contains(@class, 'top')]") != null)
                    {
                        doc.DocumentNode.SelectNodes("//div[contains(@class, 'top')]")[0].Remove();
                    }
                    while (doc.DocumentNode.SelectNodes("//a[contains(@class, 'dbtn btn-registernow')]") != null)
                    {
                        doc.DocumentNode.SelectNodes("//a[contains(@class, 'dbtn btn-registernow')]")[0].Remove();
                    }
                    while (doc.DocumentNode.SelectNodes("//div[contains(@class, 'box-search')]") != null)
                    {
                        doc.DocumentNode.SelectNodes("//div[contains(@class, 'box-search')]")[0].Remove();
                    }
                    while (doc.DocumentNode.SelectNodes("//div[contains(@class, 'footer-content')]") != null)
                    {
                        doc.DocumentNode.SelectNodes("//div[contains(@class, 'footer-content')]")[0].Remove();
                    }
                    while (doc.DocumentNode.SelectNodes("//nav[contains(@class, 'navbar navbar-default')]") != null)
                    {
                        doc.DocumentNode.SelectNodes("//nav[contains(@class, 'navbar navbar-default')]")[0].Remove();
                    }
                }

                if (!CompetitorURL.Contains("www.namecheap.com"))
                {
                    htmlOutput = doc.DocumentNode.OuterHtml;
                }
                else
                {
                    String[] namecheap = File.ReadAllLines(@"..\..\Compare\New\namecheap.com.txt");
                    htmlOutput = String.Join("\n", namecheap);
                }

                //Remove tag
                Regex rgx = new Regex(@"<head>*</head>");
                content = rgx.Replace(htmlOutput, " ");

                rgx = new Regex(@"<script[^>]*>[\s\S]*?</script>");
                content = rgx.Replace(content, " ");

                rgx = new Regex(@"<style[^>]*>[\s\S]*?</style>");
                content = rgx.Replace(content, " ");

                content = StripTagsCharArray(content).Replace("\r", "\n").Replace("\t", "\n");

                while (content.Contains("\n\n"))
                {
                    content = content.Replace("\n\n", "\n");
                }
                return content;
            }
            catch (Exception ex)
            {
                return null;
            }

        }

        /// <summary>
        /// Remove HTML tags from string using char array.
        /// </summary>
        private static string StripTagsCharArray(string source)
        {
            char[] array = new char[source.Length];
            int arrayIndex = 0;
            bool inside = false;

            for (int i = 0; i < source.Length; i++)
            {
                char let = source[i];
                if (let == '<')
                {
                    inside = true;
                    continue;
                }
                if (let == '>')
                {
                    inside = false;
                    continue;
                }
                if (!inside)
                {
                    array[arrayIndex] = let;
                    arrayIndex++;
                }
            }
            return new string(array, 0, arrayIndex);
        }

        /// <summary>
        /// Post Message To ChatWork
        /// </summary>
        /// <param name="apiToken"></param>
        /// <param name="roomID"></param>
        /// <param name="message"></param>
        /// <param name="title"></param>
        /// <returns></returns>
        public static async Task<string> PostChatwork(string apiToken, string roomID, string message, string title)
        {
            string url = String.Format(ConfigurationManager.AppSettings["ChatworkURL"], roomID);
            var client = new RestClient(url);

            var request = new RestRequest(Method.POST);

            request.AddHeader("X-ChatWorkToken", apiToken);

            request.AddParameter("body", "[info][title]Competitor’s Updatesが更新されました[/title] " + title + " \n " + message + "[/info]");

            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
            client.ExecuteAsync(request, response =>
            {
                tcs.SetResult(response.Content.ToString());
            });
            return tcs.Task.Result;
        }

        /// <summary>
        /// Get All Competitor from resource
        /// </summary>
        /// <returns></returns>
        static List<string> GetListCompetitor()
        {
            List<String> AllCompetitor = new List<String>();
            String[] listCompetitor = File.ReadAllLines(@"..\..\Competitor\ListCompetitor.txt");
            foreach (String competitor in listCompetitor)
            {
                AllCompetitor.Add(competitor);
            }
            return AllCompetitor;
        }

        /// <summary>
        /// Get Name Competitor
        /// </summary>
        /// <param name="LinkCompetitor"></param>
        /// <returns></returns>
        static String GetNameCompetitor(string LinkCompetitor)
        {
            LinkCompetitor = LinkCompetitor.Replace("http://", String.Empty);
            LinkCompetitor = LinkCompetitor.Replace("https://", String.Empty);
            LinkCompetitor = LinkCompetitor.Replace("/", String.Empty);
            LinkCompetitor = LinkCompetitor.Replace("?", String.Empty);
            return LinkCompetitor;
        }

    }
}
