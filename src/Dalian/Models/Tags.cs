using NPoco;

namespace Dalian.Models
{
    [PrimaryKey("TagId", AutoIncrement = false)]
    public class Tags
    {
        public string TagId { get; set; }
        public string TagName { get; set; }
        public bool Active { get; set; }
    }
}