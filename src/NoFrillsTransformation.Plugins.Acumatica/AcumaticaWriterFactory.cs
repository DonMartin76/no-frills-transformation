using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using NoFrillsTransformation.Interfaces;

namespace NoFrillsTransformation.Plugins.Acumatica
{
    [Export(typeof(NoFrillsTransformation.Interfaces.ITargetWriterFactory))]
    public class AcumaticaWriterFactory : ITargetWriterFactory
    {
        public bool CanWriteTarget(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                return false;
            string temp = target.ToLowerInvariant();
            if (!temp.StartsWith("acumatica.xml://") && !temp.StartsWith("file://"))
                return false;
            if (!temp.EndsWith(".xml"))
                return false;
            return true;
        }

        public ITargetWriter CreateWriter(IContext context, string target, IFieldDefinition[] fieldDefs, string? config)
        {
            context.Logger.Info("AcumaticaWriterFactory: Creating an XmlWriterPlugin.");
            return new AcumaticaWriter(context, target, GetFieldNames(fieldDefs), GetFieldSizes(fieldDefs), config);
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
