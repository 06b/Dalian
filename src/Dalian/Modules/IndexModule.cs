﻿using BookmarksManager;
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

            // Handle sites that might not exist anymore
            if (document.DocumentNode.FirstChild != null)
            {

                //Get the Title of the website
                meta.MetaTitle = document.DocumentNode.SelectSingleNode("//title").InnerText;

                //Get MetaTags of the website
                var metaTags = document.DocumentNode.SelectNodes("//meta");

                if (metaTags != null)
                {
                    foreach (var tag in metaTags)
                    {
                        //Check if Meta Description exists
                        if (tag.Attributes["name"] != null && tag.Attributes["content"] != null && tag.Attributes["name"].Value == "description")
                        {
                            meta.MetaDescription = tag.Attributes["content"].Value;
                        }

                        //Check if Meta Keywords exist
                        if (tag.Attributes["name"] != null && tag.Attributes["content"] != null && tag.Attributes["name"].Value == "keywords")
                        {
                            meta.MetaKeywords = tag.Attributes["content"].Value;
                        }
                    }
                }

            }

            return meta;
        }

        /// <summary>
        /// Checks if bookmark already exists.
        /// </summary>
        /// <param name="Url">The URL.</param>
        public static bool CheckIfBookmarkAlreadyExists(string Url)
        {

            if (Url.EndsWith("/", StringComparison.Ordinal))
            {
                //If the Url ends with a slash remove it
                Url = Url.Substring(0, Url.Length - 1);
            }

            if (Url.StartsWith("http://", StringComparison.Ordinal))
            {
                //If Url starts with Http - Remove it
                Url = Url.Substring(7);
            }
            else
            {
                //If Url starts with Https - Remove it
                Url = Url.Substring(8);
            }

            var BookmarkWithHttpsWithoutSlash = "https://" + Url;
            var BookmarkWithHttpsWithSlash = "https://" + Url + "/";
            var BookmarkWithHttpWithoutSlash = "http://" + Url;
            var BookmarkWithHttpWithSlash = "http://" + Url + "/";

            IDatabase db = new Database(ConfigurationManager.ConnectionStrings["db"].Name);

            //Check if the current requested URL already exists in the database with multiple combinations
            var Bookmark = db.FetchBy<Sites>(sql => sql.Where(x => x.Url == BookmarkWithHttpsWithoutSlash || x.Url == BookmarkWithHttpsWithSlash || x.Url == BookmarkWithHttpWithoutSlash || x.Url == BookmarkWithHttpWithSlash)).ToList();
            db.Dispose();

            if (Bookmark.Count > 0)
            {
                //Bookmark exists
                return true;
            }

            //Bookmark doesn't exist "in theory"
            return false;

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
                        //Don't create new bookmark if it already exists
                        if (!CheckIfBookmarkAlreadyExists(b.Url))
                        {
                            Debug.WriteLine("Type {0}, Title: {1}", b.GetType().Name, b.Title);
                        }
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

                //Don't create new bookmark if it already exists
                if (!CheckIfBookmarkAlreadyExists(site.Url))
                {

                    site.SiteId = ShortGuid.NewGuid().ToString();
                    site.Active = true;
                    site.DateTime = DateTime.UtcNow;

                    //Get the MetaData if it is a valid URI
                    if (UriChecker.IsValidURI(site.Url))
                    {
                        SitesMeta metadata = GetMetaData(site.Url);

                        site.MetaTitle = metadata.MetaTitle;
                        site.MetaDescription = metadata.MetaDescription;
                        site.MetaKeywords = metadata.MetaKeywords;
                    }

                    db.Insert(site);
                    db.Dispose();

                    //Get the tags for the bookmark and split them to a list
                    string TagsPost = Request.Form.Tags.Value;
                    List<string> Tags = TagsPost.ToLower().Split(',').Select(s => s.Trim()).ToList();

                    //Get all of the existing tags
                    List<Tags> AllTags = db.Fetch<Tags>();
                    db.Dispose();

                    //Get a list of the TagNames from of all the existing tags
                    List<string> AllTagNames = AllTags.Select(tn => tn.TagName).ToList();

                    //Check if the tag for the current bookmark already exists in the list of tags
                    foreach (string tag in Tags)
                    {
                        //Add a new tag since it doesn't exist
                        if (!AllTagNames.Contains(tag))
                        {
                            Tags newTag = new Tags();

                            newTag.TagId = ShortGuid.NewGuid().ToString();
                            newTag.TagName = tag;
                            newTag.Active = true;

                            db.Insert(newTag);

                            //Link the tag with the site
                            SiteTags newSiteTag = new SiteTags();
                            newSiteTag.SiteId = site.SiteId;
                            newSiteTag.TagId = newTag.TagId;

                            db.Insert(newSiteTag);

                            db.Dispose();
                        }
                        else
                        {
                            //Since the tag already exists, just link the tag with the site
                            var ExistingTag = db.FetchBy<Tags>(sql => sql.Where(x => x.TagName == tag)).FirstOrDefault();

                            SiteTags newSiteTag = new SiteTags();
                            newSiteTag.SiteId = site.SiteId;
                            newSiteTag.TagId = ExistingTag.TagId;

                            db.Insert(newSiteTag);

                            db.Dispose();
                        }
                    }


                    //Redirect to the new site
                    return Context.GetRedirect("~/sites/" + site.SiteId);

                }

                    //TODO: Probably would be better to redirect to the existing page.
                    return Response.AsRedirect("/sites");

            };
        }
    }
}