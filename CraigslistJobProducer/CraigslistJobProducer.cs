﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using System.Web;
using CraigslistJobApplier.Entities;

namespace CraigslistProducer
{
    class CraigslistJobProducer
    {
        private readonly String _craigslistUrl;
        private readonly String _craigslistLocation;

        public CraigslistJobProducer(String CraigslistUrl, String CraigslistLocation)
        {
            _craigslistUrl = CraigslistUrl;
            _craigslistLocation = CraigslistLocation;
        }

        public void QueueEmails()
        {
            var jobUrls = ExtractJobUrls();

            var replyUrls = ExtractReplyUrls(jobUrls);

            var emailsAndSubjects = ExtractEmailsAndSubjects(replyUrls);

            // add emails to database
            using (var context = new devEntities())
            {
                int emailsQueued = 0;

                foreach(var emailAndSubject in emailsAndSubjects)
                {
                    //if the email isn't already queued
                    if (context.Emails.Where(x => x.Email1 == emailAndSubject.Item1).Count() == 0)
                    {
                        context.Emails.Add(new Email()
                        {
                            Email1 = emailAndSubject.Item1,
                            MessageSubject = emailAndSubject.Item2,
                            Location = _craigslistLocation,
                            HasBeenSent = false
                        });

                        context.SaveChanges();
                        emailsQueued++;
                    }
                }
                Console.WriteLine(String.Format("Queued {0} emails for {1}", emailsQueued, _craigslistLocation));
            }
        }

        private List<Tuple<String, String>> ExtractEmailsAndSubjects(List<String> replyUrls)
        {
            var emailsAndSubjects = new List<Tuple<String, String>>();

            var page = new HtmlWeb();

            foreach(var replyUrl in replyUrls)
            {
                var doc = page.Load(replyUrl);
                var link = doc.DocumentNode.SelectSingleNode("//a[@class='mailapp']");
                var title = doc.DocumentNode.SelectSingleNode("//title");
        
                if (link != null)
                {
                    var email = link.InnerHtml;

                    var subject = title.InnerHtml;

                    subject = Regex.Match(subject, @"(?<= - ).+").ToString();

                    emailsAndSubjects.Add(new Tuple<String, String>(email, subject));
                }
            }

            return emailsAndSubjects;
        }

        private List<String> ExtractReplyUrls(List<String> jobUrls)
        {
            var replyUrls = new List<String>();

            var page = new HtmlWeb();

            foreach(var jobUrl in jobUrls)
            {
                var doc = page.Load(jobUrl);

                var link = doc.DocumentNode.SelectSingleNode("//a[@id='replylink']");

                // postings may not have email as a reply option. ignore these
                if (link != null)
                {
                    var replyPath = link.GetAttributeValue("href", null);

                    replyUrls.Add(_craigslistUrl + replyPath);
                }
            }

            return replyUrls;
        }

        private List<String> ExtractJobUrls()
        {
            var page = new HtmlWeb();
            var doc = page.Load(_craigslistUrl + "/search/sof");

            var jobUrls = new List<String>();

            foreach (var link in doc.DocumentNode.SelectNodes("//a[@class='hdrlnk']"))
            {
                var jobPath = link.GetAttributeValue("href", null);

                // Craigslist sometimes returns results for nearby areas.
                // these links will be absolute URLs, so they can be filtered out by looking for "craigslist"
                if(!jobPath.Contains("craigslist"))
                    jobUrls.Add(_craigslistUrl + jobPath);
            }

            return jobUrls;
        }
    }
}
