using NPoco;
using System;

namespace Dalian.Models
{
    [PrimaryKey("SiteId", AutoIncrement = false)]
    public class Sites
    {
        public string SiteId { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string Note { get; set; }
        public string Source { get; set; }
        public DateTime DateTime { get; set; }
        public bool Active { get; set; }
        public string MetaTitle { get; set; }
        public string MetaDescription { get; set; }
        public string MetaKeywords { get; set; }
        public int Status { get; set; }
        public bool Bookmarklet { get; set; }
        public bool ReadItLater { get; set; }
        public bool Clipped { get; set; }
        public string ArchiveUrl { get; set; }
        public bool Highlight { get; set; }
        public bool PersonalHighlight { get; set; }
    }

    public class SitesMeta
    {
        public string MetaTitle { get; set; }
        public string MetaDescription { get; set; }
        public string MetaKeywords { get; set; }
    }
}