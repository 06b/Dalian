using Dalian.Core.Helpers;
using Dalian.Models;
using Nancy;
using Nancy.Extensions;
using Nancy.ModelBinding;
using NPoco;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Dynamic;
using System.Linq;

namespace Dalian.Modules
{
    public class IndexModule : NancyModule
    {
        protected dynamic Model = new ExpandoObject();

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

                Model = site;

                return View["update", Model];
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

                db.Update(site);
                db.Dispose();

                return Response.AsRedirect("/sites/" + Id);
            };

            /// <summary>
            /// View for adding a new site
            /// </summary>
            Get["/sites/new"] = _ =>
            {
                return View["sites", Model];
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

                db.Insert(site);
                db.Dispose();

                return this.Context.GetRedirect("~/sites/" + site.SiteId);
            };
        }
    }
}