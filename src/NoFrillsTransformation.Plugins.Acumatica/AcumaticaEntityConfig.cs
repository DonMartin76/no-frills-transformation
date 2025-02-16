using System.Xml.Serialization;

namespace NoFrillsTransformation.Plugins.Acumatica
{
    [XmlRoot("config")]
    public class AcumaticaEntityConfig
    {
        // Multiple entries of <col name="FieldName" type="FieldType" default="DefaultValue"/>
        // The surrounding <table> also has a name attribute: <table name="TableName">
        [XmlElement("table")]
        public AcumaticaTableConfig? Table { get; set; }

        [XmlArray("sort")]
        [XmlArrayItem("field")]
        public string[]? SortFields { get; set; }
    }

    public class AcumaticaTableConfig
    {
        [XmlAttribute("name")]
        public string? Name { get; set; }

        [XmlElement("col")]
        public AcumaticaEntityColumnConfig[]? Columns { get; set; }
    }

    public class AcumaticaEntityColumnConfig
    {
        [XmlAttribute("name")]
        public string? Name { get; set; }
        [XmlAttribute("type")]
        public string? Type { get; set; }
        [XmlAttribute("default")]
        public string? Default { get; set; }
        [XmlAttribute("raw-default")]
        public string? RawDefault { get; set; }
    }
}
