using System.Xml.Serialization;

namespace NoFrillsTransformation.Plugins.Sql
{
    [XmlRoot("config")]
    public class SqlEntityConfig
    {
        // Multiple entries of <col name="FieldName" type="FieldType" />
        // The surrounding <table> also has a name attribute: <table name="TableName">
        [XmlElement("table")]
        public SqlTableConfig? Table { get; set; }
    }

    public class SqlTableConfig
    {
        [XmlAttribute("name")]
        public string? Name { get; set; }

        [XmlElement("col")]
        public SqlEntityColumnConfig[]? Columns { get; set; }
    }

    public class SqlEntityColumnConfig
    {
        [XmlAttribute("name")]
        public string? Name { get; set; }
        [XmlAttribute("type")]
        public string? Type { get; set; }
        [XmlAttribute("default")]
        public string? Default { get; set; }
    }
}
