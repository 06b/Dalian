using BookmarksManager;
using Dalian.Core.Helpers;
using Dalian.Models;
using HtmlAgilityPack;
using Nancy;
using Nancy.Extensions;
using Nancy.ModelBinding;
using NPoco;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Text;

namespace Dalian.Modules
{
    public class IndexModule : NancyModule
    {
        protected dynamic Model = new ExpandoObject();

        /// <summary>
        /// Gets the meta data.
        /// </summary>
        /// <param name="Url">The URL.</param>
        public static SitesMeta GetMetaData(string Url)
        {
            SitesMeta meta = new SitesMeta();

            var webGet = new HtmlWeb();
            var document = new HtmlDocument();
            try
            {
                document = webGet.Load(Url);
            }
            catch (Exception ex)
            {
                if (ex.Message == "The underlying connection was closed: The connection was closed unexpectedly.")
                {
                    //Sometimes the default UserAgent errors out - possibly due to if the site is using some sort of UserAgent Sniffing

                    // Windows ME
                    webGet.UserAgent = "Opera/9.80 (Windows ME; U; Edition Campaign 21; en) Presto/2.6 Version/10.63";
                    document = webGet.Load(Url);

                }
            }

            meta.MetaTitle = document.DocumentNode.SelectSingleNode("//title").InnerText;
            var metaTags = document.DocumentNode.SelectNodes("//meta");

            if (metaTags != null)
            {
                foreach (var tag in metaTags)
                {
                    if (tag.Attributes["name"] != null && tag.Attributes["content"] != null && tag.Attributes["name"].Value == "description")
                    {
                        meta.MetaDescription = tag.Attributes["content"].Value;
                    }

                    if (tag.Attributes["name"] != null && tag.Attributes["content"] != null && tag.Attributes["name"].Value == "keywords")
                    {
                        meta.MetaKeywords = tag.Attributes["content"].Value;
                    }
                }
            }

            return meta;
        }

