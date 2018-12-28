using DataModels;
using HtmlAgilityPack;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Candler.Properties;
using Serilog;

namespace Candler
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.RollingFile("log-{Date}.txt")
                .CreateLogger();

            // NodeBB Write API (plugin)
            // https://github.com/NodeBB/nodebb-plugin-write-api
            var client = new RestClient(new Uri(Settings.Default.BaseUrl, "/api/v2"));
            client.AddDefaultHeader("Authorization", $"Bearer {Settings.Default.BearerToken}");

            // keep content but strip unknown tags
            var config = new ReverseMarkdown.Config(ReverseMarkdown.Config.UnknownTagsOption.Bypass);
            // new HTML-to-Markdown converter
            var converter = new ReverseMarkdown.Converter(config);

            // stores tag to category association
            var tagToCategoryMap = new Dictionary<uint, int>();

            // connect to database
            using (var db = new FlarumDB())
            {
                // enumerate Flarum tags to convert them into NodeBB categories
                foreach (var tag in db.Tags.ToList())
                {
                    var request = new RestRequest("/categories", Method.POST);
                    request.AddParameter("_uid", Settings.Default.UserId);
                    request.AddParameter("parentCid", Settings.Default.ParentCid);
                    request.AddParameter("name", tag.Name);
                    request.AddParameter("description", "Imported from Flarum");

                    var response = client.Execute(request);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        Log.Fatal("Failed to create new category: {Message}", response.Content);
                        return;
                    }

                    dynamic content = JsonConvert.DeserializeObject(response.Content);

                    // maps Flarum tag ID to NodeBB category ID
                    tagToCategoryMap.Add(tag.Id, (int)content.payload.cid);

                    Log.Information("Mapped tag {@Tag} to category {@Category}",
                        tag, content);
                }

                // grab all discussions
                foreach (var discussion in db.Discussions
                    .OrderBy(d => d.StartTime)
                    .ToList())
                {
                    var topicId = -1;
                    var title = discussion.Title;

                    Log.Information("Importing discussion {Title}", title);

                    var tagId = db.DiscussionsTags.First(dt => dt.DiscussionId == discussion.Id).TagId;
                    var categoryId = tagToCategoryMap[tagId];

                    Log.Information("Discussion {@Discussion} will be imported in category {Category}",
                        discussion, categoryId);

                    // enumerate linked posts, sorted by publishing date
                    foreach (var post in db.Posts.Where(
                        p => p.DiscussionId == discussion.Id
                        && !p.HideTime.HasValue)
                        .OrderBy(i => i.Time)
                        .ToList())
                    {
                        var owner = db.Users.First(u => u.Id == post.UserId);
                        var username = owner.Username;

                        Log.Information("Importing post {@Post}", post);

                        // perform basic content transformation
                        var markdown = converter.Convert(post.Content);
                        // try to un-fuck code segments
                        var decoded = HtmlEntity.DeEntitize(markdown);

                        var body = $"*Originally posted by* **{username}**\n\n---\n\n {decoded}";

                        // new topic, create
                        if (topicId == -1)
                        {
                            var request = new RestRequest("/topics", Method.POST);
                            request.AddParameter("_uid", Settings.Default.UserId);
                            request.AddParameter("cid", categoryId);
                            request.AddParameter("title", title);
                            request.AddParameter("content", body);

                            var response = client.Execute(request);

                            if (response.StatusCode != HttpStatusCode.OK)
                            {
                                throw new Exception(response.Content);
                            }

                            dynamic content = JsonConvert.DeserializeObject(response.Content);

                            topicId = content.payload.topicData.tid;
                        }
                        else // existing topic, add post
                        {
                            var request = new RestRequest($"/topics/{topicId}", Method.POST);
                            request.AddParameter("_uid", Settings.Default.UserId);
                            request.AddParameter("content", body);

                            var response = client.Execute(request);

                            if (response.StatusCode != HttpStatusCode.OK)
                            {
                                throw new Exception(response.Content);
                            }

                            dynamic content = JsonConvert.DeserializeObject(response.Content);
                        }
                    }
                }
            }
        }
    }
}
