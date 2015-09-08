using Dalian.Core.Helpers;
using Dalian.Models;
using Nancy;
using Nancy.ModelBinding;
using NPoco;
using System;
using System.Configuration;

namespace Dalian.Modules
{
    public class IndexModule : NancyModule
    {
        public IndexModule()
        {
            Get["/"] = _ =>
            {
                return View["index"];
            };

            Get["/sites"] = _ =>
            {
                return View["sites"];
            };

            Post["/sites"] = parameters =>
            {
                Sites site = this.Bind<Sites>();
                site.SiteId = ShortGuid.NewGuid().ToString();
                site.Active = true;
                site.DateTime = DateTime.UtcNow;

                IDatabase db = new Database(ConfigurationManager.ConnectionStrings["db"].Name);
                db.Insert(site);
                db.Dispose();


                return View["sites"];
            };
        }
    }
}