        public IndexModule()
        {
            IDatabase db = new Database(ConfigurationManager.ConnectionStrings["db"].Name);

            Get["/"] = _ =>
            {
                //TODO: Maybe create some sort of dashboard? Worse case - just redirect to /sites?
                return Response.AsRedirect("/sites");
            };

            /// <summary>
            /// Retrieves all sites.
            /// </summary>
            Get["/sites"] = _ =>
            {
                List<Sites> sites = db.Fetch<Sites>();
                db.Dispose();

                Model = sites;

                return View["index", Model];
            };

            /// <summary>
            /// Retrieves a specific site
            /// </summary>
            Get["/sites/{siteId}"] = parameter =>
            {
                string Id = parameter.siteId;

                var site = db.FetchBy<Sites>(sql => sql.Where(x => x.SiteId == Id));

                List<Tags> AllTags = db.Fetch<Tags>().ToList();
                List<SiteTags> Tags = db.FetchBy<SiteTags>(sql => sql.Where(x => x.SiteId == Id)).ToList();

                List<Tags> tagList = AllTags.Where(allTags => Tags.Select(tags => tags.TagId).Contains(allTags.TagId)).ToList();

                bool firstCSV = true;
                StringBuilder tagsCSV = new StringBuilder();
                foreach (var tag in tagList)
                {
                    if (!firstCSV) { tagsCSV.Append(","); }
                    tagsCSV.Append(tag.TagName);
                    firstCSV = false;
                }

                Model.site = site;
                Model.tags = tagsCSV.ToString();

                db.Dispose();

                return View["update", Model];
            };

            Post["/sites/meta"] = parameter =>
            {

                string requestedUrl = string.Empty;
                if (Request.Form.Values.Count > 0)
                {
                    foreach (var i in Request.Form.Values)
                    {
                        if (!string.IsNullOrWhiteSpace(requestedUrl))
                        {
                            break;
                        }
                        requestedUrl = i;
                    }
                }

                if (UriChecker.IsValidURI(requestedUrl))
                {

                    SitesMeta metadata = GetMetaData(requestedUrl);
                    return Response.AsJson(metadata);
                }
                else
                {
                    return HttpStatusCode.BadRequest;
                }

            };

            /// <summary>
            /// Update a specific site
            /// </summary>
            Put["/sites/{siteId}"] = parameter =>
            {
                string Id = parameter.siteId;

                Sites snapshot = db.FetchBy<Sites>(sql => sql.Where(x => x.SiteId == Id)).FirstOrDefault();

                Sites site = this.Bind<Sites>();

                // Don't clear out fields existing fields.
                site.Active = snapshot.Active;
                site.DateTime = snapshot.DateTime;
                site.MetaTitle = snapshot.MetaTitle;
                site.MetaDescription = snapshot.MetaDescription;
                site.MetaKeywords = snapshot.MetaKeywords;

                db.Update(site);
                db.Dispose();

                string TagsPost = Request.Form.Tags.Value;
                List<string> Tags = TagsPost.ToLower().Split(',').Select(s => s.Trim()).ToList();

                List<Tags> AllTags = db.Fetch<Tags>();
                db.Dispose();

                List<string> AllTagNames = AllTags.Select(tn => tn.TagName).ToList();

                //Delete all existing Tags for current site before linking up any new tags
                db.Delete<SiteTags>("where SiteId = @0", Id);
                db.Dispose();

                foreach (string tag in Tags)
                {
                    if (!AllTagNames.Contains(tag))
                    {
                        Tags newTag = new Tags();

                        newTag.TagId = ShortGuid.NewGuid().ToString();
                        newTag.TagName = tag;
                        newTag.Active = true;

                        db.Insert(newTag);
                        db.Dispose();

                        SiteTags newSiteTag = new SiteTags();
                        newSiteTag.SiteId = site.SiteId;
                        newSiteTag.TagId = newTag.TagId;

                        db.Insert(newSiteTag);

                        db.Dispose();
                    }
                    else
                    {
                        try
                        {
                            var ExistingTag = db.FetchBy<Tags>(sql => sql.Where(x => x.TagName == tag)).FirstOrDefault();

                            SiteTags newSiteTag = new SiteTags();
                            newSiteTag.SiteId = site.SiteId;
                            newSiteTag.TagId = ExistingTag.TagId;

                            db.Insert(newSiteTag);

                            db.Dispose();
                        }
                        catch
                        {
                            //Move along
                        }
                    }
                }



                return Response.AsRedirect("/sites/" + Id);
            };

            /// <summary>
            /// View for adding a new site
            /// </summary>
            Get["/sites/new"] = _ =>
            {
                return View["sites", Model];
            };

            Post["/sites/bulk"] = _ =>
            {

                var file = Request.Files.FirstOrDefault();

                if (file != null)
                {

                    var reader = new NetscapeBookmarksReader();
                    var bookmarks = reader.Read(file.Value);
                    foreach (var b in bookmarks.AllLinks)
                    {
                        Debug.WriteLine("Type {0}, Title: {1}", b.GetType().Name, b.Title);
                    }
                }

                return Response.AsRedirect("/sites");
            };

            /// <summary>
            /// Creates a new site
            /// </summary>
            Post["/sites"] = parameters =>
            {
                Sites site = this.Bind<Sites>();
                site.SiteId = ShortGuid.NewGuid().ToString();
                site.Active = true;
                site.DateTime = DateTime.UtcNow;

                if (UriChecker.IsValidURI(site.Url))
                {
                    SitesMeta metadata = GetMetaData(site.Url);

                    site.MetaTitle = metadata.MetaTitle;
                    site.MetaDescription = metadata.MetaDescription;
                    site.MetaKeywords = metadata.MetaKeywords;
                }

                db.Insert(site);
                db.Dispose();

                string TagsPost = Request.Form.Tags.Value;
                List<string> Tags = TagsPost.ToLower().Split(',').Select(s => s.Trim()).ToList();

                List<Tags> AllTags = db.Fetch<Tags>();
                db.Dispose();

                List<string> AllTagNames = AllTags.Select(tn => tn.TagName).ToList();

                foreach (string tag in Tags)
                {
                    if (!AllTagNames.Contains(tag))
                    {
                        Tags newTag = new Tags();

                        newTag.TagId = ShortGuid.NewGuid().ToString();
                        newTag.TagName = tag;
                        newTag.Active = true;

                        db.Insert(newTag);

                        SiteTags newSiteTag = new SiteTags();
                        newSiteTag.SiteId = site.SiteId;
                        newSiteTag.TagId = newTag.TagId;

                        db.Insert(newSiteTag);

                        db.Dispose();
                    }
                    else
                    {

                        var ExistingTag = db.FetchBy<Tags>(sql => sql.Where(x => x.TagName == tag)).FirstOrDefault();

                        SiteTags newSiteTag = new SiteTags();
                        newSiteTag.SiteId = site.SiteId;
                        newSiteTag.TagId = ExistingTag.TagId;

                        db.Insert(newSiteTag);

                        db.Dispose();
                    }
                }



                return Context.GetRedirect("~/sites/" + site.SiteId);
            };
        }
    }
}