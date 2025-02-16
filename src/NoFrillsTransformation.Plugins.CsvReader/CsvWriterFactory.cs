using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using NoFrillsTransformation.Interfaces;

namespace NoFrillsTransformation.Plugins.Csv
{
    [Export(typeof(NoFrillsTransformation.Interfaces.ITargetWriterFactory))]
    public class CsvWriterFactory : ITargetWriterFactory
    {
        public bool CanWriteTarget(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                return false;
            string temp = target.ToLowerInvariant();
            if (!temp.StartsWith("file://"))
                return false;
            if (!temp.EndsWith(".csv") && !temp.EndsWith(".txt"))
                return false;
            return true;
        }

        public ITargetWriter CreateWriter(IContext context, string target, IFieldDefinition[] fieldDefs, string? config)
        {
            context.Logger.Info("CsvWriterFactory: Creating a CsvWriterPlugin.");
            if (config != null && config.Contains("sort="))
                return new CsvSortingWriterPlugin(context, target, GetFieldNames(fieldDefs), GetFieldSizes(fieldDefs), config);
            return new CsvWriterPlugin(context, target, GetFieldNames(fieldDefs), GetFieldSizes(fieldDefs), config);
        }

        private static string[] GetFieldNames(IFieldDefinition[] fieldDefs)
        {
            return fieldDefs.Select(def => def.FieldName).ToArray();
        }

        private static int[] GetFieldSizes(IFieldDefinition[] fieldDefs)
        {
            return fieldDefs.Select(def => def.FieldSize).ToArray();
        }
    }
}
