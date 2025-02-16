using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NoFrillsTransformation.Interfaces;

namespace NoFrillsTransformation.Plugins.Csv
{
    public class CsvSortingWriterPlugin : ConfigurableBase, ITargetWriter
    {
        public CsvSortingWriterPlugin(IContext context, string target, string[] fieldNames, int[] fieldSizes, string? config)
        {
            _csvWriter = new CsvWriterPlugin(context, target, fieldNames, fieldSizes, config);
            _fieldNames = fieldNames;
            ReadConfig(config);
        }

        protected override void SetConfig(string parameter, string configuration)
        {
            switch (parameter)
            {
                case "sort":
                    // we expect this to be a comma-separated list of field names
                    _sortFields = configuration.Split(',');
                    // Validate that all fields are in the field names
                    foreach (var sortField in _sortFields)
                    {
                        if (!_fieldNames.Contains(sortField))
                            throw new ArgumentException("Sort field '" + sortField + "' is not a valid field name.");
                    }
                    break;
            }
        }

        private string[]? _sortFields;

        private CsvWriterPlugin _csvWriter;

        private string[] _fieldNames;
        private List<string[]> _records = new List<string[]>();
        private int _recordsWritten = 0;
        
        public void WriteRecord(string[] fieldValues)
        {
            // The engine reuses the same array all over again, so we need to clone it.
            _records.Add((string[])fieldValues.Clone());
            _recordsWritten++;
        }
        public int RecordsWritten
        {
            get
            {
                return _recordsWritten;
            }
        }

        public void FinishWrite()
        {
            // Calculate field indexes once
            int[] sortFieldIndexes = _sortFields?
                .Select(sortField => Array.IndexOf(_fieldNames, sortField))
                .ToArray() ?? Array.Empty<int>();

            Comparison<string[]> comparison = (a, b) =>
            {
                for (int i = 0; i < sortFieldIndexes.Length; ++i)
                {
                    int fieldIndex = sortFieldIndexes[i];
                    if (fieldIndex < 0)
                    {
                        return 0;
                    }
                    int result = string.Compare(a[fieldIndex], b[fieldIndex]);
                    if (result != 0)
                    {
                        return result;
                    }
                }
                return 0;
            };
            // Now sort the _records
            _records.Sort(comparison);

            // ... and delegate to the _csvWriter
            foreach (var record in _records)
            {
                _csvWriter.WriteRecord(record);
            }
        }

        public void Dispose()
        {
            // Ändern Sie diesen Code nicht. Fügen Sie Bereinigungscode in der Methode "Dispose(bool disposing)" ein.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                if (null != _csvWriter)
                {
                    _csvWriter.Dispose();
                }
            }
        }
    }
}